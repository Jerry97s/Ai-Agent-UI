using System.Windows;
using System.Windows.Interop;
using AiAgentUi.Services;
using AiAgentUi.Views;

namespace AiAgentUi;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const int HotkeyId = 0xA11; // arbitrary
    private HwndSource? _source;
    private GlobalHotkey? _hotkey;
    private TrayService? _tray;
    private ActionMemory? _memory;
    private bool _exitRequested;
    private Window? _hotkeyWindow;

    internal ActionMemory Memory => _memory ?? throw new InvalidOperationException("Memory not initialized.");
    internal bool ExitRequested => _exitRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _memory = new ActionMemory();
        _tray = new TrayService(_memory);
        _tray.OpenRequested += () => Dispatcher.Invoke(ShowOrActivateMainWindow);
        _tray.ExitRequested += () => Dispatcher.Invoke(RequestExit);

        _hotkeyWindow = new Window
        {
            Width = 0,
            Height = 0,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
            Opacity = 0, // no transparency mode needed
        };
        _hotkeyWindow.SourceInitialized += (_, _) =>
        {
            _source = (HwndSource)PresentationSource.FromVisual(_hotkeyWindow)!;
            _source.AddHook(WndProc);

            _hotkey = new GlobalHotkey(_source.Handle, HotkeyId);
            var ok = _hotkey.Register(
                GlobalHotkey.Modifiers.Control | GlobalHotkey.Modifiers.NoRepeat,
                VkF12);

            _memory.LogEvent("hotkey.register", new { ok, keys = "Ctrl+F12" });

            if (!ok)
                _tray?.ShowBalloon("AI Agent UI", "Ctrl+F12 전역 단축키 등록에 실패했습니다. (이미 사용 중일 수 있어요)");
        };
        _hotkeyWindow.Show();
        _hotkeyWindow.Hide();

        // Ensure we have a main view instance but start hidden (tray-first).
        if (MainWindow is null)
        {
            MainWindow = new MainView();
        }

        MainWindow.Loaded += (_, _) =>
        {
            _memory.LogEvent("main.loaded");
            MainWindow.Hide();
            _tray?.ShowBalloon("AI Agent UI", "백그라운드에서 실행 중입니다. (Ctrl+F12로 열기)");
        };

        _memory.LogEvent("app.started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _memory?.LogEvent("app.exit");
        _hotkey?.Dispose();
        _source?.RemoveHook(WndProc);
        _tray?.Dispose();
        _hotkeyWindow?.Close();
        base.OnExit(e);
    }

    private void RequestExit()
    {
        _exitRequested = true;
        _memory?.LogEvent("app.exit.requested");
        Shutdown();
    }

    private void ShowOrActivateMainWindow()
    {
        if (MainWindow is null)
            MainWindow = new MainView();

        if (!MainWindow.IsVisible)
            MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
            MainWindow.WindowState = WindowState.Normal;

        MainWindow.Activate();
        MainWindow.Topmost = true; // bring to front
        MainWindow.Topmost = false;
        MainWindow.Focus();
        _memory?.LogEvent("main.show");
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        _hotkey?.ProcessMessage(msg, wParam);
        if (msg == 0x0312 && (int)wParam == HotkeyId)
        {
            handled = true;
            Dispatcher.BeginInvoke(ShowOrActivateMainWindow);
        }
        return 0;
    }

    private const uint VkF12 = 0x7B;
}

