namespace Atlas.Core.Tools;

/// <summary>
/// A compact summary of one tool branch, returned during branch discovery so the
/// model can pick a capability group before seeing any concrete tools (arch §10,
/// §35).
/// </summary>
/// <param name="Branch">The capability group.</param>
/// <param name="Description">A short, model-facing description of the group.</param>
/// <param name="ToolCount">How many tools in this branch are visible under the current scope.</param>
public sealed record ToolBranchInfo(ToolBranch Branch, string Description, int ToolCount);
