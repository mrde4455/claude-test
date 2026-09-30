using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Shows the country flag of your current public IP address as a system tray icon.
// Hover for the IP and country; left-click copies the IP; right-click for Refresh / Exit.
static class PublicIpTray
{
    const int RefreshSeconds = 30;
    static readonly string[] IpEndpoints = {
        "https://api.ipify.org", "https://checkip.amazonaws.com", "https://icanhazip.com"
    };
    // Each returns the caller's two-letter country code, as plain text or JSON.
    static readonly string[] CountryEndpoints = {
        "https://ipapi.co/country/", "https://ipinfo.io/country", "https://api.country.is/"
    };
    static readonly Regex CountryPattern =
        new Regex("^([A-Za-z]{2})$|\"country\"\\s*:\\s*\"([A-Za-z]{2})\"");

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static NotifyIcon notify;
    static ToolStripMenuItem copyItem;
    static System.Windows.Forms.Timer timer;
    static string ip;
    static int busy;
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
                return wc.DownloadString(url).Trim();
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

    static void CopyIp()
    {
        if (ip == null) return;
        try { Clipboard.SetText(ip); notify.ShowBalloonTip(1500, "Public IP", "Copied " + ip, ToolTipIcon.Info); }
        catch { }
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
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("flag." + cc))
        {
            if (stream == null) return null;
            using (var flag = new Bitmap(stream))
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
