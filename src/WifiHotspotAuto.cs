using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace WifiHotspotAuto
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool autostart = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase))
                    autostart = true;
                if (string.Equals(a, "--test", StringComparison.OrdinalIgnoreCase))
                {
                    RunTest();
                    return;
                }
            }

            bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
            if (!elevated)
            {
                MessageBox.Show("本程序需要管理员权限。请右键 WifiHotspotAuto.exe，选择“以管理员身份运行”。",
                    "Wi-Fi 热点自启", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, "WifiHotspotAuto_SingleInstance", out created))
            {
                if (!created)
                {
                    if (!autostart)
                    {
                        MessageBox.Show("程序已在运行，请从系统托盘打开设置。",
                            "Wi-Fi 热点自启", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(Application.StartupPath, "error.txt"),
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " UI: " + e.Exception + "\r\n");
                    }
                    catch { }
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(Application.StartupPath, "error.txt"),
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " APP: " + e.ExceptionObject + "\r\n");
                    }
                    catch { }
                };
                Application.Run(new TrayApp(autostart));
            }
        }

        private static void RunTest()
        {
            string path = Path.Combine(Application.StartupPath, "winrt_test_result.txt");
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                var sb = new StringBuilder();
                sb.AppendLine("profile=" + (profile == null ? "null" : profile.ProfileName));
                if (profile != null)
                {
                    var mgr = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                    sb.AppendLine("state=" + mgr.TetheringOperationalState);
                    var ap = mgr.GetCurrentAccessPointConfiguration();
                    sb.AppendLine("ssid=" + ap.Ssid);
                    sb.AppendLine("band=" + ap.Band);
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(path, "ERR: " + ex); } catch { }
            }
        }
    }

    internal static class AppIcon
    {
        public static Icon Load()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }
    }

    internal sealed class Config
    {
        public string WifiSsid = "";
        public string WifiPassword = "";
        public string HotspotSsid = "DESKTOP_HOTSPOT";
        public string HotspotPassword = "12345678";
        public int Band = 1; // 0 = 2.4G, 1 = 5G, 2 = auto
        public bool AutoStart = false;
        public bool StartMinimized = true;

        public static string AppDir
        {
            get
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WifiHotspotAuto");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string ConfigPath { get { return Path.Combine(AppDir, "config.ini"); } }
        public static string LogPath { get { return Path.Combine(AppDir, "log.txt"); } }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    foreach (string raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                    {
                        string line = raw;
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        string k = line.Substring(0, i).Trim();
                        string v = line.Substring(i + 1);
                        switch (k)
                        {
                            case "WifiSsid": c.WifiSsid = v; break;
                            case "WifiPassword": c.WifiPassword = v; break;
                            case "HotspotSsid": c.HotspotSsid = v; break;
                            case "HotspotPassword": c.HotspotPassword = v; break;
                            case "Band": int.TryParse(v, out c.Band); break;
                            case "AutoStart": c.AutoStart = v == "1"; break;
                            case "StartMinimized": c.StartMinimized = v == "1"; break;
                        }
                    }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("WifiSsid=" + OneLine(WifiSsid));
            sb.AppendLine("WifiPassword=" + OneLine(WifiPassword));
            sb.AppendLine("HotspotSsid=" + OneLine(HotspotSsid));
            sb.AppendLine("HotspotPassword=" + OneLine(HotspotPassword));
            sb.AppendLine("Band=" + Band);
            sb.AppendLine("AutoStart=" + (AutoStart ? "1" : "0"));
            sb.AppendLine("StartMinimized=" + (StartMinimized ? "1" : "0"));
            File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static string OneLine(string s)
        {
            if (s == null) return "";
            return s.Replace("\r", "").Replace("\n", "");
        }
    }

    internal static class Wifi
    {
        public static bool Connect(Config cfg, Action<string> log)
        {
            string ssid = (cfg.WifiSsid ?? "").Trim();
            if (ssid.Length == 0)
            {
                log("Wi-Fi 名称不能为空");
                return false;
            }

            if (!ProfileExists(ssid))
            {
                if (string.IsNullOrEmpty(cfg.WifiPassword))
                {
                    log("系统里没有这个 Wi-Fi 的配置，请填写密码");
                    return false;
                }

                string xmlPath = Path.Combine(
                    Path.GetTempPath(),
                    "wifi_profile_" + Guid.NewGuid().ToString("N") + ".xml");
                File.WriteAllText(xmlPath, BuildProfileXml(ssid, cfg.WifiPassword), new UTF8Encoding(false));

                int code;
                string outp = Run("wlan add profile filename=\"" + xmlPath + "\" user=current", out code);
                log("添加 Wi-Fi 配置(user=current): " + code);
                if (code != 0)
                {
                    outp = Run("wlan add profile filename=\"" + xmlPath + "\"", out code);
                    log("添加 Wi-Fi 配置(all users): " + code);
                }
                try { File.Delete(xmlPath); } catch { }

                if (code != 0)
                {
                    log(outp.Trim());
                    return false;
                }
            }

            int rc;
            string res = Run("wlan connect name=\"" + ssid + "\" ssid=\"" + ssid + "\"", out rc);
            log("发起 Wi-Fi 连接: " + rc);
            if (rc != 0)
            {
                log(res.Trim());
            }

            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                int c2;
                string o = Run("wlan show interfaces", out c2);
                if (IsConnected(o, ssid))
                {
                    log("Wi-Fi 已连接: " + ssid);
                    return true;
                }
            }

            log("等待 Wi-Fi 连接超时");
            return false;
        }

        private static bool ProfileExists(string ssid)
        {
            int code;
            Run("wlan show profile name=\"" + ssid + "\"", out code);
            return code == 0;
        }

        private static bool IsConnected(string output, string ssid)
        {
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase) &&
                    line.IndexOf(ssid, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string BuildProfileXml(string ssid, string password)
        {
            string s = XmlEscape(ssid);
            string p = XmlEscape(password ?? "");
            string auth;
            string key = "";
            if (string.IsNullOrEmpty(password))
            {
                auth = "<authentication>open</authentication><encryption>none</encryption>";
            }
            else
            {
                auth = "<authentication>WPA2PSK</authentication><encryption>AES</encryption>";
                key = "<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>" +
                      p + "</keyMaterial></sharedKey>";
            }

            return "<?xml version=\"1.0\"?>\r\n" +
                "<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">" +
                "<name>" + s + "</name>" +
                "<SSIDConfig><SSID><name>" + s + "</name></SSID></SSIDConfig>" +
                "<connectionType>ESS</connectionType>" +
                "<connectionMode>auto</connectionMode>" +
                "<MSM><security><authEncryption>" + auth + "<useOneX>false</useOneX></authEncryption>" +
                key + "</security></MSM></WLANProfile>";
        }

        private static string XmlEscape(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static string Run(string args, out int exitCode)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe");
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            try { psi.StandardOutputEncoding = Encoding.GetEncoding(936); } catch { }
            try { psi.StandardErrorEncoding = Encoding.GetEncoding(936); } catch { }

            try
            {
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    return o + e;
                }
            }
            catch (Exception ex)
            {
                exitCode = -1;
                return ex.Message;
            }
        }
    }

    internal static class Hotspot
    {
        public static bool Start(Config cfg, Action<string> log)
        {
            try
            {
                if (string.IsNullOrEmpty(cfg.HotspotSsid) || cfg.HotspotSsid.Length > 32)
                {
                    log("热点名称不能为空，且不能超过 32 个字符");
                    return false;
                }
                if (cfg.HotspotPassword == null || cfg.HotspotPassword.Length < 8)
                {
                    log("热点密码至少需要 8 位");
                    return false;
                }

                var profile = GetUpstreamProfile();
                if (profile == null)
                {
                    log("没有可用的 Internet 连接");
                    return false;
                }

                var mgr = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                var ap = mgr.GetCurrentAccessPointConfiguration();

                TetheringWiFiBand desiredBand;
                if (cfg.Band == 0) desiredBand = TetheringWiFiBand.TwoPointFourGigahertz;
                else if (cfg.Band == 1) desiredBand = TetheringWiFiBand.FiveGigahertz;
                else desiredBand = TetheringWiFiBand.Auto;

                bool sameSsid = string.Equals(ap.Ssid ?? "", cfg.HotspotSsid, StringComparison.Ordinal);
                bool sameBand = ap.Band == desiredBand;

                if (mgr.TetheringOperationalState == TetheringOperationalState.On)
                {
                    if (sameSsid && sameBand)
                    {
                        DisableNoConnectionsTimeout(log);
                        log("热点已开启，配置一致");
                        return true;
                    }

                    log("热点已开启但配置不一致，正在重启热点...");
                    var stopOp = mgr.StopTetheringAsync();
                    WaitOperation(stopOp);
                    var stopResult = stopOp.GetResults();
                    if (stopResult.Status.ToString() != "Success")
                    {
                        log("关闭旧热点失败: " + stopResult.Status);
                        return false;
                    }
                    ap = mgr.GetCurrentAccessPointConfiguration();
                }

                ap.Ssid = cfg.HotspotSsid;
                ap.Passphrase = cfg.HotspotPassword;
                ap.Band = desiredBand;

                var cfgOp = mgr.ConfigureAccessPointAsync(ap);
                WaitAction(cfgOp);

                var startOp = mgr.StartTetheringAsync();
                WaitOperation(startOp);
                var startResult = startOp.GetResults();
                if (startResult.Status.ToString() != "Success")
                {
                    log("开启热点失败: " + startResult.Status);
                    return false;
                }

                DisableNoConnectionsTimeout(log);
                log("热点已开启: " + cfg.HotspotSsid);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                log("热点异常: 拒绝访问。请右键“以管理员身份运行”本程序，或先在 Windows 设置中手动开启一次热点。");
                return false;
            }
            catch (Exception ex)
            {
                log("热点异常: " + ex.Message);
                return false;
            }
        }

        private static ConnectionProfile GetUpstreamProfile()
        {
            ConnectionProfile fallback = NetworkInformation.GetInternetConnectionProfile();
            try
            {
                foreach (var p in NetworkInformation.GetConnectionProfiles())
                {
                    if (p.NetworkAdapter != null && p.NetworkAdapter.IanaInterfaceType == 6 &&
                        p.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess)
                    {
                        return p;
                    }
                }
            }
            catch { }
            return fallback;
        }

        private static void DisableNoConnectionsTimeout(Action<string> log)
        {
            try
            {
                NetworkOperatorTetheringManager.DisableNoConnectionsTimeout();
                log("已禁用无连接自动关闭");
            }
            catch (Exception ex)
            {
                log("禁用无连接自动关闭失败: " + ex.Message);
            }
        }

        private static void WaitAction(IAsyncAction op)
        {
            var sw = Stopwatch.StartNew();
            while (op.Status == AsyncStatus.Started && sw.ElapsedMilliseconds < 30000)
                Thread.Sleep(50);
            if (op.Status == AsyncStatus.Started)
                throw new TimeoutException("配置热点超时");
            op.GetResults();
        }

        private static void WaitOperation<T>(IAsyncOperation<T> op)
        {
            var sw = Stopwatch.StartNew();
            while (op.Status == AsyncStatus.Started && sw.ElapsedMilliseconds < 30000)
                Thread.Sleep(50);
            if (op.Status == AsyncStatus.Started)
                throw new TimeoutException("开启热点超时");
        }
    }

    internal sealed class SettingsForm : Form
    {
        public TextBox TxtWifiSsid;
        public TextBox TxtWifiPwd;
        public TextBox TxtHotspotSsid;
        public TextBox TxtHotspotPwd;
        public TextBox TxtLog;
        public ComboBox CmbBand;
        public CheckBox ChkAutoStart;
        public CheckBox ChkMinimized;
        public Button BtnSave;
        public Button BtnRun;
        public Button BtnHide;

        public event EventHandler SaveClicked;
        public event EventHandler RunClicked;

        public SettingsForm(Config cfg)
        {
            Text = "Wi-Fi 热点自启";
            ClientSize = new Size(640, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = AppIcon.Load();
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = false;

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            Controls.Add(root);

            var grid = new TableLayoutPanel();
            grid.AutoSize = true;
            grid.Dock = DockStyle.Top;
            grid.ColumnCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Padding = new Padding(10, 10, 10, 0);
            root.Controls.Add(grid, 0, 0);

            TxtWifiSsid = AddText(grid, 0, "Wi-Fi 名称", cfg.WifiSsid);
            TxtWifiPwd = AddText(grid, 1, "Wi-Fi 密码", cfg.WifiPassword);
            TxtWifiPwd.UseSystemPasswordChar = true;
            TxtHotspotSsid = AddText(grid, 2, "热点名称", cfg.HotspotSsid);
            TxtHotspotPwd = AddText(grid, 3, "热点密码", cfg.HotspotPassword);
            TxtHotspotPwd.UseSystemPasswordChar = true;

            CmbBand = new ComboBox();
            CmbBand.DropDownStyle = ComboBoxStyle.DropDownList;
            CmbBand.Items.AddRange(new object[] { "5 GHz（推荐）", "2.4 GHz", "自动" });
            CmbBand.SelectedIndex = cfg.Band == 0 ? 1 : (cfg.Band == 2 ? 2 : 0);
            CmbBand.Dock = DockStyle.Fill;
            AddRow(grid, 4, "频段", CmbBand);

            ChkAutoStart = new CheckBox();
            ChkAutoStart.Text = "开机自动启动（登录后）";
            ChkAutoStart.Checked = cfg.AutoStart;
            AddRow(grid, 5, "", ChkAutoStart);

            ChkMinimized = new CheckBox();
            ChkMinimized.Text = "启动后最小化到托盘";
            ChkMinimized.Checked = cfg.StartMinimized;
            AddRow(grid, 6, "", ChkMinimized);

            TxtLog = new TextBox();
            TxtLog.Multiline = true;
            TxtLog.ReadOnly = true;
            TxtLog.ScrollBars = ScrollBars.Vertical;
            TxtLog.Dock = DockStyle.Fill;
            TxtLog.Font = new Font("Consolas", 8.5F);
            TxtLog.Margin = new Padding(10, 4, 10, 4);
            root.Controls.Add(TxtLog, 0, 1);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(8, 4, 8, 4);
            root.Controls.Add(buttons, 0, 2);

            BtnSave = new Button();
            BtnSave.Text = "保存";
            BtnSave.Width = 80;
            BtnSave.Click += delegate { if (SaveClicked != null) SaveClicked(this, EventArgs.Empty); };
            buttons.Controls.Add(BtnSave);

            BtnRun = new Button();
            BtnRun.Text = "立即连接并开热点";
            BtnRun.Width = 150;
            BtnRun.Click += delegate { if (RunClicked != null) RunClicked(this, EventArgs.Empty); };
            buttons.Controls.Add(BtnRun);

            BtnHide = new Button();
            BtnHide.Text = "隐藏到托盘";
            BtnHide.Width = 100;
            BtnHide.Click += delegate { Hide(); };
            buttons.Controls.Add(BtnHide);

            AcceptButton = BtnSave;
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(640, 520);
        }

        private static TextBox AddText(TableLayoutPanel t, int row, string label, string value)
        {
            var tb = new TextBox();
            tb.Text = value ?? "";
            tb.Dock = DockStyle.Fill;
            tb.Margin = new Padding(3, 4, 3, 4);
            AddRow(t, row, label, tb);
            return tb;
        }

        private static void AddRow(TableLayoutPanel t, int row, string label, Control c)
        {
            t.RowCount = row + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label();
            lbl.Text = label;
            lbl.AutoSize = true;
            lbl.Anchor = AnchorStyles.Left;
            lbl.Margin = new Padding(3, 7, 3, 4);
            t.Controls.Add(lbl, 0, row);
            t.Controls.Add(c, 1, row);
        }

        public void ReadInto(Config c)
        {
            c.WifiSsid = TxtWifiSsid.Text.Trim();
            c.WifiPassword = TxtWifiPwd.Text;
            c.HotspotSsid = TxtHotspotSsid.Text.Trim();
            c.HotspotPassword = TxtHotspotPwd.Text;
            c.Band = CmbBand.SelectedIndex == 1 ? 0 : (CmbBand.SelectedIndex == 2 ? 2 : 1);
            c.AutoStart = ChkAutoStart.Checked;
            c.StartMinimized = ChkMinimized.Checked;
        }

        public void AppendLog(string line)
        {
            if (TxtLog.TextLength > 120000) TxtLog.Clear();
            TxtLog.AppendText(line + "\r\n");
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private readonly Config _config;
        private readonly NotifyIcon _tray;
        private readonly bool _autostart;
        private SettingsForm _form;
        private bool _running;

        public TrayApp(bool autostart)
        {
            _config = Config.Load();
            _autostart = autostart;

            try
            {
                var fi = new FileInfo(Config.LogPath);
                if (fi.Exists && fi.Length > 512 * 1024)
                    File.WriteAllText(Config.LogPath, "");
            }
            catch { }

            _tray = new NotifyIcon();
            _tray.Icon = AppIcon.Load();
            _tray.Text = "Wi-Fi 热点自启";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开设置", null, delegate { ShowForm(); });
            menu.Items.Add("立即连接并开热点", null, delegate { RunAutomation(); });
            menu.Items.Add("查看日志", null, delegate
            {
                try { Process.Start("notepad.exe", Config.LogPath); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApp(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowForm(); };

            Log("程序启动" + (_autostart ? "（开机自启）" : ""));

            if (_autostart)
            {
                RunAutomation();
            }
            else
            {
                ShowForm();
            }
        }

        private void ShowForm()
        {
            if (_form == null || _form.IsDisposed)
            {
                _form = new SettingsForm(_config);
                _form.SaveClicked += delegate
                {
                    _form.ReadInto(_config);
                    _config.Save();
                    UpdateAutoStart();
                    Log("设置已保存");
                };
                _form.RunClicked += delegate
                {
                    _form.ReadInto(_config);
                    _config.Save();
                    UpdateAutoStart();
                    Log("设置已保存");
                    RunAutomation();
                };
                _form.FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (e.CloseReason == CloseReason.UserClosing)
                    {
                        e.Cancel = true;
                        _form.Hide();
                        _tray.ShowBalloonTip(2000, "Wi-Fi 热点",
                            "已最小化到托盘", ToolTipIcon.Info);
                    }
                };
            }

            _form.Show();
            if (_form.WindowState == FormWindowState.Minimized)
                _form.WindowState = FormWindowState.Normal;
            _form.Activate();
        }

        private const string TaskName = "WifiHotspotAuto";

        private void UpdateAutoStart()
        {
            try
            {
                if (_config.AutoStart)
                {
                    string tr = "\"" + Application.ExecutablePath + "\" --autostart";
                    RunSchtasks("/create /tn \"" + TaskName + "\" /tr \"" + tr +
                                "\" /sc onlogon /rl highest /it /delay 0000:10 /f");
                    Log("已创建开机任务: " + TaskName);
                }
                else
                {
                    try
                    {
                        RunSchtasks("/delete /tn \"" + TaskName + "\" /f");
                        Log("已删除开机任务");
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.IndexOf("找不到", StringComparison.OrdinalIgnoreCase) < 0 &&
                            ex.Message.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) < 0)
                            throw;
                    }
                }

                // 清理旧版本写入的 Run 项
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) key.DeleteValue("WifiHotspotAuto", false);
                }
            }
            catch (Exception ex)
            {
                Log("设置开机自启失败: " + ex.Message);
                MessageBox.Show("设置开机自启失败: " + ex.Message, "Wi-Fi 热点自启",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RunSchtasks(string args)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var p = Process.Start(psi))
            {
                bool exited = p.WaitForExit(15000);
                string o = p.StandardOutput.ReadToEnd();
                string e = p.StandardError.ReadToEnd();
                if (!exited)
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("schtasks 超时（可能需要交互输入密码）");
                }
                if (p.ExitCode != 0)
                    throw new Exception((o + e).Trim());
            }
        }

        private void RunAutomation()
        {
            if (_running)
            {
                Log("正在执行，请稍候...");
                return;
            }
            _running = true;

            var t = new Thread(delegate()
            {
                try
                {
                    Log("===== 开始 =====");
                    if (_autostart)
                    {
                        Log("等待系统网络就绪...");
                        Thread.Sleep(5000);
                    }

                    bool wifiOk = false;
                    for (int attempt = 1; attempt <= 3 && !wifiOk; attempt++)
                    {
                        if (attempt > 1)
                        {
                            Log("第 " + attempt + " 次重试 Wi-Fi...");
                            Thread.Sleep(10000);
                        }
                        Log("连接 Wi-Fi: " + _config.WifiSsid);
                        wifiOk = Wifi.Connect(_config, Log);
                    }
                    if (!wifiOk)
                    {
                        Log("Wi-Fi 连接失败，停止");
                        _tray.ShowBalloonTip(5000, "Wi-Fi 热点",
                            "Wi-Fi 连接失败，请查看日志", ToolTipIcon.Error);
                        return;
                    }

                    Thread.Sleep(2000);
                    bool hotspotOk = false;
                    for (int attempt = 1; attempt <= 3 && !hotspotOk; attempt++)
                    {
                        if (attempt > 1)
                        {
                            Log("第 " + attempt + " 次重试热点...");
                            Thread.Sleep(5000);
                        }
                        Log("开启热点: " + _config.HotspotSsid);
                        hotspotOk = Hotspot.Start(_config, Log);
                    }
                    if (!hotspotOk)
                    {
                        Log("热点开启失败，停止");
                        _tray.ShowBalloonTip(5000, "Wi-Fi 热点",
                            "热点开启失败，请查看日志", ToolTipIcon.Error);
                        return;
                    }

                    Log("===== 完成 =====");
                    if (_tray != null && _tray.Visible)
                    {
                        _tray.ShowBalloonTip(3000, "Wi-Fi 热点",
                            "已连接 Wi-Fi 并开启热点", ToolTipIcon.Info);
                    }
                }
                catch (Exception ex)
                {
                    Log("未处理异常: " + ex);
                }
                finally
                {
                    _running = false;
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        public void Log(string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg;
            try
            {
                File.AppendAllText(Config.LogPath, line + "\r\n", new UTF8Encoding(false));
            }
            catch
            {
                try
                {
                    File.AppendAllText(Path.Combine(Application.StartupPath, "log.txt"),
                        line + "\r\n", new UTF8Encoding(false));
                }
                catch { }
            }

            if (_form != null && !_form.IsDisposed && _form.IsHandleCreated)
            {
                try
                {
                    _form.BeginInvoke(new Action(delegate { _form.AppendLog(line); }));
                }
                catch { }
            }
        }

        private void ExitApp()
        {
            try { _tray.Visible = false; _tray.Dispose(); } catch { }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
