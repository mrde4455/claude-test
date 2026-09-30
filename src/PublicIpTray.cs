using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading;
using System.Windows.Forms;

// Shows the country flag of your current public IP address as a system tray icon.
// Hover for the IP and country; left-click copies the IP; right-click for Refresh / Exit.
static class PublicIpTray
{
    const int RefreshSeconds = 5;
    static readonly string[] IpEndpoints = {
        "https://api.ipify.org", "https://checkip.amazonaws.com", "https://icanhazip.com"
    };
    // Each returns the caller's two-letter country code, as plain text or JSON.
    static readonly string[] CountryEndpoints = {
        "https://ipapi.co/country/", "https://ipinfo.io/country", "https://api.country.is/"
    };
    static readonly Regex CountryPattern =
        new Regex("^([A-Za-z]{2})$|\"country\"\\s*:\\s*\"([A-Za-z]{2})\"");

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "PublicIpTray";

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static NotifyIcon notify;
    static ToolStripMenuItem copyItem;
    static System.Windows.Forms.Timer timer;
    static string ip;
    static string country;
    static int busy;
    static string previousIp; // last successfully detected IP, kept across offline periods
    static string lastIp, lastCountry; // country is only re-looked-up when the IP changes

    [STAThread]
    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "PublicIpTray.SingleInstance", out created))
        {
            if (!created) return;

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            Application.EnableVisualStyles();

            var menu = new ContextMenuStrip();
            copyItem = new ToolStripMenuItem("Copy IP", null, delegate { CopyIp(); });
            menu.Items.Add(copyItem);
            menu.Items.Add(new ToolStripMenuItem("Refresh now", null, delegate { Refresh(); }));
            menu.Items.Add(new ToolStripMenuItem("Test notification", null, delegate {
                ShowChangeToast("203.0.113.5", ip ?? "198.51.100.7", country);
            }));
            var startup = new ToolStripMenuItem("Start with Windows") { Checked = IsAutoStart() };
            startup.Click += delegate { startup.Checked = SetAutoStart(!startup.Checked); };
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate {
                timer.Stop();
                notify.Visible = false;
                notify.Dispose();
                Application.Exit();
            }));

            notify = new NotifyIcon { ContextMenuStrip = menu, Visible = true, Icon = MakeTextIcon("...") };
            notify.Text = "Public IP: checking...";
            notify.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) CopyIp(); };

            timer = new System.Windows.Forms.Timer { Interval = RefreshSeconds * 1000 };
            timer.Tick += delegate { Refresh(); };
            timer.Start();

            // Marshals lookup results back to the UI thread.
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Refresh();

            Application.Run();
        }
    }

    static void Refresh()
    {
        if (Interlocked.Exchange(ref busy, 1) == 1) return;
        var ui = SynchronizationContext.Current;
        ThreadPool.QueueUserWorkItem(delegate {
            var result = Lookup();
            ui.Post(delegate { busy = 0; Apply(result[0], result[1]); }, null);
        });
    }

    static string Fetch(string url)
    {
        try
        {
            using (var wc = new WebClient())
            {
                wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                return wc.DownloadString(url).Trim();
            }
        }
        catch { return null; }
    }

    // Returns { ip, countryCode }; either may be null.
    static string[] Lookup()
    {
        string addr = null, cc = null;
        foreach (var url in IpEndpoints)
        {
            string r = Fetch(url);
            if (r != null && Regex.IsMatch(r, "^[0-9a-fA-F\\.:]+$")) { addr = r; break; }
        }
        if (addr == null) return new string[] { null, null };
        if (addr == lastIp && lastCountry != null) return new string[] { addr, lastCountry };
        foreach (var url in CountryEndpoints)
        {
            string r = Fetch(url);
            if (r == null) continue;
            var m = CountryPattern.Match(r);
            if (m.Success)
            {
                cc = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToLowerInvariant();
                break;
            }
        }
        lastIp = addr;
        lastCountry = cc;
        return new string[] { addr, cc };
    }

    static void Apply(string addr, string cc)
    {
        ip = addr;
        country = cc;
        if (addr != null)
        {
            if (previousIp != null && previousIp != addr)
            {
                ShowChangeToast(previousIp, addr, cc);
            }
            previousIp = addr;
        }
        string tip;
        Icon icon = null;
        if (ip != null)
        {
            string name = CountryName(cc);
            tip = "Public IP: " + ip + (name != null ? " (" + name + ")" : "");
            if (cc != null) icon = FlagIcon(cc);
            if (icon == null) icon = MakeTextIcon(cc != null ? cc.ToUpperInvariant() : "IP");
        }
        else
        {
            tip = "Public IP: unavailable";
            icon = MakeTextIcon("?");
        }
        var old = notify.Icon;
        notify.Icon = icon;
        if (old != null) old.Dispose();
        notify.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        copyItem.Text = ip != null ? "Copy IP (" + ip + ")" : "Copy IP";
        copyItem.Enabled = ip != null;
    }

    static string CountryName(string cc)
    {
        if (cc == null) return null;
        try { return new RegionInfo(cc.ToUpperInvariant()).EnglishName; }
        catch { return cc.ToUpperInvariant(); }
    }

    static bool IsAutoStart()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunValue) != null;
        }
        catch { return false; }
    }

    // Returns the resulting state (false if the registry write failed).
    static bool SetAutoStart(bool enable)
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunValue, false);
            }
            return enable;
        }
        catch { return IsAutoStart(); }
    }

    static void CopyIp()
    {
        if (ip == null) return;
        try { Clipboard.SetText(ip); Toast.Show("Copied to clipboard", ip, null, 2000); }
        catch { }
    }

    static void ShowChangeToast(string oldIp, string newIp, string cc)
    {
        string n = CountryName(cc);
        Toast.Show("Public IP changed", oldIp + "  \u2192  " + newIp + (n != null ? "\n" + n : ""), cc, 8000);
    }

    internal static Bitmap LoadFlag(string cc)
    {
        if (cc == null) return null;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("flag." + cc))
            return stream == null ? null : new Bitmap(stream);
    }

    static Icon ToIcon(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icon;
    }

    // Draws the embedded flag PNG (16x11) scaled up and centered on a 32x32 canvas.
    static Icon FlagIcon(string cc)
    {
        using (var flag = LoadFlag(cc))
        {
            if (flag == null) return null;
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int w = 30, h = Math.Max(1, flag.Height * w / flag.Width);
                g.DrawImage(flag, new Rectangle((32 - w) / 2, (32 - h) / 2, w, h));
                return ToIcon(bmp);
            }
        }
    }

    static Icon MakeTextIcon(string text)
    {
        using (var bmp = new Bitmap(32, 32))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Segoe UI", text.Length <= 2 ? 20 : 15, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.Clear(Color.FromArgb(30, 90, 200));
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, 32, 32), fmt);
            return ToIcon(bmp);
        }
    }
}

