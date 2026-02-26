namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;
using Serilog;
using System.Runtime.InteropServices;

public sealed class Win32WindowPositioner : IWindowPositioner
{
    public const int DefaultPaletteWidth = 700;
    public const int DefaultPaletteHeight = 500;

    private readonly ILogger _log;

    public Win32WindowPositioner(ILogger log)
    {
        _log = log;
    }

    public ScreenPosition GetPositionNearCaret(IntPtr targetHwnd)
    {
        var threadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);

        var guiInfo = new NativeMethods.GUITHREADINFO();
        guiInfo.cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>();

        double dpiScale = 1.0;

        if (NativeMethods.GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
        {
            var rcCaret = guiInfo.rcCaret;
            NativeMethods.MapWindowPoints(guiInfo.hwndCaret, IntPtr.Zero, ref rcCaret, 2);

            var screenX = rcCaret.Left;
            var screenY = rcCaret.Bottom + 4; // small offset below caret

            var pt = new NativeMethods.POINT { X = screenX, Y = screenY };
            var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            dpiScale = GetDpiScale(monitor);

            var clamped = ClampToMonitor(screenX, screenY, monitor, dpiScale);
            return new ScreenPosition { X = clamped.X, Y = clamped.Y, DpiScale = dpiScale };
        }

        // Fallback: cursor position
        _log.Debug("Caret not found, using cursor position");
        if (NativeMethods.GetCursorPos(out var cursorPt))
        {
            var monitor = NativeMethods.MonitorFromPoint(cursorPt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            dpiScale = GetDpiScale(monitor);
            var clamped = ClampToMonitor(cursorPt.X, cursorPt.Y + 20, monitor, dpiScale);
            return new ScreenPosition { X = clamped.X, Y = clamped.Y, DpiScale = dpiScale };
        }

        return new ScreenPosition { X = 100, Y = 100, DpiScale = 1.0 };
    }

    private static double GetDpiScale(IntPtr monitor)
    {
        if (NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0)
            return dpiX / 96.0;
        return 1.0;
    }

    private static (double X, double Y) ClampToMonitor(int x, int y, IntPtr monitor, double dpiScale)
    {
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref mi))
            return (x / dpiScale, y / dpiScale);

        var work = mi.rcWork;
        var clampedX = Math.Max(work.Left, Math.Min(x, work.Right - DefaultPaletteWidth));
        var clampedY = Math.Max(work.Top, Math.Min(y, work.Bottom - DefaultPaletteHeight));

        // WPF uses device-independent pixels
        return (clampedX / dpiScale, clampedY / dpiScale);
    }
}
