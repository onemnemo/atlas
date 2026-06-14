namespace Atlas.Core.Budgeting;

/// <summary>
/// The proportion of a token budget assigned to each slot (arch §8).
/// </summary>
/// <remarks>
/// <para>
/// Every task entering the pipeline is assigned a token budget at the entry
/// gate, divided into reserved slots so that retrieved content can never crowd
/// out the room reserved for the model's own output. The fractions must sum to
/// <c>1.0</c>. The defaults sit inside the ranges recommended in arch §8.
/// </para>
/// <para>
/// This is policy data, kept separate from the <see cref="ContextBudget"/> it
/// parameterises so that different task types (or hardware tiers) can carry
/// different allocations without changing the budgeting logic.
/// </para>
/// </remarks>
/// <param name="SystemOverhead">Prompt structure, formatting, stage instructions (arch §8: 10–15%).</param>
/// <param name="TaskInstruction">The specific instruction for this model call (arch §8: 10–20%).</param>
/// <param name="RetrievedContent">RAG chunks, memory items, document sections (arch §8: 40–55%).</param>
/// <param name="GenerationSpace">Reserved for model output tokens (arch §8: 20–30%).</param>
public sealed record BudgetAllocation(
    double SystemOverhead,
    double TaskInstruction,
    double RetrievedContent,
    double GenerationSpace)
{
    private const double Tolerance = 0.0001;

    /// <summary>The default allocation, centred within the arch §8 ranges.</summary>
    public static BudgetAllocation Default { get; } = new(
        SystemOverhead: 0.12,
        TaskInstruction: 0.18,
        RetrievedContent: 0.45,
        GenerationSpace: 0.25);

    /// <summary>
    /// Validates that the fractions are each in <c>[0, 1]</c> and sum to <c>1.0</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The allocation is malformed.</exception>
    public void Validate()
    {
        foreach ((string name, double value) in new[]
                 {
                     (nameof(SystemOverhead), SystemOverhead),
                     (nameof(TaskInstruction), TaskInstruction),
                     (nameof(RetrievedContent), RetrievedContent),
                     (nameof(GenerationSpace), GenerationSpace),
                 })
        {
            if (value is < 0.0 or > 1.0)
            {
                throw new ArgumentException($"Budget fraction '{name}' must be in [0, 1] (was {value}).");
            }
        }

        double sum = SystemOverhead + TaskInstruction + RetrievedContent + GenerationSpace;
        if (Math.Abs(sum - 1.0) > Tolerance)
        {
            throw new ArgumentException($"Budget fractions must sum to 1.0 (summed to {sum}).");
        }
    }
}
