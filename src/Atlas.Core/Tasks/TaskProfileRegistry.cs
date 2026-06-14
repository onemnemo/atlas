using System.Diagnostics.CodeAnalysis;

namespace Atlas.Core.Tasks;

/// <summary>
/// An in-memory <see cref="ITaskProfileProvider"/> built from an explicit set of
/// profiles.
/// </summary>
/// <remarks>
/// The registry is immutable once constructed. To change the set of profiles —
/// for example, to load them from configuration — build a new registry. This
/// keeps profile resolution free of locking and guarantees a run sees a stable
/// view of its profiles.
/// </remarks>
public sealed class TaskProfileRegistry : ITaskProfileProvider
{
    private readonly IReadOnlyDictionary<string, TaskProfile> _profiles;

    /// <summary>
    /// Creates a registry from the given profiles, validating each one.
    /// </summary>
    /// <param name="profiles">The profiles to register. Ids must be unique.</param>
    /// <exception cref="ArgumentException">
    /// A profile is invalid, or two profiles share a <see cref="TaskProfile.TaskId"/>.
    /// </exception>
    public TaskProfileRegistry(IEnumerable<TaskProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var map = new Dictionary<string, TaskProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (TaskProfile profile in profiles)
        {
            profile.Validate();
            if (!map.TryAdd(profile.TaskId, profile))
            {
                throw new ArgumentException(
                    $"Duplicate TaskProfile id '{profile.TaskId}'.", nameof(profiles));
            }
        }

        _profiles = map;
    }

    /// <summary>
    /// A registry pre-populated with the built-in defaults (arch §24).
    /// </summary>
    public static TaskProfileRegistry Default { get; } = new(DefaultTaskProfiles.All);

    /// <inheritdoc />
    public IReadOnlyCollection<TaskProfile> All => (IReadOnlyCollection<TaskProfile>)_profiles.Values;

    /// <inheritdoc />
    public bool TryGet(string taskId, [NotNullWhen(true)] out TaskProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        return _profiles.TryGetValue(taskId, out profile);
    }
}
