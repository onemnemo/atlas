using System.Numerics;
using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Inference.Configuration;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Shows the configured model sheet and which concrete model each role resolves
/// to on the current hardware.
/// </summary>
internal sealed class ModelsScreen : StudioScreen
{
    private const ImGuiTableFlags TableFlags =
        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;

    private readonly InferenceOptions _options;
    private readonly IModelResolver _resolver;
    private readonly HardwareProfile _hardware;

    public ModelsScreen(InferenceOptions options, IModelResolver resolver, HardwareProfile hardware)
    {
        _options = options;
        _resolver = resolver;
        _hardware = hardware;
    }

    public override string Title => "Models";

    protected override void RenderBody()
    {
        ImGui.SeparatorText("Declared models");
        if (ImGui.BeginTable("models", 3, TableFlags))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Tier");
            ImGui.TableSetupColumn("Structured output");
            ImGui.TableHeadersRow();

            foreach (ModelDefinition model in _options.Models)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(model.Name);
                ImGui.TableNextColumn();
                ImGui.Text(model.Tier.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(model.SupportsStructuredOutput ? "yes" : "no");
            }

            ImGui.EndTable();
        }

        ImGui.SeparatorText($"Roles resolved for {_hardware.Tier}");
        if (ImGui.BeginTable("roles", 2, TableFlags))
        {
            ImGui.TableSetupColumn("Role");
            ImGui.TableSetupColumn("Model");
            ImGui.TableHeadersRow();

            foreach (ModelRole role in Enum.GetValues<ModelRole>())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(role.ToString());
                ImGui.TableNextColumn();
                try
                {
                    ModelDescriptor model = _resolver.Resolve(role, _hardware);
                    ImGui.Text($"{model.Name} ({model.Tier})");
                }
                catch (ModelResolutionException)
                {
                    ImGui.TextDisabled("—");
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled(
            "Model names are routing keys the backend must serve (the llama.cpp router " +
            "preset, or a llama-server --alias). Editing the sheet from the UI is planned.");
    }
}
