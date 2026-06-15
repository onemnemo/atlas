using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Atlas.Inference.Transport;

/// <summary>
/// Wire DTOs for the OpenAI-compatible chat-completions API exposed by
/// llama.cpp's <c>llama-server</c> (arch §31.3).
/// </summary>
/// <remarks>
/// These are deliberately internal and minimal — only the fields Atlas sends or
/// reads. Property names are pinned with <see cref="JsonPropertyNameAttribute"/>
/// to the API's snake_case so serialization does not depend on a naming policy.
/// </remarks>
internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }

    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Stop { get; init; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormatDto? ResponseFormat { get; init; }

    /// <summary>Tool definitions offered to the model. Null when not using tool-call mode.</summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolDefinitionDto>? Tools { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

internal sealed class ChatMessageDto
{
    // Not 'required': when sending we always set these, but when reading a
    // backend response we tolerate a missing role/content rather than throwing
    // on sloppy output (the transport degrades; the pipeline validates).
    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    // Nullable: tool-calls messages have null content (the model emits tool_calls
    // instead of text).  WhenWritingNull omits the key entirely which is what
    // backends expect for those messages.
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    /// <summary>Tool invocations the model emitted (present on assistant messages in tool-call mode).</summary>
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; init; }

    /// <summary>Correlation ID linking a tool-result message back to the matching tool call.</summary>
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
}

// ── Tool definition DTOs (sent in the request) ────────────────────────────────

internal sealed class ToolDefinitionDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required ToolFunctionDto Function { get; init; }
}

internal sealed class ToolFunctionDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Parameters { get; init; }
}

// ── Tool call DTOs (returned by the model) ────────────────────────────────────

internal sealed class ToolCallDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public ToolCallFunctionDto? Function { get; init; }
}

internal sealed class ToolCallFunctionDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The model's raw JSON string for the tool arguments.</summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = "{}";
}

internal sealed class ResponseFormatDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonSchemaDto? JsonSchema { get; init; }
}

internal sealed class JsonSchemaDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("schema")]
    public required JsonNode Schema { get; init; }
}

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<ChoiceDto>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public UsageDto? Usage { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

internal sealed class ChoiceDto
{
    [JsonPropertyName("message")]
    public ChatMessageDto? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    /// <summary>Shorthand: true when the model chose to call tools instead of returning text.</summary>
    internal bool IsToolCall =>
        FinishReason == "tool_calls"
        || (Message?.ToolCalls is { Count: > 0 });
}

internal sealed class UsageDto
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
}

// ── Streaming DTOs ────────────────────────────────────────────────────────────
// Used when Stream=true; the backend sends one SSE "data: {json}" line per
// token.  The final SSE event is "data: [DONE]".

internal sealed class ChatCompletionStreamChunk
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<StreamChoiceDto>? Choices { get; init; }
}

internal sealed class StreamChoiceDto
{
    [JsonPropertyName("delta")]
    public StreamDeltaDto? Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

internal sealed class StreamDeltaDto
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
