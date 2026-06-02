# RK3576 → PC 联合链路改造指引

本文档梳理了现有工程（`code/` STM32、`rknn_yolov8_demo/` RK3576、`Controller/` 上位机）之间的数据流，并给出把控制板 50 Hz 遥测与视频流一并透传到 PC 的具体改造步骤与验证方案。

## 1. 现状概览

- **STM32 控制板** (`code/User/Src` 等)：  
  - 周期 20 ms 运行 `msg_task()`（`protocol.c:28`）把 `msgStruct_t` 结构通过 `USART2` 发送，帧格式/字段与《底层驱动实现功能.docx》一致，包含帧头 `0xAA55AA56`、33 个 4 字节量、校验和和帧尾 `0xAA57AA58`。  
  - `remote_control_task()`（`protocol.c:77`）仍按串口接收上位机命令。
- **上位机 Controller** (`Controller/Form1.cs` 等)：  
  - 通过 `System.IO.Ports.SerialPort` 直接连 STM32，`serialPort.DataReceived` 中累积 192 B 并解析（参见 `Form1.cs` 中 `serial_data` / `recv` 相关逻辑）。  
  - UI 控件（`ROVTelemetryControl`、`ThrusterDisplayControl` 等）直接消费解析结果。
- **RK3576 推理程序** (`rknn_yolov8_demo/src/main.cc`):  
  - 摄像头路径已经支持在本地显示并用 TCP (`NETWORK_TARGET_IP:NETWORK_TARGET_PORT`) 推送 `[JSON长度][JSON][JPEG长度][JPEG]`，JSON 里目前只有 `fps` 与 `detections`。

改造目标：STM32 只需把原始帧发给 RK3576；RK3576 负责：
1. 通过 UART 接收并存储最新 `msgStruct_t`。
2. 在推视频帧时，把最新遥测序列化进 JSON，一并通过网口送到 PC。
3. 上位机不再读串口，而是跑 TCP 客户端，解析相同的 `[len+JSON][len+JPEG]` 协议并复用原有解析/展示逻辑。

## 2. RK3576 端改造

### 2.1 串口接入 STM32 数据

1. **硬件**：把 STM32 目前连 PC 的串口改接 RK3576 的 UART（确保 3.3 V 电平、波特率 460800，与文档一致）。  
2. **软件**（`rknn_yolov8_demo/src/main.cc` 附近）：  
   - 在文件顶部再引入 `termios`、`fcntl` 并新增串口 device 常量，例如 `#define CONTROL_SERIAL "/dev/ttyS4"`。  
   - 新建一个 `struct ControlFrame { std::array<uint8_t, 192> raw; bool valid; ... }` 保存最近一次完整帧以及解析后的字段（可直接复用 `msgStruct_t` 的布局，参考 `code/User/Inc/protocol.h` 定义）。  
   - 增加串口读线程：
     ```cpp
     void control_serial_reader(ControlFrame* shared) {
         int fd = open(CONTROL_SERIAL, O_RDWR | O_NOCTTY);
         // termios 配置 460800 8N1
         std::array<uint8_t, 512> buf{};
         std::vector<uint8_t> window;
         while (g_running) {
             ssize_t n = read(fd, buf.data(), buf.size());
             window.insert(window.end(), buf.begin(), buf.begin()+n);
             // 滑动查找 0xAA55AA56 ... 0xAA57AA58, 校验和参照协议
             if (frame_ok) {
                 std::lock_guard<std::mutex> lk(shared->mutex);
                 memcpy(shared->raw.data(), frame_start, FRAME_LEN);
                 decode_fields(shared); // 将 4 字节字段转 float/整型（使用小端）
                 shared->valid = true;
             }
         }
     }
     ```
   - 在 `main()` 初始化摄像头之前启动该线程，并在退出时 `join()`。

### 2.2 JSON 扩展

