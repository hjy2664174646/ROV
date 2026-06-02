#ifndef _Protocol_H
#define _Protocol_H

#ifdef __cplusplus
extern "C" {
#endif
#include "main.h"
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
#include "global_data.h"
#include "pid_control.h"

#define PROTO_HEAD (0xAA)	/* 鍗忚?*/
#define PROTO_TAIL (0xAA)	/* 鍗忚?*/

struct APPDEF
{
	uint8_t rbuf[MAX_FRAME_LEN];   	/* recv buffer */
	uint8_t sbuf[MAX_FRAME_LEN];		/* send buffer */
	uint8_t ReceSynFig;	 	/* get header flag */
	uint8_t reserve[3];		/* 淇濇寔4瀛楄妭瀵归綈 */
	uint16_t ReceBytes;		/* frame length */
	uint16_t rptr;			/* the index of buffer to walking through it */
	uint16_t rlen;			/* message length from header to end */
	uint16_t slen;			/* message length from header to end */
};
typedef struct  __attribute__((packed))posStruct_s
{
	int speed;				/* 閫熷害 */
	int Lon_z;					/* 缁忓害,鏁存暟 */
	int Lon_x;					/* 缁忓害,灏忔暟 */
	int Lat_z;					/* 绾害,鏁存暟 */
	int Lat_x;					/* 绾害,灏忔暟 */
	float height;					/* 娴锋嫈 */
}posStruct_t;
typedef struct  __attribute__((packed))msgStruct_s
{
	uint32_t head;				/* 甯уご */
	float timestamp;			/* 鏃堕棿?*/
	float stcGyro[3];				/* 瑙掗€熷害 */
	float stcAcc[3];				/* 鍔犻€熷害 */
	float stcMag[3];				/* 纾佸満 */
	float stcAngle[3];			/* 瑙掑害 */
	int thruster[6];			/* INS鐨勯€熷害鍜岀粡绾害 */
	posStruct_t GPS;			/* GPS鐨勯€熷害鍜岀粡绾害 */

	float gps_status;			/* SatNum.PDOP (PDOP缂╁皬100?0.01~0.99) */
	//int gps_delay;			/* GPS杈撳嚭鐩稿浜庡叾PPS寤惰繜鏃堕棿 */
	float pressure;				/* 姘村帇 */
	float temp;					/* 娓╁害 */
	float current;				/* 鐢垫祦 */
	float voltage;				/* 鐢靛帇 */
	float P30_distance;						/* P30璺濇按搴曡窛绂?*/
	float P30_confidence;						/* P30鏁版嵁缃俊搴?*/
	uint32_t checksum;			/* 鏍￠獙?*/
	uint32_t tail;				/* 甯у熬 */
}msgStruct_t;

// 鍗忚甯搁噺瀹氫箟
#define FRAME_HEAD_0        0xAA
#define FRAME_HEAD_1        0x55
#define FRAME_HEAD_2        0xAA
#define FRAME_HEAD_3        0x56
#define PID_FRAME_CODE      0xF0
#define PID_FRAME_SIGNATURE 0xA5
#define FRAME_TAIL_0        0xAA
#define FRAME_TAIL_1        0x57
#define FRAME_TAIL_2        0xAA
#define FRAME_TAIL_3        0x58

#define PROTOCOL_FRAME_SIZE 29      // 鍥哄畾29瀛楄妭
#define JOYSTICK_CENTER_X   32768     // X杞存憞鏉嗕腑浣?
#define JOYSTICK_CENTER_Y   32767     // Y杞存憞鏉嗕腑浣?

// 鎸夐敭瀹氫箟
typedef enum {
    KEY_NONE = 0x00,               // 无按键按下
    KEY_ARM_ROTATE_LEFT = 0x01,   // 机械臂左旋
    KEY_ARM_ROTATE_RIGHT = 0x02,  // 机械臂右旋
    KEY_ARM_GRAB = 0x03,          // 机械臂抓取
    KEY_ARM_RELEASE = 0x04,       // 机械臂释放
    KEY_CAMERA_UP = 0x05,         // 相机上转
    KEY_CAMERA_DOWN = 0x06,       // 相机下转
    KEY_CAMERA_LEFT = 0x07,       // 相机左转
    KEY_CAMERA_RIGHT = 0x08       // 相机右转
} remote_key_t;

// 閬ユ帶鍗忚鏁版嵁缁撴瀯
typedef struct __attribute__((packed)) {
    uint8_t frame_head[4];      // 甯уご 0xAA 0x55 0xAA 0x56
    uint8_t remote_key;         // 閬ユ帶鎵嬫焺鎸夐敭
    uint8_t auto_stable;        // 淇濈暀浣嶏紙鑷ǔ宸茬Щ闄わ級
    uint8_t auto_lock;          // 鑷攣寮€?
    uint16_t lateral_move;      // 宸﹀彸妯Щ (宸︽憞鏉嗘按?
    uint16_t forward_move;     // 鍓嶅悗绉诲姩 (宸︽憞鏉嗗瀭?
    uint16_t yaw_control;       // 宸﹀彸鍋忕Щ/鍋忚埅?(鍙虫憞鏉嗘按?
    uint16_t depth_control;     // 涓婃诞涓嬫綔/娣卞害鎺у埗 (鍙虫憞鏉嗗瀭?
    uint8_t auto_depth_switch;  // 娼滆鎸囧畾姘存繁寮€?
    float target_depth;         // 鎸囧畾姘存繁
    uint8_t light_brightness;   // 鐓ф槑鐏寒?
    uint32_t checksum;          // 鏍￠獙?
    uint8_t frame_tail[4];      // 甯у熬 0xAA 0x57 0xAA 0x58
} remote_control_frame_t;

// 鎺у埗鐘舵€佺粨?
typedef struct {
    // 杩愬姩鎺у埗 (閫熷害鑼冨洿?5.0 ~ +5.0)
    float forward_speed;        // 鍓嶈繘閫熷害 -5.0 ~ +5.0
    float lateral_speed;        // 妯Щ閫熷害 -5.0 ~ +5.0
    float vertical_speed;       // 鍨傜洿閫熷害 -5.0 ~ +5.0
    float yaw_speed;           // 鍋忚埅閫熷害 -5.0 ~ +5.0
    
    // 鎺у埗妯″紡
    bool auto_stable_enabled;   // 淇濈暀锛堝缁堝叧闂級
    bool auto_lock_enabled;     // 鑷攣妯″紡
    bool auto_depth_enabled;    // 鑷姩瀹氭繁
    float target_depth_m;       // 鐩爣娣卞害
    
    // 鐩告満鎺у埗
    remote_key_t last_key;      // 涓婃鎸夐敭
    uint8_t light_level;        // 鐓ф槑鐏寒?
    bool recording;             // 褰曞儚鐘?
    
    // 鐘舵€佹爣?
    bool data_valid;            // 鏁版嵁鏈夋晥鏍囧織
    uint32_t last_update_time;  // 鏈€鍚庢洿鏂版椂?
    uint32_t timeout_count;     // 瓒呮椂璁℃暟
} control_state_t;

// 鍗忚瑙ｆ瀽鐘?
typedef struct {
    uint8_t rx_buffer[64];      // 鎺ユ敹缂撳啿?
    uint16_t rx_index;          // 鎺ユ敹绱㈠紩
    bool frame_sync;            // 甯у悓姝ユ爣?
    uint32_t frame_count;       // 甯ц?
    uint32_t error_count;       // 閿欒璁℃暟
} protocol_parser_t;

// 娣诲姞鎸夐敭鐘舵€佺粨?
typedef struct {
    bool button_pressed;           // 鎸夐敭褰撳墠鐘?
    uint32_t press_start_time;     // 鎸夐敭鎸変笅鏃堕棿
    bool long_press_detected;      // 闀挎寜宸叉娴嬫爣?
    bool system_active;            // 绯荤粺婵€娲荤姸鎬侊紙true=寮€鏈猴紝false=寰呮満?
    uint32_t last_button_state;    // 涓婁竴娆℃寜閿姸?
} button_state_t;


// 鍏ㄥ眬鍙橀噺澹版槑
extern control_state_t g_control_state;
extern protocol_parser_t g_protocol_parser;
extern button_state_t g_power_button_state;
// 鍑芥暟澹版槑

/**
 * @brief 鍒濆鍖栧崗璁В鏋愬櫒
 */
void protocol_init(void);

/**
 * @brief 鎽囨潌鍊艰浆鎹负閫熷害
 * @param joystick_value 鎽囨潌?(0-255)
 * @param is_y_axis 鏄惁涓篩?(true=Y? false=X?
 * @return 閫熷害?(-5.0 ~ +5.0)
 */
float joystick_to_speed(uint16_t joystick_value, bool is_y_axis);

/**
 * @brief 璁＄畻鏍￠獙?
 * @param data 鏁版嵁鎸囬拡
 * @param length 鏁版嵁闀垮害
 * @return 鏍￠獙?
 */
uint32_t calculate_checksum(const uint8_t *data, uint16_t length);

/**
 * @brief 楠岃瘉鏁版嵁?
 * @param frame 鏁版嵁甯ф寚?
 * @return true=鏈夋晥, false=鏃犳晥
 */
bool validate_frame(const remote_control_frame_t *frame);

/**
 * @brief 瑙ｆ瀽鎺у埗?
 * @param frame 鎺ユ敹鍒扮殑鎺у埗?
 * @param state 鎺у埗鐘舵€佺粨?
 */
void parse_control_frame(const remote_control_frame_t *frame, control_state_t *state);

/**
 * @brief 澶勭悊鎺ユ敹鏁版嵁
 * @param data 鎺ユ敹鏁版嵁
 * @param length 鏁版嵁闀垮害
 */
void process_received_data(const uint8_t *data, uint16_t length);

/**
 * @brief 鎵ц杩愬姩鎺у埗
 * @param state 鎺у埗鐘?
 */
void execute_motion_control(const control_state_t *state);

/**
 * @brief 澶勭悊鐩告満鎺у埗
 * @param key 鎸夐敭?
 */
void handle_camera_control(remote_key_t key);

/**
 * @brief 鎺у埗鐓ф槑?
 * @param brightness 浜害绾у埆 (0-10)
 */
void control_lighting(uint8_t brightness);

/**
 * @brief 妫€鏌ユ帶鍒惰秴?
 */
void check_control_timeout(void);

/**
 * @brief 鎵撳嵃璋冭瘯淇℃伅
 */
void print_debug_info(const control_state_t *state);
/**
 * @brief 鍔熺巼璋冭妭鍛戒护澶勭悊
 */
void msg_task(void *argument);
void remote_control_task(void *argument);
bool process_power_button(uint8_t button_state);
void check_button_timeout(void);
#ifdef __cplusplus
}
#endif

#endif

