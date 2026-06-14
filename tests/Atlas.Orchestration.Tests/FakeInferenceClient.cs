using System.Collections.Concurrent;
using Atlas.Core.Inference;

namespace Atlas.Orchestration.Tests;

/// <summary>
/// A deterministic, offline <see cref="IInferenceClient"/> for orchestration
/// tests. It records every request and returns whatever the supplied responder
/// dictates, so tests can simulate clean replies, backend errors, truncation,
/// and recovery-after-escalation without a model.
/// </summary>
internal sealed class FakeInferenceClient : IInferenceClient
{
    private readonly Func<InferenceRequest, int, InferenceResponse> _responder;
    private int _callCount;

    public FakeInferenceClient(Func<InferenceRequest, int, InferenceResponse> responder) =>
        _responder = responder;

    public ConcurrentQueue<InferenceRequest> Requests { get; } = new();

    public int CallCount => _callCount;

    public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue(request);
        int index = Interlocked.Increment(ref _callCount) - 1;
        return Task.FromResult(_responder(request, index));
    }

    public static InferenceResponse Reply(InferenceRequest request, string text, FinishReason finish = FinishReason.Stop) =>
        new(text, finish, new TokenUsage(10, 5), request.Model.Name);

    public static InferenceResponse Error(InferenceRequest request) =>
        new(string.Empty, FinishReason.Error, default, request.Model.Name);
}
