# YOLO 部署工具与版本说明

## 1. 文档目的

这个文件专门记录本工程在 RK 平台重新部署 YOLO 时需要关注的三件事：

1. 电脑端要安装哪些工具，版本分别是什么
2. 开发板端要安装哪些工具，版本分别是什么
3. 按什么步骤完成“模型转换 -> 板端编译 -> 板端推理显示”

当前工程里实际使用的推理工程目录：

- `014/rknn_yolov8_demo`

当前默认运行方式：

- 电脑端 Ubuntu 虚拟机中转换模型
- 开发板端运行 `014/rknn_yolov8_demo`
- 板端执行步骤以 `014/rknn_yolov8_demo/RUN_STEPS.md` 为主

---

## 2. 总体流程

当前工程的完整流程可以分成 3 步：

### 第 1 步：电脑端准备模型转换环境

在 Ubuntu 虚拟机的 Conda 环境中安装：

- Python
- `rknn-toolkit2`
- `ultralytics`
- `torch`
- `onnx`
- `onnxruntime`
- `opencv-python`

作用：

- 训练好的 YOLO 模型导出为 `onnx`
- 再由 `rknn-toolkit2` 转成 `.rknn`

### 第 2 步：电脑端把模型转成 RKNN

典型流程：

- `pt` -> `onnx`
- `onnx` -> `rknn`

注意：

- `rknn` 模型要按目标板的平台类型导出
- `RK3568` 和 `RK3588` 不能随便共用同一份 `.rknn`

### 第 3 步：开发板端编译并运行推理程序

开发板端负责：

- 编译 C++ demo
- 加载 `.rknn`
- 调用 `librknnrt.so`
- 调用 `librga.so`
- 接摄像头
- 画框并显示视频窗口

---

## 3. 电脑端工具与版本

### 3.1 当前已经确认的 Conda 环境

当前电脑端 Ubuntu 虚拟机已经确认的环境如下：

- Conda 环境名：`lubancat-rknn`
- Python：`3.10.19`
- `rknn-toolkit2`：`2.3.2`

用户提供的 `pip list` 中，当前和模型转换相关的核心包版本如下：

### 3.2 电脑端核心工具版本清单

#### 必装工具

- `python`：`3.10.19`
- `rknn-toolkit2`：`2.3.2`
- `onnx`：`1.16.1`
- `onnxruntime`：`1.23.2`
- `opencv-python`：`4.11.0.86`
- `numpy`：`1.26.4`
- `torch`：`2.1.0`
- `torchvision`：`0.16.0`
- `ultralytics`：`8.0.100`

#### 常用辅助工具

- `onnxsim`：`0.4.36`
- `netron`：`8.7.6`
- `scipy`：`1.15.3`
- `pandas`：`2.0.3`
- `matplotlib`：`3.10.7`

### 3.3 电脑端环境的作用说明

#### `rknn-toolkit2 2.3.2`

作用：

- 负责把 `onnx` 模型转换成 `.rknn`

这是电脑端最关键的工具版本。

#### `ultralytics 8.0.100`

作用：

- 用于导出 YOLO 模型
- 常见流程是从 `.pt` 导出为 `.onnx`

#### `torch 2.1.0` + `torchvision 0.16.0`

作用：

- 支撑 `ultralytics` 模型加载与导出

#### `onnx 1.16.1`

作用：

- 提供 ONNX 模型格式支持

#### `onnxruntime 1.23.2`

作用：

- 在电脑端测试 ONNX 是否正常
- 不是板端推理必须项，但电脑端调试常用

#### `opencv-python 4.11.0.86`

作用：

- 电脑端图像处理、可视化、预处理调试

### 3.4 当前电脑端环境结论

如果你只是想“重新把之前跑通的流程再搭一遍”，那电脑端目前建议继续沿用下面这套版本：

- Python `3.10.19`
- `rknn-toolkit2 2.3.2`
- `ultralytics 8.0.100`
- `torch 2.1.0`
- `torchvision 0.16.0`
- `onnx 1.16.1`
- `onnxruntime 1.23.2`
- `opencv-python 4.11.0.86`
- `numpy 1.26.4`

这套就是当前实际已经确认存在的环境。

---

## 4. 开发板端工具与版本

### 4.1 当前工程的板端运行方式

