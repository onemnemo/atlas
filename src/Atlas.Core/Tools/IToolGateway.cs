namespace Atlas.Core.Tools;

/// <summary>
/// The single entry point the orchestration layer uses to discover and invoke
/// tools, implementing tool discovery instead of tool dumping (arch §10–§12, §35).
/// </summary>
/// <remarks>
/// <para>
/// The gateway enforces the three-step navigation the architecture mandates for
/// small models:
/// </para>
/// <list type="number">
/// <item><description>
/// <see cref="DiscoverBranches"/> — the model first picks a capability group from
/// a short list, never a flat tool dump.
/// </description></item>
/// <item><description>
/// <see cref="SelectTools"/> — only that branch's tools are revealed, capped to
/// <see cref="ToolScope.MaxToolsPerCall"/> and filtered by permission, gate, and
/// model capability.
/// </description></item>
/// <item><description>
/// <see cref="InvokeAsync"/> — a chosen tool runs under the same scope, with its
/// arguments validated first; the result is always structured.
/// </description></item>
/// </list>
/// <para>
/// Every method takes the <see cref="ToolScope"/> so exposure and execution share
/// one authority decision. Implementations must never reveal or run a tool the
/// scope does not allow.
/// </para>
/// </remarks>
public interface IToolGateway
{
    /// <summary>
    /// Returns the branches that contain at least one tool visible under
    /// <paramref name="scope"/>, each with the count of visible tools.
    /// </summary>
    IReadOnlyList<ToolBranchInfo> DiscoverBranches(ToolScope scope);

    /// <summary>
    /// Returns the tools in <paramref name="branch"/> that are visible under
    /// <paramref name="scope"/>, capped to <see cref="ToolScope.MaxToolsPerCall"/>.
    /// </summary>
    IReadOnlyList<ToolDescriptor> SelectTools(ToolBranch branch, ToolScope scope);

    /// <summary>
    /// Validates and executes <paramref name="invocation"/> under
    /// <paramref name="scope"/>. Returns a rejected result rather than throwing
    /// for an unknown tool, bad arguments, or insufficient authority.
    /// </summary>
    Task<ToolResult> InvokeAsync(
        ToolInvocation invocation,
        ToolScope scope,
        CancellationToken cancellationToken = default);
}
