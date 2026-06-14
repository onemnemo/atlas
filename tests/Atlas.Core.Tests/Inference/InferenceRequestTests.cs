using Atlas.Core.Inference;
using Xunit;

namespace Atlas.Core.Tests.Inference;

public sealed class InferenceRequestTests
{
    private static ModelDescriptor SampleModel => new("qwen3-0.6b", ModelTier.Tiny);

    [Fact]
    public void Valid_request_passes_validation()
    {
        var request = new InferenceRequest(
            SampleModel,
            [InferenceMessage.System("Route this request."), InferenceMessage.User("Improve my note.")],
            MaxOutputTokens: 64);

        request.Validate();
    }

    [Fact]
    public void Request_with_no_messages_is_rejected()
    {
        var request = new InferenceRequest(SampleModel, [], MaxOutputTokens: 64);
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_output_budget_is_rejected(int maxTokens)
    {
        var request = new InferenceRequest(SampleModel, [InferenceMessage.User("hi")], maxTokens);
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.5)]
    public void Out_of_range_temperature_is_rejected(double temperature)
    {
        var request = new InferenceRequest(SampleModel, [InferenceMessage.User("hi")], 64, temperature);
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Model_descriptor_requires_a_name()
    {
        var descriptor = new ModelDescriptor("   ", ModelTier.Small);
        Assert.Throws<ArgumentException>(descriptor.Validate);
    }

    [Fact]
    public void Token_usage_totals_correctly()
    {
        var usage = new TokenUsage(PromptTokens: 100, CompletionTokens: 40);
        Assert.Equal(140, usage.TotalTokens);
    }
}
