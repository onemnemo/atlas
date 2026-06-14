using System.Numerics;
using Atlas.Core.Hardware;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>The landing screen: backend status, detected hardware, and orientation.</summary>
internal sealed class OverviewScreen : StudioScreen
{
    private static readonly Vector4 Green = new(0.40f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 Red = new(0.95f, 0.45f, 0.45f, 1f);

    private readonly StudioState _state;
    private readonly HardwareProfile _hardware;

    public OverviewScreen(StudioState state, HardwareProfile hardware)
    {
        _state = state;
        _hardware = hardware;
    }

    public override string Title => "Overview";

    protected override void RenderBody()
    {
        ImGui.SeparatorText("Inference backend");
        if (_state.BackendHealthy)
        {
            ImGui.TextColored(Green, "● Reachable");
        }
        else
        {
            ImGui.TextColored(Red, "● Unreachable");
            ImGui.TextDisabled("Start llama-server, then it will turn green automatically.");
        }

        ImGui.Text($"Endpoint: {_state.BaseUrl}");
        if (_state.LastHealthCheck is { } checkedAt)
        {
            ImGui.TextDisabled($"Last check {checkedAt:HH:mm:ss} · {_state.LastHealthLatencyMs:F0} ms");
        }

        if (ImGui.Button("Re-check now"))
        {
            _state.RequestHealthRecheck?.Invoke();
        }

        ImGui.SeparatorText("Hardware");
        Row("Tier", _hardware.Tier.ToString());
        Row("Logical cores", _hardware.LogicalCoreCount.ToString());
        Row("Total RAM", $"{_hardware.TotalSystemMemoryBytes / (double)(1L << 30):F1} GiB");
        Row("Available RAM", $"{_hardware.AvailableSystemMemoryBytes / (double)(1L << 30):F1} GiB");
        Row("Accelerator", _hardware.Accelerator.ToString());
        Row("Max parallel inferences", _hardware.MaxConcurrentInferences.ToString());

        ImGui.SeparatorText("Session activity");
        SessionMetrics m = _state.Metrics;
        if (m.Total == 0)
        {
            ImGui.TextDisabled("No requests yet this session.");
        }
        else
        {
            ImGui.Text($"Requests: {m.Total}");
            ImGui.TextColored(Green, $"  Success: {m.Successes}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.95f, 0.80f, 0.35f, 1f), $"  Degraded: {m.Degraded}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.85f, 0.65f, 0.95f, 1f), $"  Escalated: {m.Escalated}");
            ImGui.SameLine();
            ImGui.TextColored(Red, $"  Failed: {m.Failures}");
            ImGui.Text($"Latency: last {m.LastLatencyMs:F0} ms · avg {m.AverageLatencyMs:F0} ms");
        }

        ImGui.SeparatorText("Getting started");
        ImGui.TextWrapped(
            "Use the Chat Playground to send a request through the full pipeline. " +
            "The Run Inspector shows how the last request was routed and budgeted. " +
            "Models, Task Profiles, and Permissions let you see and adjust how the " +
            "system behaves; Logs streams the live system log.");
    }

    private static void Row(string label, string value)
    {
        ImGui.TextDisabled(label + ":");
        ImGui.SameLine(220);
        ImGui.Text(value);
    }
}
