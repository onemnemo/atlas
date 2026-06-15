namespace Atlas.Core.Pipeline;

/// <summary>
/// The phase of a pipeline run that an <see cref="ActivityEntry"/> belongs to.
/// Used by the UI to assign icons and colours to activity items.
/// </summary>
public enum ActivityPhase
{
    /// <summary>The request is being dispatched to the correct route.</summary>
    Routing = 0,

    /// <summary>An external search query is being executed.</summary>
    Searching = 1,

    /// <summary>Individual result documents or URLs are being examined.</summary>
    Retrieving = 2,

    /// <summary>The model is generating a response.</summary>
    Generating = 3,

    /// <summary>A stage output is being validated or repaired.</summary>
    Validating = 4,
}

/// <summary>
/// A single progress event emitted during a pipeline run.
/// </summary>
/// <remarks>
/// Activity entries are reported via <see cref="Results.PipelineRequest.OnActivity"/>
/// and are not stored in the <see cref="Results.PipelineResult"/> — they exist only
/// for real-time UI feedback (arch §31.4).
/// </remarks>
/// <param name="Phase">Which phase of the run this event belongs to.</param>
/// <param name="Message">A short, human-readable description of what is happening.</param>
/// <param name="Detail">Optional additional detail, e.g. a URL being fetched or a model name.</param>
public sealed record ActivityEntry(
    ActivityPhase Phase,
    string Message,
    string? Detail = null);
