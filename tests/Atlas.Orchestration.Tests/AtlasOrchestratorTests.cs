using Atlas.Core;
using Atlas.Core.Hardware;
using Atlas.Core.Inference;
using Atlas.Core.Pipeline;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Inference;
using Atlas.Inference.Configuration;
using Atlas.Orchestration;
using Atlas.Orchestration.Routing;
using Atlas.Orchestration.Stages;
using Xunit;

namespace Atlas.Orchestration.Tests;

public sealed class AtlasOrchestratorTests
{
    private static HardwareProfile Hw(HardwareTier tier) =>
        new(tier, 8, 16L << 30, 8L << 30, AcceleratorKind.None, 0);

    private static AtlasOrchestrator BuildOrchestrator(FakeInferenceClient client, HardwareTier tier)
    {
        var resolver = new ConfigurableModelResolver(new InferenceOptions());
        var chatOptions = Microsoft.Extensions.Options.Options.Create(new ChatOptions());
        IPipelineStage<ChatDraftInput, string> drafter = new ChatDrafterStage(resolver, client, chatOptions);
        var route = new ChatRoute(drafter, resolver, chatOptions);

        return new AtlasOrchestrator(
            TaskProfileRegistry.Default,
            new TaskIdRequestRouter(),
            [route],
            Hw(tier));
    }

    private static PipelineRequest ChatRequest(string input = "What is osmosis?") =>
        new(TaskIds.ChatResponse, input);

    [Fact]
    public async Task Clean_reply_returns_success()
    {
        var client = new FakeInferenceClient((req, _) => FakeInferenceClient.Reply(req, "Osmosis is the movement of water."));
        var orchestrator = BuildOrchestrator(client, HardwareTier.MidRange);

        PipelineResult result = await orchestrator.ExecuteAsync(ChatRequest());

        Assert.Equal(OutcomeStatus.Success, result.Status);
        Assert.Equal("Osmosis is the movement of water.", result.Content);
        Assert.Equal("chat.reply.v1", result.OutputType);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Truncated_reply_is_degraded_but_usable()
    {
        var client = new FakeInferenceClient((req, _) =>
            FakeInferenceClient.Reply(req, "Osmosis is the movement of...", FinishReason.Length));
        var orchestrator = BuildOrchestrator(client, HardwareTier.MidRange);

        PipelineResult result = await orchestrator.ExecuteAsync(ChatRequest());

        Assert.Equal(OutcomeStatus.Degraded, result.Status);
        Assert.True(result.HasUsableOutput);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Persistent_backend_error_exhausts_repair_and_escalates_to_user()
    {
        var client = new FakeInferenceClient((req, _) => FakeInferenceClient.Error(req));
        var orchestrator = BuildOrchestrator(client, HardwareTier.MidRange);

        PipelineResult result = await orchestrator.ExecuteAsync(ChatRequest());

        Assert.Equal(OutcomeStatus.Escalated, result.Status);
        Assert.Contains(result.Warnings, w => w.Mode == Core.Diagnostics.FailureMode.RepairLoopExhausted);
        // Chat profile allows 2 retries → 3 total attempts.
        Assert.Equal(3, client.CallCount);
    }

    [Fact]
    public async Task Recovers_by_escalating_to_a_larger_model_on_high_end_hardware()
    {
        // Fail on the small main worker; succeed only once escalated to a larger tier.
        var client = new FakeInferenceClient((req, _) =>
            req.Model.Tier > ModelTier.Small
                ? FakeInferenceClient.Reply(req, "Recovered answer from the larger model.")
                : FakeInferenceClient.Error(req));
        var orchestrator = BuildOrchestrator(client, HardwareTier.HighEnd);

        PipelineResult result = await orchestrator.ExecuteAsync(ChatRequest());

        Assert.True(result.HasUsableOutput);
        Assert.Equal("Recovered answer from the larger model.", result.Content);
        Assert.Contains(client.Requests, r => r.Model.Tier > ModelTier.Small);
    }

    [Fact]
    public async Task Unknown_task_fails_with_explanation()
    {
        var client = new FakeInferenceClient((req, _) => FakeInferenceClient.Reply(req, "unused"));
        var orchestrator = BuildOrchestrator(client, HardwareTier.MidRange);

        PipelineResult result = await orchestrator.ExecuteAsync(new PipelineRequest("does.not.exist", "hello"));

        Assert.Equal(OutcomeStatus.Failed, result.Status);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Cancellation_returns_an_escalated_result_rather_than_throwing()
    {
        var client = new FakeInferenceClient((req, _) => FakeInferenceClient.Reply(req, "unused"));
        var orchestrator = BuildOrchestrator(client, HardwareTier.MidRange);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        PipelineResult result = await orchestrator.ExecuteAsync(ChatRequest(), cts.Token);

        Assert.Equal(OutcomeStatus.Escalated, result.Status);
    }
}
