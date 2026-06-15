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

        // Shared named HttpClient used by all web-search backends.
        services.AddHttpClient(SearxngWebSearchBackend.HttpClientName);

        // Web-search backend resolved once at startup based on the configured provider.
        // DuckDuckGo is the default — zero setup, no server, no API key.
        services.TryAddSingleton<IWebSearchBackend>(static provider =>
        {
            WebSearchOptions options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<WebSearchOptions>>().Value;

            return options.Provider switch
            {
                WebSearchProvider.DuckDuckGo => ActivatorUtilities.CreateInstance<DuckDuckGoWebSearchBackend>(provider),
                WebSearchProvider.Searxng => ActivatorUtilities.CreateInstance<SearxngWebSearchBackend>(provider),
                WebSearchProvider.Brave => ActivatorUtilities.CreateInstance<BraveWebSearchBackend>(provider),
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
