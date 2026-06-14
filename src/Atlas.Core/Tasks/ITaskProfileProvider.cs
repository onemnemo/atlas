using System.Diagnostics.CodeAnalysis;

namespace Atlas.Core.Tasks;

/// <summary>
/// Supplies the <see cref="TaskProfile"/> for a given task id (arch §24).
/// </summary>
/// <remarks>
/// Pipeline code never hard-codes a profile; it asks the provider. This is the
/// seam that lets profiles move from built-in defaults to user/admin
/// configuration, and lets tests substitute tailored profiles, without changing
/// any pipeline node.
/// </remarks>
public interface ITaskProfileProvider
{
    /// <summary>
    /// Attempts to resolve the profile for <paramref name="taskId"/>.
    /// </summary>
    /// <param name="taskId">The task-type identifier (see <see cref="TaskIds"/>).</param>
    /// <param name="profile">The resolved profile when found.</param>
    /// <returns><see langword="true"/> if a profile is registered for the id.</returns>
    bool TryGet(string taskId, [NotNullWhen(true)] out TaskProfile? profile);

    /// <summary>All profiles known to this provider.</summary>
    IReadOnlyCollection<TaskProfile> All { get; }
}
