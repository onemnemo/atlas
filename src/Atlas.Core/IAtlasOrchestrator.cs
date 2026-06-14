using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
/// The public entry point to the Atlas AI orchestration system (arch §28, §31.1).
/// </summary>
/// <remarks>
/// <para>
/// This is the entire surface the host application sees: submit a
/// <see cref="PipelineRequest"/>, receive a <see cref="PipelineResult"/>. The
/// orchestrator resolves the request's task profile, routes it through the
/// guarded pipeline graph, and assembles the final result — handling routing,
/// permission gates, tool scoping, retrieval, validation, retries, and
/// degradation internally (arch §28).
/// </para>
/// <para>
/// The interface is deliberately transport-agnostic. It says nothing about
/// whether Atlas runs in-process inside the Avalonia app or behind an IPC
/// boundary as a sidecar; "the public interface looks the same either way — a
/// sidecar simply adds a thin transport layer on top" (arch §31.1). Keeping this
/// surface tiny is what lets that decision be deferred without rework.
/// </para>
/// </remarks>
public interface IAtlasOrchestrator
{
    /// <summary>
    /// Executes a single request through the pipeline and returns its result.
    /// </summary>
    /// <param name="request">The work to perform.</param>
    /// <param name="cancellationToken">
    /// Cancels the run. Cancellation is cooperative: the orchestrator aims to
    /// return a partial, warning-tagged result rather than throwing where it can
    /// (arch §26).
    /// </param>
    /// <returns>
    /// The honest outcome of the run — success, degraded, escalated, or failed —
    /// never a silent failure.
    /// </returns>
    Task<PipelineResult> ExecuteAsync(PipelineRequest request, CancellationToken cancellationToken = default);
}
