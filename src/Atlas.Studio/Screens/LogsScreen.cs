using System.Numerics;
using Atlas.Studio.Logging;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;

namespace Atlas.Studio.Screens;

/// <summary>Streams the live in-memory system log with a minimum-level filter.</summary>
internal sealed class LogsScreen : StudioScreen
{
    private readonly LogBuffer _buffer;
    private bool _autoScroll = true;
    private int _minLevel = (int)LogLevel.Information;

    public LogsScreen(LogBuffer buffer) => _buffer = buffer;

    public override string Title => "Logs";

    protected override void RenderBody()
    {
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _buffer.Clear();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        var level = (LogLevel)_minLevel;
        if (ImGui.BeginCombo("Min level", level.ToString()))
        {
            for (int i = (int)LogLevel.Trace; i <= (int)LogLevel.Critical; i++)
            {
                bool selected = i == _minLevel;
                if (ImGui.Selectable(((LogLevel)i).ToString(), selected))
                {
                    _minLevel = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Separator();

        if (ImGui.BeginChild("log_scroll", new Vector2(0, 0), ImGuiChildFlags.Borders))
        {
            foreach (LogEntry entry in _buffer.Snapshot())
            {
                if ((int)entry.Level < _minLevel)
                {
                    continue;
                }

                ImGui.TextColored(LevelColor(entry.Level), $"{entry.Timestamp:HH:mm:ss} {Short(entry.Level)}");
                ImGui.SameLine();
                ImGui.TextDisabled($"[{entry.Category}]");
                ImGui.SameLine();
                ImGui.TextWrapped(entry.Message);
            }

            if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }

        ImGui.EndChild();
    }

    private static string Short(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    private static Vector4 LevelColor(LogLevel level) => level switch
    {
        LogLevel.Warning => new Vector4(0.95f, 0.80f, 0.35f, 1f),
        LogLevel.Error or LogLevel.Critical => new Vector4(0.95f, 0.45f, 0.45f, 1f),
        LogLevel.Debug or LogLevel.Trace => new Vector4(0.6f, 0.6f, 0.65f, 1f),
        _ => new Vector4(0.55f, 0.78f, 0.95f, 1f),
    };
}
