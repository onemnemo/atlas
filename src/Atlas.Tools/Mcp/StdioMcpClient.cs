using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Atlas.Tools.Mcp;

/// <summary>
/// An <see cref="IMcpClient"/> that speaks JSON-RPC 2.0 to an MCP server over the
/// server subprocess's standard input/output (the MCP stdio transport).
/// </summary>
/// <remarks>
/// <para>
/// Messages are newline-delimited JSON. A single background loop reads the
/// server's stdout and completes the matching pending request by id, so calls can
/// be awaited independently. The handshake is <c>initialize</c> followed by the
/// <c>notifications/initialized</c> notification, per the MCP specification.
/// </para>
/// <para>
/// The client is defensive by design: a server that dies, stalls, or returns an
/// error faults only the affected call (or, on exit, all in-flight calls) rather
/// than crashing the host. Transport ownership ends with disposal, which kills the
/// subprocess.
/// </para>
/// </remarks>
public sealed partial class StdioMcpClient : IMcpClient
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly McpServerOptions _server;
    private readonly McpClientOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();

    private Process? _process;
    private Task? _readLoop;
    private long _nextId;
    private volatile bool _connected;

    /// <summary>Creates a client for the given server configuration.</summary>
    public StdioMcpClient(McpServerOptions server, McpClientOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _server = server;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConnected => _connected;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connected)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _server.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            WorkingDirectory = _server.WorkingDirectory ?? string.Empty,
        };

        foreach (string arg in _server.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start MCP server '{_server.Id}'.");
        }

        _process = process;
        _readLoop = Task.Run(() => ReadLoopAsync(process), CancellationToken.None);
        _ = Task.Run(() => DrainStandardErrorAsync(process), CancellationToken.None);

        var initParams = new JsonObject
        {
            ["protocolVersion"] = _options.ProtocolVersion,
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "Atlas",
                ["version"] = "0.1.0",
            },
        };

        await SendRequestAsync("initialize", initParams, cancellationToken).ConfigureAwait(false);
        await SendNotificationAsync("notifications/initialized", cancellationToken).ConfigureAwait(false);
        _connected = true;
        LogConnected(_logger, _server.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        JsonNode? result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken)
            .ConfigureAwait(false);

        if (result?["tools"] is not JsonArray tools)
        {
            return [];
        }

        var list = new List<McpToolInfo>(tools.Count);
        foreach (JsonNode? entry in tools)
        {
            if (entry is not JsonObject obj)
            {
                continue;
            }

            string name = obj["name"]?.GetValue<string>() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            string description = obj["description"]?.GetValue<string>() ?? string.Empty;
            JsonObject? schema = obj["inputSchema"] is JsonObject schemaObj ? (JsonObject)schemaObj.DeepClone() : null;
            list.Add(new McpToolInfo(name, description, schema));
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<McpCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        var callParams = new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = (JsonObject)arguments.DeepClone(),
        };

        JsonNode? result = await SendRequestAsync("tools/call", callParams, cancellationToken)
            .ConfigureAwait(false);

        bool isError = result?["isError"]?.GetValue<bool>() ?? false;
        string text = FlattenContent(result?["content"] as JsonArray);
        return new McpCallResult(isError, text);
    }

    private static string FlattenContent(JsonArray? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (JsonNode? block in content)
        {
            if (block is JsonObject obj && obj["text"] is JsonNode textNode)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(textNode.GetValue<string>());
            }
        }

        return builder.ToString();
    }

    private async Task<JsonNode?> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
    {
        long id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            await WriteMessageAsync(envelope, cancellationToken).ConfigureAwait(false);
            using (timeout.Token.Register(static state => ((TaskCompletionSource<JsonNode?>)state!).TrySetCanceled(), tcs))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MCP server '{_server.Id}' did not respond to '{method}' within the timeout.");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendNotificationAsync(string method, CancellationToken cancellationToken)
    {
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = new JsonObject(),
        };

        await WriteMessageAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteMessageAsync(JsonObject envelope, CancellationToken cancellationToken)
    {
        Process process = _process ?? throw new InvalidOperationException("MCP client is not started.");
        string line = envelope.ToJsonString();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await process.StandardInput.WriteAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.WriteAsync("\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(Process process)
    {
        try
        {
            while (true)
            {
                string? line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                DispatchMessage(line);
            }
        }
#pragma warning disable CA1031 // The read loop must never throw out; faulting pending calls is the contract.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogReadLoopFault(_logger, _server.Id, ex);
        }
        finally
        {
            FaultAllPending(new IOException($"MCP server '{_server.Id}' connection closed."));
        }
    }

    private void DispatchMessage(string line)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(line);
        }
        catch (System.Text.Json.JsonException)
        {
            LogUnparsableMessage(_logger, _server.Id, line);
            return;
        }

        if (message?["id"] is not JsonNode idNode
            || !long.TryParse(idNode.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
        {
            // A notification or server-initiated request: nothing to correlate.
            return;
        }

        if (!_pending.TryRemove(id, out TaskCompletionSource<JsonNode?>? tcs))
        {
            return;
        }

        if (message["error"] is JsonObject error)
        {
            string error_message = error["message"]?.GetValue<string>() ?? "unknown error";
            tcs.TrySetException(new InvalidOperationException($"MCP error: {error_message}"));
            return;
        }

        tcs.TrySetResult(message["result"]);
    }

    private void FaultAllPending(Exception exception)
    {
        foreach (KeyValuePair<long, TaskCompletionSource<JsonNode?>> entry in _pending)
        {
            entry.Value.TrySetException(exception);
        }

        _pending.Clear();
    }

    private async Task DrainStandardErrorAsync(Process process)
    {
        try
        {
            while (true)
            {
                string? line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Length > 0)
                {
                    LogServerStdErr(_logger, _server.Id, line);
                }
            }
        }
#pragma warning disable CA1031 // Diagnostic drain only; swallow.
        catch (Exception)
#pragma warning restore CA1031
        {
            // Ignored: stderr draining is best-effort.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _connected = false;
        FaultAllPending(new ObjectDisposedException(nameof(StdioMcpClient)));

        Process? process = _process;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
#pragma warning disable CA1031 // Best-effort teardown.
            catch (Exception)
#pragma warning restore CA1031
            {
                // Ignored: the process may have already exited.
            }

            process.Dispose();
        }

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Best-effort teardown.
            catch (Exception)
#pragma warning restore CA1031
            {
                // Ignored.
            }
        }

        _writeLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to MCP server '{Server}'.")]
    private static partial void LogConnected(ILogger logger, string server);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP server '{Server}' read loop faulted.")]
    private static partial void LogReadLoopFault(ILogger logger, string server, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MCP server '{Server}' sent an unparsable message: {Line}")]
    private static partial void LogUnparsableMessage(ILogger logger, string server, string line);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[mcp:{Server}] {Line}")]
    private static partial void LogServerStdErr(ILogger logger, string server, string line);
}
