// Copyright (c) 2021 by Rockchip Electronics Co., Ltd. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

/*-------------------------------------------
                Includes
-------------------------------------------*/
#include <algorithm>
#include <arpa/inet.h>
#include <array>
#include <atomic>
#include <condition_variable>
#include <dlfcn.h>
#include <deque>
#include <errno.h>
#include <fcntl.h>
#include <iomanip>
#include <mutex>
#include <set>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <queue>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <string>
#include <sstream>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <sys/socket.h>
#include <termios.h>
#include <sys/time.h>
#include <thread>
#include <time.h>
#include <unistd.h>
#include <vector>

#include <linux/videodev2.h>

#define _BASETSD_H

#include "RgaUtils.h"
#include "im2d.h"
#include "opencv2/core/core.hpp"
#ifdef USE_OPENCV_HIGHGUI
#include "opencv2/highgui.hpp"
#endif
#include "opencv2/imgcodecs.hpp"
#include "opencv2/imgproc.hpp"
#include "postprocess.h"
#include "rga.h"
#include "rknn_api.h"

#define PERF_WITH_POST 1
#define CAMERA_DEVICE "/dev/video11"
#define CAMERA_DEFAULT_WIDTH 3840
#define CAMERA_DEFAULT_HEIGHT 2160
#define CAMERA_BUFFER_COUNT 4
#define NETWORK_TARGET_PORT 8888
#define NETWORK_QUEUE_MAX 16
#define NETWORK_STREAM_WIDTH 640
#define NETWORK_STREAM_HEIGHT 360
#define NETWORK_JPEG_QUALITY 45
#define NETWORK_MAX_FPS 20
#define CONTROL_SERIAL_DEVICE "/dev/ttyS4"
#define CONTROL_BAUDRATE B460800
#define CONTROL_FRAME_SIZE 140
#define COMMAND_FRAME_SIZE 29
#define PID_FRAME_CODE 0xF0
#define PID_FRAME_SIGNATURE 0xA5

typedef struct {
  void*  plane[VIDEO_MAX_PLANES];
  size_t length[VIDEO_MAX_PLANES];
} CameraBuffer;

typedef struct {
  int                      fd;
  uint32_t                 width;
  uint32_t                 height;
  int                      plane_count;
  std::vector<CameraBuffer> buffers;
} CameraContext;

static volatile sig_atomic_t g_running = 1;

static void handle_sigint(int sig) { (void)sig; g_running = 0; }

static const uint8_t CONTROL_FRAME_HEAD[4] = {0xAA, 0x55, 0xAA, 0x56};
static const uint8_t CONTROL_FRAME_TAIL[4] = {0xAA, 0x57, 0xAA, 0x58};

#pragma pack(push, 1)
typedef struct {
  uint32_t head;
  float    timestamp;
  float    gyro[3];
  float    acc[3];
  float    mag[3];
  float    angle[3];
  int32_t  thruster[6];
  int32_t  gps_speed;
  int32_t  gps_lon_int;
  int32_t  gps_lon_frac;
  int32_t  gps_lat_int;
  int32_t  gps_lat_frac;
  float    gps_alt;
  float    gps_status;
  float    pressure;
  float    temperature;
  float    current;
  float    voltage;
  float    p30_distance;
  float    p30_confidence;
  uint32_t checksum;
  uint32_t tail;
} ControlFrameRaw;
#pragma pack(pop)

typedef struct {
  ControlFrameRaw                                frame;
  std::array<uint8_t, CONTROL_FRAME_SIZE>        raw_bytes;
  bool                                           valid;
  uint64_t                                       last_update_ms;
  mutable std::mutex                             mutex;
} ControlFrameShared;

static std::atomic<uint64_t> g_control_frame_counter{0};

struct ControlSerialContext {
  ControlFrameShared       shared;
  std::atomic<bool>        stop_requested;
  bool                     started;
  std::thread              worker;
  int                      serial_fd;
  std::mutex               serial_mutex;

  ControlSerialContext() : stop_requested(false), started(false) {
    shared.valid          = false;
    shared.last_update_ms = 0;
    shared.raw_bytes.fill(0);
    serial_fd             = -1;
  }
};

typedef struct {
  std::vector<uint8_t> jpeg_data;
  std::string          meta_json;
} FramePacket;

typedef struct {
  uint8_t  camera_code;
  uint8_t  self_stabilization;
  uint8_t  self_lock;
  uint16_t left_x;
  uint16_t left_y;
  uint16_t right_x;
  uint16_t right_y;
  uint8_t  dive_switch;
  float    target_depth;
  uint8_t  light_level;
  uint32_t checksum;
} CommandFrame;

typedef struct {
  uint8_t channel_id;
  float   kp;
  float   ki;
  float   kd;
} PidUpdateFrame;

class FrameQueue
{
 public:
  explicit FrameQueue(size_t max_size) : max_size_(max_size), stopped_(false) {}

  bool push(FramePacket&& packet)
  {
    std::unique_lock<std::mutex> lock(mutex_);
    if (stopped_) {
      return false;
    }
    if (queue_.size() >= max_size_) {
      queue_.pop_front();
    }
    queue_.emplace_back(std::move(packet));
    cond_.notify_one();
    return true;
  }

  bool pop(FramePacket* packet)
  {
    std::unique_lock<std::mutex> lock(mutex_);
    cond_.wait(lock, [&] { return stopped_ || !queue_.empty(); });
    if (stopped_ && queue_.empty()) {
      return false;
    }
    *packet = std::move(queue_.front());
    queue_.pop_front();
    return true;
  }

  void stop()
  {
    std::lock_guard<std::mutex> lock(mutex_);
    stopped_ = true;
    cond_.notify_all();
  }

  bool stopped()
  {
    std::lock_guard<std::mutex> lock(mutex_);
    return stopped_;
  }

 private:
  std::deque<FramePacket> queue_;
  size_t                  max_size_;
  bool                    stopped_;
  std::mutex              mutex_;
  std::condition_variable cond_;
};

static int  camera_init(CameraContext* cam, const char* device, uint32_t req_width, uint32_t req_height);
static void camera_deinit(CameraContext* cam);
static int  camera_dequeue(CameraContext* cam, v4l2_buffer* buf, v4l2_plane planes[]);
static int  camera_queue(CameraContext* cam, v4l2_buffer* buf);
static void nv12_to_rgb_resize(const uint8_t* y_plane, const uint8_t* uv_plane, int src_w, int src_h, uint8_t* dst,
                               int dst_w, int dst_h);
static inline uint8_t clamp_u8(int value) { return value < 0 ? 0 : (value > 255 ? 255 : value); }
static bool prepare_frame_packet(const cv::Mat& image, const detect_result_group_t* group, float fps,
                                 FramePacket* packet, const ControlFrameShared* control);
static void network_sender(FrameQueue* queue, ControlSerialContext* control);
static bool send_packet(int sock, const FramePacket& packet);
static bool send_all(int sock, const uint8_t* data, size_t len);
static int  create_server_socket(int port);
static int  accept_client(int server_sock);
static void network_command_receiver(int client_sock, std::atomic<bool>* stop_flag, ControlSerialContext* control);
static bool write_control_serial(ControlSerialContext* ctx, const uint8_t* data, size_t len);
static bool decode_command_frame(const uint8_t* data, size_t len, CommandFrame* frame);
static void log_command_frame(const CommandFrame& frame);
static void append_and_debug_commands(std::vector<uint8_t>* buffer, const uint8_t* data, size_t len);
static bool is_pid_frame(const CommandFrame& frame);
static bool decode_pid_update(const uint8_t* data, size_t len, PidUpdateFrame* pid);
static const char* describe_pid_channel(uint8_t channel);
static void control_serial_reader(ControlSerialContext* ctx);
static bool configure_serial_port(int fd);
static bool parse_control_frame(std::vector<uint8_t>& buffer, ControlFrameShared* shared);
static uint64_t get_monotonic_ms();
static double decode_coordinate(int32_t integer_part, int32_t fractional_part);
static std::string bytes_to_hex(const uint8_t* data, size_t len);
/*-------------------------------------------
                  Functions
-------------------------------------------*/

