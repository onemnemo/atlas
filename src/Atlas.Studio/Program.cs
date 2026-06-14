using Atlas.Composition;
using Atlas.Studio;
using Atlas.Studio.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Atlas Studio — an immediate-mode dashboard layered on top of the orchestrator.
//
// Point it at your llama-server with ATLAS_BASE_URL (default http://localhost:8080).

string baseUrl = Environment.GetEnvironmentVariable("ATLAS_BASE_URL") ?? "http://localhost:8080";

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

using ServiceProvider provider = services.BuildServiceProvider();

using var app = new StudioApp(provider);
app.Start();

var host = new ImGuiHost("Atlas Studio");
host.Run(app.SubmitUI);
