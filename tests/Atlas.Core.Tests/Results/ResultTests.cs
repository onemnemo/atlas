using Atlas.Core.Diagnostics;
using Atlas.Core.Pipeline;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Degraded_result_must_carry_a_warning()
    {
        Assert.Throws<ArgumentException>(() =>
            PipelineResult.Degraded(Guid.NewGuid(), "content", "text.v1", []));
    }

    [Fact]
    public void Failed_result_must_explain_itself()
    {
        Assert.Throws<ArgumentException>(() =>
            PipelineResult.Failed(Guid.NewGuid(), []));
    }

    [Fact]
    public void Success_result_has_usable_output()
    {
        PipelineResult result = PipelineResult.Success(Guid.NewGuid(), "hello", "chat.reply.v1");

        Assert.True(result.HasUsableOutput);
        Assert.Equal(OutcomeStatus.Success, result.Status);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Failed_result_has_no_usable_output()
    {
        PipelineResult result = PipelineResult.Failed(
            Guid.NewGuid(),
            [AtlasWarning.Error(FailureMode.RepairLoopExhausted, "Could not produce valid output.")]);

        Assert.False(result.HasUsableOutput);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Stage_outcome_success_has_value()
    {
        StageOutcome<int> outcome = StageOutcome.Success(42);

        Assert.True(outcome.HasValue);
        Assert.Equal(42, outcome.Value);
        Assert.Equal(OutcomeStatus.Success, outcome.Status);
    }

    [Fact]
    public void Stage_outcome_degraded_requires_a_warning()
    {
        Assert.Throws<ArgumentException>(() => StageOutcome.Degraded(42));
    }

    [Fact]
    public void Stage_outcome_failed_has_no_value_and_requires_a_warning()
    {
        StageOutcome<string> outcome = StageOutcome.Failed<string>(
            AtlasWarning.Error(FailureMode.MalformedOutput, "Unparseable JSON."));

        Assert.False(outcome.HasValue);
        Assert.Null(outcome.Value);
        Assert.Equal(OutcomeStatus.Failed, outcome.Status);
    }

    [Fact]
    public void Stage_outcome_escalation_can_carry_a_partial_value()
    {
        StageOutcome<string> outcome = StageOutcome.Escalated(
            [AtlasWarning.Caution(FailureMode.RepairLoopExhausted, "Partial result only.")],
            partialValue: "half an answer");

        Assert.Equal(OutcomeStatus.Escalated, outcome.Status);
        Assert.Equal("half an answer", outcome.Value);
    }
}
