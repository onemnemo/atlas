namespace Atlas.Core.Budgeting;

/// <summary>
/// A concrete token budget for one pipeline run, divided into reserved slots
/// (arch §8).
/// </summary>
/// <remarks>
/// <para>
/// "Every unnecessary token is a liability on weak hardware" (arch §8). The
/// budget is computed once from the task's total context budget and a
/// <see cref="BudgetAllocation"/>, then each pipeline node receives its own
/// scoped slice. A drafter does not inherit the decomposer's context; a
/// validator receives only the output it is checking.
/// </para>
/// <para>
/// Token counts are floored when split so the slots never sum to more than the
/// total. Any rounding remainder is left unallocated rather than risking an
/// overflow on constrained hardware.
/// </para>
/// </remarks>
public sealed record ContextBudget
{
    private ContextBudget(
        int totalTokens,
        int systemOverheadTokens,
        int taskInstructionTokens,
        int retrievedContentTokens,
        int generationTokens)
    {
        TotalTokens = totalTokens;
        SystemOverheadTokens = systemOverheadTokens;
        TaskInstructionTokens = taskInstructionTokens;
        RetrievedContentTokens = retrievedContentTokens;
        GenerationTokens = generationTokens;
    }

    /// <summary>The total token budget for the run.</summary>
    public int TotalTokens { get; }

    /// <summary>Tokens reserved for prompt structure and stage instructions.</summary>
    public int SystemOverheadTokens { get; }

    /// <summary>Tokens reserved for the specific task instruction.</summary>
    public int TaskInstructionTokens { get; }

    /// <summary>Tokens available for retrieved content (RAG, memory, document sections).</summary>
    public int RetrievedContentTokens { get; }

    /// <summary>Tokens reserved for the model's generated output.</summary>
    public int GenerationTokens { get; }

    /// <summary>
    /// Creates a budget by applying <paramref name="allocation"/> to a total token
    /// count.
    /// </summary>
    /// <param name="totalTokens">The total budget; must be positive.</param>
    /// <param name="allocation">The slot proportions; defaults to <see cref="BudgetAllocation.Default"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalTokens"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="allocation"/> is malformed.</exception>
    public static ContextBudget Create(int totalTokens, BudgetAllocation? allocation = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalTokens);

        BudgetAllocation alloc = allocation ?? BudgetAllocation.Default;
        alloc.Validate();

        return new ContextBudget(
            totalTokens,
            (int)(totalTokens * alloc.SystemOverhead),
            (int)(totalTokens * alloc.TaskInstruction),
            (int)(totalTokens * alloc.RetrievedContent),
            (int)(totalTokens * alloc.GenerationSpace));
    }
}
