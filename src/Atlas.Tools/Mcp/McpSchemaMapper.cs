using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Atlas.Core.Inference;
using Atlas.Core.Tools;

namespace Atlas.Tools.Mcp;

/// <summary>
/// Maps an MCP tool advertisement (name + JSON Schema) onto an Atlas
/// <see cref="ToolDescriptor"/> so external tools sit in the same tree as
/// built-ins.
/// </summary>
/// <remarks>
/// The descriptor name is namespaced with the server id (<c>server.tool</c>) so
/// tools from different servers never collide, while the original remote name is
/// preserved separately for the actual <c>tools/call</c>. The authority fields are
/// taken from the server configuration, never from the server's own claims.
/// </remarks>
public static class McpSchemaMapper
{
    /// <summary>Builds a descriptor for one remote tool under the given server's authority.</summary>
    public static ToolDescriptor ToDescriptor(McpToolInfo info, McpServerOptions server)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(server);

        string summary = info.Description.Length > 0 ? info.Description : info.Name;
        return new ToolDescriptor(
            Name: $"{server.Id}.{info.Name}",
            Branch: server.Branch,
            Summary: summary,
            Parameters: ParseParameters(info.InputSchema),
            RequiredPermission: server.RequiredPermission,
            RequiredGate: server.RequiredGate,
            MinimumModelTier: ModelTier.Small,
            Origin: server.Id);
    }

    private static ImmutableArray<ToolParameter> ParseParameters(JsonObject? schema)
    {
        if (schema?["properties"] is not JsonObject properties)
        {
            return ImmutableArray<ToolParameter>.Empty;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema["required"] is JsonArray requiredArray)
        {
            foreach (JsonNode? node in requiredArray)
            {
                if (node is not null)
                {
                    required.Add(node.GetValue<string>());
                }
            }
        }

        var builder = ImmutableArray.CreateBuilder<ToolParameter>(properties.Count);
        foreach (KeyValuePair<string, JsonNode?> property in properties)
        {
            JsonObject? definition = property.Value as JsonObject;
            string typeName = definition?["type"]?.GetValue<string>() ?? "string";
            string description = definition?["description"]?.GetValue<string>() ?? string.Empty;

            builder.Add(new ToolParameter(
                Name: property.Key,
                Type: MapType(typeName),
                Description: description,
                Required: required.Contains(property.Key),
                AllowedValues: ParseEnum(definition)));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> ParseEnum(JsonObject? definition)
    {
        if (definition?["enum"] is not JsonArray values)
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(values.Count);
        foreach (JsonNode? node in values)
        {
            if (node is not null && node.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                builder.Add(node.GetValue<string>());
            }
        }

        return builder.ToImmutable();
    }

    private static ToolParameterType MapType(string jsonSchemaType) => jsonSchemaType switch
    {
        "integer" => ToolParameterType.Integer,
        "number" => ToolParameterType.Number,
        "boolean" => ToolParameterType.Boolean,
        "object" => ToolParameterType.Object,
        "array" => ToolParameterType.Array,
        _ => ToolParameterType.String,
    };
}
