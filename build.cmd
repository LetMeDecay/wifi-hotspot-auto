@echo off
setlocal
if not exist dist mkdir dist
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
  /out:dist\WifiHotspotAuto.exe /win32manifest:src\app.manifest /win32icon:src\app.ico ^
  /r:"%FW%\System.dll" ^
  /r:"%FW%\System.Core.dll" ^
  /r:"%FW%\System.Drawing.dll" ^
  /r:"%FW%\System.Windows.Forms.dll" ^
  /r:"%FW%\System.Runtime.dll" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Foundation.winmd" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Networking.winmd" ^
  src\WifiHotspotAuto.cs
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo Built dist\WifiHotspotAuto.exe
