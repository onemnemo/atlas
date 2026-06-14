using Atlas.Core.Hardware;

namespace Atlas.Orchestration;

/// <summary>
/// Scales a task's nominal token budget down for weaker hardware (arch §23).
/// </summary>
/// <remarks>
/// A <see cref="Core.Tasks.TaskProfile.ContextBudgetTokens"/> is a nominal
/// ceiling; the effective budget on a given machine is smaller on low-end
/// hardware, where "context budget" is "very short" and every token is a
/// liability (arch §8, §23). The pipeline shape is unchanged — only this number
/// adapts.
/// </remarks>
public static class HardwareBudgetPolicy
{
    private const int MinimumBudgetTokens = 256;

    /// <summary>
    /// Returns the effective token budget for <paramref name="nominalBudgetTokens"/>
    /// on the given hardware <paramref name="tier"/>.
    /// </summary>
    public static int Scale(int nominalBudgetTokens, HardwareTier tier)
    {
        double factor = tier switch
        {
            HardwareTier.LowEnd => 0.5,
            HardwareTier.MidRange => 0.8,
            HardwareTier.HighEnd => 1.0,
            _ => 0.5,
        };

        int scaled = (int)(nominalBudgetTokens * factor);
        return Math.Max(MinimumBudgetTokens, scaled);
    }
}
