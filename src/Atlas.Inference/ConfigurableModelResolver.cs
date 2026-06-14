using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Inference.Configuration;
using Microsoft.Extensions.Options;

namespace Atlas.Inference;

/// <summary>
/// The default <see cref="IModelResolver"/>: maps a role and hardware tier to a
/// concrete model using the configured model sheet (arch §16, §31.3).
/// </summary>
/// <remarks>
/// This is the single place that knows which model fills which role. Swapping a
/// model — including dropping in a fine-tuned specialist — is a configuration
/// change to the bindings, never a code change anywhere else (arch §16).
/// </remarks>
public sealed class ConfigurableModelResolver : IModelResolver
{
    private readonly Dictionary<string, ModelDefinition> _modelsByName;
    private readonly Dictionary<(ModelRole Role, HardwareTier Tier), string> _bindings;

    /// <summary>Creates a resolver from inference options.</summary>
    /// <param name="options">The inference options carrying models and role bindings.</param>
    public ConfigurableModelResolver(IOptions<InferenceOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    /// <summary>Creates a resolver directly from an options instance (used in tests).</summary>
    /// <param name="options">The inference options carrying models and role bindings.</param>
    public ConfigurableModelResolver(InferenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DefaultModelSheet.ApplyDefaults(options);

        _modelsByName = new Dictionary<string, ModelDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (ModelDefinition model in options.Models)
        {
            _modelsByName[model.Name] = model;
        }

        _bindings = new Dictionary<(ModelRole, HardwareTier), string>();
        foreach (RoleModelBinding binding in options.RoleBindings)
        {
            _bindings[(binding.Role, binding.Tier)] = binding.Model;
        }
    }

    /// <inheritdoc />
    public ModelDescriptor Resolve(ModelRole role, HardwareProfile hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        if (_bindings.TryGetValue((role, hardware.Tier), out string? modelName)
            && _modelsByName.TryGetValue(modelName, out ModelDefinition? model))
        {
            return ToDescriptor(model);
        }

        throw new ModelResolutionException(role, hardware.Tier);
    }

    /// <inheritdoc />
    public bool TryResolveEscalation(ModelRole role, HardwareProfile hardware, out ModelDescriptor? escalated)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        escalated = null;

        // The tier of the model currently serving the role (if any).
        ModelTier currentTier = ModelTier.Tiny;
        if (_bindings.TryGetValue((role, hardware.Tier), out string? currentName)
            && _modelsByName.TryGetValue(currentName, out ModelDefinition? current))
        {
            currentTier = current.Tier;
        }

        ModelTier ceiling = MaxModelTierFor(hardware.Tier);
        if (currentTier >= ceiling)
        {
            // Already at the largest model this hardware permits — escalation is
            // not available; the caller must degrade rather than retry (arch §21).
            return false;
        }

        // Prefer an explicitly-configured Fallback model for this hardware tier.
        if (_bindings.TryGetValue((ModelRole.Fallback, hardware.Tier), out string? fallbackName)
            && _modelsByName.TryGetValue(fallbackName, out ModelDefinition? fallback)
            && fallback.Tier > currentTier
            && fallback.Tier <= ceiling)
        {
            escalated = ToDescriptor(fallback);
            return true;
        }

        // Otherwise pick the smallest defined model strictly above the current
        // tier and within the hardware ceiling.
        ModelDefinition? best = null;
        foreach (ModelDefinition candidate in _modelsByName.Values)
        {
            if (candidate.Tier <= currentTier || candidate.Tier > ceiling)
            {
                continue;
            }

            if (best is null || candidate.Tier < best.Tier)
            {
                best = candidate;
            }
        }

        if (best is not null)
        {
            escalated = ToDescriptor(best);
            return true;
        }

        return false;
    }

    private static ModelDescriptor ToDescriptor(ModelDefinition model) =>
        new(model.Name, model.Tier, model.SupportsStructuredOutput);

    private static ModelTier MaxModelTierFor(HardwareTier tier) => tier switch
    {
        HardwareTier.LowEnd => ModelTier.Small,
        HardwareTier.MidRange => ModelTier.Medium,
        HardwareTier.HighEnd => ModelTier.Large,
        _ => ModelTier.Small,
    };
}
