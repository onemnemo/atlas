using Atlas.Core.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Hardware;

/// <summary>
/// The default <see cref="IHardwareProfiler"/>: detects CPU and memory from the
/// running system and classifies the machine with a <see cref="HardwareTierPolicy"/>
/// (arch §23).
/// </summary>
/// <remarks>
/// <para>
/// Accelerator detection is not yet implemented and always reports
/// <see cref="AcceleratorKind.None"/>; this is the safe default (it only biases
/// classification downward). It is isolated behind <see cref="DetectAccelerator"/>
/// so a real probe — for example shelling out to <c>nvidia-smi</c> or querying
/// platform GPU APIs — can be added later without touching classification.
/// </para>
/// <para>
/// The accelerator probe is a deliberate hand-off point: wiring up real GPU/VRAM
/// detection benefits from native libraries that should be chosen deliberately,
/// so it is left as a clearly-marked extension rather than guessed at.
/// </para>
/// </remarks>
public sealed partial class SystemHardwareProfiler : IHardwareProfiler
{
    private readonly HardwareTierPolicy _policy;
    private readonly ILogger<SystemHardwareProfiler> _logger;

    /// <summary>Creates a profiler with the given tier policy.</summary>
    /// <param name="policy">Tier thresholds; defaults to <see cref="HardwareTierPolicy.Default"/>.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    public SystemHardwareProfiler(
        HardwareTierPolicy? policy = null,
        ILogger<SystemHardwareProfiler>? logger = null)
    {
        _policy = policy ?? HardwareTierPolicy.Default;
        _logger = logger ?? NullLogger<SystemHardwareProfiler>.Instance;
    }

    /// <inheritdoc />
    public HardwareProfile Detect()
    {
        int cores = Environment.ProcessorCount;
        MemoryReading memory = SystemMemoryProbe.Read();
        (AcceleratorKind accelerator, long vramBytes) = DetectAccelerator();

        HardwareTier tier = _policy.Classify(memory.TotalBytes, cores, accelerator);

        var profile = new HardwareProfile(
            Tier: tier,
            LogicalCoreCount: cores,
            TotalSystemMemoryBytes: memory.TotalBytes,
            AvailableSystemMemoryBytes: memory.AvailableBytes,
            Accelerator: accelerator,
            TotalVideoMemoryBytes: vramBytes);

        LogDetected(
            _logger,
            tier,
            cores,
            memory.TotalBytes / (double)(1L << 30),
            memory.AvailableBytes / (double)(1L << 30),
            accelerator);

        return profile;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Detected hardware tier {Tier}: {Cores} cores, {TotalGiB:F1} GiB RAM ({AvailableGiB:F1} GiB available), accelerator {Accelerator}.")]
    private static partial void LogDetected(
        ILogger logger,
        HardwareTier tier,
        int cores,
        double totalGiB,
        double availableGiB,
        AcceleratorKind accelerator);

    /// <summary>
    /// Detects a hardware accelerator. Currently a safe no-op returning
    /// <see cref="AcceleratorKind.None"/>; this is the extension point for real
    /// GPU/NPU detection.
    /// </summary>
    private static (AcceleratorKind Kind, long VramBytes) DetectAccelerator() => (AcceleratorKind.None, 0);
}
