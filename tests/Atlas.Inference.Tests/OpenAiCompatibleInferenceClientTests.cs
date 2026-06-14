using System.Net;
using Atlas.Core.Inference;
using Atlas.Inference;
using Atlas.Inference.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Atlas.Inference.Tests;

public sealed class OpenAiCompatibleInferenceClientTests
{
    private static InferenceRequest SampleRequest() => new(
        new ModelDescriptor("qwen3-1.7b", ModelTier.Small),
        [InferenceMessage.System("You are a helpful tutor."), InferenceMessage.User("Explain osmosis.")],
        MaxOutputTokens: 128);

    private static OpenAiCompatibleInferenceClient ClientWith(StubHttpMessageHandler handler, InferenceOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        return new OpenAiCompatibleInferenceClient(httpClient, Options.Create(options ?? new InferenceOptions()));
    }

    [Fact]
    public async Task Successful_response_is_mapped()
    {
        const string Json = """
        {
          "model": "qwen3-1.7b",
          "choices": [ { "message": { "role": "assistant", "content": "Osmosis is..." }, "finish_reason": "stop" } ],
          "usage": { "prompt_tokens": 20, "completion_tokens": 8 }
        }
        """;
        StubHttpMessageHandler handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, Json);
        OpenAiCompatibleInferenceClient client = ClientWith(handler);

        InferenceResponse response = await client.CompleteAsync(SampleRequest());

        Assert.Equal("Osmosis is...", response.Text);
        Assert.Equal(FinishReason.Stop, response.FinishReason);
        Assert.Equal(20, response.Usage.PromptTokens);
        Assert.Equal(8, response.Usage.CompletionTokens);
        Assert.True(response.StoppedCleanly);
    }

    [Fact]
    public async Task Request_targets_the_configured_endpoint_and_model()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """{ "choices": [ { "message": { "content": "ok" }, "finish_reason": "stop" } ] }""");
        OpenAiCompatibleInferenceClient client = ClientWith(handler);

        await client.CompleteAsync(SampleRequest());

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("http://localhost:8080/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("qwen3-1.7b", handler.LastRequestBody);
        Assert.Contains("Explain osmosis.", handler.LastRequestBody);
    }

    [Fact]
    public async Task Per_model_endpoint_override_is_used()
    {
        var options = new InferenceOptions();
        options.ModelEndpoints["qwen3-1.7b"] = "http://localhost:9001";
        StubHttpMessageHandler handler = StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """{ "choices": [ { "message": { "content": "ok" }, "finish_reason": "stop" } ] }""");
        OpenAiCompatibleInferenceClient client = ClientWith(handler, options);

        await client.CompleteAsync(SampleRequest());

        Assert.StartsWith("http://localhost:9001/", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Backend_error_degrades_rather_than_throwing()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "{}");
        OpenAiCompatibleInferenceClient client = ClientWith(handler);

        InferenceResponse response = await client.CompleteAsync(SampleRequest());

        Assert.Equal(FinishReason.Error, response.FinishReason);
        Assert.False(response.StoppedCleanly);
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        OpenAiCompatibleInferenceClient client = ClientWith(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteAsync(SampleRequest(), cts.Token));
    }
}
