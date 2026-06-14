using Atlas.Core.Budgeting;
using Atlas.Core.Hardware;
using Atlas.Core.Pipeline;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Microsoft.Extensions.Logging;

namespace Atlas.Orchestration;

/// <summary>
/// The state for a single pipeline run, shared across the route and its stages
/// (arch §6, §8).
/// </summary>
/// <remarks>
/// This is the run-scoped sibling of the per-stage <see cref="StageContext"/>:
/// it holds the resolved profile, hardware, permissions, and overall budget for
/// the request, and mints a fresh <see cref="StageContext"/> (optionally with a
/// narrower budget) for each stage so that no stage inherits another's context
/// (arch §8).
/// </remarks>
public sealed class PipelineRunContext
{
    /// <summary>Creates a run context.</summary>
    public PipelineRunContext(
        PipelineRequest request,
        TaskProfile profile,
        HardwareProfile hardware,
        ContextBudget budget,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(logger);

        Request = request;
        Profile = profile;
        Hardware = hardware;
        Budget = budget;
        Logger = logger;
        RunId = request.RequestId;
    }

    /// <summary>The originating request.</summary>
    public PipelineRequest Request { get; }

    /// <summary>The resolved task profile governing the run.</summary>
    public TaskProfile Profile { get; }

    /// <summary>The detected hardware profile.</summary>
    public HardwareProfile Hardware { get; }

    /// <summary>The overall context budget for the run.</summary>
    public ContextBudget Budget { get; }

    /// <summary>The logger for the run.</summary>
    public ILogger Logger { get; }

    /// <summary>The correlation id for the run (equal to the request id).</summary>
    public Guid RunId { get; }

    /// <summary>
    /// Creates a stage context for this run, optionally with a narrower budget for
    /// the stage (defaults to the run budget).
    /// </summary>
    public StageContext CreateStageContext(ContextBudget? stageBudget = null) =>
        new(RunId, Profile, Hardware, Request.EffectivePermissions, stageBudget ?? Budget, Logger);
}