static void dump_tensor_attr(rknn_tensor_attr* attr)
{
  printf("  index=%d, name=%s, n_dims=%d, dims=[%d, %d, %d, %d], n_elems=%d, size=%d, fmt=%s, type=%s, qnt_type=%s, "
         "zp=%d, scale=%f\n",
         attr->index, attr->name, attr->n_dims, attr->dims[0], attr->dims[1], attr->dims[2], attr->dims[3],
         attr->n_elems, attr->size, get_format_string(attr->fmt), get_type_string(attr->type),
         get_qnt_type_string(attr->qnt_type), attr->zp, attr->scale);
}

double __get_us(struct timeval t) { return (t.tv_sec * 1000000 + t.tv_usec); }

static unsigned char* load_data(FILE* fp, size_t ofst, size_t sz)
{
  unsigned char* data;
  int            ret;

  data = NULL;

  if (NULL == fp) {
    return NULL;
  }

  ret = fseek(fp, ofst, SEEK_SET);
  if (ret != 0) {
    printf("blob seek failure.\n");
    return NULL;
  }

  data = (unsigned char*)malloc(sz);
  if (data == NULL) {
    printf("buffer malloc failure.\n");
    return NULL;
  }
  ret = fread(data, 1, sz, fp);
  return data;
}

static unsigned char* load_model(const char* filename, int* model_size)
{
  FILE*          fp;
  unsigned char* data;

  fp = fopen(filename, "rb");
  if (NULL == fp) {
    printf("Open file %s failed.\n", filename);
    return NULL;
  }

  fseek(fp, 0, SEEK_END);
  int size = ftell(fp);

  data = load_data(fp, 0, size);

  fclose(fp);

  *model_size = size;
  return data;
}

static int saveFloat(const char* file_name, float* output, int element_size)
{
  FILE* fp;
  fp = fopen(file_name, "w");
  for (int i = 0; i < element_size; i++) {
    fprintf(fp, "%.6f\n", output[i]);
  }
  fclose(fp);
  return 0;
}

static int camera_init(CameraContext* cam, const char* device, uint32_t req_width, uint32_t req_height)
{
  if (!cam) {
    return -1;
  }
  cam->fd          = -1;
  cam->width       = req_width;
  cam->height      = req_height;
  cam->plane_count = 0;
  cam->buffers.clear();

  cam->fd = open(device, O_RDWR | O_CLOEXEC);
  if (cam->fd < 0) {
    perror("open camera");
    return -1;
  }

  v4l2_capability cap;
  if (ioctl(cam->fd, VIDIOC_QUERYCAP, &cap) < 0) {
    perror("VIDIOC_QUERYCAP");
    camera_deinit(cam);
    return -1;
  }
  if (!(cap.capabilities & V4L2_CAP_VIDEO_CAPTURE_MPLANE)) {
    printf("Camera %s does not support V4L2_CAP_VIDEO_CAPTURE_MPLANE\n", device);
    camera_deinit(cam);
    return -1;
  }

  v4l2_format fmt;
  memset(&fmt, 0, sizeof(fmt));
  fmt.type                = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
  fmt.fmt.pix_mp.width    = req_width;
  fmt.fmt.pix_mp.height   = req_height;
  fmt.fmt.pix_mp.pixelformat = V4L2_PIX_FMT_NV12;
  fmt.fmt.pix_mp.field       = V4L2_FIELD_NONE;
  fmt.fmt.pix_mp.num_planes  = 1;
  fmt.fmt.pix_mp.plane_fmt[0].bytesperline = req_width;
  fmt.fmt.pix_mp.plane_fmt[0].sizeimage    = req_width * req_height * 3 / 2;

  if (ioctl(cam->fd, VIDIOC_S_FMT, &fmt) < 0) {
    perror("VIDIOC_S_FMT");
    camera_deinit(cam);
    return -1;
  }
  cam->width       = fmt.fmt.pix_mp.width;
  cam->height      = fmt.fmt.pix_mp.height;
  cam->plane_count = fmt.fmt.pix_mp.num_planes;
  if (cam->plane_count <= 0) {
    cam->plane_count = 1;
  }
  if (cam->plane_count > 2) {
    cam->plane_count = 2;
  }

  v4l2_requestbuffers req;
  memset(&req, 0, sizeof(req));
  req.count  = CAMERA_BUFFER_COUNT;
  req.type   = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
  req.memory = V4L2_MEMORY_MMAP;
  if (ioctl(cam->fd, VIDIOC_REQBUFS, &req) < 0) {
    perror("VIDIOC_REQBUFS");
    camera_deinit(cam);
    return -1;
  }
  if (req.count < CAMERA_BUFFER_COUNT) {
    printf("Camera only provided %d buffers\n", req.count);
  }

  cam->buffers.resize(req.count);
  for (uint32_t i = 0; i < req.count; ++i) {
    v4l2_buffer buf;
    v4l2_plane  planes[VIDEO_MAX_PLANES];
    memset(&buf, 0, sizeof(buf));
    memset(planes, 0, sizeof(planes));
    buf.type   = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
    buf.memory = V4L2_MEMORY_MMAP;
    buf.index  = i;
    buf.length = cam->plane_count;
    buf.m.planes = planes;
    if (ioctl(cam->fd, VIDIOC_QUERYBUF, &buf) < 0) {
      perror("VIDIOC_QUERYBUF");
      camera_deinit(cam);
      return -1;
    }

    for (int p = 0; p < cam->plane_count; ++p) {
      cam->buffers[i].length[p] = planes[p].length;
      cam->buffers[i].plane[p] =
          mmap(NULL, planes[p].length, PROT_READ | PROT_WRITE, MAP_SHARED, cam->fd, planes[p].m.mem_offset);
      if (cam->buffers[i].plane[p] == MAP_FAILED) {
        perror("mmap");
        cam->buffers[i].plane[p] = NULL;
        camera_deinit(cam);
        return -1;
      }
    }

    if (ioctl(cam->fd, VIDIOC_QBUF, &buf) < 0) {
      perror("VIDIOC_QBUF");
      camera_deinit(cam);
      return -1;
    }
  }

  int type = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
  if (ioctl(cam->fd, VIDIOC_STREAMON, &type) < 0) {
    perror("VIDIOC_STREAMON");
    camera_deinit(cam);
    return -1;
  }

  printf("Camera %s ready: %ux%u (planes=%d)\n", device, cam->width, cam->height, cam->plane_count);
  return 0;
}

static void camera_deinit(CameraContext* cam)
{
  if (!cam) {
    return;
  }
  if (cam->fd >= 0) {
    int type = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
    ioctl(cam->fd, VIDIOC_STREAMOFF, &type);
    close(cam->fd);
    cam->fd = -1;
  }
  for (size_t i = 0; i < cam->buffers.size(); ++i) {
    for (int p = 0; p < cam->plane_count; ++p) {
      if (cam->buffers[i].plane[p]) {
        munmap(cam->buffers[i].plane[p], cam->buffers[i].length[p]);
        cam->buffers[i].plane[p] = NULL;
      }
    }
  }
  cam->buffers.clear();
  cam->plane_count = 0;
}

