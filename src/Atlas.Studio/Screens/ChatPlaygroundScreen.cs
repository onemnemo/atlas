using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Atlas.Core;
using Atlas.Core.Diagnostics;
using Atlas.Core.Pipeline;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Studio.Widgets;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Sends requests through the real pipeline and shows the result, status, and
/// warnings — the main way to drive the system by hand.
/// </summary>
/// <remarks>
/// Supports tool toggles (per-request overrides of session gates), a live
/// activity feed showing what Atlas is doing step by step (routing, searching,
/// fetching URLs, generating), and streamed token display so the reply appears
/// progressively as the model generates it (arch §31.4).
/// </remarks>
internal sealed class ChatPlaygroundScreen : StudioScreen
{
    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Vector4 UserColor = new(0.55f, 0.78f, 0.95f, 1f);
    private static readonly Vector4 AtlasOkColor = new(0.40f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 WarnColor = new(0.95f, 0.80f, 0.35f, 1f);
    private static readonly Vector4 ErrColor = new(0.95f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 SearchColor = new(0.92f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 UrlColor = new(0.50f, 0.85f, 0.78f, 1f);
    private static readonly Vector4 GenerateColor = new(0.65f, 0.90f, 0.65f, 1f);
    private static readonly Vector4 DimColor = new(0.55f, 0.55f, 0.55f, 1f);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly IAtlasOrchestrator _orchestrator;
    private readonly StudioState _state;
    private readonly TextInputBuffer _input = new();
    private readonly List<Turn> _turns = [];

    private Task<PipelineResult>? _pending;
    private Stopwatch? _pendingTimer;

    // Streaming tokens — written from the pipeline task, read on the render thread.
    private readonly object _streamLock = new();
    private readonly StringBuilder _streamBuilder = new();
    private string _streamSnapshot = string.Empty;

    // Activity entries — ConcurrentQueue drains into _activityLog on the render thread.
    private readonly ConcurrentQueue<ActivityEntry> _activityQueue = new();
    private readonly List<ActivityEntry> _activityLog = [];

    public ChatPlaygroundScreen(IAtlasOrchestrator orchestrator, StudioState state)
    {
        _orchestrator = orchestrator;
        _state = state;
    }

    public override string Title => "Chat Playground";

    // ── Main render ───────────────────────────────────────────────────────────

    protected override void RenderBody()
    {
        PumpPendingResult();
        DrainActivityQueue();
        RefreshStreamSnapshot();

        // ── Tools bar (above transcript) ──────────────────────────────────────
        RenderToolsBar();
        ImGui.Separator();

        // ── Transcript ────────────────────────────────────────────────────────
        float activityH = _pending is not null ? ImGui.GetFrameHeightWithSpacing() * 4.0f : 0f;
        float inputH = ImGui.GetFrameHeightWithSpacing() * 3.6f;
        float headerH = ImGui.GetFrameHeightWithSpacing() * 2.2f; // tools bar + separator
        float transcriptH = Math.Max(80f,
            ImGui.GetContentRegionAvail().Y - headerH - activityH - inputH - 6f);

        if (ImGui.BeginChild("transcript", new Vector2(0, transcriptH), ImGuiChildFlags.Borders))
        {
            for (int i = 0; i < _turns.Count; i++)
            {
                ImGui.PushID(i);
                RenderTurn(_turns[i]);
                ImGui.PopID();
            }

            // Live streaming turn — shown while the model is generating.
            if (_pending is not null)
            {
                if (_streamSnapshot.Length > 0)
                {
                    ImGui.TextColored(GenerateColor, "Atlas (generating…)");
                    ImGui.TextWrapped(_streamSnapshot);
                }
                else
                {
                    ImGui.TextDisabled("Atlas is thinking…");
                }
            }

            // Auto-scroll when near the bottom.
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f)
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }

        ImGui.EndChild();

        // ── Activity feed (visible only while a request is in flight) ─────────
        if (_pending is not null)
        {
            RenderActivityFeed();
        }

        // ── Input area ────────────────────────────────────────────────────────
        bool busy = _pending is not null;
        ImGui.BeginDisabled(busy);
        _input.InputMultiline("##chat_input", new Vector2(-1, ImGui.GetFrameHeightWithSpacing() * 2.5f));

        bool send = ImGui.Button("Send", new Vector2(100, 0));
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _turns.Clear();
            lock (_streamLock) { _streamBuilder.Clear(); }
            _activityLog.Clear();
        }

        ImGui.EndDisabled();

        if (send && !busy && !_input.IsEmpty)
        {
            Submit();
        }
    }

    // ── Tools bar ─────────────────────────────────────────────────────────────

    private void RenderToolsBar()
    {
        ImGui.TextDisabled("Tools:");
        ImGui.SameLine();

        // Web Search toggle — mirrors the session's GrantExternal gate so the
        // route sees the change immediately when the checkbox is toggled.
        bool ws = _state.GrantExternal;
        if (ImGui.Checkbox("Web Search", ref ws))
        {
            _state.GrantExternal = ws;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("When enabled, Atlas searches the web before answering.");
            ImGui.Text("URLs will appear in the activity feed while it works.");
            ImGui.Text("Backend: configured in Settings → Web Search.");
            ImGui.EndTooltip();
        }

        // Placeholder slots for future tool branches (Notes, Mindmaps, etc.).
        ImGui.SameLine();
        ImGui.TextDisabled("|  more tools: see Permissions screen");
    }

    // ── Activity feed ─────────────────────────────────────────────────────────

    private void RenderActivityFeed()
    {
        float feedH = ImGui.GetFrameHeightWithSpacing() * 3.8f;

        if (ImGui.BeginChild("activity_feed", new Vector2(0, feedH), ImGuiChildFlags.Borders))
        {
            if (_activityLog.Count == 0)
            {
                ImGui.TextDisabled("Waiting…");
            }
            else
            {
                // Show the last few entries; older ones fade.
                int startIdx = Math.Max(0, _activityLog.Count - 6);
                for (int i = startIdx; i < _activityLog.Count; i++)
                {
                    RenderActivityEntry(_activityLog[i], isLatest: i == _activityLog.Count - 1);
                }
            }

            ImGui.SetScrollHereY(1.0f);
        }

        ImGui.EndChild();
    }

    private static void RenderActivityEntry(ActivityEntry entry, bool isLatest)
    {
        (string prefix, Vector4 color) = entry.Phase switch
        {
            ActivityPhase.Searching => ("[?]", SearchColor),
            ActivityPhase.Retrieving => ("[v]", UrlColor),
            ActivityPhase.Generating => ("[*]", GenerateColor),
            ActivityPhase.Validating => ("[!]", WarnColor),
            _ => ("[>]", DimColor),
        };

        Vector4 labelColor = isLatest ? color : DimColor;
        ImGui.TextColored(labelColor, prefix);
        ImGui.SameLine();
        ImGui.TextColored(labelColor, entry.Message);

        if (entry.Detail is { Length: > 0 })
        {
            ImGui.SameLine();
            // Truncate long URLs for display.
            string detail = entry.Detail.Length > 70
                ? string.Concat("…", entry.Detail.AsSpan(entry.Detail.Length - 67))
                : entry.Detail;
            Vector4 detailColor = isLatest ? UrlColor : new Vector4(0.38f, 0.60f, 0.56f, 1f);
            ImGui.TextColored(detailColor, detail);
        }
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private void Submit()
    {
        string prompt = _input.Text;
        _input.Clear();
        _turns.Add(Turn.FromUser(prompt));

        // Reset all per-request live state.
        lock (_streamLock) { _streamBuilder.Clear(); }
        _streamSnapshot = string.Empty;
        _activityLog.Clear();
        while (_activityQueue.TryDequeue(out _)) { }

        var request = new PipelineRequest(TaskIds.ChatResponse, prompt, _state.BuildPermissions())
        {
            OnToken = token =>
            {
                lock (_streamLock) { _streamBuilder.Append(token); }
            },
            OnActivity = entry => _activityQueue.Enqueue(entry),
        };

        _pendingTimer = Stopwatch.StartNew();
        _pending = Task.Run(() => _orchestrator.ExecuteAsync(request));
    }

    // ── Per-frame pumps ───────────────────────────────────────────────────────

    private void DrainActivityQueue()
    {
        while (_activityQueue.TryDequeue(out ActivityEntry? entry))
        {
            _activityLog.Add(entry);
        }
    }

    private void RefreshStreamSnapshot()
    {
        if (_pending is not null)
        {
            lock (_streamLock)
            {
                if (_streamBuilder.Length > 0)
                {
                    _streamSnapshot = _streamBuilder.ToString();
                }
            }
        }
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

        // Clear streaming state — the final turn replaces it.
        lock (_streamLock) { _streamBuilder.Clear(); }
        _streamSnapshot = string.Empty;

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

    // ── Turn rendering ────────────────────────────────────────────────────────

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
            OutcomeStatus.Success => AtlasOkColor,
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

    // ── Turn model ────────────────────────────────────────────────────────────

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
            var turn = new Turn { IsUser = false, Status = result.Status };
            turn.Body.SetText(result.Content ?? string.Empty);

            if (!result.Warnings.IsDefaultOrEmpty)
            {
                foreach (AtlasWarning warning in result.Warnings)
                {
                    var block = new SelectableTextBlock();
                    block.SetText($"[{warning.Mode}] {warning.Message}");
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
