## RK3588 板端构建与弹窗显示步骤

以下所有命令均在 **RK3588 板端** 执行，默认你已把整个 `rknn_yolov8_demo` 目录从 PC 拷到板子，并且板子系统内置的 OpenCV (4.5.4d) 包含 `opencv_highgui`。

### 1. 安装依赖（只需一次）

```bash
sudo apt update
sudo apt install build-essential cmake pkg-config libopencv-dev
```

> `libopencv-dev` 会同时提供 `opencv_core/imgproc/imgcodecs/highgui` 等库，后续构建会直接使用系统版本。

### 2. 配置并编译

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

生成的可执行文件和依赖会被安装到 `install/rknn_yolov8_demo_Linux/`。

### 3. 运行摄像头实时检测

```bash
cd ~/rknn_yolov8_demo/install/rknn_yolov8_demo_Linux
export LD_LIBRARY_PATH=$PWD/lib:$LD_LIBRARY_PATH
./rknn_yolov8_demo ./model/RK3588/yolov8n.rknn camera
```

- 默认读取 `/dev/video11`，如果你的摄像头节点不同，请修改 `src/main.cc` 顶部的 `#define CAMERA_DEVICE` 并重新编译。
- 弹出的 “YOLO Camera” 窗口会显示实时检测结果，按 `q`/`Esc`/`Ctrl+C` 结束。

### 4. 常见问题

- **找不到摄像头**：确保设备节点存在并拥有读取权限，可用 `ls /dev/video*` 检查。
- **没有弹窗**：确认 `cmake` 配置时传入了 `-DENABLE_HIGHGUI=ON` 且系统 OpenCV 带 `opencv_highgui`；如果通过 SSH 运行，需要开启 X11 转发或在本地显示。
- **库找不到**：运行前务必执行 `export LD_LIBRARY_PATH=$PWD/lib:$LD_LIBRARY_PATH`，或者将该路径写入 `.bashrc`。
