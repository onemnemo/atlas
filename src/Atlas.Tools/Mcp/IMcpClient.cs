using System.Text.Json.Nodes;

namespace Atlas.Tools.Mcp;

/// <summary>
/// One tool advertised by an MCP server, as returned by <c>tools/list</c>.
/// </summary>
/// <param name="Name">The remote tool name.</param>
/// <param name="Description">The server's description of the tool.</param>
/// <param name="InputSchema">The JSON Schema object describing the tool's arguments, if any.</param>
public sealed record McpToolInfo(string Name, string Description, JsonObject? InputSchema);

/// <summary>
/// The result of an MCP <c>tools/call</c>, flattened to text.
/// </summary>
/// <param name="IsError">Whether the server reported the call as an error.</param>
/// <param name="Text">The concatenated text content blocks of the result.</param>
public sealed record McpCallResult(bool IsError, string Text);

/// <summary>
/// A client for a single MCP server: performs the initialize handshake, lists
/// tools, and calls them over JSON-RPC.
/// </summary>
/// <remarks>
/// Implementations own the transport (a subprocess, in the stdio case) and must
/// be disposed to release it. Methods are expected to be called after a successful
/// <see cref="ConnectAsync"/>; calling before connecting should connect lazily or
/// throw a clear error.
/// </remarks>
public interface IMcpClient : IAsyncDisposable
{
    /// <summary>Whether the client has completed the initialize handshake.</summary>
    bool IsConnected { get; }

    /// <summary>Launches the transport and performs the MCP initialize handshake.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the tools the server advertises.</summary>
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>Invokes a remote tool by name with the given arguments.</summary>
    Task<McpCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default);
}
