using Atlas.Core.Inference;
using Atlas.Core.Permissions;

namespace Atlas.Core.Tasks;

/// <summary>
/// The first-class, declarative description of how a <em>type</em> of task must
/// be executed (arch §24).
/// </summary>
/// <remarks>
/// <para>
/// Task profiles are the contract that makes hardware-adaptive execution
/// implementable rather than aspirational: "Task profiles must be first-class
/// data structures that every pipeline node reads" (arch §24). A profile
/// describes the <em>requirements and budgets</em> of a task type
/// (autocomplete, chat, learning-path generation, …); it does not describe a
/// single user request. The per-request data travels separately so that one
/// profile can be reused across many requests and, eventually, loaded from
/// configuration rather than code.
/// </para>
/// <para>
/// A profile is a ceiling and a policy, not a script. The effective behaviour is
/// always the profile intersected with the current
/// <see cref="Hardware.HardwareProfile"/> — e.g. a profile asking for
/// <see cref="ParallelismMode.Parallel"/> still runs serially on low-end
/// hardware.
/// </para>
/// </remarks>
/// <param name="TaskId">
/// Stable identifier for this task type (e.g. <c>"chat.response"</c>). Used as a
/// registry key and as a label in logs and telemetry. See <see cref="TaskIds"/>
/// for the well-known values.
/// </param>
/// <param name="LatencyTarget">The acceptable response-time class.</param>
/// <param name="MinModelTier">
/// The minimum model capability required to attempt this task. The resolver
/// will not route the task to a model below this tier.
/// </param>
/// <param name="ContextBudgetTokens">
/// The maximum total token budget for the whole task, divided into slots by the
/// context assembler (arch §8). Must be positive.
/// </param>
/// <param name="RetrievalDepth">The ceiling on the retrieval cascade.</param>
/// <param name="ValidationStrictness">How rigorously outputs are checked.</param>
/// <param name="MaxRetries">
/// The maximum number of repair-loop iterations per stage (arch §21). Must be
/// non-negative.
/// </param>
/// <param name="Parallelism">The requested subagent concurrency.</param>
/// <param name="PermissionLevel">
/// The highest authority this task may exercise. The pipeline must still hold a
/// matching grant in the live <see cref="PermissionState"/> before acting.
/// </param>
/// <param name="CitationRequired">
/// Whether outputs must carry resolvable source references.
/// </param>
/// <param name="Resumable">
/// Whether a run of this task can be paused and resumed (e.g. partial
/// learning-path generation).
/// </param>
/// <param name="BackgroundAllowed">
/// Whether this task may run off the interactive path without blocking the UI.
/// </param>
public sealed record TaskProfile(
    string TaskId,
    LatencyTarget LatencyTarget,
    ModelTier MinModelTier,
    int ContextBudgetTokens,
    RetrievalDepth RetrievalDepth,
    ValidationStrictness ValidationStrictness,
    int MaxRetries,
    ParallelismMode Parallelism,
    PermissionLevel PermissionLevel,
    bool CitationRequired,
    bool Resumable,
    bool BackgroundAllowed)
{
    /// <summary>
    /// Validates the profile's invariants. A malformed profile is a programming
    /// error (it comes from code or configuration, never from a model), so this
    /// throws rather than degrading.
    /// </summary>
    /// <exception cref="ArgumentException">A field is outside its valid range.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskId))
        {
            throw new ArgumentException("TaskProfile.TaskId must be a non-empty identifier.", nameof(TaskId));
        }

        if (ContextBudgetTokens <= 0)
        {
            throw new ArgumentException(
                $"TaskProfile.ContextBudgetTokens must be positive (was {ContextBudgetTokens}).",
                nameof(ContextBudgetTokens));
        }

        if (MaxRetries < 0)
        {
            throw new ArgumentException(
                $"TaskProfile.MaxRetries must be non-negative (was {MaxRetries}).",
                nameof(MaxRetries));
        }
    }
}
