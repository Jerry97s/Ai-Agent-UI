using System.Text.Json.Serialization;

namespace AiAgentUi.Models;

public sealed class ChatRequestModel
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

public sealed class ChatResponseModel
{
    [JsonPropertyName("reply")]
    public string Reply { get; init; } = "";
}

