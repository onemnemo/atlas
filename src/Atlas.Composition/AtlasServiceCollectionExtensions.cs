using Atlas.Hardware;
using Atlas.Inference;
using Atlas.Inference.Configuration;
using Atlas.Orchestration;
using Atlas.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Composition;

/// <summary>
/// The single composition entry point for the whole Atlas system.
/// </summary>
public static class AtlasServiceCollectionExtensions
{
    /// <summary>
    /// Registers every Atlas module — hardware detection, inference transport and
    /// model resolution, and the orchestration runtime — into one service
    /// collection.
    /// </summary>
    /// <remarks>
    /// This is the one place that knows the system's composition. Hosts (CLI, the
    /// Studio UI, the future Avalonia integration) call this and then resolve
    /// <see cref="Core.IAtlasOrchestrator"/>. Swapping a module's implementation
    /// is a change to that module's registration, not here.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureInference">Optional inference configuration (endpoints, model sheet).</param>
    /// <param name="tierPolicy">Optional hardware tier policy override.</param>
    public static IServiceCollection AddAtlas(
        this IServiceCollection services,
        Action<InferenceOptions>? configureInference = null,
        HardwareTierPolicy? tierPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAtlasHardware(tierPolicy);
        services.AddAtlasInference(configureInference);
        services.AddAtlasOrchestration();
        services.AddAtlasTools();

        return services;
    }
}
