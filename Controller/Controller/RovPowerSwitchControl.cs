using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    /// <summary>
    /// Cyber-style ROV power switch control.
    /// </summary>
    public class RovPowerSwitchControl : UserControl
    {
        private bool _isOn;
        private bool _hovered;
        private bool _pressed;

        private readonly Color _bgDark = Color.FromArgb(12, 18, 28);
        private readonly Color _bgLight = Color.FromArgb(26, 38, 56);
        private readonly Color _accentOn = Color.FromArgb(0, 255, 200);
        private readonly Color _accentOff = Color.FromArgb(255, 110, 70);
        private readonly Color _accentIdle = Color.FromArgb(90, 140, 170);

        public event EventHandler PowerStateChanged;

        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                Invalidate();
                PowerStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public RovPowerSwitchControl()
        {
            Size = new Size(136, 41);
            MinimumSize = new Size(120, 36);
            MaximumSize = new Size(170, 48);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressed && e.Button == MouseButtons.Left)
            {
                _pressed = false;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            IsOn = !IsOn;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(2, 2, Width - 4, Height - 4);
            var accent = _isOn ? _accentOn : _accentOff;
            var glow = Color.FromArgb(_hovered ? 170 : 100, accent);

            using (var path = CreateRoundRect(rect, 10f))
            using (var glowPen = new Pen(glow, _hovered ? 5f : 3f))
            using (var borderPen = new Pen(Color.FromArgb(210, accent), 2f))
            using (var fill = new LinearGradientBrush(rect, _bgDark, _bgLight, 90f))
            using (var hatch = new HatchBrush(HatchStyle.DarkDownwardDiagonal, Color.FromArgb(40, 255, 255, 255), Color.Transparent))
            {
                g.DrawPath(glowPen, path);
                g.FillPath(fill, path);
                g.FillPath(hatch, path);
                g.DrawPath(borderPen, path);
            }

            DrawScanlines(g, rect);
            DrawSlider(g, rect, accent);
            DrawText(g, rect, accent);
            DrawDecor(g, rect, accent);
        }

        private void DrawScanlines(Graphics g, Rectangle rect)
        {
            using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
            {
                for (int y = rect.Top + 6; y < rect.Bottom - 6; y += 4)
                {
                    g.DrawLine(pen, rect.Left + 6, y, rect.Right - 6, y);
                }
            }
        }

        private void DrawSlider(Graphics g, Rectangle rect, Color accent)
        {
            int padding = 6;
            int sliderW = rect.Width / 2 - padding;
            int sliderH = rect.Height - padding * 2;
            int sliderX = _isOn ? rect.Right - sliderW - padding : rect.Left + padding;
            int sliderY = rect.Top + padding + (_pressed ? 1 : 0);
            var sliderRect = new Rectangle(sliderX, sliderY, sliderW, sliderH);

            using (var sliderPath = CreateRoundRect(sliderRect, 8f))
            using (var sliderBrush = new LinearGradientBrush(sliderRect,
                       Color.FromArgb(60, accent),
                       Color.FromArgb(230, accent),
                       LinearGradientMode.Vertical))
            using (var sliderPen = new Pen(Color.FromArgb(220, accent), 1.6f))
            {
                g.FillPath(sliderBrush, sliderPath);
                g.DrawPath(sliderPen, sliderPath);
            }

            var ledColor = _isOn ? accent : _accentIdle;
            var ledRect = new Rectangle(sliderRect.Right - 18, sliderRect.Top + 6, 10, 10);
            using (var ledBrush = new SolidBrush(Color.FromArgb(220, ledColor)))
            using (var ledGlow = new Pen(Color.FromArgb(120, ledColor), 4f))
            {
                g.DrawEllipse(ledGlow, ledRect);
                g.FillEllipse(ledBrush, ledRect);
            }
        }

        private void DrawText(Graphics g, Rectangle rect, Color accent)
        {
            string status = _isOn ? "启动" : "待机";
            using (var titleFont = new Font("Bahnschrift", 9.5f, FontStyle.Bold))
            using (var statusFont = new Font("Bahnschrift", 12f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(220, 190, 220, 240)))
            using (var statusBrush = new SolidBrush(Color.FromArgb(235, accent)))
            {
                var titlePos = new Point(rect.Left + 10, rect.Top + 6);
                var statusPos = new Point(rect.Left + 10, rect.Top + rect.Height / 2 - 2);
                g.DrawString("ROV", titleFont, titleBrush, titlePos);
                g.DrawString(status, statusFont, statusBrush, statusPos);
            }
        }

        private void DrawDecor(Graphics g, Rectangle rect, Color accent)
        {
            using (var pen = new Pen(Color.FromArgb(120, accent), 1f))
            {
                int y1 = rect.Bottom - 6;
                g.DrawLine(pen, rect.Left + 8, y1, rect.Left + 28, y1);
                g.DrawLine(pen, rect.Right - 28, y1, rect.Right - 8, y1);
            }
        }

        private GraphicsPath CreateRoundRect(Rectangle rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
