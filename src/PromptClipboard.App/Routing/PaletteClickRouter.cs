namespace PromptClipboard.App.Routing;

public enum ClickRoutingAction
{
    None,
    Select,
    SelectAndPaste,
}

public static class PaletteClickRouter
{
    public static ClickRoutingAction RouteMouseDown(int clickCount, bool isButtonClick)
        => isButtonClick ? ClickRoutingAction.None
            : clickCount >= 2 ? ClickRoutingAction.SelectAndPaste
            : ClickRoutingAction.None;

    public static ClickRoutingAction RouteMouseUp(bool isButtonClick)
        => isButtonClick ? ClickRoutingAction.None : ClickRoutingAction.Select;
}
