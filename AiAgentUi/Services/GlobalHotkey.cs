using System.Runtime.InteropServices;

namespace AiAgentUi.Services;

public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    private readonly int _id;
    private readonly nint _hwnd;
    private bool _registered;

    public event Action? Pressed;

    public GlobalHotkey(nint hwnd, int id)
    {
        _hwnd = hwnd;
        _id = id;
    }

    public bool Register(Modifiers modifiers, uint virtualKey)
    {
        _registered = RegisterHotKey(_hwnd, _id, (uint)modifiers, virtualKey);
        return _registered;
    }

    public void ProcessMessage(int msg, nint wParam)
    {
        if (msg != WM_HOTKEY)
            return;
        if ((int)wParam != _id)
            return;
        Pressed?.Invoke();
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, _id);
            _registered = false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}

