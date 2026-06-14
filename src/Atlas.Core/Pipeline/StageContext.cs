using Atlas.Core.Budgeting;
using Atlas.Core.Hardware;
using Atlas.Core.Permissions;
using Atlas.Core.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Core.Pipeline;

/// <summary>
/// The scoped, read-only execution environment handed to a single pipeline stage
/// (arch §6, §8).
/// </summary>
/// <remarks>
/// <para>
/// The context carries the decision inputs a stage needs — the task profile, the
/// detected hardware, the live permission grants, and this stage's own scoped
/// token budget — and nothing else. It does not carry retrieved content or
/// conversation history; a stage acquires those intentionally through the
/// services it depends on (arch §9). Each stage receives its own budget slice so
/// "a drafter agent does not inherit the context of the decomposer" (arch §8).
/// </para>
/// <para>
/// The context is immutable. Sharing the same instance across stages is safe;
/// giving a stage a narrower budget is done by creating a derived context with
/// <see cref="WithBudget"/>.
/// </para>
/// </remarks>
public sealed record StageContext
{
    /// <summary>Creates a stage context.</summary>
    /// <param name="runId">The pipeline run this stage belongs to, for correlation.</param>
    /// <param name="taskProfile">The active task profile every stage reads (arch §24).</param>
    /// <param name="hardware">The detected hardware profile bounding execution strategy.</param>
    /// <param name="permissions">The permissions granted for the session.</param>
    /// <param name="budget">This stage's scoped token budget.</param>
    /// <param name="logger">Logger for this stage; defaults to a no-op logger.</param>
    public StageContext(
        Guid runId,
        TaskProfile taskProfile,
        HardwareProfile hardware,
        PermissionState permissions,
        ContextBudget budget,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(taskProfile);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(budget);

        RunId = runId;
        TaskProfile = taskProfile;
        Hardware = hardware;
        Permissions = permissions;
        Budget = budget;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The id of the overall pipeline run, shared by every stage in it.</summary>
    public Guid RunId { get; }

    /// <summary>The task profile governing this run (arch §24).</summary>
    public TaskProfile TaskProfile { get; }

    /// <summary>The detected hardware profile (arch §23).</summary>
    public HardwareProfile Hardware { get; }

    /// <summary>The permissions granted for the session (arch §27).</summary>
    public PermissionState Permissions { get; }

    /// <summary>This stage's scoped token budget (arch §8).</summary>
    public ContextBudget Budget { get; }

    /// <summary>The logger scoped to this stage.</summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Produces a derived context that narrows the token budget for a sub-stage or
    /// subagent, leaving everything else unchanged.
    /// </summary>
    /// <param name="budget">The narrower budget to apply.</param>
    public StageContext WithBudget(ContextBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        return new StageContext(RunId, TaskProfile, Hardware, Permissions, budget, Logger);
    }
}
