param(
    [string]$Version = "1.0.0.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\artifacts",
    [string]$PfxPath = "$PSScriptRoot\PromptClipboard.pfx",
    [string]$PfxPassword = $env:SIGNING_CERTIFICATE_PASSWORD
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Resolve-Path "$PSScriptRoot\.."
$AppProject = "$ProjectRoot\src\PromptClipboard.App\PromptClipboard.App.csproj"
$PackagingDir = $PSScriptRoot
$StagingDir = "$OutputDir\msix-staging"
$PublishDir = "$OutputDir\publish"

Write-Host "=== Building Prompt Clipboard MSIX v$Version ===" -ForegroundColor Cyan

# 1. Clean
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

# 2. Publish self-contained app
Write-Host "Publishing app..." -ForegroundColor Yellow
dotnet publish $AppProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -o $PublishDir `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 3. Stage MSIX layout
Write-Host "Staging MSIX layout..." -ForegroundColor Yellow
Copy-Item "$PublishDir\*" $StagingDir -Recurse

# Copy manifest (update version)
$manifest = Get-Content "$PackagingDir\Package.appxmanifest" -Raw
$manifest = $manifest -replace 'Version="1\.0\.0\.0"', "Version=`"$Version`""
Set-Content "$StagingDir\AppxManifest.xml" $manifest -Encoding UTF8

# Copy assets
$assetsTarget = "$StagingDir\Assets"
New-Item -ItemType Directory -Path $assetsTarget -Force | Out-Null
Copy-Item "$PackagingDir\Assets\*" $assetsTarget

# 4. Find MakeAppx.exe — search for the actual file, not just the directory
$found = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
    Select-Object -First 1

if (-not $found) { throw "MakeAppx.exe not found. Install Windows 10 SDK with 'App packaging tools'." }

$makeAppx = $found.FullName
$signTool = Join-Path $found.Directory.FullName "signtool.exe"
Write-Host "Using SDK tools from: $($found.Directory.FullName)" -ForegroundColor Gray

# 5. Create MSIX
$msixPath = "$OutputDir\PromptClipboard-$Version-x64.msix"
Write-Host "Creating MSIX package..." -ForegroundColor Yellow
& $makeAppx pack /d $StagingDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed" }

# 6. Sign with persistent certificate
if (-not (Test-Path $PfxPath)) { throw "Certificate not found at $PfxPath. See SETUP_SECRETS.md." }
if (-not $PfxPassword) { throw "PfxPassword is required. Set env:SIGNING_CERTIFICATE_PASSWORD or pass -PfxPassword." }
& $signTool sign /fd SHA256 /a /f $PfxPath /p $PfxPassword $msixPath
if ($LASTEXITCODE -ne 0) { Write-Warning "Signing failed - MSIX will require manual signing" }

# 7. Create portable zip
Write-Host "Creating portable zip..." -ForegroundColor Yellow
$zipPath = "$OutputDir\PromptClipboard-$Version-portable-x64.zip"
Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath

# 8. Summary
Write-Host "`n=== Build complete ===" -ForegroundColor Green
Write-Host "MSIX:     $msixPath" -ForegroundColor White
Write-Host "Portable: $zipPath" -ForegroundColor White
Get-ChildItem $OutputDir -File | ForEach-Object {
    Write-Host ("  {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB))
}
