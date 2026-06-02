using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;  // ← 保留这个
using HelixToolkit.Wpf;

// ★ 添加别名来消除歧义
using WpfPolygon = System.Windows.Shapes.Polygon;  // ← 添加这行
using IoPath = System.IO.Path;                     // ← 添加这行


namespace Controller
{
    /// <summary>
    /// ROV 3D 姿态显示控件 - 完整版
    /// 功能：加载 STL 模型、实时显示姿态角、指南针、状态面板
    /// </summary>
    public class RovModelViewer : UserControl
    {
        #region 字段定义

        // 3D 渲染组件
        private readonly HelixViewport3D viewport;
        private readonly Model3DGroup modelGroup;
        private Model3D currentModel;

        // UI 布局组件
        private readonly Grid mainGrid;
        private readonly Border statusPanel;
        private readonly TextBlock statusText;
        private readonly TextBlock attitudeText;
        private readonly Border compassBorder;
        private readonly Canvas compassCanvas;
        private System.Windows.Shapes.Polygon compassNeedle;  // 指南针指针


        // 姿态角数据
        private double _pitchDegrees;   // 俯仰角
        private double _rollDegrees;    // 横滚角
        private double _yawDegrees;     // 偏航角

        // 配置常量
        private const double RovModelYOffset = -250;    // 模型垂直偏移
        private const double DefaultYawOffset = -90;    // 默认偏航偏移（校准用）

        // 配色方案
        private ColorScheme currentScheme = ColorScheme.DeepSeaBlue;

        #endregion

        #region 配色方案枚举

        public enum ColorScheme
        {
            DeepSeaBlue,      // 深海蓝（默认）
            MilitaryGreen,    // 军事绿
            OceanGradient,    // 海洋渐变
            MetallicSilver,   // 金属银
            HighTechCyan      // 高科技青色
        }

        #endregion

        #region 构造函数

        public RovModelViewer()
        {
            // 创建主网格布局
            mainGrid = new Grid();

            // 初始化 3D 视口
            viewport = CreateEnhancedViewport();
            AddEnhancedLighting();

            // 创建模型组容器
            modelGroup = new Model3DGroup();
            viewport.Children.Add(new ModelVisual3D { Content = modelGroup });

            // 添加环境元素（网格、粒子）
            AddEnvironmentElements();

            // 创建 UI 组件
            statusPanel = CreateStatusPanel();
            attitudeText = CreateAttitudeDisplay();
            statusText = CreateStatusText();
            compassBorder = CreateCompass();
            compassCanvas = (Canvas)compassBorder.Child;

            // 将姿态文本和状态文本添加到状态面板
            var stackPanel = (StackPanel)statusPanel.Child;
            stackPanel.Children.Add(attitudeText);
            stackPanel.Children.Add(statusText);

            // 组装界面
            mainGrid.Children.Add(viewport);
            mainGrid.Children.Add(statusPanel);
            mainGrid.Children.Add(compassBorder);
            mainGrid.Children.Add(CreateDecorativeBorder());

            Content = mainGrid;
        }

        #endregion

        #region 3D 视口和光照初始化

        private HelixViewport3D CreateEnhancedViewport()
        {
            var vp = new HelixViewport3D
            {
                Background = new RadialGradientBrush(
                    Color.FromRgb(15, 25, 40),
                    Color.FromRgb(5, 10, 20)
                )
                {
                    GradientOrigin = new Point(0.5, 0.3),
                    Center = new Point(0.5, 0.5),
                    RadiusX = 1,
                    RadiusY = 1
                },
                ShowCoordinateSystem = true,
                ShowFrameRate = false,
                ShowViewCube = true,

                // ★ 标准右手坐标系视角（右前上方观察）
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(800, 600, 800),          // 右前上方
                    LookDirection = new Vector3D(-800, -600, -800), // 朝向原点
                    UpDirection = new Vector3D(0, 1, 0),            // Y轴朝上
                    FieldOfView = 50
                }
            };

