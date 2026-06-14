namespace Atlas.Core.Permissions;

/// <summary>
/// The escalating ladder of authority an action may require (arch §27, §24).
/// </summary>
/// <remarks>
/// <para>
/// The members are strictly ordered from least to most dangerous, so a granted
/// level implicitly authorises everything below it and a required level can be
/// checked with <c>granted &gt;= required</c>. "The assistant must never modify
/// content … without explicit permission grants" (arch §27).
/// </para>
/// <para>
/// This ladder is deliberately one-dimensional. Orthogonal gates that are not
/// simply "more dangerous" — reaching the internet, or reading private memory —
/// are modelled separately as <see cref="ResourceGate"/> values, because being
/// allowed to delete a note does not imply being allowed to search the web.
/// </para>
/// </remarks>
public enum PermissionLevel
{
    /// <summary>Read any content. Never requires confirmation.</summary>
    Read = 0,

    /// <summary>Propose edits as a diff the user approves before anything changes.</summary>
    Suggest = 1,

    /// <summary>Draft new content the user reviews before it is saved.</summary>
    Draft = 2,

    /// <summary>
    /// Apply block-level edits directly. Requires a grant for the current
    /// session (arch §27).
    /// </summary>
    DirectEdit = 3,

    /// <summary>
    /// Bulk changes across documents or deletion of content. Requires explicit
    /// per-operation confirmation and is always confirmed before deletes.
    /// </summary>
    Destructive = 4,
}
