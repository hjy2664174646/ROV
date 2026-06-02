using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Controller  
{
    public class CyberLabel : Label
    {
        private Timer glowTimer;
        private float glowIntensity = 0.5f;
        private bool increasing = true;

        public CyberLabel()
        {
            // 设置基本样式 - 增大字体
            this.Font = new Font("Consolas", 16, FontStyle.Bold); // 从12增加到16
            this.ForeColor = Color.FromArgb(0, 255, 255);
            this.BackColor = Color.Transparent;

            // 启用双缓冲
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw, true);

            // 启动发光动画
            glowTimer = new Timer();
            glowTimer.Interval = 50; // 20fps
            glowTimer.Tick += GlowTimer_Tick;
            glowTimer.Start();
        }

        private void GlowTimer_Tick(object sender, EventArgs e)
        {
            if (increasing)
            {
                glowIntensity += 0.02f;
                if (glowIntensity >= 1.0f)
                {
                    glowIntensity = 1.0f;
                    increasing = false;
                }
            }
            else
            {
                glowIntensity -= 0.02f;
                if (glowIntensity <= 0.3f)
                {
                    glowIntensity = 0.3f;
                    increasing = true;
                }
            }
            this.Invalidate(); // 触发重绘
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 高质量渲染
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            if (string.IsNullOrEmpty(this.Text))
                return;

            // 计算文字位置
            SizeF textSize = e.Graphics.MeasureString(this.Text, this.Font);
            float x = (this.Width - textSize.Width) / 2 + 85;
            float y = (this.Height - textSize.Height) / 2 + 10;

            // 减弱发光效果 - 降低透明度和层数
            int glowAlpha = (int)(255 * glowIntensity * 0.1f); // 从0.3f降到0.1f
            using (Brush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 0, 255, 255)))
            {
                // 发光层数
                for (int i = 1; i <= 2; i++)
                {
                    e.Graphics.DrawString(this.Text, this.Font, glowBrush, x + i, y + i);
                    e.Graphics.DrawString(this.Text, this.Font, glowBrush, x - i, y - i);
                    e.Graphics.DrawString(this.Text, this.Font, glowBrush, x + i, y - i);
                    e.Graphics.DrawString(this.Text, this.Font, glowBrush, x - i, y + i);
                }
            }

            // 绘制主文字 - 降低亮度变化幅度
            int mainAlpha = (int)(255 * (0.8f + 0.1f * glowIntensity)); // 减少亮度变化
            using (Brush textBrush = new SolidBrush(Color.FromArgb(mainAlpha, 0, 255, 255)))
            {
                e.Graphics.DrawString(this.Text, this.Font, textBrush, x, y);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && glowTimer != null)
            {
                glowTimer.Stop();
                glowTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}