using Atlas.Core.Tools;

namespace Atlas.Tools;

/// <summary>
/// An executable tool: its static <see cref="ToolDescriptor"/> plus the behaviour
/// that runs when it is invoked.
/// </summary>
/// <remarks>
/// This is the implementation-side counterpart to the metadata in
/// <see cref="ToolDescriptor"/>. Built-in tools implement it directly; tools from
/// an external MCP server are wrapped in an adapter that implements it by making a
/// remote call. The gateway treats both identically.
/// </remarks>
public interface ITool
{
    /// <summary>The static definition exposed to the model and the scope policy.</summary>
    ToolDescriptor Descriptor { get; }

    /// <summary>
    /// Runs the tool. Implementations should return a structured
    /// <see cref="ToolResult"/> for expected failures rather than throwing; the
    /// gateway converts unexpected exceptions into a failed result.
    /// </summary>
    Task<ToolResult> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default);
}
