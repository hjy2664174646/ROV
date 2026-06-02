using Controller.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Controller
{
    /// <summary>
    /// 单波束声呐深度显示控件
    /// </summary>
    public class P30DepthControl : UserControl
    {
        private const int MaxSamples = 600;
        private readonly TimeSpan historyDuration = TimeSpan.FromSeconds(30);
        private readonly object sampleLock = new object();
        private readonly List<DepthData> depthSamples = new List<DepthData>();
        private float latestDepth = 0f;
        private float latestConfidence = 0f;

        private Point mousePosition = Point.Empty;
        private bool isMouseOverGraph = false;

        public struct DepthData
        {
            public float Depth;
            public float Confidence;
            public DateTime Timestamp;

            public DepthData(float depth, float confidence, DateTime timestamp)
            {
                Depth = depth;
                Confidence = confidence;
                Timestamp = timestamp;
            }
        }

        public P30DepthControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            Size = new Size(395, 350);
            BackColor = Color.FromArgb(5, 10, 20);

            MouseMove += OnMouseMoveOverControl;
            MouseLeave += (s, e) => { isMouseOverGraph = false; Invalidate(); };
        }

        public void AddSample(float depth, float confidence)
        {
            lock (sampleLock)
            {
                depthSamples.Add(new DepthData(depth, confidence, DateTime.Now));
                latestDepth = depth;
                latestConfidence = confidence;

                DateTime threshold = DateTime.Now - historyDuration;
                depthSamples.RemoveAll(s => s.Timestamp < threshold);

                if (depthSamples.Count > MaxSamples)
                {
                    depthSamples.RemoveRange(0, depthSamples.Count - MaxSamples);
                }
            }
            Invalidate();
        }

        private void OnMouseMoveOverControl(object sender, MouseEventArgs e)
        {
            mousePosition = e.Location;
            isMouseOverGraph = true;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            DrawOceanBackground(g, bounds);

            List<DepthData> snapshot;
            lock (sampleLock)
            {
                snapshot = new List<DepthData>(depthSamples);
            }

            if (snapshot.Count == 0)
            {
                DrawPlaceholder(g, bounds);
            }
            else
            {
                Rectangle headerArea = new Rectangle(bounds.Left, bounds.Top, bounds.Width, 70);
                Rectangle graphArea = new Rectangle(bounds.Left, bounds.Top + 70,
                                                   bounds.Width, bounds.Height - 70);

                DrawDepthHeader(g, headerArea);
                DrawScrollingDepthProfile(g, graphArea, snapshot);
            }

            // ★★★ 关键修复：在最后绘制边框 ★★★
            DrawControlBorder(g, bounds);
        }

        private void DrawOceanBackground(Graphics g, Rectangle bounds)
        {
            using (var oceanBrush = new LinearGradientBrush(bounds,
                       Color.FromArgb(8, 15, 28),
                       Color.FromArgb(2, 8, 18),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(oceanBrush, bounds);
            }
        }

        private void DrawDepthHeader(Graphics g, Rectangle area)
        {
            using (var panelBrush = new LinearGradientBrush(area,
                       Color.FromArgb(15, 25, 40),
                       Color.FromArgb(8, 15, 28),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(panelBrush, area);
            }

            using (var linePen = new Pen(Color.FromArgb(0, 180, 220), 2))
            {
                g.DrawLine(linePen, area.Left, area.Bottom - 1, area.Right, area.Bottom - 1);
            }

            string depthText = latestDepth > 0 ? $"{latestDepth:F2}" : "--.-";
            using (var depthFont = new Font("Consolas", 36F, FontStyle.Bold))
            using (var depthBrush = new SolidBrush(GetDepthTextColor(latestDepth)))
            {
                var size = g.MeasureString(depthText, depthFont);
                float x = area.Left + 20;
                g.DrawString(depthText, depthFont, depthBrush, x, area.Top + 12);

                using (var unitFont = new Font("Segoe UI", 11F, FontStyle.Bold))
                using (var unitBrush = new SolidBrush(Color.FromArgb(120, 160, 200)))
                {
                    g.DrawString("m", unitFont, unitBrush, x + size.Width - 10, area.Top + 38);
                }
            }

            DrawSignalIndicator(g, area);
        }

        private void DrawSignalIndicator(Graphics g, Rectangle area)
        {
            int barCount = 5;
            int activeBars = (int)(latestConfidence / 20f);
            int startX = area.Right - 100;
            int startY = area.Top + 20;

            using (var font = new Font("Segoe UI", 8F))
            using (var brush = new SolidBrush(Color.FromArgb(150, 180, 200)))
            {
                g.DrawString($"Confidence {latestConfidence:F0}%", font, brush, startX, startY - 2);
            }

            for (int i = 0; i < barCount; i++)
            {
                Color barColor = i < activeBars
                    ? Color.FromArgb(0, 220, 120)
                    : Color.FromArgb(30, 80, 80, 80);

                using (var barBrush = new SolidBrush(barColor))
                {
                    int barHeight = 6 + i * 3;
                    g.FillRectangle(barBrush,
                                   startX + i * 14,
                                   startY + 30 - barHeight,
                                   10, barHeight);
                }
            }
        }

        private void DrawScrollingDepthProfile(Graphics g, Rectangle area, List<DepthData> samples)
        {
            if (samples.Count < 2) return;

            float minDepth = 0f;
            float maxDepth = samples.Max(s => s.Depth);

            float padding = maxDepth * 0.1f;
            if (padding < 0.5f) padding = 0.5f;
            maxDepth = maxDepth + padding;

            float range = maxDepth - minDepth;

            Rectangle chartArea = new Rectangle(area.Left + 10, area.Top + 10,
                                               area.Width - 70, area.Height - 20);

            DrawDepthGrid(g, chartArea);
            DrawDepthScale(g, area, chartArea, minDepth, maxDepth);

            float columnWidth = (float)chartArea.Width / samples.Count;

            for (int i = 0; i < samples.Count; i++)
            {
                float x = chartArea.Right - (samples.Count - i) * columnWidth;
                float depth = samples[i].Depth;

                float normalizedDepth = depth / range;
                float bottomY = chartArea.Bottom - normalizedDepth * chartArea.Height;

                Color bottomColor = GetDepthColor(depth);
                Color deepWaterColor = Color.FromArgb(80, bottomColor);

                float fillHeight = chartArea.Bottom - bottomY;

                if (fillHeight > 0)
                {
                    using (var columnBrush = new LinearGradientBrush(
                               new RectangleF(x, bottomY, columnWidth, fillHeight),
                               deepWaterColor, bottomColor, LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(columnBrush, x, bottomY, columnWidth, fillHeight);
                    }

                    float seabedHeight = 15f;
                    float seabedTop = chartArea.Bottom - seabedHeight;

                    using (var seabedBaseBrush = new SolidBrush(Color.FromArgb(80, 60, 40, 30)))
                    {
                        g.FillRectangle(seabedBaseBrush, x, seabedTop, columnWidth, seabedHeight);
                    }

                    using (var sandBrush = new HatchBrush(
                               HatchStyle.SmallConfetti,
                               Color.FromArgb(100, 80, 60, 40),
                               Color.Transparent))
                    {
                        g.FillRectangle(sandBrush, x, seabedTop, columnWidth, seabedHeight);
                    }

                    using (var gravelBrush = new HatchBrush(
                               HatchStyle.LargeConfetti,
                               Color.FromArgb(60, 100, 80, 60),
                               Color.Transparent))
                    {
                        g.FillRectangle(gravelBrush, x, seabedTop + 5, columnWidth, seabedHeight - 5);
                    }

                    Random rand = new Random(i);
                    if (rand.Next(100) < 20)
                    {
                        float rockX = x + (float)rand.NextDouble() * columnWidth;
                        float rockY = seabedTop + (float)rand.NextDouble() * seabedHeight;
                        float rockSize = 2 + (float)rand.NextDouble() * 3;

                        using (var rockBrush = new SolidBrush(Color.FromArgb(150, 90, 70, 50)))
                        {
                            g.FillEllipse(rockBrush, rockX, rockY, rockSize, rockSize);
                        }
                    }
                }

                // 确保 alpha 值在 [0, 255] 范围内
                Color bottomHighlight = Color.FromArgb(
                    Math.Min(255, Math.Max(0, (int)(samples[i].Confidence * 2.55f))),
                    255, 200, 0
                );


                using (var bottomBrush = new SolidBrush(bottomHighlight))
                {
                    g.FillRectangle(bottomBrush, x, bottomY - 3, columnWidth, 3);
                }
            }

            var bottomPoints = new List<PointF>();
            for (int i = 0; i < samples.Count; i++)
            {
                float x = chartArea.Right - (samples.Count - i) * columnWidth;
                float normalizedDepth = samples[i].Depth / range;
                float y = chartArea.Bottom - normalizedDepth * chartArea.Height;
                bottomPoints.Add(new PointF(x, y));
            }

            if (bottomPoints.Count > 1)
            {
                using (var bottomPen = new Pen(Color.FromArgb(255, 220, 80), 2.5f))
                {
                    g.DrawLines(bottomPen, bottomPoints.ToArray());
                }

                using (var glowPen = new Pen(Color.FromArgb(60, 255, 220, 80), 5))
                {
                    g.DrawLines(glowPen, bottomPoints.ToArray());
                }
            }

            if (bottomPoints.Count > 0)
            {
                var lastPoint = bottomPoints.Last();

                using (var markerBrush = new SolidBrush(Color.FromArgb(255, 80, 80)))
                {
                    g.FillEllipse(markerBrush, lastPoint.X - 6, lastPoint.Y - 6, 12, 12);
                }
                using (var markerPen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(markerPen, lastPoint.X - 6, lastPoint.Y - 6, 12, 12);
                }
            }

            if (isMouseOverGraph && chartArea.Contains(mousePosition))
            {
                int index = (int)((chartArea.Right - mousePosition.X) / columnWidth);

                if (index >= 0 && index < samples.Count)
                {
                    var data = samples[samples.Count - 1 - index];

                    float actualX = chartArea.Right - (index + 1) * columnWidth;
                    float normalizedDepth = data.Depth / range;
                    float actualY = chartArea.Bottom - normalizedDepth * chartArea.Height;

                    using (var crosshairPen = new Pen(Color.FromArgb(150, 255, 255, 255), 1))
                    {
                        crosshairPen.DashStyle = DashStyle.Dash;
                        g.DrawLine(crosshairPen, actualX, chartArea.Top, actualX, chartArea.Bottom);
                        g.DrawLine(crosshairPen, chartArea.Left, actualY, chartArea.Right, actualY);
                    }

                    using (var highlightBrush = new SolidBrush(Color.FromArgb(200, 255, 100, 100)))
                    using (var highlightPen = new Pen(Color.White, 2))
                    {
                        g.FillEllipse(highlightBrush, actualX - 5, actualY - 5, 10, 10);
                        g.DrawEllipse(highlightPen, actualX - 5, actualY - 5, 10, 10);
                    }

                    string label = $"深度: {data.Depth:F2}m\n置信度: {data.Confidence:F0}%";
                    using (var labelFont = new Font("Microsoft YaHei", 9, FontStyle.Bold))
                    using (var labelBrush = new SolidBrush(Color.White))
                    using (var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                    {
                        var labelSize = g.MeasureString(label, labelFont);

                        float labelX = actualX + 10;
                        float labelY = actualY - labelSize.Height - 10;

                        if (labelX + labelSize.Width > chartArea.Right)
                            labelX = actualX - labelSize.Width - 10;

                        if (labelY < chartArea.Top)
                            labelY = actualY + 10;

                        var labelRect = new RectangleF(labelX, labelY, labelSize.Width + 8, labelSize.Height + 4);

                        g.FillRoundedRectangle(bgBrush, labelRect, 5);
                        g.DrawString(label, labelFont, labelBrush, labelX + 4, labelY + 2);
                    }
                }
            }
        }

        private void DrawDepthScale(Graphics g, Rectangle area, Rectangle chartArea, float minDepth, float maxDepth)
        {
            using (var font = new Font("Consolas", 9F, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(150, 180, 200)))
            {
                for (int i = 0; i <= 5; i++)
                {
                    float depth = maxDepth * i / 5f;
                    float normalizedDepth = depth / maxDepth;
                    float y = chartArea.Bottom - normalizedDepth * chartArea.Height;

                    string label = $"{depth:F1}m";
                    var size = g.MeasureString(label, font);

                    g.DrawString(label, font, brush, chartArea.Right + 5, y - size.Height / 2);

                    using (var tickPen = new Pen(Color.FromArgb(80, 255, 255, 255)))
                    {
                        g.DrawLine(tickPen, chartArea.Right, y, chartArea.Right + 5, y);
                    }
                }
            }
        }

        private void DrawDepthGrid(Graphics g, Rectangle area)
        {
            using (var gridPen = new Pen(Color.FromArgb(20, 255, 255, 255)))
            {
                gridPen.DashStyle = DashStyle.Dot;

                for (int i = 0; i <= 5; i++)
                {
                    float y = area.Top + area.Height * i / 5f;
                    g.DrawLine(gridPen, area.Left, y, area.Right, y);
                }

                for (int i = 0; i <= 6; i++)
                {
                    float x = area.Left + area.Width * i / 6f;
                    g.DrawLine(gridPen, x, area.Top, x, area.Bottom);
                }
            }
        }

        private void DrawPlaceholder(Graphics g, Rectangle bounds)
        {
            using (var font = new Font("Segoe UI", 12F, FontStyle.Italic))
            using (var brush = new SolidBrush(Color.FromArgb(100, 150, 180)))
            {
                string text = "🌊 Waiting for sonar data...";
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush,
                    (bounds.Width - size.Width) / 2,
                    (bounds.Height - size.Height) / 2);
            }
        }

        /// <summary>
        /// 圆角科幻边框（带装饰角）
        /// </summary>
        private void DrawControlBorder(Graphics g, Rectangle bounds)
        {
            int radius = 8;
            int inset = 2;

            Rectangle borderRect = new Rectangle(
                bounds.Left + inset,
                bounds.Top + inset,
                bounds.Width - inset * 2 - 1,
                bounds.Height - inset * 2 - 1
            );

            using (var borderPath = GraphicsExtensions.GetRoundedRect(
                new RectangleF(borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height),
                radius))
            {
                using (var glowPen = new Pen(Color.FromArgb(60, 0, 180, 220), 6))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawPath(glowPen, borderPath);
                }

                using (var borderPen = new Pen(Color.FromArgb(0, 180, 220), 2))
                {
                    borderPen.LineJoin = LineJoin.Round;
                    g.DrawPath(borderPen, borderPath);
                }
            }

            DrawCornerAccents(g, borderRect);
        }

        private void DrawCornerAccents(Graphics g, Rectangle rect)
        {
            int length = 20;

            using (var accentPen = new Pen(Color.FromArgb(255, 200, 0), 2))
            {
                // 左上角
                g.DrawLine(accentPen, rect.Left, rect.Top + length, rect.Left, rect.Top);
                g.DrawLine(accentPen, rect.Left, rect.Top, rect.Left + length, rect.Top);

                // 右上角
                g.DrawLine(accentPen, rect.Right - length, rect.Top, rect.Right, rect.Top);
                g.DrawLine(accentPen, rect.Right, rect.Top, rect.Right, rect.Top + length);

                // 右下角
                g.DrawLine(accentPen, rect.Right, rect.Bottom - length, rect.Right, rect.Bottom);
                g.DrawLine(accentPen, rect.Right, rect.Bottom, rect.Right - length, rect.Bottom);

                // 左下角
                g.DrawLine(accentPen, rect.Left + length, rect.Bottom, rect.Left, rect.Bottom);
                g.DrawLine(accentPen, rect.Left, rect.Bottom, rect.Left, rect.Bottom - length);
            }
        }

        private Color GetDepthTextColor(float depth)
        {
            if (depth < 5f) return Color.FromArgb(0, 255, 180);
            if (depth < 15f) return Color.FromArgb(0, 220, 255);
            if (depth < 30f) return Color.FromArgb(255, 200, 0);
            return Color.FromArgb(255, 100, 100);
        }

        private Color GetDepthColor(float depth)
        {
            if (depth < 5f) return Color.FromArgb(0, 180, 120);
            if (depth < 15f) return Color.FromArgb(0, 150, 200);
            if (depth < 30f) return Color.FromArgb(180, 140, 0);
            return Color.FromArgb(200, 80, 60);
        }
    }

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (var path = GetRoundedRect(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
