namespace Atlas.Core.Tools;

/// <summary>
/// A top-level capability group in the tool tree (arch §10, §11, §35).
/// </summary>
/// <remarks>
/// <para>
/// The tool tree exists to solve the tool-overload problem: a small model must
/// never see 30–50 flat tools at once. Instead it first identifies the
/// <em>branch</em> (capability group) it needs, and only then receives that
/// branch's small, scoped tool set (target 4–6 tools per call, arch §11).
/// </para>
/// <para>
/// These branches mirror mnemo's content domains plus the cross-cutting groups
/// from arch §10. New branches can be added without changing the navigation
/// logic, which keys off this taxonomy alone.
/// </para>
/// </remarks>
public enum ToolBranch
{
    /// <summary>Search, read, and edit block-based notes (arch §13).</summary>
    Notes = 0,

    /// <summary>Inspect and edit mindmap graph documents (arch §12 mindmaps).</summary>
    Mindmaps = 1,

    /// <summary>Search, generate, and edit flashcard decks and cards.</summary>
    Flashcards = 2,

    /// <summary>Inspect and edit AI-generated learning paths and their units.</summary>
    LearningPaths = 3,

    /// <summary>Search and chunk uploaded materials and files.</summary>
    Files = 4,

    /// <summary>Read and search the user's chat history.</summary>
    Chats = 5,

    /// <summary>Navigate and drive the application UI (open screens, focus items).</summary>
    AppActions = 6,

    /// <summary>Read and adjust application settings.</summary>
    Settings = 7,

    /// <summary>Search the public internet. Always gated (arch §27).</summary>
    WebSearch = 8,

    /// <summary>
    /// Tools that do not map to a first-class mnemo domain — typically supplied by
    /// an external MCP server until classified into a specific branch.
    /// </summary>
    External = 9,
}
