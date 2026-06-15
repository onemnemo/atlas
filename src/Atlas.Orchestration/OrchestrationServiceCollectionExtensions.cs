using Atlas.Core;
using Atlas.Core.Inference;
using Atlas.Core.Pipeline;
using Atlas.Core.Tasks;
using Atlas.Core.Tools;
using Atlas.Orchestration.Routing;
using Atlas.Orchestration.Stages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Atlas.Orchestration;

/// <summary>
/// DI registration for the orchestration runtime.
/// </summary>
public static class OrchestrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the orchestrator, the default request router, the task-profile
    /// registry, and the built-in pipeline routes and stages.
    /// </summary>
    /// <remarks>
    /// Requires that hardware (<see cref="Core.Hardware.HardwareProfile"/>),
    /// inference (<see cref="Core.Inference.IInferenceClient"/>,
    /// <see cref="Core.Inference.IModelResolver"/>) are also registered — typically
    /// via <c>AddAtlasHardware()</c> and <c>AddAtlasInference()</c> at the
    /// composition root.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddAtlasOrchestration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ChatOptions>();
        services.TryAddSingleton<ITaskProfileProvider>(TaskProfileRegistry.Default);
        services.TryAddSingleton<IRequestRouter, TaskIdRequestRouter>();

        // Stages.
        services.TryAddSingleton<IPipelineStage<ChatDraftInput, string>, ChatDrafterStage>();

        // Routes (one registration per task type; resolved as IEnumerable).
        // ChatRoute takes IToolGateway as optional; use a factory so DI returns
        // null rather than throwing when the tools module is not registered.
        services.AddSingleton<IPipelineRoute>(static provider => new ChatRoute(
            provider.GetRequiredService<IPipelineStage<ChatDraftInput, string>>(),
            provider.GetRequiredService<IModelResolver>(),
            provider.GetRequiredService<IOptions<ChatOptions>>(),
            provider.GetService<IToolGateway>()));

        services.TryAddSingleton<IAtlasOrchestrator, AtlasOrchestrator>();

        return services;
    }
}
