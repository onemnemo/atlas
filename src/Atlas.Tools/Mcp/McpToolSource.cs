using Atlas.Core.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Tools.Mcp;

/// <summary>
/// A <see cref="IToolSource"/> that connects to every configured MCP server and
/// exposes their tools, wrapped as <see cref="McpTool"/> adapters.
/// </summary>
/// <remarks>
/// Clients are created once per server and reused across refreshes so an
/// invocation reaches the same live connection that advertised the tool. A server
/// that fails to connect or list is skipped with a warning — the tree degrades
/// gracefully rather than failing wholesale (arch §26).
/// </remarks>
public sealed partial class McpToolSource : IToolSource, IAsyncDisposable
{
    private readonly McpClientOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpToolSource> _logger;
    private readonly Func<McpServerOptions, McpClientOptions, ILogger, IMcpClient> _clientFactory;
    private readonly Dictionary<string, IMcpClient> _clients = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the source using the default stdio client factory.</summary>
    public McpToolSource(IOptions<McpClientOptions> options, ILoggerFactory loggerFactory)
        : this(options, loggerFactory, static (server, opts, logger) => new StdioMcpClient(server, opts, logger))
    {
    }

    /// <summary>Creates the source with a custom client factory (used by tests).</summary>
    public McpToolSource(
        IOptions<McpClientOptions> options,
        ILoggerFactory loggerFactory,
        Func<McpServerOptions, McpClientOptions, ILogger, IMcpClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(clientFactory);
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpToolSource>();
        _clientFactory = clientFactory;
    }

    /// <inheritdoc />
    public string Name => "mcp";

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ITool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tools = new List<ITool>();
            foreach (McpServerOptions server in _options.Servers)
            {
                if (!server.Enabled || server.Id.Length == 0 || server.Command.Length == 0)
                {
                    continue;
                }

                await AddServerToolsAsync(server, tools, cancellationToken).ConfigureAwait(false);
            }

            return tools;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task AddServerToolsAsync(McpServerOptions server, List<ITool> tools, CancellationToken cancellationToken)
    {
        try
        {
            IMcpClient client = await GetOrConnectAsync(server, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<McpToolInfo> remoteTools = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);

            foreach (McpToolInfo info in remoteTools)
            {
                ToolDescriptor descriptor = McpSchemaMapper.ToDescriptor(info, server);
                tools.Add(new McpTool(descriptor, info.Name, client));
            }

            LogServerTools(_logger, server.Id, remoteTools.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // One bad server must not break discovery.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogServerUnavailable(_logger, server.Id, ex);
        }
    }

    private async Task<IMcpClient> GetOrConnectAsync(McpServerOptions server, CancellationToken cancellationToken)
    {
        if (_clients.TryGetValue(server.Id, out IMcpClient? existing) && existing.IsConnected)
        {
            return existing;
        }

        if (existing is not null)
        {
            await existing.DisposeAsync().ConfigureAwait(false);
            _clients.Remove(server.Id);
        }

        IMcpClient client = _clientFactory(server, _options, _loggerFactory.CreateLogger($"Atlas.Tools.Mcp.{server.Id}"));
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _clients[server.Id] = client;
        return client;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (IMcpClient client in _clients.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        _clients.Clear();
        _gate.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP server '{Server}' exposed {Count} tools.")]
    private static partial void LogServerTools(ILogger logger, string server, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP server '{Server}' is unavailable; its tools are skipped.")]
    private static partial void LogServerUnavailable(ILogger logger, string server, Exception exception);
}
