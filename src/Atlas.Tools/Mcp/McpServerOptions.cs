using System.Collections.ObjectModel;
using Atlas.Core.Permissions;
using Atlas.Core.Tools;

namespace Atlas.Tools.Mcp;

/// <summary>
/// Configuration for one external MCP server Atlas should connect to over stdio.
/// </summary>
/// <remarks>
/// <para>
/// An MCP server is launched as a subprocess and spoken to in JSON-RPC over its
/// standard input/output. Each server's tools are folded into the tool tree under
/// the branch and authority declared here — the server cannot grant itself more
/// access than the configuration allows (arch §27).
/// </para>
/// <para>
/// This is plain configuration data so servers can be added or removed without
/// code changes, and so a future UI can manage them.
/// </para>
/// </remarks>
public sealed class McpServerOptions
{
    /// <summary>A stable id for this server; also used as the tool <c>Origin</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The executable to launch (e.g. <c>npx</c>, <c>uvx</c>, or a path).</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Command-line arguments passed to <see cref="Command"/>.</summary>
    public Collection<string> Arguments { get; } = [];

    /// <summary>Optional working directory for the subprocess.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Whether Atlas should connect to this server. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The branch its tools are filed under in the tree. Defaults to <see cref="ToolBranch.External"/>.</summary>
    public ToolBranch Branch { get; set; } = ToolBranch.External;

    /// <summary>The least permission required to use this server's tools.</summary>
    public PermissionLevel RequiredPermission { get; set; } = PermissionLevel.Read;

    /// <summary>An orthogonal resource gate required to use this server's tools.</summary>
    public ResourceGate RequiredGate { get; set; } = ResourceGate.None;
}