// Small always-on-top popup in the bottom-right corner. Unlike Windows balloon
// notifications it doesn't depend on Focus Assist or per-app notification settings.
class Toast : Form
{
    static Toast current;
    readonly string title, body;
    readonly Bitmap flag;
    readonly System.Windows.Forms.Timer life = new System.Windows.Forms.Timer();

    public static void Show(string title, string body, string cc, int ms)
    {
        if (current != null) current.Close();
        current = new Toast(title, body, cc, ms);
        current.Show();
    }

    Toast(string title, string body, string cc, int ms)
    {
        this.title = title; this.body = body;
        flag = PublicIpTray.LoadFlag(cc);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 36);
        DoubleBuffered = true;
        Size = new Size(340, 86);
        var area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        Click += delegate { Close(); };
        life.Interval = ms;
        life.Tick += delegate { Close(); };
        life.Start();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008; // NOACTIVATE | TOOLWINDOW | TOPMOST
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (var pen = new Pen(Color.FromArgb(70, 70, 78)))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        int x = 16;
        if (flag != null)
        {
            int w = 40, h = Math.Max(1, flag.Height * w / flag.Width);
            g.DrawImage(flag, new Rectangle(16, (Height - h) / 2, w, h));
            x = 68;
        }
        using (var tf = new Font("Segoe UI", 10, FontStyle.Bold))
        using (var bf = new Font("Segoe UI", 10))
        {
            g.DrawString(title, tf, Brushes.White, x, 12);
            g.DrawString(body, bf, Brushes.Gainsboro, new RectangleF(x, 36, Width - x - 10, Height - 40));
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        life.Stop(); life.Dispose();
        if (flag != null) flag.Dispose();
        if (current == this) current = null;
        base.OnFormClosed(e);
    }
}
