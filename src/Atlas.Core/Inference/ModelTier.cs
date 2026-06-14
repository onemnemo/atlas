namespace Atlas.Core.Inference;

/// <summary>
/// A coarse model-capability class, expressed by approximate parameter count
/// (arch §16, §23, model sheet).
/// </summary>
/// <remarks>
/// <para>
/// Atlas never refers to a concrete model by name in task or pipeline logic. It
/// refers to a <em>role</em> (see <see cref="ModelRole"/>) and a minimum
/// <see cref="ModelTier"/>. The model registry resolves those to a concrete
/// model for the current hardware tier. This indirection is what makes models
/// swappable and lets a fine-tuned model replace a prompted one without touching
/// the surrounding architecture (arch §16).
/// </para>
/// <para>
/// Members are ordered by capability so a task's minimum requirement can be
/// compared against an available model with <c>available &gt;= required</c>.
/// </para>
/// </remarks>
public enum ModelTier
{
    /// <summary>
    /// Sub-1B class (e.g. Qwen3 0.6B). Suitable only for classification and
    /// constrained extraction — routing, not generation.
    /// </summary>
    Tiny = 0,

    /// <summary>
    /// ~1–3B class (e.g. Gemma 3 1B, Qwen3 1.7B, SmolLM3 3B). The default
    /// worker tier for most generation on modest hardware.
    /// </summary>
    Small = 1,

    /// <summary>
    /// ~3–7B class. The escalation/fallback tier when a smaller model fails
    /// validation repeatedly (arch §21, model sheet).
    /// </summary>
    Medium = 2,

    /// <summary>
    /// 7B+ class. Only available on high-end hardware and only used when the
    /// task genuinely requires it.
    /// </summary>
    Large = 3,
}
