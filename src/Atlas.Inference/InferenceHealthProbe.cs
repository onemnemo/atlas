using Atlas.Inference.Configuration;
using Microsoft.Extensions.Options;

namespace Atlas.Inference;

/// <summary>
/// Checks whether the local inference backend is reachable and ready (arch §31.4).
/// </summary>
/// <remarks>
/// The orchestrator must "never route a request to a router that has not finished
/// initializing" (arch §31.4); the dashboard also surfaces backend status to the
/// user. Both consume this probe.
/// </remarks>
public interface IInferenceHealthProbe
{
    /// <summary>Returns whether the backend's health endpoint responds successfully.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IInferenceHealthProbe"/>, hitting the backend's
/// configured health path.
/// </summary>
public sealed class InferenceHealthProbe : IInferenceHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly InferenceOptions _options;

    /// <summary>Creates the probe.</summary>
    /// <param name="httpClient">HTTP client used for the health request.</param>
    /// <param name="options">Inference options carrying the base URL and health path.</param>
    public InferenceHealthProbe(HttpClient httpClient, IOptions<InferenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        string url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.HealthPath.TrimStart('/')}";

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) // health check must never throw to its callers
        {
            return false;
        }
    }
}
