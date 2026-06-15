using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Tools.WebSearch;

/// <summary>
/// A web-search backend that calls the Brave Search JSON API.
/// </summary>
/// <remarks>
/// Requires a free Brave Search API key (2 000 queries/month on the free tier,
/// see https://brave.com/search/api). Set <c>Atlas:WebSearch:ApiKey</c> in
/// appsettings.json to activate. More reliable than HTML scraping.
/// </remarks>
public sealed partial class BraveWebSearchBackend : IWebSearchBackend
{
    private const string SearchEndpoint = "https://api.search.brave.com/res/v1/web/search";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebSearchOptions _options;
    private readonly ILogger<BraveWebSearchBackend> _logger;

    /// <summary>Creates the backend.</summary>
    public BraveWebSearchBackend(
        IHttpClientFactory httpClientFactory,
        IOptions<WebSearchOptions> options,
        ILogger<BraveWebSearchBackend> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConfigured =>
        _options.Provider == WebSearchProvider.Brave
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebSearchHit>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!IsConfigured)
        {
            return [];
        }

        string url = $"{SearchEndpoint}?q={Uri.EscapeDataString(query)}&count={Math.Clamp(maxResults, 1, 20)}";

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(SearxngWebSearchBackend.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("X-Subscription-Token", _options.ApiKey!);

            using HttpResponseMessage response = await client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            JsonNode? document = await response.Content
                .ReadFromJsonAsync<JsonNode>(cancellationToken)
                .ConfigureAwait(false);

            return Parse(document, maxResults);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            LogSearchFailed(_logger, ex);
            return [];
        }
    }

    private static List<WebSearchHit> Parse(JsonNode? document, int maxResults)
    {
        if (document?["web"]?["results"] is not JsonArray results)
        {
            return [];
        }

        var hits = new List<WebSearchHit>(Math.Min(results.Count, maxResults));
        foreach (JsonNode? node in results)
        {
            if (hits.Count >= maxResults)
            {
                break;
            }

            if (node is not JsonObject obj)
            {
                continue;
            }

            string url = obj["url"]?.GetValue<string>() ?? string.Empty;
            if (url.Length == 0)
            {
                continue;
            }

            hits.Add(new WebSearchHit(
                Title: obj["title"]?.GetValue<string>() ?? url,
                Url: url,
                Snippet: obj["description"]?.GetValue<string>() ?? string.Empty));
        }

        return hits;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Brave Search request failed.")]
    private static partial void LogSearchFailed(ILogger logger, Exception exception);
}
