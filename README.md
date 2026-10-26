# Wi-Fi Hotspot Auto

A small Windows tray utility that, after logon:

1. connects to a configured Wi-Fi network,
2. starts the Windows Mobile Hotspot.

It has a simple settings window, a tray icon, and an optional logon scheduled task that runs with highest privileges.

## Features

- Auto-connect to a configured Wi-Fi SSID
- Auto-start Windows Mobile Hotspot
- Configure hotspot SSID, password, and band (5 GHz / 2.4 GHz / auto)
- If the hotspot is already on but its SSID/band differs from the settings, restart it and apply the settings
- Tray icon: open settings, run now, view log, exit
- Optional logon scheduled task (`WifiHotspotAuto`) with highest privileges, no UAC prompt at logon
- Single-file WinForms executable, no third-party DLLs, ~26 KB

## Requirements

- Windows 10 / Windows 11, 64-bit
- .NET Framework 4.8 (built into Windows 11)
- Administrator account (the app manifest requires elevation)
- A Wi-Fi adapter that supports Windows Mobile Hotspot

## Quick Start

1. Build the executable (see below) or download a release asset.
2. Double-click `WifiHotspotAuto.exe` and approve the UAC prompt.
3. Fill in:
   - **Wi-Fi name**: the SSID to connect to (e.g. `MyWiFi`)
   - **Wi-Fi password**: needed the first time to create the profile; can be left empty if the profile already exists
   - **Hotspot name / password**: the hotspot password must be at least 8 characters
   - **Band**: `5 GHz` recommended
4. Click **Connect and start hotspot** to test.
5. Check **Start at logon** and click **Save** to create the scheduled task.

## Build

The project uses the C# compiler that ships with .NET Framework, so no Visual Studio or .NET SDK is required.

```bat
build.cmd
```

Or manually:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /nologo /target:winexe /platform:x64 /optimize+ ^
  /out:dist\WifiHotspotAuto.exe /win32manifest:src\app.manifest ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Foundation.winmd" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Networking.winmd" ^
  src\WifiHotspotAuto.cs
```

## Signing

`dist/WifiHotspotAuto.exe` is signed with a **self-signed development certificate**. See [docs/SIGNING.md](docs/SIGNING.md) for how to install and trust the certificate locally, and why a self-signed signature is not a substitute for a real code-signing certificate.

The private key (`*.pfx`) is **not** committed.

## 5 GHz hotspot prerequisite

On Intel AX210, a 5 GHz hotspot can only be started while the adapter is connected to a **5.8 GHz Wi-Fi network (channels 149–161)**. If the adapter is connected to 2.4 GHz or only Ethernet, Windows falls back to a 2.4 GHz hotspot.

Set the router's 5 GHz channel to 149/153/157/161/165 for best results.

## Start at logon

The app creates a scheduled task:

```
Task name: WifiHotspotAuto
Trigger:   at logon (10 second delay)
Run level: highest
Action:    "<path>\WifiHotspotAuto.exe" --autostart
```

The task runs elevated without a UAC prompt. If automatic task creation fails, create it manually from an elevated Command Prompt:

```bat
schtasks /create /tn "WifiHotspotAuto" ^
  /tr "\"C:\path\to\WifiHotspotAuto.exe\" --autostart" ^
  /sc onlogon /rl highest /it /delay 0000:10 /f
```

## Configuration and logs

- Config: `%APPDATA%\WifiHotspotAuto\config.ini`
- Log: `%APPDATA%\WifiHotspotAuto\log.txt`

Passwords are stored in plain text in `config.ini`; this tool is intended for a personal computer.

## Defender / SmartScreen

Because the executable is unsigned (or self-signed), Windows Defender SmartScreen may warn about an unknown publisher, and Defender's behavior heuristics may block a scheduled-task + network utility.

Options:

- If SmartScreen shows "Windows protected your PC": click **More info** → **Run anyway**.
- If Defender quarantined the file: open **Windows Security → Protection history**, restore/allow it, or add an exclusion for the folder.
- For distribution without warnings, use a trusted OV/EV code-signing certificate and build reputation.
- Report false positives: <https://www.microsoft.com/en-us/wdsi/filesubmission>

## Why C# instead of Rust

The original request suggested Rust. This implementation uses C# / WinForms because it can be built with the compiler that ships with Windows, without installing Rust/MSVC/MinGW. A Rust + native Win32 port would use less memory (~5–10 MB vs ~25–35 MB) and is a reasonable follow-up.

## License

MIT
