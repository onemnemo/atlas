using Atlas.Core.Diagnostics;
using Atlas.Core.Tools;
using Microsoft.Extensions.Logging;

namespace Atlas.Tools;

/// <summary>
/// The default <see cref="IToolGateway"/>: aggregates every <see cref="IToolSource"/>
/// into one scoped, permissioned tool tree (arch §10-§12, §35).
/// </summary>
/// <remarks>
/// <para>
/// The registry holds an immutable snapshot of the discovered tools and rebuilds
/// it on <see cref="RefreshAsync"/>. Discovery (which may launch MCP servers) is
/// therefore kept off the synchronous read path: branch discovery, tool selection,
/// and lookup all read the current snapshot without locking.
/// </para>
/// <para>
/// All exposure decisions funnel through <see cref="ToolDescriptor.IsAllowedBy"/>,
/// so a tool is never offered or invoked outside the caller's scope.
/// </para>
/// </remarks>
public sealed partial class ToolRegistry : IToolGateway
{
    private readonly IReadOnlyList<IToolSource> _sources;
    private readonly ILogger<ToolRegistry> _logger;

    private volatile IReadOnlyDictionary<string, ITool> _byName =
        new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the registry over the given sources.</summary>
    public ToolRegistry(
        IEnumerable<IToolSource> sources,
        ILogger<ToolRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);
        _sources = [.. sources];
        _logger = logger;
    }

    /// <summary>The number of tools currently in the tree (all branches, unscoped).</summary>
    public int Count => _byName.Count;

    /// <summary>
    /// Rebuilds the tool snapshot from every source. A source that fails discovery
    /// is skipped with a warning rather than failing the whole refresh.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var map = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);

        foreach (IToolSource source in _sources)
        {
            IReadOnlyList<ITool> tools;
            try
            {
                tools = await source.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One bad source must not break the whole tree.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogSourceFailed(_logger, source.Name, ex);
                continue;
            }

            foreach (ITool tool in tools)
            {
                if (!map.TryAdd(tool.Descriptor.Name, tool))
                {
                    LogDuplicateTool(_logger, tool.Descriptor.Name, source.Name);
                }
            }
        }

        _byName = map;
        LogRefreshed(_logger, map.Count, _sources.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolBranchInfo> DiscoverBranches(ToolScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var counts = new Dictionary<ToolBranch, int>();
        foreach (ITool tool in _byName.Values)
        {
            if (tool.Descriptor.IsAllowedBy(scope))
            {
                counts[tool.Descriptor.Branch] = counts.GetValueOrDefault(tool.Descriptor.Branch) + 1;
            }
        }

        return [.. counts
            .OrderBy(static pair => pair.Key)
            .Select(static pair => new ToolBranchInfo(pair.Key, ToolBranchCatalog.Describe(pair.Key), pair.Value))];
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDescriptor> SelectTools(ToolBranch branch, ToolScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return [.. _byName.Values
            .Select(static tool => tool.Descriptor)
            .Where(descriptor => descriptor.Branch == branch && descriptor.IsAllowedBy(scope))
            .OrderBy(static descriptor => descriptor.RequiredPermission)
            .ThenBy(static descriptor => descriptor.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, scope.MaxToolsPerCall))];
    }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeAsync(
        ToolInvocation invocation,
        ToolScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(scope);

        if (!_byName.TryGetValue(invocation.ToolName, out ITool? tool))
        {
            return ToolResult.Rejected(
                FailureMode.BrokenReference,
                $"Unknown tool '{invocation.ToolName}'. Discover a branch and select a tool first.");
        }

        if (!tool.Descriptor.IsAllowedBy(scope))
        {
            return ToolResult.Rejected(
                FailureMode.PermissionDenied,
                $"Tool '{invocation.ToolName}' is not permitted under the current scope.");
        }

        ToolArgumentValidator.ValidationResult validation = ToolArgumentValidator.Validate(tool.Descriptor, invocation.Arguments);
        if (!validation.IsValid)
        {
            return ToolResult.Rejected(FailureMode.MalformedOutput, validation.Error!);
        }

        try
        {
            return await tool.ExecuteAsync(invocation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Convert any tool fault into a structured failure, never a silent crash.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogInvocationFailed(_logger, invocation.ToolName, ex);
            return ToolResult.Failed(
                FailureMode.SubagentFailure,
                $"Tool '{invocation.ToolName}' threw while executing.",
                ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool source '{Source}' failed during discovery; skipping it.")]
    private static partial void LogSourceFailed(ILogger logger, string source, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate tool '{Tool}' from source '{Source}' ignored.")]
    private static partial void LogDuplicateTool(ILogger logger, string tool, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool tree refreshed: {ToolCount} tools from {SourceCount} sources.")]
    private static partial void LogRefreshed(ILogger logger, int toolCount, int sourceCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tool '{Tool}' threw during execution.")]
    private static partial void LogInvocationFailed(ILogger logger, string tool, Exception exception);
}
