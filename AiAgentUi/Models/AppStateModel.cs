namespace AiAgentUi.Models;

public sealed class AppStateModel
{
    public string? SelectedConversationId { get; set; }
    public List<ConversationModel> Conversations { get; set; } = new();
}

