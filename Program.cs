using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using QRCoder;

namespace AdbManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

record Svc(string Name, string Type, string Addr);

class MainForm : Form
{
    const string AppVersion = "1.0.0";
    const string GithubUrl = "https://github.com/mehdirzfx";

    readonly string cfgFile = Path.Combine(AppContext.BaseDirectory, "adb_path.txt");

    readonly TextBox txtAdb = new();
    readonly TextBox txtIp = new();
    readonly TextBox txtPort = new();
    readonly TextBox txtPairAddr = new();
    readonly TextBox txtCode = new();
    readonly TextBox log = new();
    readonly ListBox lstDevices = new();
    readonly PictureBox picQr = new();
    readonly Label lblStatus = new();
    CancellationTokenSource? qrCts;

    public MainForm()
    {
        Text = $"ADB Manager v{AppVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        // log
        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Vertical;
        log.Dock = DockStyle.Fill;
        log.BackColor = Color.Black;
        log.ForeColor = Color.LightGreen;
        log.Font = new Font("Consolas", 9f);

        // inputs
        txtAdb.Dock = DockStyle.Fill;
        txtIp.Width = 150; txtIp.PlaceholderText = "192.168.1.x";
        txtPort.Width = 70; txtPort.PlaceholderText = "5555";
        txtPairAddr.Dock = DockStyle.Fill; txtPairAddr.PlaceholderText = "ip:port";
        txtCode.Dock = DockStyle.Fill; txtCode.PlaceholderText = "6-digit code";

        lstDevices.Dock = DockStyle.Fill;
        lstDevices.IntegralHeight = false;

        picQr.Size = new Size(120, 120);
        picQr.SizeMode = PictureBoxSizeMode.Zoom;
        picQr.BorderStyle = BorderStyle.FixedSingle;
        picQr.Visible = false;
        picQr.Anchor = AnchorStyles.Top;

        lblStatus.AutoSize = true;
        lblStatus.Margin = new Padding(3, 4, 3, 0);

        // ---- Group: ADB ----
        var adbTbl = Table(2, 2);
        adbTbl.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
        adbTbl.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
        adbTbl.Controls.Add(txtAdb, 0, 0);
        adbTbl.Controls.Add(Btn("Browse...", Browse, 90), 1, 0);
        adbTbl.Controls.Add(lblStatus, 0, 1);
        adbTbl.SetColumnSpan(lblStatus, 2);
        var gbAdb = Group("ADB", adbTbl);

        // ---- Group: Server + Devices ----
        var devTbl = Table(2, 2);
        devTbl.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
        devTbl.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
        devTbl.RowStyles[0] = new RowStyle(SizeType.AutoSize);
        devTbl.RowStyles[1] = new RowStyle(SizeType.Absolute, 76);
        lstDevices.MinimumSize = new Size(250, 70);

        var serverRow = Row(
            Btn("Refresh", ct => RefreshDevices(ct), 100),
            Btn("Start server", async ct => { Log(await Adb("start-server", ct: ct)); await RefreshDevices(ct); }, 100),
            Btn("Kill server", async ct => { Log(await Adb("kill-server", ct: ct)); await RefreshDevices(ct); }, 100),
            Btn("Restart server", RestartServer, 100));
        devTbl.Controls.Add(serverRow, 0, 0);
        devTbl.SetColumnSpan(serverRow, 2);

        var side = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MaximumSize = new Size(300, 0),
            Dock = DockStyle.Fill
        };
        side.Controls.Add(Btn("Disconnect selected", DisconnectSelected, 140));
        side.Controls.Add(Btn("Disconnect all", async ct => { Log(await Adb("disconnect", ct: ct)); await RefreshDevices(ct); }, 140));
        side.Controls.Add(Btn("Reboot selected", RebootSelected, 140));
        side.Controls.Add(Btn("USB -> Wi-Fi", UsbToWifi, 140));

