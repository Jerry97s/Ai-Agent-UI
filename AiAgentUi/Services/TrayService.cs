using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace AiAgentUi.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly ActionMemory _memory;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService(ActionMemory memory)
    {
        _memory = memory;

        _tray = new NotifyIcon
        {
            Text = "AI Agent UI (Ctrl+F12)",
            Visible = true,
            Icon = SystemIcons.Application,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("열기 (Ctrl+F12)", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitRequested?.Invoke());
        _tray.ContextMenuStrip = menu;

        _tray.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _memory.LogEvent("tray.created");
    }

    public void ShowBalloon(string title, string message, int timeoutMs = 2500)
    {
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = message;
        _tray.ShowBalloonTip(timeoutMs);
    }

    public void Dispose()
    {
        _memory.LogEvent("tray.disposed");
        _tray.Visible = false;
        _tray.Dispose();
    }
}

