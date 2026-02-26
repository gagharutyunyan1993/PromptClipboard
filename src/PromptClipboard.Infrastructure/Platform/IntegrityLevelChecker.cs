namespace PromptClipboard.Infrastructure.Platform;

using System.Runtime.InteropServices;
using Serilog;

public sealed class IntegrityLevelChecker
{
    private readonly ILogger _log;

    public IntegrityLevelChecker(ILogger log)
    {
        _log = log;
    }

    public int GetCurrentProcessIntegrityLevel()
    {
        return GetProcessIntegrityLevel(System.Diagnostics.Process.GetCurrentProcess().Handle) ?? 0x2000; // MEDIUM
    }

    public int? GetProcessIntegrityLevel(IntPtr targetHwnd)
    {
        NativeMethods.GetWindowThreadProcessId(targetHwnd, out var pid);
        if (pid == 0) return null;

        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return null;

        try
        {
            if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out var hToken))
                return null;

            try
            {
                NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenIntegrityLevel, IntPtr.Zero, 0, out var needed);
                var buffer = Marshal.AllocHGlobal((int)needed);
                try
                {
                    if (!NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenIntegrityLevel, buffer, needed, out _))
                        return null;

                    var sidPtr = Marshal.ReadIntPtr(buffer);
                    var subAuthorityCount = Marshal.ReadByte(sidPtr, 1);
                    var ilPtr = IntPtr.Add(sidPtr, 8 + (subAuthorityCount - 1) * 4);
                    return Marshal.ReadInt32(ilPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hToken);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }
}
