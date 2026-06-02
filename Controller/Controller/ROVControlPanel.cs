using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    // 还原控制面板视觉与布局（移除串口分区），保留事件与状态方法
    public partial class ROVControlPanel : UserControl
    {
        // 颜色主题
        private Color primaryColor = Color.FromArgb(0, 255, 255);
        private Color secondaryColor = Color.FromArgb(100, 255, 200);
        private Color backgroundColor = Color.FromArgb(15, 25, 35);
        private Color panelColor = Color.FromArgb(20, 40, 60);
        private Color borderColor = Color.FromArgb(0, 200, 255);
        private Color textColor = Color.FromArgb(200, 230, 255);

        // 控件字段
        private ComboBox cb_lightLevel;
        private Button bt_light;
        private Button bt_save;
        private Button bt_new;
        private Button bt_lock;
        private TextBox tb_depth;
        private Label lbl_lightLevel;
        private Label lbl_depth;
        private Label lbl_titleFile;
        private Label lbl_titleLight;
        private Label lbl_titleDepth;

        // 事件
        public event EventHandler LightControlClicked;
        public event EventHandler SaveDataClicked;
        public event EventHandler NewFileClicked;
        public event EventHandler DepthLockClicked;
        public event EventHandler<int> LightLevelChanged;

        // 属性
        public int SelectedLightLevel => cb_lightLevel?.SelectedIndex ?? 0;
        public string DepthValue => tb_depth?.Text ?? "0.0";

        public ROVControlPanel()
        {
            InitializeComponent();
            InitializeCustomControls();
            this.BackColor = backgroundColor;
            this.Visible = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
        }

        private void InitializeComponent()
        {
            // 面板高度大小
            this.Size = new Size(550, 100);
        }

        private void InitializeCustomControls()
        {
            int margin = 10;
            int headerHeight = 30;
            int sectionGap = 22;
            int sectionTop = headerHeight + margin;
            int usableWidth = this.Width - margin * 2;
            int sectionWidth = (usableWidth - sectionGap * 2) / 3;

            // 横排三分区
            int xFile = margin;
            int xLight = margin + sectionWidth + sectionGap;
            int xDepth = margin + (sectionWidth + sectionGap) * 2;

            CreateFileSection(xFile+5, sectionTop + 35, sectionWidth, 60);
            CreateLightSection(xLight+5, sectionTop + 35, sectionWidth, 60);
            CreateDepthSection(xDepth + 5, sectionTop + 35, sectionWidth, 60);

            // 加入控件
            this.Controls.AddRange(new Control[]
            {
                // 文件
                lbl_titleFile, bt_new, bt_save,
                // 灯光
                lbl_titleLight, lbl_lightLevel, cb_lightLevel, bt_light,
                // 定深
                lbl_titleDepth, lbl_depth, tb_depth, bt_lock
            });
            foreach (Control c in this.Controls) c.BringToFront();
        }

        // 分区构建
        private void CreateFileSection(int x, int y, int width, int height)
        {
            // 标题
            lbl_titleFile = CreateTitleLabel("文件", x+5, y - 18, width);

            int controlY = y;
            int buttonWidth = 64;
            int spacing = 8;
            int col1X = x + 5;
            int col2X = col1X + buttonWidth + spacing;

            bt_new = CreateButton("新建", col1X, controlY, buttonWidth);
            bt_new.Click += (s, e) => NewFileClicked?.Invoke(this, EventArgs.Empty);

            bt_save = CreateButton("录数", col2X, controlY, buttonWidth);
            bt_save.Click += (s, e) => SaveDataClicked?.Invoke(this, EventArgs.Empty);
        }

        private void CreateLightSection(int x, int y, int width, int height)
        {
            lbl_titleLight = CreateTitleLabel("灯光", x-19, y - 18, width);

            int controlY = y + 7;
            int col1X = x - 20;
            int col2X = col1X + 100;

            lbl_lightLevel = CreateLabel("亮度:", col1X, controlY);
            cb_lightLevel = CreateComboBox(col1X + 32, controlY - 2, 64);
            for (int i = 0; i <= 10; i++) cb_lightLevel.Items.Add(i.ToString());
            cb_lightLevel.SelectedIndex = 5;
            cb_lightLevel.SelectedIndexChanged += (s, e) =>
            {
                if (cb_lightLevel.SelectedIndex >= 0)
                    LightLevelChanged?.Invoke(this, cb_lightLevel.SelectedIndex);
            };

            bt_light = CreateButton("开灯", col2X, controlY - 7, 64);
            bt_light.Click += (s, e) => LightControlClicked?.Invoke(this, EventArgs.Empty);
        }

        private void CreateDepthSection(int x, int y, int width, int height)
        {
            lbl_titleDepth = CreateTitleLabel("定深", x-15, y - 18, width);

            int controlY = y + 6;
            int col1X = x + 10;
            int col2X = col1X + 125; // 右移 30 像素

            lbl_depth = CreateLabel("目标深度(m):", col1X-25, controlY);
            tb_depth = CreateTextBox(col1X + 50, controlY - 2, 72);
            tb_depth.Text = "0.0";
            tb_depth.KeyPress += Tb_depth_KeyPress;

            bt_lock = CreateButton("定深", col2X , controlY - 6, 64);
            bt_lock.Click += (s, e) => DepthLockClicked?.Invoke(this, EventArgs.Empty);
        }

        // 基础控件工厂
        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(120, 18),
                ForeColor = secondaryColor,
                Font = new Font("微软雅黑", 9F, FontStyle.Regular),
                BackColor = Color.Transparent
            };
        }

        private ComboBox CreateComboBox(int x, int y, int width)
        {
            var cb = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = panelColor,
                ForeColor = textColor,
                Font = new Font("Consolas", 9F)
            };
            return cb;
        }

        private TextBox CreateTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BackColor = panelColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };
        }

        private Button CreateButton(string text, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(150, 0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = primaryColor;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 0, 150, 255);
            return btn;
        }

        private Label CreateTitleLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(width, 16),
                ForeColor = Color.FromArgb(200, secondaryColor),
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        // 事件：限制深度文本输入
        private void Tb_depth_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
                return;
            }
            if (e.KeyChar == '.' && tb_depth.Text.Contains("."))
            {
                e.Handled = true;
            }
        }

        // 外观绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var rect = this.ClientRectangle;
            rect.Inflate(-4, -4);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            DrawCyberBackground(g, rect);
            int headerHeight = 36;
            if (headerHeight > rect.Height - 10) headerHeight = Math.Max(20, rect.Height / 4);
            var headerRect = new Rectangle(rect.Left + 8, rect.Top + 6, rect.Width - 16, headerHeight);
            var contentRect = new Rectangle(rect.Left + 8, headerRect.Bottom + 6, rect.Width - 16, rect.Bottom - headerRect.Bottom - 12);

            DrawGrid(g, contentRect);
            DrawHeaderSplit(g, headerRect, contentRect);
            DrawFrameDecorations(g, rect);
            DrawMainTitle(g, headerRect);
        }

        private void DrawCyberBackground(Graphics g, Rectangle rect)
        {
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                rect,
                Color.FromArgb(18, 26, 40),
                Color.FromArgb(8, 14, 26),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, rect);
            }
        }

        private void DrawGrid(Graphics g, Rectangle contentRect)
        {
            if (contentRect.Width <= 0 || contentRect.Height <= 0) return;

            using (var gridPen = new Pen(Color.FromArgb(22, 0, 255, 240), 1f))
            {
                for (int x = contentRect.Left; x < contentRect.Right; x += 18)
                    g.DrawLine(gridPen, x, contentRect.Top, x, contentRect.Bottom);
                for (int y = contentRect.Top; y < contentRect.Bottom; y += 18)
                    g.DrawLine(gridPen, contentRect.Left, y, contentRect.Right, y);
            }
        }

        private void DrawMainTitle(Graphics g, Rectangle headerRect)
        {
            if (headerRect.Width <= 0 || headerRect.Height <= 0) return;
            using (Brush b = new SolidBrush(Color.FromArgb(210, secondaryColor)))
            using (var titleFont = new Font("Microsoft YaHei", 11F, FontStyle.Italic))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("ROV 控制面板", titleFont, b, headerRect, format);
            }
        }

        private void DrawHeaderSplit(Graphics g, Rectangle headerRect, Rectangle bodyRect)
        {
            if (headerRect.Width <= 0 || headerRect.Height <= 0) return;
            if (bodyRect.Height < 0) bodyRect.Height = 0;

            using (var headerBrush = new LinearGradientBrush(
                headerRect,
                Color.FromArgb(20, 60, 70),
                Color.FromArgb(10, 28, 36),
                LinearGradientMode.Vertical))
            using (var headerGlow = new LinearGradientBrush(headerRect, Color.FromArgb(80, 0, 200, 220), Color.FromArgb(20, 0, 160, 120), LinearGradientMode.Horizontal))
            using (var headerLine = new Pen(Color.FromArgb(180, 80, 220, 220), 1.6f))
            {
                g.FillRectangle(headerBrush, headerRect);
                g.FillRectangle(headerGlow, new Rectangle(headerRect.Left, headerRect.Top, headerRect.Width, 3));
                g.DrawLine(headerLine, headerRect.Left, headerRect.Bottom, headerRect.Right, headerRect.Bottom);
            }

            if (bodyRect.Height > 0 && bodyRect.Width > 0)
            {
                using (var bodyBrush = new LinearGradientBrush(
                    bodyRect,
                    Color.FromArgb(12, 26, 40),
                    Color.FromArgb(6, 16, 28),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bodyBrush, bodyRect);
                }
            }

            using (var accentPen = new Pen(Color.FromArgb(230, 200, 255, 250), 1f))
            {
                int tick = 18;
                g.DrawLine(accentPen, headerRect.Left + 6, headerRect.Top + 8, headerRect.Left + 6 + tick, headerRect.Top + 8);
                g.DrawLine(accentPen, headerRect.Right - 6 - tick, headerRect.Top + 8, headerRect.Right - 6, headerRect.Top + 8);
            }
        }

        private void DrawFrameDecorations(Graphics g, Rectangle rect)
        {
            using (var borderPen = new Pen(Color.FromArgb(200, 0, 255, 240), 1.4f))
            using (var innerPen = new Pen(Color.FromArgb(120, 150, 200, 255), 1f) { DashStyle = DashStyle.Dot })
            using (var accentBrush = new SolidBrush(Color.FromArgb(200, 80, 220, 255)))
            {
                g.DrawRectangle(borderPen, rect);
                var innerRect = Rectangle.Inflate(rect, -6, -6);
                if (innerRect.Width > 0 && innerRect.Height > 0)
                    g.DrawRectangle(innerPen, innerRect);

                var highlightDiameter = 6;
                var highlights = new[]
                {
                    new Point(rect.Left + 4, rect.Top + 4),
                    new Point(rect.Right - 4 - highlightDiameter, rect.Top + 4),
                    new Point(rect.Left + 4, rect.Bottom - 4 - highlightDiameter),
                    new Point(rect.Right - 4 - highlightDiameter, rect.Bottom - 4 - highlightDiameter)
                };
                foreach (var point in highlights)
                {
                    g.FillEllipse(accentBrush, point.X, point.Y, highlightDiameter, highlightDiameter);
                }
            }
        }

        // 供 Form1 调用的状态设置方法
        public void SetSaveButtonState(bool recording)
        {
            if (bt_save == null) return;
            bt_save.Text = recording ? "停止" : "录数";
            bt_save.BackColor = recording ? Color.LightGreen : Color.FromArgb(150, 0, 120, 215);
        }
        public void SetLockButtonState(bool isLocked)
        {
            if (bt_lock == null) return;
            bt_lock.Text = isLocked ? "解锁" : "定深";
            bt_lock.BackColor = isLocked ? Color.LightGreen : Color.FromArgb(150, 0, 120, 215);
        }
        public void SetLightButtonState(bool isOn)
        {
            if (bt_light == null) return;
            bt_light.Text = isOn ? "关灯" : "开灯";
            bt_light.BackColor = isOn ? Color.LightGreen : Color.FromArgb(150, 0, 120, 215);
        }

    }
}
