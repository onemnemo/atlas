using System.Numerics;
using Atlas.Core;
using Atlas.Core.Diagnostics;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Studio.Widgets;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Sends requests through the real pipeline and shows the result, status, and
/// warnings — the main way to drive the system by hand.
/// </summary>
internal sealed class ChatPlaygroundScreen : StudioScreen
{
    private static readonly Vector4 UserColor = new(0.55f, 0.78f, 0.95f, 1f);
    private static readonly Vector4 WarnColor = new(0.95f, 0.80f, 0.35f, 1f);
    private static readonly Vector4 ErrColor = new(0.95f, 0.45f, 0.45f, 1f);

    private readonly IAtlasOrchestrator _orchestrator;
    private readonly StudioState _state;
    private readonly TextInputBuffer _input = new();
    private readonly List<Turn> _turns = [];
    private Task<PipelineResult>? _pending;
    private System.Diagnostics.Stopwatch? _pendingTimer;

    public ChatPlaygroundScreen(IAtlasOrchestrator orchestrator, StudioState state)
    {
        _orchestrator = orchestrator;
        _state = state;
    }

    public override string Title => "Chat Playground";

    protected override void RenderBody()
    {
        PumpPendingResult();

        float inputHeight = ImGui.GetFrameHeightWithSpacing() * 3f + ImGui.GetFrameHeightWithSpacing();
        Vector2 avail = ImGui.GetContentRegionAvail();
        float transcriptHeight = Math.Max(80f, avail.Y - inputHeight);

        if (ImGui.BeginChild("transcript", new Vector2(0, transcriptHeight), ImGuiChildFlags.Borders))
        {
            for (int i = 0; i < _turns.Count; i++)
            {
                ImGui.PushID(i);
                RenderTurn(_turns[i]);
                ImGui.PopID();
            }

            if (_pending is not null)
            {
                ImGui.TextDisabled("Atlas is thinking…");
            }

            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }

        ImGui.EndChild();

        bool busy = _pending is not null;
        ImGui.BeginDisabled(busy);
        _input.InputMultiline("##chat_input", new Vector2(-1, ImGui.GetFrameHeightWithSpacing() * 2.5f));
        bool send = ImGui.Button("Send", new Vector2(120, 0));
        ImGui.SameLine();
        if (ImGui.Button("Clear transcript"))
        {
            _turns.Clear();
        }

        ImGui.EndDisabled();

        if (send && !busy && !_input.IsEmpty)
        {
            Submit();
        }
    }

    private void Submit()
    {
        string prompt = _input.Text;
        _input.Clear();
        _turns.Add(Turn.FromUser(prompt));

        var request = new PipelineRequest(TaskIds.ChatResponse, prompt, _state.BuildPermissions());
        _pendingTimer = System.Diagnostics.Stopwatch.StartNew();
        _pending = Task.Run(() => _orchestrator.ExecuteAsync(request));
    }

    private void PumpPendingResult()
    {
        if (_pending is null || !_pending.IsCompleted)
        {
            return;
        }

        Task<PipelineResult> finished = _pending;
        _pending = null;
        double latencyMs = _pendingTimer?.Elapsed.TotalMilliseconds ?? 0;
        _pendingTimer = null;

        if (finished.IsCompletedSuccessfully)
        {
            PipelineResult result = finished.Result;
            _state.LastResult = result;
            _state.Metrics.Record(result.Status, latencyMs);
            _turns.Add(Turn.FromResult(result));
        }
        else
        {
            _state.Metrics.Record(OutcomeStatus.Failed, latencyMs);
            _turns.Add(Turn.FromException(finished.Exception?.GetBaseException().Message ?? "Unknown error"));
        }
    }

    private static void RenderTurn(Turn turn)
    {
        if (turn.IsUser)
        {
            ImGui.TextColored(UserColor, "You");
            turn.Body.Draw();
            ImGui.Spacing();
            return;
        }

        Vector4 statusColor = turn.Status switch
        {
            OutcomeStatus.Success => new Vector4(0.40f, 0.85f, 0.45f, 1f),
            OutcomeStatus.Degraded => WarnColor,
            _ => ErrColor,
        };
        ImGui.TextColored(statusColor, $"Atlas [{turn.Status}]");

        turn.Body.Draw();

        for (int i = 0; i < turn.WarningBlocks.Count; i++)
        {
            ImGui.PushID(i);
            turn.WarningBlocks[i].Draw();
            ImGui.PopID();
        }

        ImGui.Spacing();
    }

    private sealed class Turn
    {
        public SelectableTextBlock Body { get; } = new();
        public List<SelectableTextBlock> WarningBlocks { get; } = [];
        public bool IsUser { get; private init; }
        public OutcomeStatus Status { get; private init; }

        public static Turn FromUser(string text)
        {
            var turn = new Turn { IsUser = true };
            turn.Body.SetText(text);
            return turn;
        }

        public static Turn FromResult(PipelineResult result)
        {
            var turn = new Turn
            {
                IsUser = false,
                Status = result.Status,
            };

            turn.Body.SetText(result.Content ?? string.Empty);

            if (!result.Warnings.IsDefaultOrEmpty)
            {
                foreach (AtlasWarning warning in result.Warnings)
                {
                    var block = new SelectableTextBlock();
                    block.SetText($"⚠ [{warning.Mode}] {warning.Message}");
                    turn.WarningBlocks.Add(block);
                }
            }

            return turn;
        }

        public static Turn FromException(string message)
        {
            var turn = new Turn { IsUser = false, Status = OutcomeStatus.Failed };
            turn.Body.SetText(message);
            return turn;
        }
    }
}
