using Atlas.Core.Inference;
using Atlas.Core.Permissions;

namespace Atlas.Core.Tools;

/// <summary>
/// The gating context that decides which tools are visible and callable for one
/// step of a pipeline run (arch §11, §12, §27).
/// </summary>
/// <remarks>
/// Scope is the single input to tool exposure. It bundles the session's granted
/// authority, the capability of the model that will choose the tool, and the hard
/// cap on how many tools may be offered at once. Keeping these together means the
/// "what can this step see and do?" question has exactly one answer object.
/// </remarks>
/// <param name="Permissions">The session's granted permission level and resource gates.</param>
/// <param name="ModelCapability">
/// The tier of the model that will select among the offered tools. Risky tools
/// are hidden from models below their <see cref="ToolDescriptor.MinimumModelTier"/>.
/// </param>
/// <param name="MaxToolsPerCall">
/// The most tools to offer in a single scoped selection. Arch §11 targets 4–6;
/// the default is 6.
/// </param>
public sealed record ToolScope(
    PermissionState Permissions,
    ModelTier ModelCapability = ModelTier.Small,
    int MaxToolsPerCall = 6)
{
    /// <summary>A read-only scope for a small model — the safe default.</summary>
    public static ToolScope ReadOnly { get; } = new(PermissionState.ReadOnly);
}
