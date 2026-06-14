using Atlas.Core.Hardware;
using Atlas.Core.Inference;

namespace Atlas.Inference.Configuration;

/// <summary>
/// The built-in model sheet: the default models and role→model bindings,
/// transcribed from <c>model-sheet.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The guiding rule from the model sheet is "do not use bigger models by
/// default": the pattern is <c>tiny model → validate → repair → reduce scope →
/// only then escalate</c>. The bindings below reflect that — a tiny classifier
/// routes, small models do the work, and a larger fallback exists only on
/// high-end hardware for use after repeated validation failure (arch §21).
/// </para>
/// <para>
/// Model <em>names</em> here are logical routing keys. They must match the names
/// the local backend serves (the llama.cpp router preset, or the model a plain
/// <c>llama-server</c> reports). They are not file paths.
/// </para>
/// </remarks>
public static class DefaultModelSheet
{
    // Logical model names — these are the routing keys sent to the backend.
    private const string Qwen3_0_6B = "qwen3-0.6b";
    private const string Qwen3_1_7B = "qwen3-1.7b";
    private const string SmolLM3_3B = "smollm3-3b";
    private const string Qwen3_4B = "qwen3-4b";

    /// <summary>The default model definitions.</summary>
    public static IReadOnlyList<ModelDefinition> Models { get; } =
    [
        new ModelDefinition(Qwen3_0_6B, ModelTier.Tiny),
        new ModelDefinition(Qwen3_1_7B, ModelTier.Small),
        new ModelDefinition(SmolLM3_3B, ModelTier.Small),
        new ModelDefinition(Qwen3_4B, ModelTier.Medium),
    ];

    /// <summary>The default role → model bindings for every hardware tier.</summary>
    public static IReadOnlyList<RoleModelBinding> RoleBindings { get; } = BuildBindings();

    /// <summary>
    /// Populates <paramref name="options"/> with the default sheet for any list
    /// it has left empty, so explicit configuration always wins.
    /// </summary>
    public static void ApplyDefaults(InferenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Models.Count == 0)
        {
            options.Models.AddRange(Models);
        }

        if (options.RoleBindings.Count == 0)
        {
            options.RoleBindings.AddRange(RoleBindings);
        }
    }

    private static List<RoleModelBinding> BuildBindings()
    {
        var bindings = new List<RoleModelBinding>();

        // Classification/extraction roles run on the tiny model across all tiers.
        AddAllTiers(bindings, ModelRole.Router, Qwen3_0_6B);
        AddAllTiers(bindings, ModelRole.RetrievalRouter, Qwen3_0_6B);
        AddAllTiers(bindings, ModelRole.ToolArgumentGenerator, Qwen3_0_6B);
        AddAllTiers(bindings, ModelRole.DiffExplainer, Qwen3_0_6B);

        // Fast worker: the small, quick model everywhere.
        AddAllTiers(bindings, ModelRole.FastWorker, Qwen3_1_7B);

        // Main worker: 1.7B on low-end, 3B from mid-range up (model sheet).
        bindings.Add(new RoleModelBinding(ModelRole.MainWorker, HardwareTier.LowEnd, Qwen3_1_7B));
        bindings.Add(new RoleModelBinding(ModelRole.MainWorker, HardwareTier.MidRange, SmolLM3_3B));
        bindings.Add(new RoleModelBinding(ModelRole.MainWorker, HardwareTier.HighEnd, SmolLM3_3B));

        // Edit planner is structure-aware: prefer the 3B where it is available.
        bindings.Add(new RoleModelBinding(ModelRole.EditPlanner, HardwareTier.LowEnd, Qwen3_1_7B));
        bindings.Add(new RoleModelBinding(ModelRole.EditPlanner, HardwareTier.MidRange, SmolLM3_3B));
        bindings.Add(new RoleModelBinding(ModelRole.EditPlanner, HardwareTier.HighEnd, SmolLM3_3B));

        // Summarizer: small model; 3B on high-end for longer histories.
        bindings.Add(new RoleModelBinding(ModelRole.Summarizer, HardwareTier.LowEnd, Qwen3_1_7B));
        bindings.Add(new RoleModelBinding(ModelRole.Summarizer, HardwareTier.MidRange, Qwen3_1_7B));
        bindings.Add(new RoleModelBinding(ModelRole.Summarizer, HardwareTier.HighEnd, SmolLM3_3B));

        // Model-assisted validation: tiny on low-end (often deterministic-only
        // there anyway), small above (arch §23).
        bindings.Add(new RoleModelBinding(ModelRole.Validator, HardwareTier.LowEnd, Qwen3_0_6B));
        bindings.Add(new RoleModelBinding(ModelRole.Validator, HardwareTier.MidRange, Qwen3_1_7B));
        bindings.Add(new RoleModelBinding(ModelRole.Validator, HardwareTier.HighEnd, Qwen3_1_7B));

        // Fallback exists only on high-end, used after repeated validation failure.
        bindings.Add(new RoleModelBinding(ModelRole.Fallback, HardwareTier.HighEnd, Qwen3_4B));

        return bindings;
    }

    private static void AddAllTiers(List<RoleModelBinding> bindings, ModelRole role, string model)
    {
        bindings.Add(new RoleModelBinding(role, HardwareTier.LowEnd, model));
        bindings.Add(new RoleModelBinding(role, HardwareTier.MidRange, model));
        bindings.Add(new RoleModelBinding(role, HardwareTier.HighEnd, model));
    }
}
