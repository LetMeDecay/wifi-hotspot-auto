<img src="docs/icon.png" width="72" alt="Wi-Fi Hotspot Auto icon">

# Wi-Fi Hotspot Auto / Wi-Fi 热点自启

Windows 托盘小工具：登录后自动连接指定 Wi-Fi，并开启移动热点。  
A Windows tray utility that connects to a Wi-Fi network and starts Mobile Hotspot at logon.

## 功能 / Features

- 自动连接指定 Wi-Fi / Auto-connect to a configured Wi-Fi SSID
- 自动开启移动热点 / Auto-start Windows Mobile Hotspot
- 禁用 Windows“无连接 5 分钟自动关闭” / Disables the 5-minute no-connections timeout
- 可设置热点名称、密码、频段 / Configurable hotspot SSID, password, band
- 托盘菜单：设置、立即执行、日志、退出 / Tray menu
- 可选登录计划任务，最高权限运行 / Optional logon scheduled task with highest privileges
- 单文件 exe，无第三方 DLL / Single exe, no third-party DLLs

## 环境 / Requirements

- Windows 10/11 x64
- .NET Framework 4.8（Windows 11 自带 / included）
- 管理员账户 / Administrator account
- 支持移动热点的无线网卡 / Wi-Fi adapter with Mobile Hotspot support

## 使用 / Usage

1. 运行 `WifiHotspotAuto.exe`，UAC 点“是”。  
   Run `WifiHotspotAuto.exe` and approve UAC.
2. 填写 Wi-Fi 名称、密码、热点名称/密码、频段。  
   Fill in the Wi-Fi SSID/password and hotspot SSID/password/band.
3. 点“立即连接并开热点”测试。  
   Click **Connect and start hotspot** to test.
4. 勾选“开机自动启动”并保存。  
   Check **Start at logon** and save.

热点密码至少 8 位。 / Hotspot password must be at least 8 characters.

## 5GHz 热点 / 5 GHz Hotspot

AX210 需要先连接 5.8GHz（149–161 信道）Wi-Fi，才能开 5GHz 热点；否则会退回 2.4GHz。  
On Intel AX210, connect to a 5.8 GHz network (channels 149–161) before starting a 5 GHz hotspot.

## 开机自启 / Start at Logon

创建计划任务 `WifiHotspotAuto`：登录时延迟 10 秒，最高权限运行。  
Creates a scheduled task `WifiHotspotAuto`: at logon, 10 s delay, highest privileges.

## 配置与日志 / Config & Logs

- 配置 / Config: `%APPDATA%\WifiHotspotAuto\config.ini`
- 日志 / Log: `%APPDATA%\WifiHotspotAuto\log.txt`

密码以明文保存在 `config.ini`。 / Passwords are stored in plain text.

## 构建 / Build

```bat
build.cmd
```

使用 Windows 自带的 .NET Framework 编译器。 / Uses the built-in .NET Framework compiler.

## 签名 / Signing

`dist/WifiHotspotAuto.exe` 使用自签名开发证书签名。信任方法及 Defender/SmartScreen 说明见 [docs/SIGNING.md](docs/SIGNING.md)。  
The binary is signed with a self-signed development certificate; see [docs/SIGNING.md](docs/SIGNING.md).

## License

MIT
