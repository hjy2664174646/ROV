
namespace Controller
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel8 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.panel7 = new System.Windows.Forms.Panel();
            this.label_wb = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label_fb = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.panel5 = new System.Windows.Forms.Panel();
            this.userCurve2 = new HslCommunication.Controls.UserCurve();
            this.userCurve4 = new HslCommunication.Controls.UserCurve();
            this.userCurve3 = new HslCommunication.Controls.UserCurve();
            this.userCurve1 = new HslCommunication.Controls.UserCurve();
            this.label3 = new System.Windows.Forms.Label();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.panel4 = new System.Windows.Forms.Panel();
            this.label14 = new System.Windows.Forms.Label();
            this.cb1 = new System.Windows.Forms.ComboBox();
            this.panel6 = new System.Windows.Forms.Panel();
            this.label13 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.cb_light = new System.Windows.Forms.ComboBox();
            this.bt_save = new System.Windows.Forms.Button();
            this.bt_new = new System.Windows.Forms.Button();
            this.bt_lock = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.bt_light = new System.Windows.Forms.Button();
            this.rovControlPanel1 = new Controller.ROVControlPanel();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            this.panel7.SuspendLayout();
            this.panel5.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Transparent;
            this.panel1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel1.Controls.Add(this.panel8);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(3, 2);
            this.panel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1929, 86);
            this.panel1.TabIndex = 0;
            // 
            // panel8
            // 
            this.panel8.BackColor = System.Drawing.Color.Transparent;
            this.panel8.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("panel8.BackgroundImage")));
            this.panel8.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel8.Location = new System.Drawing.Point(1275, 20);
            this.panel8.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel8.Name = "panel8";
            this.panel8.Size = new System.Drawing.Size(645, 34);
            this.panel8.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("方正舒体", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(668, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(598, 51);
            this.label1.TabIndex = 0;
            this.label1.Text = "水下无人系统远程监控平台";
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.Transparent;
            this.panel2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel2.Controls.Add(this.richTextBox1);
            this.panel2.Controls.Add(this.chart1);
            this.panel2.Location = new System.Drawing.Point(3, 92);
            this.panel2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1110, 1350);
            this.panel2.TabIndex = 1;
            this.panel2.Paint += new System.Windows.Forms.PaintEventHandler(this.panel2_Paint_1);
            // 
            // richTextBox1
            // 
            this.richTextBox1.BackColor = System.Drawing.Color.Navy;
            this.richTextBox1.Font = new System.Drawing.Font("楷体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.richTextBox1.ForeColor = System.Drawing.Color.White;
            this.richTextBox1.Location = new System.Drawing.Point(27, 855);
            this.richTextBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(655, 200);
            this.richTextBox1.TabIndex = 3;
            this.richTextBox1.Text = "";
            // 
            // chart1
            // 
            this.chart1.BackColor = System.Drawing.Color.Transparent;
            this.chart1.BackImageAlignment = System.Windows.Forms.DataVisualization.Charting.ChartImageAlignmentStyle.TopRight;
            chartArea1.AxisX.IsMarginVisible = false;
            chartArea1.AxisX.LabelStyle.ForeColor = System.Drawing.Color.Yellow;
            chartArea1.AxisX.LineColor = System.Drawing.Color.Yellow;
            chartArea1.AxisX.Title = "纬度";
            chartArea1.AxisX.TitleFont = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.AxisX.TitleForeColor = System.Drawing.Color.Cyan;
            chartArea1.AxisY.IsLabelAutoFit = false;
            chartArea1.AxisY.LabelStyle.Font = new System.Drawing.Font("Times New Roman", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.AxisY.LabelStyle.ForeColor = System.Drawing.Color.Yellow;
            chartArea1.AxisY.LineColor = System.Drawing.Color.Yellow;
            chartArea1.AxisY.Title = "经度";
            chartArea1.AxisY.TitleFont = new System.Drawing.Font("Times New Roman", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.AxisY.TitleForeColor = System.Drawing.Color.Cyan;
            chartArea1.BackColor = System.Drawing.Color.Transparent;
            chartArea1.BorderColor = System.Drawing.Color.Yellow;
            chartArea1.Name = "ChartArea1";
            chartArea1.ShadowColor = System.Drawing.Color.Transparent;
            this.chart1.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            this.chart1.Legends.Add(legend1);
            this.chart1.Location = new System.Drawing.Point(26, 26);
            this.chart1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.chart1.Name = "chart1";
            series1.BorderWidth = 2;
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Color = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            series1.IsVisibleInLegend = false;
            series1.LabelForeColor = System.Drawing.Color.Yellow;
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            series1.ShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            series1.SmartLabelStyle.CalloutLineColor = System.Drawing.Color.White;
            this.chart1.Series.Add(series1);
            this.chart1.Size = new System.Drawing.Size(885, 460);
            this.chart1.TabIndex = 0;
            this.chart1.Text = "chart1";
            // 
            // panel7
            // 
            this.panel7.BackColor = System.Drawing.Color.Transparent;
            this.panel7.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel7.Controls.Add(this.label_wb);
            this.panel7.Controls.Add(this.label11);
            this.panel7.Controls.Add(this.label_fb);
            this.panel7.Controls.Add(this.label10);
            this.panel7.Controls.Add(this.label4);
            this.panel7.Controls.Add(this.label9);
            this.panel7.Controls.Add(this.label5);
            this.panel7.Controls.Add(this.label8);
            this.panel7.Controls.Add(this.label6);
            this.panel7.Controls.Add(this.label7);
            this.panel7.Location = new System.Drawing.Point(1113, 802);
            this.panel7.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel7.Name = "panel7";
            this.panel7.Size = new System.Drawing.Size(669, 495);
            this.panel7.TabIndex = 4;
            this.panel7.Paint += new System.Windows.Forms.PaintEventHandler(this.panel7_Paint);
            // 
            // label_wb
            // 
            this.label_wb.AutoSize = true;
            this.label_wb.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label_wb.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label_wb.Location = new System.Drawing.Point(31, 126);
            this.label_wb.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label_wb.Name = "label_wb";
            this.label_wb.Size = new System.Drawing.Size(286, 24);
            this.label_wb.TabIndex = 2;
            this.label_wb.Text = "陀螺仪：X      Y      Z";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label11.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label11.Location = new System.Drawing.Point(411, 207);
            this.label11.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(82, 24);
            this.label11.TabIndex = 2;
            this.label11.Text = "电压：";
            // 
            // label_fb
            // 
            this.label_fb.AutoSize = true;
            this.label_fb.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label_fb.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label_fb.Location = new System.Drawing.Point(31, 246);
            this.label_fb.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label_fb.Name = "label_fb";
            this.label_fb.Size = new System.Drawing.Size(310, 24);
            this.label_fb.TabIndex = 2;
            this.label_fb.Text = "加速度计：X      Y      Z";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label10.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label10.Location = new System.Drawing.Point(220, 207);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(82, 24);
            this.label10.TabIndex = 2;
            this.label10.Text = "电流：";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label4.Location = new System.Drawing.Point(31, 166);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(286, 24);
            this.label4.TabIndex = 2;
            this.label4.Text = "磁力计：X      Y      Z";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label9.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label9.Location = new System.Drawing.Point(310, 366);
            this.label9.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(82, 24);
            this.label9.TabIndex = 2;
            this.label9.Text = "温度：";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label5.Location = new System.Drawing.Point(31, 286);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(262, 24);
            this.label5.TabIndex = 2;
            this.label5.Text = "姿态：X      Y      Z";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label8.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label8.Location = new System.Drawing.Point(31, 366);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(118, 24);
            this.label8.TabIndex = 2;
            this.label8.Text = "GPS状态：";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label6.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label6.Location = new System.Drawing.Point(31, 207);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(82, 24);
            this.label6.TabIndex = 2;
            this.label6.Text = "速度：";
            this.label6.Click += new System.EventHandler(this.label6_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("楷体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label7.ForeColor = System.Drawing.Color.PaleTurquoise;
            this.label7.Location = new System.Drawing.Point(31, 327);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(262, 24);
            this.label7.TabIndex = 2;
            this.label7.Text = "位置：X      Y      Z";
            this.label7.Click += new System.EventHandler(this.label7_Click);
            // 
            // panel5
            // 
            this.panel5.BackColor = System.Drawing.Color.Transparent;
            this.panel5.BackgroundImage = global::Controller.Properties.Resources.border_bg01;
            this.panel5.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel5.Controls.Add(this.userCurve2);
            this.panel5.Controls.Add(this.userCurve4);
            this.panel5.Controls.Add(this.userCurve3);
            this.panel5.Controls.Add(this.userCurve1);
            this.panel5.Location = new System.Drawing.Point(26, 662);
            this.panel5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel5.Name = "panel5";
            this.panel5.Size = new System.Drawing.Size(1038, 531);
            this.panel5.TabIndex = 4;
            // 
            // userCurve2
            // 
            this.userCurve2.BackColor = System.Drawing.Color.Transparent;
            this.userCurve2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.userCurve2.ColorDashLines = System.Drawing.Color.Red;
            this.userCurve2.ColorLinesAndText = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(128)))));
            this.userCurve2.IsRenderDashLine = false;
            this.userCurve2.Location = new System.Drawing.Point(21, 298);
            this.userCurve2.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.userCurve2.Name = "userCurve2";
            this.userCurve2.Size = new System.Drawing.Size(487, 240);
            this.userCurve2.TabIndex = 0;
            this.userCurve2.Title = "水平姿态曲线";
            this.userCurve2.ValueMaxLeft = 90F;
            this.userCurve2.ValueMaxRight = 90F;
            this.userCurve2.ValueMinLeft = -90F;
            this.userCurve2.ValueMinRight = -90F;
            // 
            // userCurve4
            // 
            this.userCurve4.BackColor = System.Drawing.Color.Transparent;
            this.userCurve4.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.userCurve4.ColorDashLines = System.Drawing.Color.Lime;
            this.userCurve4.ColorLinesAndText = System.Drawing.Color.Lime;
            this.userCurve4.IsRenderDashLine = false;
            this.userCurve4.Location = new System.Drawing.Point(516, 298);
            this.userCurve4.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.userCurve4.Name = "userCurve4";
            this.userCurve4.Size = new System.Drawing.Size(487, 240);
            this.userCurve4.TabIndex = 0;
            this.userCurve4.Title = "航向姿态曲线";
            this.userCurve4.ValueMaxLeft = 180F;
            this.userCurve4.ValueMaxRight = 180F;
            this.userCurve4.ValueMinLeft = -180F;
            this.userCurve4.ValueMinRight = -180F;
            // 
            // userCurve3
            // 
            this.userCurve3.BackColor = System.Drawing.Color.Transparent;
            this.userCurve3.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.userCurve3.ColorDashLines = System.Drawing.Color.Aqua;
            this.userCurve3.ColorLinesAndText = System.Drawing.Color.Aqua;
            this.userCurve3.IsRenderDashLine = false;
            this.userCurve3.Location = new System.Drawing.Point(516, 48);
            this.userCurve3.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.userCurve3.Name = "userCurve3";
            this.userCurve3.Size = new System.Drawing.Size(487, 240);
            this.userCurve3.TabIndex = 0;
            this.userCurve3.Title = "前向速度曲线";
            this.userCurve3.ValueMaxLeft = 10F;
            this.userCurve3.ValueMaxRight = 10F;
            this.userCurve3.ValueMinLeft = -10F;
            this.userCurve3.ValueMinRight = -10F;
            // 
            // userCurve1
            // 
            this.userCurve1.BackColor = System.Drawing.Color.Transparent;
            this.userCurve1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.userCurve1.ColorDashLines = System.Drawing.Color.Yellow;
            this.userCurve1.ColorLinesAndText = System.Drawing.Color.Yellow;
            this.userCurve1.IsRenderDashLine = false;
            this.userCurve1.Location = new System.Drawing.Point(21, 48);
            this.userCurve1.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.userCurve1.Name = "userCurve1";
            this.userCurve1.Size = new System.Drawing.Size(487, 240);
            this.userCurve1.TabIndex = 0;
            this.userCurve1.Title = "深度曲线";
            this.userCurve1.ValueMaxLeft = 20F;
            this.userCurve1.ValueMaxRight = 20F;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Font = new System.Drawing.Font("楷体", 26F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.label3.Location = new System.Drawing.Point(1520, 1350);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(776, 52);
            this.label3.TabIndex = 5;
            this.label3.Text = "河海大学 人工智能与自动化学院";
            // 
            // panel4
            // 
            this.panel4.BackColor = System.Drawing.Color.Transparent;
            this.panel4.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("panel4.BackgroundImage")));
            this.panel4.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel4.Location = new System.Drawing.Point(1095, 630);
            this.panel4.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(1324, 194);
            this.panel4.TabIndex = 5;
            this.panel4.Paint += new System.Windows.Forms.PaintEventHandler(this.panel4_Paint);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(0, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(100, 23);
            this.label14.TabIndex = 0;
            // 
            // cb1
            // 
            this.cb1.Location = new System.Drawing.Point(0, 0);
            this.cb1.Name = "cb1";
            this.cb1.Size = new System.Drawing.Size(121, 26);
            this.cb1.TabIndex = 0;
            // 
            // panel6
            // 
            this.panel6.BackColor = System.Drawing.Color.Transparent;
            this.panel6.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("panel6.BackgroundImage")));
            this.panel6.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel6.Location = new System.Drawing.Point(15, 20);
            this.panel6.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel6.Name = "panel6";
            this.panel6.Size = new System.Drawing.Size(645, 34);
            this.panel6.TabIndex = 1;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label13.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.label13.Location = new System.Drawing.Point(60, 75);
            this.label13.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(103, 29);
            this.label13.TabIndex = 0;
            this.label13.Text = "波特率";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label12.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.label12.Location = new System.Drawing.Point(60, 140);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(103, 29);
            this.label12.TabIndex = 0;
            this.label12.Text = "亮度：";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label15.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.label15.Location = new System.Drawing.Point(514, 140);
            this.label15.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(208, 29);
            this.label15.TabIndex = 0;
            this.label15.Text = "指定深度(m)：";
            // 
            // cb_light
            // 
            this.cb_light.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.cb_light.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cb_light.FormattingEnabled = true;
            this.cb_light.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"});
            this.cb_light.Location = new System.Drawing.Point(177, 135);
            this.cb_light.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cb_light.Name = "cb_light";
            this.cb_light.Size = new System.Drawing.Size(110, 37);
            this.cb_light.TabIndex = 1;
            // 
            // bt_save
            // 
            this.bt_save.BackColor = System.Drawing.Color.DodgerBlue;
            this.bt_save.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.bt_save.Location = new System.Drawing.Point(711, 70);
            this.bt_save.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.bt_save.Name = "bt_save";
            this.bt_save.Size = new System.Drawing.Size(150, 46);
            this.bt_save.TabIndex = 2;
            this.bt_save.Text = "开始录数";
            this.bt_save.UseVisualStyleBackColor = false;
            // 
            // bt_new
            // 
            this.bt_new.BackColor = System.Drawing.Color.DodgerBlue;
            this.bt_new.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.bt_new.Location = new System.Drawing.Point(86, 184);
            this.bt_new.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.bt_new.Name = "bt_new";
            this.bt_new.Size = new System.Drawing.Size(152, 46);
            this.bt_new.TabIndex = 2;
            this.bt_new.Text = "新建文件";
            this.bt_new.UseVisualStyleBackColor = false;
            // 
            // bt_lock
            // 
            this.bt_lock.BackColor = System.Drawing.Color.DodgerBlue;
            this.bt_lock.Font = new System.Drawing.Font("楷体", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.bt_lock.Location = new System.Drawing.Point(170, 58);
            this.bt_lock.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.bt_lock.Name = "bt_lock";
            this.bt_lock.Size = new System.Drawing.Size(152, 46);
            this.bt_lock.TabIndex = 3;
            this.bt_lock.Text = "自锁";
            this.bt_lock.UseVisualStyleBackColor = false;
            this.bt_lock.Click += new System.EventHandler(this.bt_lock_Click);
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.textBox1.Font = new System.Drawing.Font("楷体", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox1.Location = new System.Drawing.Point(710, 135);
            this.textBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(142, 39);
            this.textBox1.TabIndex = 4;
            this.textBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBox1_KeyPress);
            // 
            // label16
            // 
            this.label16.Location = new System.Drawing.Point(0, 0);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(100, 22);
            this.label16.TabIndex = 0;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.Transparent;
            this.panel3.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.panel3.Controls.Add(this.rovControlPanel1);
            this.panel3.Controls.Add(this.label16);
            this.panel3.Controls.Add(this.textBox1);
            this.panel3.Controls.Add(this.bt_lock);
            this.panel3.Controls.Add(this.bt_new);
            this.panel3.Controls.Add(this.bt_save);
            this.panel3.Controls.Add(this.bt_light);
            this.panel3.Controls.Add(this.cb_light);
            this.panel3.Controls.Add(this.label15);
            this.panel3.Controls.Add(this.label12);
            this.panel3.Controls.Add(this.label13);
            this.panel3.Location = new System.Drawing.Point(1324, 92);
            this.panel3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1270, 326);
            this.panel3.TabIndex = 2;
            // 
            // bt_light
            // 
            this.bt_light.BackColor = System.Drawing.Color.DodgerBlue;
            this.bt_light.Font = new System.Drawing.Font("楷体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.bt_light.Location = new System.Drawing.Point(86, 240);
            this.bt_light.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.bt_light.Name = "bt_light";
            this.bt_light.Size = new System.Drawing.Size(188, 46);
            this.bt_light.TabIndex = 2;
            this.bt_light.Text = "照明打开";
            this.bt_light.UseVisualStyleBackColor = false;
            this.bt_light.Click += new System.EventHandler(this.bt_light_Click);
            // 
            // rovControlPanel1
            // 
            this.rovControlPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(25)))), ((int)(((byte)(35)))));
            this.rovControlPanel1.Location = new System.Drawing.Point(196, 21);
            this.rovControlPanel1.Name = "rovControlPanel1";
            this.rovControlPanel1.Size = new System.Drawing.Size(8, 9);
            this.rovControlPanel1.TabIndex = 5;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(2553, 1646);
            this.Controls.Add(this.panel6);
            this.Controls.Add(this.panel4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel5);
            this.Controls.Add(this.panel7);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panel3);
            this.ForeColor = System.Drawing.SystemColors.ActiveBorder;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "Form1";
            this.Text = "ROV Controller";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            this.panel7.ResumeLayout(false);
            this.panel7.PerformLayout();
            this.panel5.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel5;
        private HslCommunication.Controls.UserCurve userCurve2;
        private HslCommunication.Controls.UserCurve userCurve4;
        private HslCommunication.Controls.UserCurve userCurve3;
        private HslCommunication.Controls.UserCurve userCurve1;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart1;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label_fb;
        private System.Windows.Forms.Label label_wb;
        private System.Windows.Forms.RichTextBox richTextBox1;
        // public System.IO.Ports.SerialPort sp; // removed (no serial)
        private System.Windows.Forms.Label label3;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Panel panel7;
        private System.Windows.Forms.ComboBox cb1;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Panel panel6;
        private System.Windows.Forms.Panel panel8;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox cb_light;
        private System.Windows.Forms.Button bt_save;
        private System.Windows.Forms.Button bt_new;
        private System.Windows.Forms.Button bt_lock;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label16;
        private ROVControlPanel rovControlPanel1;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button bt_light;
    }
}