当前工程不是在板子上跑 Python 的 `ultralytics` 推理，而是：

- 板端编译 C++ 程序
- 板端加载 `.rknn`
- 板端调用 RKNN runtime 进行推理

也就是说，开发板端真正重要的是：

- 编译环境
- OpenCV
- RKNN runtime
- RGA
- 摄像头相关工具

### 4.2 当前工程中可确认的板端工具

根据 `RUN_STEPS.md` 和工程配置，板端需要：

#### 系统编译工具

- `build-essential`
- `cmake`
- `pkg-config`

#### 图像与显示

- `libopencv-dev`

`RUN_STEPS.md` 里记录的系统 OpenCV 版本为：

- `OpenCV 4.5.4d`

#### 摄像头检查工具

- `v4l-utils`

虽然 `RUN_STEPS.md` 没有显式写这一项，但实际部署时强烈建议安装。

### 4.3 板端 RKNN 相关运行库

仓库中自带了两套 RKNN runtime：

- `014/runtime/RK356X/Linux/librknn_api/`
- `014/runtime/RK3588/Linux/librknn_api/`

仓库中自带了两套 RGA：

- `014/3rdparty/rga/RK356X/`
- `014/3rdparty/rga/RK3588/`

说明：

- `RK3568` 应使用 `RK356X` 这一套
- `RK3588` 应使用 `RK3588` 这一套
- 当前工程没有单独 `RK3576` 分支，当前使用上应归到 `rk3588` 这一档

### 4.4 板端当前可确认的版本信息

当前能从工程中确认到的板端信息：

- `RUN_STEPS.md` 默认目标板：`RK3588`
- `CMakeCache.txt` 当前构建目标：`rk3588`
- `RUN_STEPS.md` 中记录的板端 OpenCV：`4.5.4d`

当前**无法仅从仓库静态文件中完全确认**的内容：

- 板卡系统里真实安装的 NPU driver 版本
- 板卡运行时实际打印的 `sdk version`
- 板卡运行时实际打印的 `driver version`

这些需要在板子上运行程序时确认。

### 4.5 板端建议安装项

板端建议安装：

```bash
sudo apt update
sudo apt install build-essential cmake pkg-config libopencv-dev v4l-utils
```

如果你就是照当前工程重新部署，板端优先保证这几项齐全。

---

## 5. 平台适配关系

### 5.1 当前工程支持的目标平台

根据 `014/rknn_yolov8_demo/CMakeLists.txt`，当前只支持：

- `rk356x`
- `rk3588`

### 5.2 对应关系

#### 如果目标板是 RK3568

应使用：

- `TARGET_SOC=rk356x`
- `014/runtime/RK356X`
- `014/3rdparty/rga/RK356X`
- 重新导出的 `RK356X` 对应 `.rknn`

#### 如果目标板是 RK3588

应使用：

- `TARGET_SOC=rk3588`
- `014/runtime/RK3588`
- `014/3rdparty/rga/RK3588`
- `RK3588` 对应 `.rknn`

#### 如果目标板是 RK3576

当前工程没有单独分支，建议按当前已经跑通过的路径处理：

- `TARGET_SOC=rk3588`
- `014/runtime/RK3588`
- `014/3rdparty/rga/RK3588`

---

## 6. 版本匹配原则

整个部署流程里，最重要的版本匹配关系是：

1. 电脑端 `rknn-toolkit2`
2. 板端 `librknnrt.so`
3. 板端 NPU driver

这三者尽量保持在同一代兼容范围。

当前建议优先使用的“已知基线”是：

### 电脑端

- Python `3.10.19`
- `rknn-toolkit2 2.3.2`

### 板端

- `rk3588` 路径
- OpenCV `4.5.4d`

如果之前这套流程已经跑通过，就优先复用，不建议第一步就升级版本。

---

## 7. 当前工程里的模型转换注意点

模型转换脚本位置：

- `014/rknn_yolov8_demo/convert_rknn_demo/yolov8/onnx2rknn.py`

当前脚本默认写的是：

- `platform = 'rk3566'`
- `target_platform='rk3566'`

这说明：

- 如果你后面重新给 `RK3588` 导模型
- 不能直接照抄脚本默认目标

应根据目标板改成对应平台。

实用规则：

