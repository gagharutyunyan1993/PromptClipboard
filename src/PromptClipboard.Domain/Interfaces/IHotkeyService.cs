namespace PromptClipboard.Domain.Interfaces;

public interface IHotkeyService : IDisposable
{
    event Action? HotkeyPressed;
    bool Register(uint modifiers, uint vk);
    void Unregister();
}
