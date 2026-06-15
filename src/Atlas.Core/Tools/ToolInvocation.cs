using System.Text.Json.Nodes;

namespace Atlas.Core.Tools;

/// <summary>
/// A request to execute one tool with a set of arguments (arch §12, §14).
/// </summary>
/// <remarks>
/// This is the structured form of a model's tool call, after the raw model output
/// has been parsed. Arguments are carried as a <see cref="JsonObject"/> so they
/// can be validated against the <see cref="ToolDescriptor.Parameters"/> and passed
/// verbatim to a local executor or an MCP server without lossy conversions.
/// </remarks>
/// <param name="ToolName">The <see cref="ToolDescriptor.Name"/> being invoked.</param>
/// <param name="Arguments">The argument object; never null (use an empty object).</param>
public sealed record ToolInvocation(string ToolName, JsonObject Arguments)
{
    /// <summary>Creates an invocation with no arguments.</summary>
    public static ToolInvocation Create(string toolName) => new(toolName, []);
}
