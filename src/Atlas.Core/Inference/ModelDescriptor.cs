namespace Atlas.Core.Inference;

/// <summary>
/// Identifies a concrete model the inference backend can serve (arch §16, §31.3).
/// </summary>
/// <remarks>
/// The <see cref="Name"/> is the key the model server (llama.cpp router mode)
/// uses to select weights for a request (arch §31.3). Atlas only ever holds the
/// name and metadata — it never loads weights itself. Keeping this a small,
/// serializable descriptor means the resolver's role→model mapping can be
/// authored as configuration and changed without recompiling.
/// </remarks>
/// <param name="Name">
/// The model name as registered with the inference server (e.g.
/// <c>"qwen3-0.6b"</c>). This is the routing key, not a file path.
/// </param>
/// <param name="Tier">The capability tier of this model.</param>
/// <param name="SupportsStructuredOutput">
/// Whether the model/server can honour a constrained (grammar/JSON-schema)
/// decoding request. When <see langword="false"/>, structured stages must
/// fall back to prompt-only formatting plus stricter validation.
/// </param>
public sealed record ModelDescriptor(
    string Name,
    ModelTier Tier,
    bool SupportsStructuredOutput = false)
{
    /// <summary>Validates that the descriptor carries a usable model name.</summary>
    /// <exception cref="ArgumentException"><see cref="Name"/> is empty.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("ModelDescriptor.Name must be non-empty.", nameof(Name));
        }
    }
}
