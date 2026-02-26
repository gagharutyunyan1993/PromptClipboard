namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;
using System.Windows.Interop;
using Serilog;

public sealed class Win32HotkeyService : IHotkeyService
{
    private const int HOTKEY_ID = 0x0001;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private readonly ILogger _log;
    private bool _registered;

    public event Action? HotkeyPressed;

    public Win32HotkeyService(ILogger log)
    {
        _log = log;
    }

    public void Initialize(HwndSource hwndSource)
    {
        _hwndSource = hwndSource;
        _hwnd = hwndSource.Handle;
        hwndSource.AddHook(WndProc);
    }

    public bool Register(uint modifiers, uint vk)
    {
        if (_hwnd == IntPtr.Zero)
        {
            _log.Error("Cannot register hotkey: HwndSource not initialized");
            return false;
        }

        Unregister();

        if (NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            _registered = true;
            _log.Information("Hotkey registered: modifiers=0x{Modifiers:X}, vk=0x{Vk:X}", modifiers, vk);
            return true;
        }

        _log.Warning("Failed to register hotkey (may be occupied by another application)");
        return false;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            handled = true;
            HotkeyPressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _hwndSource?.RemoveHook(WndProc);
    }
}
