namespace Atlas.Core.Permissions;

/// <summary>
/// Orthogonal capability gates that are not part of the linear
/// <see cref="PermissionLevel"/> ladder (arch §27).
/// </summary>
/// <remarks>
/// These guard access to whole categories of resource that carry their own
/// trust and privacy implications regardless of how "destructive" an action is.
/// They are granted independently and, like all grants, are stored per session
/// and per gate — never per call.
/// </remarks>
[Flags]
public enum ResourceGate
{
    /// <summary>No special resource access.</summary>
    None = 0,

    /// <summary>
    /// Reach the network for gated internet search. Always requires permission
    /// on first use (arch §27). Local-first means this is off by default.
    /// </summary>
    GatedExternal = 1 << 0,

    /// <summary>
    /// Read the user's private/long-term memory (preferences, goals, recurring
    /// topics). Requires permission on first access (arch §27).
    /// </summary>
    PrivateMemory = 1 << 1,
}
