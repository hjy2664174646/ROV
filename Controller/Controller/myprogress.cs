using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    public partial class ThrusterDisplayControl : UserControl
    {
        private int[] thrusterValues = new int[6]; // 6个推进器的值 (-100 to 100)
        private string[] thrusterLabels = { "F", "M", "R", "F", "M", "R" };

        // 海洋风格的深蓝色调配色
        private Color[] thrusterBaseColors = {
            Color.FromArgb(70, 130, 180),   // 钢蓝色
            Color.FromArgb(95, 158, 160),   // 深青色
            Color.FromArgb(72, 118, 155),   // 深天蓝色
            Color.FromArgb(100, 149, 237),  // 矢车菊蓝
            Color.FromArgb(65, 105, 225),   // 皇家蓝
            Color.FromArgb(123, 104, 238)   // 中蓝紫色
        };

        public ThrusterDisplayControl()
        {
            InitializeComponent();
            this.Size = new Size(460, 145);
            this.BackColor = Color.Transparent;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "ThrusterDisplayControl";
            this.ResumeLayout(false);
        }

        public void UpdateThrusterValues(int[] values)
        {
            if (values != null && values.Length == 6)
            {
                Array.Copy(values, thrusterValues, 6);
                this.Invalidate();
            }
        }

        public void SetThrusterValue(int index, int value)
        {
            if (index >= 0 && index < 6)
            {
                thrusterValues[index] = Math.Max(-100, Math.Min(100, value));
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawROVSchematic(g);
            DrawTwoRowHorizontalThrusterBars(g);
        }

        private void DrawROVSchematic(Graphics g)
        {
            // 保存原始图形状态
            GraphicsState originalState = g.Save();

            // ROV主体参数 - 加长机身
            int rovWidth = 30;
            int rovHeight = 65; // 从50增加到65
            int rovCenterX = 80; // ROV中心点X坐标
            int rovCenterY = 65; // ROV中心点Y坐标

            // 设置旋转中心点并旋转45度
            //g.TranslateTransform(rovCenterX, rovCenterY);
            //g.RotateTransform(45);
            //g.TranslateTransform(-rovCenterX, -rovCenterY);

            // 计算ROV矩形位置（以中心点为基准）
            Rectangle rovBody = new Rectangle(
                rovCenterX - rovWidth / 2,
                rovCenterY - rovHeight / 2,
                rovWidth,
                rovHeight
            );

            // 头部参数
            int headDiameter = rovWidth;
            int headRadius = headDiameter / 2;
            int headX = rovBody.X;
            int headY = rovBody.Y - headRadius;

            Rectangle headRect = new Rectangle(headX, headY, headDiameter, headDiameter);

            // 1. 填充头部半圆
            using (LinearGradientBrush headBrush = new LinearGradientBrush(
                headRect,
                Color.FromArgb(100, 159, 190),
                Color.FromArgb(70, 129, 160),
                LinearGradientMode.Vertical))
            {
                g.FillPie(headBrush, headRect, 180, 180);
            }

            // 2. 填充主体矩形
            using (LinearGradientBrush rovBrush = new LinearGradientBrush(
                rovBody,
                Color.FromArgb(90, 149, 180),
                Color.FromArgb(60, 119, 150),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(rovBrush, rovBody);
            }

            // 3. 绘制边框
            using (Pen borderPen = new Pen(Color.FromArgb(200, 230, 255), 2))
            {
                // 头部圆弧
                g.DrawArc(borderPen, headRect, 180, 180);

                // 主体三边
                g.DrawLine(borderPen, rovBody.Left, rovBody.Top, rovBody.Left, rovBody.Bottom);
                g.DrawLine(borderPen, rovBody.Right, rovBody.Top, rovBody.Right, rovBody.Bottom);
                g.DrawLine(borderPen, rovBody.Left, rovBody.Bottom, rovBody.Right, rovBody.Bottom);
            }

            // 4. ROV文字（也会跟随旋转）
            Font rovFont = new Font("Arial", 8, FontStyle.Bold);
            using (Brush rovTextBrush = new SolidBrush(Color.FromArgb(220, 240, 255)))
            {
                string rovText = "ROV";
                SizeF rovTextSize = g.MeasureString(rovText, rovFont);
                g.DrawString(rovText, rovFont, rovTextBrush,
                            rovBody.X + (rovBody.Width - rovTextSize.Width) / 2,
                            rovBody.Y + (rovBody.Height - rovTextSize.Height) / 2);
            }
            rovFont.Dispose();

            // 5. 绘制推进器（也会跟随旋转）
            DrawThrusterPositions(g, rovBody);

            // 恢复原始图形状态（后续绘制的内容不受旋转影响）
            g.Restore(originalState);
        }

        private void DrawThrusterPositions(Graphics g, Rectangle rovBody)
        {
            Point[] leftThrusters = {
                new Point(rovBody.Left - 8, rovBody.Top + 5),
                new Point(rovBody.Left - 8, rovBody.Top + 30),
                new Point(rovBody.Left - 8, rovBody.Bottom - 5)
            };

            Point[] rightThrusters = {
                new Point(rovBody.Right + 8, rovBody.Top + 5),
                new Point(rovBody.Right + 8, rovBody.Top + 30),
                new Point(rovBody.Right + 8, rovBody.Bottom - 5)
            };

            for (int i = 0; i < 3; i++)
            {
                // 左侧推进器
                using (LinearGradientBrush thrusterBrush = new LinearGradientBrush(
                    new Rectangle(leftThrusters[i].X - 4, leftThrusters[i].Y - 4, 8, 8),
                    thrusterBaseColors[i],
                    Color.FromArgb(Math.Max(0, thrusterBaseColors[i].R - 30),
                                   Math.Max(0, thrusterBaseColors[i].G - 30),
                                   Math.Max(0, thrusterBaseColors[i].B - 30)),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(thrusterBrush, leftThrusters[i].X - 4, leftThrusters[i].Y - 4, 8, 8);
                }
                using (Pen thrusterPen = new Pen(Color.FromArgb(200, 230, 255), 1))
                {
                    g.DrawEllipse(thrusterPen, leftThrusters[i].X - 4, leftThrusters[i].Y - 4, 8, 8);
                }

                // 右侧推进器  
                using (LinearGradientBrush thrusterBrush = new LinearGradientBrush(
                    new Rectangle(rightThrusters[i].X - 4, rightThrusters[i].Y - 4, 8, 8),
                    thrusterBaseColors[i + 3],
                    Color.FromArgb(Math.Max(0, thrusterBaseColors[i + 3].R - 30),
                                   Math.Max(0, thrusterBaseColors[i + 3].G - 30),
                                   Math.Max(0, thrusterBaseColors[i + 3].B - 30)),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(thrusterBrush, rightThrusters[i].X - 4, rightThrusters[i].Y - 4, 8, 8);
                }
                using (Pen thrusterPen = new Pen(Color.FromArgb(200, 230, 255), 1))
                {
                    g.DrawEllipse(thrusterPen, rightThrusters[i].X - 4, rightThrusters[i].Y - 4, 8, 8);
                }
            }
        }

        private void DrawTwoRowHorizontalThrusterBars(Graphics g)
        {
            int barWidth = 110;
            int barHeight = 16;
            int startY = 40;
            int rowSpacing = 40;
            int colSpacing = 120;
            int startX = 290;  // ← 增大这个值让滑条右移
            int titleX = 220;  // ← 标题的独立X坐标
            int labelHeight = 12;
            int leftLabelWidth = 140;

            // 赛博风格配色
            Color neonCyan = Color.FromArgb(0, 255, 255);
            Color neonPurple = Color.FromArgb(255, 0, 255);
            Color neonGreen = Color.FromArgb(0, 255, 128);

            Font labelFont = new Font("Consolas", 8, FontStyle.Bold);
            Font valueFont = new Font("Consolas", 8, FontStyle.Bold);

            // 赛博风格标题 - 使用独立的titleX坐标
            Font sectionTitleFont = new Font("Consolas", 15, FontStyle.Bold);
            using (Brush titleBrush = new SolidBrush(neonCyan))
            {
                string sectionTitle = ">>> THRUSTER STATUS <<<";
                int titleY = 2;  // ← 新增：标题的Y坐标，改小这个值让标题上移

                // 发光效果
                using (Brush glowBrush = new SolidBrush(Color.FromArgb(50, neonCyan)))
                {
                    for (int offset = 1; offset <= 3; offset++)
                    {
                        g.DrawString(sectionTitle, sectionTitleFont, glowBrush,
                                   titleX - offset, titleY - offset);  // ← 使用titleY
                        g.DrawString(sectionTitle, sectionTitleFont, glowBrush,
                                   titleX + offset, titleY + offset);  // ← 使用titleY
                    }
                }

                g.DrawString(sectionTitle, sectionTitleFont, titleBrush, titleX, titleY);  // ← 使用titleY
            }
            sectionTitleFont.Dispose();

            Font rowLabelFont = new Font("Consolas", 11, FontStyle.Bold);

            using (Brush textBrush = new SolidBrush(neonCyan))
            using (Brush rowLabelBrush = new SolidBrush(neonGreen))
            {
                for (int i = 0; i < 6; i++)
                {
                    int row = i < 3 ? 0 : 1;
                    int col = i % 3;

                    int x = startX + col * colSpacing;
                    int y = startY + row * rowSpacing;

                    // 赛博风格行标签
                    if (col == 0)
                    {
                        string rowLabel = row == 0 ? "◄ LEFT ARRAY" : "► RIGHT ARRAY";
                        SizeF rowLabelSize = g.MeasureString(rowLabel, rowLabelFont);
                        float rowLabelY = y + (barHeight - rowLabelSize.Height) / 2;

                        using (Brush labelGlow = new SolidBrush(Color.FromArgb(80, neonGreen)))
                        {
                            g.DrawString(rowLabel, rowLabelFont, labelGlow,
                                       startX - leftLabelWidth + 1, rowLabelY + 1);
                        }
                        g.DrawString(rowLabel, rowLabelFont, rowLabelBrush,
                                   startX - leftLabelWidth, rowLabelY);
                    }

                    int value = thrusterValues[i];

                    // 赛博风格数值显示
                    string label = thrusterLabels[i];
                    string valueText = $"[{value:+000;-000; 000}]";
                    string combinedText = $"{label}:{valueText}";

                    SizeF combinedSize = g.MeasureString(combinedText, labelFont);

                    Color valueColor = value > 50 ? neonGreen :
                                      value > 0 ? neonCyan :
                                      value < -50 ? neonPurple :
                                      Color.FromArgb(100, 200, 255);

                    using (Brush valueGlow = new SolidBrush(Color.FromArgb(60, valueColor)))
                    {
                        g.DrawString(combinedText, labelFont, valueGlow,
                                   x + (barWidth - combinedSize.Width) / 2 + 1, y - labelHeight + 1);
                    }

                    using (Brush combinedBrush = new SolidBrush(valueColor))
                    {
                        g.DrawString(combinedText, labelFont, combinedBrush,
                                   x + (barWidth - combinedSize.Width) / 2, y - labelHeight);
                    }

                    // 赛博风格滑条
                    Rectangle barBg = new Rectangle(x, y, barWidth, barHeight);

                    using (Pen glowPen = new Pen(Color.FromArgb(80, neonCyan), 3))
                    {
                        g.DrawRectangle(glowPen, barBg.X - 1, barBg.Y - 1,
                                       barBg.Width + 2, barBg.Height + 2);
                    }

                    using (Pen borderPen = new Pen(neonCyan, 1))
                    {
                        g.DrawRectangle(borderPen, barBg);
                    }

                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        barBg,
                        Color.FromArgb(20, 20, 40),
                        Color.FromArgb(10, 10, 25),
                        LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(bgBrush, barBg);
                    }

                    // 网格线
                    using (Pen gridPen = new Pen(Color.FromArgb(40, neonCyan), 1))
                    {
                        for (int gridX = x + 10; gridX < x + barWidth; gridX += 10)
                        {
                            g.DrawLine(gridPen, gridX, y, gridX, y + barHeight);
                        }
                    }

                    // 中心线
                    int centerX = x + barWidth / 2;
                    using (Pen centerPen = new Pen(Color.FromArgb(150, neonCyan), 2))
                    {
                        g.DrawLine(centerPen, centerX, y + 2, centerX, y + barHeight - 2);
                    }

                    // 数据条填充
                    int fillWidth = Math.Abs(value) * barWidth / 200;

                    Rectangle fillRect;
                    if (value >= 0)
                    {
                        fillRect = new Rectangle(centerX, y + 2, fillWidth, barHeight - 4);
                    }
                    else
                    {
                        fillRect = new Rectangle(centerX - fillWidth, y + 2, fillWidth, barHeight - 4);
                    }

                    if (fillWidth > 0)
                    {
                        Color fillColor = value > 0 ?
                            (Math.Abs(value) > 70 ? neonGreen : neonCyan) :
                            (Math.Abs(value) > 70 ? neonPurple : Color.FromArgb(255, 100, 150));

                        using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                            fillRect,
                            Color.FromArgb(200, fillColor),
                            Color.FromArgb(100, fillColor),
                            value >= 0 ? LinearGradientMode.Horizontal : LinearGradientMode.BackwardDiagonal))
                        {
                            g.FillRectangle(fillBrush, fillRect);
                        }

                        using (Pen fillGlow = new Pen(Color.FromArgb(120, fillColor), 2))
                        {
                            g.DrawRectangle(fillGlow, fillRect);
                        }

                        // 扫描线
                        using (Pen scanLine = new Pen(Color.FromArgb(180, Color.White), 1))
                        {
                            int scanY = y + barHeight / 2;
                            g.DrawLine(scanLine, fillRect.Left, scanY, fillRect.Right, scanY);
                        }

                        // 数据流点
                        for (int dot = fillRect.Left; dot < fillRect.Right; dot += 8)
                        {
                            using (Brush dotBrush = new SolidBrush(Color.FromArgb(150, Color.White)))
                            {
                                g.FillEllipse(dotBrush, dot, y + barHeight - 4, 2, 2);
                            }
                        }
                    }
                }
            }

            labelFont.Dispose();
            valueFont.Dispose();
            rowLabelFont.Dispose();
        }
    }

    //// 扩展方法类 - 移到namespace级别（在类外面）
    //public static class GraphicsExtensions
    //{
    //    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    //    {
    //        using (GraphicsPath path = GetRoundedRect(rect, radius))
    //        {
    //            g.FillPath(brush, path);
    //        }
    //    }

    //    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    //    {
    //        using (GraphicsPath path = GetRoundedRect(rect, radius))
    //        {
    //            g.DrawPath(pen, path);
    //        }
    //    }

    //    private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    //    {
    //        GraphicsPath path = new GraphicsPath();
    //        int diameter = radius * 2;

    //        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
    //        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
    //        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
    //        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
    //        path.CloseFigure();

    //        return path;
    //    }
    //}
}