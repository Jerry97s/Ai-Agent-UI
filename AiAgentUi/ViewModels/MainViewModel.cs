using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using AiAgentUi.Models;
using AiAgentUi.Mvvm;
using AiAgentUi.Services;

namespace AiAgentUi.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int AgentTimeoutSeconds = 30;
    private CancellationTokenSource? _activeCts;
    private ChatEntryModel? _activeTyping;
    private ConversationViewModel? _activeConversation;
    private bool _cancelledByUser;

    private int _remainingSeconds;
    public int RemainingSeconds
    {
        get => _remainingSeconds;
        private set => SetProperty(ref _remainingSeconds, value);
    }

    private bool _isCountingDown;
    public bool IsCountingDown
    {
        get => _isCountingDown;
        private set => SetProperty(ref _isCountingDown, value);
    }
    public ObservableCollection<ConversationViewModel> Conversations { get; } = new();

    private ConversationViewModel? _selectedConversation;
    private ConversationViewModel? _draftHookedConversation;
    public ConversationViewModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                HookDraftChanges(value);
                _sendCommand.RaiseCanExecuteChanged();
                _clearHistoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _sendCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
                _healthCommand.RaiseCanExecuteChanged();
                _uploadCommand.RaiseCanExecuteChanged();
                _clearHistoryCommand.RaiseCanExecuteChanged();
                _newConversationCommand.RaiseCanExecuteChanged();
                _closeConversationCommand.RaiseCanExecuteChanged();
                _togglePinCommand.RaiseCanExecuteChanged();
                _deleteMessageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private readonly IAgentClient _agent;
    private readonly ActionMemory _memory;
    private readonly IFileDialogService _fileDialog;

    private readonly AsyncRelayCommand _sendCommand;
    public ICommand SendCommand => _sendCommand;

    private readonly RelayCommand _cancelCommand;
    public ICommand CancelCommand => _cancelCommand;

    private readonly AsyncRelayCommand _healthCommand;
    public ICommand HealthCommand => _healthCommand;

    private readonly AsyncRelayCommand _uploadCommand;
    public ICommand UploadCommand => _uploadCommand;

    private readonly RelayCommand _clearHistoryCommand;
    public ICommand ClearHistoryCommand => _clearHistoryCommand;

    private readonly RelayCommand _newConversationCommand;
    public ICommand NewConversationCommand => _newConversationCommand;

    private readonly RelayCommand<ConversationViewModel> _closeConversationCommand;
    public ICommand CloseConversationCommand => _closeConversationCommand;

    private readonly RelayCommand<ConversationViewModel> _togglePinCommand;
    public ICommand TogglePinCommand => _togglePinCommand;

    private readonly RelayCommand<ChatEntryModel> _deleteMessageCommand;
    public ICommand DeleteMessageCommand => _deleteMessageCommand;

    public MainViewModel(IAgentClient agent, ActionMemory memory, IFileDialogService fileDialog)
    {
        _agent = agent;
        _memory = memory;
        _fileDialog = fileDialog;

        _sendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        _cancelCommand = new RelayCommand(CancelActiveRequest, () => IsBusy);
        _healthCommand = new AsyncRelayCommand(HealthAsync, () => !IsBusy);
        _uploadCommand = new AsyncRelayCommand(UploadAndAnalyzeAsync, () => !IsBusy);
        _clearHistoryCommand = new RelayCommand(ClearHistory, () => !IsBusy && SelectedConversation is { Messages.Count: > 0 });
        _newConversationCommand = new RelayCommand(NewConversation, () => !IsBusy);
        _closeConversationCommand = new RelayCommand<ConversationViewModel>(CloseConversation, CanCloseConversation);
        _togglePinCommand = new RelayCommand<ConversationViewModel>(TogglePin, convo => !IsBusy && convo is not null);
        _deleteMessageCommand = new RelayCommand<ChatEntryModel>(DeleteMessage, CanDeleteMessage);

        LoadOrCreateConversations();
        HookDraftChanges(SelectedConversation);

        StatusText = "준비";
        _memory.LogEvent("vm.main.created");
    }

    private void CancelActiveRequest()
    {
        _cancelledByUser = true;
        try { _activeCts?.Cancel(); } catch { }

        if (_activeTyping is not null)
        {
            try
            {
                if (_activeConversation is not null)
                    _activeConversation.Messages.Remove(_activeTyping);
                else
                    RemoveTypingIndicator(_activeTyping);
            }
            catch { }
            _activeTyping = null;
        }

        StatusText = "사용자 중지됨";
        IsCountingDown = false;
        RemainingSeconds = 0;
        _memory.LogEvent("agent.cancelled");
    }

    private void HookDraftChanges(ConversationViewModel? convo)
    {
        if (_draftHookedConversation is not null)
            _draftHookedConversation.PropertyChanged -= SelectedConversation_PropertyChanged;

        _draftHookedConversation = convo;

        if (_draftHookedConversation is not null)
            _draftHookedConversation.PropertyChanged += SelectedConversation_PropertyChanged;
    }

    private void SelectedConversation_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConversationViewModel.Draft))
            _sendCommand.RaiseCanExecuteChanged();
    }

    public void Persist()
    {
        _memory.SaveState(ToStateModel());
    }

    private bool CanCloseConversation(ConversationViewModel? convo)
        => !IsBusy && convo is not null && Conversations.Count > 1;

    private void CloseConversation(ConversationViewModel? convo)
    {
        if (!CanCloseConversation(convo))
            return;

        var target = convo!;
        var wasSelected = ReferenceEquals(SelectedConversation, target);

        Conversations.Remove(target);
        _memory.LogEvent("chat.closed", new { convoId = target.Id });

        if (wasSelected)
            SelectedConversation = Conversations.FirstOrDefault();

        _memory.SaveState(ToStateModel());
    }

    private bool CanSend()
        => !IsBusy && SelectedConversation is not null && !string.IsNullOrWhiteSpace(SelectedConversation.Draft);

    private void Append(string role, string text, bool isUser)
    {
        if (SelectedConversation is null)
            return;

        AppendTo(SelectedConversation, role, text, isUser);
    }

    private void AppendTo(ConversationViewModel convo, string role, string text, bool isUser)
    {
        // If this is the first user message of the tab, use it as the tab title.
        if (isUser && convo.Messages.All(m => !m.IsUser))
        {
            // Only auto-title when the current title looks like a default placeholder.
            if (convo.Title.StartsWith("대화", StringComparison.Ordinal))
            {
                var title = text.Trim();
                if (title.Length > 24)
                    title = title[..24] + "…";
                if (!string.IsNullOrWhiteSpace(title))
                    convo.Title = title;
            }
        }

        var entry = new ChatEntryModel { Role = role, Text = text, IsUser = isUser };
        convo.Messages.Add(entry);

        if (!entry.IsTypingIndicator)
            _memory.SaveState(ToStateModel());
        _clearHistoryCommand.RaiseCanExecuteChanged();
    }

    private ChatEntryModel AddTypingIndicator(ConversationViewModel convo)
    {
        var typing = new ChatEntryModel
        {
            Role = "에이전트",
            Text = "",
            IsUser = false,
            IsTypingIndicator = true,
        };
        convo.Messages.Add(typing);
        return typing;
    }

    private void RemoveTypingIndicator(ChatEntryModel typing)
    {
        var convo = SelectedConversation;
        if (convo is null)
            return;
        convo.Messages.Remove(typing);
    }

    private async Task HealthAsync()
    {
        IsBusy = true;
        StatusText = "연결 확인 중…";
        try
        {
            var ok = await _agent.HealthAsync();
            StatusText = ok ? "서버와 연결되었습니다." : "응답이 올바르지 않습니다.";
            if (ok)
                Append("시스템", "에이전트 서버와 연결되었습니다.", isUser: false);
            _memory.LogEvent("agent.health", new { ok });
        }
        catch (Exception ex)
        {
            StatusText = $"오류: {ex.Message}";
            _memory.LogEvent("agent.health.error", new { ex.Message });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendAsync()
    {
        var convo = SelectedConversation;
        if (convo is null)
            return;

        var text = convo.Draft.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        IsBusy = true;
        AppendTo(convo, "나", text, isUser: true);
        convo.Draft = "";
        StatusText = $"응답 대기 중… {AgentTimeoutSeconds}s";
        RemainingSeconds = AgentTimeoutSeconds;
        IsCountingDown = true;

        var typing = AddTypingIndicator(convo);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AgentTimeoutSeconds));
        _activeCts = cts;
        _activeTyping = typing;
        _activeConversation = convo;
        _cancelledByUser = false;
        var countdownTask = RunCountdownAsync(cts.Token, "응답 대기 중…");
        try
        {
            var reply = await _agent.SendMessageAsync(text, cts.Token);
            convo.Messages.Remove(typing);
            if (ReferenceEquals(_activeTyping, typing)) _activeTyping = null;
            AppendTo(convo, "에이전트", reply, isUser: false);
            TryApplyConfirmedTabRename(convo, text, reply);
            StatusText = $"완료 ({reply.Length:N0}자)";
            _memory.LogEvent("agent.chat", new { ok = true, len = text.Length });
            _memory.LogEvent("agent.reply", new { len = reply.Length });
            _memory.LogEvent("agent.reply.text", new
            {
                len = reply.Length,
                truncated = reply.Length > 4000,
                text = reply.Length > 4000 ? reply[..4000] : reply,
            });

            cts.Cancel();
            await countdownTask;
        }
        catch (TaskCanceledException)
        {
            convo.Messages.Remove(typing);
            if (ReferenceEquals(_activeTyping, typing)) _activeTyping = null;
            if (_cancelledByUser)
            {
                AppendTo(convo, "에이전트", "요청이 사용자에 의해 중지되었습니다.", isUser: false);
                StatusText = "중지됨";
                _memory.LogEvent("agent.chat.cancelled", new { by = "user" });
            }
            else
            {
                AppendTo(convo, "에이전트", $"응답 실패: {AgentTimeoutSeconds}초 동안 응답이 없어 중단했습니다.", isUser: false);
                StatusText = "응답 실패";
                _memory.LogEvent("agent.chat.error", new { type = "timeout", seconds = AgentTimeoutSeconds });
            }
        }
        catch (Exception ex)
        {
            convo.Messages.Remove(typing);
            AppendTo(convo, "에이전트", ex.Message, isUser: false);
            StatusText = "오류";
            _memory.LogEvent("agent.chat.error", new { type = "exception", ex.Message });
        }
        finally
        {
            try { cts.Cancel(); } catch { }
            try { await countdownTask; } catch { }
            if (ReferenceEquals(_activeCts, cts)) _activeCts = null;
            if (ReferenceEquals(_activeConversation, convo)) _activeConversation = null;
            _memory.SaveState(ToStateModel());
            IsCountingDown = false;
            RemainingSeconds = 0;
            IsBusy = false;
        }
    }

    private async Task UploadAndAnalyzeAsync()
    {
        if (!_fileDialog.TryPickTextFile(out var path) || string.IsNullOrWhiteSpace(path))
            return;

        await AnalyzeFileAsync(path);
    }

    public async Task AnalyzeFileAsync(string path)
    {
        var convo = SelectedConversation;
        if (convo is null)
            return;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!File.Exists(path))
        {
            AppendTo(convo, "시스템", $"파일을 찾을 수 없습니다: {path}", isUser: false);
            return;
        }

        IsBusy = true;
        StatusText = "파일 읽는 중…";

        ChatEntryModel? typing = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AgentTimeoutSeconds));
        _activeCts = cts;
        _cancelledByUser = false;
        Task? countdownTask = null;
        try
        {
            var fileName = Path.GetFileName(path);
            var content = ReadTextFileBestEffort(path);

            const int maxChars = 120_000;
            var truncated = content.Length > maxChars;
            var snippet = truncated ? content[..maxChars] : content;

            // If this is the first interaction of the conversation, name it "파일 분석".
            if (convo.Messages.Count == 0)
                convo.Title = "파일 분석";

            AppendTo(convo, "시스템", $"파일 업로드됨: {fileName}{(truncated ? $" (앞 {maxChars:N0}자만 전송)" : "")}", isUser: false);
            _memory.LogEvent("file.picked", new { fileName, truncated, len = content.Length });

            // Show the uploaded content to the user as their own message (limited for UI).
            const int maxDisplayChars = 8000;
            var displayTruncated = snippet.Length > maxDisplayChars;
            var display = displayTruncated ? snippet[..maxDisplayChars] : snippet;
            AppendTo(convo, "나", $"[파일 업로드] {fileName}\n\n```text\n{display}\n```{(displayTruncated ? $"\n\n(표시는 앞 {maxDisplayChars:N0}자만)" : "")}", isUser: true);

            var prompt = BuildFileAnalysisPrompt(fileName, snippet, truncated);
            StatusText = $"분석 요청 중… {AgentTimeoutSeconds}s";
            RemainingSeconds = AgentTimeoutSeconds;
            IsCountingDown = true;

            typing = AddTypingIndicator(convo);
            _activeTyping = typing;
            _activeConversation = convo;
            countdownTask = RunCountdownAsync(cts.Token, "분석 요청 중…");
            var reply = await _agent.SendMessageAsync(prompt, cts.Token);
            convo.Messages.Remove(typing);
            if (ReferenceEquals(_activeTyping, typing)) _activeTyping = null;
            AppendTo(convo, "에이전트", reply, isUser: false);
            StatusText = $"완료 ({reply.Length:N0}자)";
            _memory.LogEvent("agent.reply", new { len = reply.Length, kind = "file" });
            _memory.LogEvent("agent.reply.text", new
            {
                kind = "file",
                len = reply.Length,
                truncated = reply.Length > 4000,
                text = reply.Length > 4000 ? reply[..4000] : reply,
            });

            cts.Cancel();
            await countdownTask;
        }
        catch (TaskCanceledException)
        {
            if (typing is not null)
                convo.Messages.Remove(typing);
            if (typing is not null && ReferenceEquals(_activeTyping, typing)) _activeTyping = null;
            if (_cancelledByUser)
            {
                AppendTo(convo, "에이전트", "요청이 사용자에 의해 중지되었습니다.", isUser: false);
                StatusText = "중지됨";
                _memory.LogEvent("agent.chat.cancelled", new { by = "user", kind = "file" });
            }
            else
            {
                AppendTo(convo, "에이전트", $"응답 실패: {AgentTimeoutSeconds}초 동안 응답이 없어 중단했습니다.", isUser: false);
                StatusText = "응답 실패";
                _memory.LogEvent("agent.chat.error", new { type = "timeout", seconds = AgentTimeoutSeconds, kind = "file" });
            }
        }
        catch (Exception ex)
        {
            if (typing is not null)
                convo.Messages.Remove(typing);
            AppendTo(convo, "시스템", $"파일 분석 실패: {ex.Message}", isUser: false);
            StatusText = "오류";
            _memory.LogEvent("file.analyze.error", new { ex.Message });
        }
        finally
        {
            try { cts.Cancel(); } catch { }
            if (countdownTask is not null)
            {
                try { await countdownTask; } catch { }
            }
            if (ReferenceEquals(_activeCts, cts)) _activeCts = null;
            if (ReferenceEquals(_activeConversation, convo)) _activeConversation = null;
            _memory.SaveState(ToStateModel());
            IsCountingDown = false;
            RemainingSeconds = 0;
            IsBusy = false;
        }
    }

    private async Task RunCountdownAsync(CancellationToken token, string prefix)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            var remaining = AgentTimeoutSeconds;
            while (!token.IsCancellationRequested && remaining > 0)
            {
                RemainingSeconds = remaining;
                IsCountingDown = true;
                StatusText = $"{prefix} {remaining}s";
                remaining--;
                await timer.WaitForNextTickAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch
        {
            // ignore
        }
    }

    public async Task AnalyzeFilesAsync(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (IsBusy)
                return;
            await AnalyzeFileAsync(p);
        }
    }

    private static string BuildFileAnalysisPrompt(string fileName, string content, bool truncated)
    {
        var sb = new StringBuilder();
        sb.AppendLine("아래 파일을 분석해줘.");
        sb.AppendLine($"- 파일명: {fileName}");
        if (truncated)
            sb.AppendLine("- 주의: 파일이 커서 앞부분만 전달했어.");
        sb.AppendLine();
        sb.AppendLine("요청:");
        sb.AppendLine("1) 핵심 요약");
        sb.AppendLine("2) 오류/경고/이상징후가 있으면 원인 추정");
        sb.AppendLine("3) 재현/확인 방법");
        sb.AppendLine("4) 해결책/개선안");
        sb.AppendLine();
        sb.AppendLine("파일 내용:");
        sb.AppendLine("```");
        sb.AppendLine(content);
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string ReadTextFileBestEffort(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch
        {
            return File.ReadAllText(path, Encoding.Default);
        }
    }

    private void ClearHistory()
    {
        var convo = SelectedConversation;
        if (convo is null)
            return;

        var count = convo.Messages.Count;
        convo.Messages.Clear();
        convo.Draft = "";
        _memory.SaveState(ToStateModel());
        StatusText = "대화 기록을 삭제했습니다.";
        _memory.LogEvent("chat.cleared", new { count, convoId = convo.Id });
        _clearHistoryCommand.RaiseCanExecuteChanged();
    }

    private void NewConversation()
    {
        var id = Guid.NewGuid().ToString("N");
        var title = $"대화 {Conversations.Count + 1}";
        var convo = new ConversationViewModel(id, title);
        Conversations.Add(convo);
        SelectedConversation = convo;
        SortConversationsPinnedFirst();
        _memory.SaveState(ToStateModel());
        _memory.LogEvent("chat.new", new { convoId = id });
    }

    private void LoadOrCreateConversations()
    {
        var state = _memory.LoadState();
        if (state.Conversations.Count == 0)
        {
            NewConversation();
            return;
        }

        foreach (var c in state.Conversations)
            Conversations.Add(ConversationViewModel.FromModel(c));

        SortConversationsPinnedFirst();

        SelectedConversation =
            Conversations.FirstOrDefault(c => c.Id == state.SelectedConversationId) ??
            Conversations.FirstOrDefault();

        _memory.LogEvent("chat.restore", new { count = state.Conversations.Count });
    }

    private AppStateModel ToStateModel()
    {
        var state = new AppStateModel
        {
            SelectedConversationId = SelectedConversation?.Id,
        };
        foreach (var c in Conversations)
            state.Conversations.Add(c.ToModel());
        return state;
    }

    private void TogglePin(ConversationViewModel? convo)
    {
        if (convo is null)
            return;

        convo.IsPinned = !convo.IsPinned;
        SortConversationsPinnedFirst();
        _memory.SaveState(ToStateModel());
        _memory.LogEvent("chat.pin.toggled", new { convoId = convo.Id, pinned = convo.IsPinned });
    }

    private void SortConversationsPinnedFirst()
    {
        if (Conversations.Count <= 1)
            return;

        var selectedId = SelectedConversation?.Id;

        var indexed = Conversations.Select((c, i) => new { c, i }).ToList();
        var desired = indexed
            .OrderByDescending(x => x.c.IsPinned)
            .ThenBy(x => x.i)
            .Select(x => x.c)
            .ToList();

        for (var targetIndex = 0; targetIndex < desired.Count; targetIndex++)
        {
            var item = desired[targetIndex];
            var currentIndex = Conversations.IndexOf(item);
            if (currentIndex != targetIndex && currentIndex >= 0)
                Conversations.Move(currentIndex, targetIndex);
        }

        if (selectedId is not null)
            SelectedConversation = Conversations.FirstOrDefault(c => c.Id == selectedId) ?? Conversations.FirstOrDefault();
    }

    private bool CanDeleteMessage(ChatEntryModel? entry)
        => entry is { IsTypingIndicator: false }
           && SelectedConversation is not null
           && SelectedConversation.Messages.Contains(entry);

    private void DeleteMessage(ChatEntryModel? entry)
    {
        if (!CanDeleteMessage(entry))
            return;

        var convo = SelectedConversation!;
        convo.Messages.Remove(entry!);
        _memory.SaveState(ToStateModel());
        _clearHistoryCommand.RaiseCanExecuteChanged();
        _deleteMessageCommand.RaiseCanExecuteChanged();
        _memory.LogEvent("chat.message.deleted", new { convoId = convo.Id });
    }

    /// <summary>
    /// 사용자가 탭 이름 변경을 요청했고, 에이전트가 수락(알겠습니다 등)하면 요청한 이름으로 탭 제목을 바꿉니다.
    /// </summary>
    private void TryApplyConfirmedTabRename(ConversationViewModel convo, string userMessage, string agentReply)
    {
        var requested = TryExtractRequestedTabTitle(userMessage);
        if (string.IsNullOrWhiteSpace(requested))
            return;

        if (!LooksLikeAffirmativeRenameReply(agentReply))
            return;

        var title = NormalizeTabTitle(requested);
        if (string.IsNullOrWhiteSpace(title))
            return;

        convo.Title = title;
        SortConversationsPinnedFirst();
        _memory.SaveState(ToStateModel());
        _memory.LogEvent("chat.title.user_confirmed_rename", new { convoId = convo.Id, title });
    }

    private static readonly Regex[] TabTitleFromUserPatterns =
    {
        new(@"['""『「](?<t>[^'""』」\r\n]{1,48})['""』」]\s*(?:로|으로)\s*(?:바꿔|변경|해줘|해주세요|바꿔줘|바꿔줄)", RegexOptions.Compiled),
        new(@"(?:탭|대화|채팅)\s*(?:이름|제목)\s*(?:을|를)\s*['""『「]?(?<t>[^\r\n'""」]{1,48}?)['""』」]?\s*(?:로|으로)", RegexOptions.Compiled),
        new(@"(?:이름|제목)\s*(?:을|를)\s*(?<t>[^\r\n,]+?)\s*(?:로|으로)\s*(?:바꿔|변경|해줘|해주세요)", RegexOptions.Compiled),
    };

    private static string? TryExtractRequestedTabTitle(string userMessage)
    {
        var text = userMessage.Trim();
        foreach (var rx in TabTitleFromUserPatterns)
        {
            var m = rx.Match(text);
            if (!m.Success || !m.Groups["t"].Success)
                continue;

            var raw = m.Groups["t"].Value.Trim();
            if (raw.Length == 0)
                continue;

            return raw;
        }

        return null;
    }

    private static bool LooksLikeAffirmativeRenameReply(string reply)
    {
        var t = reply.Trim();
        if (t.Length == 0)
            return false;

        if (t.Contains("알겠", StringComparison.Ordinal))
            return true;
        if (t.Contains("변경했", StringComparison.Ordinal) || t.Contains("바꿨", StringComparison.Ordinal))
            return true;
        if (t.Contains("바꿔 드렸", StringComparison.Ordinal) || t.Contains("바꿔드렸", StringComparison.Ordinal))
            return true;
        if (Regex.IsMatch(t, @"처리했|반영했|수정했", RegexOptions.None))
            return true;

        return false;
    }

    private static string NormalizeTabTitle(string raw)
    {
        var s = Regex.Replace(raw.Trim(), @"\s+", " ");
        if (s.Length > 48)
            s = s[..48].TrimEnd() + "…";
        return s;
    }
}

