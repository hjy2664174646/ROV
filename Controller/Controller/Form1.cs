    using Controller;
    using HidSharp;
    using HslCommunication;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Drawing2D;
using System.Drawing.Text;
    using System.IO;
    // using System.IO.Ports; // 已改为网络通信，不再使用串口
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Windows.Forms.Integration;
    using System.Windows.Forms.DataVisualization.Charting;

    namespace Controller
    {
        public partial class Form1 : Form
        {
            private readonly Stopwatch _highPrecisionTimer = Stopwatch.StartNew();
            private readonly DateTime _startTimeUtc = DateTime.UtcNow;
            private DateTime _recordingStartTime;  // 记录开始时的绝对时间
        public Form1()
        {
            InitializeComponent();
            InitializeDualPageInterface();
            //this.Size = new Size(1478, 885);
            // 设置窗体图标
            this.Icon = Properties.Resources.app_icon;  // app_icon 是你在资源管理器中的名称
        }
            public static List<byte> serial_data = new List<byte>();
            public static List<byte> temp = new List<byte>();
            private const int SerialFrameLength = 140;
            private const double GpsFractionScale = 1000000.0;
            public static byte[] recv = new byte[SerialFrameLength];
            public static int index = 0;
            private const string MutexName = "MutexName";
            public static Mutex mutex = new Mutex();
            public static bool islock = false;
            public static Thread process;
            public static Thread controlthread;
            public static NaviInform navi = new NaviInform();
            public static GamepadState gamepadState = new GamepadState();
            // 设置目标设备的 VID 和 PID
            public static int targetVid = 0x3537; // 设备VID
            public static int targetPid = 0x100f; // 设备PID

            // 1. 固定帧头（0-3字节）：0xAA 55 AA 56
            private static readonly byte[] FrameHeader = { 0xAA, 0x55, 0xAA, 0x56 };
            // 2. 固定帧尾（末尾4字节）：0xAA 57 AA 58
            private static readonly byte[] FrameFooter = { 0xAA, 0x57, 0xAA, 0x58 };
            // 3. 协议总长度（文档定义）
            public const int ProtocolTotalLength = 29;
            private const byte PidFrameCode = 0xF0;
            private const byte PidFrameSignature = 0xA5;
            // HID手柄报文长度（HID PLUS捕获为14字节）
            private const int GamepadPayloadLength = 14;
            private static bool selflockk = false;
            private static float setdeep = 0;
            private static int lightlevel = 0;
            private static bool isLightOn = false;  // 照明开关状态

            public delegate void UIupdate();
            public UIupdate mainupdate;
            private volatile bool _shouldStop = false;
            private volatile bool _stopGamepad = false;
            private DateTime lastUIUpdate = DateTime.MinValue;
            // 数据录制相关变量
            private bool isRecording = false;
            private string currentFileName = "";
            private List<string> recordedDataLines = new List<string>();
            private DateTime recordStartTime;
            private int recordedCount = 0;

            private ThrusterDisplayControl thrusterDisplay; // 推进器状态显示控件
            private GamepadDisplayControl gamepadDisplay; // 手柄状态显示控件
            private BatteryIndicatorControl batteryIndicator; // 电池电量显示控件
            private RovPowerSwitchControl rovPowerSwitch; // ROV 启动开关控件
                private VideoStreamControl videoStreamControl;
                private P30DepthControl p30DepthControl;
            private Panel navigationPanel;
            private Panel mainPageHost;
            private Panel pidPagePanel;
            private Button btnMainPage;
            private Button btnPidPage;
            private Label pidStatusLabel;
            // ✅ 正确：使用元组
            private readonly Dictionary<string, (NumericUpDown kp, NumericUpDown ki, NumericUpDown kd)> pidEditors =new Dictionary<string, (NumericUpDown kp, NumericUpDown ki, NumericUpDown kd)>();
            private readonly Dictionary<string, (decimal kp, decimal ki, decimal kd)> pidDefaultValues = new Dictionary<string, (decimal kp, decimal ki, decimal kd)>();
            private readonly Color pidPrimaryAccent = Color.FromArgb(0, 242, 255);
            private readonly Color pidSecondaryAccent = Color.FromArgb(0, 196, 204);
            private readonly Color pidMutedText = Color.FromArgb(130, 220, 245);
            private readonly Color pidCardBackground = Color.FromArgb(8, 30, 36);
            private readonly Color pidPageBackground = Color.FromArgb(12, 28, 40);
            private readonly string[] pidChannelSequence = { "YawOuter", "YawRate", "RollOuter", "RollRate", "Depth" };
            private enum PidChannelId : byte
            {
                YawOuter = 0x01,
                YawRate = 0x02,
                RollOuter = 0x03,
                RollRate = 0x04,
                Depth = 0x05
            }
            private bool dualPageInitialized;
            private const int NavigationPanelWidth = 160;
            private Button wikiButton;
            private string lastCategories = string.Empty;
            private readonly Dictionary<string, string> _wikiMock = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private List<string> lastCategoryList = new List<string>();
            private FlowLayoutPanel wikiListPanel;
            private static readonly HttpClient httpClient = new HttpClient();
            private MapViewControl mapViewControl; // 地图控件（放在原GPS轨迹区域）
            private ElementHost rovModelHost;
            private RovModelViewer rovModelViewer;
            private const string RovModelRelativePath = "Models\\rov.stl";
            private Control mapViewHostParent;
            private Point mapViewHostLocation = Point.Empty;
            private Size mapViewHostSize = Size.Empty;
            private MapPopupForm mapPopupForm;
            private bool useMapViewHost = false;
            // Map toolbar controls (mini-map actions)
            private Panel mapToolbarPanel;
            private Button btnMapExpand;
            private Button btnMapZoomIn;
            private Button btnMapZoomOut;
            private TcpClient _streamClient;
            private NetworkStream _streamNetwork;
            private Thread _streamThread;
            private CancellationTokenSource _streamCts;
            private readonly object _frameLock = new object();
            private byte[] _latestFrameBytes;
            private Thread _decodeThread;
            private CancellationTokenSource _decodeCts;
            private DateTime _lastDisplayUtc = DateTime.MinValue;
            private DateTime _lastGamepadLogUtc = DateTime.MinValue;
            private DateTime _lastStreamErrorUtc = DateTime.MinValue;
            private volatile bool _streamConnected = false;
            private readonly string _streamServerIp = "192.168.10.2";
            private readonly int _streamServerPort = 8888;
            private bool _useNetworkStream = true;

            // 发送数据录制相关变量
            private bool isRecordingSend = false;
            private string sendDataFileName = "";
            private List<string> sendDataLines = new List<string>();
            private int sendRecordedCount = 0;

            private ROVTelemetryControl telemetryControl; // 添加到数据状态显示面板控件
            private ROVControlPanel controlPanel;//控制面板控件
            private int gpsPointCount = 0;
            private GPSTrajectoryControl gpsTrajectoryControl; // GPS轨迹控件
            // 发送数据定时器
            private static int sendCount = 0;
            // 可选：如果要完全自定义标题栏，可以添加窗体拖动功能
            private bool isDragging = false;
            private Point lastCursor;
            private Point lastForm;
            private CyberWindowControls cyberWindowControls;
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            private readonly object _lockObject = new object();
            private readonly object _receiveBufferLock = new object();
            private readonly object _sendBufferLock = new object();
            private readonly object _networkStreamSendLock = new object();
            private int _inputReportLength = 0;

            // 接收数据记录
            private bool _isRecordingReceive = false;
            private string _receiveDataFileName = "";
            private List<string> _receiveDataBuffer = new List<string>();
            private int _receiveRecordCount = 0;

            // 发送数据记录
            private bool _isRecordingSend = false;
            private string _sendDataFileName = "";
            private List<string> _sendDataBuffer = new List<string>();
            private int _sendRecordCount = 0;
        
            private const int WM_NCLBUTTONDOWN = 0xA1;
            private const int HT_CAPTION = 0x2;
            private void Form1_Load(object sender, EventArgs e)
            {
                this.DoubleBuffered = true; // 启用双缓存的快捷方式
                this.SetStyle(ControlStyles.OptimizedDoubleBuffer |ControlStyles.ResizeRedraw|ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint, true);
                ApplyAppIcon();

                CreateGPSTrajectoryControl();
                ReplaceWithGamepadDisplay();
                AdjustMainPageHostWidth();
//                 comboBox2.DropDown += ComboBox2_DropDown;  // 绑定下拉事件
//                 comboBox2.Items.Add("   ");        // 添加提示信息
//                 comboBox2.SelectedIndex = 0;
                // 检查并绑定按钮事件
                try
                {
                    if (bt_save != null)
                    {
                        // 如果设计器中没有绑定，手动绑定
                        bt_save.Click -= bt_save_Click; // 先移除可能的重复绑定
                        bt_save.Click += bt_save_Click; // 绑定事件
                        Console.WriteLine("bt_save按钮事件已绑定");
                    }
                    else
                    {
                        Console.WriteLine("警告：bt_save按钮未找到！");
                    }

                    if (bt_new != null)
                    {
                        bt_new.Click -= bt_new_Click; // 先移除可能的重复绑定
                        bt_new.Click += bt_new_Click; // 绑定事件
                        Console.WriteLine("bt_new按钮事件已绑定");
                    }
                    else
                    {
                        Console.WriteLine("警告：bt_new按钮未找到！");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"按钮事件绑定错误: {ex.Message}");
                }


            
                // 显示网格线，更容易看清0线
                chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = true;
                chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = true;
                chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
                chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightGray;


                //推进器控件显示
                thrusterDisplay = new ThrusterDisplayControl();
                thrusterDisplay.Size = new Size(680, 200);
                panel4.Controls.Add(thrusterDisplay);
                thrusterDisplay.Location = new Point(45, 10);  // 相对于Panel的位置
                thrusterDisplay.BringToFront();
            
                thrusterDisplay.BackColor = Color.Transparent;  // 临时设置红色背景便于发现

                // 创建科技风数据监控控件
                CreateTelemetryControl();

                // 创建控制面板
                CreateControlPanel();
                // XBOX设备诊断
                DiagnoseGamepadDevices();
                StartGamepadThread();
                // 初始化赛博风格窗口控制按钮
                InitializeCyberWindowControls();
                BindFormEvents();
                EnableWindowDragging();
                AddDragToSpecificControls();
                ReplaceLabelWithCyberStyle();//赛博风文字
                // 初始化照明亮度ComboBox
                if (cb_light != null)
                {
                    cb_light.Items.Clear();
                    cb_light.Items.Add("0");           // 0x00
                    cb_light.Items.Add("1");          // 0x01
                    cb_light.Items.Add("2");          // 0x02
                    cb_light.Items.Add("3");          // 0x03
                    cb_light.Items.Add("4");          // 0x04
                    cb_light.Items.Add("5");          // 0x05
                    cb_light.Items.Add("6");          // 0x06
                    cb_light.Items.Add("7");          // 0x07
                    cb_light.Items.Add("8");          // 0x08
                    cb_light.Items.Add("9");          // 0x09
                    cb_light.Items.Add("10");      // 0x0A

                    cb_light.SelectedIndex = 5; // 默认选择中等亮度5
                }
                // 绑定选择变化事件
                cb_light.SelectedIndexChanged += cb_light_SelectedIndexChanged;
                // 初始化照明按钮状态
                if (bt_light != null)
                {
                    bt_light.Text = "打开照明";
                    //bt_light.BackColor = Color.DodgerBlue;
                }

                InitializeWikiButton();
                InitializeVideoDisplay();
                InitializeRovModelDisplay();
                if (_useNetworkStream)
                {
                    StartNetworkClient();
                }

                // 初始化深度输入框
                if (textBox1 != null)
                {
                    textBox1.Text = "0.0"; // 设置默认深度值
                }

                // 初始化深度锁定按钮
                if (bt_lock != null)
                {
                    bt_lock.Text = "解锁";
                    bt_lock.BackColor = Color.DodgerBlue;
                }
                Console.WriteLine($"设计器设置的尺寸: {this.Size}");
                Console.WriteLine($"WindowState: {this.WindowState}");
            
                var deviceList = DeviceList.Local;
                var device = deviceList.GetHidDevices(targetVid, targetPid).FirstOrDefault();

                

                if (panel2 != null)
                {
                    panel2.Paint -= Panel2_Paint;
                    panel2.Paint += Panel2_Paint;
                    panel2.Resize -= Panel2_Resize;
                    panel2.Resize += Panel2_Resize;
                }

                if (panel7 != null)
                {
                    panel7.Location = new Point(panel7.Left + 10, panel7.Top + 20);
                }
                // UI长宽（保持原固定尺寸，可通过拖动窗口查看超出屏幕的区域）
                this.Size = new Size(1770, 980);
            }

            private void Panel2_Resize(object sender, EventArgs e)
            {
                panel2?.Invalidate();
            }

            private void Panel2_Paint(object sender, PaintEventArgs e)
            {
                if (panel2 == null) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = panel2.ClientRectangle;
                rect.Inflate(-6, -6);
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var borderPath = BuildTechBorderPath(rect, 18))
                using (var glowBrush = new LinearGradientBrush(rect, Color.FromArgb(120, 0, 180, 255), Color.FromArgb(40, 0, 255, 200), LinearGradientMode.Horizontal))
                using (var innerGlowPen = new Pen(glowBrush, 3f))
                using (var outerPen = new Pen(Color.FromArgb(200, 40, 220, 255), 1.6f))
                using (var accentPen = new Pen(Color.FromArgb(220, 140, 255, 210), 1f))
                {
                    innerGlowPen.LineJoin = LineJoin.Round;
                    outerPen.LineJoin = LineJoin.Round;
                    accentPen.LineJoin = LineJoin.Round;

                    g.DrawPath(innerGlowPen, borderPath);
                    g.DrawPath(outerPen, borderPath);

                    DrawTechAccents(g, rect, accentPen);
                }
            }

            private static GraphicsPath BuildTechBorderPath(Rectangle rect, int cornerCut)
            {
                var path = new GraphicsPath();
                int left = rect.Left;
                int right = rect.Right;
                int top = rect.Top;
                int bottom = rect.Bottom;
                int cut = Math.Max(6, cornerCut);

                path.StartFigure();
                path.AddLine(left + cut, top, right - cut, top);
                path.AddLine(right - cut, top, right, top + cut);
                path.AddLine(right, top + cut, right, bottom - cut);
                path.AddLine(right, bottom - cut, right - cut, bottom);
                path.AddLine(right - cut, bottom, left + cut, bottom);
                path.AddLine(left + cut, bottom, left, bottom - cut);
                path.AddLine(left, bottom - cut, left, top + cut);
                path.AddLine(left, top + cut, left + cut, top);
                path.CloseFigure();
                return path;
            }

            private static void DrawTechAccents(Graphics g, Rectangle rect, Pen accentPen)
            {
                int pad = 12;
                int len = 28;
                int gap = 6;
                int left = rect.Left + pad;
                int right = rect.Right - pad;
                int top = rect.Top + pad;
                int bottom = rect.Bottom - pad;

                g.DrawLine(accentPen, left, top, left + len, top);
                g.DrawLine(accentPen, left, top, left, top + len);

                g.DrawLine(accentPen, right - len, top, right, top);
                g.DrawLine(accentPen, right, top, right, top + len);

                g.DrawLine(accentPen, left, bottom, left + len, bottom);
                g.DrawLine(accentPen, left, bottom - len, left, bottom);

                g.DrawLine(accentPen, right - len, bottom, right, bottom);
                g.DrawLine(accentPen, right, bottom - len, right, bottom);

                using (var tickPen = new Pen(Color.FromArgb(140, 120, 255, 255), 1f))
                {
                    g.DrawLine(tickPen, rect.Left + pad + len + gap, rect.Top + 4, rect.Left + pad + len + gap + 18, rect.Top + 4);
                    g.DrawLine(tickPen, rect.Right - pad - len - gap - 18, rect.Top + 4, rect.Right - pad - len - gap, rect.Top + 4);
                }
            }
            private void AddDragToSpecificControls()
            {
                // 例如让panel1也能拖动窗口（如果panel1存在）
                if (panel1 != null)
                {
                    EnableControlDragging(panel1);
                }

                // 或者让标题区域能拖动
                if (label3 != null)
                {
                    EnableControlDragging(label3);
                }

                // 让其他空白区域也能拖动
                if (panel3 != null && controlPanel == null) // 只有当控制面板未创建时才添加
                {
                    EnableControlDragging(panel3);
                }

                Console.WriteLine("特定控件拖动功能已添加");
            }
            /// <summary>
            /// 启用窗体拖动功能（如果隐藏了标题栏）
            /// </summary>
            private void EnableFormDragging()
            {
                // 在Form1的鼠标事件中添加拖动功能
                this.MouseDown += Form1_MouseDown_Drag;
                this.MouseMove += Form1_MouseMove_Drag;
                this.MouseUp += Form1_MouseUp_Drag;
            }

            private void Form1_MouseDown_Drag(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    lastCursor = Control.MousePosition;
                    lastForm = this.Location;
                }
            }
            // 可选：为特定控件也添加拖动功能（比如让Panel也能拖动窗口）
            /// <summary>
            /// 为指定控件添加拖动功能
            /// </summary>
            /// <param name="control">要添加拖动功能的控件</param>
            private void EnableControlDragging(Control control)
            {
                if (control != null)
                {
                    control.MouseDown += (sender, e) => {
                        if (e.Button == MouseButtons.Left)
                        {
                            ReleaseCapture();
                            SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                        }
                    };
                }
            }
            private void Form1_MouseMove_Drag(object sender, MouseEventArgs e)
            {
                if (isDragging)
                {
                    Point currentCursor = Control.MousePosition;
                    this.Location = new Point(
                        lastForm.X + (currentCursor.X - lastCursor.X),
                        lastForm.Y + (currentCursor.Y - lastCursor.Y)
                    );
                }
            }

            private void Form1_MouseUp_Drag(object sender, MouseEventArgs e)
            {
                isDragging = false;
            }

            // 窗体大小改变时调整控件位置
        private void Form1_Resize(object sender, EventArgs e)
        {
                if (cyberWindowControls != null)
                {
                    // 确保控件始终在右上角
                    cyberWindowControls.Location = new Point(
                        this.Width - cyberWindowControls.Width - 10,
                        10
                    );
                }
                PositionVideoDisplay();
                PositionMapView();
                CreateMapToolbar();
                PositionMapToolbar();

        }

        // 调试：在指定经纬度附近（50m范围）生成测试轨迹
        private void DebugTestSmallAreaAt(double lon, double lat)
        {
            try
            {
                if (mapViewControl == null) return;
                // 以圆形路径为例，半径约50米（经纬度换算按纬度近似处理）
                double radiusMeters = 45.0; // 小于50m，避免出圈
                double latRad = lat * Math.PI / 180.0;
                double metersPerDegLat = 111320.0; // 近似
                double metersPerDegLon = 111320.0 * Math.Cos(latRad);
                double dLat = radiusMeters / metersPerDegLat;
                double dLon = radiusMeters / metersPerDegLon;

                int points = 180; // 画一圈
                for (int i = 0; i < points; i++)
                {
                    double theta = (2 * Math.PI) * (i / (double)points);
                    double px = lon + dLon * Math.Cos(theta);
                    double py = lat + dLat * Math.Sin(theta);
                    mapViewControl.AddPoint(px, py);
                    System.Threading.Thread.Sleep(20);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DebugTestSmallAreaAt error: {ex.Message}");
            }
        }
            private void BindFormEvents()
            {
                // 绑定窗体大小改变事件
                this.Resize += Form1_Resize;

                // 如果要启用窗体拖动功能，取消下面的注释
                // EnableFormDragging();
            }
            /// <summary>
            /// 启用窗体拖动功能 - 最简单的实现方式
            /// </summary>
            private void EnableWindowDragging()
            {
                // 为整个窗体添加鼠标按下事件
                this.MouseDown += Form_MouseDown_Drag;

                Console.WriteLine("窗体拖动功能已启用");
            }

            /// <summary>
            /// 窗体拖动事件处理 - 使用Windows API实现
            /// </summary>
            private void Form_MouseDown_Drag(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    // 释放鼠标捕获
                    ReleaseCapture();
                    // 发送窗口移动消息
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            }
            // 如果需要在运行时切换控件的可见性
            public void ShowCyberControls(bool show)
            {
                if (cyberWindowControls != null)
                {
                    cyberWindowControls.Visible = show;
                }
            }

            // 设置控件的透明度（可选）
            public void SetCyberControlsOpacity(float opacity)
            {
                if (cyberWindowControls != null && opacity >= 0.0f && opacity <= 1.0f)
                {
                    // 可以通过设置控件的BackColor alpha值来调整透明度
                    // 或者使用其他方法实现透明效果
                }
            }
            /// <summary>
            /// 初始化赛博风格窗口控制按钮
            /// </summary>
            private bool cyberControlsResizeHooked;

            private void InitializeCyberWindowControls()
            {
                try
                {
                    if (cyberWindowControls == null)
                    {
                        cyberWindowControls = new CyberWindowControls();
                        cyberWindowControls.SetParentForm(this);
                    }

                    if (cyberWindowControls.Parent != this)
                    {
                        cyberWindowControls.Parent?.Controls.Remove(cyberWindowControls);
                        this.Controls.Add(cyberWindowControls);
                    }

                    RepositionCyberWindowControls();
                    cyberWindowControls.BringToFront();

                    if (!cyberControlsResizeHooked)
                    {
                        this.Resize += (_, __) => RepositionCyberWindowControls();
                        cyberControlsResizeHooked = true;
                    }

                    Console.WriteLine("赛博风格窗口控制按钮初始化完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"初始化窗口控制按钮失败: {ex.Message}");
                }
            }

            private void RepositionCyberWindowControls()
            {
                if (cyberWindowControls == null)
                {
                    return;
                }

                var leftMargin = (navigationPanel?.Width ?? 0) + 10;
                var targetX = Math.Min(Math.Max(leftMargin, 10), Math.Max(10, this.ClientSize.Width - cyberWindowControls.Width - 10));
                var targetY = 10;

                cyberWindowControls.Location = new Point(targetX, targetY);
            }
            // 移除GPS轨迹图，并记录原位置给地图使用
            private void CreateGPSTrajectoryControl()
            {
                try
                {
                    // 获取chart1的位置和大小信息
                    var oldLocation = chart1.Location;
                    var oldSize = chart1.Size;
                    var oldParent = chart1.Parent;

                    // 移除原来的chart1（GPS轨迹图）
                    oldParent.Controls.Remove(chart1);
                    useMapViewHost = true;
                    mapViewHostParent = oldParent;
                    mapViewHostLocation = new Point(oldLocation.X + 10, oldLocation.Y + 10);
                    mapViewHostSize = new Size(oldSize.Width+80, oldSize.Height + 40);

                    Console.WriteLine("GPS轨迹图已移除，地图将占用原位置");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"移除GPS轨迹图错误: {ex.Message}");
                }
            }
            // 添加创建控制面板的方法
            private void CreateControlPanel()
            {
                try
                {
                    Console.WriteLine("开始创建控制面板...");

                    controlPanel = new ROVControlPanel();
                    Console.WriteLine("ROVControlPanel实例创建成功");

                    controlPanel.Size = new Size(670, 130);
                    controlPanel.Location = new Point(30, 800);
                    controlPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    controlPanel.Visible = true;

                    Console.WriteLine($"控制面板设置: 大小={controlPanel.Size}, 位置={controlPanel.Location}");

                    controlPanel.LightControlClicked += ControlPanel_LightControlClicked;
                    controlPanel.SaveDataClicked += ControlPanel_SaveDataClicked;
                    controlPanel.NewFileClicked += ControlPanel_NewFileClicked;
                    controlPanel.DepthLockClicked += ControlPanel_DepthLockClicked;
                    controlPanel.LightLevelChanged += ControlPanel_LightLevelChanged;

                    Console.WriteLine("事件绑定完成");

                    GetMainContentHost().Controls.Add(controlPanel);
                    controlPanel.BringToFront();

                    Console.WriteLine("控制面板已添加到窗体");
                    Console.WriteLine("控制面板创建完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建控制面板失败: {ex.Message}");
                    Console.WriteLine($"错误详情: {ex.StackTrace}");
                    controlPanel = null;
                }
            }
            // 赛博风文字
            private void ReplaceLabelWithCyberStyle()
            {
                if (label3 != null)
                {
                    // 保存原始属性
                    var oldLocation = label3.Location;
                    var oldSize = label3.Size;
                    var oldText = label3.Text;
                    var oldParent = label3.Parent;

                    // 创建新的赛博风label
                    var cyberLabel = new CyberLabel();
                    cyberLabel.Location = oldLocation;
                    cyberLabel.Size = oldSize;
                    cyberLabel.Text = oldText.ToUpper(); // 转为大写
                    cyberLabel.Name = "label3_cyber";

                    // 替换控件
                    oldParent.Controls.Remove(label3);
                    oldParent.Controls.Add(cyberLabel);

                    // 更新引用
                    label3 = cyberLabel;
                }
            }
            // 事件处理方法
            // 串口连接按钮回调已移除

            private void ControlPanel_LightControlClicked(object sender, EventArgs e)
            {
                bt_light_Click(sender, e); // 调用原有的照明控制方法
            }

            private void ControlPanel_SaveDataClicked(object sender, EventArgs e)
            {
                bt_save_Click(sender, e); // 调用原有的保存数据方法
            }

            private void ControlPanel_NewFileClicked(object sender, EventArgs e)
            {
                bt_new_Click(sender, e); // 调用原有的新建文件方法
            }

            private void ControlPanel_DepthLockClicked(object sender, EventArgs e)
            {
                bt_lock_Click(sender, e); // 调用原有的深度锁定方法
            }

            private void ControlPanel_LightLevelChanged(object sender, int level)
            {
                lightlevel = level;
                if (isLightOn)
                {
                    Console.WriteLine($"亮度已更新为: {lightlevel}/10");
                }
            }
            private void ReplaceWithGamepadDisplay()
            {
                try
                {
                    // 保存原来的位置和大小
                    var oldParent = richTextBox1.Parent;

                    // 创建新的海洋风格手柄显示控件
                    gamepadDisplay = new GamepadDisplayControl();
                    var absoluteLocation = new Point(750, 900);
                    gamepadDisplay.Location = absoluteLocation;
                    gamepadDisplay.Size = new Size(100, 40);
                    gamepadDisplay.MinimumSize = new Size(100, 40);
                    gamepadDisplay.MaximumSize = new Size(100, 40);
                    gamepadDisplay.AutoSize = false;
                    gamepadDisplay.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    gamepadDisplay.BackColor = Color.Transparent;

                    // 移除原来的richTextBox1并添加新控件
                    oldParent.Controls.Remove(richTextBox1);
                    var absoluteParent = GetMainContentHost();
                    absoluteParent.Controls.Add(gamepadDisplay);
                    gamepadDisplay.BringToFront();

                    // 电池显示放在手柄控件右侧同一行
                    batteryIndicator = new BatteryIndicatorControl(6);
                    batteryIndicator.Location = new Point(gamepadDisplay.Right + 25, gamepadDisplay.Top - 4);
                    batteryIndicator.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    batteryIndicator.BackColor = Color.Transparent;
                    batteryIndicator.UpdateVoltage(navi?.voltage ?? 0);
                    absoluteParent.Controls.Add(batteryIndicator);
                    batteryIndicator.BringToFront();

                    rovPowerSwitch = new RovPowerSwitchControl();
                    rovPowerSwitch.Size = new Size(136, 41);
                    rovPowerSwitch.Location = new Point(
                        batteryIndicator.Right - 15,
                        batteryIndicator.Top + (batteryIndicator.Height - rovPowerSwitch.Height) / 2 + 2  );
                    rovPowerSwitch.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    rovPowerSwitch.BackColor = Color.Transparent;
                    rovPowerSwitch.IsOn = false; // 默认待机
                    rovPowerSwitch.PowerStateChanged += (s, e) =>
                    {
                        Console.WriteLine($"ROV电源状态: {(rovPowerSwitch.IsOn ? "启动" : "待机")}");
                    };
                    absoluteParent.Controls.Add(rovPowerSwitch);
                    rovPowerSwitch.BringToFront();

                    Console.WriteLine("海洋风格手柄显示控件创建完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"替换手柄显示控件错误: {ex.Message}");
                }
            }
        /// <summary>
        /// 获取高精度时间戳，精度达到1毫秒
        /// 使用Stopwatch确保单调递增和高精度
        /// </summary>
        private string GetHighPrecisionTimestamp()
        {
            // 使用Stopwatch的ElapsedMilliseconds确保精度
            long elapsedMilliseconds = _highPrecisionTimer.ElapsedMilliseconds;

            // 计算当前的绝对时间
            DateTime currentTime = _startTimeUtc.AddMilliseconds(elapsedMilliseconds);

            // 返回格式化的时间戳，精确到毫秒
            return currentTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        private void CreateTelemetryControl()
            {
                try
                {
                    telemetryControl = new ROVTelemetryControl();

                    // 设置控件填满panel5的大小
                    telemetryControl.Location = new Point(0, 0);  // 相对于panel5的位置
                    telemetryControl.Size = panel5.Size;          // 使用panel5的大小
                    telemetryControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; // 自动调整大小

                    // 添加到panel5
                    panel5.Controls.Add(telemetryControl);
                    panel5.BringToFront();
                    telemetryControl.BringToFront();

                    Console.WriteLine("科技风数据监控控件已嵌入到panel5");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建数据监控控件错误: {ex.Message}");
                }
            }
            private double GetElapsedMilliseconds()
            {
                return _highPrecisionTimer.Elapsed.TotalMilliseconds;
            }
        private void RecordSendDataHighPrecision(byte[] data)
        {
            if (!_isRecordingSend || string.IsNullOrEmpty(_sendDataFileName))
                return;

            try
            {
                // ★ 获取高精度时间戳
                string timestamp = GetHighPrecisionTimestamp();
                double elapsedMs = GetElapsedMilliseconds();

                // 将字节数组转换为十六进制字符串
                string hexData = BitConverter.ToString(data).Replace("-", " ");

                // 解析数据字段
                string actionCode = data[4].ToString("X2");
                string selfLock = data[6] == 0x01 ? "ON" : "OFF";

                // 摇杆数据
                ushort leftX = BitConverter.ToUInt16(data, 7);
                ushort leftY = BitConverter.ToUInt16(data, 9);
                ushort rightX = BitConverter.ToUInt16(data, 11);
                ushort rightY = BitConverter.ToUInt16(data, 13);

                // 深度控制
                bool depthLock = data[15] == 0x01;
                float targetDepth = BitConverter.ToSingle(data, 16);
                byte lightLevel = data[20];

                // 校验和
                uint checksum = BitConverter.ToUInt32(data, 21);

                // 构建CSV数据行
                string dataLine = $"{timestamp}," +                    // 绝对时间戳(1ms精度)
                                 $"{elapsedMs:F3}," +                  // 相对时间(ms)
                                 $"{sendCount}," +                     // 发送计数
                                 $"{actionCode}," +                    // 动作控制（机械臂/相机）
                                 $"{selfLock}," +                      // 自锁
                                 $"{leftX},{leftY}," +                 // 左摇杆
                                 $"{rightX},{rightY}," +               // 右摇杆
                                 $"{(depthLock ? "LOCKED" : "UNLOCKED")}," +  // 深度锁定
                                 $"{targetDepth:F2}," +                // 目标深度
                                 $"{lightLevel}," +                    // 照明亮度
                                 $"{checksum}," +                      // 校验和
                                 $"\"{hexData}\"";                     // 原始数据

                lock (_sendBufferLock)
                {
                    _sendDataBuffer.Add(dataLine);
                    _sendRecordCount++;

                    // 每50条记录保存一次
                    if (_sendDataBuffer.Count >= 50)
                    {
                        SaveSendDataToFileHighPrecision();
                        _sendDataBuffer.Clear();
                    }
                }

                // 调试输出（每1000条输出一次）
                if (_sendRecordCount % 1000 == 0)
                {
                    Console.WriteLine($"[高精度记录] 已记录 {_sendRecordCount} 条发送数据, 最新时间戳: {timestamp}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"记录发送数据错误: {ex.Message}");
            }
        }
        private void CreateSendDataFileHighPrecision()
        {
            _sendDataFileName = Path.Combine(
                Path.GetDirectoryName(currentFileName) ?? "",
                $"SendData_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            );

            using (StreamWriter writer = new StreamWriter(_sendDataFileName, false, Encoding.UTF8))
            {
                // 写入详细的文件头信息
                writer.WriteLine("# ===============================================");
                writer.WriteLine("# ROV控制指令发送记录（高精度时间戳）");
                writer.WriteLine("# ===============================================");
                writer.WriteLine($"# 创建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                writer.WriteLine("# 数据格式: CSV");
                writer.WriteLine("# 协议版本: 29字节上位机发送协议");
                writer.WriteLine("# 时间戳精度: 1毫秒");
                writer.WriteLine("# 计时器类型: System.Diagnostics.Stopwatch");
                writer.WriteLine($"# 计时器频率: {Stopwatch.Frequency} Hz");
                writer.WriteLine($"# 高精度支持: {Stopwatch.IsHighResolution}");
                writer.WriteLine("# -----------------------------------------------");
                writer.WriteLine("# 数据列说明:");
                writer.WriteLine("# TimeStamp_ms: 绝对时间戳 (yyyy-MM-dd HH:mm:ss.fff)");
                writer.WriteLine("# ElapsedTime_ms: 相对时间，从记录开始的毫秒数");
                writer.WriteLine("# SendCount: 发送计数");
                writer.WriteLine("# CameraControl: 动作控制码 (HEX，机械臂/相机)");
                writer.WriteLine("# SelfLock: 自锁状态 (ON/OFF)");
                writer.WriteLine("# LeftStickX/Y: 左摇杆值");
                writer.WriteLine("# RightStickX/Y: 右摇杆值");
                writer.WriteLine("# DepthLock: 深度锁定状态 (LOCKED/UNLOCKED)");
                writer.WriteLine("# TargetDepth_m: 目标深度 (m)");
                writer.WriteLine("# LightLevel: 照明亮度 (0-10)");
                writer.WriteLine("# Checksum: 校验和");
                writer.WriteLine("# RawData_HEX: 原始十六进制数据");
                writer.WriteLine("# ===============================================");
                writer.WriteLine("#");

                // CSV列标题
                string header = "TimeStamp_ms," +
                               "ElapsedTime_ms," +
                               "SendCount," +
                               "CameraControl," +
                               "SelfLock," +
                               "LeftStickX,LeftStickY," +
                               "RightStickX,RightStickY," +
                               "DepthLock," +
                               "TargetDepth_m," +
                               "LightLevel," +
                               "Checksum," +
                               "RawData_HEX";

                writer.WriteLine(header);
            }

            Console.WriteLine($"发送数据记录文件已创建（高精度）: {_sendDataFileName}");
        }

        // ========== 保存发送数据（高精度版本）==========
        private void SaveSendDataToFileHighPrecision()
        {
            if (string.IsNullOrEmpty(_sendDataFileName) || _sendDataBuffer.Count == 0)
                return;

            try
            {
                using (StreamWriter writer = new StreamWriter(_sendDataFileName, true, Encoding.UTF8))
                {
                    foreach (string line in _sendDataBuffer)
                    {
                        writer.WriteLine(line);
                    }
                    writer.Flush(); // 确保数据写入磁盘
                }

                Console.WriteLine($"[高精度保存] 已保存 {_sendDataBuffer.Count} 条发送记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存发送数据错误: {ex.Message}");
            }
        }

        // ========== 高精度记录接收数据的方法 ==========
        private void RecordReceiveDataHighPrecision()
        {
            if (!_isRecordingReceive || string.IsNullOrEmpty(_receiveDataFileName))
                return;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 构建CSV数据行
                string dataLine = $"{timestamp}," +                    // 绝对时间戳(秒精度)
                                 $"{navi.recvcnt}," +                  // 接收计数
                                 $"{navi.time:F3}," +                   // 系统时间
                                 $"{navi.wb[0]:F6},{navi.wb[1]:F6},{navi.wb[2]:F6}," +  // 陀螺仪
                                 $"{navi.fb[0]:F6},{navi.fb[1]:F6},{navi.fb[2]:F6}," +  // 加速度计
                                 $"{navi.Magn[0]:F6},{navi.Magn[1]:F6},{navi.Magn[2]:F6}," +  // 磁力计
                                 $"{navi.INS_att[0]:F6},{navi.INS_att[1]:F6},{navi.INS_att[2]:F6}," +  // 姿态
                                 $"{navi.thrusterValues[0]:F6},{navi.thrusterValues[1]:F6},{navi.thrusterValues[2]:F6}," +
                                 $"{navi.thrusterValues[3]:F6},{navi.thrusterValues[4]:F6},{navi.thrusterValues[5]:F6}," +  // 推进器
                                 $"{navi.GPS_vel:F6}," +               // GPS速度
                                 $"{navi.GPS_pos[0]:F10},{navi.GPS_pos[1]:F10},{navi.GPS_pos[2]:F6}," +  // GPS位置
                                 $"{navi.GPS_status:F2}," +            // GPS状态
                                 $"{navi.waterPressure:F2}," +         // 水压
                                 $"{navi.deep:F6}," +                  // 深度
                                 $"{navi.p30Confidence:F3}," +         // P30置信度
                                 $"{navi.tempearature:F2}," +          // 温度
                                 $"{navi.current:F3}," +               // 电流
                                 $"{navi.voltage:F3}";                 // 电压

                lock (_receiveBufferLock)
                {
                    _receiveDataBuffer.Add(dataLine);
                    _receiveRecordCount++;

                    // 每50条记录保存一次，平衡性能和数据安全
                    if (_receiveDataBuffer.Count >= 50)
                    {
                        SaveReceiveDataToFileHighPrecision();
                        _receiveDataBuffer.Clear();
                    }
                }

                // 调试输出（每1000条输出一次）
                if (_receiveRecordCount % 1000 == 0)
                {
                    Console.WriteLine($"已记录 {_receiveRecordCount} 条接收数据, 最新时间戳: {timestamp}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"记录接收数据错误: {ex.Message}");
            }
        }

        #region 网络视频/遥测接收
        private void InitializeWikiButton()
        {
            if (wikiButton != null) return;
            wikiButton = new Button
            {
                Text = "百科",
                Size = new Size(60, 26),
                BackColor = Color.FromArgb(30, 40, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                TabStop = false
            };
            wikiButton.FlatAppearance.BorderSize = 1;
            wikiButton.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 220);
            wikiButton.Click += async (s, e) => await ShowWikiPopupAsync();
            GetMainContentHost().Controls.Add(wikiButton);
            wikiButton.BringToFront();
            PositionWikiButton();
        }

        private void PositionWikiButton()
        {
            if (wikiButton == null || videoStreamControl == null) return;
            int x = videoStreamControl.Right - wikiButton.Width - 10;
            int y = videoStreamControl.Bottom - wikiButton.Height - 10;
            wikiButton.Location = new Point(x, y);
            wikiButton.BringToFront();
        }

        private void InitializeWikiListPanel()
        {
            if (wikiListPanel != null) return;
            wikiListPanel = new FlowLayoutPanel
            {
                AutoSize = false,
                Size = new Size(200, 70),
                BackColor = Color.FromArgb(20, 30, 45),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(6),
                Margin = new Padding(0),
                Visible = false
            };
            GetMainContentHost().Controls.Add(wikiListPanel);
            wikiListPanel.BringToFront();
            PositionWikiListPanel();
        }

        private void PositionWikiListPanel()
        {
            if (wikiListPanel == null || videoStreamControl == null) return;
            int x = videoStreamControl.Left + 8;
            int y = videoStreamControl.Bottom - wikiListPanel.Height - 10;
            wikiListPanel.Location = new Point(x, y);
            wikiListPanel.BringToFront();
        }

        private void InitializeVideoDisplay()
        {
            if (videoStreamControl != null)
            {
                return;
            }

            videoStreamControl = new VideoStreamControl
            {
                Size = new Size(445, 350)
            };
            GetMainContentHost().Controls.Add(videoStreamControl);
            videoStreamControl.BringToFront();
            PositionVideoDisplay();
            InitializeMapView();
            InitializeRovModelDisplay();
            InitializeP30DepthControl();
            PositionWikiButton();
            PositionWikiListPanel();
        }

        private void PositionVideoDisplay()
        {
            if (videoStreamControl == null)
            {
                return;
            }
            int top = 60;
            videoStreamControl.Location = new Point(755, top);
            PositionP30DepthControl();
            PositionWikiButton();
            PositionWikiListPanel();
            PositionRovModelView();
        }

        private void InitializeP30DepthControl()
        {
            if (p30DepthControl != null)
            {
                return;
            }
            p30DepthControl = new P30DepthControl();
            GetMainContentHost().Controls.Add(p30DepthControl);
            p30DepthControl.BringToFront();
            PositionP30DepthControl();
        }

        private void PositionP30DepthControl()
        {
            if (p30DepthControl == null || videoStreamControl == null)
            {
                return;
            }
            int margin = 12;
            p30DepthControl.Location = new Point(videoStreamControl.Right + margin, videoStreamControl.Top);
        }

        // 初始化地图控件（使用原GPS轨迹区域）
        private void InitializeMapView()
        {
            try
            {
                if (mapViewControl != null) return;

                mapViewControl = new MapViewControl
                {
                    Provider = "amap",    // 可改为 "baidu"
                    AmapKey = "ca85bc9f93a025b1af08a41e7032a228", // 使用你提供的高德Key
                    BaiduAk = ""
                };
                if (useMapViewHost && mapViewHostParent != null)
                {
                    mapViewHostParent.Controls.Add(mapViewControl);
                    mapViewControl.Location = mapViewHostLocation;
                    mapViewControl.Size = mapViewHostSize;
                }
                else
            {
                GetMainContentHost().Controls.Add(mapViewControl);
                PositionMapView();
            }
            mapViewControl.BringToFront();
            mapViewControl.ExpandRequested += MapViewControl_ExpandRequested;
            PreloadMapPopupForm();

                // 默认以长荡湖为初始视角（在地图HTML中通过查询参数传入），此处不再生成测试圆轨迹
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化地图控件失败: {ex.Message}");
            }
        }


        private void InitializeRovModelDisplay()
        {
            if (rovModelHost != null)
            {
                return;
            }

            rovModelViewer = new RovModelViewer();
            rovModelHost = new ElementHost
            {
                BackColor = Color.Transparent,
                Size = new Size(380, 330),
                Child = rovModelViewer
            };

            GetMainContentHost().Controls.Add(rovModelHost);
            rovModelHost.BringToFront();
            PositionRovModelView();
            LoadRovModel();
        }

        private void PositionRovModelView()
        {
            if (rovModelHost == null || videoStreamControl == null)
            {
                return;
            }

            var host = GetMainContentHost();
            if (host == null)
            {
                return;
            }

            const int margin = 12;
            int targetY = Math.Max(videoStreamControl.Top, margin);
            int x;
            if (panel7 != null && panel7.Parent == host)
            {
                targetY = panel7.Top;
                var panelRight = panel7.Right;
                x = Math.Min(host.ClientSize.Width - rovModelHost.Width - margin, panelRight + margin);
                if (x < margin)
                {
                    x = Math.Max(margin, host.ClientSize.Width - rovModelHost.Width - margin);
                }
            }
            else
            {
                int baseX = videoStreamControl.Right + margin;
                int availableX = Math.Max(host.ClientSize.Width - rovModelHost.Width - margin, margin);
                x = Math.Min(baseX, Math.Max(margin, availableX));
            }
            rovModelHost.Location = new Point(x, targetY);
        }

        private void LoadRovModel()
        {
            if (rovModelViewer == null)
            {
                return;
            }

            try
            {
                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RovModelRelativePath);
                rovModelViewer.LoadModel(modelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载ROV模型失败: {ex.Message}");
            }
        }

        private void PositionMapView()
        {
            if (mapViewControl == null) return;

            if (useMapViewHost && mapViewHostParent != null)
            {
                if (mapViewControl.Parent != mapViewHostParent)
                {
                    mapViewControl.Parent?.Controls.Remove(mapViewControl);
                    mapViewHostParent.Controls.Add(mapViewControl);
                }
                mapViewControl.Location = mapViewHostLocation;
                mapViewControl.Size = mapViewHostSize;
                return;
            }

            if (videoStreamControl == null) return;

            int margin = 10;
            mapViewControl.Width = videoStreamControl.Width + 20;
            mapViewControl.Height = 330; // 固定高度，可按需调整
            int x = videoStreamControl.Left;
            int y = videoStreamControl.Bottom + margin;
            mapViewControl.Location = new Point(x, y);
        }

        private void MapViewControl_ExpandRequested(object sender, EventArgs e)
        {
            try
            {
                double lon = mapViewControl.CenterLongitude;
                double lat = mapViewControl.CenterLatitude;
                if (mapViewControl.TryGetLastPoint(out double lastLon, out double lastLat))
                {
                    lon = lastLon; lat = lastLat;
                }
                EnsureMapPopupForm(lon, lat);
                UpdateMapPopupCenter(lon, lat);
                mapPopupForm?.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Map expand failed: {ex.Message}");
            }
        }

        private void PreloadMapPopupForm()
        {
            if (mapPopupForm != null || mapViewControl == null) return;
            double lon = mapViewControl.CenterLongitude;
            double lat = mapViewControl.CenterLatitude;
            mapPopupForm = new MapPopupForm(mapViewControl.Provider, mapViewControl.AmapKey, mapViewControl.BaiduAk, lon, lat);
        }

        private void EnsureMapPopupForm(double centerLon, double centerLat)
        {
            if (mapPopupForm == null)
            {
                mapPopupForm = new MapPopupForm(mapViewControl.Provider, mapViewControl.AmapKey, mapViewControl.BaiduAk, centerLon, centerLat);
            }
        }

        private void UpdateMapPopupCenter(double centerLon, double centerLat)
        {
            if (mapPopupForm == null) return;
            mapPopupForm.MapView.CenterLongitude = centerLon;
            mapPopupForm.MapView.CenterLatitude = centerLat;
        }

        private void StartNetworkClient()
        {
            StopNetworkClient();
            _decodeCts = new CancellationTokenSource();
            _decodeThread = new Thread(() => DecodeLoop(_decodeCts.Token))
            {
                IsBackground = true,
                Name = "FrameDecoder"
            };
            _decodeThread.Start();
            _streamCts = new CancellationTokenSource();
            _streamThread = new Thread(() => RunNetworkClient(_streamCts.Token))
            {
                IsBackground = true,
                Name = "NetworkStreamReader"
            };
            _streamThread.Start();
        }

        private void StopNetworkClient()
        {
            try
            {
                _decodeCts?.Cancel();
                if (_decodeThread != null && _decodeThread.IsAlive)
                {
                    _decodeThread.Join(500);
                }
                _streamCts?.Cancel();
                if (_streamThread != null && _streamThread.IsAlive)
                {
                    _streamThread.Join(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止网络线程错误: {ex.Message}");
            }
            finally
            {
                _streamThread = null;
                _streamCts = null;
                _decodeThread = null;
                _decodeCts = null;
                try { _streamNetwork?.Dispose(); } catch { }
                try { _streamClient?.Close(); } catch { }
                _streamNetwork = null;
                _streamClient = null;
                _streamConnected = false;
            }
        }

        private void RunNetworkClient(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    UpdateVideoStatus("Video: 连接中...");
                    _streamClient = new TcpClient();
                    _streamClient.NoDelay = true;
                    _streamClient.Connect(_streamServerIp, _streamServerPort);
                    _streamNetwork = _streamClient.GetStream();
                    _streamNetwork.ReadTimeout = 5000;
                    _streamNetwork.WriteTimeout = 2000;
                    _streamConnected = true;
                    UpdateVideoStatus("Video: 已连接");

                    while (!token.IsCancellationRequested)
                    {
                        if (!ReadPacketFromStream(_streamNetwork, token))
                        {
                            throw new IOException("连接已断开");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        LogStreamError($"网络接收错误: {ex.Message}");
                        UpdateVideoStatus($"Video: 断开 ({ex.Message})");
                    }
                }
                finally
                {
                    CloseStreamConnection("reader loop end");
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                Thread.Sleep(1000);
            }

            UpdateVideoStatus("Video: 已停止");
        }

        private bool ReadPacketFromStream(NetworkStream stream, CancellationToken token)
        {
            if (stream == null)
            {
                return false;
            }

            byte[] lenBuf = new byte[4];
            if (!ReadExact(stream, lenBuf, 0, 4, token))
            {
                return false;
            }
            int jsonLen = ReadInt32BE(lenBuf);
            if (jsonLen <= 0 || jsonLen > 4 * 1024 * 1024)
            {
                throw new InvalidDataException($"无效的JSON长度: {jsonLen}");
            }
            byte[] jsonBytes = new byte[jsonLen];
            if (!ReadExact(stream, jsonBytes, 0, jsonLen, token))
            {
                return false;
            }
            string jsonText = Encoding.UTF8.GetString(jsonBytes);

            if (!ReadExact(stream, lenBuf, 0, 4, token))
            {
                return false;
            }
            int imgLen = ReadInt32BE(lenBuf);
            if (imgLen < 0 || imgLen > 10 * 1024 * 1024)
            {
                throw new InvalidDataException($"无效的图像长度: {imgLen}");
            }
            byte[] imgBytes = null;
            if (imgLen > 0)
            {
                imgBytes = new byte[imgLen];
                if (!ReadExact(stream, imgBytes, 0, imgLen, token))
                {
                    return false;
                }
            }

            HandleStreamPacket(jsonText, imgBytes);
            return true;
        }

        private bool ReadExact(NetworkStream stream, byte[] buffer, int offset, int size, CancellationToken token)
        {
            int total = 0;
            while (total < size && !token.IsCancellationRequested)
            {
                int read = stream.Read(buffer, offset + total, size - total);
                if (read <= 0)
                {
                    return false;
                }
                total += read;
            }
            return total == size;
        }

        private int ReadInt32BE(byte[] buffer)
        {
            return (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        }

        // 兼容旧调用：将串口解析入口转到统一的网络帧解析
        private void dataconv()
        {
            try { ParseTelemetryFrame(recv); } catch { }
        }

        private void ParseTelemetryFrame(byte[] frame)
        {
            try
            {
                if (frame == null) return;
                if (frame.Length < 32) return;
                int offset = 0;
                if (frame.Length >= SerialFrameLength && frame[0] == 0xAA && frame[1] == 0x55 && frame[2] == 0xAA && frame[3] == 0x56)
                {
                    offset = 4; // 跳过帧头
                }
                // timestamp
                if (offset + 4 <= frame.Length) { navi.time = BitConverter.ToSingle(frame, offset); offset += 4; }
                // gyro
                for (int i = 0; i < 3 && offset + 4 <= frame.Length; i++) { navi.wb[i] = BitConverter.ToSingle(frame, offset); offset += 4; }
                // acc
                for (int i = 0; i < 3 && offset + 4 <= frame.Length; i++) { navi.fb[i] = BitConverter.ToSingle(frame, offset); offset += 4; }
                // mag
                for (int i = 0; i < 3 && offset + 4 <= frame.Length; i++) { navi.Magn[i] = BitConverter.ToSingle(frame, offset); offset += 4; }
                // angle
                for (int i = 0; i < 3 && offset + 4 <= frame.Length; i++) { navi.INS_att[i] = BitConverter.ToSingle(frame, offset); offset += 4; }
                // thrusters int32
                for (int i = 0; i < 6 && offset + 4 <= frame.Length; i++) { navi.thrusterValues[i] = BitConverter.ToInt32(frame, offset); offset += 4; }
                // gps speed (int32 -> m/s, assuming cm/s)
                if (offset + 4 <= frame.Length) { navi.GPS_vel = BitConverter.ToInt32(frame, offset) / 100.0f; offset += 4; }
                // lon/lat parts + alt
                if (offset + 4 * 5 <= frame.Length)
                {
                    int lon_int = BitConverter.ToInt32(frame, offset); offset += 4;
                    int lon_frac = BitConverter.ToInt32(frame, offset); offset += 4;
                    int lat_int = BitConverter.ToInt32(frame, offset); offset += 4;
                    int lat_frac = BitConverter.ToInt32(frame, offset); offset += 4;
                    navi.GPS_pos[2] = BitConverter.ToSingle(frame, offset); offset += 4;
                    navi.GPS_pos[0] = DecodeGpsCoordinate(lon_int, lon_frac);
                    navi.GPS_pos[1] = DecodeGpsCoordinate(lat_int, lat_frac);
                }
                // gps status
                if (offset + 4 <= frame.Length) { navi.GPS_status = BitConverter.ToSingle(frame, offset); offset += 4; }
                // water pressure
                if (offset + 4 <= frame.Length) { navi.waterPressure = BitConverter.ToSingle(frame, offset); offset += 4; navi.deep = navi.waterPressure; }
                // temperature/current/voltage
                if (offset + 4 <= frame.Length) { navi.tempearature = BitConverter.ToSingle(frame, offset); offset += 4; }
                if (offset + 4 <= frame.Length) { navi.current = BitConverter.ToSingle(frame, offset); offset += 4; }
                if (offset + 4 <= frame.Length) { navi.voltage = BitConverter.ToSingle(frame, offset); offset += 4; }
                // P30 depth fields
                bool hasP30Distance = false;
                if (offset + 4 <= frame.Length) { navi.p30Distance = BitConverter.ToSingle(frame, offset); offset += 4; hasP30Distance = true; }
                if (offset + 4 <= frame.Length) { navi.p30Confidence = BitConverter.ToSingle(frame, offset); offset += 4; }
                if (hasP30Distance)
                {
                    p30DepthControl?.AddSample(navi.p30Distance, navi.p30Confidence);
                }

                navi.recvcnt++;

                if (navi.recvcnt == 1 || (navi.recvcnt % 20) == 0)
                {
                    LogTelemetrySnapshot();
                }

                if (_isRecordingReceive)
                {
                    RecordReceiveDataHighPrecision();
                }

                if ((DateTime.Now - lastUIUpdate).TotalMilliseconds > 100)
                {
                    lastUIUpdate = DateTime.Now;
                    if (this.InvokeRequired) this.BeginInvoke(new Action(UIShow));
                    else UIShow();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据解析异常: {ex.Message}");
            }
        }

        private static double DecodeGpsCoordinate(int integerPart, int fractionalPart)
        {
            int sign = Math.Sign(integerPart);
            if (sign == 0)
            {
                sign = Math.Sign(fractionalPart);
            }
            double absInteger = Math.Abs(integerPart);
            double absFraction = Math.Abs(fractionalPart);
            double decimalDegrees = absInteger + absFraction / GpsFractionScale;
            return sign * decimalDegrees;
        }

        private void HandleStreamPacket(string jsonText, byte[] imageBytes)
        {
            // 确保在 UI 线程执行与控件相关的操作，避免跨线程异常
            if (this.InvokeRequired)
            {
                var jsonCopy = jsonText;
                byte[] imgCopy = imageBytes != null ? (byte[])imageBytes.Clone() : null;
                this.BeginInvoke(new Action(() => HandleStreamPacket(jsonCopy, imgCopy)));
                return;
            }
            try
            {
                if (!string.IsNullOrEmpty(jsonText))
                {
                    var root = JObject.Parse(jsonText);
                    string categoriesText = string.Empty;

                    if (root["categories"] is JArray cats && cats.Count > 0)
                    {
                        lastCategoryList = cats.Select(c => c?.ToString())
                                               .Where(s => !string.IsNullOrWhiteSpace(s))
                                               .Select(s => s.Trim())
                                               .Distinct(StringComparer.OrdinalIgnoreCase)
                                               .ToList();
                        categoriesText = string.Join("、", lastCategoryList);
                        lastCategories = categoriesText;
                    }

                    var controlToken = root["control"];
                    if (controlToken is JObject controlObj)
                    {
                        string rawHex = controlObj["raw"]?.ToString();
                        if (!string.IsNullOrEmpty(rawHex))
                        {
                            var frameBytes = HexStringToBytes(rawHex);
                        // 接受140/192两种长度，避免因长度不一致丢弃
                            if (frameBytes != null && (frameBytes.Length == recv.Length || frameBytes.Length >= SerialFrameLength))
                            {
                                ParseTelemetryFrame(frameBytes);
                            }
                        }
                    }
                    else if (controlToken is JValue controlVal && controlVal.Type == JTokenType.String)
                    {
                        string rawHex = controlVal.ToString();
                        if (!string.IsNullOrEmpty(rawHex))
                        {
                            var frameBytes = HexStringToBytes(rawHex);
                            if (frameBytes != null && (frameBytes.Length == recv.Length || frameBytes.Length >= SerialFrameLength))
                            {
                                ParseTelemetryFrame(frameBytes);
                            }
                        }
                    }

                    double fps = root["fps"]?.Value<double>() ?? 0.0;
                    string status = string.IsNullOrEmpty(categoriesText)
                        ? $"Video: {fps:F1} FPS"
                        : $"Video: {fps:F1} FPS | 目标: {categoriesText}";
                    UpdateVideoStatus(status);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析JSON失败: {ex.Message}");
            }

            if (imageBytes != null && imageBytes.Length > 0)
            {
                lock (_frameLock)
                {
                    // 只保留最新帧，老帧自动丢弃，避免延时堆积
                    _latestFrameBytes = imageBytes;
                }
            }
        }

        private void UpdateVideoFrame(Image image)
        {
            if (videoStreamControl == null)
            {
                image?.Dispose();
                return;
            }
            videoStreamControl.UpdateFrame(image);
        }

        private void DecodeLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[] frame = null;
                lock (_frameLock)
                {
                    frame = _latestFrameBytes;
                    _latestFrameBytes = null;
                }

                if (frame != null)
                {
                    try
                    {
                        using (var ms = new MemoryStream(frame))
                        using (var bmp = new Bitmap(ms))
                        {
                            // 限制显示频率，避免 UI 过载导致延迟堆积
                            if ((DateTime.UtcNow - _lastDisplayUtc).TotalMilliseconds >= 50)
                            {
                                _lastDisplayUtc = DateTime.UtcNow;
                                UpdateVideoFrame((Image)bmp.Clone());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"解码视频帧失败: {ex.Message}");
                    }
                }
                Thread.Sleep(1);
            }
        }

        private void UpdateVideoStatus(string text)
        {
            videoStreamControl?.UpdateStatus(text);
        }

        private async Task ShowWikiPopupAsync(string categoryOverride = null)
        {
            await Task.Yield(); // 保持异步上下文，避免 CS1998
            try
            {
                var categories = lastCategoryList;
                if (categories == null || categories.Count == 0)
                {
                    MessageBox.Show(this, "暂无识别到的目标类别。", "百科", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (Form popup = new Form())
                {
                    var accent = Color.FromArgb(0, 190, 255);
                    popup.Text = "百科";
                    popup.StartPosition = FormStartPosition.CenterParent;
                    popup.Size = new Size(640, 360);
                    popup.FormBorderStyle = FormBorderStyle.None;
                    popup.MaximizeBox = false;
                    popup.MinimizeBox = false;
                    popup.BackColor = Color.FromArgb(8, 12, 20);
                    popup.Padding = new Padding(12);

                    var headerPanel = new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 42,
                        BackColor = Color.FromArgb(12, 18, 32),
                        Padding = new Padding(10, 8, 10, 0)
                    };
                    var headerLabel = new Label
                    {
                        Dock = DockStyle.Left,
                        Width = 220,
                        Text = "CYBER WIKI / 在线百科",
                        ForeColor = accent,
                        Font = new Font("Consolas", 11f, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    var headerHint = new Label
                    {
                        Dock = DockStyle.Fill,
                        Text = "选择标签或直接双击快速打开百科链接",
                        ForeColor = Color.FromArgb(180, 200, 220),
                        Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    var closeButton = new Button
                    {
                        Dock = DockStyle.Right,
                        Width = 36,
                        Text = "×",
                        FlatStyle = FlatStyle.Flat,
                        ForeColor = accent,
                        BackColor = Color.Transparent,
                        FlatAppearance = { BorderSize = 0 },
                        TabStop = false
                    };
                    closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(26, 48, 76);
                    closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(36, 58, 86);
                    closeButton.Click += (s, e) => popup.Close();
                    headerPanel.Controls.Add(headerHint);
                    headerPanel.Controls.Add(closeButton);
                    headerPanel.Controls.Add(headerLabel);

                    var accentLine = new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 2,
                        BackColor = accent
                    };

                    var layout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = 1,
                        BackColor = Color.FromArgb(10, 16, 26)
                    };
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                    var listBox = new ListBox
                    {
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 10f, FontStyle.Bold),
                        BorderStyle = BorderStyle.None,
                        BackColor = Color.FromArgb(14, 22, 36),
                        ForeColor = Color.FromArgb(220, 235, 255),
                        IntegralHeight = false,
                        ItemHeight = 26,
                        DrawMode = DrawMode.OwnerDrawFixed,
                        Margin = new Padding(6, 8, 6, 8)
                    };
                    listBox.Items.AddRange(categories.Cast<object>().ToArray());
                    listBox.DrawItem += (s, e) =>
                    {
                        e.DrawBackground();
                        if (e.Index < 0) return;
                        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                        var bg = selected ? Color.FromArgb(32, accent.R, accent.G, accent.B) : Color.FromArgb(14, 22, 36);
                        using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
                        using (var pen = new Pen(Color.FromArgb(50, accent.R, accent.G, accent.B), 1))
                        {
                            e.Graphics.DrawRectangle(pen, e.Bounds);
                        }
                        TextRenderer.DrawText(
                            e.Graphics,
                            listBox.Items[e.Index]?.ToString() ?? string.Empty,
                            listBox.Font,
                            new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 4, e.Bounds.Width - 14, e.Bounds.Height - 8),
                            selected ? accent : Color.White,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                        e.DrawFocusRectangle();
                    };

                    var textContainer = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.FromArgb(12, 18, 28),
                        Padding = new Padding(10, 8, 10, 6),
                        Margin = new Padding(6, 8, 6, 8)
                    };

                    var textBox = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        Multiline = true,
                        ReadOnly = true,
                        BorderStyle = BorderStyle.None,
                        BackColor = Color.FromArgb(10, 16, 26),
                        ForeColor = Color.FromArgb(220, 235, 255),
                        Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                        ScrollBars = ScrollBars.Vertical,
                        Margin = new Padding(0)
                    };

                    var embedButton = new Button
                    {
                        Dock = DockStyle.Bottom,
                        Height = 28,
                        Text = "内嵌浏览",
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(18, 28, 46),
                        ForeColor = accent,
                        FlatAppearance = { BorderSize = 0 }
                    };
                    embedButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(26, 48, 76);
                    embedButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(36, 58, 86);

                    var linkLabel = new LinkLabel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 24,
                        Text = "打开在线百科",
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(4, 2, 4, 2),
                        LinkBehavior = LinkBehavior.AlwaysUnderline,
                        LinkColor = accent,
                        ActiveLinkColor = accent
                    };
                    string currentLink = null;
                    linkLabel.Click += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(currentLink))
                        {
                            OpenUrlInBrowser(currentLink);
                        }
                    };
                    embedButton.Click += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(currentLink))
                        {
                            ShowEmbeddedEncyclopedia(currentLink);
                        }
                    };

                    var hint = new Label
                    {
                        Dock = DockStyle.Top,
                        Height = 26,
                        Text = "选择一个标签查看百科",
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(4, 4, 4, 4),
                        ForeColor = Color.FromArgb(170, 190, 210),
                        Font = new Font("Segoe UI", 9f, FontStyle.Regular)
                    };

                    textContainer.Controls.Add(textBox);
                    textContainer.Controls.Add(embedButton);
                    textContainer.Controls.Add(linkLabel);
                    textContainer.Controls.Add(hint);

                    layout.Controls.Add(listBox, 0, 0);
                    layout.Controls.Add(textContainer, 1, 0);

                    popup.Controls.Add(layout);
                    popup.Controls.Add(accentLine);
                    popup.Controls.Add(headerPanel);

                    async Task LoadSummary(string tag)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) return;
                        textBox.Text = $"正在查询：{tag} ...";
                        string summary = await FetchEncyclopediaSummaryAsync(tag);
                        string link = await ResolveBaikeUrlAsync(tag);
                        textBox.Text = $"{tag}\r\n\r\n{summary}";
                        currentLink = link;
                        if (string.IsNullOrWhiteSpace(link))
                        {
                            linkLabel.Text = "未找到在线百科链接";
                            linkLabel.Enabled = false;
                            embedButton.Enabled = false;
                        }
                        else
                        {
                            linkLabel.Text = $"打开在线百科：{link}";
                            linkLabel.Enabled = true;
                            embedButton.Enabled = true;
                        }
                    }

                    listBox.SelectedIndexChanged += async (s, e) =>
                    {
                        if (listBox.SelectedItem is string tag)
                        {
                            await LoadSummary(tag);
                        }
                    };

                    listBox.DoubleClick += async (s, e) =>
                    {
                        if (listBox.SelectedItem is string tag)
                        {
                            await OpenEncyclopediaInBrowserAsync(tag);
                        }
                    };

                    // 初始选中：优先传入的 override，其次第一个
                    string initial = !string.IsNullOrWhiteSpace(categoryOverride) && categories.Contains(categoryOverride)
                        ? categoryOverride
                        : categories.FirstOrDefault();
                    popup.Shown += async (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(initial))
                        {
                            listBox.SelectedItem = initial;
                            await LoadSummary(initial);
                        }
                    };

                    popup.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开百科弹窗失败: {ex.Message}");
            }
        }

        private void UpdateWikiListPanel(List<string> categories)
        {
            if (wikiListPanel == null) return;
            wikiListPanel.Controls.Clear();
            if (categories == null || categories.Count == 0)
            {
                wikiListPanel.Visible = false;
                return;
            }

            foreach (var c in categories)
            {
                var btn = new Button
                {
                    Text = c,
                    AutoSize = true,
                    Margin = new Padding(4),
                    Padding = new Padding(6, 2, 6, 2),
                    BackColor = Color.FromArgb(28, 40, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Consolas", 9F, FontStyle.Bold),
                    Tag = c
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 220);
                btn.Click += async (s, e) =>
                {
                    var tag = (s as Button)?.Tag as string;
                    await OpenEncyclopediaInBrowserAsync(tag);
                };
                wikiListPanel.Controls.Add(btn);
            }
            wikiListPanel.Visible = true;
            PositionWikiListPanel();
        }

        private async Task<List<string>> BuildTitleCandidatesAsync(string title)
        {
            var titleCandidates = new List<string>();
            if (string.IsNullOrWhiteSpace(title))
            {
                return titleCandidates;
            }

            string normalized = title.Replace("（示例）", "").Replace("(示例)", "").Trim();
            string lower = normalized.ToLowerInvariant();
            string titleCase = char.IsLetter(normalized.FirstOrDefault())
                ? char.ToUpper(normalized.First()) + normalized.Substring(1).ToLower()
                : normalized;

            void AddCandidate(string candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return;
                }
                if (!titleCandidates.Any(c => c.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    titleCandidates.Add(candidate);
                }
            }

            AddCandidate(normalized);
            AddCandidate(lower);
            AddCandidate(titleCase);
            if (!lower.EndsWith("s"))
            {
                AddCandidate(lower + "s");
            }
            AddCandidate(lower.Replace(' ', '_'));
            AddCandidate(lower.Replace(' ', '-'));

            // 如果是英文标签，尝试从 Wikipedia 获取中文标题并加入候选
            if (IsLikelyEnglish(lower))
            {
                var zhFromWiki = await TryGetChineseTitleFromWikipediaAsync(normalized);
                if (!string.IsNullOrWhiteSpace(zhFromWiki))
                {
                    AddCandidate(zhFromWiki);
                }
            }

            return titleCandidates;
        }

        private bool IsLikelyEnglish(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            return text.All(c => c < 128) && !text.Any(c => c >= 0x4e00 && c <= 0x9fff);
        }

        private async Task<string> TryGetChineseTitleFromWikipediaAsync(string englishTitle)
        {
            try
            {
                var url = $"https://en.wikipedia.org/w/api.php?action=query&titles={Uri.EscapeDataString(englishTitle)}&prop=langlinks&lllang=zh&format=json";
                var resp = await httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                var pages = obj["query"]?["pages"] as JObject;
                if (pages == null) return null;
                foreach (var page in pages.Properties())
                {
                    var langlinks = page.Value["langlinks"] as JArray;
                    if (langlinks != null && langlinks.Count > 0)
                    {
                        var zhTitle = langlinks[0]?["*"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(zhTitle))
                        {
                            return zhTitle;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取中文词条失败: {ex.Message}");
            }
            return null;
        }

        private class BaikeResult
        {
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Url { get; set; }
        }

        private async Task<BaikeResult> FetchBaikeCardAsync(string query)
        {
            try
            {
                var url = $"https://baike.baidu.com/api/openapi/BaikeLemmaCardApi?scope=103&format=json&appid=379020&bk_key={Uri.EscapeDataString(query)}";
                var resp = await httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                var summary = obj["abstract"]?.ToString();
                var title = obj["card_title"]?.ToString();
                var entryUrl = obj["url"]?.ToString();
                if (string.IsNullOrWhiteSpace(entryUrl) && !string.IsNullOrWhiteSpace(title))
                {
                    entryUrl = $"https://baike.baidu.com/item/{Uri.EscapeDataString(title)}";
                }
                if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(entryUrl))
                {
                    return null;
                }
                return new BaikeResult
                {
                    Title = title,
                    Summary = summary,
                    Url = entryUrl
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取百度百科失败: {ex.Message}");
                return null;
            }
        }

        private async Task<string> FetchEncyclopediaSummaryAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "未提供类别名称。";
            }

            var candidates = await BuildTitleCandidatesAsync(title);
            foreach (var candidate in candidates)
            {
                var baike = await FetchBaikeCardAsync(candidate);
                if (baike == null) continue;

                if (!string.IsNullOrWhiteSpace(baike.Summary))
                {
                    return baike.Summary;
                }

                // 有可用链接但没有摘要时，提示用户直接打开链接
                if (!string.IsNullOrWhiteSpace(baike.Url))
                {
                    return "已找到百科链接，点击下方链接或“内嵌浏览”查看详细内容。";
                }
            }

            // 百度百科未命中时，回退到 Wikipedia 摘要（仍然会链接到百度百科搜索）
            return await FetchWikipediaSummaryAsync(title);
        }

        private async Task<string> FetchWikipediaSummaryAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "未提供类别名称。";
            }

            var titleCandidates = await BuildTitleCandidatesAsync(title);
            if (titleCandidates.Count == 0)
            {
                return "未提供类别名称。";
            }

            // 在线百科：对每个候选名尝试中文/英文 Wikipedia 简要摘要
            foreach (var candidate in titleCandidates)
            {
                var urls = new[]
                {
                    $"https://zh.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(candidate)}",
                    $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(candidate)}"
                };
                foreach (var url in urls)
                {
                    try
                    {
                        var resp = await httpClient.GetAsync(url);
                        if (!resp.IsSuccessStatusCode) continue;
                        var json = await resp.Content.ReadAsStringAsync();
                        var obj = JObject.Parse(json);
                        var extract = obj["extract"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(extract))
                        {
                            return extract;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"百科请求失败: {ex.Message}");
                    }
                }
            }

            // 使用 wiki 搜索接口做双语兜底（英文标签也尝试中文搜索）
            var searchSummary = await FetchSummaryViaSearchAsync(titleCandidates);
            if (!string.IsNullOrWhiteSpace(searchSummary))
            {
                return searchSummary;
            }

            return "未找到该类别的百科信息。";
        }

        private async Task<string> FetchSummaryViaSearchAsync(IEnumerable<string> titleCandidates)
        {
            foreach (var lang in new[] { "zh", "en" })
            {
                foreach (var candidate in titleCandidates)
                {
                    string bestTitle = await FetchOpenSearchTopAsync(lang, candidate);
                    if (string.IsNullOrWhiteSpace(bestTitle)) continue;
                    var summary = await FetchSummaryByTitleAsync(lang, bestTitle);
                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        return summary;
                    }
                }
            }
            return null;
        }

        private async Task<string> FetchSummaryByTitleAsync(string lang, string title)
        {
            try
            {
                var url = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
                var resp = await httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                var extract = obj["extract"]?.ToString();
                return string.IsNullOrWhiteSpace(extract) ? null : extract;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"百科搜索摘要失败: {ex.Message}");
                return null;
            }
        }

        private async Task<string> FetchOpenSearchTopAsync(string lang, string query)
        {
            try
            {
                var url = $"https://{lang}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&limit=1&namespace=0&format=json";
                var resp = await httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var arr = JArray.Parse(json);
                if (arr.Count >= 2 && arr[1] is JArray titles && titles.Count > 0)
                {
                    return titles[0]?.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"百科搜索失败: {ex.Message}");
            }
            return null;
        }

        private async Task<string> ResolveBaikeUrlAsync(string title)
        {
            var titleCandidates = await BuildTitleCandidatesAsync(title);
            foreach (var candidate in titleCandidates)
            {
                var baike = await FetchBaikeCardAsync(candidate);
                if (baike != null && !string.IsNullOrWhiteSpace(baike.Url))
                {
                    return baike.Url;
                }
            }

            // 尝试通过英文标题获取中文词条并直接拼出 item 链接，避免停在搜索页
            var zhTitle = await TryGetChineseTitleFromWikipediaAsync(title);
            if (!string.IsNullOrWhiteSpace(zhTitle))
            {
                return $"https://baike.baidu.com/item/{Uri.EscapeDataString(zhTitle)}";
            }

            if (titleCandidates.Count > 0)
            {
                // 最后兜底：直接跳 item，若不存在才退回搜索页
                var encoded = Uri.EscapeDataString(titleCandidates[0]);
                return $"https://baike.baidu.com/item/{encoded}";
            }

            return null;
        }

        private void OpenUrlInBrowser(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法打开百科：{ex.Message}", "百科", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OpenEncyclopediaInBrowserAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(this, "未提供类别名称。", "百科", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var url = await ResolveBaikeUrlAsync(title);
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, $"未能找到 {title} 的在线百科链接。", "百科", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenUrlInBrowser(url);
        }

        private void ShowEmbeddedEncyclopedia(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, "未找到可加载的百科链接。", "百科", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var form = new Form
                {
                    Text = "百度百科浏览器",
                    Size = new Size(900, 600),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = Color.FromArgb(8, 12, 20),
                    FormBorderStyle = FormBorderStyle.None,
                    Padding = new Padding(0)
                };
                var accent = Color.FromArgb(0, 190, 255);
                var header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = Color.FromArgb(12, 18, 32),
                    Padding = new Padding(10, 8, 10, 8)
                };
                var titleLabel = new Label
                {
                    Dock = DockStyle.Left,
                    Width = 200,
                    Text = "百科浏览器",
                    ForeColor = accent,
                    Font = new Font("Consolas", 11f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var closeButton = new Button
                {
                    Dock = DockStyle.Right,
                    Width = 36,
                    Text = "×",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = accent,
                    BackColor = Color.Transparent,
                    FlatAppearance = { BorderSize = 0 },
                    TabStop = false
                };
                closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(26, 48, 76);
                closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(36, 58, 86);
                closeButton.Click += (s, e) => form.Close();
                header.Controls.Add(closeButton);
                header.Controls.Add(titleLabel);

                var browser = new WebBrowser
                {
                    Dock = DockStyle.Fill,
                    ScriptErrorsSuppressed = true
                };
                browser.Navigate(url);

                var accentLine = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 2,
                    BackColor = accent
                };

                form.Controls.Add(browser);
                form.Controls.Add(accentLine);
                form.Controls.Add(header);
                form.Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法加载百科页面：{ex.Message}", "百科", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }
            string cleaned = hex.Trim();
            if (cleaned.Length % 2 != 0)
            {
                return null;
            }
            byte[] data = new byte[cleaned.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                int hi = HexCharToInt(cleaned[i * 2]);
                int lo = HexCharToInt(cleaned[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                {
                    return null;
                }
                data[i] = (byte)((hi << 4) | lo);
            }
            return data;
        }

        private static int HexCharToInt(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            return -1;
        }
        #endregion

        // ========== 创建接收数据CSV文件（高精度版本）==========
        private void CreateReceiveDataFileHighPrecision()
        {
            _receiveDataFileName = currentFileName;

            using (StreamWriter writer = new StreamWriter(_receiveDataFileName, false, Encoding.UTF8))
            {
                // 写入详细的文件头信息
                writer.WriteLine("# ===============================================");
                writer.WriteLine("# ROV导航数据接收记录");
                writer.WriteLine("# ===============================================");
                writer.WriteLine($"# 创建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine("# 数据格式: CSV");
                writer.WriteLine("# 协议版本: 140字节数据包");
                writer.WriteLine("# 时间戳精度: 秒");
                writer.WriteLine("# -----------------------------------------------");
                writer.WriteLine("# 数据列说明:");
                writer.WriteLine("# TimeStamp: 绝对时间戳 (yyyy-MM-dd HH:mm:ss)");
                writer.WriteLine("# RecvCount: 接收计数");
                writer.WriteLine("# Time: 系统时间");
                writer.WriteLine("# GyroX/Y/Z: 陀螺仪数据 (°/s)");
                writer.WriteLine("# AccelX/Y/Z: 加速度计数据 (m/s2)");
                writer.WriteLine("# MagnX/Y/Z: 磁力计数据 (uT)");
                writer.WriteLine("# Pitch/Roll/Yaw: 姿态角 (°)");
                writer.WriteLine("# ThrusterLF/LM/LR/RF/RM/RR: 6个推进器值");
                writer.WriteLine("# GPSVelocity: GPS速度 (m/s)");
                writer.WriteLine("# GPSLon/Lat/Alt: GPS位置 (经度/纬度/海拔)");
                writer.WriteLine("# GPSStatus: GPS状态");
                writer.WriteLine("# Pressure: 水压 (Pa)");
                writer.WriteLine("# Depth: 水深 (m) (等于 P30距离)");
                writer.WriteLine("# P30Confidence: P30数据置信度");
                writer.WriteLine("# Temperature: 温度 (°C)");
                writer.WriteLine("# Current: 电流 (A)");
                writer.WriteLine("# Voltage: 电压 (V)");
                writer.WriteLine("# ===============================================");
                writer.WriteLine("#");

                // CSV列标题
                string header = "TimeStamp,RecvCount,Time," +
                               "GyroX,GyroY,GyroZ," +
                               "AccelX,AccelY,AccelZ," +
                               "MagnX,MagnY,MagnZ," +
                               "Pitch,Roll,Yaw," +
                               "ThrusterLF,ThrusterLM,ThrusterLR,ThrusterRF,ThrusterRM,ThrusterRR," +
                               "GPSVelocity," +
                               "GPSLon,GPSLat,GPSAlt," +
                               "GPSStatus," +
                               "Pressure,Depth,P30Confidence," +
                               "Temperature,Current,Voltage";

                writer.WriteLine(header);
            }

            Console.WriteLine($"接收数据记录文件已创建: {_receiveDataFileName}");
        }

        // ========== 保存接收数据（高精度版本）==========
        private void SaveReceiveDataToFileHighPrecision()
        {
            if (string.IsNullOrEmpty(_receiveDataFileName) || _receiveDataBuffer.Count == 0)
                return;

            try
            {
                using (StreamWriter writer = new StreamWriter(_receiveDataFileName, true, Encoding.UTF8))
                {
                    foreach (string line in _receiveDataBuffer)
                    {
                        writer.WriteLine(line);
                    }
                    writer.Flush(); // 确保数据写入磁盘
                }

                Console.WriteLine($"[高精度保存] 已保存 {_receiveDataBuffer.Count} 条接收记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存接收数据错误: {ex.Message}");
            }
        }
        private void SendProtocolData()
        {
            try
            {
                byte[] protocolData = GenerateProtocol(gamepadState);
                if (SendFrame(protocolData, "CTRL"))
                {
                    sendCount++;
                    if (sendCount % 100 == 0)
                    {
                        Console.WriteLine($"已发送 {sendCount} 个数据包");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送协议数据错误: {ex.Message}");
            }
        }

        private bool SendFrame(byte[] data, string category)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            if (!_useNetworkStream || !_streamConnected || _streamNetwork == null || !_streamNetwork.CanWrite)
            {
                Console.WriteLine($"[{category}] 网络未连接，无法发送数据");
                return false;
            }

            try
            {
                lock (_networkStreamSendLock)
                {
                    _streamNetwork.Write(data, 0, data.Length);
                    _streamNetwork.Flush();
                }
                if (category != "CTRL")
                {
                    Console.WriteLine($"[{category}] 发送 {data.Length} 字节");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogStreamError($"[{category}] 发送失败: {ex.Message}");
                CloseStreamConnection($"{category.ToLower()} send error");
                return false;
            }
        }
            private void CloseStreamConnection(string reason)
            {
                lock (_networkStreamSendLock)
                {
                    try { _streamNetwork?.Dispose(); } catch { }
                    try { _streamClient?.Close(); } catch { }
                    _streamNetwork = null;
                    _streamClient = null;
                    _streamConnected = false;
                }

                LogStreamError($"网络连接已关闭: {reason}");
            }
            private void LogStreamError(string message)
            {
                if ((DateTime.UtcNow - _lastStreamErrorUtc).TotalSeconds < 1)
                {
                    return;
                }
                _lastStreamErrorUtc = DateTime.UtcNow;
                Console.WriteLine(message);
            }
            // 记录发送的数据
            private void RecordSendData(byte[] data)
            {
                if (!isRecordingSend || string.IsNullOrEmpty(sendDataFileName))
                    return;

                try
                {
                    // 获取高精度时间戳（精确到1ms）
                    DateTime now = DateTime.Now;
                    string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    // 也可以使用高性能计时器获取更高精度
                    // long ticks = DateTime.UtcNow.Ticks;
                    // double milliseconds = ticks / 10000.0;

                    // 将字节数组转换为十六进制字符串
                    string hexData = BitConverter.ToString(data).Replace("-", " ");

                    // 解析关键数据字段
                    string actionCode = data[4].ToString("X2");
                    string selfLock = data[6] == 0x01 ? "ON" : "OFF";

                    // 摇杆数据（16位无符号整数）
                    ushort leftX = BitConverter.ToUInt16(data, 7);
                    ushort leftY = BitConverter.ToUInt16(data, 9);
                    ushort rightX = BitConverter.ToUInt16(data, 11);
                    ushort rightY = BitConverter.ToUInt16(data, 13);

                    // 深度控制
                    bool depthLock = data[15] == 0x01;
                    float targetDepth = BitConverter.ToSingle(data, 16);
                    byte lightLevel = data[20];

                    // 校验和
                    uint checksum = BitConverter.ToUInt32(data, 21);

                    // 构建CSV数据行
                    string dataLine = $"{timestamp}," +
                                     $"{sendCount}," +
                                     $"{actionCode}," +
                                     $"{selfLock}," +
                                     $"{leftX},{leftY}," +
                                     $"{rightX},{rightY}," +
                                     $"{(depthLock ? "LOCKED" : "UNLOCKED")}," +
                                     $"{targetDepth:F2}," +
                                     $"{lightLevel}," +
                                     $"{checksum}," +
                                     $"\"{hexData}\"";  // 原始十六进制数据用引号包围

                    sendDataLines.Add(dataLine);
                    sendRecordedCount++;

                    // 每50条记录保存一次
                    if (sendDataLines.Count >= 50)
                    {
                        SaveSendDataToFile();
                        sendDataLines.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"记录发送数据错误: {ex.Message}");
                }
            }
            // 创建发送数据CSV文件
            private void CreateSendDataFile()
            {
                // 生成文件名（带时间戳）
                sendDataFileName = Path.Combine(
                    Path.GetDirectoryName(currentFileName) ?? "",
                    $"SendData_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                );

                using (StreamWriter writer = new StreamWriter(sendDataFileName, false, Encoding.UTF8))
                {
                    // 写入文件头信息
                    writer.WriteLine("# ROV控制指令发送记录");
                    writer.WriteLine($"# 创建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("# 数据格式: CSV");
                    writer.WriteLine("# 协议版本: 29字节上位机发送协议");
                    writer.WriteLine("# 时间戳精度: 1毫秒");
                    writer.WriteLine("#");

                    // CSV列标题
                    string header = "TimeStamp_ms," +          // 时间戳（精确到毫秒）
                                   "SendCount," +               // 发送计数
                                   "CameraControl," +           // 动作控制码(HEX，机械臂/相机)
                                   "SelfLock," +               // 自锁状态
                                   "LeftStickX,LeftStickY," +  // 左摇杆
                                   "RightStickX,RightStickY," + // 右摇杆
                                   "DepthLock," +              // 深度锁定状态
                                   "TargetDepth_m," +          // 目标深度
                                   "LightLevel," +             // 照明亮度(0-10)
                                   "Checksum," +               // 校验和
                                   "RawData_HEX";              // 原始数据(十六进制)

                    writer.WriteLine(header);
                }

                Console.WriteLine($"发送数据记录文件已创建: {sendDataFileName}");
            }
            // 保存发送数据到文件
            private void SaveSendDataToFile()
            {
                if (string.IsNullOrEmpty(sendDataFileName) || sendDataLines.Count == 0)
                    return;

                try
                {
                    using (StreamWriter writer = new StreamWriter(sendDataFileName, true, Encoding.UTF8))
                    {
                        foreach (string line in sendDataLines)
                        {
                            writer.WriteLine(line);
                        }
                    }

                    Console.WriteLine($"已保存 {sendDataLines.Count} 条发送记录");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存发送数据错误: {ex.Message}");
                }
            }




            private void Form1_MouseDown(object sender, MouseEventArgs e)
            {
               // chart1.Series[0].Points.AddXY(8, 3);
            }
            /// <summary>
            /// 生成29字节上位机发送协议
            /// </summary>
            /// <param name="gamepadState">带Menu键的手柄状态</param>
            /// <param name="sendConfig">发送配置参数</param>
            /// <param name="keyTracker">按键追踪器（检测上升沿）</param>
            /// <returns>29字节协议数组</returns>
            public byte[] GenerateProtocol(GamepadState gamepadState)
            {
                // 初始化29字节协议数组
                byte[] protocol = new byte[ProtocolTotalLength];

                #region 步骤1：填充帧头（0-3字节）
                Buffer.BlockCopy(FrameHeader, 0, protocol, 0, FrameHeader.Length);
                #endregion

                #region 步骤2：填充动作控制码（4字节，0x00-0x08）
                // 根据协议：方向键控制机械臂（左/右旋 + 抓取/释放）
                byte cameraCode = 0x00;
                switch (gamepadState.Dpad)
                {
                    case 0x1C: // 方向左键
                        cameraCode = 0x01; // 机械臂左旋
                        break;
                    case 0x0C: // 方向右键  
                        cameraCode = 0x02; // 机械臂右旋
                        break;
                    case 0x04: // 方向上键
                        cameraCode = 0x03; // 机械臂抓取
                        break;
                    case 0x14: // 方向下键
                        cameraCode = 0x04; // 机械臂释放
                        break;
                    default:
                        // 其他按键为相机控制逻辑
                        if (gamepadState.IsYKeyPressed)
                        {
                            cameraCode = 0x05; // Y=相机上转
                        }
                        else if (gamepadState.IsAKeyPressed)
                        {
                            cameraCode = 0x06; // A=相机下转
                        }
                        else if (gamepadState.IsXKeyPressed)
                        {
                            cameraCode = 0x07; // X=相机左转
                        }
                        else if (gamepadState.IsBKeyPressed)
                        {
                            cameraCode = 0x08; // B=相机右转
                        }
                        break;
                }
                protocol[4] = cameraCode;
                #endregion

                #region 步骤3：填充保留位（5字节，已移除自稳功能）
                protocol[5] = 0x00;
                #endregion

                #region 步骤4：填充自锁状态（6字节，上位机启动开关控制）
                byte selfLock = (byte)((rovPowerSwitch != null && rovPowerSwitch.IsOn) ? 0x01 : 0x00);
                protocol[6] = selfLock;
                #endregion

                #region 步骤5：填充摇杆控制字段（7-14字节，16位无符号整数，低8位在前）
                ushort leftX = gamepadState.LeftStickX;
                ushort leftY = gamepadState.LeftStickY;
                ushort rightX = gamepadState.RightStickX;
                ushort rightY = gamepadState.RightStickY;

                // 手柄未初始化或数据异常时，强制回中避免误动作
                if (leftX == 0 && leftY == 0 && rightX == 0 && rightY == 0)
                {
                    leftX = 32768;
                    leftY = 32767;
                    rightX = 32768;
                    rightY = 32767;
                }

                // 5.1 左右横移（7-8字节：左摇杆X轴）
                protocol[7] = (byte)(leftX & 0xFF); // 低8位
                protocol[8] = (byte)((leftX >> 8) & 0xFF); // 高8位

                // 5.2 上下移动（9-10字节：左摇杆Y轴）
                protocol[9] = (byte)(leftY & 0xFF);
                protocol[10] = (byte)((leftY >> 8) & 0xFF);

                // 5.3 左右偏移（11-12字节：右摇杆X轴）
                protocol[11] = (byte)(rightX & 0xFF);
                protocol[12] = (byte)((rightX >> 8) & 0xFF);

                // 5.4 上浮下潜（13-14字节：右摇杆Y轴）
                protocol[13] = (byte)(rightY & 0xFF);
                protocol[14] = (byte)((rightY >> 8) & 0xFF);
                #endregion

                #region 步骤6：填充水深控制字段（15-19字节）
                // 6.1 潜行指定水深开关（15字节：上位机控制，不从手柄获取）
                byte diveSwitch = 0x00;
                if (selflockk) // 保持原有的上位机控制逻辑
                {
                    diveSwitch = 0x01;
                }
                protocol[15] = diveSwitch;

                // 6.2 指定水深（16-19字节：float类型，小端序，上位机控制）
                byte[] depthBytes = BitConverter.GetBytes(setdeep); // 保持原有的上位机控制
                if (!BitConverter.IsLittleEndian) Array.Reverse(depthBytes);
                Buffer.BlockCopy(depthBytes, 0, protocol, 16, depthBytes.Length);
                #endregion

                #region 步骤7：填充照明灯亮度（20字节，0x00-0x0A，上位机控制）

                // 开关优先：关灯时无论亮度档位如何都发送0，开灯时才发送当前亮度
                byte effectiveLightLevel = (byte)(isLightOn ? lightlevel : 0);
                protocol[20] = effectiveLightLevel;
                #endregion

                #region 步骤8：计算并填充校验和（21-24字节）
                uint checksum = CalculateChecksum(protocol, startIndex: 4, endIndex: 20);
                byte[] checksumBytes = BitConverter.GetBytes(checksum);
                if (!BitConverter.IsLittleEndian) Array.Reverse(checksumBytes);
                Buffer.BlockCopy(checksumBytes, 0, protocol, 21, checksumBytes.Length);
                #endregion

                #region 步骤9：填充帧尾（25-28字节）
                Buffer.BlockCopy(FrameFooter, 0, protocol, 25, FrameFooter.Length);
                #endregion
                Console.WriteLine($"[{GetHighPrecisionTimestamp()}] 发送数据: {BitConverter.ToString(protocol, 0, 29)}");
                return protocol;
            }
            /// <summary>
            /// 计算校验和（指定字节范围的累加和）
            /// </summary>
            private  uint CalculateChecksum(byte[] data, int startIndex, int endIndex)
            {
                if (data == null || data.Length == 0)
                {
                    return 0;
                }

                uint sum = 0;
                int safeEnd = Math.Min(endIndex, data.Length - 1);
                for (int i = Math.Max(0, startIndex); i <= safeEnd; i++)
                {
                    sum += data[i];
                }
                return sum;
            }

            private static void WriteFloat(byte[] buffer, int offset, float value)
            {
                var bytes = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
            }

            // bt_save按钮点击事件 - 开始/停止录数
            private void bt_save_Click(object sender, EventArgs e)
            {
                try
                {
                    Console.WriteLine("bt_save按钮被点击");

                    if (!isRecording)
                    {
                        // 开始录数
                        StartRecording();
                    }
                    else
                    {
                        // 停止录数
                        StopRecording();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"bt_save按钮错误: {ex.Message}");
                    MessageBox.Show($"录数操作错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // bt_new按钮点击事件 - 新建文件
            private void bt_new_Click(object sender, EventArgs e)
            {
                try
                {
                    Console.WriteLine("bt_new按钮被点击");

                    // 如果正在录制，先询问用户
                    if (isRecording)
                    {
                        var result = MessageBox.Show("正在录制数据中，是否停止当前录制并新建文件？",
                                                   "确认操作", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes)
                        {
                            StopRecording();
                        }
                        else
                        {
                            return;
                        }
                    }

                    // 创建新文件
                    CreateNewDataFile();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"bt_new按钮错误: {ex.Message}");
                    MessageBox.Show($"新建文件错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        private void StartRecording()
        {
            // 检查是否已创建录制文件
            if (string.IsNullOrEmpty(currentFileName))
            {
                MessageBox.Show("请先新建录数文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ★★★ 关键：重置高精度计时器 ★★★
            _highPrecisionTimer.Restart();
            _recordingStartTime = DateTime.UtcNow;

            // 创建接收数据记录文件
            CreateReceiveDataFileHighPrecision();

            // 设置录制状态
            isRecording = true;
            _isRecordingReceive = true;
            _isRecordingSend = false;
            recordStartTime = DateTime.Now;

            // 清空缓冲区
            lock (_receiveBufferLock)
            {
                _receiveDataBuffer.Clear();
            }
            _receiveRecordCount = 0;
            _sendRecordCount = 0;
            recordedCount = 0;

            // 更新UI状态
            if (bt_save != null)
            {
                bt_save.Text = "停止录数";
                bt_save.BackColor = Color.Red;
                bt_save.ForeColor = Color.White;
            }
            controlPanel?.SetSaveButtonState(true);

            // 输出详细信息
            Console.WriteLine("=======================================");
            Console.WriteLine("开始录制（仅接收传感器数据）");
            Console.WriteLine($"时间戳精度: 秒");
            Console.WriteLine($"开始时间: {_recordingStartTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"接收文件: {Path.GetFileName(_receiveDataFileName)}");
            Console.WriteLine("=======================================");

            MessageBox.Show($"开始录制（仅接收传感器数据，秒级时间戳）:\n" +
                           $"接收数据: {Path.GetFileName(_receiveDataFileName)}",
                           "开始录制", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        // 停止录制数据
        private void StopRecording()
        {
            Console.WriteLine("=======================================");
            Console.WriteLine("停止录制");
            Console.WriteLine($"总运行时间: {_highPrecisionTimer.ElapsedMilliseconds} ms");
            Console.WriteLine($"接收记录数: {_receiveRecordCount}");
            Console.WriteLine("=======================================");

            isRecording = false;
            _isRecordingReceive = false;
            _isRecordingSend = false;

            // 更新UI状态
            if (bt_save != null)
            {
                bt_save.Text = "开始录数";
                bt_save.BackColor = SystemColors.Control;
                bt_save.ForeColor = SystemColors.ControlText;
            }

            // 保存剩余的接收数据
            lock (_receiveBufferLock)
            {
                if (_receiveDataBuffer.Count > 0)
                {
                    SaveReceiveDataToFileHighPrecision();
                    _receiveDataBuffer.Clear();
                    Console.WriteLine($"保存剩余 {_receiveDataBuffer.Count} 条接收数据");
                }
            }

            TimeSpan recordDuration = DateTime.Now - recordStartTime;

            string message = $"录制完成！\n\n" +
                            $"时间戳精度: 秒\n" +
                            $"录制时间: {recordDuration.Hours:D2}:{recordDuration.Minutes:D2}:{recordDuration.Seconds:D2}\n" +
                            $"总运行时间: {_highPrecisionTimer.ElapsedMilliseconds:F0} ms\n\n" +
                            $"接收记录数: {_receiveRecordCount} 条\n" +
                            $"接收文件: {Path.GetFileName(_receiveDataFileName)}";

            controlPanel?.SetSaveButtonState(false);

            MessageBox.Show(message, "录制完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        // 创建新的数据文件
        private void CreateNewDataFile()
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
            saveDialog.DefaultExt = "csv";
            saveDialog.AddExtension = true;
            saveDialog.FileName = $"ReceiveData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            saveDialog.Title = "新建接收数据文件";

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    currentFileName = saveDialog.FileName;

                    recordedDataLines.Clear();
                    recordedCount = 0;

                    Console.WriteLine($"录数文件已设置: {currentFileName}");
                    MessageBox.Show($"录数文件已设置:\n{currentFileName}\n\n点击\"开始录数\"即可记录接收传感器数据。",
                                    "文件创建成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建录数文件失败: {ex.Message}");
                    MessageBox.Show($"创建录数文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    currentFileName = "";
                }
            }
        }
            // 创建CSV文件头
            private void CreateCSVHeader()
            {
                using (StreamWriter writer = new StreamWriter(currentFileName, false, Encoding.UTF8))
                {
                    // 写入详细的CSV头部信息
                    writer.WriteLine("# ROV导航数据记录文件");
                    writer.WriteLine($"# 创建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("# 数据格式: CSV");
                    writer.WriteLine("# 协议版本: 140字节数据包");
                    writer.WriteLine("# Pressure: 水压 (Pa)");
                    writer.WriteLine("# Depth: 水深 (m) (等于 P30距离)");
                    writer.WriteLine("# P30Confidence: P30数据置信度");
                    writer.WriteLine("#");

                    // 修正后的CSV列标题 - 与实际数据结构匹配
                    string header = "TimeStamp,RecvCount,Time," +
                                   "GyroX,GyroY,GyroZ," +
                                   "AccelX,AccelY,AccelZ," +
                                   "MagnX,MagnY,MagnZ," +
                                   "Pitch,Roll,Yaw," +  // INS姿态：俯仰、横滚、偏航
                                   "ThrusterLF,ThrusterLM,ThrusterLR,ThrusterRF,ThrusterRM,ThrusterRR," +  // 6个推进器
                                   "GPSVelocity," +  // GPS速度（单个值）
                                   "GPSLon,GPSLat,GPSAlt," +  // GPS位置
                                   "GPSStatus," +
                                   "Pressure,Depth,P30Confidence," +
                                   "Temperature,Current,Voltage";

                    writer.WriteLine(header);
                }
                Console.WriteLine("CSV文件头创建完成");
            }
            // 记录当前数据（在dataconv中调用）
            private void RecordCurrentData()
            {
                if (!isRecording || string.IsNullOrEmpty(currentFileName))
                    return;

                try
                {
                    // 修正CSV数据行格式，确保所有字段都有正确的逗号分隔
                    string dataLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
                                     $"{navi.recvcnt}," +
                                     $"{navi.time:F3}," +
                                     $"{navi.wb[0]:F6},{navi.wb[1]:F6},{navi.wb[2]:F6}," +
                                     $"{navi.fb[0]:F6},{navi.fb[1]:F6},{navi.fb[2]:F6}," +
                                     $"{navi.Magn[0]:F6},{navi.Magn[1]:F6},{navi.Magn[2]:F6}," +
                                     $"{navi.INS_att[0]:F6},{navi.INS_att[1]:F6},{navi.INS_att[2]:F6}," +
                                     $"{navi.thrusterValues[0]:F6},{navi.thrusterValues[1]:F6},{navi.thrusterValues[2]:F6},{navi.thrusterValues[3]:F6},{navi.thrusterValues[4]:F6},{navi.thrusterValues[5]:F6}," +  // 添加逗号
                                     $"{navi.GPS_vel:F6}," +  // 添加逗号
                                     $"{navi.GPS_pos[0]:F10},{navi.GPS_pos[1]:F10},{navi.GPS_pos[2]:F6},{navi.GPS_status:F2}," +
                                     $"{navi.waterPressure:F2}," +
                                     $"{navi.deep:F6}," +
                                     $"{navi.p30Confidence:F3}," +
                                     $"{navi.tempearature:F2}," +
                                     $"{navi.current:F3}," +
                                     $"{navi.voltage:F3}";

                    recordedDataLines.Add(dataLine);
                    recordedCount++;

                    if (recordedDataLines.Count >= 50)
                    {
                        SaveDataToFile();
                        recordedDataLines.Clear();
                        Console.WriteLine($"已记录 {recordedCount} 条数据");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"记录数据错误: {ex.Message}");
                }
            }
            // 保存数据到文件
            private void SaveDataToFile()
            {
                if (string.IsNullOrEmpty(currentFileName) || recordedDataLines.Count == 0)
                    return;

                try
                {
                    using (StreamWriter writer = new StreamWriter(currentFileName, true, Encoding.UTF8))
                    {
                        foreach (string line in recordedDataLines)
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存数据到文件错误: {ex.Message}");
                    MessageBox.Show($"保存数据错误: {ex.Message}", "保存错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

        private void LogTelemetrySnapshot()
        {
            try
            {
                string gyro = $"{navi.wb[0]:F2},{navi.wb[1]:F2},{navi.wb[2]:F2}";
                string acc = $"{navi.fb[0]:F2},{navi.fb[1]:F2},{navi.fb[2]:F2}";
                string thruster = string.Join(",", navi.thrusterValues.Select(v => v.ToString()));
                Console.WriteLine(
                    $"[TELEMETRY] #{navi.recvcnt} t={navi.time:F2}s pressure={navi.waterPressure:F1}Pa depth={navi.deep:F2}m p30_conf={navi.p30Confidence:F2} temp={navi.tempearature:F1}°C volt={navi.voltage:F2}V current={navi.current:F2}A gyro=[{gyro}] acc=[{acc}] thr=[{thruster}] GPS=({navi.GPS_pos[0]:F5},{navi.GPS_pos[1]:F5})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"记录遥测快照失败: {ex.Message}");
            }
        }

            private void StartGamepadThread()
            {
                if (controlthread != null && controlthread.IsAlive)
                {
                    return;
                }
                _stopGamepad = false;
                controlthread = new Thread(new ThreadStart(controlprocess))
                {
                    IsBackground = true,
                    Name = "GamepadControlThread"
                };
                controlthread.Start();
            }

            private void StopGamepadThread()
            {
                _stopGamepad = true;
                if (controlthread != null && controlthread.IsAlive)
                {
                    if (!controlthread.Join(1000))
                    {
                        try
                        {
                            controlthread.Abort();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"终止手柄线程错误: {ex.Message}");
                        }
                    }
                }
                controlthread = null;
            }

            public void controlprocess()
            {
                // 50Hz数据发送定时器（使用高精度计时）
                Stopwatch sendTimer = Stopwatch.StartNew();
                const int sendIntervalMs = 20; // 50Hz = 20ms间隔

                while (!_stopGamepad)
                {
                    HidStream stream = null;

                    try
                    {
                        Console.WriteLine("正在连接游戏手柄...");
                        var device = FindCorrectGamepadInterface();

                        if (device == null)
                        {
                            Console.WriteLine("未找到游戏手柄接口，5秒后重试...");
                            Thread.Sleep(5000);
                            continue;
                        }

                        stream = device.Open();
                        stream.ReadTimeout = 100;

                        var reportDescriptor = device.GetReportDescriptor();
                        int inputReportLength = reportDescriptor.MaxInputReportLength;
                        _inputReportLength = inputReportLength;
                        byte[] buffer = new byte[Math.Max(inputReportLength, 64)];

                        Console.WriteLine($"设备连接成功，输入报告长度: {inputReportLength}");
                        Console.WriteLine($"开始读取游戏手柄数据...");
                        Console.WriteLine($"开始50Hz定时发送（高精度）");

                        try { gamepadDisplay?.SetConnected(true); } catch { }
                        int successfulReads = 0;
                        int consecutiveTimeouts = 0;
                        const int maxConsecutiveTimeouts = 50;

                        while (!_stopGamepad)
                        {
                            // 非阻塞读取手柄数据
                            try
                            {
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                if (bytesRead > 0)
                                {
                                    consecutiveTimeouts = 0;
                                    successfulReads++;

                                    if (successfulReads == 1)
                                    {
                                        Console.WriteLine($"第一次读取: {bytesRead} 字节");
                                        Console.WriteLine($"数据: {BitConverter.ToString(buffer, 0, Math.Min(bytesRead, 15))}");
                                    }

                                    // 处理游戏手柄数据
                                    ProcessGamepadData(buffer, bytesRead);

                                    // 使用高精度计时器进行50Hz定时发送
                                    if (sendTimer.ElapsedMilliseconds >= sendIntervalMs)
                                    {
                                        SendProtocolData();
                                        sendTimer.Restart();
                                    }
                                }
                            }
                            catch (TimeoutException)
                            {
                                consecutiveTimeouts++;

                                if (consecutiveTimeouts <= 3)
                                {
                                    Console.WriteLine($"读取超时 ({consecutiveTimeouts})");
                                }

                                if (consecutiveTimeouts >= maxConsecutiveTimeouts)
                                {
                                    Console.WriteLine($"连续超时 {maxConsecutiveTimeouts} 次，重新连接");
                                    break;
                                }
                                continue;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"读取数据错误: {ex.Message}");
                                break;
                            }

                            Thread.Sleep(1); // 短暂延迟，降低CPU占用
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"游戏手柄处理错误: {ex.Message}");
                        try { gamepadDisplay?.SetConnected(false); } catch { }
                    }
                    finally
                    {
                        try
                        {
                            stream?.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"关闭设备错误: {ex.Message}");
                        }
                        try { gamepadDisplay?.SetConnected(false); } catch { }
                    }

                    if (!_stopGamepad)
                    {
                        Console.WriteLine("3秒后重试...");
                        Thread.Sleep(3000);
                    }
                }
            }

            // 处理游戏手柄数据的独立方法
            private void ProcessGamepadData(byte[] buffer, int bytesRead)
            {
                try
                {
                    // 根据实际数据长度确定处理方式（统一14字节payload）
                    if (bytesRead < GamepadPayloadLength)
                    {
                        LogGamepadLength($"意外的数据长度: {bytesRead} 字节，期望 {GamepadPayloadLength} 字节");
                        return;
                    }

                    int offset = 0;
                    if (bytesRead == GamepadPayloadLength + 1)
                    {
                        // 带ReportID的情况（ReportID占1字节）
                        offset = 1;
                        LogGamepadLength($"检测到ReportID，长度: {bytesRead}，ReportID=0x{buffer[0]:X2}");
                    }
                    else if (bytesRead > GamepadPayloadLength + 1)
                    {
                        // 超长包，尝试通过评分选择offset
                        long score0 = ScoreGamepadOffset(buffer, bytesRead, 0, GamepadPayloadLength);
                        long score1 = ScoreGamepadOffset(buffer, bytesRead, 1, GamepadPayloadLength);
                        offset = score1 <= score0 ? 1 : 0;
                        LogGamepadLength($"超长数据({bytesRead}字节)，score0={score0} score1={score1} offset={offset}");
                    }

                    if (bytesRead < GamepadPayloadLength + offset)
                    {
                        LogGamepadLength($"丢弃长度不足的数据: {bytesRead} 字节，offset={offset}");
                        return;
                    }

                    byte[] gamepadData = new byte[GamepadPayloadLength];
                    Array.Copy(buffer, offset, gamepadData, 0, GamepadPayloadLength);

                    // 解析游戏手柄数据
                    gamepadState = Parse(gamepadData);

                    // 注意：这里不要直接发送，SendProtocolData会在controlprocess中定时调用
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理游戏手柄数据错误: {ex.Message}");
                }
            }
            private long ScoreGamepadOffset(byte[] buffer, int bytesRead, int offset, int expectedLength)
            {
                if (bytesRead < expectedLength + offset)
                {
                    return long.MaxValue;
                }
                ushort leftX = BitConverter.ToUInt16(buffer, offset);
                ushort leftY = BitConverter.ToUInt16(buffer, offset + 2);
                ushort rightX = BitConverter.ToUInt16(buffer, offset + 4);
                ushort rightY = BitConverter.ToUInt16(buffer, offset + 6);
                long score = 0;
                score += Math.Abs(leftX - 32768);
                score += Math.Abs(leftY - 32767);
                score += Math.Abs(rightX - 32768);
                score += Math.Abs(rightY - 32767);
                return score;
            }
            private void LogGamepadLength(string message)
            {
                if ((DateTime.UtcNow - _lastGamepadLogUtc).TotalSeconds < 1)
                {
                    return;
                }
                _lastGamepadLogUtc = DateTime.UtcNow;
                Console.WriteLine(message);
            }
            private void MonitorSpecificDevice()
            {
                Task.Run(() =>
                {
                    while (!_shouldStop)
                    {
                        try
                        {
                            var deviceList = DeviceList.Local;
                            var devices = deviceList.GetHidDevices(targetVid, targetPid);

                            bool foundCorrectInterface = false;
                            foreach (var device in devices)
                            {
                                if (device.DevicePath.ToLower().Contains("ig_00"))
                                {
                                    foundCorrectInterface = true;
                                    break;
                                }
                            }

                            if (!foundCorrectInterface)
                            {
                                Console.WriteLine("警告: ig_00 接口未检测到");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"设备监控错误: {ex.Message}");
                        }

                        Thread.Sleep(10000); // 每10秒检查一次
                    }
                });
            }
            private void DiagnoseGamepadDevices()
            {
                try
                {
                    var deviceList = DeviceList.Local;
                    var devices = deviceList.GetHidDevices(targetVid, targetPid);

                    Console.WriteLine("=== 游戏手柄设备诊断 ===");
                    Console.WriteLine($"找到 {devices.Count()} 个匹配的HID设备");

                    foreach (var device in devices)
                    {
                        Console.WriteLine($"\\n设备路径: {device.DevicePath}");
                        Console.WriteLine($"产品名称: {device.GetProductName()}");
                        Console.WriteLine($"制造商: {device.GetManufacturer()}");

                        try
                        {
                            var canOpen = device.TryOpen(out HidStream testStream);
                            Console.WriteLine($"可以打开: {canOpen}");
                            testStream?.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"测试打开失败: {ex.Message}");
                        }
                    }
                    Console.WriteLine("=== 诊断结束 ===\\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"设备诊断错误: {ex.Message}");
                }
            }
            private void MonitorGamepad()
            {
                Task.Run(() =>
                {
                    while (!_shouldStop)
                    {
                        try
                        {
                            var deviceList = DeviceList.Local;
                            var devices = deviceList.GetHidDevices(targetVid, targetPid);

                            Console.WriteLine($"检测到 {devices.Count()} 个匹配的HID设备");

                            foreach (var dev in devices)
                            {
                                Console.WriteLine($"设备路径: {dev.DevicePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"监控设备错误: {ex.Message}");
                        }

                        Thread.Sleep(5000); // 每5秒检查一次
                    }
                });
            }
        
            private bool TryReconnectGamepad()
            {
                try
                {
                    var deviceList = DeviceList.Local;
                    var device = deviceList.GetHidDevices(targetVid, targetPid).FirstOrDefault();

                    if (device != null)
                    {
                        // 尝试打开设备来验证是否可用
                        try
                        {
                            using (var testStream = device.Open())
                            {
                                Console.WriteLine("游戏手柄重新连接成功");
                                return true;
                            }
                        }
                        catch
                        {
                            Console.WriteLine("游戏手柄无法打开");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("游戏手柄仍未连接");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"检测游戏手柄连接失败: {ex.Message}");
                    return false;
                }
            }
            private HidDevice FindCorrectGamepadInterface()
            {
                try
                {
                    var deviceList = DeviceList.Local;
                    var devices = deviceList.GetHidDevices(targetVid, targetPid);

                    Console.WriteLine($"找到 {devices.Count()} 个匹配设备:");

                    foreach (var device in devices)
                    {
                        try
                        {
                            Console.WriteLine($"\\n设备路径: {device.DevicePath}");

                            string devicePath = device.DevicePath.ToLower();

                            // 专门查找 ig_00 接口（输入组0）
                            if (devicePath.Contains("ig_00"))
                            {
                                Console.WriteLine("找到正确的手柄接口 (ig_00)");

                                try
                                {
                                    string productName = device.GetProductName();
                                    Console.WriteLine($"产品名称: {productName}");

                                    // 获取报告描述符信息
                                    var reportDescriptor = device.GetReportDescriptor();
                                    Console.WriteLine($"最大输入报告长度: {reportDescriptor.MaxInputReportLength}");
                                    Console.WriteLine($"最大输出报告长度: {reportDescriptor.MaxOutputReportLength}");

                                    return device;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"获取设备信息失败: {ex.Message}");
                                    // 即使获取信息失败，仍然尝试使用这个设备
                                    return device;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"跳过接口: {GetInterfaceType(devicePath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"检查设备失败: {ex.Message}");
                            continue;
                        }
                    }

                    Console.WriteLine("未找到 ig_00 接口，使用第一个可用设备");
                    return devices.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"查找设备接口错误: {ex.Message}");
                    return null;
                }
            }

            // 辅助方法：识别接口类型
            private string GetInterfaceType(string devicePath)
            {
                if (devicePath.Contains("ig_00")) return "输入组0 (游戏手柄数据)";
                if (devicePath.Contains("mi_00")) return "多接口0";
                if (devicePath.Contains("mi_01")) return "多接口1";
                if (devicePath.Contains("mi_02")) return "多接口2";
                if (devicePath.Contains("kbd")) return "键盘接口";
                if (devicePath.Contains("mouse")) return "鼠标接口";
                return "未知接口";
            }
            public void dataProcess()
            {
                while (!_shouldStop)
                {
                    try
                    {
                        // 使用Mutex保护serial_data的访问
                        mutex.WaitOne();
                        if (serial_data.Count > 0)
                        {
                            temp.AddRange(serial_data);
                            serial_data.Clear();
                        }
                        mutex.ReleaseMutex();

                        if (temp.Count > 0)
                        {
                            for (int i = 0; i < temp.Count; i++)
                            {
                                // 状态机：0-寻找帧头，1-已找到帧头，正在接收数据
                                if (index == 0)
                                {
                                    // 寻找帧头AA 55 AA 56
                                    if (temp[i] == 0xAA)
                                    {
                                        recv[index++] = temp[i];
                                    }
                                    else
                                    {
                                        index = 0;
                                    }
                                }
                                else if (index == 1)
                                {
                                    if (temp[i] == 0x55)
                                    {
                                        recv[index++] = temp[i];
                                    }
                                    else
                                    {
                                        index = 0;
                                    }
                                }
                                else if (index == 2)
                                {
                                    if (temp[i] == 0xAA)
                                    {
                                        recv[index++] = temp[i];
                                    }
                                    else
                                    {
                                        index = 0;
                                    }
                                }
                                else if (index == 3)
                                {
                                    if (temp[i] == 0x56)
                                    {
                                        recv[index++] = temp[i];
                                    }
                                    else
                                    {
                                        index = 0;
                                    }
                                }
                                else if (index >= 4 && index < SerialFrameLength)
                                {
                                    recv[index++] = temp[i];
                                }

                                // 检查是否接收完一个完整的数据包
                                if (index == SerialFrameLength)
                                {
                                    // 检查帧尾
                                    int tailStart = SerialFrameLength - FrameFooter.Length;
                                    if (recv[tailStart + 0] == FrameFooter[0] &&
                                        recv[tailStart + 1] == FrameFooter[1] &&
                                        recv[tailStart + 2] == FrameFooter[2] &&
                                        recv[tailStart + 3] == FrameFooter[3])
                                    {
                                        dataconv(); // 解析数据
                                        index = 0;
                                    }
                                    else
                                    {
                                        // 帧尾不匹配，可能出错，重置索引
                                        index = 0;
                                    }
                                }
                            }

                            temp.Clear();
                        }

                        Thread.Sleep(1);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"数据处理错误: {ex.Message}");
                        index = 0;
                    }
                }
            }

            private void UIShow()
            {
                try
                {
                    // 更新陀螺仪数据显示
                    label_wb.Text = $"陀螺: X={navi.wb[0]:F3} °/s, Y={navi.wb[1]:F3} °/s, Z={navi.wb[2]:F3} °/s";
        
                    // 更新加速度计数据显示
                    label_fb.Text = $"加表: X={navi.fb[0]:F3} m/s2, Y={navi.fb[1]:F3} m/s2, Z={navi.fb[2]:F3} m/s2";
        
                    // 更新磁力计数据显示
                    label4.Text = $"磁力计: X={navi.Magn[0]:F1} uT, Y={navi.Magn[1]:F1} uT, Z={navi.Magn[2]:F1} uT";
        
                    // 更新姿态数据显示
                    label5.Text = $"姿态: 俯仰={navi.INS_att[0]:F2}°, 横滚={navi.INS_att[1]:F2}°, 航向={navi.INS_att[2]:F2}°";
                    rovModelViewer?.SetOrientation(navi.INS_att[0], navi.INS_att[1], navi.INS_att[2]);
        
                    // 更新速度数据显示
                    label6.Text = $"速度: {navi.GPS_vel:F2} m/s";

                    //label_thrusters_L.Text = $"左推进器: 前={navi.thrusterValues[0],3}         中={navi.thrusterValues[1],3}         后={navi.thrusterValues[2],3}";
                    //label_thrusters_R.Text = $"右推进器: 前={navi.thrusterValues[3],3}         中={navi.thrusterValues[4],3}         后={navi.thrusterValues[5],3}";
                    thrusterDisplay.UpdateThrusterValues(navi.thrusterValues);
                    // 更新位置数据显示
                    label7.Text = $"位置: 经度={navi.GPS_pos[0]:F6}°,纬度={navi.GPS_pos[1]:F6}°,海拔={navi.GPS_pos[2]:F2} m";
        
                    // 更新GPS数据显示
                    label8.Text = $"GPS状态: {navi.GPS_status:F2}";
                    // 如果存在以下标签，取消注释
                    // label_gps_vel.Text = $"GPS速度: E={navi.GPS_vel[0]:F2} m/s, N={navi.GPS_vel[1]:F2} m/s, U={navi.GPS_vel[2]:F2} m/s";
                    // label_gps_pos.Text = $"GPS位置: 经度={navi.GPS_pos[0]:F6}°, 纬度={navi.GPS_pos[1]:F6}°, 海拔={navi.GPS_pos[2]:F2} m";
        
                    // 更新传感器数据显示
                    label9.Text = $"温度: {navi.tempearature:F2} °C";
                    label10.Text = $"电流: {navi.current:F2} A";
                    label11.Text = $"电压: {navi.voltage:F2} V";
                    batteryIndicator?.UpdateVoltage(navi.voltage);
                    // 如果存在深度标签，取消注释
                    // label_deep.Text = $"水深: {navi.deep:F2} m";

                    // 更新手柄状态显示
                    //richTextBox1.Text = ToDebugString(gamepadState);
                    if (gamepadDisplay != null)
                    {
                        // 方法1：直接传递GamepadState对象（推荐）
                        gamepadDisplay.UpdateGamepadState(gamepadState);

                        // 方法2：或者使用原来的文本格式（兼容方式）
                        // string debugText = ToDebugString(gamepadState);
                        // gamepadDisplay.SetDisplayText(debugText);
                    }
                    else
                    {
                        // 备用：如果新控件创建失败，仍使用richTextBox1
                        richTextBox1.Text = ToDebugString(gamepadState);
                    }
                    // 轨迹绘制 - 使用GPS位置数据
                    DrawGPSTrajectory();

                    /*                // 绘制深度曲线 - 使用正确的曲线名称
                                    userCurve1.AddCurveData("A", navi.deep);

                                    // 绘制姿态曲线 - 俯仰和横滚，使用正确的曲线名称
                                    float[] att = { navi.INS_att[0], navi.INS_att[1] };
                                    userCurve2.AddCurveData(new string[] { "A", "B" }, att);

                                    // 绘制速度曲线 - 使用X方向速度，使用正确的曲线名称
                                    userCurve3.AddCurveData("A", navi.GPS_vel);

                                    // 绘制航向曲线 - 使用正确的曲线名称
                                    userCurve4.AddCurveData("A", navi.INS_att[2]);*/
                    // 使用新的科技风数据监控控件
                    if (telemetryControl != null)
                    {
                        telemetryControl.UpdateTelemetry(
                            navi.deep,          // 深度
                            navi.INS_att[0],    // 俯仰角
                            navi.INS_att[1],    // 横滚角
                            navi.GPS_vel,       // 前进速度
                            navi.INS_att[2]     // 航向角
                        );
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UI更新错误: {ex.Message}");
                }
            }
            private void TestTelemetryControl()
            {
                if (telemetryControl != null)
                {
                    // 测试调用 - 如果这里能自动补全UpdateTelemetry，说明方法存在
                    telemetryControl.UpdateTelemetry(10.5f, 5.0f, -3.0f, 2.5f, 45.0f);
                }
            }
            // GPS轨迹绘制方法
            // GPS轨迹绘制方法
            private void DrawGPSTrajectory()
            {
                try
                {
                    // 检查GPS坐标是否合理（不为零）
                    if (Math.Abs(navi.GPS_pos[0]) < 0.000001 || Math.Abs(navi.GPS_pos[1]) < 0.000001)
                    {
                        return; // 坐标无效时不绘制
                    }

                    // 使用新的GPS轨迹控件
                    if (gpsTrajectoryControl != null)
                    {
                        gpsTrajectoryControl.AddTrajectoryPoint(navi.GPS_pos[0], navi.GPS_pos[1], DateTime.Now);
                    }

                    // 同步到右下角真实地图
                    if (mapViewControl != null)
                    {
                        mapViewControl.AddPoint(navi.GPS_pos[0], navi.GPS_pos[1]);
                    }

                    // 可选：每50个点输出一次调试信息 500ms一个点
                    gpsPointCount++; // ? 使用类字段
                    if (gpsPointCount % 50 == 0)
                    {
                        Console.WriteLine($"GPS轨迹已绘制 {gpsPointCount} 个点");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GPS轨迹绘制错误: {ex.Message}");
                }
            }
            private bool IsDeviceReady(HidDevice device)
            {
                try
                {
                    // 先检查设备是否存在
                    if (device == null) return false;

                    // 尝试获取设备信息
                    var info = device.GetReportDescriptor();
                    return info != null;
                }
                catch
                {
                    return false;
                }
            }

            private HidDevice WaitForDeviceReady(int maxWaitMs = 5000)
            {
                var startTime = DateTime.Now;

                while ((DateTime.Now - startTime).TotalMilliseconds < maxWaitMs)
                {
                    try
                    {
                        var deviceList = DeviceList.Local;
                        var device = deviceList.GetHidDevices(targetVid, targetPid).FirstOrDefault();

                        if (device != null && IsDeviceReady(device))
                        {
                            return device;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"检测设备时出错: {ex.Message}");
                    }

                    Thread.Sleep(200); // 等待200ms后重试
                }

                return null;
            }
            private HidStream OpenGamepadDevice()
            {
                var device = FindCorrectGamepadInterface();
                if (device == null)
                {
                    Console.WriteLine("未找到可用的游戏手柄接口");
                    return null;
                }

                // 尝试多次打开设备
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        Console.WriteLine($"尝试打开游戏手柄设备 (第{attempt}次)...");

                        // 检查设备是否可以打开
                        if (device.TryOpen(out HidStream stream))
                        {
                            stream.ReadTimeout = 1000;
                            Console.WriteLine("设备打开成功!");
                            return stream;
                        }
                        else
                        {
                            Console.WriteLine($"设备被占用，无法打开 (第{attempt}次)");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"设备访问被拒绝 (第{attempt}次)，可能被系统占用");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"打开设备失败 (第{attempt}次): {ex.Message}");
                    }

                    if (attempt < 5)
                    {
                        Thread.Sleep(1000); // 等待1秒后重试
                    }
                }

                return null;
            }
            private HidStream OpenDeviceWithRetry(HidDevice device, int maxRetries = 3)
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        Console.WriteLine($"尝试打开设备 (第{attempt}次)...");
                        var stream = device.Open();

                        // 测试设备是否真正可用
                        stream.ReadTimeout = 500;
                        Console.WriteLine("设备打开成功");
                        return stream;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"设备被占用，等待释放... (第{attempt}次尝试)");
                        Thread.Sleep(1000); // 等待1秒
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"打开设备失败 (第{attempt}次): {ex.Message}");
                        if (attempt < maxRetries)
                        {
                            Thread.Sleep(500);
                        }
                    }
                }

                return null;
            }
            private void relativepos(double[] lastpos, double[] pos,out double[] xyz)
            {
                xyz = new double[3];
                double Re = 6378137;
                double arcdeg =  Math.PI/180;
                xyz[1] = (pos[1] - lastpos[1])*arcdeg * Re;
                xyz[0] = (pos[0] - lastpos[0]) * arcdeg * Math.Cos(lastpos[1] * arcdeg) * Re;
                xyz[2] = pos[2] - lastpos[2];
            }

            public GamepadState Parse(byte[] rawData)
            {
                // 验证数据长度
                if (rawData == null)
                    throw new ArgumentNullException(nameof(rawData), "原始通信数据不能为空");
                if (rawData.Length != GamepadPayloadLength)
                {
                    Console.WriteLine($"警告：数据长度为 {rawData.Length} 字节，期望{GamepadPayloadLength}字节");
                    // 如果数据长度不够，用0填充
                    byte[] paddedData = new byte[GamepadPayloadLength];
                    Array.Copy(rawData, 0, paddedData, 0, Math.Min(rawData.Length, GamepadPayloadLength));
                    rawData = paddedData;
                }

                var state = new GamepadState();

                try
                {
                    // 字节0-1: 左摇杆X轴，16位无符号整数，小端序，32768为中心
                    state.LeftStickX = BitConverter.ToUInt16(rawData, 0);

                    // 字节2-3: 左摇杆Y轴，16位无符号整数，小端序，32767为中心
                    state.LeftStickY = BitConverter.ToUInt16(rawData, 2);

                    // 字节4-5: 右摇杆X轴，16位无符号整数，小端序，32768为中心  
                    state.RightStickX = BitConverter.ToUInt16(rawData, 4);

                    // 字节6-7: 右摇杆Y轴，16位无符号整数，小端序，32767为中心
                    state.RightStickY = BitConverter.ToUInt16(rawData, 6);

                    // 字节8: 左扳机，1字节(0-255)，映射到16位范围(0-65535)
                    state.LeftTrigger = (ushort)(rawData[8]); // 255 * 257 = 65535

                    // 字节9: 右扳机，1字节(0-255)，映射到16位范围(0-65535)
                    state.RightTrigger = (ushort)(rawData[9]); // 255 * 257 = 65535

                    // 字节10: 主按键位，8位二进制位
                    byte mainKeys = rawData[10];
                    state.IsAKeyPressed = (mainKeys & (1 << 0)) != 0;      // bit0：A键
                    state.IsBKeyPressed = (mainKeys & (1 << 1)) != 0;      // bit1：B键
                    state.IsXKeyPressed = (mainKeys & (1 << 2)) != 0;      // bit2：X键
                    state.IsYKeyPressed = (mainKeys & (1 << 3)) != 0;      // bit3：Y键
                    state.IsLBKeyPressed = (mainKeys & (1 << 4)) != 0;     // bit4：LB键
                    state.IsRBKeyPressed = (mainKeys & (1 << 5)) != 0;     // bit5：RB键
                    state.IsSelectPressed = (mainKeys & (1 << 6)) != 0;    // bit6：视图键
                    state.IsMemuPressed = (mainKeys & (1 << 7)) != 0;      // bit7：菜单键

                    // 字节11: 方向键
                    state.Dpad = rawData[11];

                    // 字节12-13: 空，固定为0（跳过）

                    // 设置兼容性字段
                    state.IsStartPressed = state.IsMemuPressed;           // 菜单键作为Start键
                    state.IsShareKeyPressed = false;      // 自稳已移除，Share键保持关闭

                    // 其他字段保持默认值false
                    state.IsXboxKeyPressed = false;
                    state.IsLSKeyPressed = false;   // 摇杆按下状态在此协议中未定义
                    state.IsRSKeyPressed = false;   // 摇杆按下状态在此协议中未定义
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析游戏手柄数据错误: {ex.Message}");
                }

                return state;
            }


            /// <summary>
            /// 调试输出
            /// </summary>
            public  string ToDebugString(GamepadState state)
            {
                // 左摇杆方向判断
                string lsXDir = state.LeftStickX < 32768 ? "左" : (state.LeftStickX > 32768 ? "右" : "中");
                string lsYDir = state.LeftStickY < 32767 ? "上" : (state.LeftStickY > 32767 ? "下" : "中");
                // 右摇杆方向判断
                string rsXDir = state.RightStickX < 32768 ? "左" : (state.RightStickX > 32768 ? "右" : "中");
                string rsYDir = state.RightStickY < 32767 ? "上" : (state.RightStickY > 32767 ? "下" : "中");

                // 字符串拼接
                return "=== 手柄状态 ===\n" +
                       "左摇杆：X=" + state.LeftStickX + "（" + lsXDir + "），Y=" + state.LeftStickY + "（" + lsYDir + "）\n" +
                       "右摇杆：X=" + state.RightStickX + "（" + rsXDir + "），Y=" + state.RightStickY + "（" + rsYDir + "）\n" +
                       //"扳机：LT=" + state.LeftTrigger + "，RT=" + state.RightTrigger + "\n" +
                       "方向键：" + state.DpadDescription + "（原始值：" + state.Dpad + "）\n" +
                       "方向键映射：左=0x01 右=0x02 上=0x03 下=0x04，Y/A/X/B=0x05/0x06/0x07/0x08\n" +
                       "主按键：A=" + state.IsAKeyPressed + "，B=" + state.IsBKeyPressed + "，X=" + state.IsXKeyPressed + "，Y=" + state.IsYKeyPressed + "，LB=" + state.IsLBKeyPressed + "，RB=" + state.IsRBKeyPressed + "\n" +
                       "功能键：Select=" + state.IsSelectPressed + "，Start=" + state.IsStartPressed + "，Xbox=" + state.IsXboxKeyPressed + "，LS按下=" + state.IsLSKeyPressed + "，RS按下=" + state.IsRSKeyPressed + "\n" +
                       "Share键：" + state.IsShareKeyPressed;
            }

            private void bt_lock_Click(object sender, EventArgs e)
            {
                try
                {
                    if (selflockk)
                    {
                        // 当前是锁定状态，执行解锁操作
                        selflockk = false;
                        setdeep = 0;

                        // 更新按钮显示为"解锁"状态
                        bt_lock.Text = "解锁";
                        bt_lock.BackColor = Color.DodgerBlue;
                        bt_lock.ForeColor = SystemColors.ControlText;

                        // 更新控制面板状态
                        controlPanel?.SetLockButtonState(false);

                        Console.WriteLine("深度锁定已关闭");
                    }
                    else
                    {
                        // 当前是解锁状态，执行锁定操作 - 需要验证输入
                        string depthText = "";

                        // 从控制面板或textBox1获取深度值
                        if (controlPanel != null)
                        {
                            depthText = controlPanel.DepthValue;
                        }
                        else if (textBox1 != null)
                        {
                            depthText = textBox1.Text;
                        }
                        else
                        {
                            MessageBox.Show("深度输入框未初始化", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // 检查是否为空
                        if (string.IsNullOrEmpty(depthText))
                        {
                            MessageBox.Show("请先输入目标深度值", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBox1?.Focus(); // 聚焦到输入框
                            return;
                        }

                        // 尝试转换为float
                        if (float.TryParse(depthText, out float depth))
                        {
                            // 验证深度值范围（根据协议，深度应该≥0.00）
                            if (depth < 0)
                            {
                                MessageBox.Show("深度值不能为负数", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                textBox1?.SelectAll(); // 选中所有文本方便用户重新输入
                                textBox1?.Focus();
                                return;
                            }

                            // 可选：设置最大深度限制
                            if (depth > 20) // 假设最大深度20米
                            {
                                var result = MessageBox.Show($"深度值 {depth:F2}m 似乎过大，是否继续？",
                                                           "确认深度", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                if (result == DialogResult.No)
                                {
                                    textBox1?.SelectAll();
                                    textBox1?.Focus();
                                    return;
                                }
                            }

                            // 设置深度锁定
                            setdeep = depth;
                            selflockk = true;

                            // 更新按钮显示为"锁定"状态
                            bt_lock.Text = "锁定";
                            bt_lock.BackColor = Color.LightGreen;
                            bt_lock.ForeColor = Color.Black;

                            // 更新控制面板状态
                            controlPanel?.SetLockButtonState(true);

                            Console.WriteLine($"深度锁定已开启，目标深度: {setdeep:F2}m");
                        }
                        else
                        {
                            // 转换失败，显示错误信息
                            MessageBox.Show($"输入的深度值格式不正确: \"{depthText}\"\n请输入有效的数字（例如: 10.5）",
                                           "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            textBox1?.SelectAll(); // 选中错误文本
                            textBox1?.Focus();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"深度锁定操作错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine($"bt_lock_Click错误: {ex.Message}");
                }
            }
            private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
            {
                // 允许数字、小数点、退格键、删除键
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != (char)Keys.Back)
                {
                    e.Handled = true; // 阻止无效字符输入
                    return;
                }

                // 如果按下小数点，检查是否已经存在小数点
                if (e.KeyChar == '.')
                {
                    string currentText = textBox1.Text;
                    if (currentText.Contains("."))
                    {
                        e.Handled = true; // 阻止多个小数点
                    }
                }
            }
            // 更新UI显示当前状态
            private void UpdateRecordingStatus()
            {
                try
                {
                    if (isRecording)
                    {
                        // 显示录制状态
                        TimeSpan duration = DateTime.Now - recordStartTime;
                        string statusText = $"录制中... {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2} | 已记录: {recordedCount} 条";

                        // 如果有状态标签，更新它（请根据实际标签名称修改）
                        if (this.Controls.ContainsKey("label_status"))
                        {
                            Label statusLabel = this.Controls["label_status"] as Label;
                            if (statusLabel != null)
                                statusLabel.Text = statusText;
                        }

                        Console.WriteLine(statusText);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"更新录制状态错误: {ex.Message}");
                }
            }

            // 窗体关闭时的处理
            private void Form1_FormClosing(object sender, FormClosingEventArgs e)
            {
                try
                {
                    if (isRecording)
                    {
                        var result = MessageBox.Show("正在录制数据，是否保存并退出？",
                                                   "确认退出", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            StopRecording(); // 停止录制并保存数据
                        }
                        else if (result == DialogResult.Cancel)
                        {
                            e.Cancel = true; // 取消关闭
                            return;
                        }
                    }

                    // 其他清理工作...
                    _shouldStop = true;
                    _stopGamepad = true;
                    StopNetworkClient();
                    StopGamepadThread();
                    // ... 现有的清理代码 ...
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"窗体关闭处理错误: {ex.Message}");
                }
            }

            private void bt_light_Click(object sender, EventArgs e)
            {
                try
                {
                    if (controlPanel == null)
                    {
                        MessageBox.Show("控制面板未初始化", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 翻转照明状态
                    isLightOn = !isLightOn;

                    if (isLightOn)
                    {
                        // 打开照明
                        int selectedLevel = controlPanel.SelectedLightLevel;

                        if (selectedLevel < 0)
                        {
                            MessageBox.Show("请先选择照明亮度级别", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            isLightOn = false;
                            return;
                        }

                        // 更新亮度值
                        lightlevel = selectedLevel;

                        // 更新控制面板按钮状态
                        controlPanel.SetLightButtonState(true);

                        Console.WriteLine($"照明已打开，当前亮度: {lightlevel}/10 (0x{lightlevel:X2})");
                    }
                    else
                    {
                        // 关闭照明
                        lightlevel = 0;

                        // 更新控制面板按钮状态
                        controlPanel.SetLightButtonState(false);

                        Console.WriteLine("照明已关闭 (0x00)");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"照明控制错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Console.WriteLine($"bt_light_Click错误: {ex.Message}");
                    isLightOn = !isLightOn; // 回滚状态
                }
            }
            // 添加这个新方法
            private void UpdateLightLevel()
            {
                if (cb_light == null) return;

                if (isLightOn)
                {
                    // 照明开启时，使用选中的亮度值
                    int selectedIndex = cb_light.SelectedIndex;
                    if (selectedIndex == 0)
                    {
                        // 如果选中"关灯"但照明开启，设置为最低亮度
                        lightlevel = 1;
                        cb_light.SelectedIndex = 1; // 自动切换到亮度1
                    }
                    else
                    {
                        lightlevel = selectedIndex;
                    }
                }
                else
                {
                    // 照明关闭时，亮度为0
                    lightlevel = 0;
                }
            }
            private void cb_light_SelectedIndexChanged(object sender, EventArgs e)
            {
                try
                {
                    if (isLightOn && cb_light != null)
                    {
                        // 只有在照明开启时才实时更新亮度
                        UpdateLightLevel();
                        Console.WriteLine($"亮度已更新为: {lightlevel}/10 (0x{lightlevel:X2})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"亮度更新错误: {ex.Message}");
                }
            }

            private void label7_Click(object sender, EventArgs e)
            {

            }

            private void label6_Click(object sender, EventArgs e)
            {

            }

            private void label2_Click(object sender, EventArgs e)
            {

            }

            private void label17_Click(object sender, EventArgs e)
            {

            }

            private void label17_Click_1(object sender, EventArgs e)
            {

            }

            private void panel6_Paint(object sender, PaintEventArgs e)
            {

            }

            private void panel7_Paint(object sender, PaintEventArgs e)
            {
                if (panel7 == null) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var rect = panel7.ClientRectangle;
                rect.Inflate(-4, -4);
                if (rect.Width <= 0 || rect.Height <= 0) return;

                int headerHeight = 36;
                if (headerHeight > rect.Height - 10) headerHeight = Math.Max(20, rect.Height / 4);

                var headerRect = new Rectangle(rect.Left + 8, rect.Top + 6, rect.Width - 16, headerHeight);
                var contentRect = new Rectangle(rect.Left + 8, headerRect.Bottom + 6, rect.Width - 16, rect.Bottom - headerRect.Bottom - 12);

                using (var bgBrush = new LinearGradientBrush(rect, Color.FromArgb(18, 26, 40), Color.FromArgb(8, 14, 26), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, rect);
                }

                using (var gridPen = new Pen(Color.FromArgb(22, 0, 255, 240), 1f))
                {
                    for (int x = contentRect.Left; x < contentRect.Right; x += 18)
                    {
                        g.DrawLine(gridPen, x, contentRect.Top, x, contentRect.Bottom);
                    }
                    for (int y = contentRect.Top; y < contentRect.Bottom; y += 18)
                    {
                        g.DrawLine(gridPen, contentRect.Left, y, contentRect.Right, y);
                    }
                }

                using (var headerBrush = new LinearGradientBrush(headerRect, Color.FromArgb(20, 60, 70), Color.FromArgb(10, 28, 36), LinearGradientMode.Vertical))
                using (var headerGlow = new LinearGradientBrush(headerRect, Color.FromArgb(80, 0, 200, 220), Color.FromArgb(20, 0, 160, 120), LinearGradientMode.Horizontal))
                using (var headerLine = new Pen(Color.FromArgb(180, 80, 220, 220), 1.6f))
                {
                    g.FillRectangle(headerBrush, headerRect);
                    g.FillRectangle(headerGlow, new Rectangle(headerRect.Left, headerRect.Top, headerRect.Width, 3));
                    g.DrawLine(headerLine, headerRect.Left, headerRect.Bottom, headerRect.Right, headerRect.Bottom);
                }

                using (var borderPen = new Pen(Color.FromArgb(200, 0, 220, 210), 1.6f))
                using (var glowPen = new Pen(Color.FromArgb(120, 0, 200, 180), 3f))
                {
                    g.DrawRectangle(glowPen, rect);
                    g.DrawRectangle(borderPen, rect);

                    int tri = 16;
                    var tl = new Point[]
                    {
                        new Point(rect.Left, rect.Top),
                        new Point(rect.Left + tri, rect.Top),
                        new Point(rect.Left, rect.Top + tri)
                    };
                    var tr = new Point[]
                    {
                        new Point(rect.Right, rect.Top),
                        new Point(rect.Right - tri, rect.Top),
                        new Point(rect.Right, rect.Top + tri)
                    };
                    var bl = new Point[]
                    {
                        new Point(rect.Left, rect.Bottom),
                        new Point(rect.Left + tri, rect.Bottom),
                        new Point(rect.Left, rect.Bottom - tri)
                    };
                    var br = new Point[]
                    {
                        new Point(rect.Right, rect.Bottom),
                        new Point(rect.Right - tri, rect.Bottom),
                        new Point(rect.Right, rect.Bottom - tri)
                    };
                    g.DrawPolygon(borderPen, tl);
                    g.DrawPolygon(borderPen, tr);
                    g.DrawPolygon(borderPen, bl);
                    g.DrawPolygon(borderPen, br);
                }

                using (var accentPen = new Pen(Color.FromArgb(200, 120, 255, 230), 1f))
                {
                    int tick = 18;
                    g.DrawLine(accentPen, headerRect.Left + 6, headerRect.Top + 8, headerRect.Left + 6 + tick, headerRect.Top + 8);
                    g.DrawLine(accentPen, headerRect.Right - 6 - tick, headerRect.Top + 8, headerRect.Right - 6, headerRect.Top + 8);
                    using (var titleFont = new Font("Microsoft YaHei", 12f, FontStyle.Italic))
                    using (var titleBrush = new SolidBrush(Color.FromArgb(230, 200, 255, 250)))
                    using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("传感器信息", titleFont, titleBrush, headerRect, format);
                    }
                }

            }

            private void label17_Click_2(object sender, EventArgs e)
            {

            }

            private void PositionMapToolbar()
            {
                if (mapViewControl == null) return;
                if (mapToolbarPanel == null) return;
                var targetParent = mapViewControl.Parent ?? GetMainContentHost();
                if (mapToolbarPanel.Parent != targetParent)
                {
                    mapToolbarPanel.Parent?.Controls.Remove(mapToolbarPanel);
                    targetParent.Controls.Add(mapToolbarPanel);
                }
                mapToolbarPanel.Width = mapViewControl.Width;
                mapToolbarPanel.Location = new Point(mapViewControl.Left, mapViewControl.Bottom + 6);
            }

            private void CreateMapToolbar()
            {
                if (mapToolbarPanel != null) return;
                var targetParent = mapViewControl?.Parent ?? GetMainContentHost();
                mapToolbarPanel = new Panel
                {
                    Height = 32,
                    BackColor = Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left
                };
                btnMapExpand = new Button
                {
                    Text = "放大",
                    AutoSize = false,
                    Size = new Size(56, 26)
                };
                btnMapExpand.Click += (s, e) => MapViewControl_ExpandRequested(s, e);
                btnMapZoomIn = new Button
                {
                    Text = "+",
                    AutoSize = false,
                    Size = new Size(36, 26)
                };
                btnMapZoomIn.Click += (s, e) => { try { mapViewControl?.ZoomIn(); } catch { } };
                btnMapZoomOut = new Button
                {
                    Text = "-",
                    AutoSize = false,
                    Size = new Size(36, 26)
                };
                btnMapZoomOut.Click += (s, e) => { try { mapViewControl?.ZoomOut(); } catch { } };
                mapToolbarPanel.Controls.Add(btnMapZoomOut);
                mapToolbarPanel.Controls.Add(btnMapZoomIn);
                mapToolbarPanel.Controls.Add(btnMapExpand);
                targetParent.Controls.Add(mapToolbarPanel);
                btnMapZoomOut.Location = new Point(0, 3);
                btnMapZoomIn.Location = new Point(btnMapZoomOut.Right + 6, 3);
                btnMapExpand.Location = new Point(btnMapZoomIn.Right + 10, 3);
            }

        private void panel2_Paint_1(object sender, PaintEventArgs e)
        {

        }

        private void panel4_Paint(object sender, PaintEventArgs e)
        {

        }

        private void AdjustMainPageHostWidth()
        {
            if (mainPageHost == null) return;
            int navWidth = navigationPanel?.Width ?? NavigationPanelWidth;
            int targetWidth = Math.Max(ClientSize.Width - navWidth, 0);
            mainPageHost.Width = targetWidth;
        }

        private void ApplyAppIcon()
        {
            try
            {
                string runPath = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(runPath, "Resources", "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Set icon failed: {ex.Message}");
            }
        }

        private void InitializeDualPageInterface()
        {
            if (dualPageInitialized || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            dualPageInitialized = true;

            var legacyControls = this.Controls.Cast<Control>().ToList();
            this.ClientSize = new Size(this.ClientSize.Width + NavigationPanelWidth, this.ClientSize.Height);

            this.SuspendLayout();
            navigationPanel = BuildNavigationPanel();
            mainPageHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Name = "mainPageHost"
            };

            pidPagePanel = BuildPidPagePanel();
            pidPagePanel.Dock = DockStyle.Fill;
            pidPagePanel.Visible = true;

            this.Controls.Add(pidPagePanel);
            this.Controls.Add(mainPageHost);
            this.Controls.Add(navigationPanel);

            foreach (var ctrl in legacyControls)
            {
                this.Controls.Remove(ctrl);
                mainPageHost.Controls.Add(ctrl);
            }

            ShowMainPage();
            this.ResumeLayout();
        }

        private Control GetMainContentHost()
        {
            return mainPageHost ?? (Control)this;
        }

        private Panel BuildNavigationPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Left,
                Width = NavigationPanelWidth,
                BackColor = Color.FromArgb(6, 10, 26)
            };

            // ✅ 赛博风格顶部装饰条
            var accentBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 4,
                BackColor = Color.FromArgb(0, 190, 255)
            };

            var titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 60,
                Text = "CYBER SWITCH",
                ForeColor = Color.FromArgb(0, 190, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };

            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(10, 30, 10, 10)
            };

            // ✅ 创建增强版导航按钮（带融合效果）
            btnMainPage = CreateEnhancedNavButton("主界面", "SYSTEM CORE");
            btnPidPage = CreateEnhancedNavButton("PID 调参", "CYBER LAB");

            btnMainPage.Click += OnNavButtonClicked;
            btnPidPage.Click += OnNavButtonClicked;

            stack.Controls.Add(btnMainPage);
            stack.Controls.Add(btnPidPage);

            panel.Controls.Add(stack);
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(accentBar);

            return panel;
        }
        /// <summary>
        /// 创建现代化导航按钮 - Discord/Notion 风格
        /// </summary>
        private Button CreateEnhancedNavButton(string title, string subtitle)
        {
            var button = new Button
            {
                Width = NavigationPanelWidth - 20,
                Height = 75,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 10, 0),
                Margin = new Padding(10, 0, 10, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,  // 关键：透明背景
                Font = new Font("微软雅黑", 10F),
                Cursor = Cursors.Hand,
                Tag = new { Title = title, Subtitle = subtitle }  // 存储文本信息
            };

            // 移除默认边框
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;

            // 完全自定义绘制
            button.Paint += (s, e) =>
            {
                var btn = s as Button;
                if (btn?.Tag == null) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var rect = btn.ClientRectangle;
                bool isSelected = IsButtonSelected(btn);
                dynamic data = btn.Tag;

                // ═══════════════════════════════════════════════════════
                // 背景绘制
                // ═══════════════════════════════════════════════════════
                if (isSelected)
                {
                    // 选中状态：深色圆角背景
                    using (var path = GetRoundedRect(new Rectangle(2, 2, rect.Width - 4, rect.Height - 4), 8))
                    using (var bgBrush = new SolidBrush(Color.FromArgb(25, 35, 55)))
                    {
                        g.FillPath(bgBrush, path);
                    }

                    // 左侧蓝色指示器
                    using (var indicatorBrush = new LinearGradientBrush(
                               new Rectangle(0, 10, 4, rect.Height - 20),
                               Color.FromArgb(0, 180, 255),
                               Color.FromArgb(0, 220, 255),
                               LinearGradientMode.Vertical))
                    {
                        var indicatorPath = GetRoundedRect(new Rectangle(6, 15, 4, rect.Height - 30), 2);
                        g.FillPath(indicatorBrush, indicatorPath);
                    }

                    // 右侧渐变淡出（融合感）
                    using (var fadeoutBrush = new LinearGradientBrush(
                               new Rectangle(rect.Width - 40, 0, 40, rect.Height),
                               Color.FromArgb(30, 25, 35, 55),
                               Color.FromArgb(0, 25, 35, 55),
                               LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(fadeoutBrush, rect.Width - 40, 0, 40, rect.Height);
                    }
                }
                else
                {
                    // 未选中：鼠标悬停显示淡背景
                    var mousePos = btn.PointToClient(System.Windows.Forms.Cursor.Position);
                    if (btn.ClientRectangle.Contains(mousePos))
                    {
                        using (var path = GetRoundedRect(new Rectangle(2, 2, rect.Width - 4, rect.Height - 4), 8))
                        using (var hoverBrush = new SolidBrush(Color.FromArgb(15, 255, 255, 255)))
                        {
                            g.FillPath(hoverBrush, path);
                        }
                    }
                }

                // ═══════════════════════════════════════════════════════
                // 文字绘制
                // ═══════════════════════════════════════════════════════
                int textStartX = 24;

                // 主标题
                using (var titleFont = new Font("微软雅黑", 11F, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(isSelected
                           ? Color.FromArgb(200, 230, 255)   // 选中：亮色
                           : Color.FromArgb(150, 180, 210))) // 未选中：暗色
                {
                    g.DrawString(data.Title, titleFont, titleBrush, textStartX, 18);
                }

                // 副标题
                using (var subtitleFont = new Font("微软雅黑", 9F))
                using (var subtitleBrush = new SolidBrush(Color.FromArgb(100, 130, 160)))
                {
                    g.DrawString(data.Subtitle, subtitleFont, subtitleBrush, textStartX, 42);
                }

                // ═══════════════════════════════════════════════════════
                // 选中状态：右侧箭头图标
                // ═══════════════════════════════════════════════════════
                if (isSelected)
                {
                    using (var arrowPen = new Pen(Color.FromArgb(0, 200, 255), 2))
                    {
                        int arrowX = rect.Width - 20;
                        int arrowY = rect.Height / 2;
                        g.DrawLine(arrowPen, arrowX - 5, arrowY - 5, arrowX, arrowY);
                        g.DrawLine(arrowPen, arrowX - 5, arrowY + 5, arrowX, arrowY);
                    }
                }
            };

            // 鼠标交互
            button.MouseEnter += (s, e) => button.Invalidate();
            button.MouseLeave += (s, e) => button.Invalidate();
            button.MouseDown += (s, e) => button.Invalidate();
            button.MouseUp += (s, e) => button.Invalidate();

            return button;
        }



        /// <summary>
        /// 判断按钮是否为当前选中状态
        /// </summary>
        private bool IsButtonSelected(Button button)
        {
            if (button == null) return false;

            // 根据当前显示的页面判断
            if (mainPageHost != null && pidPagePanel != null)
            {
                if (button == btnMainPage)
                {
                    return mainPageHost.Visible;
                }
                else if (button == btnPidPage)
                {
                    return pidPagePanel.Visible;
                }
            }

            return false;
        }


        private Button CreateNavButton(string title, string subtitle)
        {
            var button = new Button
            {
                Width = NavigationPanelWidth - 30,
                Height = 90,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 20),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(120, 200, 255),
                BackColor = Color.FromArgb(20, 28, 48),
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 170, 255);
            button.Text = $"{title}\r\n{subtitle}";
            return button;
        }

        private void OnNavButtonClicked(object sender, EventArgs e)
        {
            if (ReferenceEquals(sender, btnMainPage))
            {
                ShowMainPage();
            }
            else if (ReferenceEquals(sender, btnPidPage))
            {
                ShowPidPage();
            }
        }

        private void ShowMainPage()
        {
            if (mainPageHost == null || pidPagePanel == null)
            {
                return;
            }

            mainPageHost.Visible = true;
            pidPagePanel.Visible = false;
            mainPageHost.BringToFront();
            cyberWindowControls?.BringToFront();
            UpdateNavSelection(btnMainPage);
        }

        private void ShowPidPage()
        {
            if (mainPageHost == null || pidPagePanel == null)
            {
                return;
            }

            pidPagePanel.Visible = true;
            mainPageHost.Visible = false;
            pidPagePanel.BringToFront();
            cyberWindowControls?.BringToFront();
            UpdateNavSelection(btnPidPage);
        }

        private void UpdateNavSelection(Button selected)
        {
            foreach (var btn in new[] { btnMainPage, btnPidPage })
            {
                if (btn == null) continue;

                bool isActive = ReferenceEquals(btn, selected);

                // ✅ 更新颜色状态
                btn.BackColor = isActive
                    ? Color.FromArgb(15, 35, 60)      // 选中：深色，融合主界面
                    : Color.FromArgb(20, 28, 48);     // 未选中：独立深灰色

                btn.ForeColor = isActive
                    ? Color.FromArgb(0, 255, 220)     // 选中：高亮青色
                    : Color.FromArgb(120, 200, 255);  // 未选中：柔和蓝色

                // ★ 关键：触发按钮重绘，应用自定义边框效果
                btn.Invalidate();
            }
        }

        /// <summary>
        /// 创建现代化导航按钮 - 参考主流应用的设计语言
        /// </summary>
        private Button CreateModernNavButton(string title, string subtitle)
        {
            var button = new Button
            {
                Width = NavigationPanelWidth - 20,
                Height = 75,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 10, 0),
                Margin = new Padding(10, 0, 10, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,  // ★ 关键：透明背景
                Font = new Font("微软雅黑", 10F),
                Cursor = Cursors.Hand,
                Tag = new { Title = title, Subtitle = subtitle }  // 存储文本信息
            };

            // 移除默认边框
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;

            // ✅ 完全自定义绘制
            button.Paint += (s, e) =>
            {
                var btn = s as Button;
                if (btn?.Tag == null) return;

                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = btn.ClientRectangle;
                bool isSelected = IsButtonSelected(btn);
                var data = btn.Tag as dynamic;

                // ═══════════════════════════════════════════════════════
                // 方案 A：Discord/Notion 风格 - 圆角卡片 + 左侧指示器
                // ═══════════════════════════════════════════════════════
                if (isSelected)
                {
                    // ★ 选中状态：深色背景 + 左侧蓝色竖条
                    using (var path = GetRoundedRect(new Rectangle(2, 2, rect.Width - 4, rect.Height - 4), 8))
                    using (var bgBrush = new SolidBrush(Color.FromArgb(25, 35, 55)))
                    {
                        g.FillPath(bgBrush, path);
                    }

                    // 左侧指示器（蓝色竖条）
                    using (var indicatorBrush = new LinearGradientBrush(
                               new Rectangle(0, 10, 4, rect.Height - 20),
                               Color.FromArgb(0, 180, 255),
                               Color.FromArgb(0, 220, 255),
                               LinearGradientMode.Vertical))
                    {
                        var indicatorPath = GetRoundedRect(new Rectangle(6, 15, 4, rect.Height - 30), 2);
                        g.FillPath(indicatorBrush, indicatorPath);
                    }

                    // 微弱的右侧渐变（融合感）
                    using (var fadeoutBrush = new LinearGradientBrush(
                               new Rectangle(rect.Width - 40, 0, 40, rect.Height),
                               Color.FromArgb(30, 25, 35, 55),
                               Color.FromArgb(0, 25, 35, 55),
                               LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(fadeoutBrush, rect.Width - 40, 0, 40, rect.Height);
                    }
                }
                else
                {
                    // ★ 未选中状态：鼠标悬停时显示淡背景
                    var mousePos = btn.PointToClient(System.Windows.Forms.Cursor.Position);
                    if (btn.ClientRectangle.Contains(mousePos))
                    {
                        using (var path = GetRoundedRect(new Rectangle(2, 2, rect.Width - 4, rect.Height - 4), 8))
                        using (var hoverBrush = new SolidBrush(Color.FromArgb(15, 255, 255, 255)))
                        {
                            g.FillPath(hoverBrush, path);
                        }
                    }
                }

                // ═══════════════════════════════════════════════════════
                // 绘制文字内容（两行文本布局）
                // ═══════════════════════════════════════════════════════
                int textStartX = 24;

                // 标题（粗体，较大）
                using (var titleFont = new Font("微软雅黑", 11F, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(isSelected ? Color.FromArgb(200, 230, 255) : Color.FromArgb(150, 180, 210)))
                {
                    g.DrawString(data.Title, titleFont, titleBrush, textStartX, 18);
                }

                // 副标题（常规，较小）
                using (var subtitleFont = new Font("微软雅黑", 9F))
                using (var subtitleBrush = new SolidBrush(Color.FromArgb(100, 130, 160)))
                {
                    g.DrawString(data.Subtitle, subtitleFont, subtitleBrush, textStartX, 42);
                }

                // ═══════════════════════════════════════════════════════
                // 可选：右侧图标或箭头（表示可点击）
                // ═══════════════════════════════════════════════════════
                if (isSelected)
                {
                    using (var arrowPen = new Pen(Color.FromArgb(0, 200, 255), 2))
                    {
                        // 简单的右箭头图标
                        int arrowX = rect.Width - 20;
                        int arrowY = rect.Height / 2;
                        g.DrawLine(arrowPen, arrowX - 5, arrowY - 5, arrowX, arrowY);
                        g.DrawLine(arrowPen, arrowX - 5, arrowY + 5, arrowX, arrowY);
                    }
                }
            };

            // 鼠标交互
            button.MouseEnter += (s, e) => button.Invalidate();
            button.MouseLeave += (s, e) => button.Invalidate();
            button.MouseDown += (s, e) => button.Invalidate();
            button.MouseUp += (s, e) => button.Invalidate();

            return button;
        }

        /// <summary>
        /// 辅助方法：创建圆角矩形路径
        /// </summary>
        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            // 左上角
            path.AddArc(arc, 180, 90);

            // 右上角
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // 右下角
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // 左下角
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private Panel BuildPidPagePanel()
        {
            pidEditors.Clear();
            pidDefaultValues.Clear();

            var pidCards = new (string key, string title, string subtitle, string description, decimal kp, decimal ki, decimal kd)[]
            {
        ("YawOuter", "偏航角环 (外环)", "Yaw Angle PID", "级联外环：角度误差 -> 目标角速度 (g_pid_controllers[PID_YAW])。", 1.2m, 0.05m, 0.0m),
        ("YawRate", "偏航角速率环 (内环)", "Yaw Rate PID", "级联内环：角速度误差 -> 推进器指令 (g_yaw_rate_pid)。", 0.04m, 0.002m, 0.0005m),
        ("RollOuter", "横滚角环 (外环)", "Roll Angle PID", "级联外环：角度误差 -> 目标角速度 (g_pid_controllers[PID_ROLL])。", 0.8m, 0.02m, 0.0m),
        ("RollRate", "横滚角速率环 (内环)", "Roll Rate PID", "级联内环：角速度误差 -> 推进器指令 (g_roll_rate_pid)。", 0.035m, 0.0015m, 0.0003m),
        ("Depth", "定水深 (单环)", "Depth Hold PID", "单环：深度误差 -> 垂直推进器 (g_pid_controllers[PID_DEPTH])。", -5.0m, -0.02m, 0.0m)
            };

            var host = new Panel
            {
                BackColor = pidPageBackground,
                Padding = new Padding(40, 30, 40, 170),
                Name = "pidPagePanel"
            };

            // ★ 增强的背景绘制 - 添加网格、光效、装饰图案
            host.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var rect = new Rectangle(0, 0, host.Width - 1, host.Height - 1);

                // 1. 深色渐变背景
                using (var brush = new LinearGradientBrush(rect,
                           Color.FromArgb(24, 34, 54),
                           Color.FromArgb(7, 18, 32),
                           LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, rect);
                }

                // 2. 绘制赛博网格背景
                DrawCyberGrid(g, rect);

                // 3. 绘制装饰性电路板图案
                DrawCircuitPattern(g, rect);

                // 4. 主边框 - 双层发光效果
                using (var glowPen = new Pen(Color.FromArgb(60, 0, 220, 255), 4))
                {
                    g.DrawRectangle(glowPen, Rectangle.Inflate(rect, -2, -2));
                }
                using (var borderPen = new Pen(Color.FromArgb(200, 0, 200, 255), 2))
                {
                    g.DrawRectangle(borderPen, rect);
                }

                // 5. 内部装饰框 - 科技感层次
                using (var innerPen = new Pen(Color.FromArgb(80, 0, 180, 255), 1))
                {
                    var inner = Rectangle.Inflate(rect, -12, -12);
                    g.DrawRectangle(innerPen, inner);
                }

                // 6. 四角装饰 - 三角形切角设计
                DrawCornerDecorations(g, rect);

                // 7. 顶部装饰条纹
                DrawTopAccentBars(g, rect);

                // 8. 底部状态指示灯
                DrawStatusIndicators(g, rect);
            };

            host.MouseDown += Form_MouseDown_Drag;

            // ★ 增强的标题区域
            var titleContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = Color.Transparent
            };

            titleContainer.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 标题背景装饰板
                var titleRect = new Rectangle(20, 10, titleContainer.Width - 40, 100);
                using (var titleBrush = new LinearGradientBrush(titleRect,
                           Color.FromArgb(30, 0, 160, 200),
                           Color.FromArgb(10, 0, 80, 120),
                           LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(titleBrush, titleRect);
                }

                // 标题装饰边框
                using (var pen = new Pen(Color.FromArgb(150, 0, 220, 255), 2))
                {
                    g.DrawRectangle(pen, titleRect);
                }

                // 左侧装饰图案 - 齿轮/芯片符号
                DrawTitleIcon(g, new Rectangle(30, 20, 80, 80));
            };

            var title = new Label
            {
                Text = "⚙ PID 调参实验舱 ⚙",
                ForeColor = Color.FromArgb(0, 240, 255),
                Font = new Font("微软雅黑", 22F, FontStyle.Bold),
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 55,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 10, 0, 0)
            };

            var subtitle = new Label
            {
                ForeColor = Color.FromArgb(150, 200, 255),
                Font = new Font("Consolas", 11F, FontStyle.Italic),
                Text = "┤ 数据映射：firmware/User/Src/pid_control.c  →  Depth / Yaw / Roll ├",
                Padding = new Padding(0, 5, 0, 10),
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 35,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 装饰分隔线
            var separator = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = Color.FromArgb(0, 200, 255)
            };

            titleContainer.Controls.Add(separator);
            titleContainer.Controls.Add(subtitle);
            titleContainer.Controls.Add(title);

            var cardsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(0, 30, 0, 0),
                BackColor = Color.Transparent,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var yawOuter = pidCards[0];
            var yawRate = pidCards[1];
            var rollOuter = pidCards[2];
            var rollRate = pidCards[3];
            var depth = pidCards[4];

            var yawOuterCard = CreatePidCard(yawOuter.key, yawOuter.title, yawOuter.subtitle, yawOuter.description, yawOuter.kp, yawOuter.ki, yawOuter.kd);
            var yawRateCard = CreatePidCard(yawRate.key, yawRate.title, yawRate.subtitle, yawRate.description, yawRate.kp, yawRate.ki, yawRate.kd);
            var rollOuterCard = CreatePidCard(rollOuter.key, rollOuter.title, rollOuter.subtitle, rollOuter.description, rollOuter.kp, rollOuter.ki, rollOuter.kd);
            var rollRateCard = CreatePidCard(rollRate.key, rollRate.title, rollRate.subtitle, rollRate.description, rollRate.kp, rollRate.ki, rollRate.kd);
            var depthCard = CreatePidCard(depth.key, depth.title, depth.subtitle, depth.description, depth.kp, depth.ki, depth.kd);

            cardsLayout.Controls.Add(yawOuterCard, 0, 0);
            cardsLayout.Controls.Add(yawRateCard, 0, 1);
            cardsLayout.Controls.Add(rollOuterCard, 1, 0);
            cardsLayout.Controls.Add(rollRateCard, 1, 1);
            cardsLayout.Controls.Add(depthCard, 2, 0);
            cardsLayout.SetRowSpan(depthCard, 2);

            // ★ 增强的操作栏
            var actionBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                Padding = new Padding(20, 10, 20, 10),
                BackColor = Color.Transparent
            };

            actionBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var barRect = actionBar.ClientRectangle;

                // 操作栏背景
                using (var brush = new LinearGradientBrush(barRect,
                           Color.FromArgb(20, 10, 30, 45),
                           Color.FromArgb(10, 4, 12, 20),
                           LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, barRect);
                }

                // 顶部装饰线
                using (var pen = new Pen(Color.FromArgb(0, 200, 255), 2))
                {
                    g.DrawLine(pen, 0, 0, barRect.Width, 0);
                }

                // 装饰角标
                DrawActionBarDecorations(g, barRect);
            };

            pidStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(0, 255, 200),
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                Text = "⚡ 系统就绪 - 等待指令...",
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var applyButton = CreateEnhancedActionButton("▶ 下发参数", Color.FromArgb(0, 220, 255));
            applyButton.Click += (s, e) => ApplyPidSettings();

            var resetButton = CreateEnhancedActionButton("↻ 恢复默认", Color.FromArgb(0, 180, 220));
            resetButton.Click += (s, e) => ResetPidEditors();

            var actionButtonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0, 5, 0, 0)
            };

            actionButtonsPanel.Controls.Add(applyButton);
            actionButtonsPanel.Controls.Add(resetButton);

            actionBar.Controls.Add(actionButtonsPanel);
            actionBar.Controls.Add(pidStatusLabel);

            host.Controls.Add(cardsLayout);
            host.Controls.Add(actionBar);
            host.Controls.Add(titleContainer);

            return host;
        }

        // ========== 新增装饰绘制方法 ==========

        /// <summary>
        /// 绘制赛博网格背景
        /// </summary>
        private void DrawCyberGrid(Graphics g, Rectangle bounds)
        {
            using (var gridPen = new Pen(Color.FromArgb(15, 0, 180, 220), 1))
            {
                int gridSize = 40;

                // 垂直线
                for (int x = 0; x < bounds.Width; x += gridSize)
                {
                    g.DrawLine(gridPen, x, 0, x, bounds.Height);
                }

                // 水平线
                for (int y = 0; y < bounds.Height; y += gridSize)
                {
                    g.DrawLine(gridPen, 0, y, bounds.Width, y);
                }

                // 交叉点加强
                using (var dotBrush = new SolidBrush(Color.FromArgb(30, 0, 200, 255)))
                {
                    for (int x = 0; x < bounds.Width; x += gridSize)
                    {
                        for (int y = 0; y < bounds.Height; y += gridSize)
                        {
                            g.FillEllipse(dotBrush, x - 2, y - 2, 4, 4);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 绘制电路板装饰图案
        /// </summary>
        private void DrawCircuitPattern(Graphics g, Rectangle bounds)
        {
            using (var pen = new Pen(Color.FromArgb(20, 0, 220, 180), 1.5f))
            {
                var rng = new Random(42); // 固定种子保持图案一致

                // 随机电路走线
                for (int i = 0; i < 15; i++)
                {
                    int x1 = rng.Next(bounds.Width);
                    int y1 = rng.Next(bounds.Height);
                    int x2 = x1 + rng.Next(-100, 100);
                    int y2 = y1 + rng.Next(-100, 100);

                    g.DrawLine(pen, x1, y1, x2, y2);

                    // 连接点
                    using (var brush = new SolidBrush(Color.FromArgb(40, 0, 255, 200)))
                    {
                        g.FillEllipse(brush, x1 - 3, y1 - 3, 6, 6);
                        g.FillEllipse(brush, x2 - 3, y2 - 3, 6, 6);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制四角装饰 - 切角三角形
        /// </summary>
        private void DrawCornerDecorations(Graphics g, Rectangle bounds)
        {
            int size = 30;
            using (var pen = new Pen(Color.FromArgb(180, 0, 220, 255), 2))
            using (var fillBrush = new SolidBrush(Color.FromArgb(30, 0, 180, 220)))
            {
                // 左上角
                Point[] topLeft = {
            new Point(bounds.Left, bounds.Top + size),
            new Point(bounds.Left + size, bounds.Top),
            new Point(bounds.Left, bounds.Top)
        };
                g.FillPolygon(fillBrush, topLeft);
                g.DrawPolygon(pen, topLeft);

                // 右上角
                Point[] topRight = {
            new Point(bounds.Right - size, bounds.Top),
            new Point(bounds.Right, bounds.Top + size),
            new Point(bounds.Right, bounds.Top)
        };
                g.FillPolygon(fillBrush, topRight);
                g.DrawPolygon(pen, topRight);

                // 左下角
                Point[] bottomLeft = {
            new Point(bounds.Left, bounds.Bottom - size),
            new Point(bounds.Left + size, bounds.Bottom),
            new Point(bounds.Left, bounds.Bottom)
        };
                g.FillPolygon(fillBrush, bottomLeft);
                g.DrawPolygon(pen, bottomLeft);

                // 右下角
                Point[] bottomRight = {
            new Point(bounds.Right - size, bounds.Bottom),
            new Point(bounds.Right, bounds.Bottom - size),
            new Point(bounds.Right, bounds.Bottom)
        };
                g.FillPolygon(fillBrush, bottomRight);
                g.DrawPolygon(pen, bottomRight);
            }

            // 角标装饰线
            using (var accentPen = new Pen(Color.FromArgb(0, 255, 220), 1.5f))
            {
                int offset = 40;
                int length = 25;

                // 左上
                g.DrawLine(accentPen, bounds.Left + offset, bounds.Top + 10, bounds.Left + offset + length, bounds.Top + 10);
                g.DrawLine(accentPen, bounds.Left + 10, bounds.Top + offset, bounds.Left + 10, bounds.Top + offset + length);

                // 右上
                g.DrawLine(accentPen, bounds.Right - offset - length, bounds.Top + 10, bounds.Right - offset, bounds.Top + 10);
                g.DrawLine(accentPen, bounds.Right - 10, bounds.Top + offset, bounds.Right - 10, bounds.Top + offset + length);

                // 左下
                g.DrawLine(accentPen, bounds.Left + offset, bounds.Bottom - 10, bounds.Left + offset + length, bounds.Bottom - 10);
                g.DrawLine(accentPen, bounds.Left + 10, bounds.Bottom - offset - length, bounds.Left + 10, bounds.Bottom - offset);

                // 右下
                g.DrawLine(accentPen, bounds.Right - offset - length, bounds.Bottom - 10, bounds.Right - offset, bounds.Bottom - 10);
                g.DrawLine(accentPen, bounds.Right - 10, bounds.Bottom - offset - length, bounds.Right - 10, bounds.Bottom - offset);
            }
        }

        /// <summary>
        /// 绘制顶部装饰条纹
        /// </summary>
        private void DrawTopAccentBars(Graphics g, Rectangle bounds)
        {
            using (var pen = new Pen(Color.FromArgb(100, 0, 255, 220), 2))
            {
                int y = 50;
                int spacing = 8;
                int barLength = 60;

                for (int i = 0; i < 5; i++)
                {
                    int x = bounds.Width / 2 - 150 + i * (barLength + spacing);
                    g.DrawLine(pen, x, y, x + barLength, y);
                }
            }
        }

        /// <summary>
        /// 绘制底部状态指示灯
        /// </summary>
        private void DrawStatusIndicators(Graphics g, Rectangle bounds)
        {
            int y = bounds.Bottom - 30;
            int spacing = 15;
            int dotSize = 8;

            var colors = new Color[] {
        Color.FromArgb(0, 255, 100),  // 绿色 - 系统正常
        Color.FromArgb(0, 200, 255),  // 蓝色 - 网络连接
        Color.FromArgb(255, 200, 0)   // 黄色 - 数据传输
    };

            for (int i = 0; i < colors.Length; i++)
            {
                int x = bounds.Width / 2 - (colors.Length * (dotSize + spacing)) / 2 + i * (dotSize + spacing);

                // 发光效果
                using (var glowBrush = new SolidBrush(Color.FromArgb(80, colors[i])))
                {
                    g.FillEllipse(glowBrush, x - 4, y - 4, dotSize + 8, dotSize + 8);
                }

                // 指示灯本体
                using (var dotBrush = new SolidBrush(colors[i]))
                {
                    g.FillEllipse(dotBrush, x, y, dotSize, dotSize);
                }
            }
        }

        /// <summary>
        /// 绘制标题区域装饰图标
        /// </summary>
        private void DrawTitleIcon(Graphics g, Rectangle iconRect)
        {
            using (var pen = new Pen(Color.FromArgb(0, 220, 255), 2.5f))
            using (var fillBrush = new SolidBrush(Color.FromArgb(30, 0, 180, 220)))
            {
                // 绘制六边形芯片符号
                Point center = new Point(iconRect.Left + iconRect.Width / 2, iconRect.Top + iconRect.Height / 2);
                int radius = 30;
                Point[] hexagon = new Point[6];

                for (int i = 0; i < 6; i++)
                {
                    double angle = Math.PI / 3 * i;
                    hexagon[i] = new Point(
                        center.X + (int)(radius * Math.Cos(angle)),
                        center.Y + (int)(radius * Math.Sin(angle))
                    );
                }

                g.FillPolygon(fillBrush, hexagon);
                g.DrawPolygon(pen, hexagon);

                // 内部装饰线
                using (var innerPen = new Pen(Color.FromArgb(0, 255, 220), 1.5f))
                {
                    g.DrawEllipse(innerPen, center.X - 15, center.Y - 15, 30, 30);
                    g.DrawLine(innerPen, center.X - 20, center.Y, center.X + 20, center.Y);
                    g.DrawLine(innerPen, center.X, center.Y - 20, center.X, center.Y + 20);
                }
            }
        }

        /// <summary>
        /// 绘制操作栏装饰
        /// </summary>
        private void DrawActionBarDecorations(Graphics g, Rectangle bounds)
        {
            using (var pen = new Pen(Color.FromArgb(80, 0, 200, 255), 1))
            {
                // 左侧装饰
                for (int i = 0; i < 3; i++)
                {
                    int y = 20 + i * 25;
                    g.DrawLine(pen, 10, y, 50, y);
                }

                // 右侧装饰
                for (int i = 0; i < 3; i++)
                {
                    int y = 20 + i * 25;
                    g.DrawLine(pen, bounds.Width - 50, y, bounds.Width - 10, y);
                }
            }
        }

        /// <summary>
        /// 创建增强型操作按钮
        /// </summary>
        private Button CreateEnhancedActionButton(string text, Color accentColor)
        {
            var button = new Button
            {
                Text = text,
                Width = 160,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(10, 25, 40),
                ForeColor = accentColor,
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                Margin = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.BorderColor = accentColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, accentColor);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, accentColor);

            // 按钮发光效果
            button.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = button.ClientRectangle;

                // 内部光晕
                using (var glowPen = new Pen(Color.FromArgb(50, accentColor), 2))
                {
                    g.DrawRectangle(glowPen, Rectangle.Inflate(rect, -4, -4));
                }

                // 左侧装饰条
                using (var accentBrush = new SolidBrush(Color.FromArgb(100, accentColor)))
                {
                    g.FillRectangle(accentBrush, 0, 0, 4, rect.Height);
                }
            };

            return button;
        }

        // ========== 增强PID卡片 ==========
        private Control CreatePidCard(string key, string title, string subtitle, string description, decimal kp, decimal ki, decimal kd)
        {
            var card = new Panel
            {
                Width = 360,
                Height = 280,
                BackColor = pidCardBackground,
                Margin = new Padding(0, 0, 30, 30),
                Padding = new Padding(20)
            };

            // ★ 卡片增强绘制
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

                // 卡片背景渐变
                using (var fillBrush = new LinearGradientBrush(rect,
                           Color.FromArgb(18, 45, 65),
                           Color.FromArgb(8, 15, 28),
                           LinearGradientMode.ForwardDiagonal))
                {
                    g.FillRectangle(fillBrush, rect);
                }

                // 内部网格装饰
                using (var gridPen = new Pen(Color.FromArgb(15, 0, 180, 200), 1))
                {
                    for (int x = 0; x < card.Width; x += 20)
                    {
                        g.DrawLine(gridPen, x, 0, x, card.Height);
                    }
                    for (int y = 0; y < card.Height; y += 20)
                    {
                        g.DrawLine(gridPen, 0, y, card.Width, y);
                    }
                }

                // 发光边框
                using (var glowPen = new Pen(Color.FromArgb(80, 0, 220, 255), 3))
                {
                    g.DrawRectangle(glowPen, Rectangle.Inflate(rect, -2, -2));
                }

                // 主边框
                using (var borderPen = new Pen(Color.FromArgb(200, pidSecondaryAccent), 2))
                {
                    g.DrawRectangle(borderPen, rect);
                }

                // 顶部装饰条
                using (var topBrush = new LinearGradientBrush(
                           new Rectangle(0, 0, card.Width, 40),
                           Color.FromArgb(40, 0, 200, 255),
                           Color.FromArgb(10, 0, 100, 150),
                           LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(topBrush, 0, 0, card.Width, 3);
                }

                // 左侧装饰标识
                using (var accentBrush = new SolidBrush(Color.FromArgb(0, 220, 255)))
                {
                    g.FillRectangle(accentBrush, 0, 40, 4, 60);
                }

                // 右下角芯片图标
                DrawCardChipIcon(g, new Rectangle(card.Width - 40, card.Height - 40, 30, 30));
            };

            card.Resize += (s, e) => card.Invalidate();

            var header = new Label
            {
                Text = $"《 {title} 》",
                ForeColor = Color.FromArgb(0, 240, 255),
                Font = new Font("微软雅黑", 13F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent
            };

            var subHeader = new Label
            {
                Text = $"▸ {subtitle}",
                ForeColor = Color.FromArgb(0, 200, 230),
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            var desc = new Label
            {
                Text = description,
                ForeColor = Color.FromArgb(130, 200, 220),
                Dock = DockStyle.Top,
                Height = 52,
                Font = new Font("微软雅黑", 9F),
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(5, 10, 5, 10)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            // ========== Kp 编辑器 ==========
            var kpEditor = new NumericUpDown
            {
                Minimum = -1000m,        // ✅ 1. 先设置最小值
                Maximum = 1000m,         // ✅ 2. 再设置最大值
                DecimalPlaces = 3,
                Increment = 0.1m,
                Value = kp,              // ✅ 3. 最后设置值
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(15, 30, 50),
                ForeColor = Color.FromArgb(0, 255, 200),
                BorderStyle = BorderStyle.FixedSingle
            };

            // ========== Ki 编辑器 ==========
            var kiEditor = new NumericUpDown
            {
                Minimum = -1000m,
                Maximum = 1000m,
                DecimalPlaces = 3,
                Increment = 0.1m,
                Value = ki,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(15, 30, 50),
                ForeColor = Color.FromArgb(0, 255, 200),
                BorderStyle = BorderStyle.FixedSingle
            };

            // ========== Kd 编辑器 ==========
            var kdEditor = new NumericUpDown
            {
                Minimum = -1000m,
                Maximum = 1000m,
                DecimalPlaces = 3,
                Increment = 0.1m,
                Value = kd,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(15, 30, 50),
                ForeColor = Color.FromArgb(0, 255, 200),
                BorderStyle = BorderStyle.FixedSingle
            };

            var kpLabel = CreatePidValueLabel("Kp ▸");
            var kiLabel = CreatePidValueLabel("Ki ▸");
            var kdLabel = CreatePidValueLabel("Kd ▸");

            // NumericUpDown 增强样式
            foreach (var nud in new[] { kpEditor, kiEditor, kdEditor })
            {
                nud.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    var editor = s as NumericUpDown;
                    if (editor == null) return;

                    // 绘制发光边框
                    using (var pen = new Pen(Color.FromArgb(100, 0, 200, 255), 1))
                    {
                        g.DrawRectangle(pen, 0, 0, editor.Width - 1, editor.Height - 1);
                    }
                };

                // 鼠标悬停效果
                nud.MouseEnter += (s, e) =>
                {
                    var editor = s as NumericUpDown;
                    if (editor != null)
                    {
                        editor.BackColor = Color.FromArgb(20, 40, 65);
                    }
                };

                nud.MouseLeave += (s, e) =>
                {
                    var editor = s as NumericUpDown;
                    if (editor != null)
                    {
                        editor.BackColor = Color.FromArgb(15, 30, 50);
                    }
                };
            }

            grid.Controls.Add(kpLabel, 0, 0);
            grid.Controls.Add(kpEditor, 1, 0);
            grid.Controls.Add(kiLabel, 0, 1);
            grid.Controls.Add(kiEditor, 1, 1);
            grid.Controls.Add(kdLabel, 0, 2);
            grid.Controls.Add(kdEditor, 1, 2);

            pidEditors[key] = (kpEditor, kiEditor, kdEditor);
            pidDefaultValues[key] = (kp, ki, kd);

            card.Controls.Add(grid);
            card.Controls.Add(desc);
            card.Controls.Add(subHeader);
            card.Controls.Add(header);

            return card;
        }

        /// <summary>
        /// 绘制卡片芯片图标装饰
        /// </summary>
        private void DrawCardChipIcon(Graphics g, Rectangle iconRect)
        {
            using (var pen = new Pen(Color.FromArgb(80, 0, 220, 255), 1.5f))
            using (var fillBrush = new SolidBrush(Color.FromArgb(20, 0, 180, 220)))
            {
                // 绘制小型芯片符号
                Point center = new Point(iconRect.Left + iconRect.Width / 2, iconRect.Top + iconRect.Height / 2);
                int size = 12;

                Rectangle chipRect = new Rectangle(center.X - size / 2, center.Y - size / 2, size, size);
                g.FillRectangle(fillBrush, chipRect);
                g.DrawRectangle(pen, chipRect);

                // 芯片引脚
                using (var pinPen = new Pen(Color.FromArgb(0, 200, 255), 1))
                {
                    // 上下左右引脚
                    g.DrawLine(pinPen, center.X, chipRect.Top - 5, center.X, chipRect.Top);
                    g.DrawLine(pinPen, center.X, chipRect.Bottom, center.X, chipRect.Bottom + 5);
                    g.DrawLine(pinPen, chipRect.Left - 5, center.Y, chipRect.Left, center.Y);
                    g.DrawLine(pinPen, chipRect.Right, center.Y, chipRect.Right + 5, center.Y);
                }
            }
        }


        

        private NumericUpDown CreatePidNumeric(decimal value)
        {
            var input = new NumericUpDown
            {
                DecimalPlaces = 3,
                Increment = 0.01M,
                Minimum = -20M,
                Maximum = 20M,
                Value = value,
                Size = new Size(140, 28),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(6, 18, 26),
                ForeColor = pidPrimaryAccent,
                TextAlign = HorizontalAlignment.Center
            };
            input.ValueChanged += MarkPidDirty;
            return input;
        }

        private Label CreatePidValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = pidMutedText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
        }

        private Button CreateActionButton(string text, Color accentColor)
        {
            var button = new Button
            {
                Text = text,
                Width = 140,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.FromArgb(5, 15, 20),
                Font = new Font("微软雅黑", 10F, FontStyle.Bold),
                Margin = new Padding(15, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 150);
            return button;
        }

        private void MarkPidDirty(object sender, EventArgs e)
        {
            if (pidStatusLabel != null)
            {
                pidStatusLabel.Text = "参数已修改，等待下发...";
            }
        }

        private void ResetPidEditors()
        {
            foreach (var kvp in pidEditors)
            {
                if (!pidDefaultValues.TryGetValue(kvp.Key, out var defaults))
                {
                    continue;
                }
                kvp.Value.kp.Value = defaults.kp;  // ✅ 使用 .kp（小写）
                kvp.Value.ki.Value = defaults.ki;
                kvp.Value.kd.Value = defaults.kd;
            }
            if (pidStatusLabel != null)
            {
                pidStatusLabel.Text = "⚡ 已恢复到固件默认值";
            }
        }


        private void ApplyPidSettings()
        {
            var timeStamp = DateTime.Now.ToString("HH:mm:ss");
            var builder = new StringBuilder();
            builder.AppendLine($"[{timeStamp}] PID 更新:");

            foreach (var kvp in pidEditors)
            {
                builder.AppendLine($"  {kvp.Key}: Kp={kvp.Value.kp.Value:F3}, Ki={kvp.Value.ki.Value:F3}, Kd={kvp.Value.kd.Value:F3}");
            }

            if (pidStatusLabel != null)
            {
                pidStatusLabel.Text = "参数已整理，准备通过RK3576下发...";
            }

            if (richTextBox1 != null)
            {
                richTextBox1.AppendText(builder.ToString() + Environment.NewLine);
            }

            bool sendSucceeded = true;
            foreach (var key in pidChannelSequence)
            {
                if (!pidEditors.TryGetValue(key, out var editor))
                {
                    continue;
                }
                if (!TryGetPidChannelId(key, out var channelId))
                {
                    continue;
                }

                var pidFrame = BuildPidFrame(channelId, editor.kp.Value, editor.ki.Value, editor.kd.Value);
                if (!SendFrame(pidFrame, "PID"))
                {
                    sendSucceeded = false;
                    break;
                }

                Thread.Sleep(2);
            }

            if (sendSucceeded)
            {
                if (pidStatusLabel != null)
                {
                    pidStatusLabel.Text = "PID参数已通过RK3576网口发送";
                }
                richTextBox1?.AppendText($"[{timeStamp}] PID参数已发送到RK3576板卡\r\n");
            }
            else
            {
                if (pidStatusLabel != null)
                {
                    pidStatusLabel.Text = "网络未连接，PID参数尚未下发";
                }
                richTextBox1?.AppendText($"[{timeStamp}] PID发送失败：网络未连接\r\n");
            }
        }

        private byte[] BuildPidFrame(PidChannelId channel, decimal kp, decimal ki, decimal kd)
        {
            var frame = new byte[ProtocolTotalLength];
            Buffer.BlockCopy(FrameHeader, 0, frame, 0, FrameHeader.Length);

            frame[4] = PidFrameCode;
            frame[5] = PidFrameSignature;
            frame[6] = (byte)channel;

            WriteFloat(frame, 7, (float)kp);
            WriteFloat(frame, 11, (float)ki);
            WriteFloat(frame, 15, (float)kd);

            frame[20] = 0x5A;

            uint checksum = CalculateChecksum(frame, 4, 20);
            var checksumBytes = BitConverter.GetBytes(checksum);
            if (!BitConverter.IsLittleEndian) Array.Reverse(checksumBytes);
            Buffer.BlockCopy(checksumBytes, 0, frame, 21, 4);
            Buffer.BlockCopy(FrameFooter, 0, frame, 25, 4);
            return frame;
        }

        private bool TryGetPidChannelId(string key, out PidChannelId channelId)
        {
            switch (key)
            {
                case "YawOuter":
                    channelId = PidChannelId.YawOuter;
                    return true;
                case "YawRate":
                    channelId = PidChannelId.YawRate;
                    return true;
                case "RollOuter":
                    channelId = PidChannelId.RollOuter;
                    return true;
                case "RollRate":
                    channelId = PidChannelId.RollRate;
                    return true;
                case "Depth":
                    channelId = PidChannelId.Depth;
                    return true;
                default:
                    channelId = 0;
                    return false;
            }
        }

        

        private void label2_Click_1(object sender, EventArgs e)
        {

        }
    }

    public class DataPoint
    {
        public double X { get; }
        public double Y { get; }

            public DataPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        public class NaviInform
        {
            public int recvcnt;
            public float time;
            public float[] fb = new float[3];
            public float[] wb = new float[3];
            public float[] Magn = new float[3];
            public float[] INS_att = new float[3];
            public int[] thrusterValues = new int[6]; 
            public float GPS_vel;
            public double[] GPS_pos = new double[3];
            public double[] last_pos = new double[3];
            public float GPS_status;
            public float waterPressure;
            public float p30Distance;
            public float p30Confidence;
            public float deep;
            public float tempearature;
            public float current;
            public float voltage;
        }

        /// <summary>
        /// 游戏手柄解析后的状态（完全匹配新建 DOCX 文档 (2).docx 数据格式）
        /// </summary>
        public class GamepadState
        {
            #region 左摇杆（LS）- 文档0-3字节，16位无符号整数，中心值32768
            /// <summary>
            /// 左摇杆X轴：0~65535，32768=中心，<32768向左，>32768向右（文档0-1字节）
            /// </summary>
            public ushort LeftStickX { get; set; }

            /// <summary>
            /// 左摇杆Y轴：0~65535，32768=中心，<32768向上，>32768向下（文档2-3字节）
            /// </summary>
            public ushort LeftStickY { get; set; }
            #endregion

            #region 右摇杆（RS）- 文档4-7字节，16位无符号整数，中心值32768
            /// <summary>
            /// 右摇杆X轴：0~65535，32768=中心，<32768向左，>32768向右（文档4-5字节）
            /// </summary>
            public ushort RightStickX { get; set; }

            /// <summary>
            /// 右摇杆Y轴：0~65535，32768=中心，<32768向上，>32768向下（文档6-7字节）
            /// </summary>
            public ushort RightStickY { get; set; }
            #endregion

            #region 扳机键（LT/RT）- 文档8-11字节，16位无符号整数，0=松开，65535=按下
            /// <summary>
            /// 左扳机（LT）- 文档8-9字节
            /// </summary>
            public ushort LeftTrigger { get; set; }

            /// <summary>  
            /// 右扳机（RT）- 文档10-11字节
            /// </summary>
            public ushort RightTrigger { get; set; }
            #endregion  

            #region 方向键（D-pad）- 文档12字节，1字节整数（0=中心，1-8对应8方向）
            /// <summary>
            /// 方向键状态值（文档12字节原始值）
            /// </summary>
            public byte Dpad { get; set; }

            /// <summary>
            /// 方向键状态描述（C# 7.3 兼容：用switch语句替换开关表达式）
            /// </summary>
            public string DpadDescription
            {
                get
                {
                    // 根据新的XBOX HID协议
                    switch (Dpad)
                    {
                        case 0x00: return "中心";
                        case 0x04: return "上";
                        case 0x0C: return "右";
                        case 0x14: return "下";
                        case 0x1C: return "左";
                        default: return $"组合/未知(0x{Dpad:X2})";
                    }
                }
            }
            #endregion

            #region 主按键位 - 文档13字节，8位二进制位（1=按下）
            public bool IsAKeyPressed { get; set; }  // bit0（文档定义：A键）
            public bool IsBKeyPressed { get; set; }  // bit1（文档定义：B键）
            public bool IsXKeyPressed { get; set; }  // bit3（文档定义：X键）
            public bool IsYKeyPressed { get; set; }  // bit4（文档定义：Y键）
            public bool IsLBKeyPressed { get; set; } // bit6（文档定义：LB键）
            public bool IsRBKeyPressed { get; set; } // bit7（文档定义：RB键）
            #endregion
            public bool IsMemuPressed { get; set; } // bit2（文档定义：Menu键）
            #region 功能键位 - 文档14字节，8位二进制位（1=按下）
            public bool IsSelectPressed { get; set; } // bit2（文档定义：Select键）
            public bool IsStartPressed { get; set; }  // bit3（文档定义：Start键）
            public bool IsXboxKeyPressed { get; set; } // bit4（文档定义：Xbox键）
            public bool IsLSKeyPressed { get; set; }  // bit5（文档定义：LS键）
            public bool IsRSKeyPressed { get; set; }  // bit6（文档定义：RS键）
            #endregion

            #region Share键 - 文档15字节，bit0=1表示按下
            public bool IsShareKeyPressed { get; set; }
            #endregion
        }

}
