using System.Text.Json.Nodes;
using Atlas.Core.Diagnostics;
using Atlas.Core.Inference;
using Atlas.Core.Permissions;
using Atlas.Core.Tools;
using Atlas.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Tools.Tests;

public sealed class ToolRegistryTests
{
    private static async Task<ToolRegistry> BuildAsync(params ITool[] tools)
    {
        var registry = new ToolRegistry(
            [new FakeToolSource("local", tools)],
            NullLogger<ToolRegistry>.Instance);
        await registry.RefreshAsync();
        return registry;
    }

    [Fact]
    public async Task DiscoverBranches_Hides_Branches_With_No_Visible_Tools()
    {
        var web = new FakeTool(FakeTool.Descriptor_("web.search", ToolBranch.WebSearch, gate: ResourceGate.GatedExternal));
        var note = new FakeTool(FakeTool.Descriptor_("notes.search", ToolBranch.Notes));
        ToolRegistry registry = await BuildAsync(web, note);

        IReadOnlyList<ToolBranchInfo> branches = registry.DiscoverBranches(ToolScope.ReadOnly);

        Assert.Contains(branches, b => b.Branch == ToolBranch.Notes);
        Assert.DoesNotContain(branches, b => b.Branch == ToolBranch.WebSearch);
    }

    [Fact]
    public async Task DiscoverBranches_Reveals_Gated_Branch_When_Gate_Open()
    {
        var web = new FakeTool(FakeTool.Descriptor_("web.search", ToolBranch.WebSearch, gate: ResourceGate.GatedExternal));
        ToolRegistry registry = await BuildAsync(web);

        var scope = new ToolScope(PermissionState.ReadOnly.WithGate(ResourceGate.GatedExternal));
        IReadOnlyList<ToolBranchInfo> branches = registry.DiscoverBranches(scope);

        Assert.Contains(branches, b => b.Branch == ToolBranch.WebSearch && b.ToolCount == 1);
    }

    [Fact]
    public async Task SelectTools_Hides_Tools_Above_Model_Capability()
    {
        var risky = new FakeTool(FakeTool.Descriptor_("notes.edit", ToolBranch.Notes, tier: ModelTier.Medium));
        var safe = new FakeTool(FakeTool.Descriptor_("notes.search", ToolBranch.Notes, tier: ModelTier.Tiny));
        ToolRegistry registry = await BuildAsync(risky, safe);

        var tinyScope = new ToolScope(PermissionState.ReadOnly, ModelTier.Tiny);
        IReadOnlyList<ToolDescriptor> visible = registry.SelectTools(ToolBranch.Notes, tinyScope);

        Assert.Single(visible);
        Assert.Equal("notes.search", visible[0].Name);
    }

    [Fact]
    public async Task SelectTools_Caps_At_MaxToolsPerCall()
    {
        ITool[] tools = Enumerable.Range(0, 10)
            .Select(i => (ITool)new FakeTool(FakeTool.Descriptor_($"notes.t{i}", ToolBranch.Notes)))
            .ToArray();
        ToolRegistry registry = await BuildAsync(tools);

        var scope = new ToolScope(PermissionState.ReadOnly, ModelTier.Small, MaxToolsPerCall: 4);
        IReadOnlyList<ToolDescriptor> visible = registry.SelectTools(ToolBranch.Notes, scope);

        Assert.Equal(4, visible.Count);
    }

    [Fact]
    public async Task InvokeAsync_Rejects_Unknown_Tool()
    {
        ToolRegistry registry = await BuildAsync();

        ToolResult result = await registry.InvokeAsync(ToolInvocation.Create("nope"), ToolScope.ReadOnly);

        Assert.Equal(ToolResultStatus.Rejected, result.Status);
        Assert.Equal(FailureMode.BrokenReference, result.Mode);
    }

    [Fact]
    public async Task InvokeAsync_Rejects_When_Permission_Insufficient()
    {
        var edit = new FakeTool(FakeTool.Descriptor_("notes.edit", ToolBranch.Notes, permission: PermissionLevel.DirectEdit));
        ToolRegistry registry = await BuildAsync(edit);

        ToolResult result = await registry.InvokeAsync(ToolInvocation.Create("notes.edit"), ToolScope.ReadOnly);

        Assert.Equal(ToolResultStatus.Rejected, result.Status);
        Assert.Equal(FailureMode.PermissionDenied, result.Mode);
        Assert.Equal(0, edit.InvocationCount);
    }

    [Fact]
    public async Task InvokeAsync_Rejects_Missing_Required_Argument()
    {
        var tool = new FakeTool(FakeTool.Descriptor_(
            "notes.search",
            ToolBranch.Notes,
            parameters: [ToolParameter.Text("query", "the query")]));
        ToolRegistry registry = await BuildAsync(tool);

        ToolResult result = await registry.InvokeAsync(ToolInvocation.Create("notes.search"), ToolScope.ReadOnly);

        Assert.Equal(ToolResultStatus.Rejected, result.Status);
        Assert.Equal(FailureMode.MalformedOutput, result.Mode);
        Assert.Equal(0, tool.InvocationCount);
    }

    [Fact]
    public async Task InvokeAsync_Runs_Tool_When_Arguments_Valid()
    {
        var tool = new FakeTool(
            FakeTool.Descriptor_("notes.search", ToolBranch.Notes, parameters: [ToolParameter.Text("query", "the query")]),
            invocation => ToolResult.Ok(invocation.Arguments["query"]!.GetValue<string>()));
        ToolRegistry registry = await BuildAsync(tool);

        var invocation = new ToolInvocation("notes.search", new JsonObject { ["query"] = "photosynthesis" });
        ToolResult result = await registry.InvokeAsync(invocation, ToolScope.ReadOnly);

        Assert.Equal(ToolResultStatus.Ok, result.Status);
        Assert.Equal("photosynthesis", result.Content);
        Assert.Equal(1, tool.InvocationCount);
    }

    [Fact]
    public async Task InvokeAsync_Converts_Tool_Exception_Into_Failed_Result()
    {
        var tool = new FakeTool(
            FakeTool.Descriptor_("notes.search", ToolBranch.Notes),
            static _ => throw new InvalidOperationException("boom"));
        ToolRegistry registry = await BuildAsync(tool);

        ToolResult result = await registry.InvokeAsync(ToolInvocation.Create("notes.search"), ToolScope.ReadOnly);

        Assert.Equal(ToolResultStatus.Failed, result.Status);
        Assert.Equal(FailureMode.SubagentFailure, result.Mode);
    }
}
