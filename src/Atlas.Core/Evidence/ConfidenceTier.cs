namespace Atlas.Core.Evidence;

/// <summary>
/// A coarse confidence band for a finding (arch §18).
/// </summary>
/// <remarks>
/// A tier is used rather than a raw float because small models cannot produce
/// calibrated probabilities, and a deterministic validator reasoning over
/// "high/medium/low" is more robust than one comparing arbitrary decimals. When
/// a numeric score is genuinely available (e.g. an embedding similarity), keep
/// it alongside the tier rather than discarding it; the tier remains the value
/// validators branch on.
/// </remarks>
public enum ConfidenceTier
{
    /// <summary>Weak support; treat as a lead to verify, not a fact.</summary>
    Low = 0,

    /// <summary>Reasonable support; usable but flag if it drives an important action.</summary>
    Medium = 1,

    /// <summary>Strong support; directly grounded in a reliable source.</summary>
    High = 2,
}
