using System.Collections.ObjectModel;

namespace Atlas.Tools.Mcp;

/// <summary>
/// Top-level configuration for MCP integration: which servers to connect to and
/// the handshake parameters.
/// </summary>
public sealed class McpClientOptions
{
    /// <summary>The configuration section name used when binding from appsettings.</summary>
    public const string SectionName = "Atlas:Mcp";

    /// <summary>The MCP protocol version Atlas advertises during initialize.</summary>
    public string ProtocolVersion { get; set; } = "2024-11-05";

    /// <summary>How long to wait for a single JSON-RPC response before giving up.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>The external servers to connect to.</summary>
    public Collection<McpServerOptions> Servers { get; } = [];
}
