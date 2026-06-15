namespace Atlas.Tools.WebSearch;

/// <summary>
/// The web-search backend providers Atlas can be configured to use.
/// </summary>
public enum WebSearchProvider
{
    /// <summary>No backend; web search is explicitly disabled.</summary>
    None = 0,

    /// <summary>
    /// A self-hosted or third-party SearXNG instance exposing the JSON search API.
    /// Privacy-respecting and key-free, but requires running a local server.
    /// </summary>
    Searxng = 1,

    /// <summary>
    /// DuckDuckGo's HTML search endpoint — no API key, no server, no setup.
    /// This is the default: it works immediately out of the box. Parses the
    /// DDG HTML response; degrades gracefully to an empty result if the format
    /// changes rather than crashing.
    /// </summary>
    DuckDuckGo = 2,

    /// <summary>
    /// Brave Search API. Requires an API key (free tier available at
    /// https://brave.com/search/api). More reliable than HTML scraping.
    /// Set <see cref="WebSearchOptions.ApiKey"/> to enable.
    /// </summary>
    Brave = 3,
}

/// <summary>
/// Configuration for the gated web-search tool (arch §27).
/// </summary>
/// <remarks>
/// Web search reaches the public internet, so it is off by default: a provider
/// must be chosen and, at runtime, the session must hold the internet resource
/// gate. Keeping the provider behind configuration means the search backend can
/// be swapped without touching the tool.
/// </remarks>
public sealed class WebSearchOptions
{
    /// <summary>The configuration section name used when binding from appsettings.</summary>
    public const string SectionName = "Atlas:WebSearch";

    /// <summary>Which backend to use. Defaults to <see cref="WebSearchProvider.DuckDuckGo"/> (no setup required).</summary>
    public WebSearchProvider Provider { get; set; } = WebSearchProvider.DuckDuckGo;

    /// <summary>The backend base URL (e.g. a SearXNG instance root).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>An optional API key, for backends that require one.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The minimum number of results Atlas considers sufficient.
    /// When the backend returns fewer results than this, the tool result is still
    /// returned but marked as a thin result in the activity feed so the UI can
    /// warn the user.  Defaults to 1 (any result is acceptable).
    /// </summary>
    public int MinResults { get; set; } = 1;

    /// <summary>The default number of results to return when the caller does not specify.</summary>
    public int DefaultMaxResults { get; set; } = 5;

    /// <summary>The hard cap on results regardless of caller request.</summary>
    public int ResultLimit { get; set; } = 10;
}
