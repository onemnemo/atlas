namespace Atlas.Core.Memory;

/// <summary>
/// The scoped layers of memory Atlas distinguishes (arch §9).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Critical rule (arch §9, §29):</strong> memory is <em>never</em>
/// automatically injected into model context. Every item from any layer must be
/// intentionally retrieved by the pipeline in response to a specific need.
/// Automatic injection is the primary cause of context pollution. The layers
/// below describe <em>where</em> something lives and <em>how</em> it is
/// accessed — they are not an invitation to concatenate everything into a prompt.
/// </para>
/// <para>
/// Each layer has a different lifetime and access pattern; see the per-member
/// documentation. The two "always available without a gate" layers
/// (<see cref="ImmediateContext"/> and <see cref="SessionSummary"/>) are still
/// subject to the token budget — being available is not the same as being free.
/// </para>
/// </remarks>
public enum MemoryLayer
{
    /// <summary>
    /// Recent messages, the active note, selected text, current screen. Lifetime:
    /// the current turn. Always present (but budgeted).
    /// </summary>
    ImmediateContext = 0,

    /// <summary>
    /// Compressed task history, key decisions, and current state for the session.
    /// Lifetime: the current session. Auto-retrieved (but budgeted).
    /// </summary>
    SessionSummary = 1,

    /// <summary>
    /// Temporary working state for a single pipeline run. Lifetime: one pipeline
    /// execution. Scoped strictly to that run and discarded afterwards.
    /// </summary>
    TaskLocalScratch = 2,

    /// <summary>
    /// User/project memory: preferences, goals, learning level, recurring topics,
    /// style. Lifetime: long-term. Gated retrieval — guarded by
    /// <see cref="Permissions.ResourceGate.PrivateMemory"/>.
    /// </summary>
    UserProjectMemory = 3,

    /// <summary>
    /// Knowledge memory: notes, files, flashcards, mindmaps, generated artifacts.
    /// Lifetime: long-term. Queried on demand.
    /// </summary>
    KnowledgeMemory = 4,

    /// <summary>
    /// Structural memory: relationships between concepts, notes, and topics.
    /// Lifetime: long-term. Accessed by graph traversal.
    /// </summary>
    StructuralMemory = 5,

    /// <summary>
    /// Long-term chat memory: past conversations, decisions, previous outputs.
    /// Lifetime: long-term. Accessed by semantic search.
    /// </summary>
    LongTermChatMemory = 6,
}