1. 在 `build_detection_json()`（`main.cc:600` 左右）内部追加 `control` 字段：  
   - 若 `ControlFrame.valid == true`，生成：
     ```json
     {
       "timestamp": 123456,
       "gyro": [gx, gy, gz],
       "acc": [ax, ay, az],
       "mag": [...],
       "att": {"pitch":..,"roll":..,"yaw":..},
       "ins_vel": [vx, vy, vz],
       "ins_pos": {"lon": ..., "lat": ..., "alt": ...},
       "gps_vel": [ve, vn, vu],
       "gps_pos": {...},
       "gps_status": {"sat": x, "pdop": y},
       "pressure": ...,
       "temperature": ...,
       "current": ...,
       "voltage": ...
     }
     ```
   - 所有单位/缩放请参照 docx（例如速度 m/s，纬经度拆成整数/小数两段，可在 RK3576 端组合成 `double`）。
2. 若暂未收到有效帧，JSON 可以带 `"control": null`。

### 2.3 推流队列写入

现有 `network_queue.push()` 只塞视频帧；需要把控制数据写入 JSON 即可，不影响二进制协议。若希望 PC 能够独立缓存原始 192 B，可在 JSON 中增加 `raw_hex`（上位机再按需解析）。频率方面：摄像头帧率通常 ≥20 FPS，大于 50 Hz；为确保控制数据 50 Hz 更新，可额外：
- 让串口线程每获取一帧都直接 `network_queue.push()` 一个「空白视频帧」（只带控制 JSON，JPEG 长度置 0）或更新共享结构。由于视频帧更密，将 JSON 中 `control.timestamp` 对齐 STM32 帧号即可。

### 2.4 校验与日志

在串口线程打印统计：
```cpp
printf("[CTRL] frames=%u lost=%u last_ts=%u\\n", ...);
```
同时在网络线程检测若 JSON 中 `control.valid` 过期（>40 ms 未刷新）则写 warning，便于排查。

## 3. 上位机 Controller 改造

### 3.1 TCP 客户端

1. 在 `Form1` 中加入 `TcpClient`、`NetworkStream` 字段以及一个 `CancellationTokenSource`。  
2. 在「连接」按钮中新增模式：  
   - 如果选择「以太网」则调用 `ConnectToStreamServer(string ip, int port)`，内部启动后台任务循环读取 `[len+JSON][len+JPEG]`。  
   - 把串口连接 UI（`ROVControlPanel.cs` 中 `SerialPortConnectClicked`）扩展为可输入 IP/端口并切换模式。

### 3.2 数据解包与复用现有解析

1. 在网络读线程中：
   ```csharp
   int jsonLen = ReadInt32BE(stream);
   byte[] jsonBytes = ReadExactly(stream, jsonLen);
   var payload = JsonSerializer.Deserialize<StreamPacket>(jsonBytes);
   int imgLen = ReadInt32BE(stream);
   byte[] imgBytes = ReadExactly(stream, imgLen);
   ```
2. 构造一个新的 `TelemetryFrame`：
   - 若 `payload.control` 不为空，则把字段重新序列化为原始 192 B：  
     - 可以在 RK3576 JSON 中附带 `raw_hex`，上位机直接转换为 `byte[]`，再调用当前串口解析方法（封装成 `ParseTelemetry(byte[] frame)`)。  
     - 或者直接用 JSON 字段赋值给 `navi` / UI 控件，不再走二进制解析。建议保留原解析函数：将 `Form1` 里串口 `DataReceived` 中的帧处理逻辑抽到 `ProcessTelemetryFrame(byte[] frame)`，网络线程收到 `raw` 后复用它即可，避免改 UI 代码。
3. JPEG 可在 PC 端另开窗口显示或只校验长度（如果仍需在 PC 上看视频，可把 `imgBytes` 送入 `cv::Mat` 或 `PictureBox`）。

### 3.3 UI 与录制

- 控制面板上的状态灯 `controlPanel.UpdateSerialStatus()` 需要支持 TCP 状态。  
- 录制功能（`recordedDataLines`、`sendDataLines` 等）沿用 `ProcessTelemetryFrame` 的输出；如果需要同步保存视频，可在 PC 端额外使用 `VideoWriter`。

