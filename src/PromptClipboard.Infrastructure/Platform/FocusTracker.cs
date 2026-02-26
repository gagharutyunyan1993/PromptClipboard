namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;

public sealed class FocusTracker : IFocusTracker
{
    public IntPtr SavedHwnd { get; private set; }

    public void CaptureForegroundWindow()
    {
        SavedHwnd = NativeMethods.GetForegroundWindow();
    }

    public void ClearSavedHwnd()
    {
        SavedHwnd = IntPtr.Zero;
    }

    public IntPtr GetCurrentForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public bool IsWindowValid(IntPtr hwnd)
    {
        return NativeMethods.IsWindow(hwnd);
    }
}
