using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AiAgentUi.Services;
using AiAgentUi.ViewModels;

namespace AiAgentUi.Views;

public partial class MainView : Window
{
    private ConversationViewModel? _wiredConversation;
    private readonly AgentApiClient _agent;
    private readonly MainViewModel _vm;
    private ActionMemory Memory => ((App)System.Windows.Application.Current).Memory;

    public MainView()
    {
        InitializeComponent();
        _agent = new AgentApiClient(AgentBaseUrl);
        _vm = new MainViewModel(_agent, Memory, new FileDialogService());
        DataContext = _vm;

        Memory.LogEvent("main.created");

        Loaded += (_, _) => WireConversationScroll();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedConversation))
                WireConversationScroll();
        };
    }

    internal const string AgentBaseUrl = "http://127.0.0.1:8787";

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!((App)System.Windows.Application.Current).ExitRequested)
        {
            e.Cancel = true;
            Hide();
            _vm.Persist();
            Memory.LogEvent("main.hidden");
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Persist();
        _agent.Dispose();
        Memory.LogEvent("main.closed");
        base.OnClosed(e);
    }

    private void ScrollChatToBottom()
    {
        if (!IsLoaded)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (ChatList.Items.Count > 0)
                ChatList.ScrollIntoView(ChatList.Items[^1]);
        }, DispatcherPriority.Background);
    }

    private void WireConversationScroll()
    {
        try
        {
            var convo = _vm.SelectedConversation;
            if (convo is null)
                return;

            if (_wiredConversation is not null)
                _wiredConversation.Messages.CollectionChanged -= ConversationMessages_CollectionChanged;

            _wiredConversation = convo;
            _wiredConversation.Messages.CollectionChanged += ConversationMessages_CollectionChanged;
            ScrollChatToBottom();
        }
        catch
        {
            // ignore wiring issues
        }
    }

    private void ConversationMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => ScrollChatToBottom();

    private void MessageBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            return;

        if (DataContext is MainViewModel vm && vm.SendCommand.CanExecute(null))
        {
            e.Handled = true;
            vm.SendCommand.Execute(null);
        }
    }

    private void Root_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Root_Drop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            var files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files is null || files.Length == 0)
                return;

            // Filter directories out; only files.
            var paths = files.Where(File.Exists).ToArray();
            if (paths.Length == 0)
                return;

            await _vm.AnalyzeFilesAsync(paths);
        }
        catch (Exception ex)
        {
            Memory.LogEvent("file.drop.error", new { ex.Message });
        }
    }
}

