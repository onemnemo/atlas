namespace Atlas.Core.Inference;

/// <summary>
/// The author role of an <see cref="InferenceMessage"/>.
/// </summary>
/// <remarks>
/// These mirror the conventional chat roles understood by llama.cpp's
/// OpenAI-compatible endpoint (arch §31.3). Atlas builds the smallest sufficient
/// set of messages for each call rather than replaying whole histories — context
/// is acquired intentionally per stage (arch §8, §9).
/// </remarks>
public enum MessageRole
{
    /// <summary>System/instruction message that frames the call.</summary>
    System = 0,

    /// <summary>Content originating from the user or an upstream stage.</summary>
    User = 1,

    /// <summary>Prior model output, included only when genuinely needed.</summary>
    Assistant = 2,
}

/// <summary>
/// A single message in an inference request.
/// </summary>
/// <param name="Role">Who authored the message.</param>
/// <param name="Content">The message text.</param>
public sealed record InferenceMessage(MessageRole Role, string Content)
{
    /// <summary>Creates a system message.</summary>
    public static InferenceMessage System(string content) => new(MessageRole.System, content);

    /// <summary>Creates a user message.</summary>
    public static InferenceMessage User(string content) => new(MessageRole.User, content);

    /// <summary>Creates an assistant message.</summary>
    public static InferenceMessage Assistant(string content) => new(MessageRole.Assistant, content);
}
