using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AiAgentUi.Services;
using AiAgentUi.Views;

namespace AiAgentUi;

/// <summary>
/// Interaction logic for App.xaml — StartupUri로 메인 창 표시, 코드에서는 트레이·전역 단축키만 추가.
/// </summary>
public partial class App : System.Windows.Application
{
    private const int HotkeyId = 0xA11;
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
        // WPF가 MainView.xaml을 띄우고 MainWindow를 연결
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _memory = new ActionMemory();
        _tray = new TrayService(_memory);
        _tray.OpenRequested += () => Dispatcher.Invoke(ShowOrActivateMainWindow);
        _tray.ExitRequested += () => Dispatcher.Invoke(RequestExit);

        _memory.LogEvent("app.started");

        // 핫키용 1×1 창을 먼저 띄우면 메인이 뒤에 남는 경우가 있어, UI를 먼저 앞으로
        Dispatcher.BeginInvoke(BringMainWindowToForeground, DispatcherPriority.ApplicationIdle);
        Dispatcher.BeginInvoke(InitializeGlobalHotkeyShell, DispatcherPriority.Background);

        Dispatcher.BeginInvoke(
            () => ShowStartupHintNearTray(),
            DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 셸 <see cref="System.Windows.Forms.NotifyIcon.ShowBalloonTip"/> 은 프로세스(예: dotnet.exe) 아이콘을 쓸 수 있어
    /// 우리 ICO가 안 나옵니다. 리소스 아이콘이 확실히 적용되는 WPF 미니 창으로 안내합니다.
    /// </summary>
    private void ShowStartupHintNearTray()
    {
        ShowTrayHintWindow(
            "AiAgentUi",
            "AI Agent UI",
            "창을 닫으면 트레이로 숨깁니다. Ctrl+F12로 다시 열 수 있어요.");
    }

    private void ShowTrayHintWindow(string titleBar, string headline, string body, int dismissMs = 3200)
    {
        ImageSource? ico;
        try
        {
            ico = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch
        {
            ico = null;
        }

        var win = new Window
        {
            Title = titleBar,
            Icon = ico,
            Width = 328,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            Topmost = true,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
            Background = System.Windows.Media.Brushes.White,
        };

        var root = new StackPanel { Margin = new Thickness(14, 11, 14, 13) };
        root.Children.Add(new TextBlock
        {
            Text = headline,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 27)),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(new TextBlock
        {
            Text = body,
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 12.5,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(113, 113, 122)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
        });
        win.Content = root;

        win.Loaded += (_, _) =>
        {
            win.UpdateLayout();
            var area = SystemParameters.WorkArea;
            win.Left = area.Right - win.ActualWidth - 16;
            win.Top = area.Bottom - win.ActualHeight - 12;
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(dismissMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                win.Close();
            }
            catch
            {
                // 무시
            }
        };

        timer.Start();
        win.Show();
    }

    private void BringMainWindowToForeground()
    {
        if (MainWindow is null)
            return;

        MainWindow.ShowInTaskbar = true;
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Show();
        MainWindow.ShowActivated = true;

        var helper = new WindowInteropHelper(MainWindow);
        helper.EnsureHandle();
        var hwnd = helper.Handle;
        ShowWindow(hwnd, SW_SHOW);
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);
        MainWindow.Activate();
    }

    private void InitializeGlobalHotkeyShell()
    {
        if (_hotkeyWindow is not null)
            return;

        ImageSource? hotkeyIco = null;
        try
        {
            hotkeyIco = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch
        {
            // 무시
        }

        _hotkeyWindow = new Window
        {
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
            Opacity = 1,
            ShowActivated = false,
            Icon = hotkeyIco,
        };
        _hotkeyWindow.SourceInitialized += (_, _) =>
        {
            _source = (HwndSource)PresentationSource.FromVisual(_hotkeyWindow)!;
            _source.AddHook(WndProc);

            _hotkey = new GlobalHotkey(_source.Handle, HotkeyId);
            var ok = _hotkey.Register(
                GlobalHotkey.Modifiers.Control | GlobalHotkey.Modifiers.NoRepeat,
                VkF12);

            _memory?.LogEvent("hotkey.register", new { ok, keys = "Ctrl+F12" });

            if (!ok)
                Dispatcher.BeginInvoke(() =>
                    ShowTrayHintWindow(
                        "AiAgentUi",
                        "AI Agent UI",
                        "Ctrl+F12 전역 단축키 등록에 실패했습니다. (이미 사용 중일 수 있어요)"));
        };
        _hotkeyWindow.Show();
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

        var w = MainWindow;
        w.ShowInTaskbar = true;

        if (!w.IsVisible)
            w.Show();

        if (w.WindowState == WindowState.Minimized)
            w.WindowState = WindowState.Normal;

        w.ShowActivated = true;
        var helper = new WindowInteropHelper(w);
        helper.EnsureHandle();
        var hwnd = helper.Handle;
        ShowWindow(hwnd, SW_SHOW);
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);

        w.Activate();
        w.Topmost = true;
        w.Topmost = false;
        w.Focus();
        _memory?.LogEvent("main.show");
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        _hotkey?.ProcessMessage(msg, wParam);
        if (msg == 0x0312 && (int)wParam == HotkeyId)
        {
            handled = true;
            Dispatcher.BeginInvoke(ShowOrActivateMainWindow, DispatcherPriority.Normal);
        }
        return IntPtr.Zero;
    }

    private const uint VkF12 = 0x7B;

    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);
}
