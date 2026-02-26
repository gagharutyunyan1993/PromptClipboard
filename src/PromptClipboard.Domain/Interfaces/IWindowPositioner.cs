namespace PromptClipboard.Domain.Interfaces;

public struct ScreenPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double DpiScale { get; set; }
}

public interface IWindowPositioner
{
    ScreenPosition GetPositionNearCaret(IntPtr targetHwnd);
}
