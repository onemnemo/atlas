namespace Atlas.Core.Permissions;

/// <summary>
/// The set of permissions currently granted within a session (arch §27).
/// </summary>
/// <remarks>
/// <para>
/// "Permission grants are stored per-session and per-action-class, not
/// per-call" (arch §27). An instance of this type is that per-session record.
/// It is immutable: granting a new permission produces a new state via the
/// <c>With…</c> helpers, which keeps the audit trail simple and avoids races on
/// a shared mutable grant bag.
/// </para>
/// <para>
/// This is pure data. The decision of <em>when</em> to ask the user for a grant,
/// and how to surface the request, belongs to the permission gate in the
/// orchestration layer — not here.
/// </para>
/// </remarks>
/// <param name="GrantedLevel">
/// The highest <see cref="PermissionLevel"/> the user has authorised this
/// session. Defaults to <see cref="PermissionLevel.Read"/>, the always-safe floor.
/// </param>
/// <param name="GrantedGates">The orthogonal resource gates the user has opened.</param>
public sealed record PermissionState(
    PermissionLevel GrantedLevel = PermissionLevel.Read,
    ResourceGate GrantedGates = ResourceGate.None)
{
    /// <summary>
    /// A read-only session with no elevated grants — the safe default every
    /// session starts from.
    /// </summary>
    public static PermissionState ReadOnly { get; } = new(PermissionLevel.Read, ResourceGate.None);

    /// <summary>
    /// Returns whether an action requiring <paramref name="required"/> is
    /// currently authorised.
    /// </summary>
    public bool Allows(PermissionLevel required) => GrantedLevel >= required;

    /// <summary>
    /// Returns whether the given orthogonal resource <paramref name="gate"/> is
    /// currently open.
    /// </summary>
    public bool Allows(ResourceGate gate) => (GrantedGates & gate) == gate;

    /// <summary>
    /// Produces a new state with <paramref name="level"/> granted, never lowering
    /// an already-higher grant.
    /// </summary>
    public PermissionState WithLevel(PermissionLevel level) =>
        level > GrantedLevel ? this with { GrantedLevel = level } : this;

    /// <summary>Produces a new state with the given resource <paramref name="gate"/> opened.</summary>
    public PermissionState WithGate(ResourceGate gate) =>
        this with { GrantedGates = GrantedGates | gate };
}
