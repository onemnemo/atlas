using Atlas.Core.Tools;
using Atlas.Tools.Mcp;
using Atlas.Tools.WebSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Tools;

/// <summary>
/// Dependency-injection registration for the scoped tool tree.
/// </summary>
public static class ToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tool gateway, the built-in tools, and the MCP and web-search
    /// sources. Configuration is bound from options; web search defaults to the
    /// disabled backend until a provider is chosen.
    /// </summary>
    public static IServiceCollection AddAtlasTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<McpClientOptions>();
        services.AddOptions<WebSearchOptions>();

        // Web-search backend: a concrete provider when configured, else the
        // disabled backend that returns a clear "not configured" result.
        services.AddHttpClient(SearxngWebSearchBackend.HttpClientName);
        services.TryAddSingleton<IWebSearchBackend>(static provider =>
        {
            WebSearchOptions options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<WebSearchOptions>>().Value;

            return options.Provider switch
            {
                WebSearchProvider.Searxng => ActivatorUtilities.CreateInstance<SearxngWebSearchBackend>(provider),
                _ => new DisabledWebSearchBackend(),
            };
        });

        // Built-in local tools.
        services.AddSingleton<ITool, WebSearchTool>();
        services.AddSingleton<IToolSource, LocalToolSource>();

        // External MCP tools.
        services.AddSingleton<McpToolSource>();
        services.AddSingleton<IToolSource>(static provider => provider.GetRequiredService<McpToolSource>());

        services.AddSingleton<ToolRegistry>();
        services.TryAddSingleton<IToolGateway>(static provider => provider.GetRequiredService<ToolRegistry>());

        return services;
    }
}
