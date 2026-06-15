using System.Text.Json.Nodes;
using Atlas.Core.Tools;

namespace Atlas.Tools;

/// <summary>
/// Validates a model's tool-call arguments against a tool's declared parameters
/// before the tool runs (arch §14, §25).
/// </summary>
/// <remarks>
/// <para>
/// Small models routinely emit malformed or under-specified arguments. Validating
/// deterministically here — in code, not by trusting the model — is exactly the
/// "deterministic code handles what it can" principle (arch §25). A clear,
/// structured rejection lets the orchestrator feed a corrected retry back to the
/// model.
/// </para>
/// <para>
/// Validation is strict about declared parameters (required presence, value kind,
/// enum membership) but lenient about <em>extra</em> properties: a model that adds
/// an irrelevant field should be corrected by ignoring it, not by failing the call.
/// </para>
/// </remarks>
public static class ToolArgumentValidator
{
    /// <summary>The outcome of validating an invocation's arguments.</summary>
    /// <param name="IsValid">Whether the arguments satisfy the declared parameters.</param>
    /// <param name="Error">A plain-language explanation when invalid; otherwise null.</param>
    public readonly record struct ValidationResult(bool IsValid, string? Error)
    {
        /// <summary>A successful validation.</summary>
        public static ValidationResult Valid { get; } = new(true, null);

        /// <summary>Creates a failed validation with an explanation.</summary>
        public static ValidationResult Invalid(string error) => new(false, error);
    }

    /// <summary>Validates <paramref name="arguments"/> against <paramref name="descriptor"/>.</summary>
    public static ValidationResult Validate(ToolDescriptor descriptor, JsonObject arguments)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (ToolParameter parameter in descriptor.Parameters)
        {
            bool present = arguments.TryGetPropertyValue(parameter.Name, out JsonNode? node);

            if (!present || node is null)
            {
                if (parameter.Required)
                {
                    return ValidationResult.Invalid(
                        $"Missing required argument '{parameter.Name}' ({parameter.Type}).");
                }

                continue;
            }

            if (!MatchesType(node, parameter.Type))
            {
                return ValidationResult.Invalid(
                    $"Argument '{parameter.Name}' must be of type {parameter.Type}.");
            }

            if (!parameter.AllowedValues.IsEmpty
                && node.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                string value = node.GetValue<string>();
                if (!parameter.AllowedValues.Contains(value, StringComparer.Ordinal))
                {
                    return ValidationResult.Invalid(
                        $"Argument '{parameter.Name}' must be one of: {string.Join(", ", parameter.AllowedValues)}.");
                }
            }
        }

        return ValidationResult.Valid;
    }

    private static bool MatchesType(JsonNode node, ToolParameterType type) => type switch
    {
        ToolParameterType.String => IsValueKind(node, System.Text.Json.JsonValueKind.String),
        ToolParameterType.Boolean => node is JsonValue && node.AsValue().TryGetValue(out bool _),
        ToolParameterType.Integer => node is JsonValue && node.AsValue().TryGetValue(out long _),
        ToolParameterType.Number => node is JsonValue && node.AsValue().TryGetValue(out double _),
        ToolParameterType.Object => node is JsonObject,
        ToolParameterType.Array => node is JsonArray,
        _ => false,
    };

    private static bool IsValueKind(JsonNode node, System.Text.Json.JsonValueKind kind) =>
        node is JsonValue && node.GetValueKind() == kind;
}
