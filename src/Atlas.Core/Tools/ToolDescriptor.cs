using System.Collections.Immutable;
using Atlas.Core.Inference;
using Atlas.Core.Permissions;

namespace Atlas.Core.Tools;

/// <summary>
/// The static, transport-agnostic definition of a single tool: what it is, which
/// branch it lives on, what arguments it takes, and what authority it requires
/// (arch §10–§12, §27, §35).
/// </summary>
/// <remarks>
/// <para>
/// A descriptor is pure metadata. It is the unit the scope policy filters and the
/// model sees; the executable behaviour lives behind the gateway. The same
/// descriptor shape describes a built-in local tool and a tool discovered from an
/// external MCP server — only <see cref="Origin"/> differs — so the tree treats
/// both uniformly.
/// </para>
/// <para>
/// Gating fields are intentionally explicit so exposure is decided by data, not
/// code: a tool is shown only when the session permission, resource gate, and
/// model capability all clear its requirements (arch §11, §12, §27).
/// </para>
/// </remarks>
/// <param name="Name">
/// A stable, unique identifier in <c>branch.verb</c> form (e.g. <c>notes.search</c>).
/// This is what a tool call references.
/// </param>
/// <param name="Branch">The capability group this tool belongs to.</param>
/// <param name="Summary">A short, model-facing description of what the tool does.</param>
/// <param name="Parameters">The declared arguments, in display order.</param>
/// <param name="RequiredPermission">
/// The least <see cref="PermissionLevel"/> a session must hold for the tool to be
/// offered or invoked. Defaults to <see cref="PermissionLevel.Read"/>.
/// </param>
/// <param name="RequiredGate">
/// An orthogonal resource gate the session must have opened (e.g. internet).
/// Defaults to <see cref="ResourceGate.None"/>.
/// </param>
/// <param name="MinimumModelTier">
/// The smallest model tier trusted to call this tool correctly. Lets risky tools
/// be hidden from the weakest models (arch §11). Defaults to
/// <see cref="ModelTier.Tiny"/>.
/// </param>
/// <param name="Origin">
/// Where the tool comes from: <c>"local"</c> for built-ins, or an MCP server id.
/// </param>
public sealed record ToolDescriptor(
    string Name,
    ToolBranch Branch,
    string Summary,
    ImmutableArray<ToolParameter> Parameters,
    PermissionLevel RequiredPermission = PermissionLevel.Read,
    ResourceGate RequiredGate = ResourceGate.None,
    ModelTier MinimumModelTier = ModelTier.Tiny,
    string Origin = "local")
{
    /// <summary>The declared parameters, normalised to a non-default empty array.</summary>
    public ImmutableArray<ToolParameter> Parameters { get; init; } =
        Parameters.IsDefault ? ImmutableArray<ToolParameter>.Empty : Parameters;

    /// <summary>
    /// Whether this tool only reads state. Read-only tools are safe to expose
    /// broadly; mutating tools require an elevated grant.
    /// </summary>
    public bool IsReadOnly => RequiredPermission <= PermissionLevel.Read;

    /// <summary>
    /// Returns whether the tool is permitted under <paramref name="scope"/>: the
    /// session must hold the required permission and gate, and the model must be
    /// capable enough.
    /// </summary>
    public bool IsAllowedBy(ToolScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Permissions.Allows(RequiredPermission)
            && scope.Permissions.Allows(RequiredGate)
            && scope.ModelCapability >= MinimumModelTier;
    }
}
