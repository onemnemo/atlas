using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Core.Diagnostics;
using Atlas.Core.Inference;
using Atlas.Core.Permissions;
using Atlas.Core.Tools;
using Microsoft.Extensions.Options;

namespace Atlas.Tools.WebSearch;

/// <summary>
/// The built-in gated web-search tool: the flagship of the
/// <see cref="ToolBranch.WebSearch"/> branch (arch §10, §27).
/// </summary>
/// <remarks>
/// It is guarded by <see cref="ResourceGate.GatedExternal"/>, so it is invisible
/// and uninvokable unless the session has opened the internet gate. Results are
/// returned as compact JSON carrying each hit's URL, so any claim the model later
/// makes from them can be cited (arch §11, §17).
/// </remarks>
public sealed class WebSearchTool : ITool
{
    private const string QueryArg = "query";
    private const string MaxResultsArg = "max_results";

    private readonly IWebSearchBackend _backend;
    private readonly WebSearchOptions _options;

    /// <summary>Creates the tool over the configured backend.</summary>
    public WebSearchTool(IWebSearchBackend backend, IOptions<WebSearchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(options);
        _backend = backend;
        _options = options.Value;

        Descriptor = new ToolDescriptor(
            Name: "web.search",
            Branch: ToolBranch.WebSearch,
            Summary: "Search the public internet and return titled results with URLs and snippets.",
            Parameters:
            [
                ToolParameter.Text(QueryArg, "The search query."),
                ToolParameter.Whole(MaxResultsArg, "Maximum number of results to return.", required: false),
            ],
            RequiredPermission: PermissionLevel.Read,
            RequiredGate: ResourceGate.GatedExternal,
            MinimumModelTier: ModelTier.Small,
            Origin: "local");
    }

    /// <inheritdoc />
    public ToolDescriptor Descriptor { get; }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (!_backend.IsConfigured)
        {
            return ToolResult.Failed(
                FailureMode.ResourceUnavailable,
                "Web search is not configured. Choose a provider and set its URL in settings.");
        }

        string query = invocation.Arguments[QueryArg]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Rejected(FailureMode.MalformedOutput, "A non-empty 'query' is required.");
        }

        int maxResults = ResolveMaxResults(invocation.Arguments);

        IReadOnlyList<WebSearchHit> hits = await _backend
            .SearchAsync(query, maxResults, cancellationToken)
            .ConfigureAwait(false);

        if (hits.Count == 0)
        {
            return ToolResult.Failed(FailureMode.RetrievalEmpty, "The search returned no results.");
        }

        return ToolResult.Ok(Serialize(hits));
    }

    private int ResolveMaxResults(JsonObject arguments)
    {
        int requested = _options.DefaultMaxResults;
        if (arguments.TryGetPropertyValue(MaxResultsArg, out JsonNode? node)
            && node is not null
            && node.AsValue().TryGetValue(out int parsed)
            && parsed > 0)
        {
            requested = parsed;
        }

        return Math.Clamp(requested, 1, Math.Max(1, _options.ResultLimit));
    }

    private static string Serialize(IReadOnlyList<WebSearchHit> hits)
    {
        var array = new JsonArray();
        foreach (WebSearchHit hit in hits)
        {
            array.Add(new JsonObject
            {
                ["title"] = hit.Title,
                ["url"] = hit.Url,
                ["snippet"] = hit.Snippet,
            });
        }

        return array.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
