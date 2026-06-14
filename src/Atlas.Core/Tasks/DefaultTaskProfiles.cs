using Atlas.Core.Inference;
using Atlas.Core.Permissions;

namespace Atlas.Core.Tasks;

/// <summary>
/// The built-in <see cref="TaskProfile"/> definitions, transcribed from the
/// task-profile table in arch §24.
/// </summary>
/// <remarks>
/// These are conservative, hardware-agnostic ceilings. Context budgets are
/// nominal maxima that the execution layer scales down on weaker hardware
/// (arch §23). Permission levels default to the least authority that still lets
/// the task be useful — generation tasks default to <see cref="PermissionLevel.Draft"/>
/// (user reviews before save) and edit tasks to <see cref="PermissionLevel.Suggest"/>
/// (diff shown) rather than to direct edits.
/// </remarks>
public static class DefaultTaskProfiles
{
    /// <summary>Autocomplete: tiny, instant, no retrieval, no validation (arch §24).</summary>
    public static TaskProfile Autocomplete { get; } = new(
        TaskId: TaskIds.Autocomplete,
        LatencyTarget: LatencyTarget.Fast,
        MinModelTier: ModelTier.Tiny,
        ContextBudgetTokens: 512,
        RetrievalDepth: RetrievalDepth.None,
        ValidationStrictness: ValidationStrictness.None,
        MaxRetries: 0,
        Parallelism: ParallelismMode.Serial,
        PermissionLevel: PermissionLevel.Suggest,
        CitationRequired: false,
        Resumable: false,
        BackgroundAllowed: false);

    /// <summary>Inline rewrite: small model, active document only, schema check (arch §24).</summary>
    public static TaskProfile InlineRewrite { get; } = new(
        TaskId: TaskIds.InlineRewrite,
        LatencyTarget: LatencyTarget.Fast,
        MinModelTier: ModelTier.Small,
        ContextBudgetTokens: 1024,
        RetrievalDepth: RetrievalDepth.Shallow,
        ValidationStrictness: ValidationStrictness.SchemaOnly,
        MaxRetries: 1,
        Parallelism: ParallelismMode.Serial,
        PermissionLevel: PermissionLevel.Suggest,
        CitationRequired: false,
        Resumable: false,
        BackgroundAllowed: false);

    /// <summary>Chat response: small–medium model, retrieval cascade, schema + scope (arch §24).</summary>
    public static TaskProfile ChatResponse { get; } = new(
        TaskId: TaskIds.ChatResponse,
        LatencyTarget: LatencyTarget.Normal,
        MinModelTier: ModelTier.Small,
        ContextBudgetTokens: 4096,
        RetrievalDepth: RetrievalDepth.Moderate,
        ValidationStrictness: ValidationStrictness.Full,
        MaxRetries: 2,
        Parallelism: ParallelismMode.Serial,
        PermissionLevel: PermissionLevel.Read,
        CitationRequired: false,
        Resumable: false,
        BackgroundAllowed: false);

    /// <summary>Flashcard generation: small–medium model, source chunks, full validation (arch §24).</summary>
    public static TaskProfile FlashcardGeneration { get; } = new(
        TaskId: TaskIds.FlashcardGeneration,
        LatencyTarget: LatencyTarget.Normal,
        MinModelTier: ModelTier.Small,
        ContextBudgetTokens: 4096,
        RetrievalDepth: RetrievalDepth.Moderate,
        ValidationStrictness: ValidationStrictness.Full,
        MaxRetries: 2,
        Parallelism: ParallelismMode.Limited,
        PermissionLevel: PermissionLevel.Draft,
        CitationRequired: true,
        Resumable: false,
        BackgroundAllowed: true);

    /// <summary>Learning-path generation: the benchmark task — deep retrieval, paranoid validation (arch §22, §24).</summary>
    public static TaskProfile LearningPathGeneration { get; } = new(
        TaskId: TaskIds.LearningPathGeneration,
        LatencyTarget: LatencyTarget.Slow,
        MinModelTier: ModelTier.Medium,
        ContextBudgetTokens: 8192,
        RetrievalDepth: RetrievalDepth.Deep,
        ValidationStrictness: ValidationStrictness.Paranoid,
        MaxRetries: 3,
        Parallelism: ParallelismMode.Parallel,
        PermissionLevel: PermissionLevel.Draft,
        CitationRequired: true,
        Resumable: true,
        BackgroundAllowed: true);

    /// <summary>Mindmap editing: small model, graph traversal, schema + scope (arch §24).</summary>
    public static TaskProfile MindmapEditing { get; } = new(
        TaskId: TaskIds.MindmapEditing,
        LatencyTarget: LatencyTarget.Normal,
        MinModelTier: ModelTier.Small,
        ContextBudgetTokens: 2048,
        RetrievalDepth: RetrievalDepth.Moderate,
        ValidationStrictness: ValidationStrictness.Full,
        MaxRetries: 2,
        Parallelism: ParallelismMode.Serial,
        PermissionLevel: PermissionLevel.Suggest,
        CitationRequired: false,
        Resumable: false,
        BackgroundAllowed: false);

    /// <summary>File ingestion: tiny model, no retrieval, background, schema check (arch §24).</summary>
    public static TaskProfile FileIngestion { get; } = new(
        TaskId: TaskIds.FileIngestion,
        LatencyTarget: LatencyTarget.Background,
        MinModelTier: ModelTier.Tiny,
        ContextBudgetTokens: 1024,
        RetrievalDepth: RetrievalDepth.None,
        ValidationStrictness: ValidationStrictness.SchemaOnly,
        MaxRetries: 1,
        Parallelism: ParallelismMode.Serial,
        PermissionLevel: PermissionLevel.Read,
        CitationRequired: false,
        Resumable: true,
        BackgroundAllowed: true);

    /// <summary>All built-in profiles, in a stable order.</summary>
    public static IReadOnlyList<TaskProfile> All { get; } =
    [
        Autocomplete,
        InlineRewrite,
        ChatResponse,
        FlashcardGeneration,
        LearningPathGeneration,
        MindmapEditing,
        FileIngestion,
    ];
}
