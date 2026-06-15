using Atlas.Composition;
using Atlas.Orchestration;
using Atlas.Studio;
using Atlas.Studio.Logging;
using Atlas.Tools.Mcp;
using Atlas.Tools.WebSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Atlas Studio — an immediate-mode dashboard layered on top of the orchestrator.
//
// Configuration is loaded from appsettings.json (next to the executable) and
// can be overridden with environment variables prefixed ATLAS__
// (double underscore), e.g. ATLAS__BaseUrl=http://localhost:8080.
// The old ATLAS_BASE_URL env var is also respected for backward compatibility.

IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("ATLAS__")
    .Build();

// ATLAS_BASE_URL wins over appsettings for backward compatibility.
string baseUrl = Environment.GetEnvironmentVariable("ATLAS_BASE_URL")
    ?? config["Atlas:BaseUrl"]
    ?? "http://localhost:8080";

var logBuffer = new LogBuffer();

var services = new ServiceCollection();
services.AddSingleton(logBuffer);
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    // Atlas's own logs stay at Information; the chatty framework/HTTP categories
    // (the health poll alone would flood these every few seconds) drop to Warning.
    builder.AddFilter("System", LogLevel.Warning);
    builder.AddFilter("Microsoft", LogLevel.Warning);
    builder.AddFilter("Atlas", LogLevel.Information);
    builder.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
    builder.AddProvider(new LogBufferProvider(logBuffer));
});

services.AddAtlas(inference => inference.BaseUrl = baseUrl);

// Bind tool and chat options from configuration so users can edit appsettings.json
// rather than recompiling. These are applied after AddAtlas(), which registers
// the options instances, so the Configure call overrides the defaults.
services.Configure<WebSearchOptions>(config.GetSection("Atlas:WebSearch"));
services.Configure<McpClientOptions>(config.GetSection("Atlas:Mcp"));
services.Configure<ChatOptions>(config.GetSection("Atlas:Chat"));

using ServiceProvider provider = services.BuildServiceProvider();

using var app = new StudioApp(provider);
app.Start();

var host = new ImGuiHost("Atlas Studio");
host.Run(app.SubmitUI);
