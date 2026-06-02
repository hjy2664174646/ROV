#ifndef _ROV_H
#define _ROV_H

#ifdef __cplusplus
extern "C" {
#endif

#include "global_data.h"
#include <stdbool.h>
#include <stdint.h>
	
/* 功率计算说明：
 * PWM计数值与脉宽关系：counter = -0.2*duty + 60
 * - duty=0:   counter=60  (1.5ms, 停转)
 * - duty=100: counter=40  (1.0ms, 100%正转)  
 * - duty=-100:counter=80  (2.0ms, 100%反转)
 * 
 * 输出限幅由宏 THRUSTER_OUTPUT_LIMIT_DUTY 控制（0-100%）。
 */
// 推进器配置常量（使用 PWM 计数值，可通过 THRUSTER_OUTPUT_LIMIT_DUTY 限制输出百分比）
#define THRUSTER_COUNT               6
#define THRUSTER_PWM_CENTER_VALUE    60.0f  // PWM counter for neutral (1.5 ms)
#define THRUSTER_PWM_MAX_DELTA       20.0f  // Maximum center offset (1.0~2.0 ms pulse)
#define THRUSTER_OUTPUT_LIMIT_DUTY   100.0f // Duty limit percentage (0~100)
#define THRUSTER_SLEW_FWD_ACCEL_DUTY_PER_S 50.0f // +duty accel rate
#define THRUSTER_SLEW_FWD_DECEL_DUTY_PER_S 300.0f // +duty decel rate
#define THRUSTER_SLEW_REV_ACCEL_DUTY_PER_S 50.0f // -duty accel rate
#define THRUSTER_SLEW_REV_DECEL_DUTY_PER_S 300.0f // -duty decel rate
#define THRUSTER_MAX_OFFSET         ((THRUSTER_PWM_MAX_DELTA * THRUSTER_OUTPUT_LIMIT_DUTY) / 100.0f)
#define THRUSTER_MIN_VALUE          (THRUSTER_PWM_CENTER_VALUE - THRUSTER_MAX_OFFSET)
#define THRUSTER_MAX_VALUE          (THRUSTER_PWM_CENTER_VALUE + THRUSTER_MAX_OFFSET)
#define THRUSTER_CENTER_VALUE        THRUSTER_PWM_CENTER_VALUE
typedef enum {
    THRUSTER_LEFT_FRONT = 0,    // 左前推进器 (45度斜置)
    THRUSTER_LEFT_MID = 1,      // 左中推进器 (垂直)
    THRUSTER_LEFT_REAR = 2,     // 左后推进器 (45度斜置)
    THRUSTER_RIGHT_FRONT = 3,   // 右前推进器 (45度斜置)
    THRUSTER_RIGHT_MID = 4,     // 右中推进器 (垂直)
    THRUSTER_RIGHT_REAR = 5     // 右后推进器 (45度斜置)
} thruster_index_t;

// ROV运动控制结构体
typedef struct {
    float    thruster_values[THRUSTER_COUNT];  // Thruster PWM targets (float for finer control)
    bool motion_enabled;                       // 运动使能标志
    uint32_t last_update_time;                 // 最后更新时间
} rov_motion_t;	

// 函数声明
//rov任务
void StartrovTask(void *argument);
// ROV运动控制函数
void rov_motion_init(void);
void clear_all_thrusters(void);
void apply_thruster_outputs(void);

// 基础运动函数 (速度范围: -5.0 ~ +5.0)
void motion_forward(float speed);
void motion_backward(float speed);
void motion_right(float speed);
void motion_left(float speed);
void motion_up(float speed);
void motion_down(float speed);
void motion_turn_right(float speed);
void motion_turn_left(float speed);
void motion_stop(void);
void motion_roll(float roll_value);
void add_thruster_output(thruster_index_t thruster, float value);
void thruster_startup_beep(void);
void thruster_standby_beep(void);
void buzzer_init(void);
void buzzer_beep(uint16_t freq_hz, uint16_t duration_ms);
// 系统管理函数
void set_motion_enabled(bool enabled);
uint16_t get_thruster_value(thruster_index_t thruster_index);
void print_thruster_status(void);
void rov_motion_update(void);
void emergency_stop(void);
void test_all_thrusters(void);
void debug_print_thruster_outputs(void);

// Utility helpers
float duty2counter(float duty);


#ifdef __cplusplus
}
#endif

#endif
