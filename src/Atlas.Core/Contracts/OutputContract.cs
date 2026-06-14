using System.Collections.Immutable;

namespace Atlas.Core.Contracts;

/// <summary>
/// The typed specification of what a single pipeline stage must produce
/// (arch §25).
/// </summary>
/// <remarks>
/// <para>
/// Output contracts are the ground truth that makes the rest of the system
/// checkable and replaceable (arch §25):
/// </para>
/// <list type="bullet">
///   <item>validators know exactly what they are checking, with no runtime inference;</item>
///   <item>downstream stages know exactly what they will receive;</item>
///   <item>a future fine-tuned model has a precise target format for training data;</item>
///   <item>repair loops have a concrete specification to repair toward;</item>
///   <item>a stage can be replaced or upgraded without breaking its neighbours.</item>
/// </list>
/// <para>
/// A contract describes the <em>shape and policy</em> of an output. The actual
/// schema validation logic lives behind the validator named by
/// <see cref="ValidationEntrypoint"/>, keeping this type pure data that can be
/// serialized and, later, authored as configuration.
/// </para>
/// </remarks>
/// <param name="OutputType">
/// Stable, versioned identifier of the schema/format this stage produces
/// (e.g. <c>"flashcard.deck.v1"</c>). Versioned so the format can evolve without
/// breaking consumers pinned to an older shape.
/// </param>
/// <param name="Format">The surface form of the output.</param>
/// <param name="RequiredFields">
/// Field names that must be present for the output to be accepted. Empty for
/// non-structured formats.
/// </param>
/// <param name="OptionalFields">Field names that enrich the output but are not mandatory.</param>
/// <param name="ForbiddenContent">
/// Content categories that must not appear (e.g. raw filesystem paths, PII).
/// Enforced by the validator; expressed here as stable category tokens.
/// </param>
/// <param name="MaxOutputTokens">
/// Optional ceiling on output size in tokens. <see langword="null"/> means no
/// explicit limit beyond the task's overall generation budget.
/// </param>
/// <param name="CitationPolicy">Whether and how the output must cite sources.</param>
/// <param name="ValidationEntrypoint">
/// Identifier of the validator responsible for this output type. The validator
/// registry resolves it to a concrete validator, keeping this contract free of
/// behaviour.
/// </param>
public sealed record OutputContract(
    string OutputType,
    OutputFormat Format,
    ImmutableArray<string> RequiredFields,
    ImmutableArray<string> OptionalFields,
    ImmutableArray<string> ForbiddenContent,
    int? MaxOutputTokens,
    CitationPolicy CitationPolicy,
    string ValidationEntrypoint)
{
    /// <summary>
    /// Creates a contract for free-form text output (no fields, no schema), such
    /// as autocomplete or a plain chat reply.
    /// </summary>
    /// <param name="outputType">The versioned output-type identifier.</param>
    /// <param name="validationEntrypoint">The validator id for this output.</param>
    /// <param name="format">The text format; defaults to plain text.</param>
    /// <param name="maxOutputTokens">Optional output size ceiling.</param>
    /// <param name="citationPolicy">Citation policy; defaults to none.</param>
    public static OutputContract Text(
        string outputType,
        string validationEntrypoint,
        OutputFormat format = OutputFormat.PlainText,
        int? maxOutputTokens = null,
        CitationPolicy citationPolicy = CitationPolicy.None) =>
        new(
            OutputType: outputType,
            Format: format,
            RequiredFields: [],
            OptionalFields: [],
            ForbiddenContent: [],
            MaxOutputTokens: maxOutputTokens,
            CitationPolicy: citationPolicy,
            ValidationEntrypoint: validationEntrypoint);

    /// <summary>Validates the contract's own structural invariants.</summary>
    /// <exception cref="ArgumentException">A required identifier is missing.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputType))
        {
            throw new ArgumentException("OutputContract.OutputType must be non-empty.", nameof(OutputType));
        }

        if (string.IsNullOrWhiteSpace(ValidationEntrypoint))
        {
            throw new ArgumentException(
                "OutputContract.ValidationEntrypoint must be non-empty.", nameof(ValidationEntrypoint));
        }

        if (MaxOutputTokens is <= 0)
        {
            throw new ArgumentException(
                $"OutputContract.MaxOutputTokens must be positive when set (was {MaxOutputTokens}).",
                nameof(MaxOutputTokens));
        }
    }
}
