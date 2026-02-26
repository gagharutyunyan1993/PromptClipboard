param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\docs\screenshots",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$appProject = Join-Path $repoRoot "src\PromptClipboard.App\PromptClipboard.App.csproj"
$appExe = Join-Path $repoRoot "src\PromptClipboard.App\bin\$Configuration\net8.0-windows\PromptClipboard.App.exe"
$appDataDir = Join-Path $env:APPDATA "PromptClipboard"
$settingsPath = Join-Path $appDataDir "settings.json"
$settingsBackupPath = Join-Path $appDataDir "settings.screenshots.backup.json"
$runtimeDir = Join-Path $OutputDir "_runtime"
$screenshotDbPath = Join-Path $runtimeDir "screenshots-prompts.db"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32Capture
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public const byte VK_CONTROL = 0x11;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_MENU = 0x12;
    public const byte VK_Q = 0x51;
    public const byte VK_RETURN = 0x0D;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
"@

function Get-WindowTitle([IntPtr]$hWnd) {
    if ($hWnd -eq [IntPtr]::Zero) { return "" }
    $builder = New-Object System.Text.StringBuilder 512
    [void][Win32Capture]::GetWindowText($hWnd, $builder, $builder.Capacity)
    return $builder.ToString()
}

function Find-WindowHandleByTitle([string[]]$titleContains) {
    $result = [IntPtr]::Zero
    $callback = [Win32Capture+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        if (-not [Win32Capture]::IsWindowVisible($hWnd)) {
            return $true
        }

        $title = Get-WindowTitle $hWnd
        foreach ($token in $titleContains) {
            if ($title -like "*$token*") {
                $script:__windowMatch = $hWnd
                return $false
            }
        }
        return $true
    }

    $script:__windowMatch = [IntPtr]::Zero
    [void][Win32Capture]::EnumWindows($callback, [IntPtr]::Zero)
    $result = $script:__windowMatch
    Remove-Variable __windowMatch -Scope Script -ErrorAction SilentlyContinue
    return $result
}

function Wait-ForWindowByTitle([string[]]$titleContains, [int]$timeoutMs = 10000) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $hwnd = Find-WindowHandleByTitle $titleContains
        if ($hwnd -ne [IntPtr]::Zero) {
            return $hwnd
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Window not found. Expected title containing: $($titleContains -join ', ')"
}

