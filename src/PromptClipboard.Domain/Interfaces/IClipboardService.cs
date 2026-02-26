namespace PromptClipboard.Domain.Interfaces;

public interface IClipboardSnapshot : IDisposable
{
    bool IsEmpty { get; }
}

public interface IClipboardService
{
    IClipboardSnapshot Save();
    void SetTextWithMarker(string text, Guid operationId);
    bool HasMarker(Guid operationId);
    void Restore(IClipboardSnapshot snapshot);
}
