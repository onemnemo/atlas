using Atlas.Core.Hardware;
using Xunit;

namespace Atlas.Core.Tests.Hardware;

public sealed class HardwareProfileTests
{
    private static HardwareProfile Profile(HardwareTier tier, int cores, AcceleratorKind accelerator = AcceleratorKind.None) =>
        new(tier, cores, TotalSystemMemoryBytes: 16L << 30, AvailableSystemMemoryBytes: 8L << 30, accelerator, TotalVideoMemoryBytes: 0);

    [Fact]
    public void Low_end_permits_only_serial_inference()
    {
        Assert.Equal(1, Profile(HardwareTier.LowEnd, cores: 8).MaxConcurrentInferences);
    }

    [Fact]
    public void Mid_range_permits_limited_parallelism()
    {
        Assert.Equal(2, Profile(HardwareTier.MidRange, cores: 8).MaxConcurrentInferences);
    }

    [Fact]
    public void High_end_scales_with_cores_but_stays_bounded()
    {
        Assert.Equal(2, Profile(HardwareTier.HighEnd, cores: 4).MaxConcurrentInferences);
        Assert.Equal(6, Profile(HardwareTier.HighEnd, cores: 12).MaxConcurrentInferences);
        Assert.Equal(8, Profile(HardwareTier.HighEnd, cores: 64).MaxConcurrentInferences);
    }

    [Fact]
    public void Accelerator_presence_is_reported()
    {
        Assert.False(Profile(HardwareTier.LowEnd, 4).HasAccelerator);
        Assert.True(Profile(HardwareTier.HighEnd, 8, AcceleratorKind.Cuda).HasAccelerator);
    }

    [Fact]
    public void Tiers_are_ordered_by_capability()
    {
        Assert.True(HardwareTier.LowEnd < HardwareTier.MidRange);
        Assert.True(HardwareTier.MidRange < HardwareTier.HighEnd);
    }
}
