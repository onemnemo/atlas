using System.Text.Json.Nodes;
using Atlas.Core.Budgeting;
using Atlas.Core.Diagnostics;
using Atlas.Core.Inference;
using Atlas.Core.Pipeline;
using Atlas.Core.Permissions;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Core.Tools;
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
/// When the session has the <see cref="ResourceGate.GatedExternal"/> gate open
/// and an <see cref="IToolGateway"/> is available, the route runs a web-search
/// pre-fetch before the first model call and injects the results into the prompt
/// as grounded context.  Streaming tokens are forwarded via
/// <see cref="PipelineRequest.OnToken"/> when the inference client supports it.
/// Activity events are reported via <see cref="PipelineRequest.OnActivity"/>
/// throughout the route so the UI can display a live status feed (arch §31.4).
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
    private readonly IToolGateway? _toolGateway;

    /// <summary>Creates the route.</summary>
    /// <param name="drafter">The stage that calls the model and assembles a draft.</param>
    /// <param name="modelResolver">Resolves the correct model for each hardware tier.</param>
    /// <param name="chatOptions">Runtime-tunable chat settings.</param>
    /// <param name="toolGateway">
    /// Optional gateway for tool invocations.  When supplied and the session has
    /// the <see cref="ResourceGate.GatedExternal"/> gate open, a web search is run
    /// before generation and its results are injected as context.
    /// </param>
    public ChatRoute(
        IPipelineStage<ChatDraftInput, string> drafter,
        IModelResolver modelResolver,
        IOptions<ChatOptions> chatOptions,
        IToolGateway? toolGateway = null)
    {
        ArgumentNullException.ThrowIfNull(drafter);
        ArgumentNullException.ThrowIfNull(modelResolver);
        ArgumentNullException.ThrowIfNull(chatOptions);
        _drafter = drafter;
        _modelResolver = modelResolver;
        _chatOptions = chatOptions;
        _toolGateway = toolGateway;
    }

    /// <inheritdoc />
    public string TaskId => TaskIds.ChatResponse;

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        PipelineRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        Action<ActivityEntry>? reportActivity = context.Request.OnActivity;

        // ── Optional web-search pre-fetch ─────────────────────────────────────
        // Runs once before the repair loop so retries all benefit from the same
        // retrieved context (no re-fetching on each attempt).
        string? searchContext = null;
        if (_toolGateway is not null
            && context.Request.EffectivePermissions.Allows(ResourceGate.GatedExternal))
        {
            searchContext = await FetchSearchContextAsync(
                context.Request.Input,
                context.Request.EffectivePermissions,
                reportActivity,
                cancellationToken).ConfigureAwait(false);
        }

        // ── Repair loop ───────────────────────────────────────────────────────
        var failureTrail = new List<AtlasWarning>();
        ContextBudget budget = context.Budget;
        ModelDescriptor? modelOverride = null;
        bool hasEscalated = false;

        // attempt 0 is the initial draft; each further iteration is one repair
        // step, bounded by the profile's retry budget (arch §21).
        for (int attempt = 0; attempt <= context.Profile.MaxRetries; attempt++)
        {
            reportActivity?.Invoke(new ActivityEntry(ActivityPhase.Generating, "Generating response…"));

            // A user-configured cap overrides the per-task generation slice; 0 means "auto".
            int generationCap = _chatOptions.Value.MaxOutputTokens > 0
                ? _chatOptions.Value.MaxOutputTokens
                : budget.GenerationTokens;

            // Only stream tokens on the first (non-repair) attempt; repairs just
            // need a complete answer quickly.
            Action<string>? onToken = attempt == 0 ? context.Request.OnToken : null;

            StageContext stageContext = context.CreateStageContext(budget);
            var input = new ChatDraftInput(
                UserInput: BuildUserInput(context.Request.Input, searchContext),
                SystemPrompt: _chatOptions.Value.SystemPrompt,
                ModelOverride: modelOverride,
                MaxOutputTokensOverride: generationCap,
                OnToken: onToken);

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
                    return PipelineResult.Degraded(context.RunId, outcome.Value, OutputTypeName, outcome.Warnings);
            }

            // Failed: record why, then plan the next repair step.
            if (!outcome.Warnings.IsDefaultOrEmpty)
            {
                failureTrail.AddRange(outcome.Warnings);
            }

            if (attempt < context.Profile.MaxRetries)
            {
                (budget, modelOverride, hasEscalated) = PlanNextAttempt(context, attempt, budget, modelOverride, hasEscalated);
            }
        }

        failureTrail.Add(AtlasWarning.Error(
            FailureMode.RepairLoopExhausted,
            "Atlas could not produce a reliable reply after several attempts. You can retry or rephrase."));
        return PipelineResult.Escalated(context.RunId, failureTrail, outputType: OutputTypeName);
    }

    // ── Web search helper ─────────────────────────────────────────────────────

    private async Task<string?> FetchSearchContextAsync(
        string query,
        PermissionState permissions,
        Action<ActivityEntry>? reportActivity,
        CancellationToken cancellationToken)
    {
        string shortQuery = query.Length > 80 ? string.Concat(query.AsSpan(0, 77), "...") : query;
        reportActivity?.Invoke(new ActivityEntry(ActivityPhase.Searching, $"Searching the web for '{shortQuery}'…"));

        var scope = new ToolScope(permissions, ModelTier.Small, MaxToolsPerCall: 1);
        var invocation = new ToolInvocation("web.search", new JsonObject { ["query"] = query });

        ToolResult result = await _toolGateway!
            .InvokeAsync(invocation, scope, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk || string.IsNullOrWhiteSpace(result.Content))
        {
            reportActivity?.Invoke(new ActivityEntry(ActivityPhase.Retrieving, "Web search returned no usable results."));
            return null;
        }

        // Parse hits and report individual URLs as activity entries so the UI
        // can show addresses while the route works (arch §31.4).
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(result.Content) is JsonArray hits)
            {
                reportActivity?.Invoke(new ActivityEntry(
                    ActivityPhase.Retrieving,
                    $"Retrieved {hits.Count} result{(hits.Count == 1 ? "" : "s")}."));

                foreach (JsonNode? hit in hits)
                {
                    string url = hit?["url"]?.GetValue<string>() ?? string.Empty;
                    string title = hit?["title"]?.GetValue<string>() ?? url;
                    if (!string.IsNullOrEmpty(url))
                    {
                        reportActivity?.Invoke(new ActivityEntry(ActivityPhase.Retrieving, title, url));
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Unparseable content from the tool: still pass it to the model as-is.
        }

        return result.Content;
    }

    private static string BuildUserInput(string userInput, string? searchContext)
    {
        if (string.IsNullOrWhiteSpace(searchContext))
        {
            return userInput;
        }

        return
            "[Web search results — use these to ground your answer, cite the URLs]\n"
            + searchContext
            + "\n\n[User question]\n"
            + userInput;
    }

    // ── Repair logic ──────────────────────────────────────────────────────────

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

        // Later steps: try escalating the model once, then reduce scope.
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
