using Atlas.Core.Tools;

namespace Atlas.Tools;

/// <summary>
/// Human- and model-facing descriptions of each <see cref="ToolBranch"/>, used
/// during branch discovery (arch §10, §35).
/// </summary>
/// <remarks>
/// The description is what the model reads when choosing a capability group, so it
/// is phrased as "what you can do here", not "what this enum means".
/// </remarks>
public static class ToolBranchCatalog
{
    private static readonly Dictionary<ToolBranch, string> Descriptions =
        new()
        {
            [ToolBranch.Notes] = "Search, read, and edit the user's block-based notes.",
            [ToolBranch.Mindmaps] = "Inspect and edit mindmap graphs: nodes, edges, and subtrees.",
            [ToolBranch.Flashcards] = "Search, create, and edit flashcard decks and cards.",
            [ToolBranch.LearningPaths] = "Inspect and edit AI-generated learning paths and their units.",
            [ToolBranch.Files] = "Search uploaded materials and retrieve file outlines and chunks.",
            [ToolBranch.Chats] = "Search and read the user's chat history.",
            [ToolBranch.AppActions] = "Navigate the app: open screens and focus items.",
            [ToolBranch.Settings] = "Read and adjust application settings.",
            [ToolBranch.WebSearch] = "Search the public internet (requires permission).",
            [ToolBranch.External] = "Tools provided by connected external MCP servers.",
        };

    /// <summary>Returns the model-facing description for <paramref name="branch"/>.</summary>
    public static string Describe(ToolBranch branch) =>
        Descriptions.TryGetValue(branch, out string? text) ? text : branch.ToString();
}
