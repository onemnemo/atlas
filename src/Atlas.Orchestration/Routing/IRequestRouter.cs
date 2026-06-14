using Atlas.Core.Results;

namespace Atlas.Orchestration.Routing;

/// <summary>
/// Maps an incoming request to the pipeline route that should handle it
/// (arch §5, §16).
/// </summary>
/// <remarks>
/// <para>
/// This is the architecture's "tool router" seam (arch §16): a classification
/// step that selects a route. In version 1 it is rule-based; later it can be
/// replaced by a fine-tuned <see cref="Core.Inference.ModelRole.Router"/> model
/// that classifies free-form intent — without changing anything downstream,
/// because the contract is just "request in, route key out".
/// </para>
/// </remarks>
public interface IRequestRouter
{
    /// <summary>
    /// Returns the route key (a task id) that should handle
    /// <paramref name="request"/>.
    /// </summary>
    string Route(PipelineRequest request);
}

/// <summary>
/// The default, deterministic router: it trusts the task id the caller supplied
/// on the request.
/// </summary>
/// <remarks>
/// Deterministic code does as much as possible (arch §19): when the host already
/// knows the task type (e.g. the editor invoked autocomplete), there is nothing
/// for a model to classify. A model-based router is only needed for genuinely
/// free-form intent, and replaces this implementation when it exists.
/// </remarks>
public sealed class TaskIdRequestRouter : IRequestRouter
{
    /// <inheritdoc />
    public string Route(PipelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.TaskId;
    }
}
