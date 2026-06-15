namespace Atlas.Tools;

/// <summary>
/// A provider of executable tools — for example the set of built-in local tools,
/// or the tools discovered from one external MCP server.
/// </summary>
/// <remarks>
/// Sources are listed asynchronously because discovery may involve I/O (launching
/// an MCP server and reading its tool list). The registry aggregates every source
/// into one tree and refreshes that snapshot out of band, so the synchronous
/// <see cref="Atlas.Core.Tools.IToolGateway"/> surface always reads a stable view.
/// </remarks>
public interface IToolSource
{
    /// <summary>A stable, human-readable name for diagnostics (e.g. <c>"local"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Returns the tools currently provided by this source. Implementations should
    /// surface discovery failures as an empty list (and log), never by throwing —
    /// one broken MCP server must not take down the whole tree.
    /// </summary>
    ValueTask<IReadOnlyList<ITool>> ListToolsAsync(CancellationToken cancellationToken = default);
}
