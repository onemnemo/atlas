using System.Numerics;
using System.Text.Json.Nodes;
using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Core.Tools;
using Atlas.Studio.Widgets;
using Atlas.Tools;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Browses the scoped tool tree the way the model navigates it — pick a branch,
/// see only that branch's tools — and lets the operator invoke a tool by hand to
/// see exactly what the model would receive (arch §10-§12, §35).
/// </summary>
internal sealed class ToolsScreen : StudioScreen
{
    private const ImGuiTableFlags TableFlags =
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;

    private static readonly ModelTier[] Tiers =
        [ModelTier.Tiny, ModelTier.Small, ModelTier.Medium, ModelTier.Large];

    private readonly IToolGateway _gateway;
    private readonly ToolRegistry _registry;
    private readonly StudioState _state;
    private readonly TextInputBuffer _args = new();
    private readonly SelectableTextBlock _resultBlock = new();

    private int _scopeTierIndex = 1;
    private ToolBranch? _selectedBranch;
    private ToolDescriptor? _selectedTool;
    private ToolResultStatus? _resultStatus;
    private Task<ToolResult>? _pending;
    private Task? _refreshing;

    public ToolsScreen(IToolGateway gateway, ToolRegistry registry, StudioState state, HardwareProfile hardware)
    {
        _gateway = gateway;
        _registry = registry;
        _state = state;
        _ = hardware;
    }

    public override string Title => "Tools";

    private ToolScope BuildScope() => new(_state.BuildPermissions(), Tiers[_scopeTierIndex]);

    protected override void RenderBody()
    {
        PumpPending();
        RenderHeader();
        ImGui.Separator();

        ToolScope scope = BuildScope();
        float branchWidth = 240f;

        if (ImGui.BeginChild("branches", new Vector2(branchWidth, 0), ImGuiChildFlags.Borders))
        {
            RenderBranches(scope);
        }

        ImGui.EndChild();
        ImGui.SameLine();

        if (ImGui.BeginChild("tools", new Vector2(0, 0), ImGuiChildFlags.Borders))
        {
            RenderToolsAndDetail(scope);
        }

        ImGui.EndChild();
    }

    private void RenderHeader()
    {
        ImGui.Text($"Tools loaded: {_registry.Count}");
        ImGui.SameLine();
        bool busy = _refreshing is { IsCompleted: false };
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("Refresh tree"))
        {
            _refreshing = Task.Run(() => _registry.RefreshAsync());
        }

        ImGui.EndDisabled();

