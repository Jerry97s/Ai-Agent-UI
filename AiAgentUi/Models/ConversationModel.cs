using System.Collections.ObjectModel;

namespace AiAgentUi.Models;

public sealed class ConversationModel
{
    public string Id { get; init; } = "";
    public string Title { get; set; } = "";
    public bool IsPinned { get; set; }
    public ObservableCollection<ChatEntryModel> Messages { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

