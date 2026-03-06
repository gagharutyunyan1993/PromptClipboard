namespace PromptClipboard.App.Routing;

using System.Windows.Input;

public enum KeyRoutingAction
{
    None,
    MoveDown,
    MoveUp,
    RevealDown,
    RevealUp,
    Paste,
    PasteAsText,
    OpenEditor,
    HandleEscape,
    EnterQuickAdd,
    Create,
    CreateWithTitle,
    QuickAddSave,
}

public record KeyInput(Key Key, ModifierKeys Modifiers, bool IsQuickAddMode, bool ShowRevealedPrompt);

public record KeyRoutingResult(KeyRoutingAction Action, bool Handled);

public static class PaletteKeyboardRouter
{
    private static readonly KeyRoutingResult PassThrough = new(KeyRoutingAction.None, false);

    public static KeyRoutingResult Route(KeyInput input)
    {
        var (key, modifiers, isQuickAdd, showRevealed) = input;

        switch (key)
        {
            case Key.Down when isQuickAdd:
            case Key.Up when isQuickAdd:
                return PassThrough;

            case Key.Down when showRevealed:
                return new(KeyRoutingAction.RevealDown, true);
            case Key.Up when showRevealed:
                return new(KeyRoutingAction.RevealUp, true);
            case Key.Down:
                return new(KeyRoutingAction.MoveDown, true);
            case Key.Up:
                return new(KeyRoutingAction.MoveUp, true);

            case Key.Enter when modifiers == ModifierKeys.Alt:
                return isQuickAdd
                    ? new(KeyRoutingAction.None, true)
                    : new(KeyRoutingAction.OpenEditor, true);
            case Key.Enter when modifiers == ModifierKeys.Control:
                return isQuickAdd
                    ? new(KeyRoutingAction.QuickAddSave, true)
                    : new(KeyRoutingAction.PasteAsText, true);
            case Key.Enter when modifiers == ModifierKeys.None:
                return isQuickAdd
                    ? PassThrough
                    : new(KeyRoutingAction.Paste, true);

            case Key.S when modifiers == ModifierKeys.Control:
                return isQuickAdd
                    ? new(KeyRoutingAction.QuickAddSave, true)
                    : PassThrough;

            case Key.Escape:
                return new(KeyRoutingAction.HandleEscape, true);

            case Key.N when modifiers == ModifierKeys.Control:
                return new(KeyRoutingAction.EnterQuickAdd, true);
            case Key.N when modifiers == (ModifierKeys.Control | ModifierKeys.Alt):
                return new(KeyRoutingAction.Create, true);
            case Key.N when modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                return new(KeyRoutingAction.CreateWithTitle, true);

            default:
                return PassThrough;
        }
    }
}
