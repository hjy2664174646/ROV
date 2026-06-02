using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Controller
{
    public partial class GPSTrajectoryControl : UserControl
    {
        private List<GPSPoint> trajectoryPoints = new List<GPSPoint>();
        private Timer animationTimer;
        private int animationFrame = 0;

        // ✅ 1小时数据容量计算：
        // 采样率500ms = 2点/秒
        // 1小时 = 3600秒 × 2点/秒 = 7200点
        // 设置为8000提供安全余量
        private const int maxPoints = 8000;

        // 赛博风格配色
        private Color neonCyan = Color.FromArgb(0, 255, 255);
        private Color neonGreen = Color.FromArgb(0, 255, 128);
        private Color neonPurple = Color.FromArgb(255, 0, 255);
        private Color darkBg = Color.FromArgb(10, 15, 25);
        private Color gridColor = Color.FromArgb(40, 0, 100, 150);

        // 轨迹范围和精度
        private double minLon = double.MaxValue;
        private double maxLon = double.MinValue;
        private double minLat = double.MaxValue;
        private double maxLat = double.MinValue;
        private int displayPrecision = 6;

        // 当前位置
        private GPSPoint currentPosition;

        // ✅ 性能优化：缓存转换后的像素点
        private List<PointF> cachedPixelPoints = new List<PointF>();
        private bool needsRecalculation = true;
        private Rectangle lastPlotArea;

        // ✅ 记录开始时间用于显示录制时长
        private DateTime? startTime = null;

        public GPSTrajectoryControl()
        {
            InitializeComponent();
            SetupControl();
            StartAnimation();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "GPSTrajectoryControl";
            this.Size = new Size(600, 400);
            this.ResumeLayout(false);
        }

        private void SetupControl()
        {
            this.BackColor = darkBg;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
        }

        private void StartAnimation()
        {
            animationTimer = new Timer();
            animationTimer.Interval = 50; // 20 FPS
            animationTimer.Tick += (s, e) => {
                animationFrame = (animationFrame + 1) % 360;
                // 不自动刷新，节省性能
            };
            animationTimer.Start();
        }

        // 添加GPS轨迹点
        public void AddTrajectoryPoint(double longitude, double latitude, DateTime timestamp)
        {
            var point = new GPSPoint
            {
                Longitude = longitude,
                Latitude = latitude,
                Timestamp = timestamp
            };

            // 记录开始时间
            if (startTime == null)
            {
                startTime = timestamp;
            }

            trajectoryPoints.Add(point);
            currentPosition = point;

            // ✅ 达到maxPoints时移除最旧的点（滚动窗口，始终显示最近1小时）
            if (trajectoryPoints.Count > maxPoints)
            {
                trajectoryPoints.RemoveAt(0);
                needsRecalculation = true;
            }

            // 更新范围
            UpdateBounds();

            // 计算显示精度
            CalculateDisplayPrecision();

            // 标记需要重新计算像素坐标
            needsRecalculation = true;

            // 仅在添加新点时刷新界面
            this.Invalidate();
        }

        private new void UpdateBounds()
        {
            if (trajectoryPoints.Count == 0) return;

            minLon = trajectoryPoints.Min(p => p.Longitude);
            maxLon = trajectoryPoints.Max(p => p.Longitude);
            minLat = trajectoryPoints.Min(p => p.Latitude);
            maxLat = trajectoryPoints.Max(p => p.Latitude);
        }

        private void CalculateDisplayPrecision()
        {
            if (trajectoryPoints.Count < 2) return;

            double lonRange = maxLon - minLon;
            double latRange = maxLat - minLat;
            double maxRange = Math.Max(lonRange, latRange);

            if (maxRange > 1.0)
                displayPrecision = 2;
            else if (maxRange > 0.1)
                displayPrecision = 3;
            else if (maxRange > 0.01)
                displayPrecision = 4;
            else if (maxRange > 0.001)
                displayPrecision = 5;
            else if (maxRange > 0.0001)
                displayPrecision = 6;
            else
                displayPrecision = 7;
        }

        // ✅ 预计算所有点的像素坐标（性能优化）
        private void RecalculatePixelPoints(Rectangle plotArea)
        {
            if (!needsRecalculation && plotArea == lastPlotArea &&
                cachedPixelPoints.Count == trajectoryPoints.Count)
            {
                return; // 无需重新计算
            }

            cachedPixelPoints.Clear();

            if (maxLon == minLon || maxLat == minLat)
            {
                PointF center = new PointF(plotArea.X + plotArea.Width / 2, plotArea.Y + plotArea.Height / 2);
                for (int i = 0; i < trajectoryPoints.Count; i++)
                {
                    cachedPixelPoints.Add(center);
                }
            }
            else
            {
                foreach (var gpsPoint in trajectoryPoints)
                {
                    double normalizedX = (gpsPoint.Longitude - minLon) / (maxLon - minLon);
                    double normalizedY = (gpsPoint.Latitude - minLat) / (maxLat - minLat);

                    float x = plotArea.Left + (float)(normalizedX * plotArea.Width);
                    float y = plotArea.Bottom - (float)(normalizedY * plotArea.Height);

                    cachedPixelPoints.Add(new PointF(x, y));
                }
            }

            needsRecalculation = false;
            lastPlotArea = plotArea;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawCyberBackground(g);
            DrawGrid(g);
            DrawCoordinateLabelsOptimized(g);
            DrawTrajectoryAllPoints(g);
            DrawCurrentPosition(g);
            DrawHUDInfo(g);
        }

        private void DrawCyberBackground(Graphics g)
        {
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(5, 10, 20),
                Color.FromArgb(15, 25, 35),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            using (Pen glowPen = new Pen(Color.FromArgb(80, neonCyan), 3))
            {
                g.DrawRectangle(glowPen, 2, 2, this.Width - 5, this.Height - 5);
            }

            using (Pen borderPen = new Pen(neonCyan, 1))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            DrawCornerDecorations(g);
        }

        private void DrawCornerDecorations(Graphics g)
        {
            using (Pen decorPen = new Pen(neonGreen, 2))
            {
                int cornerSize = 20;

                g.DrawLine(decorPen, 10, 10, 10 + cornerSize, 10);
                g.DrawLine(decorPen, 10, 10, 10, 10 + cornerSize);

                g.DrawLine(decorPen, this.Width - 10 - cornerSize, 10, this.Width - 10, 10);
                g.DrawLine(decorPen, this.Width - 10, 10, this.Width - 10, 10 + cornerSize);

                g.DrawLine(decorPen, 10, this.Height - 10, 10 + cornerSize, this.Height - 10);
                g.DrawLine(decorPen, 10, this.Height - 10 - cornerSize, 10, this.Height - 10);

                g.DrawLine(decorPen, this.Width - 10 - cornerSize, this.Height - 10, this.Width - 10, this.Height - 10);
                g.DrawLine(decorPen, this.Width - 10, this.Height - 10 - cornerSize, this.Width - 10, this.Height - 10);
            }
        }

        private void DrawGrid(Graphics g)
        {
            if (trajectoryPoints.Count < 2) return;

            Rectangle plotArea = GetPlotArea();

            using (Pen gridPen = new Pen(Color.FromArgb(30, neonCyan), 1))
            {
                gridPen.DashStyle = DashStyle.Dot;

                int verticalLines = Math.Max(4, plotArea.Width / 100);
                int horizontalLines = Math.Min(8, plotArea.Height / 40);

                for (int i = 0; i <= verticalLines; i++)
                {
                    int x = plotArea.Left + i * plotArea.Width / verticalLines;
                    g.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
                }

                for (int i = 0; i <= horizontalLines; i++)
                {
                    int y = plotArea.Top + i * plotArea.Height / horizontalLines;
                    g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                }
            }
        }

        private int CalculateOptimalLabelCount(int availableSpace, int minSpacing = 80)
        {
            int maxLabels = availableSpace / minSpacing;
            int[] preferredCounts = { 2, 3, 4, 5, 6, 8, 10 };

            for (int i = preferredCounts.Length - 1; i >= 0; i--)
            {
                if (preferredCounts[i] <= maxLabels)
                {
                    return preferredCounts[i];
                }
            }

            return Math.Max(2, maxLabels);
        }

        private void DrawCoordinateLabelsOptimized(Graphics g)
        {
            if (trajectoryPoints.Count < 2) return;

            Rectangle plotArea = GetPlotArea();
            Font labelFont = new Font("Consolas", 8, FontStyle.Regular);

            using (Brush labelBrush = new SolidBrush(Color.FromArgb(180, neonCyan)))
            {
                int lonLabelCount = CalculateOptimalLabelCount(plotArea.Width, 100);
                int latLabelCount = CalculateOptimalLabelCount(plotArea.Height, 50);

                for (int i = 0; i <= lonLabelCount; i++)
                {
                    double lon = minLon + (maxLon - minLon) * i / (double)lonLabelCount;
                    int precision = displayPrecision;
                    if (lonLabelCount > 6) precision = Math.Max(2, precision - 1);
                    if (lonLabelCount > 8) precision = Math.Max(2, precision - 2);

                    string lonText = lon.ToString($"F{precision}");
                    int x = plotArea.Left + i * plotArea.Width / lonLabelCount;

                    GraphicsState state = g.Save();
                    g.TranslateTransform(x, plotArea.Bottom + 15);
                    g.RotateTransform(18);
                    g.DrawString(lonText, labelFont, labelBrush, 0, 0);
                    g.Restore(state);
                }

                for (int i = 0; i <= latLabelCount; i++)
                {
                    double lat = minLat + (maxLat - minLat) * (latLabelCount - i) / (double)latLabelCount;
                    int precision = displayPrecision;
                    if (latLabelCount > 6) precision = Math.Max(2, precision - 1);

                    string latText = lat.ToString($"F{precision}");
                    int y = plotArea.Top + i * plotArea.Height / latLabelCount;
                    SizeF textSize = g.MeasureString(latText, labelFont);

                    g.DrawString(latText, labelFont, labelBrush,
                               plotArea.Left - textSize.Width - 5, y - textSize.Height / 2);
                }
            }

            labelFont.Dispose();
        }

        // ✅ 绘制所有点 - 针对8000点优化
        private void DrawTrajectoryAllPoints(Graphics g)
        {
            if (trajectoryPoints.Count < 2) return;

            Rectangle plotArea = GetPlotArea();
            RecalculatePixelPoints(plotArea);

            if (cachedPixelPoints.Count < 2) return;

            // ✅ 使用GraphicsPath批量绘制所有点
            using (GraphicsPath trajectoryPath = new GraphicsPath())
            {
                for (int i = 1; i < cachedPixelPoints.Count; i++)
                {
                    trajectoryPath.AddLine(cachedPixelPoints[i - 1], cachedPixelPoints[i]);
                }

                // 一次性绘制整条轨迹线
                using (Pen trajectoryPen = new Pen(neonGreen, 2))
                {
                    g.DrawPath(trajectoryPen, trajectoryPath);
                }
            }

            // ✅ 渐变色效果（分段显示新旧程度）
            int segmentCount = 10; // 分10段显示渐变
            int pointsPerSegment = cachedPixelPoints.Count / segmentCount;

            for (int seg = 0; seg < segmentCount; seg++)
            {
                int startIdx = seg * pointsPerSegment;
                int endIdx = (seg == segmentCount - 1) ? cachedPixelPoints.Count - 1 : (seg + 1) * pointsPerSegment;

                float alpha = (float)(seg + 1) / segmentCount;
                Color segmentColor = Color.FromArgb((int)(100 + 155 * alpha), neonGreen);

                using (Pen segmentPen = new Pen(segmentColor, 2))
                {
                    for (int i = startIdx + 1; i <= endIdx && i < cachedPixelPoints.Count; i++)
                    {
                        g.DrawLine(segmentPen, cachedPixelPoints[i - 1], cachedPixelPoints[i]);
                    }
                }
            }

            // ✅ 轨迹点标记（适度抽稀）
            int pointInterval = Math.Max(40, cachedPixelPoints.Count / 150);
            for (int i = 0; i < cachedPixelPoints.Count; i += pointInterval)
            {
                PointF p = cachedPixelPoints[i];
                float alpha = (float)i / cachedPixelPoints.Count;
                Color pointColor = Color.FromArgb((int)(50 + 205 * alpha), neonCyan);

                using (Brush pointBrush = new SolidBrush(pointColor))
                {
                    g.FillEllipse(pointBrush, p.X - 2, p.Y - 2, 4, 4);
                }
            }
        }

        private void DrawCurrentPosition(Graphics g)
        {
            if (currentPosition == null || cachedPixelPoints.Count == 0) return;

            PointF currentPoint = cachedPixelPoints[cachedPixelPoints.Count - 1];

            float pulseSize = 8 + 4 * (float)Math.Sin(animationFrame * Math.PI / 30);

            using (Brush glowBrush = new SolidBrush(Color.FromArgb(100, neonPurple)))
            {
                g.FillEllipse(glowBrush,
                    currentPoint.X - pulseSize,
                    currentPoint.Y - pulseSize,
                    pulseSize * 2,
                    pulseSize * 2);
            }

            using (Brush coreBrush = new SolidBrush(neonPurple))
            {
                g.FillEllipse(coreBrush, currentPoint.X - 4, currentPoint.Y - 4, 8, 8);
            }

            using (Pen crossPen = new Pen(Color.White, 2))
            {
                g.DrawLine(crossPen, currentPoint.X - 12, currentPoint.Y, currentPoint.X + 12, currentPoint.Y);
                g.DrawLine(crossPen, currentPoint.X, currentPoint.Y - 12, currentPoint.X, currentPoint.Y + 12);
            }
        }

        private void DrawHUDInfo(Graphics g)
        {
            Font hudFont = new Font("Consolas", 9, FontStyle.Bold);
            Font titleFont = new Font("Consolas", 11, FontStyle.Bold);

            using (Brush titleBrush = new SolidBrush(neonCyan))
            {
                string title = ">>> GPS TRAJECTORY <<<";

                using (Brush glowBrush = new SolidBrush(Color.FromArgb(50, neonCyan)))
                {
                    for (int offset = 1; offset <= 3; offset++)
                    {
                        g.DrawString(title, titleFont, glowBrush, 15 - offset, 15 - offset);
                        g.DrawString(title, titleFont, glowBrush, 15 + offset, 15 + offset);
                    }
                }

                g.DrawString(title, titleFont, titleBrush, 15, 15);
            }

            // 信息面板
            Rectangle infoPanel = new Rectangle(this.Width - 180, 40, 170, 130);

            using (Brush panelBrush = new SolidBrush(Color.FromArgb(80, 0, 20, 40)))
            {
                g.FillRectangle(panelBrush, infoPanel);
            }

            using (Pen panelPen = new Pen(neonCyan, 1))
            {
                g.DrawRectangle(panelPen, infoPanel);
            }

            // ✅ 计算录制时长
            string duration = "00:00:00";
            if (startTime.HasValue && currentPosition != null)
            {
                TimeSpan elapsed = currentPosition.Timestamp - startTime.Value;
                duration = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }

            // ✅ 计算容量百分比
            float capacityPercent = (trajectoryPoints.Count / (float)maxPoints) * 100f;

            using (Brush infoBrush = new SolidBrush(Color.FromArgb(200, neonGreen)))
            {
                string[] infoLines = {
                    $"POINTS: {trajectoryPoints.Count:D4}/{maxPoints}",
                    $"CAPACITY: {capacityPercent:F1}%",
                    $"DURATION: {duration}",
                    $"RENDER: ALL POINTS",
                    currentPosition != null ? $"LON: {currentPosition.Longitude.ToString($"F{displayPrecision}")}" : "LON: ---",
                    currentPosition != null ? $"LAT: {currentPosition.Latitude.ToString($"F{displayPrecision}")}" : "LAT: ---",
                    $"RANGE: {(maxLon - minLon).ToString($"F{Math.Max(4, displayPrecision)}")}"
                };

                for (int i = 0; i < infoLines.Length; i++)
                {
                    g.DrawString(infoLines[i], hudFont, infoBrush,
                               infoPanel.X + 10, infoPanel.Y + 10 + i * 16);
                }
            }

            hudFont.Dispose();
            titleFont.Dispose();
        }

        private Rectangle GetPlotArea()
        {
            return new Rectangle(80, 50, this.Width - 200, this.Height - 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer?.Stop();
                animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class GPSPoint
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public DateTime Timestamp { get; set; }
    }
}