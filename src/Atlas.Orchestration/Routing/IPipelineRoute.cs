using Atlas.Core.Results;

namespace Atlas.Orchestration.Routing;

/// <summary>
/// A complete pipeline route for one task type (arch §5, §6).
/// </summary>
/// <remarks>
/// "The system is shared, but each task travels through a different route"
/// (arch §5). A route composes stages, retrieval, validation, and repair for a
/// specific task type and produces the final <see cref="PipelineResult"/>. New
/// task types are added by registering new routes, never by branching inside a
/// monolithic pipeline.
/// </remarks>
public interface IPipelineRoute
{
    /// <summary>The task id this route handles (matches a <see cref="Core.Tasks.TaskIds"/> value).</summary>
    string TaskId { get; }

    /// <summary>
    /// Executes the route for a prepared run and returns its honest result.
    /// </summary>
    /// <param name="context">The run context (profile, hardware, budget, permissions).</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<PipelineResult> ExecuteAsync(PipelineRunContext context, CancellationToken cancellationToken = default);
}
