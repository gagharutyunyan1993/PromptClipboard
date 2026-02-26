namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;
using Serilog;
using System.Runtime.InteropServices;

public sealed class Win32InputSimulator : IInputSimulator
{
    private readonly ILogger _log;

    public Win32InputSimulator(ILogger log)
    {
        _log = log;
    }

    public uint SimulateCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        // Ctrl down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

        // V down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;

        // V up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        var structSize = Marshal.SizeOf<NativeMethods.INPUT>();
        _log.Debug("SendInput: structSize={Size}, sending 4 events", structSize);

        var sent = NativeMethods.SendInput(4, inputs, structSize);
        if (sent == 4)
        {
            _log.Debug("SendInput succeeded: {Sent}/4", sent);
            return sent;
        }

        // Fallback: keybd_event (older API, works when SendInput is blocked)
        var lastError = Marshal.GetLastWin32Error();
        _log.Warning("SendInput returned {Sent}/4 (error={Error}), falling back to keybd_event", sent, lastError);

        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(NativeMethods.VK_V, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

        _log.Debug("keybd_event fallback completed");
        return 4;
    }
}
