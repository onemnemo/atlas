using System.Text.Json.Nodes;
using Atlas.Core.Permissions;
using Atlas.Core.Tools;
using Atlas.Tools;
using Atlas.Tools.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Atlas.Tools.Tests;

public sealed class McpToolSourceTests
{
    private static McpClientOptions OptionsWithServer(McpServerOptions server)
    {
        var options = new McpClientOptions();
        options.Servers.Add(server);
        return options;
    }

    [Fact]
    public async Task ListToolsAsync_Wraps_Remote_Tools_Under_Server_Authority()
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["q"] = new JsonObject { ["type"] = "string" } },
            ["required"] = new JsonArray("q"),
        };
        var fake = new FakeMcpClient([new McpToolInfo("search", "Search the web", schema)]);
        var server = new McpServerOptions
        {
            Id = "tavily",
            Command = "noop",
            Branch = ToolBranch.WebSearch,
            RequiredGate = ResourceGate.GatedExternal,
        };

        var source = new McpToolSource(
            Options.Create(OptionsWithServer(server)),
            NullLoggerFactory.Instance,
            (_, _, _) => fake);

        IReadOnlyList<ITool> tools = await source.ListToolsAsync();

        ITool tool = Assert.Single(tools);
        Assert.Equal("tavily.search", tool.Descriptor.Name);
        Assert.Equal(ToolBranch.WebSearch, tool.Descriptor.Branch);
        Assert.Equal(ResourceGate.GatedExternal, tool.Descriptor.RequiredGate);
        Assert.Equal("tavily", tool.Descriptor.Origin);
        ToolParameter parameter = Assert.Single(tool.Descriptor.Parameters);
        Assert.Equal("q", parameter.Name);
        Assert.True(parameter.Required);

        await source.DisposeAsync();
    }

    [Fact]
    public async Task Tool_Invocation_Calls_Remote_With_Original_Name()
    {
        var fake = new FakeMcpClient([new McpToolInfo("search", "Search", null)]);
        var server = new McpServerOptions { Id = "srv", Command = "noop" };
        var source = new McpToolSource(
            Options.Create(OptionsWithServer(server)),
            NullLoggerFactory.Instance,
            (_, _, _) => fake);

        IReadOnlyList<ITool> tools = await source.ListToolsAsync();
        ToolResult result = await tools[0].ExecuteAsync(new ToolInvocation("srv.search", new JsonObject { ["q"] = "x" }));

        Assert.Equal(ToolResultStatus.Ok, result.Status);
        Assert.Equal("search", fake.LastCalledTool);
        Assert.Equal("called search", result.Content);

        await source.DisposeAsync();
    }

    [Fact]
    public async Task ListToolsAsync_Skips_Disabled_Server()
    {
        var fake = new FakeMcpClient([new McpToolInfo("search", "Search", null)]);
        var server = new McpServerOptions { Id = "srv", Command = "noop", Enabled = false };
        var source = new McpToolSource(
            Options.Create(OptionsWithServer(server)),
            NullLoggerFactory.Instance,
            (_, _, _) => fake);

        IReadOnlyList<ITool> tools = await source.ListToolsAsync();

        Assert.Empty(tools);
        await source.DisposeAsync();
    }
}
