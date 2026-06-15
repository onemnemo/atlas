using System.Diagnostics;
using Atlas.Core;
using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Core.Tasks;
using Atlas.Core.Tools;
using Atlas.Inference;
using Atlas.Inference.Configuration;
using Atlas.Orchestration;
using Atlas.Studio.Logging;
using Atlas.Studio.Screens;
using Atlas.Tools;
using Atlas.Tools.WebSearch;
using Hexa.NET.ImGui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Atlas.Studio;

/// <summary>
/// The dashboard application: owns the screens, the top menu, and the background
/// backend-health poll. It is a pure consumer of the composed Atlas services.
/// </summary>
internal sealed partial class StudioApp : IDisposable
{
    private readonly StudioState _state;
    private readonly IInferenceHealthProbe _healthProbe;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<StudioApp> _logger;
    private readonly List<StudioScreen> _screens;
    private readonly CancellationTokenSource _cts = new();
    private bool? _lastKnownHealthy;

    public StudioApp(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var orchestrator = provider.GetRequiredService<IAtlasOrchestrator>();
        var hardware = provider.GetRequiredService<HardwareProfile>();
        var resolver = provider.GetRequiredService<IModelResolver>();
        var profiles = provider.GetRequiredService<ITaskProfileProvider>();
        var inferenceOptions = provider.GetRequiredService<IOptions<InferenceOptions>>().Value;
        var chatOptions = provider.GetRequiredService<IOptions<ChatOptions>>().Value;
        var webSearchOptions = provider.GetRequiredService<IOptions<WebSearchOptions>>().Value;
        var logBuffer = provider.GetRequiredService<LogBuffer>();
        var toolGateway = provider.GetRequiredService<IToolGateway>();
        _toolRegistry = provider.GetRequiredService<ToolRegistry>();
        _healthProbe = provider.GetRequiredService<IInferenceHealthProbe>();
        _logger = provider.GetService<ILogger<StudioApp>>() ?? NullLogger<StudioApp>.Instance;

        _state = new StudioState { BaseUrl = inferenceOptions.BaseUrl };
        _state.RequestHealthRecheck = () => _ = CheckHealthOnceAsync(_cts.Token);

        _screens =
        [
            new OverviewScreen(_state, hardware),
            new ChatPlaygroundScreen(orchestrator, _state),
            new RunInspectorScreen(_state, profiles, resolver, hardware),
            new ModelsScreen(inferenceOptions, resolver, hardware),
            new TaskProfilesScreen(profiles),
            new ToolsScreen(toolGateway, _toolRegistry, _state, hardware),
            new SettingsScreen(_state, inferenceOptions, chatOptions, webSearchOptions),
            new PermissionsScreen(_state),
            new LogsScreen(logBuffer),
        ];
    }

    /// <summary>Starts background work: the health poll and the initial tool-tree discovery.</summary>
    public void Start()
    {
        _ = PollHealthAsync(_cts.Token);
        _ = _toolRegistry.RefreshAsync(_cts.Token);
    }

    /// <summary>Renders one frame: the dockspace, the menu bar, and every open screen.</summary>
    public void SubmitUI()
    {
        ImGui.DockSpaceOverViewport();
        RenderMenuBar();

        foreach (StudioScreen screen in _screens)
        {
            screen.Draw();
        }
    }

    private void RenderMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }

        if (ImGui.BeginMenu("View"))
        {
            foreach (StudioScreen screen in _screens)
            {
                if (ImGui.MenuItem(screen.Title, string.Empty, screen.Open))
                {
                    screen.Open = !screen.Open;
                }
            }

            ImGui.EndMenu();
        }

        ImGui.Separator();
        ImGui.TextDisabled(_state.BackendHealthy ? "backend: online" : "backend: offline");

        ImGui.EndMainMenuBar();
    }

    private async Task PollHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CheckHealthOnceAsync(cancellationToken).ConfigureAwait(false);

            int seconds = Math.Clamp(_state.HealthPollSeconds, 1, 60);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CheckHealthOnceAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        bool healthy;
        try
        {
            healthy = await _healthProbe.IsHealthyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        stopwatch.Stop();

        _state.BackendHealthy = healthy;
        _state.LastHealthLatencyMs = stopwatch.Elapsed.TotalMilliseconds;
        _state.LastHealthCheck = DateTime.Now;

        if (_lastKnownHealthy != healthy)
        {
            _lastKnownHealthy = healthy;
            if (healthy)
            {
                LogBackendOnline(_logger, _state.BaseUrl);
            }
            else
            {
                LogBackendOffline(_logger, _state.BaseUrl);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inference backend is reachable at {Url}.")]
    private static partial void LogBackendOnline(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inference backend at {Url} is not reachable — waiting for it to come up.")]
    private static partial void LogBackendOffline(ILogger logger, string url);
}
