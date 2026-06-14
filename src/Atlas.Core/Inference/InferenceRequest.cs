using System.Collections.Immutable;

namespace Atlas.Core.Inference;

/// <summary>
/// A single, fully-resolved request to the inference backend (arch §31.3).
/// </summary>
/// <remarks>
/// <para>
/// By the time a request reaches the inference client the logical role has
/// already been resolved to a concrete <see cref="Model"/> by the
/// <see cref="IModelResolver"/>. The client is a thin transport: it forwards the
/// request to the model named by <see cref="ModelDescriptor.Name"/> and returns
/// the response. This matches llama.cpp router mode, where "each incoming
/// request specifies a model by name" (arch §31.3).
/// </para>
/// <para>
/// When <see cref="JsonSchema"/> is set and the model supports it, the backend is
/// asked to constrain decoding to that schema — the cheapest way to make small
/// models emit valid structured output (arch §19). It is a hint, not a guarantee:
/// the output is still validated downstream, because models lie even under
/// constraints.
/// </para>
/// </remarks>
/// <param name="Model">The concrete model to serve this request.</param>
/// <param name="Messages">The minimal sufficient messages for this call.</param>
/// <param name="MaxOutputTokens">
/// Hard ceiling on generated tokens, taken from the stage's generation budget.
/// </param>
/// <param name="Temperature">
/// Sampling temperature. Defaults to <c>0</c> for deterministic, repeatable
/// behaviour — the default for routing, extraction, and validation.
/// </param>
/// <param name="StopSequences">Sequences that, if generated, end the response.</param>
/// <param name="JsonSchema">
/// Optional JSON schema to constrain decoding to. Ignored when the resolved
/// model does not support structured output.
/// </param>
public sealed record InferenceRequest(
    ModelDescriptor Model,
    ImmutableArray<InferenceMessage> Messages,
    int MaxOutputTokens,
    double Temperature = 0.0,
    ImmutableArray<string> StopSequences = default,
    string? JsonSchema = null)
{
    /// <summary>Validates the request's invariants before it is sent.</summary>
    /// <exception cref="ArgumentException">The request is malformed.</exception>
    public void Validate()
    {
        Model.Validate();

        if (Messages.IsDefaultOrEmpty)
        {
            throw new ArgumentException("InferenceRequest.Messages must contain at least one message.", nameof(Messages));
        }

        if (MaxOutputTokens <= 0)
        {
            throw new ArgumentException(
                $"InferenceRequest.MaxOutputTokens must be positive (was {MaxOutputTokens}).",
                nameof(MaxOutputTokens));
        }

        if (Temperature is < 0.0 or > 2.0)
        {
            throw new ArgumentException(
                $"InferenceRequest.Temperature must be in [0, 2] (was {Temperature}).",
                nameof(Temperature));
        }
    }
}