static int camera_dequeue(CameraContext* cam, v4l2_buffer* buf, v4l2_plane planes[])
{
  if (!cam || cam->fd < 0) {
    return -1;
  }
  memset(buf, 0, sizeof(*buf));
  memset(planes, 0, sizeof(v4l2_plane) * VIDEO_MAX_PLANES);
  buf->type     = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE;
  buf->memory   = V4L2_MEMORY_MMAP;
  buf->length   = cam->plane_count;
  buf->m.planes = planes;

  while (1) {
    if (ioctl(cam->fd, VIDIOC_DQBUF, buf) == 0) {
      return 0;
    }
    if (errno == EINTR) {
      continue;
    }
    if (errno == EAGAIN) {
      return 1;
    }
    perror("VIDIOC_DQBUF");
    return -1;
  }
}

static int camera_queue(CameraContext* cam, v4l2_buffer* buf)
{
  if (!cam || cam->fd < 0) {
    return -1;
  }
  if (ioctl(cam->fd, VIDIOC_QBUF, buf) < 0) {
    perror("VIDIOC_QBUF");
    return -1;
  }
  return 0;
}

static void nv12_to_rgb_resize(const uint8_t* y_plane, const uint8_t* uv_plane, int src_w, int src_h, uint8_t* dst,
                               int dst_w, int dst_h)
{
  if (!y_plane || !dst) {
    return;
  }
  if (!uv_plane) {
    uv_plane = y_plane + src_w * src_h;
  }
  for (int y = 0; y < dst_h; ++y) {
    int src_y = y * src_h / dst_h;
    const uint8_t* y_row  = y_plane + src_y * src_w;
    const uint8_t* uv_row = uv_plane + (src_y / 2) * src_w;
    for (int x = 0; x < dst_w; ++x) {
      int src_x   = x * src_w / dst_w;
      int y_value = y_row[src_x];
      int uv_idx  = (src_x / 2) * 2;
      int u       = uv_row[uv_idx];
      int v       = uv_row[uv_idx + 1];
      int c       = y_value - 16;
      int d       = u - 128;
      int e       = v - 128;
      if (c < 0) {
        c = 0;
      }

      int r = (298 * c + 409 * e + 128) >> 8;
      int g = (298 * c - 100 * d - 208 * e + 128) >> 8;
      int b = (298 * c + 516 * d + 128) >> 8;

      dst[(y * dst_w + x) * 3 + 0] = clamp_u8(r);
      dst[(y * dst_w + x) * 3 + 1] = clamp_u8(g);
      dst[(y * dst_w + x) * 3 + 2] = clamp_u8(b);
    }
  }
}

static uint64_t get_monotonic_ms()
{
  struct timespec ts;
  clock_gettime(CLOCK_MONOTONIC, &ts);
  return static_cast<uint64_t>(ts.tv_sec) * 1000ULL + ts.tv_nsec / 1000000ULL;
}

static double decode_coordinate(int32_t integer_part, int32_t fractional_part)
{
  const double scale = 1000000.0;
  return (static_cast<double>(integer_part) + static_cast<double>(fractional_part) / scale) / 100.0;
}

static std::string bytes_to_hex(const uint8_t* data, size_t len)
{
  static const char* kHex = "0123456789ABCDEF";
  if (!data || len == 0) {
    return "";
  }
  std::string out;
  out.reserve(len * 2);
  for (size_t i = 0; i < len; ++i) {
    uint8_t value = data[i];
    out.push_back(kHex[(value >> 4) & 0x0F]);
    out.push_back(kHex[value & 0x0F]);
  }
  return out;
}

static std::string build_detection_json(const detect_result_group_t* group, float fps, const ControlFrameShared* control)
{
  std::ostringstream oss;
  oss << std::fixed << std::setprecision(2);
  oss << "{\"fps\":" << fps;

  // categories 汇总
  oss << ",\"categories\":[";
  if (group && group->count > 0) {
    for (int i = 0; i < group->count; ++i) {
      const detect_result_t* det = &(group->results[i]);
      oss << (i == 0 ? "\"" : ",\"");
      oss << det->name;
      oss << "\"";
    }
  }
  oss << "]";

  // detections 详细信息
  oss << ",\"detections\":[";
  if (group && group->count > 0) {
    for (int i = 0; i < group->count; ++i) {
      const detect_result_t* det = &(group->results[i]);
      oss << (i == 0 ? "" : ",");
      oss << "{\"label\":\"" << det->name << "\",\"score\":" << det->prop << ",\"box\":["
          << det->box.left << "," << det->box.top << "," << det->box.right << "," << det->box.bottom << "]}";
    }
  }
  oss << "]";

  bool wrote_control = false;
  if (control) {
    uint64_t now_ms   = get_monotonic_ms();
    std::lock_guard<std::mutex> lock(control->mutex);
    bool fresh = control->valid && (now_ms - control->last_update_ms) <= 500;
    if (fresh) {
      oss << ",\"control\":{";
      oss << "\"valid\":true";
      const ControlFrameRaw& frame = control->frame;
      oss << ",\"timestamp\":" << frame.timestamp;
      oss << ",\"gyro\":[" << frame.gyro[0] << "," << frame.gyro[1] << "," << frame.gyro[2] << "]";
      oss << ",\"acc\":[" << frame.acc[0] << "," << frame.acc[1] << "," << frame.acc[2] << "]";
      oss << ",\"mag\":[" << frame.mag[0] << "," << frame.mag[1] << "," << frame.mag[2] << "]";
      oss << ",\"angle\":[" << frame.angle[0] << "," << frame.angle[1] << "," << frame.angle[2] << "]";
      oss << ",\"thruster\":[" << frame.thruster[0] << "," << frame.thruster[1] << "," << frame.thruster[2] << ","
          << frame.thruster[3] << "," << frame.thruster[4] << "," << frame.thruster[5] << "]";
      oss << ",\"gps_speed\":" << frame.gps_speed;
      oss << ",\"gps\":{";
      oss << "\"lon\":" << decode_coordinate(frame.gps_lon_int, frame.gps_lon_frac) << ",";
      oss << "\"lat\":" << decode_coordinate(frame.gps_lat_int, frame.gps_lat_frac) << ",";
      oss << "\"alt\":" << frame.gps_alt;
      oss << "}";
      oss << ",\"gps_status\":" << frame.gps_status;
      oss << ",\"pressure\":" << frame.pressure;
      oss << ",\"temperature\":" << frame.temperature;
      oss << ",\"current\":" << frame.current;
      oss << ",\"voltage\":" << frame.voltage;
      oss << ",\"p30_distance\":" << frame.p30_distance;
      oss << ",\"p30_confidence\":" << frame.p30_confidence;
      oss << ",\"raw\":\"" << bytes_to_hex(control->raw_bytes.data(), control->raw_bytes.size()) << "\"";
      oss << "}";
      wrote_control = true;
    }
  }
  if (!wrote_control) {
    oss << ",\"control\":null";
  }

  oss << "}";
  return oss.str();
}

static bool prepare_frame_packet(const cv::Mat& image, const detect_result_group_t* group, float fps,
                                 FramePacket* packet, const ControlFrameShared* control)
{
  if (!packet) {
    return false;
  }
  std::vector<int> encode_params = {cv::IMWRITE_JPEG_QUALITY, NETWORK_JPEG_QUALITY};
  if (!cv::imencode(".jpg", image, packet->jpeg_data, encode_params)) {
    return false;
  }
  packet->meta_json = build_detection_json(group, fps, control);
  return true;
}

