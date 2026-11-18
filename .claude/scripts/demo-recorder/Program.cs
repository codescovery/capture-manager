using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CcmDemo
{
    // Records the README animations. Every frame is drawn from pictures of windows this tool owns or started
    // (three demo windows with made-up content, Capture Manager, a throwaway browser window): the screen is never
    // captured, so nothing else on the desktop can end up in a GIF.
    //
    //   ccm-demo --scene focus|pause|web --lang en|pt --app <CodescoveryCaptureManager.App.exe> --out <frames dir>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            var options = new Options(args);
            using var recorder = new Recorder(options);
            try
            {
                recorder.Run();
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(options.Output, "error.txt"), ex.ToString());
                return 1;
            }
        }
    }

    internal sealed class Options
    {
        public Options(string[] args)
        {
            Scene = Read(args, "--scene", "focus");
            Language = Read(args, "--lang", "en");
            AppPath = Read(args, "--app", null);
            Output = Read(args, "--out", Path.Combine(Path.GetTempPath(), "ccm-demo-frames"));
            Directory.CreateDirectory(Output);
        }

        public string Scene { get; }
        public string Language { get; }
        public string AppPath { get; }
        public string Output { get; }

        private static string Read(string[] args, string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }

    /// <summary>One step of a scene: what happens, the caption under the picture, how long it stays.</summary>
    internal sealed record Step(Action Action, string Caption, double Seconds);

    internal sealed class Recorder : IDisposable
    {
        private const int FramesPerSecond = 10;
        private const string ProfileId = "5b1d6a0e-3c7f-4a55-9d2e-6c1f0a7b9e42";
        private static readonly Rectangle AppBounds = new Rectangle(660, 60, 900, 600);

        private readonly Options _options;
        private readonly DemoWindow[] _windows = { new RoadmapWindow(), new BudgetWindow(), new ChatWindow() };
        private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "ccm-demo-data-" + Guid.NewGuid().ToString("N"));
        private readonly string _browserProfile = Path.Combine(Path.GetTempPath(), "ccm-demo-browser-" + Guid.NewGuid().ToString("N"));
        private Process _app;
        private Process _browser;
        private IntPtr _appWindow;
        private IntPtr _browserWindow;
        private DemoWindow _focused;
        private int _frame;

        public Recorder(Options options) => _options = options;

        private bool IsWebScene => _options.Scene == "web";

        public void Run()
        {
            foreach (var window in _windows) window.Show();
            Pump(0.5);
            StartApp();
            if (IsWebScene) StartBrowser();
            foreach (var step in BuildScene()) Play(step);
        }

        // ------------------------------------------------------------------ scenes

        private IEnumerable<Step> BuildScene()
        {
            var roadmap = _windows[0];
            var budget = _windows[1];
            var chat = _windows[2];
            var text = Captions.For(_options.Language);
            return _options.Scene switch
            {
                "pause" => new[]
                {
                    new Step(() => Focus(roadmap), text.Sharing, 2.0),
                    new Step(PressPauseHotkey, text.PausePressed, 2.6),
                    new Step(() => Focus(budget), text.PauseHolds, 2.6),
                    new Step(PressPauseHotkey, text.Resumed, 2.6),
                },
                "web" => new[]
                {
                    new Step(() => Focus(roadmap), text.WebIntro, 3.0),
                    new Step(() => Focus(budget), text.WebFollows, 2.6),
                    new Step(() => Focus(chat), text.WebBlank, 2.6),
                    new Step(() => Focus(roadmap), text.WebBack, 2.4),
                },
                _ => new[]
                {
                    new Step(() => Focus(roadmap), text.Picked, 2.6),
                    new Step(() => Focus(budget), text.Follows, 2.4),
                    new Step(() => Focus(chat), text.NotPicked, 3.0),
                    new Step(() => Focus(roadmap), text.Back, 2.4),
                },
            };
        }

        private void Play(Step step)
        {
            step.Action();
            var frames = (int)Math.Round(step.Seconds * FramesPerSecond);
            for (var index = 0; index < frames; index++)
            {
                Pump(1.0 / FramesPerSecond);
                SaveFrame(step.Caption);
            }
        }

        private void Focus(DemoWindow window)
        {
            for (var attempt = 0; attempt < 5 && !Native.IsForeground(window.Handle); attempt++)
            {
                Native.TakeForeground(window.Handle);
                Pump(0.15);
            }
            if (!Native.IsForeground(window.Handle))
                throw new InvalidOperationException($"Could not give the foreground to '{window.Text}': the recording would show the wrong thing.");
            _focused = window;
        }

        private void PressPauseHotkey() => Native.PostHotkey(_appWindow);

        // ------------------------------------------------------------------ processes

        private void StartApp()
        {
            Directory.CreateDirectory(_dataDirectory);
            var rules = string.Join(",", _windows.Where(window => window.IsPicked).Select(window =>
                $"{{\"processName\":\"ccm-demo\",\"titleMatch\":\"Contains\",\"titlePattern\":\"{window.Text}\",\"autoAttach\":false}}"));
            File.WriteAllText(Path.Combine(_dataDirectory, "profiles.json"), $"[{{\"id\":\"{ProfileId}\",\"name\":\"Demo\",\"rules\":[{rules}]}}]");
            File.WriteAllText(Path.Combine(_dataDirectory, "settings.json"), $"{{\"lastProfileId\":\"{ProfileId}\"}}");

            var start = new ProcessStartInfo(_options.AppPath) { UseShellExecute = false };
            start.Environment["CAPTURE_MANAGER_DATA_DIR"] = _dataDirectory;
            _app = Process.Start(start);
            _appWindow = WaitForWindow(() => Native.FindWindow(_app.Id, (title, _) => title == "Captured Window"), "Capture Manager");
            Native.Place(_appWindow, AppBounds);
            Pump(2.5);
        }

        private void StartBrowser()
        {
            var edge = new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }
                .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "Microsoft", "Edge", "Application", "msedge.exe"))
                .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Microsoft Edge not found.");
            var arguments = string.Join(" ", "--app=http://127.0.0.1:47800/", $"--user-data-dir=\"{_browserProfile}\"", "--guest", "--no-first-run",
                "--no-default-browser-check", "--disable-sync", "--disable-features=CalculateNativeWinOcclusion", "--disable-backgrounding-occluded-windows",
                $"--window-position={AppBounds.X},{AppBounds.Y}", $"--window-size={AppBounds.Width},{AppBounds.Height}");
            _browser = Process.Start(new ProcessStartInfo(edge, arguments) { UseShellExecute = false });
            _browserWindow = WaitForWindow(
                () => Native.FindWindow(0, (title, className) => className == "Chrome_WidgetWin_1" && title.StartsWith("Capture Manager", StringComparison.Ordinal)),
                "the web viewer window");
            Native.Place(_browserWindow, AppBounds);
            Pump(3.0);
        }

        private IntPtr WaitForWindow(Func<IntPtr> find, string name)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < 30)
            {
                var window = find();
                if (window != IntPtr.Zero) return window;
                Pump(0.25);
            }
            throw new TimeoutException($"Timed out waiting for {name}.");
        }

        private static void Pump(double seconds)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(5);
            }
        }

        // ------------------------------------------------------------------ drawing

        private void SaveFrame(string caption)
        {
            using var canvas = new Bitmap(Layout.Width, Layout.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(canvas))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.Clear(Layout.Background);
                DrawDesktop(graphics);
                DrawOutput(graphics);
                DrawCaption(graphics, caption);
            }
            canvas.Save(Path.Combine(_options.Output, $"{++_frame:0000}.png"), ImageFormat.Png);
        }

        private void DrawDesktop(Graphics graphics)
        {
            var text = Captions.For(_options.Language);
            Layout.Heading(graphics, text.Desktop, Layout.DesktopX, Layout.HeadingY);
            for (var index = 0; index < _windows.Length; index++)
            {
                var window = _windows[index];
                var bounds = new Rectangle(Layout.DesktopX, Layout.ContentY + index * (Layout.ThumbHeight + Layout.ThumbGap), Layout.ThumbWidth, Layout.ThumbHeight);
                using (var picture = Native.Capture(window.Handle)) graphics.DrawImage(picture, bounds);
                var isFocused = window == _focused;
                Layout.Outline(graphics, bounds, isFocused ? Layout.Brand : Layout.Stroke, isFocused ? 3 : 1);
                var labelX = bounds.Right + 16;
                Layout.Write(graphics, window.Label, 15, FontStyle.Bold, Layout.TextColor, labelX, bounds.Y + 6);
                Layout.Chip(graphics, window.IsPicked ? text.PickedChip : text.NotPickedChip, window.IsPicked ? Layout.Brand : Layout.Muted, labelX, bounds.Y + 34);
                if (isFocused) Layout.Write(graphics, "● " + text.FocusedTag, 13, FontStyle.Bold, Layout.Brand, labelX, bounds.Y + 66);
            }
        }

        private void DrawOutput(Graphics graphics)
        {
            var text = Captions.For(_options.Language);
            Layout.Heading(graphics, IsWebScene ? text.WebViewer : text.Viewers, Layout.OutputX, Layout.HeadingY);
            using var picture = Native.Capture(IsWebScene ? _browserWindow : _appWindow);
            var scale = Math.Min((float)Layout.OutputWidth / picture.Width, (float)Layout.OutputHeight / picture.Height);
            var bounds = new Rectangle(Layout.OutputX, Layout.ContentY, (int)(picture.Width * scale), (int)(picture.Height * scale));
            graphics.DrawImage(picture, bounds);
            Layout.Outline(graphics, bounds, Layout.Stroke, 1);
        }

        private static void DrawCaption(Graphics graphics, string caption)
        {
            using var font = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Layout.TextColor);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(caption, font, brush, new RectangleF(0, Layout.Height - Layout.CaptionHeight, Layout.Width, Layout.CaptionHeight), format);
        }

        public void Dispose()
        {
            Kill(_browser);
            Kill(_app);
            foreach (var window in _windows) window.Dispose();
            TryDelete(_dataDirectory);
            TryDelete(_browserProfile);
        }

        private static void Kill(Process process)
        {
            try
            {
                if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
                process?.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
        }

        private static void TryDelete(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A file still held by a process that is shutting down: temp folder, left for the system to clean.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    internal static class Layout
    {
        public const int Width = 1280;
        public const int Height = 660;
        public const int CaptionHeight = 70;
        public const int HeadingY = 20;
        public const int ContentY = 52;
        public const int DesktopX = 28;
        public const int ThumbWidth = 250;
        public const int ThumbHeight = 160;
        public const int ThumbGap = 14;
        public const int OutputX = 500;
        public const int OutputWidth = 752;
        public const int OutputHeight = 508;

        public static readonly Color Background = Color.FromArgb(22, 22, 22);
        public static readonly Color Stroke = Color.FromArgb(70, 70, 70);
        public static readonly Color Brand = Color.FromArgb(141, 198, 63);
        public static readonly Color Muted = Color.FromArgb(150, 150, 150);
        public static readonly Color TextColor = Color.FromArgb(240, 240, 240);

        public static void Heading(Graphics graphics, string text, int x, int y)
            => Write(graphics, text.ToUpperInvariant(), 13, FontStyle.Bold, Muted, x, y);

        public static void Write(Graphics graphics, string text, float size, FontStyle style, Color color, float x, float y)
        {
            using var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            graphics.DrawString(text, font, brush, x, y);
        }

        public static void Outline(Graphics graphics, Rectangle bounds, Color color, float width)
        {
            using var pen = new Pen(color, width);
            graphics.DrawRectangle(pen, bounds);
        }

        public static void Chip(Graphics graphics, string text, Color color, float x, float y)
        {
            using var font = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = graphics.MeasureString(text, font);
            var bounds = new RectangleF(x, y, size.Width + 16, 24);
            using var path = Shapes.Rounded(bounds, 12);
            using var pen = new Pen(color, 1.5f);
            using var brush = new SolidBrush(color);
            graphics.DrawPath(pen, path);
            graphics.DrawString(text, font, brush, x + 8, y + 4);
        }
    }

    internal sealed record Captions(
        string Desktop, string Viewers, string WebViewer, string PickedChip, string NotPickedChip, string FocusedTag,
        string Picked, string Follows, string NotPicked, string Back,
        string Sharing, string PausePressed, string PauseHolds, string Resumed,
        string WebIntro, string WebFollows, string WebBlank, string WebBack)
    {
        private static readonly Captions English = new(
            "Your desktop", "What your viewers see: the Capture Manager window", "What your viewers see: the web viewer in a browser tab",
            "picked", "not picked", "focused",
            "You picked two windows. Viewers see the one you are working in.",
            "Switch to the other picked window: the shared view follows.",
            "Focus anything you did not pick: viewers see nothing.",
            "Back to a picked window, and it is shared again.",
            "A picked window is being shared.",
            "Ctrl+Alt+P pauses: everything is hidden at once.",
            "While paused nothing is shown, whatever you focus.",
            "Press Ctrl+Alt+P again to resume.",
            "The same picture is streamed to a page on your computer.",
            "Share that browser tab: it follows your focus too.",
            "Windows you did not pick never reach the page.",
            "Low delay, hardware encoded.");

        private static readonly Captions Portuguese = new(
            "Sua área de trabalho", "O que sua audiência vê: a janela do Capture Manager", "O que sua audiência vê: o viewer web numa aba do navegador",
            "escolhida", "não escolhida", "em foco",
            "Você escolheu duas janelas. A audiência vê aquela em que você trabalha.",
            "Troque para a outra janela escolhida: a imagem acompanha.",
            "Foque algo que você não escolheu: a audiência não vê nada.",
            "De volta a uma janela escolhida, ela aparece de novo.",
            "Uma janela escolhida está sendo compartilhada.",
            "Ctrl+Alt+P pausa: tudo some de uma vez.",
            "Em pausa nada aparece, seja qual for a janela em foco.",
            "Pressione Ctrl+Alt+P de novo para retomar.",
            "A mesma imagem é transmitida para uma página no seu computador.",
            "Compartilhe essa aba do navegador: ela também segue o seu foco.",
            "Janelas que você não escolheu nunca chegam à página.",
            "Baixo atraso, codificação por hardware.");

        public static Captions For(string language) => language == "pt" ? Portuguese : English;
    }
}
