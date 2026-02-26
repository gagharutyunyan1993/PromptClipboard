namespace PromptClipboard.Domain.Interfaces;

public interface IFocusTracker
{
    IntPtr SavedHwnd { get; }
    void CaptureForegroundWindow();
    void ClearSavedHwnd();
    IntPtr GetCurrentForegroundWindow();
    bool IsWindowValid(IntPtr hwnd);
}
