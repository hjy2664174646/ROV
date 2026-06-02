using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    /// <summary>
    /// 赛博风格窗口控制按钮组件（最小化、最大化、关闭）
    /// </summary>
    public partial class CyberWindowControls : UserControl
    {
        private Panel minimizeBtn;
        private Panel maximizeBtn;
        private Panel closeBtn;
        private Form parentForm;
        private bool isMaximized = false;

        // 赛博风格颜色配置
        private readonly Color CyberBlue = Color.FromArgb(0, 255, 255);      // 青色
        private readonly Color CyberPurple = Color.FromArgb(147, 0, 211);    // 紫色
        private readonly Color CyberRed = Color.FromArgb(255, 20, 147);      // 深粉红
        private readonly Color CyberGreen = Color.FromArgb(0, 255, 127);     // 春绿色
        private readonly Color DarkBg = Color.FromArgb(20, 25, 35);          // 深色背景
        private readonly Color HoverGlow = Color.FromArgb(100, 0, 255, 255); // 半透明发光效果

        public CyberWindowControls()
        {
            InitializeComponent();
            this.Size = new Size(120, 35);
            this.BackColor = Color.Transparent;
            this.Dock = DockStyle.None;
            this.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        private void InitializeComponent()
        {
            // 创建最小化按钮
            minimizeBtn = CreateCyberButton("_", CyberBlue);
            minimizeBtn.Location = new Point(5, 5);
            minimizeBtn.Click += MinimizeBtn_Click;
            this.Controls.Add(minimizeBtn);

            // 创建最大化按钮
            maximizeBtn = CreateCyberButton("□", CyberPurple);
            maximizeBtn.Location = new Point(40, 5);
            maximizeBtn.Click += MaximizeBtn_Click;
            this.Controls.Add(maximizeBtn);

            // 创建关闭按钮
            closeBtn = CreateCyberButton("×", CyberRed);
            closeBtn.Location = new Point(75, 5);
            closeBtn.Click += CloseBtn_Click;
            this.Controls.Add(closeBtn);
        }

        /// <summary>
        /// 创建赛博风格按钮
        /// </summary>
        private Panel CreateCyberButton(string text, Color accentColor)
        {
            var btn = new Panel();
            btn.Size = new Size(30, 25);
            btn.BackColor = DarkBg;
            btn.Cursor = Cursors.Hand;
            btn.Tag = new { Text = text, AccentColor = accentColor, IsHovered = false };

            // 添加鼠标事件
            btn.MouseEnter += (s, e) => {
                var tag = (dynamic)btn.Tag;
                btn.Tag = new { tag.Text, tag.AccentColor, IsHovered = true };
                btn.Invalidate();
            };

            btn.MouseLeave += (s, e) => {
                var tag = (dynamic)btn.Tag;
                btn.Tag = new { tag.Text, tag.AccentColor, IsHovered = false };
                btn.Invalidate();
            };

            // 自定义绘制
            btn.Paint += CyberButton_Paint;

            return btn;
        }

        /// <summary>
        /// 赛博风格按钮绘制
        /// </summary>
        private void CyberButton_Paint(object sender, PaintEventArgs e)
        {
            var btn = sender as Panel;
            var tag = (dynamic)btn.Tag;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            string text = tag.Text;
            Color accentColor = tag.AccentColor;
            bool isHovered = tag.IsHovered;

            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);

            // 背景渐变
            using (var bgBrush = new LinearGradientBrush(rect,
                   Color.FromArgb(15, 20, 30),
                   Color.FromArgb(25, 35, 50),
                   LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // 边框绘制
            if (isHovered)
            {
                // 发光效果边框
                using (var glowPen = new Pen(accentColor, 2))
                {
                    g.DrawRectangle(glowPen, rect);
                }

                // 内部发光
                using (var innerGlowBrush = new SolidBrush(Color.FromArgb(30, accentColor)))
                {
                    g.FillRectangle(innerGlowBrush, rect);
                }
            }
            else
            {
                // 普通边框
                using (var borderPen = new Pen(Color.FromArgb(100, accentColor), 1))
                {
                    g.DrawRectangle(borderPen, rect);
                }
            }

            // 绘制文字 
            var textColor = isHovered ? accentColor : Color.FromArgb(200, accentColor);
            using (var textBrush = new SolidBrush(textColor))
            using (var font = new Font("Consolas", 12, FontStyle.Bold))
            {
                var textSize = g.MeasureString(text, font);

                // 计算文字位置，对最小化按钮的"-"做特殊调整
                float textX = (btn.Width - textSize.Width) / 2;
                float textY = (btn.Height - textSize.Height) / 2;

                // 如果是最小化按钮的"-"，往上移动2-3像素
                if (text == "_")
                {
                    textY -= 6; // 往上移动3像素，你可以调整这个数值
                }

                var textPos = new PointF(textX, textY);
                g.DrawString(text, font, textBrush, textPos);
            }

            // 科技感装饰线条
            if (isHovered)
            {
                using (var decorPen = new Pen(Color.FromArgb(150, accentColor), 1))
                {
                    // 左上角装饰
                    g.DrawLine(decorPen, 2, 2, 8, 2);
                    g.DrawLine(decorPen, 2, 2, 2, 8);

                    // 右下角装饰
                    g.DrawLine(decorPen, btn.Width - 9, btn.Height - 3, btn.Width - 3, btn.Height - 3);
                    g.DrawLine(decorPen, btn.Width - 3, btn.Height - 9, btn.Width - 3, btn.Height - 3);
                }
            }
        }

        /// <summary>
        /// 设置父窗体引用
        /// </summary>
        public void SetParentForm(Form form)
        {
            parentForm = form;

            // 监听窗体状态变化来更新最大化按钮图标
            if (parentForm != null)
            {
                parentForm.Resize += (s, e) => UpdateMaximizeButtonIcon();
                UpdateMaximizeButtonIcon();
            }
        }

        /// <summary>
        /// 更新最大化按钮图标
        /// </summary>
        private void UpdateMaximizeButtonIcon()
        {
            if (parentForm == null) return;

            isMaximized = parentForm.WindowState == FormWindowState.Maximized;
            var tag = (dynamic)maximizeBtn.Tag;

            // 更新按钮文字：最大化时显示还原图标，正常时显示最大化图标
            string newText = isMaximized ? "❐" : "□";
            maximizeBtn.Tag = new { Text = newText, tag.AccentColor, tag.IsHovered };
            maximizeBtn.Invalidate();
        }

        #region 按钮事件处理
        private void MinimizeBtn_Click(object sender, EventArgs e)
        {
            if (parentForm != null)
            {
                parentForm.WindowState = FormWindowState.Minimized;
            }
        }

        private void MaximizeBtn_Click(object sender, EventArgs e)
        {
            if (parentForm != null)
            {
                if (parentForm.WindowState == FormWindowState.Maximized)
                {
                    parentForm.WindowState = FormWindowState.Normal;
                }
                else
                {
                    parentForm.WindowState = FormWindowState.Maximized;
                }
            }
        }

        private void CloseBtn_Click(object sender, EventArgs e)
        {
            if (parentForm != null)
            {
                parentForm.Close();
            }
        }
        #endregion

        /// <summary>
        /// 重写背景绘制以添加整体科技感效果
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 绘制背景发光线条
            using (var glowPen = new Pen(Color.FromArgb(50, CyberBlue), 1))
            {
                // 顶部发光线
                g.DrawLine(glowPen, 0, 0, this.Width, 0);
                // 底部发光线  
                g.DrawLine(glowPen, 0, this.Height - 1, this.Width, this.Height - 1);
            }
        }

        /// <summary>
        /// 启用控件的双缓冲以减少闪烁
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}