namespace Atlas.Orchestration;

/// <summary>
/// Tunable settings for the chat route (arch §24).
/// </summary>
/// <remarks>
/// These are read at call time, so a host (such as the Studio UI) can adjust the
/// live instance and have it take effect on the next request without a restart.
/// They are policy knobs, not behaviour — the route and stage own the behaviour.
/// </remarks>
public sealed class ChatOptions
{
    /// <summary>The configuration section name used when binding from appsettings.</summary>
    public const string SectionName = "Atlas:Chat";

    /// <summary>Sampling temperature for chat drafting (0 = deterministic, higher = more varied).</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Hard cap on generated output tokens. <c>0</c> means "use the per-task
    /// budget" (the default). Raise this when short answers are being cut off —
    /// small models can spend the budget on reasoning before reaching the answer.
    /// </summary>
    public int MaxOutputTokens { get; set; }

    /// <summary>The system framing prepended to every chat request.</summary>
    public string SystemPrompt { get; set; } =
        "You are Atlas, a careful local study assistant for the mnemo app. " +
        "Answer clearly and concisely. If you are unsure, say so rather than inventing facts.";

    /// <summary>
    /// Maximum number of tool-call rounds in agent mode before the loop is
    /// considered exhausted and the run is escalated.  Defaults to 5, which
    /// is generous while still bounding runaway loops (arch §21).
    /// </summary>
    public int MaxToolIterations { get; set; } = 5;
}
