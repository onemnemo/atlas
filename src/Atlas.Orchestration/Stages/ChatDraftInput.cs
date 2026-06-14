using Atlas.Core.Inference;

namespace Atlas.Orchestration.Stages;

/// <summary>
/// The input to the <see cref="ChatDrafterStage"/>.
/// </summary>
/// <remarks>
/// The optional overrides are how the route drives the repair loop without the
/// stage needing to know about retries: on a retry the route can hand the stage
/// an escalated model (<see cref="ModelOverride"/>) or a reduced generation
/// ceiling (<see cref="MaxOutputTokensOverride"/>), per the scope-reduction
/// strategy in arch §21.
/// </remarks>
/// <param name="UserInput">The user's message.</param>
/// <param name="SystemPrompt">The system framing for the assistant.</param>
/// <param name="ModelOverride">
/// A specific model to use instead of resolving the main worker (used for
/// escalation retries).
/// </param>
/// <param name="MaxOutputTokensOverride">
/// A reduced output ceiling for a scope-reduced retry; falls back to the stage's
/// budgeted generation tokens when null.
/// </param>
public sealed record ChatDraftInput(
    string UserInput,
    string SystemPrompt,
    ModelDescriptor? ModelOverride = null,
    int? MaxOutputTokensOverride = null);
