using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CcmDemo
{
    /// <summary>A window with made-up content, standing in for one of the user's programs.</summary>
    internal abstract class DemoWindow : Form
    {
        protected DemoWindow(string title, Point location)
        {
            Text = title;
            StartPosition = FormStartPosition.Manual;
            Location = location;
            ClientSize = new Size(560, 340);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowIcon = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// <summary>Short name shown next to the thumbnail in the recording.</summary>
        public abstract string Label { get; }

        public abstract bool IsPicked { get; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Draw(e.Graphics);
        }

        protected abstract void Draw(Graphics graphics);

        protected static void Print(Graphics graphics, string text, float size, FontStyle style, Color color, float x, float y)
        {
            using var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            graphics.DrawString(text, font, brush, x, y);
        }

        protected static void Card(Graphics graphics, Color color, RectangleF bounds, float radius = 10)
        {
            using var path = Shapes.Rounded(bounds, radius);
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
    }

    internal sealed class RoadmapWindow : DemoWindow
    {
        public RoadmapWindow() : base("Roadmap - Slides", new Point(40, 60)) { }

        public override string Label => "Roadmap - Slides";

        public override bool IsPicked => true;

        protected override void Draw(Graphics graphics)
        {
            graphics.Clear(Color.White);
            Card(graphics, Color.FromArgb(37, 99, 235), new RectangleF(0, 0, 560, 74), 0);
            Print(graphics, "Product roadmap", 30, FontStyle.Bold, Color.White, 24, 16);
            var cards = new[] { ("Q1", "Discovery", Color.FromArgb(219, 234, 254)), ("Q2", "Private beta", Color.FromArgb(220, 252, 231)), ("Q3", "Launch", Color.FromArgb(254, 243, 199)) };
            for (var index = 0; index < cards.Length; index++)
            {
                var bounds = new RectangleF(24 + index * 174, 104, 162, 190);
                Card(graphics, cards[index].Item3, bounds);
                Print(graphics, cards[index].Item1, 34, FontStyle.Bold, Color.FromArgb(30, 41, 59), bounds.X + 16, bounds.Y + 16);
                Print(graphics, cards[index].Item2, 17, FontStyle.Regular, Color.FromArgb(51, 65, 85), bounds.X + 16, bounds.Y + 72);
                Card(graphics, Color.FromArgb(60, 30, 41, 59), new RectangleF(bounds.X + 16, bounds.Y + 120, 130, 8), 4);
                Card(graphics, Color.FromArgb(60, 30, 41, 59), new RectangleF(bounds.X + 16, bounds.Y + 140, 90, 8), 4);
            }
        }
    }

    internal sealed class BudgetWindow : DemoWindow
    {
        private static readonly string[] Rows = { "Design", "Engineering", "Marketing", "Support", "Total" };
        private static readonly string[] Values = { "12,400", "58,900", "21,750", "9,300", "102,350" };

        public BudgetWindow() : base("Budget 2027 - Sheets", new Point(80, 110)) { }

        public override string Label => "Budget 2027 - Sheets";

        public override bool IsPicked => true;

        protected override void Draw(Graphics graphics)
        {
            graphics.Clear(Color.White);
            Card(graphics, Color.FromArgb(22, 163, 74), new RectangleF(0, 0, 560, 56), 0);
            Print(graphics, "Budget 2027", 24, FontStyle.Bold, Color.White, 24, 12);
            using var line = new Pen(Color.FromArgb(226, 232, 240));
            for (var row = 0; row < Rows.Length; row++)
            {
                var top = 84 + row * 46;
                var isTotal = row == Rows.Length - 1;
                if (isTotal) Card(graphics, Color.FromArgb(240, 253, 244), new RectangleF(24, top - 6, 512, 44), 6);
                graphics.DrawLine(line, 24, top + 38, 536, top + 38);
                var style = isTotal ? FontStyle.Bold : FontStyle.Regular;
                Print(graphics, Rows[row], 19, style, Color.FromArgb(30, 41, 59), 36, top + 4);
                Print(graphics, Values[row], 19, style, Color.FromArgb(30, 41, 59), 420, top + 4);
            }
        }
    }

    internal sealed class ChatWindow : DemoWindow
    {
        public ChatWindow() : base("Team chat (private)", new Point(120, 160)) { }

        public override string Label => "Team chat (private)";

        public override bool IsPicked => false;

        protected override void Draw(Graphics graphics)
        {
            graphics.Clear(Color.FromArgb(30, 27, 46));
            Print(graphics, "Team chat", 22, FontStyle.Bold, Color.White, 24, 16);
            Bubble(graphics, "Lunch at 12?", 24, 70, false);
            Bubble(graphics, "Yes! Do not tell Sam about the party", 150, 132, true);
            Bubble(graphics, "The cake is hidden in room 4", 24, 194, false);
            Bubble(graphics, "Perfect, see you at 6", 262, 256, true);
        }

        private static void Bubble(Graphics graphics, string text, float x, float y, bool isMine)
        {
            using var font = new Font("Segoe UI", 17, FontStyle.Regular, GraphicsUnit.Pixel);
            var size = graphics.MeasureString(text, font);
            Card(graphics, isMine ? Color.FromArgb(124, 58, 237) : Color.FromArgb(55, 48, 80), new RectangleF(x, y, size.Width + 28, 44), 16);
            Print(graphics, text, 17, FontStyle.Regular, Color.White, x + 14, y + 10);
        }
    }

    internal static class Shapes
    {
        public static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
