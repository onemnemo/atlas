using System.Text.Json.Nodes;
using Atlas.Tools.Mcp;

namespace Atlas.Tools.Tests;

/// <summary>An in-memory <see cref="IMcpClient"/> that needs no subprocess.</summary>
internal sealed class FakeMcpClient : IMcpClient
{
    private readonly IReadOnlyList<McpToolInfo> _tools;

    public FakeMcpClient(IReadOnlyList<McpToolInfo> tools) => _tools = tools;

    public bool IsConnected { get; private set; }

    public string? LastCalledTool { get; private set; }

    public JsonObject? LastArguments { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_tools);

    public Task<McpCallResult> CallToolAsync(string toolName, JsonObject arguments, CancellationToken cancellationToken = default)
    {
        LastCalledTool = toolName;
        LastArguments = arguments;
        return Task.FromResult(new McpCallResult(IsError: false, Text: $"called {toolName}"));
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
