using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Inference;
using Atlas.Inference.Configuration;
using Xunit;

namespace Atlas.Inference.Tests;

public sealed class ConfigurableModelResolverTests
{
    private static HardwareProfile Hw(HardwareTier tier) =>
        new(tier, LogicalCoreCount: 8, TotalSystemMemoryBytes: 16L << 30, AvailableSystemMemoryBytes: 8L << 30, AcceleratorKind.None, 0);

    private static ConfigurableModelResolver Resolver() => new(new InferenceOptions());

    [Fact]
    public void Router_resolves_to_the_tiny_model_on_every_tier()
    {
        ConfigurableModelResolver resolver = Resolver();

        foreach (HardwareTier tier in Enum.GetValues<HardwareTier>())
        {
            ModelDescriptor model = resolver.Resolve(ModelRole.Router, Hw(tier));
            Assert.Equal(ModelTier.Tiny, model.Tier);
        }
    }

    [Fact]
    public void Main_worker_scales_up_from_low_to_mid_range()
    {
        ConfigurableModelResolver resolver = Resolver();

        ModelDescriptor low = resolver.Resolve(ModelRole.MainWorker, Hw(HardwareTier.LowEnd));
        ModelDescriptor mid = resolver.Resolve(ModelRole.MainWorker, Hw(HardwareTier.MidRange));

        // The low-end main worker must not be larger than the mid-range one.
        Assert.True(low.Tier <= mid.Tier);
    }

    [Fact]
    public void Unconfigured_role_tier_combination_throws()
    {
        ConfigurableModelResolver resolver = Resolver();

        // The default sheet only binds Fallback on high-end hardware.
        ModelResolutionException ex = Assert.Throws<ModelResolutionException>(
            () => resolver.Resolve(ModelRole.Fallback, Hw(HardwareTier.LowEnd)));

        Assert.Equal(ModelRole.Fallback, ex.Role);
        Assert.Equal(HardwareTier.LowEnd, ex.Tier);
    }

    [Fact]
    public void Low_end_cannot_escalate_beyond_its_ceiling()
    {
        ConfigurableModelResolver resolver = Resolver();

        bool escalated = resolver.TryResolveEscalation(ModelRole.MainWorker, Hw(HardwareTier.LowEnd), out ModelDescriptor? model);

        Assert.False(escalated);
        Assert.Null(model);
    }

    [Fact]
    public void High_end_escalates_to_a_larger_fallback_model()
    {
        ConfigurableModelResolver resolver = Resolver();

        bool escalated = resolver.TryResolveEscalation(ModelRole.MainWorker, Hw(HardwareTier.HighEnd), out ModelDescriptor? model);

        Assert.True(escalated);
        Assert.NotNull(model);
        // Escalation must move to a strictly larger tier than the main worker (Small).
        Assert.True(model!.Tier > ModelTier.Small);
    }

    [Fact]
    public void Explicit_configuration_overrides_the_default_sheet()
    {
        var options = new InferenceOptions();
        options.Models.Add(new ModelDefinition("custom-tuned-router", ModelTier.Tiny));
        options.RoleBindings.Add(new RoleModelBinding(ModelRole.Router, HardwareTier.MidRange, "custom-tuned-router"));

        var resolver = new ConfigurableModelResolver(options);

        // Because RoleBindings was non-empty, defaults are NOT applied, so only the
        // explicit binding exists — demonstrating configuration fully owns the map.
        ModelDescriptor model = resolver.Resolve(ModelRole.Router, Hw(HardwareTier.MidRange));
        Assert.Equal("custom-tuned-router", model.Name);
    }
}
