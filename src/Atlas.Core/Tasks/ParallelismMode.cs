namespace Atlas.Core.Tasks;

/// <summary>
/// How much concurrency a task's subagents are permitted to use (arch §17, §24).
/// </summary>
/// <remarks>
/// This is the task's <em>request</em> for parallelism; the effective level is
/// the minimum of this value and what the current <see cref="Hardware.HardwareProfile"/>
/// allows. On low-end hardware "subagents must default to serial execution …
/// and only run in parallel when the hardware profile explicitly permits it"
/// (arch §17).
/// </remarks>
public enum ParallelismMode
{
    /// <summary>Subagents run one at a time. Always safe on any hardware tier.</summary>
    Serial = 0,

    /// <summary>A small bounded number of subagents may run concurrently (≈2).</summary>
    Limited = 1,

    /// <summary>
    /// Subagents may run concurrently up to the hardware concurrency budget,
    /// managed by the coordinator.
    /// </summary>
    Parallel = 2,
}
