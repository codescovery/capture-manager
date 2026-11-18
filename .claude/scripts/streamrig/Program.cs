using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Test window for the Capture Manager stream: the client area flips black/white on a timer and a bar moves every
// compositor frame. Each flip is time-stamped; a page served from here decodes the stream and measures the delay.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var width = Arg(args, "--width", 1280);
        var height = Arg(args, "--height", 720);
        var port = Arg(args, "--port", 47811);
        var toggleMs = Arg(args, "--toggle-ms", 233);
        var page = Path.Combine(AppContext.BaseDirectory, "rig.html");
        var pageIndex = Array.IndexOf(args, "--page");
        if (pageIndex >= 0) page = args[pageIndex + 1];

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var form = new RigForm(width, height, toggleMs);
        new Thread(() => RigServer.Run(port, form, page)) { IsBackground = true }.Start();
        Application.Run(form);
    }

    private static int Arg(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 ? int.Parse(args[index + 1], CultureInfo.InvariantCulture) : fallback;
    }
}

internal sealed class RigForm : Form
{
    public static readonly Stopwatch Clock = Stopwatch.StartNew();
    private const int BarHeight = 40;
    private const int BarWidth = 120;

    private readonly int _toggleMs;
    private readonly List<(double Time, int State)> _toggles = new();
    private readonly Size _client;
    private bool _white;
    private bool _togglePending;
    private double _nextToggle;
    private int _barX;
    private int _tickQueued;
    private long _paints;
    private double _lastPaint;
    private double _maxPaintGap;

    public double TakeMaxPaintGap() { var gap = _maxPaintGap; _maxPaintGap = 0; return gap; }
    private volatile bool _closing;

    public RigForm(int width, int height, int toggleMs)
    {
        _toggleMs = toggleMs;
        _client = new Size(width, height);
        Text = "ccm-stream-rig";
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public static double Now => Clock.Elapsed.TotalMilliseconds;

    public long Paints => Interlocked.Read(ref _paints);

    public bool IsForeground => GetForegroundWindow() == _handle;

    public Size ClientPixels { get; private set; }

    private IntPtr _handle;

    public (double, int)[] TogglesSince(double since)
    {
        lock (_toggles) return _toggles.Where(t => t.Time >= since).ToArray();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _handle = Handle;
        ClientSize = _client;
        ClientPixels = ClientSize;
        TakeForeground();
        new Thread(VsyncLoop) { IsBackground = true }.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        base.OnFormClosing(e);
    }

    // One tick per compositor frame, so the window changes at the display rate.
    private void VsyncLoop()
    {
        while (!_closing)
        {
            DwmFlush();
            if (Interlocked.Exchange(ref _tickQueued, 1) == 1) continue;
            try { BeginInvoke(new Action(Tick)); } catch (InvalidOperationException) { return; }
        }
    }

    private void Tick()
    {
        Interlocked.Exchange(ref _tickQueued, 0);
        var now = Now;
        var barTop = 0;
        var oldBar = new Rectangle(_barX, barTop, BarWidth, BarHeight);
        _barX = (_barX + 16) % Math.Max(1, Math.Min(ClientSize.Width, 1800) - BarWidth);
        if (now >= _nextToggle)
        {
            _white = !_white;
            _togglePending = true;
            _nextToggle = now + _toggleMs;
            Invalidate();
            return;
        }

        Invalidate(oldBar);
        Invalidate(new Rectangle(_barX, barTop, BarWidth, BarHeight));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(_white ? Color.White : Color.Black);
        using var brush = new SolidBrush(Color.FromArgb(141, 198, 63));
        e.Graphics.FillRectangle(brush, _barX, 0, BarWidth, BarHeight);
        Interlocked.Increment(ref _paints);
        var paintedAt = Now;
        if (_lastPaint > 0 && paintedAt - _lastPaint > _maxPaintGap) _maxPaintGap = paintedAt - _lastPaint;
        _lastPaint = paintedAt;
        if (!_togglePending) return;

        _togglePending = false;
        lock (_toggles) _toggles.Add((Now, _white ? 1 : 0));
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        const int GetMinMaxInfo = 0x24;
        if (m.Msg != GetMinMaxInfo) return;

        // Allow a window larger than the screen (4K source on a smaller display).
        var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
        info.MaxTrackSize = new Point(8192, 8192);
        Marshal.StructureToPtr(info, m.LParam, false);
    }

    public void TakeForeground()
    {
        if (SetForegroundWindow(_handle) && IsForeground) return;

        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var ownThread = GetCurrentThreadId();
        AttachThreadInput(ownThread, foregroundThread, true);
        BringWindowToTop(_handle);
        SetForegroundWindow(_handle);
        AttachThreadInput(ownThread, foregroundThread, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attached);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}

internal static class RigServer
{
    public static void Run(int port, RigForm form, string pagePath)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var lastPaints = 0L;
        var lastSample = RigForm.Now;
        var paintsPerSecond = 0.0;
        while (true)
        {
            var context = listener.GetContext();
            var path = context.Request.Url.AbsolutePath;
            string body;
            var contentType = "application/json";
            if (path == "/time") body = "{\"t\":" + Number(RigForm.Now) + "}";
            else if (path == "/toggles")
            {
                var since = double.Parse(context.Request.QueryString["since"] ?? "0", CultureInfo.InvariantCulture);
                body = "[" + string.Join(",", form.TogglesSince(since).Select(t => "{\"t\":" + Number(t.Item1) + ",\"s\":" + t.Item2 + "}")) + "]";
            }
            else if (path == "/status")
            {
                var now = RigForm.Now;
                var paints = form.Paints;
                if (now - lastSample >= 500)
                {
                    paintsPerSecond = (paints - lastPaints) * 1000.0 / (now - lastSample);
                    lastPaints = paints;
                    lastSample = now;
                }
                body = "{\"foreground\":" + (form.IsForeground ? "true" : "false") + ",\"paintsPerSecond\":" + Number(paintsPerSecond) +
                       ",\"maxPaintGapMs\":" + Number(form.TakeMaxPaintGap()) + ",\"clientWidth\":" + form.ClientPixels.Width + ",\"clientHeight\":" + form.ClientPixels.Height + "}";
            }
            else if (path == "/focus")
            {
                form.BeginInvoke(new Action(form.TakeForeground));
                body = "{}";
            }
            else if (path == "/quit")
            {
                form.BeginInvoke(new Action(form.Close));
                body = "{}";
            }
            else
            {
                body = File.ReadAllText(pagePath);
                contentType = "text/html; charset=utf-8";
            }

            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = contentType;
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
