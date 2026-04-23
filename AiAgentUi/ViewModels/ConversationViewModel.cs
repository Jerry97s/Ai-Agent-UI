using System.Collections.ObjectModel;
using AiAgentUi.Models;
using AiAgentUi.Mvvm;

namespace AiAgentUi.ViewModels;

public sealed class ConversationViewModel : ObservableObject
{
    public string Id { get; }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    private string _title;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ObservableCollection<ChatEntryModel> Messages { get; } = new();

    private string _draft = "";
    public string Draft
    {
        get => _draft;
        set => SetProperty(ref _draft, value);
    }

    public ConversationViewModel(string id, string title)
    {
        Id = id;
        _title = title;
    }

    public static ConversationViewModel FromModel(ConversationModel model)
    {
        var vm = new ConversationViewModel(model.Id, string.IsNullOrWhiteSpace(model.Title) ? "대화" : model.Title);
        vm.IsPinned = model.IsPinned;
        foreach (var m in model.Messages)
            vm.Messages.Add(m);
        return vm;
    }

    public ConversationModel ToModel()
    {
        var model = new ConversationModel
        {
            Id = Id,
            Title = Title,
            IsPinned = IsPinned,
        };
        foreach (var m in Messages)
            model.Messages.Add(m);
        return model;
    }
}