        devTbl.Controls.Add(lstDevices, 0, 1);
        devTbl.Controls.Add(side, 1, 1);
        var gbDevices = Group("Server && Devices", devTbl);

        // ---- Group: Connect ----
        var connTbl = Table(1, 2);
        connTbl.Controls.Add(Row(Lbl("IP:"), txtIp, Lbl("Port:"), txtPort), 0, 0);
        connTbl.Controls.Add(Row(
            Btn("Connect", Connect, 100),
            Btn("Auto connect (mDNS)", AutoConnect, 150),
            Btn("Scan ports", ScanPorts, 100)), 0, 1);
        var gbConnect = Group("Connect (IP / Port)", connTbl);

        // ---- Group: Pair ----
        var pairLeft = Table(2, 3);
        pairLeft.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
        pairLeft.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
        pairLeft.Controls.Add(Lbl("Address:"), 0, 0);
        pairLeft.Controls.Add(txtPairAddr, 1, 0);
        pairLeft.Controls.Add(Lbl("Code:"), 0, 1);
        pairLeft.Controls.Add(txtCode, 1, 1);
        var pairBtns = Row(
            Btn("Pair", PairManual, 80),
            Btn("Pair with QR", QrPair, 110));
        pairLeft.Controls.Add(pairBtns, 0, 2);
        pairLeft.SetColumnSpan(pairBtns, 2);
        pairLeft.Dock = DockStyle.Fill;

