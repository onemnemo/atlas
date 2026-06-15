namespace Atlas.Tools.WebSearch;

/// <summary>
/// The web-search backend providers Atlas can be configured to use.
/// </summary>
public enum WebSearchProvider
{
    /// <summary>No backend; web search is unavailable until one is configured.</summary>
    None = 0,

    /// <summary>
    /// A self-hosted or third-party SearXNG instance exposing the JSON search API.
    /// Privacy-respecting and key-free, which fits the local-first philosophy.
    /// </summary>
    Searxng = 1,
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

    /// <summary>Which backend to use. Defaults to <see cref="WebSearchProvider.None"/>.</summary>
    public WebSearchProvider Provider { get; set; } = WebSearchProvider.None;

    /// <summary>The backend base URL (e.g. a SearXNG instance root).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>An optional API key, for backends that require one.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The default number of results to return when the caller does not specify.</summary>
    public int DefaultMaxResults { get; set; } = 5;

    /// <summary>The hard cap on results regardless of caller request.</summary>
    public int ResultLimit { get; set; } = 10;
}
