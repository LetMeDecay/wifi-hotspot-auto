param(
    [string]$File = "dist\WifiHotspotAuto.exe",
    [string]$Pfx = $env:SIGN_PFX,
    [string]$Password = $env:SIGN_PFX_PASSWORD
)

$ErrorActionPreference = "Stop"

if (-not $Pfx) {
    throw "Set SIGN_PFX to the path of a code-signing .pfx file, and optionally SIGN_PFX_PASSWORD."
}
if (-not (Test-Path $Pfx)) {
    throw "PFX not found: $Pfx"
}
if (-not (Test-Path $File)) {
    throw "File not found: $File"
}

# EphemeralKeySet avoids importing the private key into the user's certificate store.
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    (Resolve-Path $Pfx).Path, $Password, $flags)

Set-AuthenticodeSignature -FilePath $File -Certificate $cert -HashAlgorithm SHA256

Get-AuthenticodeSignature $File | Format-List Status, StatusMessage, SignerCertificate