            vp.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 150, 220),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.3
            };

            return vp;
        }




        /// <summary>
        /// 增强的光照系统
        /// </summary>
        private void AddEnhancedLighting()
        {
            var lightGroup = new Model3DGroup();

            // 主光源（模拟阳光从水面照下）
            lightGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(180, 210, 240),
                new Vector3D(0.3, -0.7, -0.6)
            ));

            // 辅助光源（模拟反射光）
            lightGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(80, 120, 160),
                new Vector3D(-0.8, -0.3, 0.2)
            ));

            // 背光（增强轮廓）
            lightGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(60, 90, 120),
                new Vector3D(0, 0.5, 0.8)
            ));

            // 点光源（模拟 ROV 探照灯）
            var spotLight = new PointLight(
                Color.FromRgb(255, 240, 200),
                new Point3D(0, -300, 200)
            )
            {
                Range = 1000,
                ConstantAttenuation = 1,
                LinearAttenuation = 0.001,
                QuadraticAttenuation = 0.0001
            };
            lightGroup.Children.Add(spotLight);

            // 环境光（基础照明）
            lightGroup.Children.Add(new AmbientLight(Color.FromRgb(50, 60, 70)));

            viewport.Children.Add(new ModelVisual3D { Content = lightGroup });
        }

        #endregion

        #region 环境元素（网格、粒子）

        /// <summary>
        /// 添加环境元素(网格、坐标轴、粒子)
        /// </summary>
        private void AddEnvironmentElements()
        {
            // ★ 参考网格保持水平(XZ 平面)
            var gridLines = new GridLinesVisual3D
            {
                Width = 4000,          // X 方向宽度
                Length = 4000,         // Z 方向长度
                MinorDistance = 100,
                MajorDistance = 500,
                Thickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 150, 200)),

                // 设置网格在水平面(XZ平面)
                Center = new Point3D(0, -40, 0),         // ★ 网格中心下移 40 单位
                LengthDirection = new Vector3D(1, 0, 0), // 长度方向沿 X 轴
                Normal = new Vector3D(0, 1, 0)           // 法线朝上(Y轴正方向)
            };
            viewport.Children.Add(gridLines);

            // ★ 坐标轴整体下移 40 单位（修改起点和终点的 Y 坐标）
            const double axisOffset = -100;  // 统一的 Y 轴偏移量

            // X轴 - 红色朝右 (+X)
            var xAxis = new ArrowVisual3D
            {
                Point1 = new Point3D(0, axisOffset, 0),          // ★ 起点下移
                Point2 = new Point3D(200, axisOffset, 0),        // ★ 终点下移
                Diameter = 5,
                Fill = Brushes.Red
            };
            viewport.Children.Add(xAxis);

            // Y轴 - 绿色朝上 (+Y)
            var yAxis = new ArrowVisual3D
            {
                Point1 = new Point3D(0, axisOffset, 0),          // ★ 起点下移
                Point2 = new Point3D(0, 200 + axisOffset, 0),    // ★ 终点下移（保持 200 长度）
                Diameter = 5,
                Fill = Brushes.Green
            };
            viewport.Children.Add(yAxis);

            // Z轴 - 蓝色朝前 (+Z)
            var zAxis = new ArrowVisual3D
            {
                Point1 = new Point3D(0, axisOffset, 0),          // ★ 起点下移
                Point2 = new Point3D(0, axisOffset, 200),        // ★ 终点下移
                Diameter = 5,
                Fill = Brushes.Blue
            };
            viewport.Children.Add(zAxis);

            // 添加水下粒子效果
            AddUnderwaterParticles();
        }




        /// <summary>
        /// 添加水下粒子效果（装饰性）
        /// </summary>
        private void AddUnderwaterParticles()
        {
            var random = new Random();
            for (int i = 0; i < 30; i++)
            {
                var particle = new SphereVisual3D
                {
                    Center = new Point3D(
                        random.Next(-1000, 1000),
                        random.Next(-1000, 1000),
                        random.Next(-500, 500)
                    ),
                    Radius = random.Next(2, 8),
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)random.Next(20, 60),
                        200, 220, 255
                    ))
                };
                viewport.Children.Add(particle);
            }
        }

        #endregion

        #region UI 创建方法

        /// <summary>
        /// 创建状态面板（左上角）
        /// </summary>
        private Border CreateStatusPanel()
        {
            var panel = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromArgb(180, 10, 20, 35),
                    Color.FromArgb(150, 5, 10, 20),
                    new Point(0, 0),
                    new Point(1, 1)
                ),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 180, 220)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 180, 220),
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.6
                }
            };

            var stackPanel = new StackPanel();

            // 标题
            var title = new TextBlock
            {
                Text = "🤖 ROV 姿态监控",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 220, 255)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };

            stackPanel.Children.Add(title);
            panel.Child = stackPanel;
            return panel;
        }

        /// <summary>
        /// 创建姿态角显示文本
        /// </summary>
        private TextBlock CreateAttitudeDisplay()
        {
            return new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(200, 240, 255)),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Text = "横滚: 0.0°\n俯仰: 0.0°\n偏航: 0.0°",
                LineHeight = 18
            };
        }

        /// <summary>
        /// 创建状态文本（系统状态）
        /// </summary>
        private TextBlock CreateStatusText()
        {
            return new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)),
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0),
                Text = "● 系统正常"
            };
        }

        /// <summary>
        /// 创建指南针（右上角）
        /// </summary>
        private Border CreateCompass()
        {
            var compassBg = new Border
            {
                Width = 120,
                Height = 120,
                Background = new RadialGradientBrush(
                    Color.FromArgb(200, 10, 20, 35),
                    Color.FromArgb(180, 5, 10, 20)
                ),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 180, 220)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(60),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 180, 220),
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.6
                }
            };

            var canvas = new Canvas
            {
                Width = 120,
                Height = 120
            };

            // 绘制方向标记和指针
            DrawCompassMarks(canvas);

            compassBg.Child = canvas;
            return compassBg;
        }

        /// <summary>
        /// 绘制指南针刻度和指针
        /// </summary>
        private void DrawCompassMarks(Canvas canvas)
        {
            var center = new Point(60, 60);
            var radius = 50;

            // 方向文字（北、东、南、西）
            string[] directions = { "N", "E", "S", "W" };
            double[] angles = { -90, 0, 90, 180 };

            for (int i = 0; i < 4; i++)
            {
                var angle = angles[i] * Math.PI / 180;
                var x = center.X + radius * Math.Cos(angle);
                var y = center.Y + radius * Math.Sin(angle);

                var text = new TextBlock
                {
                    Text = directions[i],
                    Foreground = Brushes.Cyan,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14
                };

                Canvas.SetLeft(text, x - 7);
                Canvas.SetTop(text, y - 10);
                canvas.Children.Add(text);
            }

            // ★ 修复：绘制指针（红色箭头）
            compassNeedle = new System.Windows.Shapes.Polygon  // ← 添加完整命名空间
            {
                Points = new PointCollection
                {
                    new Point(5, 0),     // 箭头顶端
                    new Point(0, 35),    // 左侧（底部）
                    new Point(5, 40),    // 底部中心（旋转支点）
                    new Point(10, 35)    // 右侧（底部）
                },
                Fill = Brushes.Red,
                RenderTransformOrigin = new Point(0.5, 1.0)
            };

            canvas.Children.Add(compassNeedle);
            Canvas.SetLeft(compassNeedle, center.X - 5);
            Canvas.SetTop(compassNeedle, center.Y - 40);
            canvas.Tag = compassNeedle; // 保存引用以便后续旋转
        }


        /// <summary>
        /// 创建装饰边框（整体外框）
        /// </summary>
        private Border CreateDecorativeBorder()
        {
            return new Border
            {
                BorderBrush = new LinearGradientBrush(
                    Color.FromArgb(150, 0, 180, 220),
                    Color.FromArgb(80, 0, 100, 150),
                    new Point(0, 0),
                    new Point(1, 1)
                ),
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(10),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 200, 255),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.4
                }
            };
        }

        #endregion

        #region 核心公共方法

        public void LoadModel(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                UpdateStatusText("✗ 模型文件不存在", false);
                Console.WriteLine($"✗ 模型加载失败: {path}");
                return;
            }

            try
            {
                var reader = new StLReader();
                var model = reader.Read(path);

                if (model == null)
                {
                    UpdateStatusText("✗ 模型解析失败", false);
                    Console.WriteLine("✗ STL 文件解析失败");
                    return;
                }

                modelGroup.Children.Clear();
                currentModel = model;

                // 应用材质和初始变换
                ApplyModelMaterial(currentModel, currentScheme);
                UpdateTransform();
                modelGroup.Children.Add(model);

                // 自动缩放视口以显示完整模型
                //viewport.ZoomExtents();

                // 添加加载动画
                AnimateModelEntry();

                UpdateStatusText("● 模型加载成功", true);

                // ★ 修复：使用完整命名空间
                Console.WriteLine($"✓ 模型加载成功: {System.IO.Path.GetFileName(path)}");  // ← 修改这行
            }
            catch (Exception ex)
            {
                UpdateStatusText("✗ 加载异常", false);
                Console.WriteLine($"✗ 模型加载异常: {ex.Message}");
            }
        }


        /// <summary>
        /// 设置 ROV 的姿态角度（Form1.cs 调用的核心方法）
        /// </summary>
        /// <param name="pitch">俯仰角（度）</param>
        /// <param name="roll">横滚角（度）</param>
        /// <param name="yaw">偏航角（度）</param>
        public void SetOrientation(double pitch, double roll, double yaw)
        {
            if (currentModel == null)
            {
                return; // 模型未加载时不更新
            }

            // 保存姿态角
            _pitchDegrees = pitch;
            _rollDegrees = roll;
            _yawDegrees = yaw;

            // 更新 3D 模型姿态
            UpdateTransform();

            // 更新指南针
            UpdateCompass();

            // 更新状态面板文字
            UpdateAttitudeDisplay();
        }

        /// <summary>
        /// 设置配色方案
        /// </summary>
        public void SetColorScheme(ColorScheme scheme)
        {
            currentScheme = scheme;
            if (currentModel != null)
            {
                ApplyModelMaterial(currentModel, scheme);
            }
        }

        #endregion

        #region 私有更新方法

        private void UpdateTransform()
        {
            if (currentModel == null)
            {
                return;
            }

            var transformGroup = new Transform3DGroup();

            // ★ 标准右手坐标系的旋转
            // Roll - 绕 X 轴旋转（红色轴）
            transformGroup.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), _rollDegrees)
            ));

            // Pitch - 绕 Y 轴旋转（绿色轴，朝前）
            transformGroup.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), _yawDegrees)
            ));

            // Yaw（偏航）- 绕 Z 轴旋转（蓝色轴，朝上）
            transformGroup.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), _pitchDegrees)
            ));

            // 添加垂直偏移
            transformGroup.Children.Add(new TranslateTransform3D(0, RovModelYOffset, 0));

            currentModel.Transform = transformGroup;
        }


        /// <summary>
        /// 更新指南针指针的旋转角度
        /// </summary>
        private void UpdateCompass()
        {
            if (compassNeedle == null)
            {
                return;
            }

            // 创建旋转变换（顺时针旋转偏航角）
            var rotateTransform = new RotateTransform
            {
                Angle = _yawDegrees            // 使用当前偏航角
            };

            compassNeedle.RenderTransform = rotateTransform;
        }

        /// <summary>
        /// 更新状态面板中的姿态角文字显示
        /// </summary>
        private void UpdateAttitudeDisplay()
        {
            if (attitudeText == null)
            {
                return;
            }

            // 更新姿态角显示（保留一位小数）
            attitudeText.Text = string.Format(
                "横滚: {0:F1}°\n俯仰: {1:F1}°\n偏航: {2:F1}°",
                _rollDegrees,
                _pitchDegrees,
                _yawDegrees
            );
        }

        /// <summary>
        /// 更新状态文本（显示系统状态）
        /// </summary>
        /// <param name="message">状态消息</param>
        /// <param name="isSuccess">是否成功（影响颜色）</param>
        private void UpdateStatusText(string message, bool isSuccess)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.Text = message;

            // 根据成功/失败设置不同颜色
            statusText.Foreground = new SolidColorBrush(
                isSuccess
                    ? Color.FromRgb(100, 200, 100)  // 绿色 = 成功
                    : Color.FromRgb(255, 100, 100)  // 红色 = 失败
            );
        }

        #endregion

        #region 材质和动画方法

        /// <summary>
        /// 应用材质到模型（递归处理模型组）
        /// </summary>
        private void ApplyModelMaterial(Model3D model, ColorScheme scheme)
        {
            if (model is GeometryModel3D geometryModel)
            {
                geometryModel.Material = CreateMaterial(scheme);
                geometryModel.BackMaterial = CreateMaterial(scheme);
            }
            else if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                {
                    ApplyModelMaterial(child, scheme);
                }
            }
        }

        /// <summary>
        /// 根据配色方案创建材质
        /// </summary>
        private Material CreateMaterial(ColorScheme scheme)
        {
            MaterialGroup materialGroup = new MaterialGroup();

            switch (scheme)
            {
                case ColorScheme.DeepSeaBlue:
                    materialGroup.Children.Add(new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(20, 80, 140))
                    ));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(100, 150, 200)), 35
                    ));
                    materialGroup.Children.Add(new EmissiveMaterial(
                        new SolidColorBrush(Color.FromRgb(5, 15, 30))
                    ));
                    break;

                case ColorScheme.MilitaryGreen:
                    materialGroup.Children.Add(new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(60, 90, 60))
                    ));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(120, 140, 120)), 25
                    ));
                    materialGroup.Children.Add(new EmissiveMaterial(
                        new SolidColorBrush(Color.FromRgb(10, 15, 10))
                    ));
                    break;

                case ColorScheme.OceanGradient:
                    var gradientBrush = new LinearGradientBrush();
                    gradientBrush.GradientStops.Add(
                        new GradientStop(Color.FromRgb(30, 100, 150), 0)
                    );
                    gradientBrush.GradientStops.Add(
                        new GradientStop(Color.FromRgb(10, 60, 100), 0.5)
                    );
                    gradientBrush.GradientStops.Add(
                        new GradientStop(Color.FromRgb(5, 30, 60), 1)
                    );
                    materialGroup.Children.Add(new DiffuseMaterial(gradientBrush));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(150, 200, 255)), 40
                    ));
                    break;

                case ColorScheme.MetallicSilver:
                    materialGroup.Children.Add(new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(192, 192, 192))
                    ));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(255, 255, 255)), 50
                    ));
                    break;

                case ColorScheme.HighTechCyan:
                    materialGroup.Children.Add(new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(0, 200, 220))
                    ));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(150, 255, 255)), 45
                    ));
                    materialGroup.Children.Add(new EmissiveMaterial(
                        new SolidColorBrush(Color.FromRgb(0, 40, 50))
                    ));
                    break;

                default:
                    // 默认使用深海蓝
                    materialGroup.Children.Add(new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(20, 80, 140))
                    ));
                    materialGroup.Children.Add(new SpecularMaterial(
                        new SolidColorBrush(Color.FromRgb(100, 150, 200)), 30
                    ));
                    break;
            }

            return materialGroup;
        }

        /// <summary>
        /// 模型入场动画（缩放效果）
        /// </summary>
        private void AnimateModelEntry()
        {
            if (currentModel == null) return;

            var scaleAnimation = new DoubleAnimation(0.1, 1, TimeSpan.FromSeconds(0.8))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleTransform = new ScaleTransform3D();
            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(currentModel.Transform);
            currentModel.Transform = transformGroup;

            scaleTransform.BeginAnimation(ScaleTransform3D.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform3D.ScaleYProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform3D.ScaleZProperty, scaleAnimation);
        }

        #endregion
    }
}
