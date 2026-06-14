using Atlas.Core.Hardware;

namespace Atlas.Hardware;

/// <summary>
/// Detects the host's <see cref="HardwareProfile"/> (arch §23).
/// </summary>
/// <remarks>
/// Detection "should run at startup and update dynamically" (arch §23). A
/// profiler therefore exposes both a one-shot <see cref="Detect"/> for startup
/// and is expected to be cheap enough to re-run when the system wants to react
/// to changing load. Implementations must never throw for a missing probe; they
/// degrade to conservative defaults instead (mis-detecting downward is safe).
/// </remarks>
public interface IHardwareProfiler
{
    /// <summary>
    /// Probes the current machine and classifies it into a hardware tier.
    /// </summary>
    /// <returns>A fresh, immutable snapshot of detected capability.</returns>
    HardwareProfile Detect();
}