static bool send_all(int sock, const uint8_t* data, size_t len)
{
  size_t total_sent = 0;
  while (len > 0) {
    ssize_t sent = send(sock, data, len, 0);
    if (sent > 0) {
      data += sent;
      len -= sent;
      total_sent += static_cast<size_t>(sent);
      continue;
    }
    if (sent == 0) {
      printf("[NET] send() returned 0 after %zu bytes, closing socket\n", total_sent);
      return false;
    }
    if (errno == EINTR) {
      continue;
    }
    if (errno == EAGAIN || errno == EWOULDBLOCK) {
      printf("[NET] send() timed out after %zu bytes (errno=%d)\n", total_sent, errno);
    } else {
      perror("[NET] send");
    }
    return false;
  }
  return true;
}

static bool send_packet(int sock, const FramePacket& packet)
{
  uint32_t json_len = static_cast<uint32_t>(packet.meta_json.size());
  uint32_t img_len  = static_cast<uint32_t>(packet.jpeg_data.size());
  uint32_t json_be  = htonl(json_len);
  uint32_t img_be   = htonl(img_len);
  if (!send_all(sock, reinterpret_cast<uint8_t*>(&json_be), sizeof(json_be))) {
    return false;
  }
  if (!packet.meta_json.empty()) {
    if (!send_all(sock, reinterpret_cast<const uint8_t*>(packet.meta_json.data()), packet.meta_json.size())) {
      return false;
    }
  }
  if (!send_all(sock, reinterpret_cast<uint8_t*>(&img_be), sizeof(img_be))) {
    return false;
  }
  if (img_len > 0) {
    if (!send_all(sock, packet.jpeg_data.data(), packet.jpeg_data.size())) {
      return false;
    }
  }
  return true;
}

static int create_server_socket(int port)
{
  int sock = socket(AF_INET, SOCK_STREAM, 0);
  if (sock < 0) {
    perror("socket");
    return -1;
  }
  int opt = 1;
  if (setsockopt(sock, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt)) < 0) {
    perror("setsockopt");
  }
  sockaddr_in addr;
  memset(&addr, 0, sizeof(addr));
  addr.sin_family      = AF_INET;
  addr.sin_addr.s_addr = htonl(INADDR_ANY);
  addr.sin_port        = htons(port);
  if (bind(sock, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) < 0) {
    perror("bind");
    close(sock);
    return -1;
  }
  if (listen(sock, 4) < 0) {
    perror("listen");
    close(sock);
    return -1;
  }
  int flags = fcntl(sock, F_GETFL, 0);
  if (flags >= 0) {
    fcntl(sock, F_SETFL, flags | O_NONBLOCK);
  }
  printf("[NET] Listening on 0.0.0.0:%d\n", port);
  return sock;
}

static void configure_client_socket(int sock)
{
  if (sock < 0) {
    return;
  }
  int enable = 1;
  if (setsockopt(sock, SOL_SOCKET, SO_KEEPALIVE, &enable, sizeof(enable)) < 0) {
    perror("setsockopt SO_KEEPALIVE");
  }
#ifdef TCP_KEEPIDLE
  int keep_idle = 30;
  if (setsockopt(sock, IPPROTO_TCP, TCP_KEEPIDLE, &keep_idle, sizeof(keep_idle)) < 0) {
    perror("setsockopt TCP_KEEPIDLE");
  }
#endif
#ifdef TCP_KEEPINTVL
  int keep_intvl = 5;
  if (setsockopt(sock, IPPROTO_TCP, TCP_KEEPINTVL, &keep_intvl, sizeof(keep_intvl)) < 0) {
    perror("setsockopt TCP_KEEPINTVL");
  }
#endif
#ifdef TCP_KEEPCNT
  int keep_cnt = 3;
  if (setsockopt(sock, IPPROTO_TCP, TCP_KEEPCNT, &keep_cnt, sizeof(keep_cnt)) < 0) {
    perror("setsockopt TCP_KEEPCNT");
  }
#endif
#ifdef TCP_USER_TIMEOUT
  int user_timeout_ms = 8000;
  if (setsockopt(sock, IPPROTO_TCP, TCP_USER_TIMEOUT, &user_timeout_ms, sizeof(user_timeout_ms)) < 0) {
    perror("setsockopt TCP_USER_TIMEOUT");
  }
#endif
  timeval send_timeout;
  send_timeout.tv_sec  = 2;
  send_timeout.tv_usec = 0;
  if (setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, &send_timeout, sizeof(send_timeout)) < 0) {
    perror("setsockopt SO_SNDTIMEO");
  }
  printf("[NET] Client keepalive configured (idle=%ds interval=%ds count=%d timeout=%dms)\n",
#ifdef TCP_KEEPIDLE
         keep_idle,
#else
         0,
#endif
#ifdef TCP_KEEPINTVL
         keep_intvl,
#else
         0,
#endif
#ifdef TCP_KEEPCNT
         keep_cnt,
#else
         0,
#endif
#ifdef TCP_USER_TIMEOUT
         user_timeout_ms
#else
         0
#endif
  );
}

static int accept_client(int server_sock)
{
  sockaddr_in client_addr;
  socklen_t   addr_len = sizeof(client_addr);
  int         client   = accept(server_sock, reinterpret_cast<sockaddr*>(&client_addr), &addr_len);
  if (client >= 0) {
    char addr_str[INET_ADDRSTRLEN] = {0};
    inet_ntop(AF_INET, &client_addr.sin_addr, addr_str, sizeof(addr_str));
    printf("[NET] Client connected from %s:%d\n", addr_str, ntohs(client_addr.sin_port));
    return client;
  }
  if (errno == EAGAIN || errno == EWOULDBLOCK) {
    return -2;
  }
  perror("accept");
  return -1;
}

static bool write_control_serial(ControlSerialContext* ctx, const uint8_t* data, size_t len)
{
  if (!ctx || !data || len == 0) {
    return false;
  }
  std::lock_guard<std::mutex> lock(ctx->serial_mutex);
  if (ctx->serial_fd < 0) {
    return false;
  }
  size_t total = 0;
  while (total < len) {
    ssize_t written = write(ctx->serial_fd, data + total, len - total);
    if (written > 0) {
      total += written;
      continue;
    }
    if (written < 0 && (errno == EINTR)) {
      continue;
    }
    perror("write control serial");
    return false;
  }
  tcdrain(ctx->serial_fd);
  return true;
}

static bool decode_command_frame(const uint8_t* data, size_t len, CommandFrame* frame)
{
  if (!data || !frame || len < COMMAND_FRAME_SIZE) {
    return false;
  }
  if (memcmp(data, CONTROL_FRAME_HEAD, 4) != 0) {
    return false;
  }
  if (memcmp(data + COMMAND_FRAME_SIZE - 4, CONTROL_FRAME_TAIL, 4) != 0) {
    return false;
  }
  uint32_t checksum = 0;
  for (size_t i = 4; i <= 20 && i < COMMAND_FRAME_SIZE - 8; ++i) {
    checksum += data[i];
  }
  uint32_t frame_checksum = 0;
  memcpy(&frame_checksum, data + 21, sizeof(uint32_t));
  if (checksum != frame_checksum) {
    return false;
  }
  memset(frame, 0, sizeof(CommandFrame));
  frame->camera_code        = data[4];
  frame->self_stabilization = data[5];
  frame->self_lock          = data[6];
  frame->left_x             = data[7] | (data[8] << 8);
  frame->left_y             = data[9] | (data[10] << 8);
  frame->right_x            = data[11] | (data[12] << 8);
  frame->right_y            = data[13] | (data[14] << 8);
  frame->dive_switch        = data[15];
  memcpy(&frame->target_depth, data + 16, sizeof(float));
  frame->light_level = data[20];
  frame->checksum    = frame_checksum;
  return true;
}