- 给 `RK3568` 导模型：按 `rk356x` 这一类目标处理
- 给 `RK3588` 导模型：按 `rk3588` 目标处理
- 给 `RK3576` 导模型：在当前工程里先按现有 `rk3588` 路径处理

---

## 8. 推荐的重新部署步骤

下面这套步骤适合“之前已经跑通过，现在重新整理并再部署一次”。

### 第 1 步：确认电脑端环境

进入 Conda 环境：

```bash
conda activate lubancat-rknn
```

确认核心版本：

```bash
python --version
pip show rknn-toolkit2
pip show ultralytics
pip show torch
pip show onnx
pip show opencv-python
```

目标是确认下面这些版本不乱：

- Python `3.10.19`
- `rknn-toolkit2 2.3.2`
- `ultralytics 8.0.100`
- `torch 2.1.0`
- `onnx 1.16.1`
- `opencv-python 4.11.0.86`

### 第 2 步：电脑端导出模型

典型流程：

- 使用 `ultralytics` 从 `.pt` 导出 `.onnx`
- 使用 `rknn-toolkit2` 从 `.onnx` 导出 `.rknn`

### 第 3 步：确认板端基础依赖

在板子上执行：

```bash
sudo apt update
sudo apt install build-essential cmake pkg-config libopencv-dev v4l-utils
```

### 第 4 步：确认板端摄像头节点

```bash
ls /dev/video*
v4l2-ctl --list-devices
```

当前代码默认摄像头设备是：

- `/dev/video11`

如果板子上不是这个节点，后续运行时要特别注意。

### 第 5 步：板端编译工程

当前 `RUN_STEPS.md` 对应的是 `RK3588` 路径：

```bash
cd ~/rknn_yolov8_demo
rm -rf build/native
mkdir -p build/native
cd build/native

cmake ../.. \
  -DTARGET_SOC=rk3588 \
  -DLIB_ARCH=aarch64 \
  -DUSE_SYSTEM_OPENCV=ON \
  -DENABLE_HIGHGUI=ON

make -j$(nproc)
make install
```

### 第 6 步：板端运行并确认版本

图片测试：

```bash
cd ~/rknn_yolov8_demo/install/rknn_yolov8_demo_Linux
export LD_LIBRARY_PATH=$PWD/lib:$LD_LIBRARY_PATH
./rknn_yolov8_demo ./model/RK3588/yolov8n.rknn ./model/test.jpg
```

摄像头测试：

```bash
cd ~/rknn_yolov8_demo/install/rknn_yolov8_demo_Linux
export LD_LIBRARY_PATH=$PWD/lib:$LD_LIBRARY_PATH
./rknn_yolov8_demo ./model/RK3588/yolov8n.rknn camera
```

运行时重点记录输出：

- `sdk version`
- `driver version`

后续补记在这里：

- 板端 RKNN API 版本：`待补充`
- 板端 NPU Driver 版本：`待补充`

---

## 9. 当前推荐保留的版本基线

### 电脑端推荐保留

- Conda 环境：`lubancat-rknn`
- Python：`3.10.19`
- `rknn-toolkit2`：`2.3.2`
- `ultralytics`：`8.0.100`
- `torch`：`2.1.0`
- `torchvision`：`0.16.0`
- `onnx`：`1.16.1`
- `onnxruntime`：`1.23.2`
- `opencv-python`：`4.11.0.86`
- `numpy`：`1.26.4`

### 开发板端推荐保留

- `build-essential`
- `cmake`
- `pkg-config`
- `libopencv-dev`
- `v4l-utils`
- `RK3588` 对应 runtime / rga 路径

---

## 10. 重新部署前检查清单

### 电脑端检查

- 已进入 `lubancat-rknn`
- Python 版本正确
- `rknn-toolkit2` 版本正确
- `ultralytics` 版本正确
- `torch` 版本正确
- `onnx` 版本正确

### 板端检查

- 已安装 `build-essential`
- 已安装 `cmake`
- 已安装 `pkg-config`
- 已安装 `libopencv-dev`
- 已安装 `v4l-utils`
- 已确认摄像头设备节点
- 已确认运行路径使用 `rk3588`
- 已记录程序打印的 `sdk version` 和 `driver version`

### 模型检查

- 当前 `.rknn` 是否与目标板平台匹配
- 如果换 `RK3568`，是否重新导出了 `RK356X` 版本模型
