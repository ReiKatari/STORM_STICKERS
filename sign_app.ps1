# PowerShell Script to generate a self-signed code signing certificate and sign STORM STICKERS binaries

try {
    Import-Module Microsoft.PowerShell.Security -ErrorAction SilentlyContinue
} catch {}

$ErrorActionPreference = "Stop"

Write-Host "Checking for existing code-signing certificate..." -ForegroundColor Cyan
$certSubject = "CN=STORM STICKERS Code Signing"

# Use .NET X509Store API to avoid Cert:\ drive dependency issues in some execution environments
$myStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("My", "CurrentUser")
$myStore.Open("ReadOnly")
$cert = $myStore.Certificates | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1
$myStore.Close()

if ($null -eq $cert) {
    Write-Host "Creating a new self-signed code-signing certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject $certSubject `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(5)
    
    Write-Host "Registering the certificate in the Trusted Root Certification Authorities for Current User..." -ForegroundColor Yellow
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
    $store.Open("ReadWrite")
    $store.Add($cert)
    $store.Close()
    Write-Host "Certificate successfully generated and registered as trusted." -ForegroundColor Green
} else {
    Write-Host "Found existing trusted certificate." -ForegroundColor Green
}

# Locate build directory
$binDir = Join-Path "E:\STORM STICKERS" "bin\Debug\net10.0-windows10.0.26100.0\win-x64"
if (-not (Test-Path $binDir)) {
    Write-Error "Build output directory not found. Please compile the project first using: dotnet build"
}

$exePath = Join-Path $binDir "STORM STICKERS.exe"
$dllPath = Join-Path $binDir "STORM STICKERS.dll"

Write-Host "Signing binaries..." -ForegroundColor Cyan
if (Test-Path $exePath) {
    Write-Host "Signing STORM STICKERS.exe..."
    Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert | Out-Null
    $exeSig = Get-AuthenticodeSignature -FilePath $exePath
    $exeColor = if ($exeSig.Status -eq "Valid") { "Green" } else { "Red" }
    Write-Host "Status: $($exeSig.Status)" -ForegroundColor $exeColor
} else {
    Write-Warning "STORM STICKERS.exe not found."
}

if (Test-Path $dllPath) {
    Write-Host "Signing STORM STICKERS.dll..."
    Set-AuthenticodeSignature -FilePath $dllPath -Certificate $cert | Out-Null
    $dllSig = Get-AuthenticodeSignature -FilePath $dllPath
    $dllColor = if ($dllSig.Status -eq "Valid") { "Green" } else { "Red" }
    Write-Host "Status: $($dllSig.Status)" -ForegroundColor $dllColor
} else {
    Write-Warning "STORM STICKERS.dll not found."
}

Write-Host "Signing completed!" -ForegroundColor Green