static void log_command_frame(const CommandFrame& frame)
{
  printf("[CMD] camera=%u stab=%u lock=%u LX=%u LY=%u RX=%u RY=%u dive=%u depth=%.2f light=%u checksum=0x%08X\n",
         frame.camera_code, frame.self_stabilization, frame.self_lock, frame.left_x, frame.left_y, frame.right_x,
         frame.right_y, frame.dive_switch, frame.target_depth, frame.light_level, frame.checksum);

  if (is_pid_frame(frame)) {
    printf("[CMD] Detected PID update frame (channel=%u)\n", frame.self_lock);
  }
}

static bool is_pid_frame(const CommandFrame& frame)
{
  return (frame.camera_code == PID_FRAME_CODE && frame.self_stabilization == PID_FRAME_SIGNATURE);
}

static const char* describe_pid_channel(uint8_t channel)
{
  switch (channel) {
    case 0x01:
      return "Yaw Outer";
    case 0x02:
      return "Yaw Rate";
    case 0x03:
      return "Roll Outer";
    case 0x04:
      return "Roll Rate";
    case 0x05:
      return "Depth";
    default:
      return "Unknown";
  }
}

static bool decode_pid_update(const uint8_t* data, size_t len, PidUpdateFrame* pid)
{
  if (!data || !pid || len < COMMAND_FRAME_SIZE) {
    return false;
  }
  pid->channel_id = data[6];
  memcpy(&pid->kp, data + 7, sizeof(float));
  memcpy(&pid->ki, data + 11, sizeof(float));
  memcpy(&pid->kd, data + 15, sizeof(float));
  return true;
}

static void append_and_debug_commands(std::vector<uint8_t>* buffer, const uint8_t* data, size_t len)
{
  if (!buffer || !data || len == 0) {
    return;
  }
  buffer->insert(buffer->end(), data, data + len);
  while (buffer->size() >= COMMAND_FRAME_SIZE) {
    if (memcmp(buffer->data(), CONTROL_FRAME_HEAD, 4) != 0) {
      buffer->erase(buffer->begin());
      continue;
    }
    if (memcmp(buffer->data() + COMMAND_FRAME_SIZE - 4, CONTROL_FRAME_TAIL, 4) != 0) {
      buffer->erase(buffer->begin());
      continue;
    }
    CommandFrame frame;
    if (decode_command_frame(buffer->data(), COMMAND_FRAME_SIZE, &frame)) {
      log_command_frame(frame);
      if (is_pid_frame(frame)) {
        PidUpdateFrame pid_info;
        if (decode_pid_update(buffer->data(), COMMAND_FRAME_SIZE, &pid_info)) {
          printf("[PID] %s (ID=%u) -> KP=%.4f KI=%.4f KD=%.4f\n",
                 describe_pid_channel(pid_info.channel_id), pid_info.channel_id, pid_info.kp, pid_info.ki, pid_info.kd);
        }
      }
    }
    buffer->erase(buffer->begin(), buffer->begin() + COMMAND_FRAME_SIZE);
  }
  if (buffer->size() > COMMAND_FRAME_SIZE * 4) {
    buffer->erase(buffer->begin(), buffer->end() - COMMAND_FRAME_SIZE);
  }
}

static void network_command_receiver(int client_sock, std::atomic<bool>* stop_flag, ControlSerialContext* control)
{
  std::vector<uint8_t> recv_buf(512);
  std::vector<uint8_t> debug_bytes;
  std::vector<uint8_t> forward_bytes;
  debug_bytes.reserve(COMMAND_FRAME_SIZE * 2);
  forward_bytes.reserve(COMMAND_FRAME_SIZE * 4);
  while (stop_flag && !stop_flag->load()) {
    ssize_t n = recv(client_sock, recv_buf.data(), recv_buf.size(), 0);
    if (n > 0) {
      append_and_debug_commands(&debug_bytes, recv_buf.data(), static_cast<size_t>(n));
      forward_bytes.insert(forward_bytes.end(), recv_buf.begin(), recv_buf.begin() + static_cast<size_t>(n));

      while (forward_bytes.size() >= COMMAND_FRAME_SIZE) {
        auto it = std::search(forward_bytes.begin(), forward_bytes.end(), std::begin(CONTROL_FRAME_HEAD),
                              std::end(CONTROL_FRAME_HEAD));
        if (it == forward_bytes.end()) {
          forward_bytes.clear();
          break;
        }
        size_t head_index = std::distance(forward_bytes.begin(), it);
        if (head_index > 0) {
          forward_bytes.erase(forward_bytes.begin(), forward_bytes.begin() + head_index);
        }
        if (forward_bytes.size() < COMMAND_FRAME_SIZE) {
          break;
        }
        if (memcmp(forward_bytes.data() + COMMAND_FRAME_SIZE - 4, CONTROL_FRAME_TAIL, 4) != 0) {
          forward_bytes.erase(forward_bytes.begin());
          continue;
        }

        CommandFrame frame;
        if (!decode_command_frame(forward_bytes.data(), COMMAND_FRAME_SIZE, &frame)) {
          forward_bytes.erase(forward_bytes.begin());
          continue;
        }

        if (!write_control_serial(control, forward_bytes.data(), COMMAND_FRAME_SIZE)) {
          printf("[NET] Failed to forward command frame to serial\n");
        }
        forward_bytes.erase(forward_bytes.begin(), forward_bytes.begin() + COMMAND_FRAME_SIZE);
      }

      if (forward_bytes.size() > COMMAND_FRAME_SIZE * 8) {
        forward_bytes.erase(forward_bytes.begin(), forward_bytes.end() - COMMAND_FRAME_SIZE);
      }
    } else if (n == 0) {
      printf("[NET] Client closed connection\n");
      break;
    } else {
      if (errno == EINTR) {
        continue;
      }
      perror("recv");
      break;
    }
  }
}

static void network_sender(FrameQueue* queue, ControlSerialContext* control)
{
  if (!queue) {
    return;
  }
  int listen_sock = -1;
  while (listen_sock < 0 && !queue->stopped()) {
    listen_sock = create_server_socket(NETWORK_TARGET_PORT);
    if (listen_sock < 0) {
      struct timespec ts = {0, 500 * 1000 * 1000};
      nanosleep(&ts, NULL);
    }
  }
  if (listen_sock < 0) {
    return;
  }

  int                  client_sock = -1;
  std::thread          recv_thread;
  std::atomic<bool>    recv_stop{false};

  while (true) {
    if (client_sock < 0) {
      int accepted = accept_client(listen_sock);
      if (accepted >= 0) {
        client_sock = accepted;
        configure_client_socket(client_sock);
        recv_stop.store(false);
        recv_thread = std::thread(network_command_receiver, client_sock, &recv_stop, control);
      } else if (accepted == -2) {
        if (queue->stopped()) {
          break;
        }
      }
      if (client_sock < 0) {
        struct timespec ts = {0, 100 * 1000 * 1000};
        nanosleep(&ts, NULL);
        continue;
      }
    }

    FramePacket packet;
    if (!queue->pop(&packet)) {
      break;
    }

    if (!send_packet(client_sock, packet)) {
      printf("[NET] Send failed, closing client\n");
      recv_stop.store(true);
      shutdown(client_sock, SHUT_RDWR);
      if (recv_thread.joinable()) {
        recv_thread.join();
      }
      close(client_sock);
      client_sock = -1;
    }
  }

  if (client_sock >= 0) {
    recv_stop.store(true);
    shutdown(client_sock, SHUT_RDWR);
    if (recv_thread.joinable()) {
      recv_thread.join();
    }
    close(client_sock);
  }
  if (listen_sock >= 0) {
    close(listen_sock);
  }
}

