using System.Collections.Immutable;
using Atlas.Core.Inference;
using Atlas.Core.Permissions;
using Atlas.Core.Tools;
using Atlas.Tools;

namespace Atlas.Tools.Tests;

/// <summary>A deterministic, in-memory tool for exercising the registry.</summary>
internal sealed class FakeTool : ITool
{
    private readonly Func<ToolInvocation, ToolResult> _behaviour;

    public FakeTool(ToolDescriptor descriptor, Func<ToolInvocation, ToolResult>? behaviour = null)
    {
        Descriptor = descriptor;
        _behaviour = behaviour ?? (static _ => ToolResult.Ok("ok"));
    }

    public ToolDescriptor Descriptor { get; }

    public int InvocationCount { get; private set; }

    public Task<ToolResult> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        return Task.FromResult(_behaviour(invocation));
    }

    public static ToolDescriptor Descriptor_(
        string name,
        ToolBranch branch = ToolBranch.Notes,
        PermissionLevel permission = PermissionLevel.Read,
        ResourceGate gate = ResourceGate.None,
        ModelTier tier = ModelTier.Tiny,
        ImmutableArray<ToolParameter> parameters = default) =>
        new(name, branch, $"summary of {name}", parameters, permission, gate, tier);
}

/// <summary>A synchronous tool source over a fixed set of tools.</summary>
internal sealed class FakeToolSource : IToolSource
{
    private readonly IReadOnlyList<ITool> _tools;

    public FakeToolSource(string name, params ITool[] tools)
    {
        Name = name;
        _tools = tools;
    }

    public string Name { get; }

    public ValueTask<IReadOnlyList<ITool>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_tools);
}
