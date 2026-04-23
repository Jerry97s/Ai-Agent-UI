namespace AiAgentUi.Models;

public sealed class ChatEntryModel
{
    public string Role { get; init; } = "";
    public string Text { get; init; } = "";
    public bool IsUser { get; init; }
    public bool IsTypingIndicator { get; init; }
}

