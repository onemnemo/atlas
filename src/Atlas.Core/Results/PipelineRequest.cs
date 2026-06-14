using System.Collections.Immutable;
using Atlas.Core.Permissions;

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
}
