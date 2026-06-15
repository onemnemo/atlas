namespace Atlas.Tools.WebSearch;

/// <summary>
/// A single web-search result.
/// </summary>
/// <param name="Title">The result title.</param>
/// <param name="Url">The result URL — the citation source for any claim drawn from it.</param>
/// <param name="Snippet">A short excerpt or description.</param>
public sealed record WebSearchHit(string Title, string Url, string Snippet);

/// <summary>
/// A pluggable web-search backend. The tool is provider-agnostic; this is the
/// seam where a concrete search service is wired in.
/// </summary>
public interface IWebSearchBackend
{
    /// <summary>Whether the backend is configured and usable.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Runs a search and returns up to <paramref name="maxResults"/> hits. Should
    /// return an empty list (not throw) when there are simply no results.
    /// </summary>
    ValueTask<IReadOnlyList<WebSearchHit>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);
}
