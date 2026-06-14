using Atlas.Core.Hardware;

namespace Atlas.Hardware;

/// <summary>
/// The thresholds that map raw machine capability onto a
/// <see cref="HardwareTier"/> (arch §23).
/// </summary>
/// <remarks>
/// <para>
/// Classification is intentionally simple and conservative: a machine is only
/// promoted to a higher tier when it comfortably clears the bar on both memory
/// and CPU. The presence of a hardware accelerator promotes the machine to at
/// least mid-range, since GPU/NPU offload changes what is feasible regardless of
/// CPU count.
/// </para>
/// <para>
/// These thresholds are a record so they can be overridden from configuration
/// without changing detection code — tiering policy is data, not logic.
/// </para>
/// </remarks>
/// <param name="MidRangeMinMemoryBytes">Minimum total RAM to qualify as mid-range.</param>
/// <param name="MidRangeMinCores">Minimum logical cores to qualify as mid-range.</param>
/// <param name="HighEndMinMemoryBytes">Minimum total RAM to qualify as high-end.</param>
/// <param name="HighEndMinCores">Minimum logical cores to qualify as high-end.</param>
public sealed record HardwareTierPolicy(
    long MidRangeMinMemoryBytes,
    int MidRangeMinCores,
    long HighEndMinMemoryBytes,
    int HighEndMinCores)
{
    private const long Gigabyte = 1L << 30;

    /// <summary>The default thresholds (mid-range ≥ 8 GB / 4 cores; high-end ≥ 16 GB / 8 cores).</summary>
    public static HardwareTierPolicy Default { get; } = new(
        MidRangeMinMemoryBytes: 8 * Gigabyte,
        MidRangeMinCores: 4,
        HighEndMinMemoryBytes: 16 * Gigabyte,
        HighEndMinCores: 8);

    /// <summary>
    /// Classifies a machine with the given resources into a tier.
    /// </summary>
    /// <param name="totalMemoryBytes">Total physical RAM.</param>
    /// <param name="logicalCores">Logical CPU core count.</param>
    /// <param name="accelerator">The detected accelerator, if any.</param>
    public HardwareTier Classify(long totalMemoryBytes, int logicalCores, AcceleratorKind accelerator)
    {
        bool meetsHighEnd = totalMemoryBytes >= HighEndMinMemoryBytes && logicalCores >= HighEndMinCores;
        if (meetsHighEnd)
        {
            return HardwareTier.HighEnd;
        }

        bool meetsMidRange = totalMemoryBytes >= MidRangeMinMemoryBytes && logicalCores >= MidRangeMinCores;
        if (meetsMidRange || accelerator is not AcceleratorKind.None)
        {
            // A discrete accelerator lifts an otherwise-weak machine to mid-range,
            // but never skips straight to high-end on CPU/RAM grounds alone.
            return HardwareTier.MidRange;
        }

        return HardwareTier.LowEnd;
    }
}
