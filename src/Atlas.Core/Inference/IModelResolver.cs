using Atlas.Core.Hardware;

namespace Atlas.Core.Inference;

/// <summary>
/// Resolves a logical <see cref="ModelRole"/> to a concrete
/// <see cref="ModelDescriptor"/> for the current hardware (arch §16, §31.3).
/// </summary>
/// <remarks>
/// This is the single place that knows the role→model mapping (the model sheet).
/// Everything upstream speaks in roles; everything downstream speaks in model
/// names. Changing which model serves a role — including substituting a
/// fine-tuned specialist — is a change here alone (arch §16).
/// </remarks>
public interface IModelResolver
{
    /// <summary>
    /// Resolves the model that should serve <paramref name="role"/> on the given
    /// <paramref name="hardware"/>.
    /// </summary>
    /// <param name="role">The capability the caller needs.</param>
    /// <param name="hardware">The hardware profile bounding model size.</param>
    /// <returns>The concrete model to route the request to.</returns>
    /// <exception cref="ModelResolutionException">
    /// No model is configured for the role on this hardware tier.
    /// </exception>
    ModelDescriptor Resolve(ModelRole role, HardwareProfile hardware);

    /// <summary>
    /// Attempts to resolve an escalation model one tier above the model that
    /// currently serves <paramref name="role"/>, used after repeated validation
    /// failure (arch §21, model sheet).
    /// </summary>
    /// <param name="role">The role whose worker failed validation.</param>
    /// <param name="hardware">The hardware profile bounding model size.</param>
    /// <param name="escalated">The larger model to retry with, when one exists.</param>
    /// <returns>
    /// <see langword="true"/> if a higher-tier model is available to escalate to;
    /// otherwise <see langword="false"/> (the caller must then degrade, not retry).
    /// </returns>
    bool TryResolveEscalation(ModelRole role, HardwareProfile hardware, out ModelDescriptor? escalated);
}

/// <summary>
/// Thrown when no model is configured to serve a requested role on the current
/// hardware. This is a configuration error, not a model failure.
/// </summary>
public sealed class ModelResolutionException : Exception
{
    /// <summary>Creates the exception for a role/tier with no configured model.</summary>
    public ModelResolutionException(ModelRole role, HardwareTier tier)
        : base($"No model is configured for role '{role}' on hardware tier '{tier}'.")
    {
        Role = role;
        Tier = tier;
    }

    /// <summary>The role that could not be resolved.</summary>
    public ModelRole Role { get; }

    /// <summary>The hardware tier for which resolution was attempted.</summary>
    public HardwareTier Tier { get; }
}