function Save-WindowScreenshot([IntPtr]$hWnd, [string]$filePath) {
    if ($hWnd -eq [IntPtr]::Zero) {
        throw "Cannot capture screenshot for zero window handle"
    }

    $rect = New-Object Win32Capture+RECT
    if (-not [Win32Capture]::GetWindowRect($hWnd, [ref]$rect)) {
        throw "GetWindowRect failed for hwnd $hWnd"
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Invalid window size $width x $height"
    }

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.CopyFromScreen(
            $rect.Left,
            $rect.Top,
            0,
            0,
            (New-Object System.Drawing.Size($width, $height)),
            [System.Drawing.CopyPixelOperation]::SourceCopy
        )
        $bitmap.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Send-Keys([string]$keys, [int]$delayMs = 500) {
    try {
        [System.Windows.Forms.SendKeys]::SendWait($keys)
    }
    catch {
        $wsh = New-Object -ComObject WScript.Shell
        $wsh.SendKeys($keys)
    }
    Start-Sleep -Milliseconds $delayMs
}

function Send-CtrlShiftQ([int]$delayMs = 700) {
    [Win32Capture]::keybd_event([Win32Capture]::VK_CONTROL, 0, 0, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_SHIFT, 0, 0, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_Q, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [Win32Capture]::keybd_event([Win32Capture]::VK_Q, 0, [Win32Capture]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_SHIFT, 0, [Win32Capture]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_CONTROL, 0, [Win32Capture]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $delayMs
}

function Send-AltEnter([int]$delayMs = 700) {
    [Win32Capture]::keybd_event([Win32Capture]::VK_MENU, 0, 0, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_RETURN, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [Win32Capture]::keybd_event([Win32Capture]::VK_RETURN, 0, [Win32Capture]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [Win32Capture]::keybd_event([Win32Capture]::VK_MENU, 0, [Win32Capture]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $delayMs
}

function Focus-Window([IntPtr]$hWnd, [int]$delayMs = 180) {
    if ($hWnd -eq [IntPtr]::Zero) {
        throw "Cannot focus zero window handle"
    }
    [void][Win32Capture]::SetForegroundWindow($hWnd)
    Start-Sleep -Milliseconds $delayMs
}

function Click-WindowRelative([IntPtr]$hWnd, [double]$relativeX, [double]$relativeY, [int]$delayMs = 120) {
    if ($hWnd -eq [IntPtr]::Zero) {
        throw "Cannot click on zero window handle"
    }

    $rect = New-Object Win32Capture+RECT
    if (-not [Win32Capture]::GetWindowRect($hWnd, [ref]$rect)) {
        throw "GetWindowRect failed for click target"
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $x = [int]($rect.Left + ($width * $relativeX))
    $y = [int]($rect.Top + ($height * $relativeY))

    [void][Win32Capture]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 60
    [Win32Capture]::mouse_event([Win32Capture]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 35
    [Win32Capture]::mouse_event([Win32Capture]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $delayMs
}

function Ensure-PaletteWindow([int]$timeoutMs = 2500) {
    try {
        $palette = Wait-ForWindowByTitle @("Prompt Clipboard") $timeoutMs
        Focus-Window $palette
        return $palette
    }
    catch {
        Send-CtrlShiftQ 700
        $palette = Wait-ForWindowByTitle @("Prompt Clipboard") $timeoutMs
        Focus-Window $palette
        return $palette
    }
}

function Type-Query([IntPtr]$palette, [string]$query) {
    if ([string]::IsNullOrEmpty($query)) {
        return
    }
    Click-WindowRelative $palette 0.16 0.09 120
    Send-Keys $query 650
}

function Open-Palette([string]$query = "") {
    Send-CtrlShiftQ 850
    $palette = Ensure-PaletteWindow
    Type-Query $palette $query
    return $palette
}

function Close-Palette() {
    Send-CtrlShiftQ 450
    Start-Sleep -Milliseconds 250
}

function Open-NewPromptWindow([IntPtr]$palette) {
    $coords = @(
        @(0.94, 0.09),
        @(0.91, 0.09),
        @(0.96, 0.09),
        @(0.94, 0.12)
    )

    foreach ($xy in $coords) {
        Click-WindowRelative $palette $xy[0] $xy[1] 800
        try {
            return (Wait-ForWindowByTitle @("New prompt") 1800)
        }
        catch { }
    }

    Focus-Window $palette
    Send-Keys "{TAB}{TAB}{TAB}{TAB}{TAB}{ENTER}" 900
    return (Wait-ForWindowByTitle @("New prompt") 2500)
}

function Open-EditPromptWindow([IntPtr]$palette) {
    Focus-Window $palette

    Send-AltEnter 900
    try {
        return (Wait-ForWindowByTitle @("Edit prompt") 1800)
    }
    catch { }

    $coords = @(
        @(0.86, 0.29),
        @(0.84, 0.29),
        @(0.88, 0.29),
        @(0.86, 0.33)
    )

    foreach ($xy in $coords) {
        Click-WindowRelative $palette $xy[0] $xy[1] 850
        try {
            return (Wait-ForWindowByTitle @("Edit prompt") 1800)
        }
        catch { }
    }

    throw "Unable to open Edit prompt window"
}

function Capture-Step([IntPtr]$hWnd, [string]$fileName) {
    $path = Join-Path $OutputDir $fileName
    Save-WindowScreenshot $hWnd $path
}

function Capture-ByTitleOrFallback([string[]]$titles, [IntPtr]$fallbackHwnd, [string]$fileName, [int]$timeoutMs = 5000) {
    try {
        $hwnd = Wait-ForWindowByTitle $titles $timeoutMs
    }
    catch {
        Write-Warning "Window not found by title ($($titles -join ', ')); using fallback."
        $hwnd = $fallbackHwnd
    }
    Focus-Window $hwnd
    Capture-Step $hwnd $fileName
    return $hwnd
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null
Get-ChildItem -LiteralPath $OutputDir -Filter "*.png" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
if (Test-Path $screenshotDbPath) {
    Remove-Item $screenshotDbPath -Force -ErrorAction SilentlyContinue
}

if (-not $SkipBuild) {
    Write-Host "Building PromptClipboard.App ($Configuration)..."
    dotnet build $appProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed"
    }
}

if (-not (Test-Path $appExe)) {
    throw "Executable not found: $appExe"
}

$appProcess = $null
$settingsBackedUp = $false

try {
    if (Test-Path $settingsPath) {
        Copy-Item $settingsPath $settingsBackupPath -Force
        $settingsBackedUp = $true
    }

    $screenshotSettings = @{
        hotkey = "Ctrl+Shift+Q"
        hotkeyModifiers = 6
        hotkeyVk = 81
        dbPath = $screenshotDbPath
        autoStart = $false
        pasteDelayMs = 50
        restoreDelayMs = 150
    } | ConvertTo-Json -Depth 3
    Set-Content -LiteralPath $settingsPath -Value $screenshotSettings -Encoding UTF8

    $runningBefore = @(Get-Process PromptClipboard.App -ErrorAction SilentlyContinue)
    if ($runningBefore.Count -gt 0) {
        $runningBefore | Stop-Process -Force
        Start-Sleep -Milliseconds 700
    }

    $appProcess = Start-Process -FilePath $appExe -PassThru
    Start-Sleep -Milliseconds 1600

    if ($null -eq $appProcess -or $appProcess.HasExited) {
        throw "PromptClipboard.App process is not running"
    }

    Write-Host "Capturing: home"
    $palette = Open-Palette
    Capture-Step $palette "01-home.png"
    Close-Palette

    Write-Host "Capturing: keyword search"
    $palette = Open-Palette "email"
    Capture-Step $palette "02-search-email.png"
    Close-Palette

    Write-Host "Capturing: tag filter"
    $palette = Open-Palette "#work"
    Capture-Step $palette "03-filter-tag-work.png"
    Close-Palette

    Write-Host "Capturing: Jira search"
    $palette = Open-Palette "jira"
    Capture-Step $palette "04-search-jira.png"
    Close-Palette

    Write-Host "Capturing: no results"
    $palette = Open-Palette "zzzz_not_found"
    Capture-Step $palette "05-empty-results.png"
    Close-Palette

    Write-Host "Capturing: code search"
    $palette = Open-Palette "code"
    Capture-Step $palette "07-search-code.png"
    Close-Palette

    Write-Host "Capturing: template dialog"
    $palette = Open-Palette "email"
    Send-Keys "{ENTER}" 900
    try {
        $templateDialog = Wait-ForWindowByTitle @("Fill template", "Template") 4000
    }
    catch {
        Send-Keys "{ENTER}" 900
        $templateDialog = Wait-ForWindowByTitle @("Fill template", "Template") 4000
    }
    Focus-Window $templateDialog
    Capture-Step $templateDialog "06-template-dialog.png"
    Send-Keys "%{F4}" 450
    Close-Palette

    Write-Host "Capturing: new prompt"
    $palette = Open-Palette
    $newPromptWindow = Open-NewPromptWindow $palette
    Focus-Window $newPromptWindow
    Capture-Step $newPromptWindow "08-new-prompt.png"
    Click-WindowRelative $newPromptWindow 0.20 0.19 140
    Send-Keys "Status update for team" 220
    Click-WindowRelative $newPromptWindow 0.20 0.35 140
    Send-Keys "Write key updates and blockers." 220
    Click-WindowRelative $newPromptWindow 0.20 0.64 140
    Send-Keys "work,team" 220
    Click-WindowRelative $newPromptWindow 0.20 0.78 140
    Send-Keys "General" 220
    Click-WindowRelative $newPromptWindow 0.56 0.78 140
    Send-Keys "en" 220
    Capture-Step $newPromptWindow "09-new-prompt-filled.png"
    Send-Keys "%{F4}" 450
    Close-Palette

    Write-Host "Capturing: edit prompt"
    $palette = Open-Palette "email"
    $editorWindow = Open-EditPromptWindow $palette
    Focus-Window $editorWindow
    Capture-Step $editorWindow "10-edit-prompt.png"
    Send-Keys "%{F4}" 450
    Close-Palette

    Write-Host "Screenshots saved to $OutputDir"
    Get-ChildItem -LiteralPath $OutputDir -Filter "*.png" |
        Sort-Object Name |
        ForEach-Object { Write-Host " - $($_.Name)" }
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force
    }

    if ($settingsBackedUp -and (Test-Path $settingsBackupPath)) {
        Move-Item -Path $settingsBackupPath -Destination $settingsPath -Force
    }
    elseif (-not $settingsBackedUp -and (Test-Path $settingsPath)) {
        Remove-Item $settingsPath -Force -ErrorAction SilentlyContinue
    }
}
