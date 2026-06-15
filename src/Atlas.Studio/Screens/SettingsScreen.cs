using System.Numerics;
using Atlas.Inference.Configuration;
using Atlas.Orchestration;
using Atlas.Studio.Widgets;
using Atlas.Tools.WebSearch;
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
    private readonly WebSearchOptions _webSearch;

    private readonly TextInputBuffer _baseUrl = new(512);
    private readonly TextInputBuffer _systemPrompt = new();
    private readonly TextInputBuffer _searxngUrl = new(512);
    private readonly TextInputBuffer _endpointModel = new(256);
    private readonly TextInputBuffer _endpointUrl = new(512);

    public SettingsScreen(StudioState state, InferenceOptions inference, ChatOptions chat, WebSearchOptions webSearch)
    {
        _state = state;
        _inference = inference;
        _chat = chat;
        _webSearch = webSearch;
        _baseUrl.SetText(inference.BaseUrl);
        _systemPrompt.SetText(chat.SystemPrompt);
        _searxngUrl.SetText(webSearch.BaseUrl);
    }

    public override string Title => "Settings";

    protected override void RenderBody()
    {
        RenderBackendSection();
        RenderChatSection();
        RenderWebSearchSection();
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

    private void RenderWebSearchSection()
    {
        ImGui.SeparatorText("Web search");

        Vector4 statusColor;
        string statusText;

        switch (_webSearch.Provider)
        {
            case WebSearchProvider.DuckDuckGo:
                statusColor = new Vector4(0.40f, 0.85f, 0.45f, 1f);
                statusText = "Active — DuckDuckGo (no setup required)";
                break;

            case WebSearchProvider.Searxng when !string.IsNullOrWhiteSpace(_webSearch.BaseUrl):
                statusColor = new Vector4(0.40f, 0.85f, 0.45f, 1f);
                statusText = $"Active — SearXNG at {_webSearch.BaseUrl}";
                break;

            case WebSearchProvider.Searxng:
                statusColor = new Vector4(0.95f, 0.80f, 0.35f, 1f);
                statusText = "SearXNG selected but no base URL is set";
                break;

            case WebSearchProvider.Brave when !string.IsNullOrWhiteSpace(_webSearch.ApiKey):
                statusColor = new Vector4(0.40f, 0.85f, 0.45f, 1f);
                statusText = "Active — Brave Search (API key set)";
                break;

            case WebSearchProvider.Brave:
                statusColor = new Vector4(0.95f, 0.80f, 0.35f, 1f);
                statusText = "Brave selected but no API key is set";
                break;

            default:
                statusColor = new Vector4(0.95f, 0.45f, 0.45f, 1f);
                statusText = "Disabled (Provider = None)";
                break;
        }

        ImGui.TextColored(statusColor, $"● {statusText}");

        // Live-editable field only makes sense for SearXNG.
        if (_webSearch.Provider == WebSearchProvider.Searxng)
        {
            ImGui.SetNextItemWidth(360);
            _searxngUrl.InputLine("SearXNG base URL");
            ImGui.SameLine();
            if (ImGui.Button("Apply##searxng"))
            {
                _webSearch.BaseUrl = _searxngUrl.Text;
            }
        }

        // Result count controls — live-editable without restart.
        int minR = _webSearch.MinResults;
        if (ImGui.SliderInt("Min results", ref minR, 1, 10))
        {
            _webSearch.MinResults = minR;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Atlas will warn in the activity feed when fewer than this many results are returned.");
        }

        int maxR = _webSearch.DefaultMaxResults;
        if (ImGui.SliderInt("Max results (default)", ref maxR, 1, _webSearch.ResultLimit))
        {
            _webSearch.DefaultMaxResults = maxR;
        }

        int limitR = _webSearch.ResultLimit;
        if (ImGui.SliderInt("Hard result cap", ref limitR, 1, 20))
        {
            _webSearch.ResultLimit = Math.Max(limitR, _webSearch.DefaultMaxResults);
        }

        ImGui.TextDisabled("Change the provider in appsettings.json (next to the executable) and restart.");
        ImGui.TextDisabled("Provider options: DuckDuckGo (default), Searxng, Brave, None");
        ImGui.TextDisabled("Remember to enable the internet gate in Permissions before invoking web search.");
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
