using Atlas.Core.Permissions;
using Xunit;

namespace Atlas.Core.Tests.Permissions;

public sealed class PermissionStateTests
{
    [Fact]
    public void ReadOnly_default_allows_only_reading()
    {
        PermissionState state = PermissionState.ReadOnly;

        Assert.True(state.Allows(PermissionLevel.Read));
        Assert.False(state.Allows(PermissionLevel.Suggest));
        Assert.False(state.Allows(PermissionLevel.DirectEdit));
        Assert.False(state.Allows(PermissionLevel.Destructive));
    }

    [Fact]
    public void Allows_is_monotonic_across_the_ladder()
    {
        PermissionState state = PermissionState.ReadOnly.WithLevel(PermissionLevel.DirectEdit);

        // A higher grant authorises everything below it.
        Assert.True(state.Allows(PermissionLevel.Read));
        Assert.True(state.Allows(PermissionLevel.Suggest));
        Assert.True(state.Allows(PermissionLevel.Draft));
        Assert.True(state.Allows(PermissionLevel.DirectEdit));
        // But not above it.
        Assert.False(state.Allows(PermissionLevel.Destructive));
    }

    [Fact]
    public void WithLevel_never_lowers_an_existing_grant()
    {
        PermissionState elevated = PermissionState.ReadOnly.WithLevel(PermissionLevel.Destructive);

        PermissionState afterLower = elevated.WithLevel(PermissionLevel.Suggest);

        Assert.Equal(PermissionLevel.Destructive, afterLower.GrantedLevel);
    }

    [Fact]
    public void Resource_gates_are_independent_of_the_level_ladder()
    {
        // Being allowed the most destructive action does not open orthogonal gates.
        PermissionState destructive = PermissionState.ReadOnly.WithLevel(PermissionLevel.Destructive);

        Assert.False(destructive.Allows(ResourceGate.GatedExternal));
        Assert.False(destructive.Allows(ResourceGate.PrivateMemory));

        PermissionState withInternet = destructive.WithGate(ResourceGate.GatedExternal);
        Assert.True(withInternet.Allows(ResourceGate.GatedExternal));
        Assert.False(withInternet.Allows(ResourceGate.PrivateMemory));
    }

    [Fact]
    public void WithGate_accumulates_gates_without_clearing_others()
    {
        PermissionState state = PermissionState.ReadOnly
            .WithGate(ResourceGate.GatedExternal)
            .WithGate(ResourceGate.PrivateMemory);

        Assert.True(state.Allows(ResourceGate.GatedExternal));
        Assert.True(state.Allows(ResourceGate.PrivateMemory));
    }
}
