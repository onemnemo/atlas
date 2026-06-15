using System.Text;
using Atlas.Core.Inference;

namespace Atlas.Core.Tools;

/// <summary>
/// Converts <see cref="ToolDescriptor"/>s into <see cref="ToolDefinition"/>s
/// suitable for passing to <see cref="IInferenceClient.CompleteWithToolsAsync"/>
/// (arch §12 — scoped tool selection).
/// </summary>
/// <remarks>
/// The conversion emits a JSON Schema <c>object</c> string for each tool's
/// parameters so the model can produce well-formed arguments.  The mapping
/// follows JSON Schema draft-07 conventions, which every major function-calling
/// model understands.
/// </remarks>
public static class ToolDescriptorMapper
{
    /// <summary>
    /// Converts a collection of descriptors to a list of
    /// <see cref="ToolDefinition"/>s ready for the inference client.
    /// </summary>
    public static IReadOnlyList<ToolDefinition> ToDefinitions(
        IEnumerable<ToolDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var result = new List<ToolDefinition>();
        foreach (ToolDescriptor descriptor in descriptors)
        {
            result.Add(ToDefinition(descriptor));
        }

        return result;
    }

    /// <summary>Converts a single descriptor.</summary>
    public static ToolDefinition ToDefinition(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        string? schema = descriptor.Parameters.Length > 0
            ? BuildParameterSchema(descriptor.Parameters)
            : null;

        return new ToolDefinition(
            Name: descriptor.Name,
            Description: descriptor.Summary,
            ParametersSchemaJson: schema);
    }

    // ── JSON Schema builder ───────────────────────────────────────────────────

    private static string BuildParameterSchema(
        System.Collections.Immutable.ImmutableArray<ToolParameter> parameters)
    {
        var sb = new StringBuilder();
        sb.Append("""{"type":"object","properties":{""");

        bool first = true;
        foreach (ToolParameter param in parameters)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append('"');
            sb.Append(param.Name);
            sb.Append("""":{"type":""");
            sb.Append('"');
            sb.Append(ToJsonSchemaType(param.Type));
            sb.Append('"');

            if (!string.IsNullOrWhiteSpace(param.Description))
            {
                sb.Append(""","description":""");
                sb.Append('"');
                sb.Append(EscapeJsonString(param.Description));
                sb.Append('"');
            }

            if (!param.AllowedValues.IsDefaultOrEmpty)
            {
                sb.Append(""","enum":[""");
                for (int i = 0; i < param.AllowedValues.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('"');
                    sb.Append(EscapeJsonString(param.AllowedValues[i]));
                    sb.Append('"');
                }

                sb.Append(']');
            }

            sb.Append('}');
        }

        sb.Append('}');

        // Collect required parameter names.
        bool hasRequired = false;
        foreach (ToolParameter param in parameters)
        {
            if (param.Required)
            {
                hasRequired = true;
                break;
            }
        }

        if (hasRequired)
        {
            sb.Append(""","required":[""");
            bool firstReq = true;
            foreach (ToolParameter param in parameters)
            {
                if (!param.Required)
                {
                    continue;
                }

                if (!firstReq)
                {
                    sb.Append(',');
                }

                firstReq = false;
                sb.Append('"');
                sb.Append(param.Name);
                sb.Append('"');
            }

            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string ToJsonSchemaType(ToolParameterType type) => type switch
    {
        ToolParameterType.String => "string",
        ToolParameterType.Integer => "integer",
        ToolParameterType.Number => "number",
        ToolParameterType.Boolean => "boolean",
        ToolParameterType.Object => "object",
        ToolParameterType.Array => "array",
        _ => "string",
    };

    private static string EscapeJsonString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal)
             .Replace("\r", "\\r", StringComparison.Ordinal)
             .Replace("\t", "\\t", StringComparison.Ordinal);
}
