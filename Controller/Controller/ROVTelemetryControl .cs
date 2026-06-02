using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace Controller
{
    public partial class ROVTelemetryControl : UserControl
    {
        // 数据存储 - 增加时间戳
        private Queue<DataPoint> depthData = new Queue<DataPoint>();
        private Queue<DataPoint> pitchData = new Queue<DataPoint>();
        private Queue<DataPoint> rollData = new Queue<DataPoint>();
        private Queue<DataPoint> speedData = new Queue<DataPoint>();
        private Queue<DataPoint> headingData = new Queue<DataPoint>();

        // 当前数值
        private float currentDepth = 0;
        private float currentPitch = 0;
        private float currentRoll = 0;
        private float currentSpeed = 0;
        private float currentHeading = 0;

        // 时间相关
        private DateTime startTime = DateTime.Now;
        private int maxDataPoints = 100;
        private double timeWindowSeconds = 60; // 显示最近60秒的数据

        // 配置参数
        private Color primaryColor = Color.FromArgb(0, 255, 255);
        private Color secondaryColor = Color.FromArgb(100, 200, 255);
        private Color speedColor = Color.FromArgb(0, 255, 150);
        private Color headingColor = Color.FromArgb(255, 200, 0);

        private Timer borderAnimationTimer;
        private float borderGlowIntensity = 0.5f;
        private bool borderGlowIncreasing = true;

        
        // 构造函数
        public ROVTelemetryControl()
        {
            InitializeComponent();
            this.Size = new Size(600, 400);
            this.BackColor = Color.Transparent;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);

            // 启动边框动画
            borderAnimationTimer = new Timer();
            borderAnimationTimer.Interval = 100; // 10fps
            borderAnimationTimer.Tick += BorderAnimationTimer_Tick;
            borderAnimationTimer.Start();
        }

        private void BorderAnimationTimer_Tick(object sender, EventArgs e)
        {
            if (borderGlowIncreasing)
            {
                borderGlowIntensity += 0.03f;
                if (borderGlowIntensity >= 1.0f)
                {
                    borderGlowIntensity = 1.0f;
                    borderGlowIncreasing = false;
                }
            }
            else
            {
                borderGlowIntensity -= 0.03f;
                if (borderGlowIntensity <= 0.3f)
                {
                    borderGlowIntensity = 0.3f;
                    borderGlowIncreasing = true;
                }
            }
            this.Invalidate(); // 触发重绘
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "ROVTelemetryControl";
            this.ResumeLayout(false);
        }

        // 数据点类 - 包含时间戳
        public class DataPoint
        {
            public DateTime Time { get; set; }
            public float Value { get; set; }

            public DataPoint(DateTime time, float value)
            {
                Time = time;
                Value = value;
            }
        }
        private void DrawDualTimeSeriesChart(Graphics g, Rectangle rect, Queue<DataPoint> data1, Queue<DataPoint> data2,
                                    Color lineColor1, Color lineColor2, float minRange, float maxRange,
                                    string title, string unit)
        {
            // 绘制图表背景
            using (LinearGradientBrush chartBg = new LinearGradientBrush(
                rect,
                Color.FromArgb(20, 35, 50),
                Color.FromArgb(15, 25, 35),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(chartBg, rect);
            }

            // 绘制边框
            using (Pen borderPen = new Pen(Color.FromArgb(80, lineColor1), 2))
            {
                g.DrawRectangle(borderPen, rect);
            }

            // 绘制标题和当前值
            DrawDualChartTitle(g, rect, title,
                             data1.Count > 0 ? data1.Last().Value : 0,
                             data2.Count > 0 ? data2.Last().Value : 0,
                             unit, lineColor1, lineColor2);

            // 为坐标轴留出空间
            Rectangle chartArea = new Rectangle(rect.X + 60, rect.Y + 35, rect.Width - 80, rect.Height - 45);

            // 绘制时间轴和数值轴
            DrawTimeAxis(g, chartArea, lineColor1);
            DrawValueAxis(g, chartArea, lineColor1, minRange, maxRange, unit);

            // ⭐ 关键修改：计算统一的时间范围
            DateTime? sharedStartTime = null;
            DateTime? sharedEndTime = null;

            if (data1.Count > 0 || data2.Count > 0)
            {
                var allTimes = new List<DateTime>();
                if (data1.Count > 0)
                {
                    allTimes.Add(data1.First().Time);
                    allTimes.Add(data1.Last().Time);
                }
                if (data2.Count > 0)
                {
                    allTimes.Add(data2.First().Time);
                    allTimes.Add(data2.Last().Time);
                }

                sharedStartTime = allTimes.Min();
                sharedEndTime = allTimes.Max();
            }

            // 绘制两条数据曲线 - 使用统一的时间范围
            if (data1.Count > 1 && sharedStartTime.HasValue && sharedEndTime.HasValue)
            {
                DrawDataCurveWithTimeRange(g, chartArea, data1, lineColor1, minRange, maxRange,
                                          sharedStartTime.Value, sharedEndTime.Value);
            }

            if (data2.Count > 1 && sharedStartTime.HasValue && sharedEndTime.HasValue)
            {
                DrawDataCurveWithTimeRange(g, chartArea, data2, lineColor2, minRange, maxRange,
                                          sharedStartTime.Value, sharedEndTime.Value);
            }

            // 绘制图例
            DrawLegend(g, new Rectangle(rect.X + rect.Width - 120, rect.Y + 25, 110, 30),
                      lineColor1, lineColor2, "俯仰角", "横滚角");

            // 如果没有数据，显示等待信息
            if (data1.Count == 0 && data2.Count == 0)
            {
                using (Font noDataFont = new Font("微软雅黑", 9, FontStyle.Regular))
                using (Brush noDataBrush = new SolidBrush(Color.FromArgb(100, 150, 150)))
                {
                    string noDataText = "等待姿态数据...";
                    SizeF textSize = g.MeasureString(noDataText, noDataFont);
                    g.DrawString(noDataText, noDataFont, noDataBrush,
                               chartArea.X + (chartArea.Width - textSize.Width) / 2,
                               chartArea.Y + (chartArea.Height - textSize.Height) / 2);
                }
            }
        }
        // ⭐ 新增方法：使用指定时间范围绘制曲线
        private void DrawDataCurveWithTimeRange(Graphics g, Rectangle chartArea, Queue<DataPoint> data,
                                               Color lineColor, float min, float max,
                                               DateTime startTime, DateTime endTime)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 2) return;

            double timeSpan = (endTime - startTime).TotalSeconds;
            if (timeSpan < 1) timeSpan = 1; // 避免除零

            float range = max - min;

            // 转换数据点为屏幕坐标
            List<PointF> points = new List<PointF>();

            foreach (var point in dataArray)
            {
                // 计算X坐标（基于统一的时间范围）
                double timeRatio = (point.Time - startTime).TotalSeconds / timeSpan;
                float x = chartArea.X + (float)(chartArea.Width * timeRatio);

                // 计算Y坐标（数值）
                float clampedValue = Math.Max(min, Math.Min(max, point.Value));
                float valueRatio = (clampedValue - min) / range;
                float y = chartArea.Y + chartArea.Height - (chartArea.Height * valueRatio);

                points.Add(new PointF(x, y));
            }

            if (points.Count > 1)
            {
                // 绘制曲线
                using (Pen curvePen = new Pen(lineColor, 2))
                {
                    g.DrawLines(curvePen, points.ToArray());
                }

                // 绘制填充区域
                if (points.Count > 2)
                {
                    using (Brush fillBrush = new SolidBrush(Color.FromArgb(30, lineColor)))
                    {
                        var fillPoints = new List<PointF>(points);
                        fillPoints.Add(new PointF(points.Last().X, chartArea.Y + chartArea.Height));
                        fillPoints.Add(new PointF(points.First().X, chartArea.Y + chartArea.Height));
                        g.FillPolygon(fillBrush, fillPoints.ToArray());
                    }
                }

                // 高亮最新数据点
                if (points.Count > 0)
                {
                    PointF lastPoint = points.Last();
                    using (SolidBrush pointBrush = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(pointBrush, lastPoint.X - 4, lastPoint.Y - 4, 8, 8);
                    }
                    using (SolidBrush innerBrush = new SolidBrush(lineColor))
                    {
                        g.FillEllipse(innerBrush, lastPoint.X - 3, lastPoint.Y - 3, 6, 6);
                    }
                }
            }
        }
        private void DrawDualChartTitle(Graphics g, Rectangle rect, string title, float value1, float value2,
                                           string unit, Color color1, Color color2)
            {
                // 标题
                using (Font titleFont = new Font("微软雅黑", 9, FontStyle.Bold))
                using (Brush titleBrush = new SolidBrush(Color.FromArgb(200, 230, 255)))
                {
                    g.DrawString(title, titleFont, titleBrush, rect.X + 8, rect.Y + 4);
                }

                // 俯仰角当前值
                using (Font valueFont = new Font("Consolas", 9, FontStyle.Bold))
                using (Brush valueBrush1 = new SolidBrush(color1))
                {
                    string pitchText = $"俯仰: {value1:F1}{unit}";
                    SizeF textSize = g.MeasureString(pitchText, valueFont);
                    g.DrawString(pitchText, valueFont, valueBrush1,
                               rect.X + rect.Width - textSize.Width - 8, rect.Y + 4);
                }

                // 横滚角当前值
                using (Font valueFont = new Font("Consolas", 9, FontStyle.Bold))
                using (Brush valueBrush2 = new SolidBrush(color2))
                {
                    string rollText = $"横滚: {value2:F1}{unit}";
                    SizeF textSize = g.MeasureString(rollText, valueFont);
                    g.DrawString(rollText, valueFont, valueBrush2,
                               rect.X + rect.Width - textSize.Width - 8, rect.Y + 18);
                }
            }

            private void DrawLegend(Graphics g, Rectangle rect, Color color1, Color color2, string label1, string label2)
            {
                using (Font legendFont = new Font("微软雅黑", 7, FontStyle.Regular))
                using (Brush textBrush = new SolidBrush(Color.FromArgb(180, 200, 220)))
                {
                    // 俯仰角图例
                    using (Pen line1Pen = new Pen(color1, 2))
                    {
                        g.DrawLine(line1Pen, rect.X, rect.Y + 5, rect.X + 15, rect.Y + 5);
                    }
                    g.DrawString(label1, legendFont, textBrush, rect.X + 18, rect.Y + 1);

                    // 横滚角图例
                    using (Pen line2Pen = new Pen(color2, 2))
                    {
                        g.DrawLine(line2Pen, rect.X, rect.Y + 18, rect.X + 15, rect.Y + 18);
                    }
                    g.DrawString(label2, legendFont, textBrush, rect.X + 18, rect.Y + 14);
                }
            }

            // 更新ROV数据
            public void UpdateTelemetry(float depth, float pitch, float roll, float speed, float heading)
            {
                currentDepth = depth;
                currentPitch = pitch;
                currentRoll = roll;
                currentSpeed = speed;
                currentHeading = heading;

                DateTime now = DateTime.Now;

                // 添加带时间戳的数据点
                AddTimedDataPoint(depthData, new DataPoint(now, depth));
                AddTimedDataPoint(pitchData, new DataPoint(now, pitch));
                AddTimedDataPoint(rollData, new DataPoint(now, roll));
                AddTimedDataPoint(speedData, new DataPoint(now, speed));
                AddTimedDataPoint(headingData, new DataPoint(now, heading));

                // 调试输出
                if (depthData.Count % 20 == 0)
                {
                    Console.WriteLine($"时间序列数据: 深度={depth:F2}m, 数据点数={depthData.Count}");
                }

                this.Invalidate();
            }

            private void AddTimedDataPoint(Queue<DataPoint> queue, DataPoint point)
            {
                queue.Enqueue(point);

                // 移除过时的数据点
                DateTime cutoffTime = DateTime.Now.AddSeconds(-timeWindowSeconds);
                while (queue.Count > 0 && queue.Peek().Time < cutoffTime)
                {
                    queue.Dequeue();
                }

                // 限制最大数据点数
                while (queue.Count > maxDataPoints)
                {
                    queue.Dequeue();
                }
            }

        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 绘制科技风背景
            DrawTechBackground(g);

            // 计算田字格布局 - 2x2排列
            int margin = 15;
            int headerHeight = 30;
            int gapBetweenCharts = 10; // 图表之间的间距

            // 计算每个图表的宽度和高度
            int chartWidth = (this.Width - margin * 2 - gapBetweenCharts) / 2;
            int chartHeight = (this.Height - headerHeight - margin * 2 - gapBetweenCharts) / 2;

            // 绘制标题
            DrawMainTitle(g, new Rectangle(0, 0, this.Width, headerHeight));

            // 田字格四个位置
            // 左上：深度
            Rectangle depthRect = new Rectangle(
                margin,
                headerHeight + margin,
                chartWidth,
                chartHeight);

            // 右上：姿态
            Rectangle attitudeRect = new Rectangle(
                margin + chartWidth + gapBetweenCharts,
                headerHeight + margin,
                chartWidth,
                chartHeight);

            // 左下：速度
            Rectangle speedRect = new Rectangle(
                margin,
                headerHeight + margin + chartHeight + gapBetweenCharts,
                chartWidth,
                chartHeight);

            // 右下：航向
            Rectangle headingRect = new Rectangle(
                margin + chartWidth + gapBetweenCharts,
                headerHeight + margin + chartHeight + gapBetweenCharts,
                chartWidth,
                chartHeight);

            // 绘制各个时间序列图表
            DrawTimeSeriesChart(g, depthRect, depthData, primaryColor, 0f, 3.0f, "深度 (m)", "m");
            DrawDualTimeSeriesChart(g, attitudeRect, pitchData, rollData, secondaryColor, Color.FromArgb(255, 150, 100), -90f, 90f, "姿态角度 (°)", "°");
            DrawTimeSeriesChart(g, speedRect, speedData, speedColor, -10f, 10f, "前进速度 (m/s)", "m/s");
            DrawTimeSeriesChart(g, headingRect, headingData, headingColor, -180f, 180f, "航向角 (°)", "°");
        }
        private void DrawTechBackground(Graphics g)
        {
            // 科技风深色背景
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(12, 20, 28),
                Color.FromArgb(18, 28, 38),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // 绘制背景网格
            using (Pen gridPen = new Pen(Color.FromArgb(15, primaryColor), 1))
            {
                // 垂直网格线
                for (int x = 0; x < this.Width; x += 50)
                {
                    g.DrawLine(gridPen, x, 0, x, this.Height);
                }
                // 水平网格线
                for (int y = 0; y < this.Height; y += 30)
                {
                    g.DrawLine(gridPen, 0, y, this.Width, y);
                }
            }

            // 绘制赛博风最外层边框
            DrawCyberOuterBorder(g);
        }

        private void DrawCyberOuterBorder(Graphics g)
        {
            Rectangle outerRect = new Rectangle(0, 0, this.Width, this.Height);
            Rectangle innerRect = new Rectangle(3, 3, this.Width - 6, this.Height - 6);

            // 主边框 - 发光青色
            using (Pen outerBorderPen = new Pen(Color.FromArgb(200, 0, 255, 255), 3))
            {
                g.DrawRectangle(outerBorderPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // 内层边框 - 更亮的青色
            using (Pen innerBorderPen = new Pen(Color.FromArgb(150, 100, 255, 255), 1))
            {
                g.DrawRectangle(innerBorderPen, innerRect);
            }

            // 绘制四角的装饰性科技元素
            DrawCornerDecorations(g);

            // 绘制边框发光效果
            DrawBorderGlow(g);
        }

        private void DrawCornerDecorations(Graphics g)
        {
            int cornerSize = 20;
            int borderOffset = 8;

            using (Pen decorPen = new Pen(Color.FromArgb(180, 0, 255, 255), 2))
            {
                // 左上角
                g.DrawLine(decorPen, borderOffset, borderOffset + cornerSize, borderOffset, borderOffset);
                g.DrawLine(decorPen, borderOffset, borderOffset, borderOffset + cornerSize, borderOffset);

                // 右上角
                g.DrawLine(decorPen, this.Width - borderOffset - cornerSize, borderOffset, this.Width - borderOffset, borderOffset);
                g.DrawLine(decorPen, this.Width - borderOffset, borderOffset, this.Width - borderOffset, borderOffset + cornerSize);

                // 左下角
                g.DrawLine(decorPen, borderOffset, this.Height - borderOffset - cornerSize, borderOffset, this.Height - borderOffset);
                g.DrawLine(decorPen, borderOffset, this.Height - borderOffset, borderOffset + cornerSize, this.Height - borderOffset);

                // 右下角
                g.DrawLine(decorPen, this.Width - borderOffset - cornerSize, this.Height - borderOffset, this.Width - borderOffset, this.Height - borderOffset);
                g.DrawLine(decorPen, this.Width - borderOffset, this.Height - borderOffset, this.Width - borderOffset, this.Height - borderOffset - cornerSize);
            }

            // 添加小的科技细节点
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(255, 0, 255, 255)))
            {
                int dotSize = 3;
                // 四角的小点
                g.FillEllipse(dotBrush, borderOffset - 1, borderOffset - 1, dotSize, dotSize);
                g.FillEllipse(dotBrush, this.Width - borderOffset - 2, borderOffset - 1, dotSize, dotSize);
                g.FillEllipse(dotBrush, borderOffset - 1, this.Height - borderOffset - 2, dotSize, dotSize);
                g.FillEllipse(dotBrush, this.Width - borderOffset - 2, this.Height - borderOffset - 2, dotSize, dotSize);
            }
        }

        private void DrawBorderGlow(Graphics g)
        {
            // 动态发光效果
            int baseAlpha = (int)(100 * borderGlowIntensity);

            for (int i = 1; i <= 5; i++)
            {
                int alpha = Math.Max(10, baseAlpha - (i * 15));
                using (Pen glowPen = new Pen(Color.FromArgb(alpha, 0, 255, 255), 1))
                {
                    Rectangle glowRect = new Rectangle(-i, -i, this.Width + (i * 2), this.Height + (i * 2));
                    g.DrawRectangle(glowPen, glowRect);
                }
            }

            // 主边框使用动态亮度
            int mainAlpha = (int)(255 * (0.7f + 0.3f * borderGlowIntensity));
            using (Pen mainBorderPen = new Pen(Color.FromArgb(mainAlpha, 0, 255, 255), 3))
            {
                g.DrawRectangle(mainBorderPen, 1, 1, this.Width - 3, this.Height - 3);
            }
        }

        private void DrawMainTitle(Graphics g, Rectangle rect)
            {
                using (Font titleFont = new Font("微软雅黑", 14, FontStyle.Bold))
                using (Brush titleBrush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(100, 200, 255),
                    LinearGradientMode.Horizontal))
                {
                    string title = "ROV 实时遥测数据 - 时间序列监控";
                    SizeF titleSize = g.MeasureString(title, titleFont);
                    g.DrawString(title, titleFont, titleBrush,
                               (this.Width - titleSize.Width) / 2, 5);
                }

                // 绘制当前时间
                using (Font timeFont = new Font("Consolas", 8, FontStyle.Regular))
                using (Brush timeBrush = new SolidBrush(Color.FromArgb(150, 200, 255)))
                {
                    string timeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    SizeF timeSize = g.MeasureString(timeText, timeFont);
                    g.DrawString(timeText, timeFont, timeBrush,
                               this.Width - timeSize.Width - 12, 10);
                }
            }

            private void DrawTimeSeriesChart(Graphics g, Rectangle rect, Queue<DataPoint> data, Color lineColor,
                                           float minRange, float maxRange, string title, string unit)
            {
                // 绘制图表背景
                using (LinearGradientBrush chartBg = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(20, 35, 50),
                    Color.FromArgb(15, 25, 35),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(chartBg, rect);
                }

                // 绘制边框
                using (Pen borderPen = new Pen(Color.FromArgb(80, lineColor), 2))
                {
                    g.DrawRectangle(borderPen, rect);
                }

                // 绘制标题和当前值
                DrawChartTitle(g, rect, title, data.Count > 0 ? data.Last().Value : 0, unit, lineColor);

                // 为坐标轴留出空间
                Rectangle chartArea = new Rectangle(rect.X + 60, rect.Y + 25, rect.Width - 80, rect.Height - 35);

                // 绘制时间轴和数值轴
                DrawTimeAxis(g, chartArea, lineColor);
                DrawValueAxis(g, chartArea, lineColor, minRange, maxRange, unit);

                // 绘制数据曲线
                if (data.Count > 1)
                {
                    DrawDataCurve(g, chartArea, data, lineColor, minRange, maxRange);
                }
                else if (data.Count == 0)
                {
                    // 显示"等待数据"
                    using (Font noDataFont = new Font("微软雅黑", 9, FontStyle.Regular))
                    using (Brush noDataBrush = new SolidBrush(Color.FromArgb(100, 150, 150)))
                    {
                        string noDataText = "等待数据...";
                        SizeF textSize = g.MeasureString(noDataText, noDataFont);
                        g.DrawString(noDataText, noDataFont, noDataBrush,
                                   chartArea.X + (chartArea.Width - textSize.Width) / 2,
                                   chartArea.Y + (chartArea.Height - textSize.Height) / 2);
                    }
                }
            }

        private void DrawChartTitle(Graphics g, Rectangle rect, string title, float currentValue, string unit, Color color)
        {
            // 标题
            using (Font titleFont = new Font("微软雅黑", 9, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(200, 230, 255)))
            {
                g.DrawString(title, titleFont, titleBrush, rect.X + 8, rect.Y + 4);
            }

            // 当前值 - ✅ 改为F3
            using (Font valueFont = new Font("Consolas", 10, FontStyle.Bold))
            using (Brush valueBrush = new SolidBrush(color))
            {
                string valueText = $"{currentValue:F3} {unit}";  // ✅ 修改这里
                SizeF valueSize = g.MeasureString(valueText, valueFont);
                g.DrawString(valueText, valueFont, valueBrush,
                           rect.X + rect.Width - valueSize.Width - 8, rect.Y + 4);
            }
        }

        private void DrawTimeAxis(Graphics g, Rectangle chartArea, Color lineColor)
        {
            using (Pen axisPen = new Pen(Color.FromArgb(60, lineColor), 1))
            using (Font timeFont = new Font("Consolas", 7, FontStyle.Regular))
            using (Brush timeBrush = new SolidBrush(Color.FromArgb(140, 180, 200)))
            {
                // 绘制X轴
                g.DrawLine(axisPen, chartArea.X, chartArea.Y + chartArea.Height,
                          chartArea.X + chartArea.Width, chartArea.Y + chartArea.Height);

                // 绘制时间刻度 - 显示相对时间
                int timeTickCount = 6;
                for (int i = 0; i < timeTickCount; i++)
                {
                    double ratio = (double)i / (timeTickCount - 1);
                    int x = chartArea.X + (int)(chartArea.Width * ratio);

                    // 刻度线
                    g.DrawLine(axisPen, x, chartArea.Y + chartArea.Height - 3,
                              x, chartArea.Y + chartArea.Height + 3);

                    // 时间标签 - 显示相对秒数
                    int relativeSeconds = (int)(timeWindowSeconds * ratio);
                    string timeLabel = $"-{(int)(timeWindowSeconds - relativeSeconds)}s";
                    SizeF labelSize = g.MeasureString(timeLabel, timeFont);
                    g.DrawString(timeLabel, timeFont, timeBrush,
                               x - labelSize.Width / 2, chartArea.Y + chartArea.Height + 5);
                }
            }
        }

        private void DrawValueAxis(Graphics g, Rectangle chartArea, Color lineColor, float min, float max, string unit)
            {
                using (Pen axisPen = new Pen(Color.FromArgb(60, lineColor), 1))
                using (Font valueFont = new Font("Consolas", 7, FontStyle.Regular))
                using (Brush valueBrush = new SolidBrush(Color.FromArgb(140, 180, 200)))
                {
                    // 绘制Y轴
                    g.DrawLine(axisPen, chartArea.X, chartArea.Y,
                              chartArea.X, chartArea.Y + chartArea.Height);

                    // 绘制数值刻度
                    int valueTickCount = 6;
                    float range = max - min;

                    for (int i = 0; i < valueTickCount; i++)
                    {
                        float ratio = (float)i / (valueTickCount - 1);
                        float value = min + range * ratio;
                        int y = chartArea.Y + chartArea.Height - (int)(chartArea.Height * ratio);

                        // 网格线
                        using (Pen gridPen = new Pen(Color.FromArgb(25, lineColor), 1))
                        {
                            g.DrawLine(gridPen, chartArea.X, y, chartArea.X + chartArea.Width, y);
                        }

                        // 刻度线
                        g.DrawLine(axisPen, chartArea.X - 3, y, chartArea.X + 3, y);

                        // 数值标签
                        string valueLabel = value.ToString("F3");
                        SizeF labelSize = g.MeasureString(valueLabel, valueFont);
                        g.DrawString(valueLabel, valueFont, valueBrush,
                                   chartArea.X - labelSize.Width - 5, y - labelSize.Height / 2);
                    }

                    // 单位标签
                    g.DrawString(unit, valueFont, valueBrush,
                               chartArea.X - 15, chartArea.Y - 15);
                }
            }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                borderAnimationTimer?.Stop();
                borderAnimationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
        private void DrawDataCurve(Graphics g, Rectangle chartArea, Queue<DataPoint> data, Color lineColor, float min, float max)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length < 2) return;

            // 使用实际数据的时间范围，而不是固定的时间窗口
            DateTime startTime = dataArray[0].Time;  // 最早的数据点时间
            DateTime endTime = dataArray[dataArray.Length - 1].Time;  // 最新的数据点时间
            double timeSpan = (endTime - startTime).TotalSeconds;

            // 如果时间跨度太小，使用固定窗口
            if (timeSpan < 1) timeSpan = 1;

            float range = max - min;

            // 转换数据点为屏幕坐标
            List<PointF> points = new List<PointF>();

            foreach (var point in dataArray)
            {
                // 计算X坐标（基于实际数据时间范围）
                double timeRatio = (point.Time - startTime).TotalSeconds / timeSpan;
                float x = chartArea.X + (float)(chartArea.Width * timeRatio);

                // 计算Y坐标（数值）
                float clampedValue = Math.Max(min, Math.Min(max, point.Value));
                float valueRatio = (clampedValue - min) / range;
                float y = chartArea.Y + chartArea.Height - (chartArea.Height * valueRatio);

                points.Add(new PointF(x, y));
            }

            if (points.Count > 1)
            {
                // 绘制曲线
                using (Pen curvePen = new Pen(lineColor, 2))
                {
                    g.DrawLines(curvePen, points.ToArray());
                }

                // 绘制填充区域
                if (points.Count > 2)
                {
                    using (Brush fillBrush = new SolidBrush(Color.FromArgb(30, lineColor)))
                    {
                        var fillPoints = new List<PointF>(points);
                        fillPoints.Add(new PointF(points.Last().X, chartArea.Y + chartArea.Height));
                        fillPoints.Add(new PointF(points.First().X, chartArea.Y + chartArea.Height));
                        g.FillPolygon(fillBrush, fillPoints.ToArray());
                    }
                }

                // 高亮最新数据点
                if (points.Count > 0)
                {
                    PointF lastPoint = points.Last();
                    using (SolidBrush pointBrush = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(pointBrush, lastPoint.X - 4, lastPoint.Y - 4, 8, 8);
                    }
                    using (SolidBrush innerBrush = new SolidBrush(lineColor))
                    {
                        g.FillEllipse(innerBrush, lastPoint.X - 3, lastPoint.Y - 3, 6, 6);
                    }
                }
            }
        }
    }
    }