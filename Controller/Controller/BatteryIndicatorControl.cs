using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    /// <summary>
    /// 赛博风电池电量控件：根据 6S 电压计算剩余电量百分比并绘制电池图标 + 斜体数字。
    /// </summary>
    public class BatteryIndicatorControl : UserControl
    {
        private float _voltage;
        private float _percent;
        private readonly int _cellCount;

        private readonly Color _bgDark = Color.FromArgb(16, 20, 30);
        private readonly Color _bgLight = Color.FromArgb(26, 36, 52);
        private readonly Color _frameColor = Color.FromArgb(0, 255, 220);
        private readonly Color _shadow = Color.FromArgb(80, 0, 255, 190);

        public BatteryIndicatorControl(int cellCount = 6)
        {
            _cellCount = cellCount;
            Size = new Size(180, 48);
            MinimumSize = new Size(150, 40);
            MaximumSize = new Size(240, 60);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        /// <summary>
        /// 更新电压值（单位：V，整包电压）。
        /// </summary>
        public void UpdateVoltage(float voltage)
        {
            _voltage = voltage;
            _percent = VoltageToPercent(voltage);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bodyRect = new Rectangle(6, 8, 88, Height - 16);
            var tipRect = new Rectangle(bodyRect.Right, bodyRect.Top + bodyRect.Height / 4, 10, bodyRect.Height / 2);
            var textRect = new Rectangle(bodyRect.Right + 14, bodyRect.Top - 2, Width - (bodyRect.Right + 18), bodyRect.Height + 4);

            DrawBatteryBody(g, bodyRect, tipRect);
            DrawFill(g, bodyRect);
            DrawText(g, textRect);
            DrawDecor(g);
        }

        private void DrawBatteryBody(Graphics g, Rectangle bodyRect, Rectangle tipRect)
        {
            using (var bodyPath = CreateRoundRect(bodyRect, 8f))
            using (var bodyBrush = new LinearGradientBrush(bodyRect, _bgDark, _bgLight, 90f))
            using (var framePen = new Pen(Color.FromArgb(180, _frameColor), 2f))
            using (var glowPen = new Pen(_shadow, 6f))
            using (var tipBrush = new SolidBrush(Color.FromArgb(50, _frameColor)))
            using (var tipPen = new Pen(Color.FromArgb(200, _frameColor), 2f))
            {
                g.DrawPath(glowPen, bodyPath);
                g.FillPath(bodyBrush, bodyPath);
                g.DrawPath(framePen, bodyPath);

                g.FillRectangle(tipBrush, tipRect);
                g.DrawRectangle(tipPen, tipRect);
            }
        }

        private void DrawFill(Graphics g, Rectangle bodyRect)
        {
            var innerRect = Rectangle.Inflate(bodyRect, -6, -6);
            float pct = Math.Max(0f, Math.Min(1f, _percent / 100f));
            float fillWidth = innerRect.Width * pct;
            if (fillWidth < 2f) return;

            var fillRect = new RectangleF(innerRect.Left, innerRect.Top, fillWidth, innerRect.Height);
            var levelColor = GetLevelColor(pct);

            using (var fillBrush = new LinearGradientBrush(fillRect,
                       Color.FromArgb(50, levelColor),
                       Color.FromArgb(200, levelColor),
                       LinearGradientMode.Horizontal))
            using (var hatch = new HatchBrush(HatchStyle.ForwardDiagonal, Color.FromArgb(60, 255, 255, 255), Color.Transparent))
            using (var pen = new Pen(Color.FromArgb(220, levelColor), 1.6f))
            {
                g.FillRectangle(fillBrush, fillRect);
                g.FillRectangle(hatch, fillRect);
                g.DrawRectangle(pen, Rectangle.Round(fillRect));
            }
        }

        private void DrawText(Graphics g, Rectangle textRect)
        {
            string percentText = $"{_percent:0}%";
            string voltageText = $"{_voltage:F1}V";
            var levelColor = GetLevelColor(Math.Max(0f, Math.Min(1f, _percent / 100f)));

            using (var percentFont = new Font("Consolas", 15f, FontStyle.Italic | FontStyle.Bold))
            using (var voltageFont = new Font("Consolas", 10f, FontStyle.Regular))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            using (var textBrush = new SolidBrush(Color.FromArgb(230, levelColor)))
            using (var subTextBrush = new SolidBrush(Color.FromArgb(200, 180, 220, 240)))
            {
                var shadowRect = new Rectangle(textRect.X + 1, textRect.Y + 1, textRect.Width, textRect.Height);
                TextRenderer.DrawText(g, percentText, percentFont, shadowRect, Color.FromArgb(70, 0, 0, 0), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                TextRenderer.DrawText(g, percentText, percentFont, textRect, Color.FromArgb(levelColor.A, levelColor), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                var voltagePoint = new Point(textRect.X - 4, textRect.Y + textRect.Height / 2 + 4);
                TextRenderer.DrawText(g, voltageText, voltageFont, voltagePoint, subTextBrush.Color, TextFormatFlags.Left);
            }
        }

        private void DrawDecor(Graphics g)
        {
            int y1 = Height / 2;
            using (var pen = new Pen(Color.FromArgb(100, _frameColor), 1f))
            {
                int baseX = Width - 68; // 向左收一点，让装饰线更贴近数字
                g.DrawLine(pen, baseX, y1 - 8, baseX + 23, y1 - 8);
                g.DrawLine(pen, baseX, y1 + 8, baseX + 19, y1 + 8);
            }
        }

        private float VoltageToPercent(float packVoltage)
        {
            if (_cellCount <= 0) return 0f;

            const float emptyCell = 3.6f; // 6S 航模电池常见安全下限（每节）
            const float fullCell = 4.2f;  // 满充

            float perCell = packVoltage / _cellCount;
            float t = (perCell - emptyCell) / (fullCell - emptyCell);
            t = Math.Max(0f, Math.Min(1f, t));

            // 略微偏向中高段，避免过早显示满电
            t = (float)Math.Pow(t, 0.9f);
            return t * 100f;
        }

        private Color GetLevelColor(float pct)
        {
            pct = Math.Max(0f, Math.Min(1f, pct));
            if (pct < 0.25f)
            {
                return Lerp(Color.FromArgb(255, 70, 50), Color.FromArgb(255, 140, 0), pct / 0.25f);
            }
            if (pct < 0.6f)
            {
                float t = (pct - 0.25f) / 0.35f;
                return Lerp(Color.FromArgb(255, 140, 0), Color.FromArgb(0, 255, 180), t);
            }
            return Lerp(Color.FromArgb(0, 255, 180), Color.FromArgb(0, 210, 255), (pct - 0.6f) / 0.4f);
        }

        private Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            byte r = (byte)(a.R + (b.R - a.R) * t);
            byte g = (byte)(a.G + (b.G - a.G) * t);
            byte bl = (byte)(a.B + (b.B - a.B) * t);
            byte al = (byte)(a.A + (b.A - a.A) * t);
            return Color.FromArgb(al, r, g, bl);
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
