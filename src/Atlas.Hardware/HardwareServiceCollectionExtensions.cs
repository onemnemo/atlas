using Atlas.Core.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Hardware;

/// <summary>
/// DI registration for hardware detection.
/// </summary>
public static class HardwareServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IHardwareProfiler"/> and a singleton
    /// <see cref="HardwareProfile"/> detected once at startup.
    /// </summary>
    /// <remarks>
    /// The profile is resolved eagerly as a singleton so the whole application
    /// shares one consistent view of the hardware for the lifetime of the run.
    /// Components that need to react to changing load can depend on
    /// <see cref="IHardwareProfiler"/> directly and re-detect.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">Optional tier policy override.</param>
    public static IServiceCollection AddAtlasHardware(
        this IServiceCollection services,
        HardwareTierPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(policy ?? HardwareTierPolicy.Default);
        services.TryAddSingleton<IHardwareProfiler, SystemHardwareProfiler>();
        services.TryAddSingleton(static sp => sp.GetRequiredService<IHardwareProfiler>().Detect());

        return services;
    }
}