        var pairTbl = Table(2, 1);
        pairTbl.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
        pairTbl.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, 135);
        pairLeft.MinimumSize = new Size(300, 0);
        pairTbl.Controls.Add(pairLeft, 0, 0);
        pairTbl.Controls.Add(picQr, 1, 0);
        pairTbl.MinimumSize = new Size(0, 128);
        var gbPair = Group("Wireless pairing", pairTbl);

        // ---- Group: Log (fixed height, pinned to the bottom) ----
        var gbLog = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Bottom,
            Height = 105,
            Padding = new Padding(8, 4, 8, 8),
            Margin = new Padding(0)
        };
        gbLog.Controls.Add(log);

        // ---- Footer ----
        var link = new LinkLabel
        {
            Text = "github.com/mehdirzfx",
            AutoSize = true,
            Margin = new Padding(3, 2, 3, 0)
        };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(GithubUrl) { UseShellExecute = true }); } catch { }
        };
        var lblVer = new Label
        {
            Text = $"mehdirzfx  |  v{AppVersion}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(3, 2, 3, 0)
        };
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(link, 0, 0);
        footer.Controls.Add(lblVer, 1, 0);

        // ---- Root ----
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8, 8, 8, 0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(gbAdb, 0, 0);
        root.Controls.Add(gbDevices, 0, 1);
        root.Controls.Add(gbConnect, 0, 2);
        root.Controls.Add(gbPair, 0, 3);

        var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 3, 8, 3) };
        footerPanel.Controls.Add(footer);

        // scroll host: if the screen is too short, a scrollbar appears instead of clipping
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(root);
        Controls.Add(scroll);       // fill (added first)
        Controls.Add(gbLog);        // bottom
        Controls.Add(footerPanel);  // very bottom (added last)

        Load += async (_, _) =>
        {
            // size the window to its content, limited to the visible screen area
            var pref = root.GetPreferredSize(new Size(0, 0));
            var wa = Screen.FromControl(this).WorkingArea;
            var frame = Size - ClientSize;
            int maxW = wa.Width - frame.Width, maxH = wa.Height - frame.Height;
            int w = Math.Max(pref.Width, 600);
            int h = pref.Height + gbLog.Height + footerPanel.Height + 8;
            if (h > maxH) { h = maxH; w += SystemInformation.VerticalScrollBarWidth; }
            ClientSize = new Size(Math.Min(w, maxW), h);
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);

            txtAdb.Text = FindAdb();
            if (txtAdb.Text == "")
                Log("adb.exe not found. Click Browse and select adb.exe (platform-tools folder).");
            else
                await RefreshDevices();
            UpdateStatus();
        };

        FormClosing += (_, _) =>
        {
            try { qrCts?.Cancel(); } catch { }
            try { File.WriteAllText(cfgFile, txtAdb.Text.Trim()); } catch { }
        };
    }

    // ---------- UI helpers ----------

    static GroupBox Group(string title, Control content)
    {
        var g = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 4, 8, 8),
            Margin = new Padding(0, 0, 0, 6)
        };
        content.Dock = DockStyle.Fill;
        g.Controls.Add(content);
        return g;
    }

    static TableLayoutPanel Table(int cols, int rows)
    {
        var t = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = cols,
            RowCount = rows
        };
        for (int i = 0; i < cols; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < rows; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return t;
    }

    /// <summary>
    /// Action button. While its action is running the button turns into "Stop";
    /// clicking it again cancels that action (the other buttons stay usable).
    /// </summary>
    Button Btn(string text, Func<CancellationToken, Task> action, int width = 0)
    {
        var b = new Button { Text = text, Margin = new Padding(3), Height = 26 };
        if (width > 0) b.Width = width; else b.AutoSize = true;

        CancellationTokenSource? cts = null;
        b.Click += async (_, _) =>
        {
            if (cts != null)
            {
                // already running -> this click means "stop"
                try { cts.Cancel(); } catch { }
                b.Enabled = false;
                return;
            }

            var mine = new CancellationTokenSource();
            cts = mine;
            b.Text = "Stop";
            try { await action(mine.Token); }
            catch (OperationCanceledException) { Log($"Stopped: {text}"); }
            catch (Exception ex) { Log("Error: " + ex.Message); }
            finally
            {
                cts = null;
                mine.Dispose();
                b.Text = text;
                b.Enabled = true;
            }
        };
        return b;
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        f.Controls.AddRange(controls);
        return f;
    }

    static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };

    void Log(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }

    void UpdateStatus()
    {
        bool running = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners().Any(e => e.Port == 5037);
        lblStatus.Text = $"Port 5037: {(running ? "adb server running" : "free (server stopped)")}";
        lblStatus.ForeColor = running ? Color.Green : Color.Firebrick;
    }

    string? Serial() => lstDevices.SelectedItem?.ToString()?.Split(' ', '\t')[0];

    void HideQr()
    {
        picQr.Image = null;
        picQr.Visible = false;
    }

    // ---------- adb runner ----------

    string FindAdb()
    {
        try
        {
            if (File.Exists(cfgFile))
            {
                var saved = File.ReadAllText(cfgFile).Trim();
                if (File.Exists(saved)) return saved;
            }
        }
        catch { }

        var list = new List<string>();
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            if (!string.IsNullOrWhiteSpace(d)) list.Add(Path.Combine(d.Trim(), "adb.exe"));
        foreach (var env in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var v = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrWhiteSpace(v)) list.Add(Path.Combine(v, "platform-tools", "adb.exe"));
        }
        list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Android", "Sdk", "platform-tools", "adb.exe"));
        return list.FirstOrDefault(File.Exists) ?? "";
    }

    async Task<string> Adb(string args, int timeoutMs = 20000, CancellationToken ct = default)
    {
        var path = txtAdb.Text.Trim();
        if (path == "" || !File.Exists(path)) return "adb.exe not found. Set the path first.";

        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo(path, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var outT = p.StandardOutput.ReadToEndAsync();
        var errT = p.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try { await p.WaitForExitAsync(linked.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            ct.ThrowIfCancellationRequested();   // user pressed Stop -> propagate
            return "Timeout: " + args;           // otherwise it was just the timeout
        }

        // start-server leaves a daemon holding the pipes, so don't wait forever
        var o = await Task.WhenAny(outT, Task.Delay(1500, CancellationToken.None)) == outT ? outT.Result : "";
        var e = await Task.WhenAny(errT, Task.Delay(1500, CancellationToken.None)) == errT ? errT.Result : "";
        return (o + e).Trim();
    }

    // ---------- actions ----------

    Task Browse(CancellationToken ct)
    {
        using var dlg = new OpenFileDialog { Filter = "adb.exe|adb.exe" };
        if (dlg.ShowDialog() == DialogResult.OK) txtAdb.Text = dlg.FileName;
        return Task.CompletedTask;
    }

    async Task RefreshDevices(CancellationToken ct = default)
    {
        var o = await Adb("devices -l", ct: ct);
        lstDevices.Items.Clear();
        foreach (var line in o.Split('\n'))
        {
            var l = line.Trim();
            if (l.Length == 0 || l.StartsWith("*") || l.StartsWith("List of")) continue;
            lstDevices.Items.Add(l);
        }
        if (lstDevices.Items.Count == 1) lstDevices.SelectedIndex = 0;
        UpdateStatus();
    }

    async Task RestartServer(CancellationToken ct)
    {
        Log(await Adb("kill-server", ct: ct));
        await Task.Delay(500, ct);
        Log(await Adb("start-server", ct: ct));
        await RefreshDevices(ct);
        Log("Server restarted.");
    }

    async Task Connect(CancellationToken ct)
    {
        var ip = txtIp.Text.Trim();
        if (ip == "") { Log("Enter the phone IP."); return; }
        var port = txtPort.Text.Trim();
        if (port == "") port = "5555";
        Log(await Adb($"connect {ip}:{port}", ct: ct));
        await RefreshDevices(ct);
    }

    async Task DisconnectSelected(CancellationToken ct)
    {
        var s = Serial();
        if (s == null) { Log("Select a device first."); return; }
        Log(await Adb($"disconnect {s}", ct: ct));
        await RefreshDevices(ct);
    }

    async Task RebootSelected(CancellationToken ct)
    {
        var s = Serial();
        if (s == null) { Log("Select a device first."); return; }
        Log(await Adb($"-s {s} reboot", ct: ct));
        await Task.Delay(1000, ct);
        await RefreshDevices(ct);
    }

    async Task UsbToWifi(CancellationToken ct)
    {
        var s = Serial();
        if (s == null || s.Contains(':') || s.StartsWith("adb-")) { Log("Select a device connected via USB."); return; }

        var route = await Adb($"-s {s} shell ip route", ct: ct);
        var line = route.Split('\n').FirstOrDefault(l => l.Contains("wlan")) ?? route;
        var m = Regex.Match(line, @"src (\d+\.\d+\.\d+\.\d+)");
        if (!m.Success) { Log("Could not read the phone IP. Is Wi-Fi on?"); return; }

        var ip = m.Groups[1].Value;
        Log(await Adb($"-s {s} tcpip 5555", ct: ct));
        await Task.Delay(2000, ct);
        Log(await Adb($"connect {ip}:5555", ct: ct));
        txtIp.Text = ip;
        txtPort.Text = "5555";
        await RefreshDevices(ct);
    }

    // ---------- mDNS / auto connect ----------

    async Task<List<Svc>> Mdns(CancellationToken ct)
    {
        var o = await Adb("mdns services", 8000, ct);
        var list = new List<Svc>();
        foreach (Match m in Regex.Matches(o, @"^(\S+)\s+(_adb-tls-\w+\._tcp)\.?\s+(\S+:\d+)", RegexOptions.Multiline))
            list.Add(new Svc(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
        return list;
    }

    async Task AutoConnect(CancellationToken ct)
    {
        Log("Searching for devices (mDNS)...");
        var svcs = (await Mdns(ct)).Where(s => s.Type.Contains("connect")).ToList();
        if (svcs.Count == 0)
        {
            Log("Nothing found. Turn on Wireless debugging, be on the same Wi-Fi, or enter the IP and use Scan ports.");
            return;
        }
        foreach (var s in svcs)
        {
            ct.ThrowIfCancellationRequested();
            Log($"Found {s.Name} at {s.Addr}");
            Log(await Adb($"connect {s.Addr}", ct: ct));
        }
        await RefreshDevices(ct);
    }

    // ---------- port scan ----------

    async Task ScanPorts(CancellationToken ct)
    {
        var ip = txtIp.Text.Trim();
        if (ip == "") { Log("Enter the phone IP first."); return; }

        Log($"Scanning {ip} (5555 and 30000-50000)... press Stop to cancel.");
        var open = new ConcurrentBag<int>();
        int done = 0;
        var ports = Enumerable.Range(30000, 20001).Append(5555).ToList();

        // run the heavy part off the UI thread so the window stays responsive
        await Task.Run(async () =>
        {
            using var sem = new SemaphoreSlim(400);
            var tasks = ports.Select(async port =>
            {
                try { await sem.WaitAsync(ct); }
                catch (OperationCanceledException) { return; }
                try
                {
                    using var c = new TcpClient();
                    using var to = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    to.CancelAfter(400);
                    try
                    {
                        await c.ConnectAsync(ip, port, to.Token);
                        if (c.Connected) open.Add(port);
                    }
                    catch { }
                }
                finally
                {
                    sem.Release();
                    Interlocked.Increment(ref done);
                }
            }).ToList();
            await Task.WhenAll(tasks);
        }, CancellationToken.None);

        ct.ThrowIfCancellationRequested();

        if (open.IsEmpty) { Log("No open ports found."); return; }
        Log("Open ports: " + string.Join(", ", open.OrderBy(x => x)));

        foreach (var port in open.OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();
            var r = await Adb($"connect {ip}:{port}", ct: ct);
            Log($"{ip}:{port} -> {r}");
            if (r.Contains("connected to"))
            {
                txtPort.Text = port.ToString();
                break;
            }
        }
        await RefreshDevices(ct);
    }

    // ---------- pairing ----------

    async Task PairManual(CancellationToken ct)
    {
        var addr = txtPairAddr.Text.Trim();
        var code = txtCode.Text.Trim();
        if (addr == "" || code == "") { Log("Enter ip:port and the pairing code."); return; }
        Log(await Adb($"pair {addr} {code}", ct: ct));
    }

    void ShowQr(string text)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new QRCode(data);
        picQr.Image = qr.GetGraphic(8);
        picQr.Visible = true;
    }

    async Task QrPair(CancellationToken outer)
    {
        qrCts?.Cancel();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        qrCts = cts;
        var ct = cts.Token;

        string name = "ADB_" + Random.Shared.Next(100000, 999999);
        string pass = Random.Shared.Next(10000000, 99999999).ToString();
        ShowQr($"WIFI:T:ADB;S:{name};P:{pass};;");
        Log("Phone: Developer options > Wireless debugging > Pair device with QR code, then scan.");

        try
        {
            for (int i = 0; i < 120; i++)
            {
                ct.ThrowIfCancellationRequested();
                var svc = (await Mdns(ct)).FirstOrDefault(s => s.Type.Contains("pairing") && s.Name == name);
                if (svc != null)
                {
                    Log(await Adb($"pair {svc.Addr} {pass}", ct: ct));
                    HideQr();
                    await Task.Delay(2000, ct);
                    await AutoConnect(ct);
                    return;
                }
                await Task.Delay(1000, ct);
            }
            Log("QR expired. Try again.");
            HideQr();
        }
        catch (OperationCanceledException)
        {
            HideQr();
            throw;   // the button reports "Stopped"
        }
        finally
        {
            if (ReferenceEquals(qrCts, cts)) qrCts = null;
        }
    }
}