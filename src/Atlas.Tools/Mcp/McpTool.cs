using Atlas.Core.Diagnostics;
using Atlas.Core.Tools;

namespace Atlas.Tools.Mcp;

/// <summary>
/// Adapts one tool on an MCP server to the local <see cref="ITool"/> interface so
/// the gateway can invoke it like any built-in.
/// </summary>
internal sealed class McpTool : ITool
{
    private readonly IMcpClient _client;
    private readonly string _remoteName;

    public McpTool(ToolDescriptor descriptor, string remoteName, IMcpClient client)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(remoteName);
        ArgumentNullException.ThrowIfNull(client);
        Descriptor = descriptor;
        _remoteName = remoteName;
        _client = client;
    }

    public ToolDescriptor Descriptor { get; }

    public async Task<ToolResult> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        McpCallResult result = await _client
            .CallToolAsync(_remoteName, invocation.Arguments, cancellationToken)
            .ConfigureAwait(false);

        return result.IsError
            ? ToolResult.Failed(FailureMode.SubagentFailure, "The MCP tool reported an error.", result.Text)
            : ToolResult.Ok(result.Text);
    }
}
