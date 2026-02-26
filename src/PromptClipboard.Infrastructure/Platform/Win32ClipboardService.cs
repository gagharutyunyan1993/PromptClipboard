namespace PromptClipboard.Infrastructure.Platform;

using PromptClipboard.Domain.Interfaces;
using Serilog;
using System.Runtime.InteropServices;
using System.Windows;

public sealed class Win32ClipboardService : IClipboardService
{
    private readonly ILogger _log;
    private static readonly string MarkerFormat = "PromptClipboard.Marker";

    public Win32ClipboardService(ILogger log)
    {
        _log = log;
    }

    public IClipboardSnapshot Save()
    {
        return RetryClipboardOp(() =>
        {
            var data = Clipboard.GetDataObject();
            if (data == null)
                return new ClipboardSnapshot(null);

            var snapshot = new DataObject();
            foreach (var format in data.GetFormats())
            {
                try
                {
                    var obj = data.GetData(format);
                    if (obj != null)
                        snapshot.SetData(format, obj);
                }
                catch
                {
                    // Some formats may not be retrievable
                }
            }
            return new ClipboardSnapshot(snapshot);
        }, "Save");
    }

    public void SetTextWithMarker(string text, Guid operationId)
    {
        // Try native Win32 first — much more reliable than WPF/OLE
        if (TrySetTextNative(text))
        {
            _log.Debug("Clipboard text set via native Win32 API");
            return;
        }

        // Fallback to WPF
        _log.Debug("Native clipboard failed, falling back to WPF");
        RetryClipboardOp(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetData(MarkerFormat, operationId.ToString());
            Clipboard.SetDataObject(data, true);
        }, "SetTextWithMarker");
    }

    public bool HasMarker(Guid operationId)
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetDataPresent(MarkerFormat) == true)
            {
                var marker = data.GetData(MarkerFormat) as string;
                return marker == operationId.ToString();
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to check clipboard marker");
        }
        return false;
    }

    public void Restore(IClipboardSnapshot snapshot)
    {
        if (snapshot is ClipboardSnapshot cs)
        {
            RetryClipboardOp(() =>
            {
                if (cs.Data == null)
                    Clipboard.Clear();
                else
                    Clipboard.SetDataObject(cs.Data, true);
            }, "Restore");
        }
    }

    /// <summary>
    /// Sets clipboard text using native Win32 API (bypasses WPF/OLE).
    /// More reliable — same approach as Windows clipboard (Win+V).
    /// </summary>
    private bool TrySetTextNative(string text)
    {
        var delays = new[] { 30, 60, 120, 250, 500, 1000 };
        for (var attempt = 0; attempt < delays.Length; attempt++)
        {
            if (TrySetTextNativeOnce(text))
                return true;

            _log.Debug("Native OpenClipboard attempt {Attempt}/{Total} failed, retrying in {Delay}ms...",
                attempt + 1, delays.Length, delays[attempt]);
            Thread.Sleep(delays[attempt]);
        }

        // Last attempt
        return TrySetTextNativeOnce(text);
    }

    private bool TrySetTextNativeOnce(string text)
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            NativeMethods.EmptyClipboard();

            var chars = text.ToCharArray();
            var byteCount = (nuint)((chars.Length + 1) * sizeof(char));
            var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, byteCount);
            if (hGlobal == IntPtr.Zero)
                return false;

            var ptr = NativeMethods.GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(chars, 0, ptr, chars.Length);
                // Null-terminate
                Marshal.WriteInt16(ptr + chars.Length * sizeof(char), 0);
            }
            finally
            {
                NativeMethods.GlobalUnlock(hGlobal);
            }

            var result = NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal);
            if (result == IntPtr.Zero)
            {
                // SetClipboardData failed — we still own hGlobal, must free it
                NativeMethods.GlobalFree(hGlobal);
                return false;
            }
            return true;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private T RetryClipboardOp<T>(Func<T> op, string opName)
    {
        var delays = new[] { 50, 100, 200, 400, 800, 1500 };
        for (var i = 0; i <= delays.Length; i++)
        {
            try
            {
                return op();
            }
            catch (Exception ex) when (i < delays.Length)
            {
                _log.Debug("Clipboard {Op} attempt {Attempt}/{Total} failed: {Msg}, retrying in {Delay}ms...",
                    opName, i + 1, delays.Length, ex.Message, delays[i]);
                Thread.Sleep(delays[i]);
            }
        }
        throw new InvalidOperationException($"Clipboard {opName} failed after {delays.Length} retries");
    }

    private void RetryClipboardOp(Action op, string opName)
    {
        RetryClipboardOp(() => { op(); return 0; }, opName);
    }

    private sealed class ClipboardSnapshot : IClipboardSnapshot
    {
        public DataObject? Data { get; }
        public bool IsEmpty => Data == null;

        public ClipboardSnapshot(DataObject? data)
        {
            Data = data;
        }

        public void Dispose() { }
    }
}
