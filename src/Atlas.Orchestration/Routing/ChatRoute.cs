using Atlas.Core.Budgeting;
using Atlas.Core.Diagnostics;
using Atlas.Core.Inference;
using Atlas.Core.Pipeline;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Orchestration.Stages;
using Microsoft.Extensions.Options;

namespace Atlas.Orchestration.Routing;

/// <summary>
/// The pipeline route for assistant chat responses (arch §5, §21, §24).
/// </summary>
/// <remarks>
/// <para>
/// This route demonstrates the architecture's repair philosophy end-to-end on
/// the simplest generative task. It runs the drafter, and on failure walks the
/// bounded ladder from arch §21: <em>retry → reduce scope → escalate the model →
/// give up honestly</em>. The retry budget comes from the task profile and is
/// itself capped by hardware (arch §23).
/// </para>
/// <para>
/// It never returns a silent failure. A clean draft is a success; a truncated
/// draft is degraded with a warning; an exhausted repair loop escalates to the
/// user with an explanation (arch §26).
/// </para>
/// </remarks>
public sealed class ChatRoute : IPipelineRoute
{
    private const string OutputTypeName = "chat.reply.v1";

    private readonly IPipelineStage<ChatDraftInput, string> _drafter;
    private readonly IModelResolver _modelResolver;
    private readonly IOptions<ChatOptions> _chatOptions;

    /// <summary>Creates the route.</summary>
    public ChatRoute(
        IPipelineStage<ChatDraftInput, string> drafter,
        IModelResolver modelResolver,
        IOptions<ChatOptions> chatOptions)
    {
        ArgumentNullException.ThrowIfNull(drafter);
        ArgumentNullException.ThrowIfNull(modelResolver);
        ArgumentNullException.ThrowIfNull(chatOptions);
        _drafter = drafter;
        _modelResolver = modelResolver;
        _chatOptions = chatOptions;
    }

    /// <inheritdoc />
    public string TaskId => TaskIds.ChatResponse;

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        PipelineRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Warnings from failed-but-recovered attempts are tracked here and only
        // surfaced if the loop ultimately fails. A reply that recovers after a
        // transient retry is a clean success, not a degraded one.
        var failureTrail = new List<AtlasWarning>();
        ContextBudget budget = context.Budget;
        ModelDescriptor? modelOverride = null;
        bool hasEscalated = false;

        // attempt 0 is the initial draft; each further iteration is one repair
        // step, bounded by the profile's retry budget (arch §21).
        for (int attempt = 0; attempt <= context.Profile.MaxRetries; attempt++)
        {
            StageContext stageContext = context.CreateStageContext(budget);
            var input = new ChatDraftInput(
                UserInput: context.Request.Input,
                SystemPrompt: _chatOptions.Value.SystemPrompt,
                ModelOverride: modelOverride,
                MaxOutputTokensOverride: budget.GenerationTokens);

            StageOutcome<string> outcome = await _drafter
                .ExecuteAsync(stageContext, input, cancellationToken)
                .ConfigureAwait(false);

            switch (outcome.Status)
            {
                case OutcomeStatus.Success when outcome.HasValue:
                    return outcome.Warnings.IsDefaultOrEmpty
                        ? PipelineResult.Success(context.RunId, outcome.Value, OutputTypeName)
                        : PipelineResult.Degraded(context.RunId, outcome.Value, OutputTypeName, outcome.Warnings);

                case OutcomeStatus.Degraded when outcome.HasValue:
                    // A truncated-but-usable reply: surface its own warnings.
                    return PipelineResult.Degraded(context.RunId, outcome.Value, OutputTypeName, outcome.Warnings);
            }

            // Failed (or value-less): record why, then plan the next repair step.
            if (!outcome.Warnings.IsDefaultOrEmpty)
            {
                failureTrail.AddRange(outcome.Warnings);
            }

            if (attempt < context.Profile.MaxRetries)
            {
                (budget, modelOverride, hasEscalated) = PlanNextAttempt(context, attempt, budget, modelOverride, hasEscalated);
            }
        }

        // Repair loop exhausted: hand back to the user with an explanation rather
        // than failing silently or fabricating a reply (arch §21, §26).
        failureTrail.Add(AtlasWarning.Error(
            FailureMode.RepairLoopExhausted,
            "Atlas could not produce a reliable reply after several attempts. You can retry or rephrase."));
        return PipelineResult.Escalated(context.RunId, failureTrail, outputType: OutputTypeName);
    }

    private (ContextBudget Budget, ModelDescriptor? ModelOverride, bool HasEscalated) PlanNextAttempt(
        PipelineRunContext context,
        int attempt,
        ContextBudget budget,
        ModelDescriptor? modelOverride,
        bool hasEscalated)
    {
        // Step 1 (attempt 0 -> 1): plain retry; transient backend faults often clear.
        if (attempt == 0)
        {
            return (budget, modelOverride, hasEscalated);
        }

        // Later steps: try escalating the model once, otherwise reduce scope.
        if (!hasEscalated
            && _modelResolver.TryResolveEscalation(ModelRole.MainWorker, context.Hardware, out ModelDescriptor? escalated)
            && escalated is not null)
        {
            return (budget, escalated, true);
        }

        return (ReduceScope(budget), modelOverride, hasEscalated);
    }

    private static ContextBudget ReduceScope(ContextBudget budget) =>
        ContextBudget.Create(Math.Max(256, budget.TotalTokens / 2));
}
