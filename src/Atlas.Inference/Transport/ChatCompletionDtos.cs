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

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
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
}

internal sealed class UsageDto
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
}
