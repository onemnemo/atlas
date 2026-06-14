using Atlas.Core.Budgeting;
using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Orchestration;
using Hexa.NET.ImGui;

namespace Atlas.Studio.Screens;

/// <summary>
/// Shows how a chat request is routed and budgeted on this hardware, and the
/// outcome of the most recent run.
/// </summary>
/// <remarks>
/// This is the "pipeline trace" view. It currently reflects the static shape of
/// the chat route (profile, hardware-scaled budget, resolved models) plus the
/// last result; per-stage timing capture is a planned enhancement once the
/// orchestrator emits a structured run trace.
/// </remarks>
internal sealed class RunInspectorScreen : StudioScreen
{
    private readonly StudioState _state;
    private readonly ITaskProfileProvider _profiles;
    private readonly IModelResolver _resolver;
    private readonly HardwareProfile _hardware;

    public RunInspectorScreen(
        StudioState state,
        ITaskProfileProvider profiles,
        IModelResolver resolver,
        HardwareProfile hardware)
    {
        _state = state;
        _profiles = profiles;
        _resolver = resolver;
        _hardware = hardware;
    }

    public override string Title => "Run Inspector";

    protected override void RenderBody()
    {
        ImGui.SeparatorText("Chat route");

        if (_profiles.TryGet(TaskIds.ChatResponse, out TaskProfile? profile))
        {
            int effective = HardwareBudgetPolicy.Scale(profile.ContextBudgetTokens, _hardware.Tier);
            ContextBudget budget = ContextBudget.Create(effective);

            ImGui.Text($"Profile: {profile.TaskId}");
            ImGui.Text($"Latency target: {profile.LatencyTarget}    Max retries: {profile.MaxRetries}");
            ImGui.Text($"Nominal budget: {profile.ContextBudgetTokens} tokens");
            ImGui.Text($"Effective budget ({_hardware.Tier}): {budget.TotalTokens} tokens");
            ImGui.BulletText($"System overhead: {budget.SystemOverheadTokens}");
            ImGui.BulletText($"Task instruction: {budget.TaskInstructionTokens}");
            ImGui.BulletText($"Retrieved content: {budget.RetrievedContentTokens}");
            ImGui.BulletText($"Generation: {budget.GenerationTokens}");
        }
        else
        {
            ImGui.TextDisabled("No chat profile registered.");
        }

        ImGui.SeparatorText("Resolved models");
        ResolvedRow("Router", ModelRole.Router);
        ResolvedRow("Main worker", ModelRole.MainWorker);
        ResolvedRow("Validator", ModelRole.Validator);
        if (_resolver.TryResolveEscalation(ModelRole.MainWorker, _hardware, out ModelDescriptor? escalated) && escalated is not null)
        {
            ImGui.Text($"Escalation: {escalated.Name} ({escalated.Tier})");
        }
        else
        {
            ImGui.TextDisabled("Escalation: none available on this hardware");
        }

        ImGui.SeparatorText("Last result");
        if (_state.LastResult is { } result)
        {
            ImGui.Text($"Status: {result.Status}");
            ImGui.Text($"Request: {result.RequestId}");
            if (!result.Warnings.IsDefaultOrEmpty)
            {
                foreach (var w in result.Warnings)
                {
                    ImGui.BulletText($"[{w.Severity}/{w.Mode}] {w.Message}");
                }
            }
        }
        else
        {
            ImGui.TextDisabled("No run yet. Send a request from the Chat Playground.");
        }
    }

    private void ResolvedRow(string label, ModelRole role)
    {
        try
        {
            ModelDescriptor model = _resolver.Resolve(role, _hardware);
            ImGui.Text($"{label}: {model.Name} ({model.Tier})");
        }
        catch (ModelResolutionException)
        {
            ImGui.TextDisabled($"{label}: unbound on this hardware");
        }
    }
}
