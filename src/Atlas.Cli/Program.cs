using Atlas.Composition;
using Atlas.Core;
using Atlas.Core.Hardware;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Hardware;
using Atlas.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Atlas CLI — a minimal host for verifying the pipeline against real models.
//
//   atlas hw                  Print the detected hardware profile.
//   atlas health              Check whether the inference backend is reachable.
//   atlas chat "your prompt"  Run a chat request end-to-end.
//   atlas "your prompt"       Shorthand for chat.
//
// The inference base URL defaults to http://localhost:8080 and can be overridden
// with the ATLAS_BASE_URL environment variable.

string baseUrl = Environment.GetEnvironmentVariable("ATLAS_BASE_URL") ?? "http://localhost:8080";

var services = new ServiceCollection();
services.AddLogging(builder => builder
    .SetMinimumLevel(LogLevel.Information)
    .AddFilter("System", LogLevel.Warning)
    .AddFilter("Microsoft", LogLevel.Warning)
    .AddFilter("Atlas", LogLevel.Information)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
services.AddAtlas(inference => inference.BaseUrl = baseUrl);

using ServiceProvider provider = services.BuildServiceProvider();

string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

return command switch
{
    "hw" or "hardware" => RunHardware(provider),
    "health" => await RunHealthAsync(provider),
    "chat" => await RunChatAsync(provider, string.Join(' ', args.Skip(1))),
    "help" or "--help" or "-h" => PrintHelp(),
    _ => await RunChatAsync(provider, string.Join(' ', args)),
};

static int RunHardware(IServiceProvider provider)
{
    HardwareProfile profile = provider.GetRequiredService<IHardwareProfiler>().Detect();
    Console.WriteLine($"Tier:        {profile.Tier}");
    Console.WriteLine($"Cores:       {profile.LogicalCoreCount}");
    Console.WriteLine($"Total RAM:   {profile.TotalSystemMemoryBytes / (double)(1L << 30):F1} GiB");
    Console.WriteLine($"Avail RAM:   {profile.AvailableSystemMemoryBytes / (double)(1L << 30):F1} GiB");
    Console.WriteLine($"Accelerator: {profile.Accelerator}");
    Console.WriteLine($"Max parallel inferences: {profile.MaxConcurrentInferences}");
    return 0;
}

static async Task<int> RunHealthAsync(IServiceProvider provider)
{
    IInferenceHealthProbe probe = provider.GetRequiredService<IInferenceHealthProbe>();
    bool healthy = await probe.IsHealthyAsync();
    Console.WriteLine(healthy ? "Inference backend: HEALTHY" : "Inference backend: UNREACHABLE");
    return healthy ? 0 : 1;
}

static async Task<int> RunChatAsync(IServiceProvider provider, string prompt)
{
    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("No prompt provided. Usage: atlas chat \"your question\"");
        return 2;
    }

    IAtlasOrchestrator orchestrator = provider.GetRequiredService<IAtlasOrchestrator>();
    PipelineResult result = await orchestrator.ExecuteAsync(new PipelineRequest(TaskIds.ChatResponse, prompt));

    Console.WriteLine();
    Console.WriteLine($"[{result.Status}]");
    if (result.Content is not null)
    {
        Console.WriteLine(result.Content);
    }

    if (!result.Warnings.IsDefaultOrEmpty)
    {
        Console.WriteLine();
        Console.WriteLine("Warnings:");
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  - [{warning.Severity}/{warning.Mode}] {warning.Message}");
        }
    }

    return result.HasUsableOutput ? 0 : 1;
}

static int PrintHelp()
{
    Console.WriteLine("Atlas CLI");
    Console.WriteLine("  atlas hw                  Print the detected hardware profile.");
    Console.WriteLine("  atlas health              Check whether the inference backend is reachable.");
    Console.WriteLine("  atlas chat \"your prompt\"  Run a chat request end-to-end.");
    Console.WriteLine("  atlas \"your prompt\"       Shorthand for chat.");
    Console.WriteLine();
    Console.WriteLine("Set ATLAS_BASE_URL to point at your llama-server (default http://localhost:8080).");
    return 0;
}
