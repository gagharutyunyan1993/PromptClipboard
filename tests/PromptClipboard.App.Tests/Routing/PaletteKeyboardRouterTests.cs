namespace PromptClipboard.App.Tests.Routing;

using System.Windows.Input;
using PromptClipboard.App.Routing;

public sealed class PaletteKeyboardRouterTests
{
    // --- Arrow keys in QuickAdd = pass-through ---

    [Fact]
    public void ArrowDown_QuickAdd_ReturnsNone()
    {
        var result = Route(Key.Down, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    [Fact]
    public void ArrowUp_QuickAdd_ReturnsNone()
    {
        var result = Route(Key.Up, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    // --- Arrow keys in List mode = navigation ---

    [Fact]
    public void ArrowDown_ListMode_ReturnsMoveDown()
    {
        var result = Route(Key.Down);
        Assert.Equal(KeyRoutingAction.MoveDown, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void ArrowUp_ListMode_ReturnsMoveUp()
    {
        var result = Route(Key.Up);
        Assert.Equal(KeyRoutingAction.MoveUp, result.Action);
        Assert.True(result.Handled);
    }

    // --- Arrow keys with revealed prompt ---

    [Fact]
    public void ArrowDown_Revealed_ReturnsRevealDown()
    {
        var result = Route(Key.Down, showRevealed: true);
        Assert.Equal(KeyRoutingAction.RevealDown, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void ArrowUp_Revealed_ReturnsRevealUp()
    {
        var result = Route(Key.Up, showRevealed: true);
        Assert.Equal(KeyRoutingAction.RevealUp, result.Action);
        Assert.True(result.Handled);
    }

    // --- Enter variants ---

    [Fact]
    public void Enter_NoMod_ListMode_ReturnsPaste()
    {
        var result = Route(Key.Enter);
        Assert.Equal(KeyRoutingAction.Paste, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Enter_NoMod_QuickAdd_ReturnsNone()
    {
        var result = Route(Key.Enter, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Enter_Ctrl_ListMode_ReturnsPasteAsText()
    {
        var result = Route(Key.Enter, ModifierKeys.Control);
        Assert.Equal(KeyRoutingAction.PasteAsText, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Enter_Ctrl_QuickAdd_ReturnsQuickAddSave()
    {
        var result = Route(Key.Enter, ModifierKeys.Control, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.QuickAddSave, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Enter_Alt_ListMode_ReturnsOpenEditor()
    {
        var result = Route(Key.Enter, ModifierKeys.Alt);
        Assert.Equal(KeyRoutingAction.OpenEditor, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Enter_Alt_QuickAdd_HandledNoAction()
    {
        var result = Route(Key.Enter, ModifierKeys.Alt, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.True(result.Handled);
    }

    // --- Ctrl+S ---

    [Fact]
    public void CtrlS_QuickAdd_ReturnsQuickAddSave()
    {
        var result = Route(Key.S, ModifierKeys.Control, isQuickAdd: true);
        Assert.Equal(KeyRoutingAction.QuickAddSave, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void CtrlS_ListMode_ReturnsNone()
    {
        var result = Route(Key.S, ModifierKeys.Control);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    // --- Escape ---

    [Fact]
    public void Escape_ReturnsHandleEscape()
    {
        var result = Route(Key.Escape);
        Assert.Equal(KeyRoutingAction.HandleEscape, result.Action);
        Assert.True(result.Handled);
    }

    // --- Create shortcuts ---

    [Fact]
    public void CtrlN_ReturnsEnterQuickAdd()
    {
        var result = Route(Key.N, ModifierKeys.Control);
        Assert.Equal(KeyRoutingAction.EnterQuickAdd, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void CtrlAltN_ReturnsCreate()
    {
        var result = Route(Key.N, ModifierKeys.Control | ModifierKeys.Alt);
        Assert.Equal(KeyRoutingAction.Create, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void CtrlShiftN_ReturnsCreateWithTitle()
    {
        var result = Route(Key.N, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.Equal(KeyRoutingAction.CreateWithTitle, result.Action);
        Assert.True(result.Handled);
    }

    // --- Unrecognized keys ---

    [Fact]
    public void UnrecognizedKey_ReturnsNone()
    {
        var result = Route(Key.F12);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Letter_NoModifier_ReturnsNone()
    {
        var result = Route(Key.A);
        Assert.Equal(KeyRoutingAction.None, result.Action);
        Assert.False(result.Handled);
    }

    // --- Helpers ---

    private static KeyRoutingResult Route(
        Key key,
        ModifierKeys modifiers = ModifierKeys.None,
        bool isQuickAdd = false,
        bool showRevealed = false)
        => PaletteKeyboardRouter.Route(new KeyInput(key, modifiers, isQuickAdd, showRevealed));
}
