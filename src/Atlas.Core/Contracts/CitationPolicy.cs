namespace Atlas.Core.Contracts;

/// <summary>
/// Whether and how an output must cite its sources (arch §25).
/// </summary>
/// <remarks>
/// The citation policy is enforced by a deterministic validator: presence and
/// resolvability of references is checked by code, not judged by a model
/// (arch §19). <see cref="RequiredWithMethod"/> additionally demands that each
/// citation records how it was found, mirroring the
/// <see cref="Evidence.ExtractionMethod"/> on findings.
/// </remarks>
public enum CitationPolicy
{
    /// <summary>Citations are not expected.</summary>
    None = 0,

    /// <summary>Citations are welcome but not required for acceptance.</summary>
    Optional = 1,

    /// <summary>At least one resolvable citation is required.</summary>
    Required = 2,

    /// <summary>
    /// Citations are required and each must declare its extraction method, so
    /// validation can weight trust accordingly (arch §18).
    /// </summary>
    RequiredWithMethod = 3,
}
