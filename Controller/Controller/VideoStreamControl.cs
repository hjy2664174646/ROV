using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Controller
{
    public class VideoStreamControl : UserControl
    {
        private readonly PictureBox pictureBox;
        private readonly Label statusLabel;
        private readonly Panel headerPanel;
        private readonly Color accentColor = Color.FromArgb(0, 255, 255);
        private readonly Color borderColor = Color.FromArgb(0, 210, 255);
        private readonly object imageLock = new object();

        public VideoStreamControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            UpdateStyles();
            BackColor = Color.FromArgb(5, 10, 20);
            Size = new Size(445, 350);
            Padding = new Padding(18, 12, 18, 18);

            headerPanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };
            headerPanel.Paint += HeaderPanel_Paint;

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = accentColor,
                Font = new Font("Consolas", 10F, FontStyle.Bold),  
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0), 
                Text = "Video: 未连接"
            };
            headerPanel.Controls.Add(statusLabel);

            Panel contentPanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.FromArgb(10, 10, 10),
                BorderStyle = BorderStyle.None
            };

            contentPanel.Controls.Add(pictureBox);
            Controls.Add(contentPanel);
            Controls.Add(headerPanel);
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(headerPanel.ClientRectangle,
                       Color.FromArgb(80, accentColor),
                       Color.FromArgb(10, accentColor), LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, headerPanel.ClientRectangle);
            }
            using (Pen pen = new Pen(borderColor, 2))
            {
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            Rectangle inner = Rectangle.Inflate(outer, -6, -6);

            using (GraphicsPath outerPath = RoundedRect(outer, 18))
            using (GraphicsPath innerPath = RoundedRect(inner, 12))
            using (Pen outerPen = new Pen(borderColor, 3))
            using (Pen innerPen = new Pen(Color.FromArgb(80, borderColor), 1))
            {
                e.Graphics.DrawPath(outerPen, outerPath);
                e.Graphics.DrawPath(innerPen, innerPath);
            }

            using (GraphicsPath glowPath = RoundedRect(Rectangle.Inflate(inner, -8, -8), 10))
            using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
            {
                glowBrush.CenterColor = Color.FromArgb(60, accentColor);
                glowBrush.SurroundColors = new[] { Color.Transparent };
                e.Graphics.FillPath(glowBrush, glowPath);
            }
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void UpdateStatus(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatus(text)));
                return;
            }
            statusLabel.Text = text;
        }

        public void UpdateFrame(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                Image clone = (Image)image.Clone();
                BeginInvoke(new Action(() => UpdateFrame(clone)));
                image.Dispose();
                return;
            }

            lock (imageLock)
            {
                var old = pictureBox.Image;
                pictureBox.Image = image;
                old?.Dispose();
            }
        }
    }

    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }
}
