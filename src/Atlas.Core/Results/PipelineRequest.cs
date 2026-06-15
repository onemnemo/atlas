using System.Collections.Immutable;
using Atlas.Core.Permissions;
using Atlas.Core.Pipeline;

namespace Atlas.Core.Results;

/// <summary>
/// The public input to the orchestrator: a single unit of work to run through
/// the pipeline (arch §31.1).
/// </summary>
/// <remarks>
/// <para>
/// A request names a <em>task type</em> (by <see cref="TaskId"/>, resolved to a
/// <see cref="Tasks.TaskProfile"/> by the orchestrator) and carries the
/// per-request payload. It deliberately does <em>not</em> carry retrieved
/// content, tool lists, or memory — those are acquired intentionally inside the
/// pipeline, never pushed in from outside (arch §9).
/// </para>
/// <para>
/// The type is immutable and serializable so the public boundary looks identical
/// whether the orchestrator runs in-process or behind an IPC/sidecar transport
/// (arch §31.1). App-specific context (such as which note or selection is
/// active) is passed through <see cref="Metadata"/> as opaque key/value pairs so
/// the core contract does not need to know mnemo's data model.
/// </para>
/// </remarks>
/// <param name="TaskId">The task-type identifier to run (see <see cref="Tasks.TaskIds"/>).</param>
/// <param name="Input">The user's request or the content to act on.</param>
/// <param name="Permissions">
/// The permissions granted for the session this request belongs to. Defaults to
/// read-only; the pipeline must request elevation through the permission gate
/// before acting.
/// </param>
/// <param name="SessionId">
/// Optional identifier tying this request to a conversation/session, used to
/// scope session memory and permission grants.
/// </param>
/// <param name="Metadata">
/// Optional opaque context (e.g. active document id, selection range). Keys are
/// defined by the host application, not by Atlas.Core.
/// </param>
public sealed record PipelineRequest(
    string TaskId,
    string Input,
    PermissionState? Permissions = null,
    string? SessionId = null,
    ImmutableDictionary<string, string>? Metadata = null)
{
    /// <summary>A unique identifier for this request, used for correlation in logs and results.</summary>
    public Guid RequestId { get; init; } = Guid.NewGuid();

    /// <summary>The effective permission state, defaulting to read-only when none was supplied.</summary>
    public PermissionState EffectivePermissions => Permissions ?? PermissionState.ReadOnly;

    // ── Live-feedback callbacks ───────────────────────────────────────────────
    // These are intentionally delegates (not IProgress<T>) so they are invoked
    // directly on the pipeline thread without SynchronizationContext marshalling.
    // Consumers must handle thread-safety themselves.  Both are optional and
    // null-safe: the pipeline never requires them to be set (arch §31.4).

    /// <summary>
    /// Called with each partial token as the model streams its reply.
    /// Only fired during the final generation stage, never during tool calls or
    /// intermediate repairs.
    /// </summary>
    public Action<string>? OnToken { get; init; }

    /// <summary>
    /// Called each time the pipeline transitions to a new activity (routing,
    /// searching, fetching, generating…).  Useful for a live status indicator.
    /// </summary>
    public Action<ActivityEntry>? OnActivity { get; init; }

    // ── Execution flags ───────────────────────────────────────────────────────

    /// <summary>
    /// When true, the pipeline runs the model-directed tool-call loop (arch §12):
    /// the model sees a scoped set of tools, can invoke them one or more times,
    /// and produces its final reply after all tool results have been incorporated.
    /// <para>
    /// When false (the default), the pipeline uses the simpler pre-search
    /// injection strategy: if the internet gate is open it runs a web search
    /// automatically and injects the results as context, without the model
    /// choosing when or whether to search.
    /// </para>
    /// </summary>
    public bool AgentMode { get; init; }
}
