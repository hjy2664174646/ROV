# ROV Control System

本仓库为 ROV（水下遥控机器人）相关代码集合，包含下位机控制板、PC 上位机以及 RK3576/Rockchip 板端视觉识别相关代码。工程按照实际运行设备拆分为三部分：STM32F4 控制板负责传感器采集、推进器控制和运动闭环；PC 上位机负责操控、遥测显示、地图/视频/模型界面；RK3576 板端负责基于 Ultralytics/YOLO 与 RKNN 的视觉推理。

## 工程架构

```text
ROV Project
├── code/                # STM32F4 下位机控制板代码
├── Controller/          # PC 上位机控制软件
├── ultralytics-main/    # RK3576/Rockchip 板端视觉识别与模型部署代码
├── integration_plan.md  # 系统集成规划文档
├── 工作空间切换指南.md
└── 底层驱动实现功能.docx
```

整体数据流如下：

```text
PC 上位机 Controller
        │
        │ 控制指令 / PID 参数 / 电源与灯光控制 / 云台控制
        ▼
STM32F4 控制板 code
        │
        ├── 推进器、舵机、灯光、电源等执行机构控制
        ├── GPS、IMU、罗盘、水深、声呐、电池等传感器采集
        └── 姿态、深度、运动控制与遥测数据回传

RK3576/Rockchip 板端 ultralytics-main
        │
        └── 摄像头图像采集、YOLO/RKNN 目标检测、视觉辅助功能
```

## 目录说明

### `code/` STM32F4 下位机工程

该目录为 STM32F4 控制板固件工程，主要由 STM32CubeMX 生成代码、FreeRTOS 任务、HAL 驱动和用户自定义控制模块组成。

主要文件与目录：

```text
code/
├── UNCU.ioc                 # STM32CubeMX 配置文件
├── MDK-ARM/UNCU.uvprojx     # Keil MDK 工程文件
├── Core/                    # STM32 HAL 初始化代码与 FreeRTOS 入口
├── Drivers/                 # STM32 HAL/CMSIS 驱动
├── Middlewares/             # FreeRTOS、FatFs 等中间件
├── FATFS/                   # SD 卡/FATFS 文件系统相关代码
├── ARM_CM4F/                # Cortex-M4F/FreeRTOS 端口相关文件
└── User/                    # ROV 业务代码与设备驱动
```

`User/` 中的主要模块：

```text
User/Src/rov.c              # ROV 运动控制、推进器分配与输出
User/Src/protocol.c         # 上位机通信协议解析、遥测数据发送
User/Src/pid_control.c      # 姿态/深度等 PID 控制逻辑
User/Src/INS_task.c         # 惯导/姿态解算任务
User/Src/gps.c              # GPS 数据解析与配置
User/Src/ms5837.c           # MS5837 水深/压力传感器
User/Src/P30_sonar.c        # P30 声呐测距
User/Src/icm45686.c         # IMU 设备驱动
User/Src/qmc5883p.c         # 电子罗盘驱动
User/Src/peripheral_app.c   # 串口 DMA、外设应用层处理
User/Devices/BMI088/        # BMI088 传感器驱动
User/Algorithm/             # PID、卡尔曼滤波等算法模块
```

固件中使用 FreeRTOS 组织任务，包含 ROV 运动控制、遥测通信、GPS、SD 卡、电池 ADC、水深、声呐、遥控输入和 INS 等任务。控制板通过多路 UART/DMA 接收上位机、GPS、声呐等外设数据，并输出 PWM 控制推进器、舵机及其他执行机构。

### `Controller/` PC 上位机工程

该目录为 Windows PC 上位机程序，使用 C#/.NET WinForms 开发，工程入口为：

```text
Controller/Controller.sln
Controller/Controller/Controller.csproj
Controller/Controller/Program.cs
Controller/Controller/Form1.cs
```

主要功能模块：

```text
Controller/Controller/Form1.cs                  # 主窗口与主要控制逻辑
Controller/Controller/ROVControlPanel.cs        # ROV 控制面板
Controller/Controller/ROVTelemetryControl .cs   # 遥测数据显示控件
Controller/Controller/VideoStreamControl.cs     # 视频流显示控件
Controller/Controller/GPSTrajectoryControl .cs  # GPS 轨迹显示
Controller/Controller/MapViewControl.cs         # 地图视图
Controller/Controller/MapPopupForm.cs           # 地图弹窗
Controller/Controller/RovModelViewer.cs         # ROV 三维模型显示
Controller/Controller/P30DepthControl.cs        # 深度/声呐数据显示
Controller/Controller/RovPowerSwitchControl.cs  # 电源开关控制
Controller/Controller/BatteryIndicatorControl.cs # 电池状态显示
Controller/Controller/Resources/                # UI 图片、地图 HTML、背景资源
Controller/Controller/Models/rov.STL            # ROV 三维模型
```

依赖项通过 NuGet 包管理，当前工程包含 `HelixToolkit.Wpf` 等库，用于三维模型显示。可使用 Visual Studio 打开 `Controller.sln` 进行编译、调试和发布。

### `ultralytics-main/` RK3576/Rockchip 板端代码

