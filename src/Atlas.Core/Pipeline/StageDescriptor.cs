using Atlas.Core.Contracts;
using Atlas.Core.Inference;

namespace Atlas.Core.Pipeline;

/// <summary>
/// The self-description of a pipeline stage: what it is, what it produces, and
/// whether it depends on a model (arch §16, §25).
/// </summary>
/// <remarks>
/// <para>
/// The descriptor is the metadata that makes a stage replaceable. Two
/// implementations of the same logical stage (a prompted model today, a
/// fine-tuned model tomorrow) share a <see cref="StageId"/> and
/// <see cref="OutputContract"/> but differ in <see cref="Version"/>. The
/// surrounding pipeline binds to the contract, never to the implementation
/// (arch §16, §25).
/// </para>
/// <para>
/// <see cref="ModelRole"/> being <see langword="null"/> marks a deterministic
/// stage — one that does work code can do reliably and therefore "cannot fail
/// at" (arch §19). A non-null role names the capability the stage needs from the
/// model registry, which is the seam a specialist trained model plugs into.
/// </para>
/// </remarks>
/// <param name="StageId">Stable logical identifier of the stage (e.g. <c>"router"</c>, <c>"drafter"</c>).</param>
/// <param name="Version">
/// Implementation version, so a replacement can be tracked and A/B compared
/// without changing the stage id.
/// </param>
/// <param name="OutputContract">The typed contract this stage's output satisfies.</param>
/// <param name="ModelRole">
/// The model role this stage consumes, or <see langword="null"/> for a purely
/// deterministic stage.
/// </param>
public sealed record StageDescriptor(
    string StageId,
    string Version,
    OutputContract OutputContract,
    ModelRole? ModelRole = null)
{
    /// <summary>Whether this stage relies on a model (and is thus a candidate for fine-tuning).</summary>
    public bool IsModelBacked => ModelRole is not null;
}
