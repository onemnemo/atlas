using Atlas.Core.Tasks;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>Lists the registered task profiles and their execution policy.</summary>
internal sealed class TaskProfilesScreen : StudioScreen
{
    private const ImGuiTableFlags TableFlags =
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp |
        ImGuiTableFlags.ScrollX;

    private readonly ITaskProfileProvider _profiles;

    public TaskProfilesScreen(ITaskProfileProvider profiles) => _profiles = profiles;

    public override string Title => "Task Profiles";

    protected override void RenderBody()
    {
        ImGui.TextDisabled($"{_profiles.All.Count} profiles registered (read-only view).");

        if (ImGui.BeginTable("profiles", 8, TableFlags))
        {
            ImGui.TableSetupColumn("Task");
            ImGui.TableSetupColumn("Latency");
            ImGui.TableSetupColumn("Min tier");
            ImGui.TableSetupColumn("Budget");
            ImGui.TableSetupColumn("Retrieval");
            ImGui.TableSetupColumn("Validation");
            ImGui.TableSetupColumn("Retries");
            ImGui.TableSetupColumn("Permission");
            ImGui.TableHeadersRow();

            foreach (TaskProfile profile in _profiles.All)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(profile.TaskId);
                ImGui.TableNextColumn();
                ImGui.Text(profile.LatencyTarget.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.MinModelTier.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.ContextBudgetTokens.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.RetrievalDepth.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.ValidationStrictness.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.MaxRetries.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(profile.PermissionLevel.ToString());
            }

            ImGui.EndTable();
        }
    }
}
