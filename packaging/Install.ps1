<#
.SYNOPSIS
    Installs Prompt Clipboard MSIX package.
.DESCRIPTION
    Imports the signing certificate to CurrentUser\TrustedPeople (no admin required)
    and installs the MSIX package. Use -AllUsers for machine-wide install (requires admin).
.EXAMPLE
    .\Install.ps1
    .\Install.ps1 -AllUsers
#>
param(
    [switch]$AllUsers
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# --- Find files ---
$msixFile = Get-ChildItem $scriptDir -Filter "PromptClipboard-*.msix" | Select-Object -First 1
if (-not $msixFile) {
    Write-Host "ERROR: MSIX file not found." -ForegroundColor Red
    Write-Host "Place this script in the same folder as PromptClipboard-*.msix" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

$cerFile = Get-ChildItem $scriptDir -Filter "PromptClipboard*.cer" | Select-Object -First 1
if (-not $cerFile) {
    # Extract from MSIX signature as fallback
    $sig = Get-AuthenticodeSignature $msixFile.FullName
    if ($sig.SignerCertificate) {
        $cerPath = Join-Path $scriptDir "PromptClipboard.cer"
        [IO.File]::WriteAllBytes($cerPath, $sig.SignerCertificate.Export(
            [Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        $cerFile = Get-Item $cerPath
    } else {
        Write-Host "ERROR: Cannot extract certificate from MSIX." -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
}

Write-Host "`n=== Prompt Clipboard Installer ===" -ForegroundColor Cyan
Write-Host "Package: $($msixFile.Name)"
Write-Host "Cert:    $($cerFile.Name)`n"

# --- AllUsers mode: self-elevate ---
if ($AllUsers) {
    if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "Requesting administrator privileges for machine-wide install..." -ForegroundColor Yellow
        Start-Process powershell.exe -Verb RunAs -ArgumentList (
            "-ExecutionPolicy Bypass -File `"$PSCommandPath`" -AllUsers"
        )
        exit
    }
    $storeLocation = [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
    Write-Host "Mode: Machine-wide (all users)" -ForegroundColor Yellow
} else {
    $storeLocation = [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    Write-Host "Mode: Current user (no admin required)" -ForegroundColor Green
}

# --- Import certificate ---
Write-Host "Importing certificate to TrustedPeople..." -ForegroundColor Yellow
$store = New-Object Security.Cryptography.X509Certificates.X509Store(
    [Security.Cryptography.X509Certificates.StoreName]::TrustedPeople,
    $storeLocation)
$store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$cert = New-Object Security.Cryptography.X509Certificates.X509Certificate2($cerFile.FullName)
$store.Add($cert)
$store.Close()
Write-Host "  Certificate trusted." -ForegroundColor Green

# --- Install MSIX ---
Write-Host "Installing $($msixFile.Name)..." -ForegroundColor Yellow
Add-AppPackage -Path $msixFile.FullName
Write-Host "`nDone! Launch from Start Menu or press Ctrl+Shift+Q." -ForegroundColor Green

Read-Host "`nPress Enter to exit"