        if (busy)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("refreshing…");
        }

        ImGui.SetNextItemWidth(160);
        if (ImGui.BeginCombo("Model capability", Tiers[_scopeTierIndex].ToString()))
        {
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (ImGui.Selectable(Tiers[i].ToString(), i == _scopeTierIndex))
                {
                    _scopeTierIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"scope: {_state.PermissionLevel}, gates [{(_state.GrantExternal ? "web " : "")}{(_state.GrantPrivateMemory ? "memory" : "")}]");
    }

    private void RenderBranches(ToolScope scope)
    {
        ImGui.TextDisabled("Branches");
        IReadOnlyList<ToolBranchInfo> branches = _gateway.DiscoverBranches(scope);
        if (branches.Count == 0)
        {
            ImGui.TextWrapped("No tools are visible under the current scope. Open the web gate in Permissions, or connect an MCP server.");
            return;
        }

        foreach (ToolBranchInfo branch in branches)
        {
            bool selected = _selectedBranch == branch.Branch;
            if (ImGui.Selectable($"{branch.Branch} ({branch.ToolCount})", selected))
            {
                _selectedBranch = branch.Branch;
                _selectedTool = null;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(branch.Description);
            }
        }
    }

    private void RenderToolsAndDetail(ToolScope scope)
    {
        if (_selectedBranch is not { } branch)
        {
            ImGui.TextDisabled("Select a branch to see its tools.");
            return;
        }

        ImGui.TextDisabled($"{branch} tools");
        foreach (ToolDescriptor tool in _gateway.SelectTools(branch, scope))
        {
            bool selected = _selectedTool?.Name == tool.Name;
            if (ImGui.Selectable(tool.Name, selected))
            {
                SelectTool(tool);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tool.Summary);
            }
        }

        if (_selectedTool is not null)
        {
            ImGui.Separator();
            RenderToolDetail(scope);
        }
    }

    private void RenderToolDetail(ToolScope scope)
    {
        ToolDescriptor tool = _selectedTool!;
        ImGui.TextWrapped(tool.Summary);
        ImGui.TextDisabled($"requires: {tool.RequiredPermission}" +
            (tool.RequiredGate == Core.Permissions.ResourceGate.None ? "" : $" + {tool.RequiredGate}") +
            $" · origin: {tool.Origin}");

        if (!tool.Parameters.IsEmpty && ImGui.BeginTable("params", 3, TableFlags))
        {
            ImGui.TableSetupColumn("Argument");
            ImGui.TableSetupColumn("Type");
            ImGui.TableSetupColumn("Required");
            ImGui.TableHeadersRow();

            foreach (ToolParameter parameter in tool.Parameters)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(parameter.Name);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(parameter.Description);
                }

                ImGui.TableNextColumn();
                ImGui.Text(parameter.Type.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(parameter.Required ? "yes" : "no");
            }

            ImGui.EndTable();
        }

        ImGui.Text("Arguments (JSON)");
        _args.InputMultiline("##toolargs", new Vector2(-1, ImGui.GetFrameHeightWithSpacing() * 3f));

        bool busy = _pending is not null;
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("Invoke"))
        {
            Invoke(scope);
        }

        ImGui.EndDisabled();

        if (busy)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("running…");
        }

        if (_resultStatus is { } status)
        {
            ImGui.Separator();
            Vector4 color = status switch
            {
                ToolResultStatus.Ok => new Vector4(0.40f, 0.85f, 0.45f, 1f),
                ToolResultStatus.Rejected => new Vector4(0.95f, 0.80f, 0.35f, 1f),
                _ => new Vector4(0.95f, 0.45f, 0.45f, 1f),
            };
            ImGui.TextColored(color, $"Result: {status}");
            _resultBlock.Draw();
        }
    }

    private void SelectTool(ToolDescriptor tool)
    {
        _selectedTool = tool;
        _resultStatus = null;

        var template = new JsonObject();
        foreach (ToolParameter parameter in tool.Parameters)
        {
            template[parameter.Name] = parameter.Type switch
            {
                ToolParameterType.Integer or ToolParameterType.Number => JsonValue.Create(0),
                ToolParameterType.Boolean => JsonValue.Create(false),
                _ => JsonValue.Create(string.Empty),
            };
        }

        _args.SetText(template.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private void Invoke(ToolScope scope)
    {
        JsonObject arguments;
        try
        {
            arguments = JsonNode.Parse(_args.Text) as JsonObject ?? [];
        }
        catch (System.Text.Json.JsonException ex)
        {
            _resultStatus = ToolResultStatus.Rejected;
            _resultBlock.SetText($"Arguments are not valid JSON: {ex.Message}");
            return;
        }

        var invocation = new ToolInvocation(_selectedTool!.Name, arguments);
        _pending = Task.Run(() => _gateway.InvokeAsync(invocation, scope));
    }

    private void PumpPending()
    {
        if (_pending is null || !_pending.IsCompleted)
        {
            return;
        }

        Task<ToolResult> finished = _pending;
        _pending = null;

        if (finished.IsCompletedSuccessfully)
        {
            ToolResult result = finished.Result;
            _resultStatus = result.Status;
            _resultBlock.SetText(result.IsOk
                ? result.Content
                : $"[{result.Mode}] {result.Message}\n{result.Content}".TrimEnd());
        }
        else
        {
            _resultStatus = ToolResultStatus.Failed;
            _resultBlock.SetText(finished.Exception?.GetBaseException().Message ?? "Unknown error");
        }
    }
}