static bool configure_serial_port(int fd)
{
  termios options;
  if (tcgetattr(fd, &options) != 0) {
    perror("tcgetattr");
    return false;
  }
  cfmakeraw(&options);
  cfsetispeed(&options, CONTROL_BAUDRATE);
  cfsetospeed(&options, CONTROL_BAUDRATE);
  options.c_cflag |= (CLOCAL | CREAD);
  options.c_cflag &= ~CSTOPB;
  options.c_cflag &= ~CRTSCTS;
  options.c_cflag &= ~CSIZE;
  options.c_cflag |= CS8;
  options.c_iflag &= ~(IXON | IXOFF | IXANY);
  options.c_cc[VMIN]  = 0;
  options.c_cc[VTIME] = 1;
  if (tcsetattr(fd, TCSANOW, &options) != 0) {
    perror("tcsetattr");
    return false;
  }
  return true;
}

static bool parse_control_frame(std::vector<uint8_t>& buffer, ControlFrameShared* shared)
{
  if (!shared) {
    return false;
  }
  while (buffer.size() >= CONTROL_FRAME_SIZE) {
    auto it = std::search(buffer.begin(), buffer.end(), std::begin(CONTROL_FRAME_HEAD),
                          std::end(CONTROL_FRAME_HEAD));
    if (it == buffer.end()) {
      buffer.clear();
      return false;
    }
    size_t head_index = std::distance(buffer.begin(), it);
    if (buffer.size() - head_index < CONTROL_FRAME_SIZE) {
      buffer.erase(buffer.begin(), buffer.begin() + head_index);
      return false;
    }
    const uint8_t* frame_ptr = buffer.data() + head_index;
    if (memcmp(frame_ptr + CONTROL_FRAME_SIZE - 4, CONTROL_FRAME_TAIL, 4) != 0) {
      buffer.erase(buffer.begin(), buffer.begin() + head_index + 1);
      continue;
    }
    uint32_t checksum = 0;
    for (size_t i = 4; i < CONTROL_FRAME_SIZE - 8; ++i) {
      checksum += frame_ptr[i];
    }
    uint32_t frame_checksum = 0;
    memcpy(&frame_checksum, frame_ptr + CONTROL_FRAME_SIZE - 8, sizeof(uint32_t));
    if (checksum != frame_checksum) {
      buffer.erase(buffer.begin(), buffer.begin() + head_index + CONTROL_FRAME_SIZE);
      continue;
    }
    ControlFrameRaw raw_frame;
    memcpy(&raw_frame, frame_ptr, sizeof(ControlFrameRaw));
    {
      std::lock_guard<std::mutex> lock(shared->mutex);
      shared->frame          = raw_frame;
      std::copy(frame_ptr, frame_ptr + CONTROL_FRAME_SIZE, shared->raw_bytes.begin());
      shared->valid          = true;
      shared->last_update_ms = get_monotonic_ms();
    }
    uint64_t count = g_control_frame_counter.fetch_add(1) + 1;
    if (count == 1 || (count % 50) == 0) {
      printf("[CTRL] frame=%lu ts=%.2f gyro=[%.2f %.2f %.2f] pressure=%.2fPa voltage=%.2fV p30=%.2f conf=%.2f\n",
             count, raw_frame.timestamp, raw_frame.gyro[0], raw_frame.gyro[1], raw_frame.gyro[2], raw_frame.pressure,
             raw_frame.voltage, raw_frame.p30_distance, raw_frame.p30_confidence);
    }
    buffer.erase(buffer.begin(), buffer.begin() + head_index + CONTROL_FRAME_SIZE);
    return true;
  }
  return false;
}

