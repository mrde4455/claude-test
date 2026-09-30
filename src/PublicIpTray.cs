using System;
using System.Drawing;
using System.Drawing.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Shows the current public IP address as a system tray icon.
// Left-click copies it; right-click for Refresh / Exit.
static class PublicIpTray
{
    const int RefreshMinutes = 5;
    static readonly string[] Endpoints = {
        "https://api.ipify.org", "https://checkip.amazonaws.com", "https://icanhazip.com"
    };

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static NotifyIcon notify;
    static ToolStripMenuItem copyItem;
    static System.Windows.Forms.Timer timer;
    static string ip;
    static int busy;

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

            notify = new NotifyIcon { ContextMenuStrip = menu, Visible = true, Icon = MakeIcon("...") };
            notify.Text = "Public IP: checking...";
            notify.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) CopyIp(); };

            timer = new System.Windows.Forms.Timer { Interval = RefreshMinutes * 60 * 1000 };
            timer.Tick += delegate { Refresh(); };
            timer.Start();

            // Marshals lookup results back to the UI thread.
            var ctx = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            Refresh();

            Application.Run();
        }
    }

    static void Refresh()
    {
        if (Interlocked.Exchange(ref busy, 1) == 1) return;
        var ui = SynchronizationContext.Current;
        ThreadPool.QueueUserWorkItem(delegate {
            string result = Lookup();
            ui.Post(delegate { busy = 0; Apply(result); }, null);
        });
    }

    static string Lookup()
    {
        foreach (var url in Endpoints)
        {
            try
            {
                using (var wc = new WebClient())
                {
                    string r = wc.DownloadString(url).Trim();
                    if (System.Text.RegularExpressions.Regex.IsMatch(r, "^[0-9a-fA-F\\.:]+$")) return r;
                }
            }
            catch { }
        }
        return null;
    }

    static void Apply(string result)
    {
        ip = result;
        string label, tip;
        if (ip != null)
        {
            label = ip.Contains(".") ? ip.Substring(ip.LastIndexOf('.') + 1) : "6";
            tip = "Public IP: " + ip;
        }
        else
        {
            label = "?";
            tip = "Public IP: unavailable";
        }
        var old = notify.Icon;
        notify.Icon = MakeIcon(label);
        if (old != null) old.Dispose();
        notify.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        copyItem.Text = ip != null ? "Copy IP (" + ip + ")" : "Copy IP";
        copyItem.Enabled = ip != null;
    }

    static void CopyIp()
    {
        if (ip == null) return;
        try { Clipboard.SetText(ip); notify.ShowBalloonTip(1500, "Public IP", "Copied " + ip, ToolTipIcon.Info); }
        catch { }
    }

    static Icon MakeIcon(string text)
    {
        using (var bmp = new Bitmap(32, 32))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Segoe UI", text.Length <= 2 ? 20 : 15, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.Clear(Color.FromArgb(30, 90, 200));
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, 32, 32), fmt);
            IntPtr h = bmp.GetHicon();
            var icon = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return icon;
        }
    }
}
