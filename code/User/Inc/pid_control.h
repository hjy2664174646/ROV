#ifndef _PID_CONTROL_H
#define _PID_CONTROL_H

#ifdef __cplusplus
extern "C" {
#endif

#include "main.h"
#include <stdint.h>
#include <stdbool.h>
#include <math.h>
#include "peripheral_app.h"
	
#define YAW_TARGET_MAX_CHANGE_RATE  0.5f  // ÿ֡仯0.5 (25/)
	
typedef struct msgStruct_s msgStruct_t;
// PIDṹ
typedef struct {
    float kp;                   // ϵ
    float ki;                   // ϵ  
    float kd;                   // ΢ϵ
    
    float setpoint;             // Ŀֵ
    float input;                // ǰֵ
    float output;               // ֵ
    
    float last_input;           // ϴֵ
    float integral;             // ۻ
    float integral_max;         // ޷ֵ
    float integral_min;         // ޷Сֵ
    
    float output_max;           // ޷ֵ
    float output_min;           // ޷Сֵ
    
    uint32_t last_time;         // ϴμʱ
    float sample_time;          // ʱ()
    
    bool enabled;               // PIDʹܱ־
} pid_controller_t;

// ģʽö
typedef enum {
    CONTROL_MANUAL = 0,         // ֶģʽ
    CONTROL_DEPTH_HOLD,         // ģʽ  
    CONTROL_ATTITUDE_HOLD,      // ̬ģʽ
    CONTROL_FULL_AUTO           // ȫԶģʽ
} control_mode_t;

typedef enum {
    PID_REMOTE_YAW_OUTER = 0x01,
    PID_REMOTE_YAW_RATE  = 0x02,
    PID_REMOTE_ROLL_OUTER = 0x03,
    PID_REMOTE_ROLL_RATE  = 0x04,
    PID_REMOTE_DEPTH      = 0x05
} pid_remote_channel_t;

// PID
typedef enum {
    PID_DEPTH = 0,              // PID
    PID_YAW,                    // ƫPID
    PID_ROLL,                   // PID
    PID_COUNT                   // PID
} pid_index_t;

// ȫֱ
extern pid_controller_t g_pid_controllers[PID_COUNT];
extern control_mode_t g_current_control_mode;

// 

/**
 * @brief ʼPID
 */
void pid_init(void);

/**
 * @brief PID㺯
 * @param pid PIDָ
 * @return PIDֵ
 */
float pid_compute(pid_controller_t *pid);

/**
 * @brief PID
 * @param index PID
 * @param kp ϵ
 * @param ki ϵ  
 * @param kd ΢ϵ
 */
void pid_set_tunings(pid_index_t index, float kp, float ki, float kd);

/**
 * @brief PIDĿֵ
 * @param index PID
 * @param setpoint Ŀֵ
 */
void pid_set_setpoint(pid_index_t index, float setpoint);

/**
 * @brief PIDֵ
 * @param index PID
 * @param input ֵ
 */
void pid_set_input(pid_index_t index, float input);

/**
 * @brief ȡPIDֵ
 * @param index PID
 * @return PIDֵ
 */
float pid_get_output(pid_index_t index);

/**
 * @brief PID
 * @param index PID
 */
void pid_reset(pid_index_t index);

/**
 * @brief ʹ/PID
 * @param index PID
 * @param enabled ʹܱ־
 */
void pid_set_enabled(pid_index_t index, bool enabled);

/**
 * @brief ȿ
 * @param target_depth Ŀ()
 * @param current_depth ǰ()
 * @return ֱٶ(-5.0~5.0)
 */
float depth_control_task(float target_depth, float current_depth);

/**
 * @brief ƫǿ
 * @param target_yaw Ŀƫ()
 * @param current_yaw ǰƫ()
 * @return ƫٶ(-5.0~5.0)
 */
float yaw_control_task(float target_yaw, float current_yaw);

/**
 * @brief ǿ
 * @param target_roll Ŀ()
 * @param current_roll ǰ()
 * @return (-5.0~5.0)
 */
float roll_control_task(float target_roll, float current_roll);


/**
 * @brief ȡǰģʽ
 * @return ǰģʽ
 */
control_mode_t get_current_control_mode(void);

/**
 * @brief 񣺷ֲPID
 */
void execute_layered_control(float manual_lateral, float manual_forward, float manual_vertical, float manual_yaw_rate);

/**
 * @brief Ƕȹһ-180~180
 * @param angle Ƕ
 * @return һĽǶ
 */
float normalize_angle(float angle);

/**
 * @brief ȡǰֵ(Ӵ)
 * @return ǰ()
 */
float get_current_depth(void);

/**
 * @brief غ
 */
void set_depth_lock(bool enable, float target_depth);
bool is_depth_locked(void);

/**
 * @brief ƫǿغ  
 */
void set_yaw_target(float target_yaw);
float get_yaw_target(void);
void reset_yaw_target(void);

/**
 * @brief ̬غ
 */
void set_roll_target(float target_roll);
bool is_attitude_locked(void);
void reset_all_attitude_targets(void);
void motion_roll(float roll_value);
/**
 * @brief ģʽƺ
 */
void smart_mode_switch(bool enable_depth, bool enable_attitude);
/**
 * @brief ״̬
 * @param enabled true=, false=
 * @param target_depth Ŀ()ʱЧ
 */
void set_depth_lock_state(bool enabled, float target_depth);
void print_control_status(void);
/**
 * @brief ̬Ŀֵ
 */
void set_attitude_targets(float yaw, float roll);
/**
 * @brief ÿģʽ
 */
void set_control_mode(control_mode_t mode);
/**
 * @brief Ŀֵ
 */
void set_depth_target(float depth);
/**
 * @brief ǰ̬ΪĿ̬
 */
void lock_current_attitude(void);
/**
 * @brief Ϊˮƽ̬Ŀ
 */
void set_level_attitude(void);
/**
 * @brief Enable/disable attitude control
 */
void set_attitude_control_enabled(bool enabled);
/**
 * @brief ȡ״̬Ϣ
 */
void print_layered_control_status(void);
/**
 * @brief PID
 */
void emergency_pid_reset(void);

// ȡǰ̬ĸ
extern msgStruct_t msg;
float get_current_yaw_angle(void);
float get_current_roll_angle(void);
float get_current_pitch_angle(void);
float pid_compute_indexed(pid_index_t index);
float pid_compute_ex(pid_controller_t *pid, bool is_angle);
#ifdef __cplusplus
}
#endif

#endif 
/* _PID_CONTROL_H */
