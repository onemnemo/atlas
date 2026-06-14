namespace Atlas.Core.Tasks;

/// <summary>
/// The well-known task-type identifiers Atlas ships with (arch §24).
/// </summary>
/// <remarks>
/// These are plain strings rather than an enum on purpose: task types are
/// expected to grow, and profiles are intended to become configuration-driven.
/// A string key lets a new task type be added by registering a profile, without
/// recompiling the contract layer. The constants exist so first-party code gets
/// compile-time checking and a single place to find the canonical spelling.
/// </remarks>
public static class TaskIds
{
    /// <summary>Fast next-token / next-phrase completion in the editor.</summary>
    public const string Autocomplete = "autocomplete";

    /// <summary>Rewrite of the active document or current selection.</summary>
    public const string InlineRewrite = "inline.rewrite";

    /// <summary>General assistant chat response.</summary>
    public const string ChatResponse = "chat.response";

    /// <summary>Generate flashcards from source material.</summary>
    public const string FlashcardGeneration = "flashcard.generation";

    /// <summary>Generate a full, sequenced learning path — the architecture's benchmark task (arch §22).</summary>
    public const string LearningPathGeneration = "learningpath.generation";

    /// <summary>Inspect and edit a mindmap via graph traversal.</summary>
    public const string MindmapEditing = "mindmap.editing";

    /// <summary>Parse, chunk, and index an uploaded file in the background.</summary>
    public const string FileIngestion = "file.ingestion";
}
