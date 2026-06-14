using Atlas.Core.Hardware;
using Atlas.Core.Inference;

namespace Atlas.Inference.Configuration;

/// <summary>
/// A model the inference backend can serve, as declared in configuration.
/// </summary>
/// <param name="Name">The model name used as the routing key (arch §31.3).</param>
/// <param name="Tier">The capability tier of the model.</param>
/// <param name="SupportsStructuredOutput">Whether the backend can constrain this model's decoding to a schema.</param>
public sealed record ModelDefinition(string Name, ModelTier Tier, bool SupportsStructuredOutput = true);

/// <summary>
/// Binds a logical role to a concrete model on a specific hardware tier
/// (the model sheet, expressed as data).
/// </summary>
/// <param name="Role">The role being assigned.</param>
/// <param name="Tier">The hardware tier this binding applies to.</param>
/// <param name="Model">The model name to use for the role on that tier.</param>
public sealed record RoleModelBinding(ModelRole Role, HardwareTier Tier, string Model);

/// <summary>
/// Configuration for the inference transport and model resolution (arch §31.3).
/// </summary>
/// <remarks>
/// <para>
/// The defaults target a single local <c>llama-server</c> at
/// <see cref="BaseUrl"/>. To run several models as separate processes, add
/// per-model overrides to <see cref="ModelEndpoints"/>; to use router mode,
/// leave them empty and point <see cref="BaseUrl"/> at the router. The rest of
/// Atlas is unaffected either way.
/// </para>
/// <para>
/// When <see cref="Models"/> and <see cref="RoleBindings"/> are left empty, the
/// <see cref="DefaultModelSheet"/> populates them, so the system has a working
/// model map out of the box.
/// </para>
/// </remarks>
public sealed class InferenceOptions
{
    /// <summary>The configuration section name used when binding from appsettings.</summary>
    public const string SectionName = "Atlas:Inference";

    /// <summary>The default endpoint serving models that have no explicit override.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Per-model base-URL overrides (model name → base URL), for multi-process serving.</summary>
    public Dictionary<string, string> ModelEndpoints { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The models the backend can serve. Populated from the default sheet when empty.</summary>
    public List<ModelDefinition> Models { get; } = [];

    /// <summary>The role → model bindings per hardware tier. Populated from the default sheet when empty.</summary>
    public List<RoleModelBinding> RoleBindings { get; } = [];

    /// <summary>Per-request timeout, in seconds.</summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>Relative path of the backend's health endpoint.</summary>
    public string HealthPath { get; set; } = "/health";

    /// <summary>Relative path of the OpenAI-compatible chat-completions endpoint.</summary>
    public string ChatCompletionsPath { get; set; } = "/v1/chat/completions";

    /// <summary>
    /// Returns the base URL that serves <paramref name="modelName"/>: a per-model
    /// override when present, otherwise <see cref="BaseUrl"/>.
    /// </summary>
    public string ResolveEndpoint(string modelName) =>
        ModelEndpoints.TryGetValue(modelName, out string? url) ? url : BaseUrl;
}
