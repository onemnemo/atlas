namespace Atlas.Core.Pipeline;

/// <summary>
/// A single, replaceable node in the guarded pipeline graph (arch §6, §16, §25).
/// </summary>
/// <remarks>
/// <para>
/// A stage transforms a typed input into a typed output under a scoped
/// <see cref="StageContext"/>. The typed input/output pair, together with the
/// stage's <see cref="StageDescriptor.OutputContract"/>, is the "clear
/// input/output contract from the start" the architecture requires so that any
/// node can be "replaced with a trained model later without touching the
/// surrounding architecture" (arch §16).
/// </para>
/// <para>
/// Implementations must not throw for anticipated failure. They report it via a
/// <see cref="StageOutcome{TOutput}"/> with the appropriate
/// <see cref="Results.OutcomeStatus"/> and warnings (arch §26). Exceptions are
/// reserved for true programming errors.
/// </para>
/// <para>
/// The same interface covers deterministic stages (validators, assemblers,
/// budgeters) and model-backed stages (router, drafter, summarizer). Whether a
/// stage uses a model is declared by its descriptor, not by its type — keeping
/// "deterministic code does as much as possible" (arch §19) a composition choice
/// rather than a structural one.
/// </para>
/// </remarks>
/// <typeparam name="TInput">The stage's input type.</typeparam>
/// <typeparam name="TOutput">The stage's output type, described by its output contract.</typeparam>
public interface IPipelineStage<in TInput, TOutput>
{
    /// <summary>The stage's self-description, including its output contract.</summary>
    StageDescriptor Descriptor { get; }

    /// <summary>
    /// Executes the stage against <paramref name="input"/> within
    /// <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The scoped execution environment for this stage.</param>
    /// <param name="input">The typed input to transform.</param>
    /// <param name="cancellationToken">Cancels the stage (e.g. on a latency-gate timeout).</param>
    /// <returns>The stage's typed outcome, never throwing for anticipated failure.</returns>
    ValueTask<StageOutcome<TOutput>> ExecuteAsync(
        StageContext context,
        TInput input,
        CancellationToken cancellationToken = default);
}
