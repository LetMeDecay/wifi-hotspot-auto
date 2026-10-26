# Signing

`dist/WifiHotspotAuto.exe` is signed with a **self-signed development certificate**:

```
Subject:    CN=WifiHotspotAuto Dev, O=WifiHotspotAuto, C=CN
Thumbprint: 9A12D43BB6B85512114AE0272A71081EACE35599
Valid:      2026-10-25 .. 2028-10-26
```

The public certificate is `certs/WifiHotspotAuto_Dev.cer`. The private key (`*.pfx`) is **not** committed.

## Trust the certificate on this PC

A self-signed signature is only considered valid after its root is trusted. On the machine where you want to run the app:

1. Double-click `certs/WifiHotspotAuto_Dev.cer`.
2. Click **Install Certificate**.
3. Choose **Current User** → **Next**.
4. Choose **Place all certificates in the following store** → **Browse** → **Trusted Root Certification Authorities** → **OK** → **Next** → **Finish**.
5. (Optional) Repeat and install it into **Trusted Publishers** as well.
6. Verify:

```powershell
Get-AuthenticodeSignature .\dist\WifiHotspotAuto.exe |
  Format-List Status, StatusMessage, SignerCertificate
```

`Status` should become `Valid`. If it still says `UnknownError`, the root certificate was not installed for the current user.

## Limitations

A self-signed certificate:

- proves the file was signed by the holder of this private key and has not been modified;
- does **not** make Windows SmartScreen or Defender trust the app on other machines;
- is not a replacement for a real OV/EV code-signing certificate.

If Defender blocks the app because of its behavior (elevation + scheduled task + network control), signing alone will not necessarily stop the block. Options:

- **SmartScreen** ("Windows protected your PC"): click **More info** → **Run anyway**.
- **Defender quarantine**: open **Windows Security → Protection history** and restore/allow the file.
- Add a folder exclusion only if you trust the build.
- Submit a false positive: <https://www.microsoft.com/en-us/wdsi/filesubmission>
- For public distribution without warnings, buy a trusted code-signing certificate and build reputation.

## Rebuild and sign

```powershell
# 1. Build
.\build.cmd

# 2. Sign with your own PFX (keep it out of git)
$env:SIGN_PFX = "C:\path\to\codesign.pfx"
$env:SIGN_PFX_PASSWORD = "pfx-password"
powershell -ExecutionPolicy Bypass -File .\sign.ps1 -File .\dist\WifiHotspotAuto.exe
```

`sign.ps1` uses `EphemeralKeySet` so the private key is not imported into the certificate store. It signs with SHA-256 and no timestamp; add a timestamp server in the script if you need the signature to outlive the certificate.