该目录包含运行在 RK3576/Rockchip 板端的视觉识别相关代码，主要包括 Ultralytics YOLO 工程和 RKNN 部署 Demo。

主要目录：

```text
ultralytics-main/
├── ultralytics-main/        # Ultralytics YOLO Python 工程
├── 014/rknn_yolov8_demo/    # RKNN YOLOv8 C/C++ 推理 Demo
├── 014/runtime/             # rknn_server 运行环境相关文件
├── 014/3rdparty/            # 第三方库，如 stb、rk_mpi_mmz
├── TOOL.md
└── 说明.doc
```

RKNN YOLOv8 Demo 相关文件：

```text
014/rknn_yolov8_demo/src/main.cc        # 板端推理主程序
014/rknn_yolov8_demo/src/postprocess.cc # YOLO 后处理
014/rknn_yolov8_demo/CMakeLists.txt     # CMake 构建配置
014/rknn_yolov8_demo/build-linux_*.sh   # Rockchip Linux 平台构建脚本
014/rknn_yolov8_demo/model/             # RKNN 模型与测试图片
014/rknn_yolov8_demo/convert_rknn_demo/ # ONNX 转 RKNN 相关脚本
014/rknn_yolov8_demo/RUN_STEPS.md       # 板端构建与运行步骤
```

该部分用于摄像头实时图像采集、YOLOv8 目标检测、RKNN 模型加载与推理加速。实际部署时可根据板卡型号、摄像头节点和模型路径调整构建参数与运行参数。

## 机械设计资料

`solidworks/` 目录保存 ROV 机械结构 CAD 资料，主要包括：

- `2号潜器/`: 早期潜器机械结构和相关 SolidWorks 零件
- `2号潜器 - pro/`: 第二代潜器改进版本的装配体和零件
- `电力载波模块外壳/`: 电力载波模块外壳及相关模型
- `*.SLDASM`、`*.SLDPRT`: SolidWorks 装配体和零件源文件
- `*.STEP`、`*.STL`、`*.3mf`: 三维交换、打印和查看格式
- `*.zip`: 机械结构资料备份压缩包

SolidWorks 临时锁文件（文件名以 `~$` 开头）和 `.err` 临时错误文件不会提交。CAD 文件使用 Git LFS 管理，首次克隆或拉取机械资料可能需要较长时间。打开装配体时请保持引用零件的相对目录结构不变。
## 快速开始

### 1. STM32F4 控制板固件

1. 使用 STM32CubeMX 打开 `code/UNCU.ioc` 检查或修改外设配置。
2. 使用 Keil MDK 打开 `code/MDK-ARM/UNCU.uvprojx`。
3. 编译工程并将固件下载到 STM32F4 控制板。
4. 按实际硬件连接推进器、传感器、GPS、声呐、通信串口和供电模块。

### 2. PC 上位机

1. 使用 Visual Studio 打开 `Controller/Controller.sln`。
2. 还原 NuGet 依赖。
3. 编译并运行 `Controller` 项目。
4. 根据实际串口、网络、视频源等连接方式配置上位机。

### 3. RK3576/Rockchip 视觉端

1. 进入 `ultralytics-main/014/rknn_yolov8_demo/`。
2. 按 `RUN_STEPS.md` 中的步骤安装依赖、配置 CMake 并编译。
3. 将 RKNN 模型放入对应 `model/` 目录。
4. 在板端运行 Demo，进行摄像头实时检测或图片检测。

## 主要功能

- ROV 六推进器运动控制
- 姿态、航向、深度等 PID 闭环控制
- 上位机控制指令解析与遥测数据回传
- GPS 定位与轨迹显示
- IMU、电子罗盘、水深传感器、声呐、电池电压等数据采集
- 灯光、电源、摄像头云台等外设控制
- PC 上位机可视化控制界面
- 视频流显示、地图显示、三维 ROV 模型显示
- RK3576/Rockchip 板端 YOLO/RKNN 视觉推理

## 开发环境参考

| 模块 | 参考环境 |
| --- | --- |
| 下位机固件 | STM32CubeMX、Keil MDK、STM32 HAL、FreeRTOS |
| 上位机 | Windows、Visual Studio、C#、.NET Framework/WinForms |
| 视觉端 | RK3576/Rockchip Linux、CMake、OpenCV、RKNN Toolkit2、Ultralytics YOLO |

## 上传 GitHub 前建议

上传前建议检查以下内容：

- 删除或忽略编译产物，例如 `bin/`、`obj/`、`.vs/`、`__pycache__/`、中间构建目录等。
- 检查是否包含不希望公开的模型权重、数据集、串口配置、IP 地址、密钥或个人路径。
- 如果模型文件较大，建议使用 Git LFS 管理 `.rknn`、`.onnx`、`.pt` 等文件。
- 保留必要的工程文件，例如 `.sln`、`.csproj`、`.ioc`、`.uvprojx`、`CMakeLists.txt`。
- 为每个子工程补充更详细的硬件接线、通信协议和部署说明。

## 说明

本仓库目前以工程代码归档和系统集成为主，不同模块需要分别在对应硬件或开发环境中编译运行。具体串口号、摄像头节点、模型路径、通信参数和硬件接线请根据实际 ROV 平台进行配置。
