using System.Drawing;
using System.Drawing.Drawing2D;

namespace Controller.Utils
{
    public static class GraphicsExtensions
    {
        /// <summary>
        /// 填充圆角矩形
        /// </summary>
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF rect, float radius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(rect, radius))
            {
                graphics.FillPath(brush, path);
            }
        }

        /// <summary>
        /// 绘制圆角矩形边框
        /// </summary>
        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF rect, float radius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(rect, radius))
            {
                graphics.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// 创建圆角矩形路径
        /// </summary>
        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;

            // 左上角
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            // 右上角
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            // 右下角
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            // 左下角
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
