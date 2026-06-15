namespace Atlas.Tools.WebSearch;

/// <summary>
/// The default web-search backend used when no provider is configured. It reports
/// itself unconfigured so the tool can return a clear, actionable message instead
/// of failing opaquely.
/// </summary>
public sealed class DisabledWebSearchBackend : IWebSearchBackend
{
    /// <inheritdoc />
    public bool IsConfigured => false;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WebSearchHit>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<WebSearchHit>>([]);
}
