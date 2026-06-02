using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Controller
{
    public class MapViewControl : UserControl
    {
        private WebBrowser web;
        private bool documentReady = false;
        private Queue<Tuple<double, double>> pendingPoints = new Queue<Tuple<double, double>>();

        // Config: choose provider and keys. Provider: "amap" or "baidu". If keys missing, page will fallback to OSM.
        public string Provider { get; set; } = "amap"; // default China-friendly
        public string AmapKey { get; set; } = "";      // TODO: set your Gaode key here
        public string BaiduAk { get; set; } = "";      // TODO: set your Baidu AK here

        // Default center: precise point on Changdang Lake (Jintan, Changzhou, Jiangsu)
        public double CenterLongitude { get; set; } = 119.57160716701571;
        public double CenterLatitude  { get; set; } = 31.68808736873601;

        // Track last point for popup centering
        private bool hasLastPoint = false;
        private double lastLon = 0, lastLat = 0;

        // Small overlay button to request expand
        private Button btnExpand;
        private Button btnZoomIn;
        private Button btnZoomOut;
        public event EventHandler ExpandRequested;
        public string ExpandButtonText
        {
            get => btnExpand?.Text ?? string.Empty;
            set { if (btnExpand != null) btnExpand.Text = value; }
        }

        public MapViewControl()
        {
            InitializeComponent();
            this.Padding = new Padding(10);
            this.BackColor = Color.FromArgb(8, 16, 28);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void InitializeComponent()
        {
            this.web = new WebBrowser();
            this.web.ScriptErrorsSuppressed = true;
            this.web.IsWebBrowserContextMenuEnabled = false;
            this.web.WebBrowserShortcutsEnabled = true;
            this.web.ScrollBarsEnabled = false;
            this.web.Dock = DockStyle.Fill;
            this.Controls.Add(this.web);
            this.Size = new Size(520, 290);

            // Overlay controls: expand and zoom +/-
            btnExpand = new Button();
            btnExpand.AutoSize = false;
            btnExpand.Size = new Size(72, 28);
            btnExpand.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExpand.Click += (s, e) => ExpandRequested?.Invoke(this, EventArgs.Empty);
            ApplyCyberButtonStyle(btnExpand, "放大");
            btnExpand.UseVisualStyleBackColor = false;
            this.Controls.Add(btnExpand);

            btnZoomIn = new Button();
            btnZoomIn.AutoSize = false;
            btnZoomIn.Size = new Size(32, 28);
            btnZoomIn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnZoomIn.Click += (s, e) => { try { if (documentReady && web.Document != null) web.Document.InvokeScript("zoomIn"); } catch { } };
            ApplyCyberButtonStyle(btnZoomIn, "+");
            btnZoomIn.UseVisualStyleBackColor = false;
            this.Controls.Add(btnZoomIn);

            btnZoomOut = new Button();
            btnZoomOut.AutoSize = false;
            btnZoomOut.Size = new Size(32, 28);
            btnZoomOut.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnZoomOut.Click += (s, e) => { try { if (documentReady && web.Document != null) web.Document.InvokeScript("zoomOut"); } catch { } };
            ApplyCyberButtonStyle(btnZoomOut, "-");
            btnZoomOut.UseVisualStyleBackColor = false;
            this.Controls.Add(btnZoomOut);

            this.Resize += (s, e) =>
            {
                UpdateButtonPositions();
            };
            this.Load += (s, e) => UpdateButtonPositions();
            UpdateButtonPositions();

            this.Load += MapViewControl_Load;
            this.web.DocumentCompleted += Web_DocumentCompleted;
        }

        private void MapViewControl_Load(object sender, EventArgs e)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var htmlPath = Path.Combine(baseDir, "Resources", "map.html");
                if (!File.Exists(htmlPath))
                {
                    Console.WriteLine($"[MapView] map.html not found at {htmlPath}");
                    return;
                }

                string url = new Uri(htmlPath).AbsoluteUri +
                             $"?provider={Uri.EscapeDataString(Provider)}&amapKey={Uri.EscapeDataString(AmapKey)}&baiduAk={Uri.EscapeDataString(BaiduAk)}" +
                             $"&centerLon={CenterLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&centerLat={CenterLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                web.Navigate(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapView] Load error: {ex.Message}");
            }
        }

        private void Web_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            documentReady = true;
            // flush pending points
            while (pendingPoints.Count > 0)
            {
                var p = pendingPoints.Dequeue();
                SafeInvokeAddPoint(p.Item1, p.Item2);
            }
        }

        public void AddPoint(double longitude, double latitude)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AddPoint(longitude, latitude)));
                return;
            }
            if (!documentReady || web.Document == null)
            {
                pendingPoints.Enqueue(Tuple.Create(longitude, latitude));
                return;
            }
            hasLastPoint = true;
            lastLon = longitude; lastLat = latitude;
            SafeInvokeAddPoint(longitude, latitude);
        }

        private void SafeInvokeAddPoint(double longitude, double latitude)
        {
            try
            {
                web.Document.InvokeScript("addPoint", new object[] { longitude, latitude });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapView] addPoint error: {ex.Message}");
            }
        }

        public void ClearTrack()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(ClearTrack));
                return;
            }
            pendingPoints.Clear();
            try
            {
                if (documentReady && web.Document != null)
                {
                    web.Document.InvokeScript("clearTrack");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapView] clearTrack error: {ex.Message}");
            }
        }

        public bool TryGetLastPoint(out double lon, out double lat)
        {
            lon = lastLon; lat = lastLat; return hasLastPoint;
        }

        public void ZoomIn()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(ZoomIn));
                return;
            }
            try { if (documentReady && web.Document != null) web.Document.InvokeScript("zoomIn"); } catch { }
        }

        public void ZoomOut()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(ZoomOut));
                return;
            }
            try { if (documentReady && web.Document != null) web.Document.InvokeScript("zoomOut"); } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (Pen outerPen = new Pen(Color.FromArgb(220, 25, 45, 65), 2.5f))
            {
                g.DrawRectangle(outerPen, rect);
            }
        }

        private void ApplyCyberButtonStyle(Button btn, string text)
        {
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 220);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 60, 90);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 90, 130);
            btn.BackColor = Color.FromArgb(18, 28, 40);
            btn.ForeColor = Color.FromArgb(220, 240, 255);
            btn.Font = new Font("Consolas", 10f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.TabStop = false;
        }

        private void UpdateButtonPositions()
        {
            if (btnExpand == null || btnZoomIn == null || btnZoomOut == null) return;
            int right = this.Width - this.Padding.Right;
            int bottom = this.Height - this.Padding.Bottom;
            int y = bottom - btnExpand.Height - 8;
            btnExpand.Location = new Point(right - btnExpand.Width - 8, y);
            btnZoomIn.Location = new Point(btnExpand.Left - 12 - btnZoomIn.Width, y);
            btnZoomOut.Location = new Point(btnZoomIn.Left - 8 - btnZoomOut.Width, y);
            btnExpand.BringToFront();
            btnZoomIn.BringToFront();
            btnZoomOut.BringToFront();
        }

        private GraphicsPath CreateRoundRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(d, d));
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
    }
}
