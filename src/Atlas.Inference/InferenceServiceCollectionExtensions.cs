using Atlas.Core.Inference;
using Atlas.Inference.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Atlas.Inference;

/// <summary>
/// DI registration for the inference transport and model resolution.
/// </summary>
public static class InferenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IModelResolver"/>, <see cref="IInferenceClient"/>, and
    /// <see cref="IInferenceHealthProbe"/> backed by a local OpenAI-compatible
    /// endpoint.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional configuration of <see cref="InferenceOptions"/>. The default model
    /// sheet is applied afterwards for anything left unset, so the system always
    /// has a working model map.
    /// </param>
    public static IServiceCollection AddAtlasInference(
        this IServiceCollection services,
        Action<InferenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<InferenceOptions>()
            .Configure(options =>
            {
                configure?.Invoke(options);
                DefaultModelSheet.ApplyDefaults(options);
            });

        services.TryAddSingleton<IModelResolver, ConfigurableModelResolver>();

        services.AddHttpClient<IInferenceClient, OpenAiCompatibleInferenceClient>()
            .ConfigureHttpClient(ConfigureTimeout);

        services.AddHttpClient<IInferenceHealthProbe, InferenceHealthProbe>()
            .ConfigureHttpClient(ConfigureTimeout);

        return services;
    }

    private static void ConfigureTimeout(IServiceProvider provider, HttpClient client)
    {
        InferenceOptions options = provider.GetRequiredService<IOptions<InferenceOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
    }
}
