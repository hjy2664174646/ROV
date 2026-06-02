using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    public partial class GamepadDisplayControl : UserControl
    {
        private GamepadState currentGamepadState;
        private string displayText = "等待手柄数据...";
        private bool _connected = false;

        public GamepadDisplayControl()
        {
            InitializeComponent();
            this.Size = new Size(100, 40);
            this.MinimumSize = new Size(100, 40);
            this.MaximumSize = new Size(100, 40);
            this.AutoSize = false;
            this.BackColor = Color.Transparent;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 调用基类以让父容器背景透出
            base.OnPaintBackground(pevent);
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "GamepadDisplayControl";
            this.ResumeLayout(false);
        }

        // 更新手柄状态的方法
        public void UpdateGamepadState(GamepadState state)
        {
            currentGamepadState = state;
            displayText = FormatGamepadInfo(state);
            this.Invalidate(); // 触发重绘
        }

        // 直接设置显示文本的方法
        public void SetDisplayText(string text)
        {
            displayText = text ?? "无数据";
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawGamepadIndicator(g);
        }

        private void DrawGamepadIndicator(Graphics g)
        {
            Rectangle rect = this.ClientRectangle;
            int margin = 2;

            float frameWidth = Math.Max(70f, rect.Width - margin * 2);
            float frameHeight = Math.Max(28f, rect.Height - margin * 2);
            var frameRect = new RectangleF(margin, margin, frameWidth, frameHeight);

            Color glowColor, borderColor, fillStart, fillEnd, textColor;
            string text;

            if (_connected)
            {
                glowColor = Color.FromArgb(120, 0, 255, 220);
                borderColor = Color.FromArgb(200, 0, 210, 210);
                fillStart = Color.FromArgb(60, 0, 255, 200);
                fillEnd = Color.FromArgb(150, 0, 160, 200);
                textColor = Color.FromArgb(220, 245, 255);
                text = "已连接";
            }
            else
            {
                glowColor = Color.FromArgb(140, 255, 120, 0);
                borderColor = Color.FromArgb(210, 255, 140, 40);
                fillStart = Color.FromArgb(80, 255, 120, 0);
                fillEnd = Color.FromArgb(160, 200, 50, 0);
                textColor = Color.FromArgb(255, 240, 220);
                text = "未连接";
            }

            using (var path = CreateRoundRect(frameRect, 12f))
            using (var glowPen = new Pen(glowColor, 7f))
            using (var innerGlowPen = new Pen(Color.FromArgb(glowColor.A, glowColor), 2.2f))
            using (var fill = new LinearGradientBrush(frameRect, fillStart, fillEnd, 12f))
            using (var border = new Pen(borderColor, 2f))
            using (var hatch = new HatchBrush(HatchStyle.LightDownwardDiagonal, Color.FromArgb(40, 255, 255, 255), Color.Transparent))
            {
                g.DrawPath(glowPen, path);
                g.DrawPath(innerGlowPen, path);
                g.FillPath(fill, path);
                g.FillPath(hatch, path);
                g.DrawPath(border, path);
            }

            var innerRect = RectangleF.Inflate(frameRect, -8, -8);
            TextRenderer.DrawText(g, text, new Font("Consolas", 11F, FontStyle.Italic | FontStyle.Bold),
                Rectangle.Round(innerRect), textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
        public void SetConnected(bool connected)
        {
            _connected = connected;
            this.Invalidate();
        }

        private GraphicsPath CreateRoundRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            RectangleF arc = new RectangleF(rect.Location, new SizeF(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
        private string FormatGamepadInfo(GamepadState state)
        {
            if (state == null)
                return "手柄未连接或无数据";

            string lsXDir = state.LeftStickX < 32768 ? "左" : (state.LeftStickX > 32768 ? "右" : "中");
            string lsYDir = state.LeftStickY < 32767 ? "上" : (state.LeftStickY > 32767 ? "下" : "中");
            string rsXDir = state.RightStickX < 32768 ? "左" : (state.RightStickX > 32768 ? "右" : "中");
            string rsYDir = state.RightStickY < 32767 ? "上" : (state.RightStickY > 32767 ? "下" : "中");

            return $"左摇杆: X={state.LeftStickX}({lsXDir})   Y={state.LeftStickY}({lsYDir})\n" +
                   $"右摇杆: X={state.RightStickX}({rsXDir})   Y={state.RightStickY}({rsYDir})\n" +
                   $"方向键: {state.DpadDescription}\n" +
                   $"主按键: A={state.IsAKeyPressed}   B={state.IsBKeyPressed}   X={state.IsXKeyPressed} Y={state.IsYKeyPressed}\n" +
                   $"肩键: LB={state.IsLBKeyPressed}   RB={state.IsRBKeyPressed}\n" +
                   $"功能键: Select={state.IsSelectPressed}   Start={state.IsStartPressed}";
        }
    }
}
