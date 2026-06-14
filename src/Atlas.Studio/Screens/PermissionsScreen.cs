using Atlas.Core.Permissions;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Adjusts the session's permission ceiling and resource gates applied to
/// requests sent from the dashboard (arch §27).
/// </summary>
internal sealed class PermissionsScreen : StudioScreen
{
    private readonly StudioState _state;

    public PermissionsScreen(StudioState state) => _state = state;

    public override string Title => "Permissions";

    protected override void RenderBody()
    {
        ImGui.SeparatorText("Permission level");
        ImGui.TextWrapped(
            "The highest authority requests may exercise this session. Read is the " +
            "always-safe floor; each step up authorises more impactful actions.");

        foreach (PermissionLevel level in Enum.GetValues<PermissionLevel>())
        {
            if (ImGui.RadioButton(level.ToString(), _state.PermissionLevel == level))
            {
                _state.PermissionLevel = level;
            }
        }

        ImGui.SeparatorText("Resource gates");
        ImGui.TextWrapped("Orthogonal capability gates, independent of the level ladder.");

        bool external = _state.GrantExternal;
        if (ImGui.Checkbox("Allow gated external (internet) access", ref external))
        {
            _state.GrantExternal = external;
        }

        bool privateMemory = _state.GrantPrivateMemory;
        if (ImGui.Checkbox("Allow private-memory access", ref privateMemory))
        {
            _state.GrantPrivateMemory = privateMemory;
        }

        ImGui.SeparatorText("Effective state");
        PermissionState effective = _state.BuildPermissions();
        ImGui.Text($"Granted level: {effective.GrantedLevel}");
        ImGui.Text($"Granted gates: {effective.GrantedGates}");
    }
}
