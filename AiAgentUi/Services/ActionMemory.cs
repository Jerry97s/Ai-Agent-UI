using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AiAgentUi.Models;

namespace AiAgentUi.Services;

public sealed class ActionMemory
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _gate = new();
    private readonly string _folder;
    private readonly string _eventsFolder;
    private readonly bool _eventsInAppFolder;
    private readonly string _statePath;
    private readonly string _legacyChatPath;

    public ActionMemory(string appName = "AiAgentUi")
    {
        // State stays in AppData (more reliable across install locations).
        _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

        Directory.CreateDirectory(_folder);
        _statePath = Path.Combine(_folder, "state.json");
        _legacyChatPath = Path.Combine(_folder, "chat.json");

        // Events/logs go next to the executable in ./Logs for easy access.
        // If not writable (e.g. Program Files), fall back to AppData.
        var preferred = Path.Combine(AppContext.BaseDirectory, "Logs");
        _eventsInAppFolder = TryEnsureFolder(preferred);
        _eventsFolder = _eventsInAppFolder ? preferred : Path.Combine(_folder, "Logs");
        Directory.CreateDirectory(_eventsFolder);
    }

    public string DataFolder => _folder;

    public void LogEvent(string type, object? data = null)
    {
        var evt = new
        {
            ts = DateTimeOffset.Now,
            type,
            data,
        };

        var line = JsonSerializer.Serialize(evt, Json);
        lock (_gate)
        {
            var path = GetEventsPath();
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private string GetEventsPath()
    {
        var now = DateTimeOffset.Now;
        var dayFolder = Path.Combine(_eventsFolder, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayFolder);
        return Path.Combine(dayFolder, $"events_{now:HH}.jsonl");
    }

    private static bool TryEnsureFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var test = Path.Combine(folder, ".write_test");
            File.WriteAllText(test, "ok", Encoding.UTF8);
            File.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public AppStateModel LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                // Migration: older versions used chat.json (single conversation).
                var legacy = LoadLegacyChat();
                if (legacy.Count == 0)
                    return new AppStateModel();

                var convoId = Guid.NewGuid().ToString("N");
                var convo = new ConversationModel
                {
                    Id = convoId,
                    Title = "대화 1",
                };
                foreach (var m in legacy)
                    convo.Messages.Add(m);

                var migratedState = new AppStateModel
                {
                    SelectedConversationId = convoId,
                    Conversations = new List<ConversationModel> { convo },
                };

                // Persist migrated state so next startup is stable
                SaveState(migratedState);
                LogEvent("state.migrated", new { from = "chat.json", count = legacy.Count });
                return migratedState;
            }

            var json = File.ReadAllText(_statePath, Encoding.UTF8);
            var state = JsonSerializer.Deserialize<AppStateModel>(json, Json);
            return state ?? new AppStateModel();
        }
        catch
        {
            return new AppStateModel();
        }
    }

    private List<ChatEntryModel> LoadLegacyChat()
    {
        try
        {
            if (!File.Exists(_legacyChatPath))
                return new List<ChatEntryModel>();

            var json = File.ReadAllText(_legacyChatPath, Encoding.UTF8);
            var items = JsonSerializer.Deserialize<List<ChatEntryModel>>(json, Json);
            return items ?? new List<ChatEntryModel>();
        }
        catch
        {
            return new List<ChatEntryModel>();
        }
    }

    public void SaveState(AppStateModel state, int maxMessagesPerConversation = 200)
    {
        lock (_gate)
        {
            foreach (var c in state.Conversations)
            {
                if (c.Messages.Count > maxMessagesPerConversation)
                {
                    var trimmed = c.Messages.TakeLast(maxMessagesPerConversation).ToList();
                    c.Messages.Clear();
                    foreach (var m in trimmed)
                        c.Messages.Add(m);
                }
            }
        }

        var json = JsonSerializer.Serialize(state, Json);
        lock (_gate)
        {
            File.WriteAllText(_statePath, json, Encoding.UTF8);
        }
    }
}