### 3.4 上位机发送控制指令

若未来仍需从 PC 下发控制数据（例如遥控手柄指令），则：  
1. 在 Controller 内构造原串口协议帧（黄色部分字段），发送给 RK3576。  
2. RK3576 接收后通过另一 UART 送到 STM32（`remote_control_task` 监听的口）。  
3. 网络协议也可复用 `[len+cmd][cmd_bytes]`，在 TCP 连接里全双工：PC 发送帧 -> RK3576 解包 -> 串口透传。实现思路与上行相同，可在 `network_sender` 的 socket 上监听 `recv()` 并把数据推给串口线程。

## 4. 验证步骤

1. **串口环回测试**：STM32 依旧用原线连 PC，确认数据正常；再改接 RK3576，使用 `minicom -D /dev/ttySx -b 460800` 验证能抓到帧（校验和正确）。  
2. **RK3576 本地日志**：编译新的 `rknn_yolov8_demo`，运行时观察 `[CTRL]` 日志是否 20 ms 刷新；用 `tcpdump`/`nc -l 8888 > out.bin` 确认有网络流量。  
3. **PC 端接收器**：先用简单 Python/ C# 客户端读取 `[len+JSON][len+JPEG]`，检查 JSON 内 `control.timestamp` 是否随 50 Hz 更新。  
4. **Controller 集成**：切换 UI 到「以太网」模式，确认：  
   - 遥测数据显示与旧串口一致。  
   - 视频窗口能实时刷新（可在 C# 使用 `Bitmap bmp = new Bitmap(new MemoryStream(imgBytes)); pictureBox.Image = bmp;`）。  
   - 断开网线后程序能检测并重连。
5. **系统联合测试**：STM32->RK->PC 全链路运行不少于 30 min，观察是否有 `g_protocol_parser.error_count` 或网络掉线，确保 50 Hz 数据完整。

按照以上步骤，RK3576 将成为中继节点，把 STM32 遥测和 YOLO 检测一并封装推送到 PC，上位机 Controller 只需维护 TCP 通道即可完成显示与控制。

## 5. 现网实现（rk3576 服务端）调试备忘

- **端到端链路**：STM32 → `/dev/ttyS4` (rk3576) → TCP 8888 → Controller。rk3576 现在在 `network_sender()` 内创建监听，Controller 作为客户端连接 `192.168.10.2:8888`，双方在同一 Socket 上全双工收发。
- **上行数据**（STM32→PC）：`control_serial_reader()` 解析 132 字节帧，`build_detection_json()` 将字段（含 `raw` hex）写入 JSON；Controller 在 `HandleStreamPacket()`→`ParseTelemetryFrame()` 中每 20 帧打印 `[TELEMETRY]` 日志，可观察深度、电压、推力等是否异常。
- **下行数据**（PC→STM32）：`SendProtocolData()` 仍生成 29 字节命令帧，优先经 `_streamNetwork` 发送；rk3576 的 `network_command_receiver()` 收到后会打印 `[CMD] camera=... depth=...` 并写回串口，便于确认参数正确。
- **推荐调试顺序**：
  1. 启动 rk3576 程序，等待 `[CTRL] Serial port ... opened` 与 `[NET] Listening ...`。
  2. 在 Controller 控制台确认“Video: 已连接”以及 `[TELEMETRY]` 日志滚动；若没有，检查网络/串口状态。
  3. 操作手柄/控制面板，观察 Controller 的 `发送数据` 记录与 rk3576 输出的 `[CMD]`；若控制板未响应可用串口抓包核对 29 字节帧。
  4. 如需定位网络阻塞，可在两端运行 `netstat`/`tcpdump`；如需追踪串口，可在 rk3576 使用 `stty -F /dev/ttyS4` 检查波特率。

借助上述日志与步骤，可快速定位瓶颈（网络、串口或协议）并完成回归自检。*** End Patch ***!
