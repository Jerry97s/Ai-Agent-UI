using System.Text.Json;
using AiAgentUi.Models;
using Xunit;

namespace AiAgentUi.Tests;

public sealed class ChatDtosJsonTests
{
    private static readonly JsonSerializerOptions CamelWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions RelaxedRead = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void ChatRequest_serializes_to_expected_json_shape()
    {
        var json = JsonSerializer.Serialize(new ChatRequestModel { Message = "hello" }, CamelWrite);
        Assert.Contains("\"message\":\"hello\"", json);
    }

    [Fact]
    public void ChatResponse_deserializes_from_agent_payload()
    {
        const string json = """{"reply":"from agent"}""";
        var dto = JsonSerializer.Deserialize<ChatResponseModel>(json, RelaxedRead);
        Assert.NotNull(dto);
        Assert.Equal("from agent", dto!.Reply);
    }
}
