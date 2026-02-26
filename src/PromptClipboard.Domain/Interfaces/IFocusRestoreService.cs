namespace PromptClipboard.Domain.Interfaces;

public interface IFocusRestoreService
{
    bool RestoreFocus(IntPtr targetHwnd);
}
