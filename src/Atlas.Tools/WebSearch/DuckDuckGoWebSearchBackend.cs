using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Atlas.Tools.WebSearch;

/// <summary>
/// A web-search backend that queries DuckDuckGo's HTML endpoint — the zero-setup
/// default. No API key, no local server, no Docker.
/// </summary>
/// <remarks>
/// <para>
/// Uses <c>https://html.duckduckgo.com/html/</c> (GET) with a standard browser
/// User-Agent. Results are extracted from the HTML using compiled regex. This is a
/// well-known, long-stable approach used by many open-source tools.
/// </para>
/// <para>
/// If DDG changes their HTML structure the search degrades to an empty result set
/// (reported as <see cref="Core.Diagnostics.FailureMode.RetrievalEmpty"/>) rather
/// than throwing or returning garbage. Users who need higher reliability or
/// privacy can configure the SearXNG or Brave backends instead.
/// </para>
/// </remarks>
public sealed partial class DuckDuckGoWebSearchBackend : IWebSearchBackend
{
    private const string SearchEndpoint = "https://html.duckduckgo.com/html/";

    // Extracts the redirect URL from DDG's /l/?uddg= wrapper and the anchor text.
    [GeneratedRegex(
        @"href=""//duckduckgo\.com/l/\?(?:[^""]*?&)?uddg=([^&""]+)[^""]*""[^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ResultLinkRegex();

    // Extracts the snippet text from result__snippet anchors.
    [GeneratedRegex(
        @"class=""result__snippet""[^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ResultSnippetRegex();

    // Strips inline HTML tags (e.g. <b> from bold-matched terms).
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DuckDuckGoWebSearchBackend> _logger;

    /// <summary>Creates the backend.</summary>
    public DuckDuckGoWebSearchBackend(
        IHttpClientFactory httpClientFactory,
        ILogger<DuckDuckGoWebSearchBackend> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Always true — DDG needs no local server or API key.</remarks>
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebSearchHit>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string html;
        try
        {
            html = await FetchHtmlAsync(query, cancellationToken).ConfigureAwait(false);
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

        List<WebSearchHit> hits = ParseHtml(html, maxResults);
        if (hits.Count == 0)
        {
            LogNoResults(_logger, query);
        }

        return hits;
    }

    private async Task<string> FetchHtmlAsync(string query, CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient(SearxngWebSearchBackend.HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{SearchEndpoint}?q={Uri.EscapeDataString(query)}");

        // A recognisable browser UA avoids being treated as a bot.
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static List<WebSearchHit> ParseHtml(string html, int maxResults)
    {
        var links = ResultLinkRegex().Matches(html);
        var snippets = ResultSnippetRegex().Matches(html);

        var hits = new List<WebSearchHit>(Math.Min(links.Count, maxResults));

        for (int i = 0; i < links.Count && hits.Count < maxResults; i++)
        {
            Match link = links[i];

            string rawUrl = Uri.UnescapeDataString(link.Groups[1].Value);
            if (!rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Skip non-HTTP results (e.g. internal DDG pages).
                continue;
            }

            string title = DecodeHtml(link.Groups[2].Value);
            if (title.Length == 0)
            {
                continue;
            }

            string snippet = i < snippets.Count
                ? DecodeHtml(snippets[i].Groups[1].Value)
                : string.Empty;

            hits.Add(new WebSearchHit(title, rawUrl, snippet));
        }

        return hits;
    }

    private static string DecodeHtml(string html)
    {
        // Strip inline tags, then decode the most common HTML entities.
        string stripped = HtmlTagRegex().Replace(html, string.Empty).Trim();
        return stripped
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#x27;", "'", StringComparison.Ordinal)
            .Replace("&apos;", "'", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal)
            .Replace("&nbsp;", " ", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DuckDuckGo search request failed.")]
    private static partial void LogSearchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DuckDuckGo returned no results for query '{Query}'.")]
    private static partial void LogNoResults(ILogger logger, string query);
}
