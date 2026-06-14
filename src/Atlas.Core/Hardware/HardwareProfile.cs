namespace Atlas.Core.Hardware;

/// <summary>
/// A snapshot of the machine's detected execution capability (arch §23).
/// </summary>
/// <remarks>
/// <para>
/// A profile is produced by hardware detection at startup and may be refreshed
/// dynamically as load changes — the system should "degrade gracefully when
/// hardware is under load, not fail silently" (arch §23). Because the profile is
/// an immutable snapshot, a refresh produces a <em>new</em> profile rather than
/// mutating an existing one; this keeps any in-flight pipeline run consistent
/// with the profile it started under.
/// </para>
/// <para>
/// This type intentionally records only what the orchestrator needs to make
/// execution-strategy decisions. It is not a full system-information dump.
/// </para>
/// </remarks>
/// <param name="Tier">The capability class this machine falls into.</param>
/// <param name="LogicalCoreCount">Number of logical CPU cores available.</param>
/// <param name="TotalSystemMemoryBytes">Total physical RAM, in bytes.</param>
/// <param name="AvailableSystemMemoryBytes">
/// RAM currently available, in bytes. Used to gate model loading and parallelism.
/// </param>
/// <param name="Accelerator">The inference accelerator available, if any.</param>
/// <param name="TotalVideoMemoryBytes">
/// Total dedicated VRAM in bytes when a GPU accelerator is present; otherwise
/// <c>0</c>. Gates how many models can be resident and how many parallel slots
/// each may use (arch §31.3).
/// </param>
public sealed record HardwareProfile(
    HardwareTier Tier,
    int LogicalCoreCount,
    long TotalSystemMemoryBytes,
    long AvailableSystemMemoryBytes,
    AcceleratorKind Accelerator,
    long TotalVideoMemoryBytes)
{
    /// <summary>
    /// Whether a hardware inference accelerator (a GPU or platform NPU) is
    /// present. When <see langword="false"/>, inference is CPU-bound and
    /// parallelism should be treated conservatively regardless of core count.
    /// </summary>
    public bool HasAccelerator => Accelerator is not AcceleratorKind.None;

    /// <summary>
    /// The maximum number of inference operations the architecture permits to
    /// run concurrently on this profile (arch §17). This is an upper bound for
    /// the orchestrator's concurrency budget, not a target to saturate.
    /// </summary>
    public int MaxConcurrentInferences => Tier switch
    {
        HardwareTier.LowEnd => 1,
        HardwareTier.MidRange => 2,
        HardwareTier.HighEnd => Math.Max(2, Math.Min(LogicalCoreCount / 2, 8)),
        _ => 1,
    };
}

/// <summary>
/// The kind of inference accelerator detected on the host.
/// </summary>
/// <remarks>
/// This is a deliberately small enumeration. Atlas does not need to distinguish
/// every vendor/driver permutation — it only needs enough to decide whether
/// GPU/NPU offload is viable and how aggressive to be with parallelism.
/// </remarks>
public enum AcceleratorKind
{
    /// <summary>No accelerator; inference runs on the CPU.</summary>
    None = 0,

    /// <summary>A CUDA-capable NVIDIA GPU.</summary>
    Cuda = 1,

    /// <summary>An AMD GPU via ROCm/HIP.</summary>
    Rocm = 2,

    /// <summary>A Vulkan-capable GPU (vendor-agnostic offload path).</summary>
    Vulkan = 3,

    /// <summary>Apple-silicon GPU via Metal.</summary>
    Metal = 4,

    /// <summary>A neural processing unit exposed by the platform.</summary>
    Npu = 5,
}
