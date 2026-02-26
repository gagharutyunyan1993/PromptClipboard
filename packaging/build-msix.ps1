param(
    [string]$Version = "1.0.0.0",
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\artifacts"
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

# 4. Find MakeAppx.exe
$sdkDir = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64" -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkDir) {
    $sdkDir = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64" -Directory |
        Sort-Object Name -Descending | Select-Object -First 1
}

$makeAppx = Join-Path $sdkDir.FullName "makeappx.exe"
$signTool = Join-Path $sdkDir.FullName "signtool.exe"

if (-not (Test-Path $makeAppx)) { throw "MakeAppx.exe not found. Install Windows 10 SDK." }

# 5. Create MSIX
$msixPath = "$OutputDir\PromptClipboard-$Version-x64.msix"
Write-Host "Creating MSIX package..." -ForegroundColor Yellow
& $makeAppx pack /d $StagingDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed" }

# 6. Create self-signed certificate and sign
$certPath = "$OutputDir\PromptClipboard.pfx"
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=PromptClipboard" `
    -KeyUsage DigitalSignature `
    -FriendlyName "Prompt Clipboard Dev" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$password = ConvertTo-SecureString -String "PromptClipboard2026" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password | Out-Null

& $signTool sign /fd SHA256 /a /f $certPath /p "PromptClipboard2026" $msixPath
if ($LASTEXITCODE -ne 0) { Write-Warning "Signing failed - MSIX will require manual signing" }

# Cleanup cert from store
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue

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
