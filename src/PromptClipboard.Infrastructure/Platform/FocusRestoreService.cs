namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;
using Serilog;

public sealed class FocusRestoreService : IFocusRestoreService
{
    private readonly ILogger _log;

    public FocusRestoreService(ILogger log)
    {
        _log = log;
    }

    public bool RestoreFocus(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero)
            return false;

        // Level 1: Direct SetForegroundWindow
        NativeMethods.SetForegroundWindow(targetHwnd);
        if (NativeMethods.GetForegroundWindow() == targetHwnd)
        {
            _log.Debug("Focus restored via Level 1 (SetForegroundWindow)");
            return true;
        }

        // Level 2: AttachThreadInput + SetForegroundWindow
        var ourThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
        try
        {
            NativeMethods.AttachThreadInput(ourThread, targetThread, true);
            NativeMethods.SetForegroundWindow(targetHwnd);
        }
        finally
        {
            NativeMethods.AttachThreadInput(ourThread, targetThread, false);
        }
        if (NativeMethods.GetForegroundWindow() == targetHwnd)
        {
            _log.Debug("Focus restored via Level 2 (AttachThreadInput)");
            return true;
        }

        // Level 3: ALT trick
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.SetForegroundWindow(targetHwnd);
        if (NativeMethods.GetForegroundWindow() == targetHwnd)
        {
            _log.Debug("Focus restored via Level 3 (ALT trick)");
            return true;
        }

        // Level 4: AllowSetForegroundWindow
        NativeMethods.GetWindowThreadProcessId(targetHwnd, out var targetPid);
        NativeMethods.AllowSetForegroundWindow((int)targetPid);
        NativeMethods.SetForegroundWindow(targetHwnd);
        if (NativeMethods.GetForegroundWindow() == targetHwnd)
        {
            _log.Debug("Focus restored via Level 4 (AllowSetForegroundWindow)");
            return true;
        }

        _log.Warning("All focus restore levels failed for hwnd {Hwnd}", targetHwnd);
        return false;
    }
}