static void control_serial_reader(ControlSerialContext* ctx)
{
  if (!ctx) {
    return;
  }
  ControlFrameShared* shared = &ctx->shared;
  while (!ctx->stop_requested.load()) {
    int fd = open(CONTROL_SERIAL_DEVICE, O_RDWR | O_NOCTTY | O_NONBLOCK);
    if (fd < 0) {
      perror("open control serial");
      struct timespec ts = {0, 500 * 1000 * 1000};
      nanosleep(&ts, NULL);
      continue;
    }
    if (!configure_serial_port(fd)) {
      close(fd);
      struct timespec ts = {0, 500 * 1000 * 1000};
      nanosleep(&ts, NULL);
      continue;
    }
    {
      std::lock_guard<std::mutex> lock(ctx->serial_mutex);
      ctx->serial_fd = fd;
    }
    printf("[CTRL] Serial port %s opened @460800\n", CONTROL_SERIAL_DEVICE);
    std::vector<uint8_t> buffer;
    buffer.reserve(1024);
    uint64_t last_check_ms    = get_monotonic_ms();
    uint64_t last_seen_update = 0;
    while (!ctx->stop_requested.load()) {
      uint8_t tmp[256];
      ssize_t n = read(fd, tmp, sizeof(tmp));
      if (n > 0) {
        buffer.insert(buffer.end(), tmp, tmp + n);
        while (parse_control_frame(buffer, shared)) {
          // keep draining any complete frames that arrived in this read
        }
      } else if (n == 0) {
        struct timespec ts = {0, 10 * 1000 * 1000};
        nanosleep(&ts, NULL);
      } else {
        if (errno != EAGAIN && errno != EINTR) {
          perror("read control serial");
          break;
        }
        struct timespec ts = {0, 10 * 1000 * 1000};
        nanosleep(&ts, NULL);
      }
      uint64_t now_ms = get_monotonic_ms();
      if (now_ms - last_check_ms >= 1000) {
        uint64_t shared_update = 0;
        bool     shared_valid  = false;
        {
          std::lock_guard<std::mutex> lock(shared->mutex);
          shared_update = shared->last_update_ms;
          shared_valid  = shared->valid;
        }
        if (shared_valid && shared_update == last_seen_update && (now_ms - shared_update) > 2000) {
          printf("[CTRL] no frames for %lums, reopening serial\n",
                 static_cast<unsigned long>(now_ms - shared_update));
          break;
        }
        last_seen_update = shared_update;
        last_check_ms    = now_ms;
      }
    }
    printf("[CTRL] Serial port reader restarting...\n");
    {
      std::lock_guard<std::mutex> lock(ctx->serial_mutex);
      ctx->serial_fd = -1;
    }
    close(fd);
  }
}
/*-------------------------------------------
                  Main Functions
-------------------------------------------*/
int main(int argc, char** argv)
{
  int            status     = 0;
  char*          model_name = NULL;
  rknn_context   ctx;
  size_t         actual_size        = 0;
  int            img_width          = 0;
  int            img_height         = 0;
  int            img_channel        = 0;
  const float    nms_threshold      = NMS_THRESH;
  const float    box_conf_threshold = BOX_THRESH;
  struct timeval start_time, stop_time;
  int            ret;
  CameraContext  camera_ctx;
  camera_ctx.fd          = -1;
  camera_ctx.plane_count = 0;
  FrameQueue            network_queue(NETWORK_QUEUE_MAX);
  bool                  network_thread_started = false;
  std::thread           network_thread;
  ControlSerialContext  control_serial;

  // init rga context
  rga_buffer_t src;
  rga_buffer_t dst;
  im_rect      src_rect;
  im_rect      dst_rect;
  memset(&src_rect, 0, sizeof(src_rect));
  memset(&dst_rect, 0, sizeof(dst_rect));
  memset(&src, 0, sizeof(src));
  memset(&dst, 0, sizeof(dst));

  if (argc != 3) {
    printf("Usage: %s <rknn model> <jpg>|camera\n", argv[0]);
    return -1;
  }

  bool use_camera = (strcmp(argv[2], "camera") == 0);
  if (use_camera) {
    signal(SIGINT, handle_sigint);
    printf("Camera input mode enabled. Device: %s\n", CAMERA_DEVICE);
  }

  printf("post process config: box_conf_threshold = %.2f, nms_threshold = %.2f\n", box_conf_threshold, nms_threshold);

  model_name = (char*)argv[1];
  cv::Mat orig_img;
  cv::Mat img;
  char*   image_name = argv[2];

  if (!use_camera) {
    printf("Read %s ...\n", image_name);
    orig_img = cv::imread(image_name, 1);
    if (!orig_img.data) {
      printf("cv::imread %s fail!\n", image_name);
      return -1;
    }
    cv::cvtColor(orig_img, img, cv::COLOR_BGR2RGB);
    img_width  = img.cols;
    img_height = img.rows;
    printf("img width = %d, img height = %d\n", img_width, img_height);
  }

  /* Create the neural network */
  printf("Loading mode...\n");
  int            model_data_size = 0;
  unsigned char* model_data      = load_model(model_name, &model_data_size);
  ret                            = rknn_init(&ctx, model_data, model_data_size, 0, NULL);
  if (ret < 0) {
    printf("rknn_init error ret=%d\n", ret);
    return -1;
  }

  rknn_sdk_version version;
  ret = rknn_query(ctx, RKNN_QUERY_SDK_VERSION, &version, sizeof(rknn_sdk_version));
  if (ret < 0) {
    printf("rknn_init error ret=%d\n", ret);
    return -1;
  }
  printf("sdk version: %s driver version: %s\n", version.api_version, version.drv_version);

  rknn_input_output_num io_num;
  ret = rknn_query(ctx, RKNN_QUERY_IN_OUT_NUM, &io_num, sizeof(io_num));
  if (ret < 0) {
    printf("rknn_init error ret=%d\n", ret);
    return -1;
  }
  printf("model input num: %d, output num: %d\n", io_num.n_input, io_num.n_output);

  rknn_tensor_attr input_attrs[io_num.n_input];
  memset(input_attrs, 0, sizeof(input_attrs));
  for (int i = 0; i < io_num.n_input; i++) {
    input_attrs[i].index = i;
    ret                  = rknn_query(ctx, RKNN_QUERY_INPUT_ATTR, &(input_attrs[i]), sizeof(rknn_tensor_attr));
    if (ret < 0) {
      printf("rknn_init error ret=%d\n", ret);
      return -1;
    }
    dump_tensor_attr(&(input_attrs[i]));
  }

  rknn_tensor_attr output_attrs[io_num.n_output];
  memset(output_attrs, 0, sizeof(output_attrs));
  for (int i = 0; i < io_num.n_output; i++) {
    output_attrs[i].index = i;
    ret                   = rknn_query(ctx, RKNN_QUERY_OUTPUT_ATTR, &(output_attrs[i]), sizeof(rknn_tensor_attr));
    dump_tensor_attr(&(output_attrs[i]));
  }

  int channel = 3;
  int width   = 0;
  int height  = 0;
  if (input_attrs[0].fmt == RKNN_TENSOR_NCHW) {
    printf("model is NCHW input fmt\n");
    channel = input_attrs[0].dims[1];
    height  = input_attrs[0].dims[2];
    width   = input_attrs[0].dims[3];
  } else {
    printf("model is NHWC input fmt\n");
    height  = input_attrs[0].dims[1];
    width   = input_attrs[0].dims[2];
    channel = input_attrs[0].dims[3];
  }

  printf("model input height=%d, width=%d, channel=%d\n", height, width, channel);

  rknn_input inputs[1];
  memset(inputs, 0, sizeof(inputs));
  inputs[0].index        = 0;
  inputs[0].type         = RKNN_TENSOR_UINT8;
  inputs[0].size         = width * height * channel;
  inputs[0].fmt          = RKNN_TENSOR_NHWC;
  inputs[0].pass_through = 0;

  // You may not need resize when src resulotion equals to dst resulotion
  void* resize_buf = nullptr;

  if (!use_camera) {
    if (img_width != width || img_height != height) {
      printf("resize with RGA!\n");
      resize_buf = malloc(height * width * channel);
      memset(resize_buf, 0x00, height * width * channel);

      src = wrapbuffer_virtualaddr((void*)img.data, img_width, img_height, RK_FORMAT_RGB_888);
      dst = wrapbuffer_virtualaddr((void*)resize_buf, width, height, RK_FORMAT_RGB_888);
      ret = imcheck(src, dst, src_rect, dst_rect);
      if (IM_STATUS_NOERROR != ret) {
        printf("%d, check error! %s", __LINE__, imStrError((IM_STATUS)ret));
        return -1;
      }
      IM_STATUS STATUS = imresize(src, dst);

      // for debug
      cv::Mat resize_img(cv::Size(width, height), CV_8UC3, resize_buf);
      cv::imwrite("resize_input.jpg", resize_img);

      inputs[0].buf = resize_buf;
    } else {
      inputs[0].buf = (void*)img.data;
    }
  } else {
    resize_buf = malloc(height * width * channel);
    if (!resize_buf) {
      printf("camera resize buffer malloc failure.\n");
      return -1;
    }
    memset(resize_buf, 0x00, height * width * channel);
    inputs[0].buf = resize_buf;
    img_width     = width;
    img_height    = height;
  }

  if (!use_camera) {
    gettimeofday(&start_time, NULL);
    rknn_inputs_set(ctx, io_num.n_input, inputs);

    rknn_output outputs[io_num.n_output];
    memset(outputs, 0, sizeof(outputs));
    for (int i = 0; i < io_num.n_output; i++) {
      outputs[i].want_float = 0;
    }

    ret = rknn_run(ctx, NULL);
    ret = rknn_outputs_get(ctx, io_num.n_output, outputs, NULL);
    gettimeofday(&stop_time, NULL);
    printf("once run use %f ms\n", (__get_us(stop_time) - __get_us(start_time)) / 1000);

    // post process
    float scale_w = (float)width / img_width;
    float scale_h = (float)height / img_height;

    detect_result_group_t detect_result_group;
    std::vector<float>    out_scales;
    std::vector<int32_t>  out_zps;
    std::vector<int32_t>  grid_hs;
    std::vector<int32_t>  grid_ws;
    std::vector<int8_t*>  output_ptrs(io_num.n_output);
    int                   dfl_len           = output_attrs[0].dims[1] / 4;
    int                   output_per_branch = io_num.n_output / 3;
    for (int i = 0; i < io_num.n_output; ++i) {
      output_ptrs[i] = (int8_t*)outputs[i].buf;
      out_scales.push_back(output_attrs[i].scale);
      out_zps.push_back(output_attrs[i].zp);
      grid_hs.push_back(output_attrs[i].dims[2]);
      grid_ws.push_back(output_attrs[i].dims[3]);
    }
    post_process(output_ptrs.data(), dfl_len, output_per_branch, height, width, box_conf_threshold, nms_threshold,
                 scale_w, scale_h, out_zps, out_scales, grid_hs, grid_ws, &detect_result_group);

    // Draw Objects
    char text[256];
    for (int i = 0; i < detect_result_group.count; i++) {
      detect_result_t* det_result = &(detect_result_group.results[i]);
      sprintf(text, "%s %.1f%%", det_result->name, det_result->prop * 100);
      printf("%s @ (%d %d %d %d) %f\n", det_result->name, det_result->box.left, det_result->box.top,
             det_result->box.right, det_result->box.bottom, det_result->prop);
      int x1 = det_result->box.left;
      int y1 = det_result->box.top;
      int x2 = det_result->box.right;
      int y2 = det_result->box.bottom;
      rectangle(orig_img, cv::Point(x1, y1), cv::Point(x2, y2), cv::Scalar(255, 0, 0, 255), 3);
      putText(orig_img, text, cv::Point(x1, y1 - 5), cv::FONT_HERSHEY_SIMPLEX, 0.5, cv::Scalar(0, 255, 0));
    }

    imwrite("./out.jpg", orig_img);
    ret = rknn_outputs_release(ctx, io_num.n_output, outputs);

    // loop test
    int test_count = 10;
    gettimeofday(&start_time, NULL);
    for (int i = 0; i < test_count; ++i) {
      rknn_inputs_set(ctx, io_num.n_input, inputs);
      ret = rknn_run(ctx, NULL);
      ret = rknn_outputs_get(ctx, io_num.n_output, outputs, NULL);
#if PERF_WITH_POST
      for (int j = 0; j < io_num.n_output; ++j) {
        output_ptrs[j] = (int8_t*)outputs[j].buf;
      }
      post_process(output_ptrs.data(), dfl_len, output_per_branch, height, width, box_conf_threshold, nms_threshold,
                   scale_w, scale_h, out_zps, out_scales, grid_hs, grid_ws, &detect_result_group);
#endif
      ret = rknn_outputs_release(ctx, io_num.n_output, outputs);
    }
    gettimeofday(&stop_time, NULL);
    printf("loop count = %d , average run  %f ms\n", test_count,
           (__get_us(stop_time) - __get_us(start_time)) / 1000.0 / test_count);
  } else {
    std::vector<float>    out_scales;
    std::vector<int32_t>  out_zps;
    std::vector<int32_t>  grid_hs;
    std::vector<int32_t>  grid_ws;
    std::vector<int8_t*>  output_ptrs(io_num.n_output);
    for (int i = 0; i < io_num.n_output; ++i) {
      out_scales.push_back(output_attrs[i].scale);
      out_zps.push_back(output_attrs[i].zp);
      grid_hs.push_back(output_attrs[i].dims[2]);
      grid_ws.push_back(output_attrs[i].dims[3]);
    }
    int dfl_len           = output_attrs[0].dims[1] / 4;
    int output_per_branch = io_num.n_output / 3;

    if (camera_init(&camera_ctx, CAMERA_DEVICE, CAMERA_DEFAULT_WIDTH, CAMERA_DEFAULT_HEIGHT) != 0) {
      printf("Failed to initialize camera.\n");
      status = -1;
    } else {
      if (!network_thread_started) {
        network_thread = std::thread(network_sender, &network_queue, &control_serial);
        network_thread_started = true;
      }
      if (!control_serial.started) {
        control_serial.stop_requested.store(false);
        control_serial.worker = std::thread(control_serial_reader, &control_serial);
        control_serial.started = true;
      }
      printf("Press Ctrl+C to stop camera inference.\n");
      rknn_output outputs[io_num.n_output];
      memset(outputs, 0, sizeof(outputs));
      for (int i = 0; i < io_num.n_output; i++) {
        outputs[i].want_float = 0;
      }
      while (g_running) {
        v4l2_buffer buf;
        v4l2_plane  planes[VIDEO_MAX_PLANES];
        int         dq_ret = camera_dequeue(&camera_ctx, &buf, planes);
        if (dq_ret == 1) {
          continue;
        }
        if (dq_ret < 0) {
          status = -1;
          break;
        }

        uint8_t* y_plane  = static_cast<uint8_t*>(camera_ctx.buffers[buf.index].plane[0]);
        uint8_t* uv_plane = nullptr;
        if (camera_ctx.plane_count > 1 && camera_ctx.buffers[buf.index].plane[1]) {
          uv_plane = static_cast<uint8_t*>(camera_ctx.buffers[buf.index].plane[1]);
        }

        nv12_to_rgb_resize(y_plane, uv_plane, camera_ctx.width, camera_ctx.height, (uint8_t*)resize_buf, width,
                           height);

        gettimeofday(&start_time, NULL);
        rknn_inputs_set(ctx, io_num.n_input, inputs);
        ret = rknn_run(ctx, NULL);
        ret = rknn_outputs_get(ctx, io_num.n_output, outputs, NULL);
        gettimeofday(&stop_time, NULL);
        printf("frame latency %f ms\n", (__get_us(stop_time) - __get_us(start_time)) / 1000);
        float frame_latency_ms = (__get_us(stop_time) - __get_us(start_time)) / 1000.0f;
        float fps_value        = frame_latency_ms > 0 ? 1000.0f / frame_latency_ms : 0.0f;

        detect_result_group_t detect_result_group;
        for (int i = 0; i < io_num.n_output; ++i) {
          output_ptrs[i] = (int8_t*)outputs[i].buf;
        }
        post_process(output_ptrs.data(), dfl_len, output_per_branch, height, width, box_conf_threshold, nms_threshold,
                     1.0f, 1.0f, out_zps, out_scales, grid_hs, grid_ws, &detect_result_group);

        cv::Mat rgb_img(cv::Size(width, height), CV_8UC3, resize_buf);
        cv::Mat display_img;
        cv::cvtColor(rgb_img, display_img, cv::COLOR_RGB2BGR);
        char text[256];
        for (int i = 0; i < detect_result_group.count; i++) {
          detect_result_t* det_result = &(detect_result_group.results[i]);
          sprintf(text, "%s %.1f%%", det_result->name, det_result->prop * 100);
          printf("%s @ (%d %d %d %d) %f\n", det_result->name, det_result->box.left, det_result->box.top,
                 det_result->box.right, det_result->box.bottom, det_result->prop);
          rectangle(display_img, cv::Point(det_result->box.left, det_result->box.top),
                    cv::Point(det_result->box.right, det_result->box.bottom), cv::Scalar(255, 0, 0, 255), 2);
          putText(display_img, text, cv::Point(det_result->box.left, det_result->box.top - 4),
                  cv::FONT_HERSHEY_SIMPLEX, 0.5, cv::Scalar(0, 255, 0));
        }
        if (detect_result_group.count > 0) {
          imwrite("./camera_out.jpg", display_img);
        }
#ifdef USE_OPENCV_HIGHGUI
        cv::imshow("YOLO Camera", display_img);
        int key = cv::waitKey(1);
        if (key == 27 || key == 'q' || key == 'Q') {
          g_running = 0;
        }
#endif

        if (network_thread_started) {
          cv::Mat stream_img;
          if (display_img.cols != NETWORK_STREAM_WIDTH || display_img.rows != NETWORK_STREAM_HEIGHT) {
            cv::resize(display_img, stream_img, cv::Size(NETWORK_STREAM_WIDTH, NETWORK_STREAM_HEIGHT));
          } else {
            stream_img = display_img;
          }
          static uint64_t last_stream_ms = 0;
          uint64_t        now_ms         = get_monotonic_ms();
          if (NETWORK_MAX_FPS > 0 && last_stream_ms != 0 &&
              (now_ms - last_stream_ms) < (uint64_t)(1000 / NETWORK_MAX_FPS)) {
            // throttle network streaming to avoid backlog/latency
          } else {
            last_stream_ms = now_ms;
            FramePacket packet;
            if (prepare_frame_packet(stream_img, &detect_result_group, fps_value, &packet, &control_serial.shared)) {
              network_queue.push(std::move(packet));
            }
          }
        }

        rknn_outputs_release(ctx, io_num.n_output, outputs);
        camera_queue(&camera_ctx, &buf);
      }
    }
  }

  if (use_camera) {
#ifdef USE_OPENCV_HIGHGUI
    cv::destroyWindow("YOLO Camera");
#endif
    camera_deinit(&camera_ctx);
  }
  if (control_serial.started) {
    control_serial.stop_requested.store(true);
    if (control_serial.worker.joinable()) {
      control_serial.worker.join();
    }
  }
  if (network_thread_started) {
    network_queue.stop();
    if (network_thread.joinable()) {
      network_thread.join();
    }
  }

  deinitPostProcess();

  // release
  ret = rknn_destroy(ctx);

  if (model_data) {
    free(model_data);
  }

  if (resize_buf) {
    free(resize_buf);
  }

  return status;
}
