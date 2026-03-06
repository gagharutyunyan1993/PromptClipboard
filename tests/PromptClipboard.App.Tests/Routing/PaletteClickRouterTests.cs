namespace PromptClipboard.App.Tests.Routing;

using PromptClipboard.App.Routing;

public sealed class PaletteClickRouterTests
{
    [Fact]
    public void MouseDown_DoubleClick_NotButton_ReturnsSelectAndPaste()
    {
        var action = PaletteClickRouter.RouteMouseDown(clickCount: 2, isButtonClick: false);
        Assert.Equal(ClickRoutingAction.SelectAndPaste, action);
    }

    [Fact]
    public void MouseDown_SingleClick_NotButton_ReturnsNone()
    {
        var action = PaletteClickRouter.RouteMouseDown(clickCount: 1, isButtonClick: false);
        Assert.Equal(ClickRoutingAction.None, action);
    }

    [Fact]
    public void MouseDown_DoubleClick_IsButton_ReturnsNone()
    {
        var action = PaletteClickRouter.RouteMouseDown(clickCount: 2, isButtonClick: true);
        Assert.Equal(ClickRoutingAction.None, action);
    }

    [Fact]
    public void MouseDown_TripleClick_NotButton_ReturnsSelectAndPaste()
    {
        var action = PaletteClickRouter.RouteMouseDown(clickCount: 3, isButtonClick: false);
        Assert.Equal(ClickRoutingAction.SelectAndPaste, action);
    }

    [Fact]
    public void MouseUp_NotButton_ReturnsSelect()
    {
        var action = PaletteClickRouter.RouteMouseUp(isButtonClick: false);
        Assert.Equal(ClickRoutingAction.Select, action);
    }

    [Fact]
    public void MouseUp_IsButton_ReturnsNone()
    {
        var action = PaletteClickRouter.RouteMouseUp(isButtonClick: true);
        Assert.Equal(ClickRoutingAction.None, action);
    }
}
