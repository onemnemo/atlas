using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Tools.WebSearch;

/// <summary>
/// A web-search backend that queries a SearXNG instance's JSON API.
/// </summary>
/// <remarks>
/// SearXNG is a self-hostable metasearch engine: privacy-respecting, key-free,
/// and returns a simple JSON document, which makes it a good local-first default.
/// The instance URL is configured by the user; failures degrade to an empty
/// result set rather than throwing.
/// </remarks>
public sealed partial class SearxngWebSearchBackend : IWebSearchBackend
{
    /// <summary>The named <see cref="HttpClient"/> used for web search.</summary>
    public const string HttpClientName = "AtlasWebSearch";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebSearchOptions _options;
    private readonly ILogger<SearxngWebSearchBackend> _logger;

    /// <summary>Creates the backend.</summary>
    public SearxngWebSearchBackend(
        IHttpClientFactory httpClientFactory,
        IOptions<WebSearchOptions> options,
        ILogger<SearxngWebSearchBackend> logger)
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
        _options.Provider == WebSearchProvider.Searxng && !string.IsNullOrWhiteSpace(_options.BaseUrl);

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

        string requestUri = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/search?q={1}&format=json",
            _options.BaseUrl.TrimEnd('/'),
            Uri.EscapeDataString(query));

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            JsonNode? document = await client
                .GetFromJsonAsync<JsonNode>(requestUri, cancellationToken)
                .ConfigureAwait(false);

            return Parse(document, maxResults);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            LogSearchFailed(_logger, ex);
            return [];
        }
        catch (System.Text.Json.JsonException ex)
        {
            LogSearchFailed(_logger, ex);
            return [];
        }
    }

    private static List<WebSearchHit> Parse(JsonNode? document, int maxResults)
    {
        if (document?["results"] is not JsonArray results)
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
                Snippet: obj["content"]?.GetValue<string>() ?? string.Empty));
        }

        return hits;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web search request failed.")]
    private static partial void LogSearchFailed(ILogger logger, Exception exception);
}
