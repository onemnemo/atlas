using System.Numerics;
using Atlas.Inference.Configuration;
using Atlas.Orchestration;
using Atlas.Studio.Widgets;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Live, editable system settings: where the backend is, how it is polled, and
/// how the chat route behaves.
/// </summary>
/// <remarks>
/// Edits mutate the same options instances the running services read at call
/// time, so changes take effect on the next request without a restart. The one
/// exception (called out in the UI) is the request timeout, which is bound to the
/// HTTP client at construction.
/// </remarks>
internal sealed class SettingsScreen : StudioScreen
{
    private const ImGuiTableFlags TableFlags =
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;

    private readonly StudioState _state;
    private readonly InferenceOptions _inference;
    private readonly ChatOptions _chat;

    private readonly TextInputBuffer _baseUrl = new(512);
    private readonly TextInputBuffer _systemPrompt = new();
    private readonly TextInputBuffer _endpointModel = new(256);
    private readonly TextInputBuffer _endpointUrl = new(512);

    public SettingsScreen(StudioState state, InferenceOptions inference, ChatOptions chat)
    {
        _state = state;
        _inference = inference;
        _chat = chat;
        _baseUrl.SetText(inference.BaseUrl);
        _systemPrompt.SetText(chat.SystemPrompt);
    }

    public override string Title => "Settings";

    protected override void RenderBody()
    {
        RenderBackendSection();
        RenderChatSection();
        RenderEndpointOverrides();
    }

    private void RenderBackendSection()
    {
        ImGui.SeparatorText("Backend");

        ImGui.SetNextItemWidth(360);
        _baseUrl.InputLine("Base URL");
        ImGui.SameLine();
        if (ImGui.Button("Apply##baseurl"))
        {
            _inference.BaseUrl = _baseUrl.Text;
            _state.BaseUrl = _baseUrl.Text;
            _state.RequestHealthRecheck?.Invoke();
        }

        int poll = _state.HealthPollSeconds;
        if (ImGui.SliderInt("Health poll (seconds)", ref poll, 1, 60))
        {
            _state.HealthPollSeconds = poll;
        }

        int timeout = _inference.RequestTimeoutSeconds;
        if (ImGui.SliderInt("Request timeout (seconds)", ref timeout, 5, 600))
        {
            _inference.RequestTimeoutSeconds = timeout;
        }

        ImGui.TextDisabled("Timeout applies to HTTP clients created after this change.");

        if (ImGui.Button("Re-check backend now"))
        {
            _state.RequestHealthRecheck?.Invoke();
        }

        ImGui.SameLine();
        string latency = _state.LastHealthCheck is { } t
            ? $"last check {t:HH:mm:ss} ({_state.LastHealthLatencyMs:F0} ms)"
            : "no check yet";
        ImGui.TextDisabled(latency);
    }

    private void RenderChatSection()
    {
        ImGui.SeparatorText("Chat route");

        var temperature = (float)_chat.Temperature;
        if (ImGui.SliderFloat("Temperature", ref temperature, 0f, 1.5f))
        {
            _chat.Temperature = temperature;
        }

        int maxTokens = _chat.MaxOutputTokens;
        if (ImGui.SliderInt("Max output tokens (0 = auto)", ref maxTokens, 0, 8192))
        {
            _chat.MaxOutputTokens = maxTokens;
        }

        ImGui.TextDisabled("Raise this if short answers get cut off mid-sentence.");

        ImGui.Text("System prompt");
        _systemPrompt.InputMultiline("##systemprompt", new Vector2(-1, ImGui.GetFrameHeightWithSpacing() * 3f));
        if (ImGui.Button("Apply##prompt"))
        {
            _chat.SystemPrompt = _systemPrompt.Text;
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset##prompt"))
        {
            _chat.SystemPrompt = new ChatOptions().SystemPrompt;
            _systemPrompt.SetText(_chat.SystemPrompt);
        }
    }

    private void RenderEndpointOverrides()
    {
        ImGui.SeparatorText("Per-model endpoint overrides");
        ImGui.TextDisabled("Route a specific model to its own server (otherwise the base URL is used).");

        if (_inference.ModelEndpoints.Count > 0 && ImGui.BeginTable("endpoints", 2, TableFlags))
        {
            ImGui.TableSetupColumn("Model");
            ImGui.TableSetupColumn("Endpoint");
            ImGui.TableHeadersRow();

            foreach (KeyValuePair<string, string> entry in _inference.ModelEndpoints)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(entry.Key);
                ImGui.TableNextColumn();
                ImGui.Text(entry.Value);
            }

            ImGui.EndTable();
        }

        ImGui.SetNextItemWidth(180);
        _endpointModel.InputLine("Model##ep");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(260);
        _endpointUrl.InputLine("URL##ep");
        ImGui.SameLine();
        if (ImGui.Button("Set##ep") && !_endpointModel.IsEmpty && !_endpointUrl.IsEmpty)
        {
            _inference.ModelEndpoints[_endpointModel.Text] = _endpointUrl.Text;
            _endpointModel.Clear();
            _endpointUrl.Clear();
        }
    }
}
