using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace AiAgentUi.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly ActionMemory _memory;
    private readonly Icon? _appIcon;
    private readonly Font _menuFont;
    private readonly ContextMenuStrip _menu;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService(ActionMemory memory)
    {
        _memory = memory;
        _appIcon = TryCreateIconFromLogo();

        var basis = SystemFonts.MenuFont;
        _menuFont = basis != null
            ? new Font(basis, basis.Style)
            : new Font("Segoe UI", 9f);

        _menu = BuildContextMenu();

        _tray = new NotifyIcon
        {
            Text = "AI Agent UI (Ctrl+F12)",
            Visible = true,
            Icon = _appIcon ?? SystemIcons.Application,
            ContextMenuStrip = _menu,
        };

        _tray.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _memory.LogEvent("tray.created");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font = _menuFont,
            AutoSize = true,
            DropShadowEnabled = true,
        };

        AddMenuItem(menu, "열기", "Ctrl+F12", (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "종료", null, (_, _) => ExitRequested?.Invoke());

        return menu;
    }

    private void AddMenuItem(ContextMenuStrip menu, string title, string? shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(title)
        {
            Padding = new Padding(12, 6, 12, 6),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
        if (!string.IsNullOrEmpty(shortcut))
            item.ShortcutKeyDisplayString = shortcut;

        item.Click += onClick;
        menu.Items.Add(item);
    }

    /// <summary>
    /// 트레이·알림 토스트에 동일 아이콘을 씁니다.
    /// 1) 리소스 <c>app.ico</c> — 호스트가 <c>dotnet.exe</c>이거나 경로 추적이 어려울 때도 동작합니다.
    /// 2) EXE 연결 아이콘 — DLL만 넘기면 실패하는 경우 보조.
    /// 3) <c>logo.png</c> 래스터 폴백.
    /// </summary>
    private static Icon? TryCreateIconFromLogo()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico");
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info?.Stream is not null)
            {
                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                ms.Position = 0;
                using var fromRes = new Icon(ms);
                return (Icon)fromRes.Clone();
            }
        }
        catch
        {
            // 아래 후보 시도
        }

        foreach (var exe in GetCandidateExePathsForEmbeddedIcon())
        {
            try
            {
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                    continue;

                using var fromExe = Icon.ExtractAssociatedIcon(exe);
                if (fromExe is not null)
                    return (Icon)fromExe.Clone();
            }
            catch
            {
                // 다음 후보
            }
        }

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/logo.png");
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info?.Stream is null)
                return null;

            using var ms = new MemoryStream();
            info.Stream.CopyTo(ms);
            ms.Position = 0;

            using var original = new Bitmap(ms);
            const int trayPx = 32;
            using var scaled = new Bitmap(trayPx, trayPx, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(original, 0, 0, trayPx, trayPx);
            }

            using var tmp = Icon.FromHandle(scaled.GetHicon());
            return (Icon)tmp.Clone();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 실제 앱 호스트인 .exe 경로만 모읍니다 (<c>dotnet.exe</c> 제외).
    /// DLL 옆의 동일 이름 .exe를 찾아 매니지드 출력 폴더에서 ICO를 읽습니다.
    /// </summary>
    private static IEnumerable<string> GetCandidateExePathsForEmbeddedIcon()
    {
        static string? Safe(Func<string?> get)
        {
            try
            {
                return get();
            }
            catch
            {
                return null;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in new[]
                 {
                     Safe(() => Environment.ProcessPath),
                     Safe(() =>
                     {
                         using var proc = Process.GetCurrentProcess();
                         return proc.MainModule?.FileName;
                     }),
                 })
        {
            var n = NormalizeToAppExePath(raw);
            if (n is not null && seen.Add(n))
                yield return n;
        }

        var dll = Safe(() => Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(dll))
        {
            var dir = Path.GetDirectoryName(dll);
            var baseName = Path.GetFileNameWithoutExtension(dll);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(baseName))
            {
                var sibling = Path.Combine(dir, baseName + ".exe");
                var n = NormalizeToAppExePath(sibling);
                if (n is not null && seen.Add(n))
                    yield return n;
            }
        }
    }

    private static string? NormalizeToAppExePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }

        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;
        if (path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(path) ? path : null;
    }

    public void Dispose()
    {
        _memory.LogEvent("tray.disposed");
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon?.Dispose();
        _menuFont.Dispose();
        _menu.Dispose();
    }
}
