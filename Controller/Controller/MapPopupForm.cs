using System;
using System.Drawing;
using System.Windows.Forms;

namespace Controller
{
    public class MapPopupForm : Form
    {
        private readonly MapViewControl mapView;

        public MapPopupForm(string provider, string amapKey, string baiduAk, double centerLon, double centerLat)
        {
            this.Text = string.Empty;
            this.ControlBox = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(960, 720);
            this.BackColor = Color.FromArgb(8, 16, 28);

            mapView = new MapViewControl
            {
                Dock = DockStyle.Fill,
                Provider = provider,
                AmapKey = amapKey,
                BaiduAk = baiduAk,
                CenterLongitude = centerLon,
                CenterLatitude = centerLat
            };
            mapView.ExpandButtonText = "缩小";
            mapView.ExpandRequested += (s, e) => this.Close();
            this.Controls.Add(mapView);

            var btnClose = new Button
            {
                Text = "关闭",
                AutoSize = false,
                Size = new Size(72, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 36, 52),
                ForeColor = Color.White
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 220);
            this.Controls.Add(btnClose);
            this.Layout += (s, e) =>
            {
                btnClose.Location = new Point(this.ClientSize.Width - btnClose.Width - 12,
                                              this.ClientSize.Height - btnClose.Height - 12);
            };
            btnClose.Click += (s, e) => this.Close();
        }

        public MapViewControl MapView => mapView;
    }
}
