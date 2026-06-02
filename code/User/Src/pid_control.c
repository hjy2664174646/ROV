/**
 * @file pid_control.c  
 * @brief ROV PID控制模块实现
 */

#include "pid_control_api.h"
#include "protocol.h"
#include "rov.h"

// 全局变量定义
pid_controller_t g_pid_controllers[PID_COUNT];
control_mode_t g_current_control_mode = CONTROL_MANUAL;
// 推进器输出值数组
extern rov_motion_t g_rov_motion;  
// 外部传感器数据
extern struct SAngle stcAngle;   // 角度数据
extern struct SPress stcPress;  // 气压/深度数据
extern struct SAcc stcAcc;      // 加速度数据
extern struct SGyro stcGyro;    // 陀螺仪数据
extern msgStruct_t msg;
// 控制状态标志
bool g_attitude_control_always_on = false;   // 姿态控制始终开启
bool g_depth_lock_enabled = false;          // 深度锁定状态
static pid_controller_t g_yaw_rate_pid = {0};
static pid_controller_t g_roll_rate_pid = {0};

static void pid_reset_instance(pid_controller_t *pid);
static void sync_yaw_loop(float current_yaw, float current_rate);
static void sync_roll_loop(float current_roll, float current_rate);
static float run_yaw_cascade(float current_yaw, float current_rate);
static float run_roll_cascade(float current_roll, float current_rate);
static float get_current_yaw_rate(void);
static float get_current_roll_rate(void);

static void apply_remote_pid_values(pid_controller_t *pid, float kp, float ki, float kd, const char *label)
{
    if (!pid) {
        return;
    }
    pid->kp = kp;
    pid->ki = ki;
    pid->kd = kd;
    pid_reset_instance(pid);
    if (label != NULL) {
        printf("[PID UPDATE] %s -> KP=%.4f KI=%.4f KD=%.4f\r\n", label, kp, ki, kd);
    }
}
/**
 * @brief 初始化PID控制器
 */
void pid_init(void)
{
    // 深度PID参数 (根据实际调试调整)
    g_pid_controllers[PID_DEPTH] = (pid_controller_t){
        .kp = -5.0f,                 // 比例系数
        .ki = -0.02f,               // 积分系数（补偿静差）
        .kd = 0.0f,                 // 微分系数
        .setpoint = 0.0f,           // 目标深度
        .input = 0.0f,              // 当前深度
        .output = 0.0f,             // 输出
        .last_input = 0.0f,         // 上次输入
        .integral = 0.0f,           // 积分累积
        .integral_max = 50.0f,      // 积分限幅
        .integral_min = -50.0f,
        .output_max = 1.0f,         // 输出限幅
        .output_min = -1.0f,
        .sample_time = 0.02f,       // 50Hz采样
        .enabled = false
    };
    
    // 偏航角PID参数
    g_pid_controllers[PID_YAW] = (pid_controller_t){
        .kp = 1.2f,                // 外环比例：角度差 -> 角速度目标
        .ki = 0.05f,               // 积分系数
        .kd = 0.0f,                // 微分系数
        .setpoint = 0.0f,           // 目标偏航角
        .input = 0.0f,              // 当前偏航角
        .output = 0.0f,
        .last_input = 0.0f,
        .integral = 0.0f,
        .integral_max = 200.0f,      // 积分限幅
        .integral_min = -200.0f,
        .output_max = 25.0f,         // 角速度目标限幅
        .output_min = -25.0f,
        .sample_time = 0.02f,
        .enabled = false
    };
    
    // 横滚角PID参数
    g_pid_controllers[PID_ROLL] = (pid_controller_t){
        .kp = 0.8f,                 // 比例系数
        .ki = 0.02f,                // 积分系数
        .kd = 0.0f,                 // 微分系数
        .setpoint = 0.0f,           // 目标横滚角
        .input = 0.0f,              // 当前横滚角
        .output = 0.0f,
        .last_input = 0.0f,
        .integral = 0.0f,
        .integral_max = 100.0f,      // 积分限幅
        .integral_min = -100.0f,
        .output_max = 20.0f,         // 角速度目标限幅
        .output_min = -20.0f,
        .sample_time = 0.02f,
        .enabled = false
    };
    
    g_yaw_rate_pid = (pid_controller_t){
        .kp = 0.04f,
        .ki = 0.002f,
        .kd = 0.0005f,
        .setpoint = 0.0f,
        .input = 0.0f,
        .output = 0.0f,
        .last_input = 0.0f,
        .integral = 0.0f,
        .integral_max = 50.0f,
        .integral_min = -50.0f,
        .output_max = 4.0f,
        .output_min = -4.0f,
        .sample_time = 0.02f,
        .enabled = false
    };
    
    g_roll_rate_pid = (pid_controller_t){
        .kp = 0.035f,
        .ki = 0.0015f,
        .kd = 0.0003f,
        .setpoint = 0.0f,
        .input = 0.0f,
        .output = 0.0f,
        .last_input = 0.0f,
        .integral = 0.0f,
        .integral_max = 40.0f,
        .integral_min = -40.0f,
        .output_max = 3.0f,
        .output_min = -3.0f,
        .sample_time = 0.02f,
        .enabled = false
    };
    
		printf("PID Controllers Initialized for 6-Thruster ROV:\r\n");
		printf("  ✅ Depth Control: LEFT_MID + RIGHT_MID (Vertical)\r\n");
		printf("  ✅ Yaw Control: cascaded (angle -> rate -> thrusters)\r\n");
		printf("  ✅ Roll Control: cascaded (angle -> rate -> thrusters)\r\n");
		printf("  ❌ Pitch Control: NOT AVAILABLE (Hardware Limitation)\r\n");
}

/**
 * @brief 推进器死区补偿函数
 * @param pid_output PID原始输出 (-5.0 ~ +5.0)
 * @return 补偿后的输出值
 * 
 * @note 推进器死区特性：
 *       - 占空比 ±10% 内无法启动
 *       - 对应PWM counter值：60 ± 2 (即58~62范围内推进器不转)
 *       - 对应速度值约：±0.5
 */
float compensate_deadzone(float pid_output)
{
    // ✅ 死区参数（所有轴通用）
    const float DEADZONE_THRESHOLD = 0.5f;    // 10%死区 = 0.1 × 5.0
    const float MIN_EFFECTIVE_OUTPUT = 0.5f;  // 最小有效输出
    
    // 极小输出直接返回0
    if (fabs(pid_output) < 0.01f) {
        return 0.0f;
    }
    
    // 输出在死区内，补偿到最小有效值
    if (fabs(pid_output) < DEADZONE_THRESHOLD) {
        return (pid_output > 0) ? MIN_EFFECTIVE_OUTPUT : -MIN_EFFECTIVE_OUTPUT;
    }
    
    // 输出在死区外，线性补偿
    if (pid_output > 0) {
        return pid_output + (MIN_EFFECTIVE_OUTPUT - DEADZONE_THRESHOLD);
    } else {
        return pid_output - (MIN_EFFECTIVE_OUTPUT - DEADZONE_THRESHOLD);
    }
}

static void pid_reset_instance(pid_controller_t *pid)
{
    if (!pid) {
        return;
    }
    pid->integral = 0.0f;
    pid->last_input = pid->input;
    pid->output = 0.0f;
    pid->last_time = HAL_GetTick();
}

/**
 * @brief 改进的PID计算函数 - 支持角度环绕处理
 * @param pid PID控制器指针
 * @param is_angle 是否为角度控制（true=角度，false=普通值）
 * @return PID输出值
 */
float pid_compute_ex(pid_controller_t *pid, bool is_angle)
{
    if (!pid->enabled) {
        return 0.0f;
    }
    
    uint32_t now = HAL_GetTick();
    float dt = (now - pid->last_time) / 1000.0f;
    
    if (dt >= pid->sample_time) {
        float error = pid->setpoint - pid->input;
        
        // ✅ 关键修复：角度误差归一化到-180~180度
        if (is_angle) {
            while (error > 180.0f) {
                error -= 360.0f;
            }
            while (error < -180.0f) {
                error += 360.0f;
            }
        }
        
        // 抗积分饱和：仅在输出未饱和时累积
        float tentative_output = pid->kp * error + pid->ki * pid->integral;
        bool output_saturated = (tentative_output > pid->output_max) || 
                                 (tentative_output < pid->output_min);
        
        if (!output_saturated) {
            pid->integral += error * dt;
            
            // 积分限幅
            if (pid->integral > pid->integral_max) pid->integral = pid->integral_max;
            else if (pid->integral < pid->integral_min) pid->integral = pid->integral_min;
        }
        
        // ✅ 修复微分项的角度跳变问题
        float derivative;
        if (is_angle) {
            // 计算输入变化（角度差分）
            float input_delta = pid->input - pid->last_input;
            
            // 归一化角度差分（处理跨越±180°的情况）
            while (input_delta > 180.0f) {
                input_delta -= 360.0f;
            }
            while (input_delta < -180.0f) {
                input_delta += 360.0f;
            }
            
            derivative = -input_delta / dt;  // 注意符号：输入增大，微分项应减小输出
        } else {
            // 普通值的微分计算
            derivative = (pid->last_input - pid->input) / dt;
        }
        
        // PID输出计算
        pid->output = pid->kp * error + pid->ki * pid->integral + pid->kd * derivative;
        
        // 输出限幅
        if (pid->output > pid->output_max) pid->output = pid->output_max;
        else if (pid->output < pid->output_min) pid->output = pid->output_min;
        
        pid->last_input = pid->input;
        pid->last_time = now;
    }
    
    return pid->output;
}
/**
 * @brief 带索引的PID计算（推荐使用这个）
 */
float pid_compute_indexed(pid_index_t index)
{
    if (index >= PID_COUNT) {
        return 0.0f;
    }
    
    bool is_angle = (index == PID_YAW || index == PID_ROLL);
    return pid_compute_ex(&g_pid_controllers[index], is_angle);
}
/**
 * @brief 设置PID参数
 */
void pid_set_tunings(pid_index_t index, float kp, float ki, float kd)
{
    if (index < PID_COUNT) {
        g_pid_controllers[index].kp = kp;
        g_pid_controllers[index].ki = ki;
        g_pid_controllers[index].kd = kd;
    }
}

/**
 * @brief 设置PID目标值
 */
void pid_set_setpoint(pid_index_t index, float setpoint)
{
    if (index < PID_COUNT) {
        g_pid_controllers[index].setpoint = setpoint;
    }
}

/**
 * @brief 设置PID输入值
 */
void pid_set_input(pid_index_t index, float input)
{
    if (index < PID_COUNT) {
        g_pid_controllers[index].input = input;
    }
}

/**
 * @brief 获取PID输出值
 */
float pid_get_output(pid_index_t index)
{
    if (index < PID_COUNT) {
        return g_pid_controllers[index].output;
    }
    return 0.0f;
}

/**
 * @brief 重置PID控制器
 */
void pid_reset(pid_index_t index)
{
    if (index < PID_COUNT) {
        pid_reset_instance(&g_pid_controllers[index]);
    }
}

/**
 * @brief 使能/禁用PID控制器
 */
void pid_set_enabled(pid_index_t index, bool enabled)
{
    if (index < PID_COUNT) {
        g_pid_controllers[index].enabled = enabled;
        if (enabled) {
            pid_reset(index); // 使能时重置PID状态
        } else {
            g_pid_controllers[index].output = 0.0f;
        }
        
        if (index == PID_YAW) {
            g_yaw_rate_pid.enabled = enabled;
            if (enabled) {
                pid_reset_instance(&g_yaw_rate_pid);
            } else {
                g_yaw_rate_pid.output = 0.0f;
            }
        } else if (index == PID_ROLL) {
            g_roll_rate_pid.enabled = enabled;
            if (enabled) {
                pid_reset_instance(&g_roll_rate_pid);
            } else {
                g_roll_rate_pid.output = 0.0f;
            }
        }
    }
}

/**
 * @brief 角度归一化到-180~180度
 */
float normalize_angle(float angle)
{
    while (angle > 180.0f) {
        angle -= 360.0f;
    }
    while (angle < -180.0f) {
        angle += 360.0f;
    }
    return angle;
}

/**
 * @brief 获取当前深度值
 */
float get_current_depth(void)
{
    const float SEA_LEVEL_PRESSURE = 101325.0f;  // Pa (标准大气压)
    const float WATER_DENSITY = 1025.0f;          // kg/m³
    const float GRAVITY = 9.80665f;               // m/s²
    
    // 压力传感器需要校准偏移量
    float depth = (msg.pressure - SEA_LEVEL_PRESSURE) / (WATER_DENSITY * GRAVITY);
    
    return (depth > 0.0f) ? depth : 0.0f;
}


/**
 * @brief 设置偏航角目标值
 * @param target_yaw 目标偏航角(度)
 */
void set_yaw_target(float target_yaw)
{
    // 角度归一化到-180~180度范围
    target_yaw = normalize_angle(target_yaw);
    
    // 设置偏航PID目标值
    pid_set_setpoint(PID_YAW, target_yaw);
    
    // 如果当前不在姿态保持模式，自动启用
    if (g_current_control_mode == CONTROL_MANUAL) {
        set_control_mode(CONTROL_ATTITUDE_HOLD);
    } else if (g_current_control_mode == CONTROL_DEPTH_HOLD) {
        set_control_mode(CONTROL_FULL_AUTO);
    }
    
    // 确保偏航PID控制器启用
    pid_set_enabled(PID_YAW, true);
    
    printf("Yaw target set to: %.1f degrees\r\n", target_yaw);
}
// 添加偏航角重置功能 (比如按键触发)
void reset_yaw_target(void)
{
    float current_yaw = get_current_yaw_angle();
    set_yaw_target(current_yaw);
    printf("Yaw target reset to current angle: %.1f°\r\n", current_yaw);
}
/**
 * @brief 获取当前偏航目标值
 * @return 当前偏航目标角度(度)
 */
float get_yaw_target(void)
{
    return g_pid_controllers[PID_YAW].setpoint;
}

/**
 * @brief 检查深度是否处于锁定状态
 * @return true=深度锁定, false=深度未锁定
 */
bool is_depth_locked(void)
{
    return g_pid_controllers[PID_DEPTH].enabled;
}

/**
 * @brief 检查姿态是否处于锁定状态
 * @return true=姿态锁定, false=姿态未锁定
 */
bool is_attitude_locked(void)
{
    return (g_pid_controllers[PID_YAW].enabled || g_pid_controllers[PID_ROLL].enabled );
}

/**
 * @brief 设置横滚角目标值
 * @param target_roll 目标横滚角(度)
 */
void set_roll_target(float target_roll)
{
    target_roll = normalize_angle(target_roll);
    pid_set_setpoint(PID_ROLL, target_roll);
    
    if (!g_pid_controllers[PID_ROLL].enabled) {
        pid_set_enabled(PID_ROLL, true);
    }
    
    printf("Roll target set to: %.1f degrees\r\n", target_roll);
}


/**
 * @brief 设置深度锁定状态
 */
void set_depth_lock_state(bool enabled, float target_depth)
{
    g_depth_lock_enabled = enabled;
    
    if (enabled) 
		{
        g_pid_controllers[PID_DEPTH].enabled = true;
        g_pid_controllers[PID_DEPTH].setpoint = target_depth;
        pid_reset(PID_DEPTH);
        printf("DEPTH LOCKED at %.2fm - PID control active\r\n", target_depth);
    } 
		else 
		{
        g_pid_controllers[PID_DEPTH].enabled = false;
        g_pid_controllers[PID_DEPTH].output = 0.0f;
        printf("DEPTH UNLOCKED - Manual control active\r\n");
    }
}

/**
 * @brief 锁定当前姿态为目标姿态
 */
void lock_current_attitude(void)
{
    float current_yaw = get_current_yaw_angle();
		float current_roll = get_current_roll_angle();
    float current_yaw_rate = get_current_yaw_rate();
    float current_roll_rate = get_current_roll_rate();
    
    set_attitude_targets(current_yaw, current_roll);
    sync_yaw_loop(current_yaw, current_yaw_rate);
    sync_roll_loop(current_roll, current_roll_rate);
    printf("Current attitude locked: Yaw=%.1f°, Roll=%.1f°\r\n", current_yaw, current_roll);
}

/**
 * @brief 重置为水平姿态目标
 */
void set_level_attitude(void)
{
    set_attitude_targets(g_pid_controllers[PID_YAW].setpoint, 0.0f); // 保持当前偏航，横滚归零
    printf("Level attitude set (Roll=0°)\r\n");
}

/**
 * @brief 主控制任务：分层PID控制
 */
/**
 * @brief Enable/disable attitude control
 */
void set_attitude_control_enabled(bool enabled)
{
    if (g_attitude_control_always_on == enabled) {
        return;
    }
    g_attitude_control_always_on = enabled;

    if (enabled) {
        lock_current_attitude();
        pid_set_enabled(PID_YAW, true);
        pid_set_enabled(PID_ROLL, true);
    } else {
        pid_set_enabled(PID_YAW, false);
        pid_set_enabled(PID_ROLL, false);
        g_yaw_rate_pid.enabled = false;
        g_roll_rate_pid.enabled = false;
        g_yaw_rate_pid.output = 0.0f;
        g_roll_rate_pid.output = 0.0f;
    }
}

void execute_layered_control(float manual_lateral, float manual_forward, 
                             float manual_vertical, float manual_yaw_rate)
{
    extern button_state_t g_power_button_state;
    if (!g_power_button_state.system_active) {
        clear_all_thrusters();
        return;
    }
    
    clear_all_thrusters();
    
    // 获取传感器数据
    float current_yaw = normalize_angle(get_current_yaw_angle());
    float current_roll = normalize_angle(get_current_roll_angle());
    float current_depth = get_current_depth();
    float current_yaw_rate = get_current_yaw_rate();
    float current_roll_rate = get_current_roll_rate();
    bool attitude_enabled = g_attitude_control_always_on;
    
    // 更新PID输入
    pid_set_input(PID_DEPTH, current_depth);
    
    // ========== 偏航控制 ==========
    static bool last_manual_yaw_control = false;
    float yaw_deadzone_enter = 0.3f;
    float yaw_deadzone_exit = 0.15f;
    
    bool manual_yaw_control;
    if (last_manual_yaw_control) {
        manual_yaw_control = (fabs(manual_yaw_rate) > yaw_deadzone_exit);
    } else {
        manual_yaw_control = (fabs(manual_yaw_rate) > yaw_deadzone_enter);
    }
    
    float yaw_pid_output = 0.0f;
    float roll_pid_output = 0.0f;
    
    if (manual_yaw_control) {
        // ??????
        yaw_pid_output = manual_yaw_rate * 0.5f;
        if (attitude_enabled) {
            sync_yaw_loop(current_yaw, current_yaw_rate);
        }
    } else if (attitude_enabled) {
        // ? ?????? - ??????
        g_pid_controllers[PID_YAW].setpoint = normalize_angle(g_pid_controllers[PID_YAW].setpoint);
        
        float raw_yaw_output = run_yaw_cascade(current_yaw, current_yaw_rate);
        yaw_pid_output = compensate_deadzone(raw_yaw_output);  // ? ????
        
        #if 1  // ????
        static uint32_t last_yaw_debug = 0;
        uint32_t now = HAL_GetTick();
        if (now - last_yaw_debug > 1000) {
            float error = g_pid_controllers[PID_YAW].setpoint - current_yaw;
            while (error > 180.0f) error -= 360.0f;
            while (error < -180.0f) error += 360.0f;
            
            printf("[YAW PID] Tgt=%.1f? Cur=%.1f? Err=%.1f? Raw=%.2f Comp=%.2f\\r\\n",
                   g_pid_controllers[PID_YAW].setpoint, current_yaw, 
                   error, raw_yaw_output, yaw_pid_output);
            last_yaw_debug = now;
        }
        #endif
    } else {
        yaw_pid_output = 0.0f;
    }

    last_manual_yaw_control = manual_yaw_control;

    // Roll control
    if (attitude_enabled) {
        float raw_roll_output = run_roll_cascade(current_roll, current_roll_rate);
        roll_pid_output = compensate_deadzone(raw_roll_output);

        #if 0  // Roll debug
        static uint32_t last_roll_debug = 0;
        uint32_t now = HAL_GetTick();
        if (now - last_roll_debug > 2000) {
            printf("[ROLL PID] Tgt=%.1f deg Cur=%.1f deg Raw=%.2f Comp=%.2f\\r\\n",
                   g_pid_controllers[PID_ROLL].setpoint, current_roll,
                   raw_roll_output, roll_pid_output);
            last_roll_debug = now;
        }
        #endif
    } else {
        roll_pid_output = 0.0f;
    }

    float final_vertical;
    if (g_depth_lock_enabled) {
        float raw_depth_output = pid_compute_indexed(PID_DEPTH);
        final_vertical = compensate_deadzone(raw_depth_output);  // ✅ 死区补偿
        
        #if 1  // 深度调试
        static uint32_t last_depth_debug = 0;
        uint32_t now = HAL_GetTick();
        if (now - last_depth_debug > 1000) {
            float error = g_pid_controllers[PID_DEPTH].setpoint - current_depth;
            printf("[DEPTH PID] Tgt=%.2fm Cur=%.2fm Err=%.2fm Raw=%.2f Comp=%.2f\r\n",
                   g_pid_controllers[PID_DEPTH].setpoint, current_depth, 
                   error, raw_depth_output, final_vertical);
            last_depth_debug = now;
        }
        #endif
    } else {
        final_vertical = manual_vertical;
    }
    
    // ========== 准备最终输出 ==========
    float final_lateral = manual_lateral;
    float final_forward = manual_forward;
    float final_yaw = yaw_pid_output;  // 已补偿
    
    // 限制范围
    final_lateral = fmaxf(-5.0f, fminf(5.0f, final_lateral));
    final_forward = fmaxf(-5.0f, fminf(5.0f, final_forward));
    final_vertical = fmaxf(-5.0f, fminf(5.0f, final_vertical));
    final_yaw = fmaxf(-5.0f, fminf(5.0f, final_yaw));
    
    // ========== 执行运动控制 ==========
    if (fabs(final_lateral) > 0.1f) {
        if (final_lateral > 0) motion_right(final_lateral);
        else motion_left(-final_lateral);
    }
    
    if (fabs(final_vertical) > 0.1f) {
        if (final_vertical > 0) motion_down(final_vertical);
        else motion_up(-final_vertical);
    }
    
    if (fabs(final_yaw) > 0.05f) {  // ✅ 降低阈值，让补偿后的小输出也能执行
        if (final_yaw > 0) motion_turn_right(final_yaw);
        else motion_turn_left(-final_yaw);
    }
    
    if (fabs(final_forward) > 0.1f) {
        if (final_forward > 0) motion_forward(final_forward);
        else motion_backward(-final_forward);
    }
    
    // ✅ 横滚控制 - 降低阈值
    if (attitude_enabled && fabs(roll_pid_output) > 0.05f) {
        motion_roll(roll_pid_output);
    }
    
}


/**
 * @brief 获取控制状态信息
 */
void print_layered_control_status(void)
{
    printf("=== Layered Control Status ===\r\n");
    
    // 姿态控制状态（始终开启）
    float current_yaw = get_current_yaw_angle();
		float current_roll = get_current_roll_angle();
    
    printf("Attitude Control: %s\r\n", g_attitude_control_always_on ? "ON" : "OFF");
    printf("  Yaw: %.1f° -> %.1f° (rate cmd: %.2f)\r\n", 
           current_yaw, g_pid_controllers[PID_YAW].setpoint, g_yaw_rate_pid.output);
    printf("  Roll: %.1f° -> %.1f° (rate cmd: %.2f)\r\n", 
           current_roll, g_pid_controllers[PID_ROLL].setpoint, g_roll_rate_pid.output);
    
    // 深度控制状态（条件开启）
    printf("Depth Control: %s\r\n", g_depth_lock_enabled ? "LOCKED" : "MANUAL");
    if (g_depth_lock_enabled) {
        float current_depth = get_current_depth();
        printf("  Depth: %.2fm -> %.2fm (PID: %.2f)\r\n", 
               current_depth, g_pid_controllers[PID_DEPTH].setpoint, g_pid_controllers[PID_DEPTH].output);
    } else {
        printf("  Depth: Manual control via joystick\r\n");
    }
    
    printf("============================\r\n");
}

/**
 * @brief 设置控制模式
 */
void set_control_mode(control_mode_t mode)
{
    // 保存之前的模式（用于模式切换逻辑）
    control_mode_t prev_mode = g_current_control_mode;
    g_current_control_mode = mode;
    
    switch(mode) {
        case CONTROL_MANUAL: {
            // 手动模式：只保持基础姿态稳定，禁用深度锁定
            g_depth_lock_enabled = false;
            pid_set_enabled(PID_DEPTH, false);
            
            // 姿态控制保持开启但设为当前值（避免突然变化）
            if (!g_pid_controllers[PID_YAW].enabled || !g_pid_controllers[PID_ROLL].enabled) {
                lock_current_attitude();
            }
            pid_set_enabled(PID_YAW, true);
            pid_set_enabled(PID_ROLL, true);
            
            printf("Control Mode: MANUAL\r\n");
            printf("  ✅ Attitude PID: Active (current lock)\r\n");
            printf("  ❌ Depth PID: Disabled\r\n");
            break;
        }
        
        case CONTROL_DEPTH_HOLD: {
            // 深度保持模式：启用深度PID + 姿态PID
            g_depth_lock_enabled = true;
            pid_set_enabled(PID_DEPTH, true);
            
            // 如果深度目标值无效，设为当前深度
            if (g_pid_controllers[PID_DEPTH].setpoint == 0.0f) {
                float current_depth = get_current_depth();
                g_pid_controllers[PID_DEPTH].setpoint = current_depth;
            }
            
            // 确保姿态控制激活
            if (!g_pid_controllers[PID_YAW].enabled || !g_pid_controllers[PID_ROLL].enabled) {
                lock_current_attitude();
            }
            pid_set_enabled(PID_YAW, true);
            pid_set_enabled(PID_ROLL, true);
            
            printf("Control Mode: DEPTH HOLD\r\n");
            printf("  ✅ Attitude PID: Active\r\n");
            printf("  ✅ Depth PID: Active (Target: %.2fm)\r\n", g_pid_controllers[PID_DEPTH].setpoint);
            break;
        }
        
        case CONTROL_ATTITUDE_HOLD: {
            // 姿态保持模式：增强姿态控制，禁用深度锁定
            g_depth_lock_enabled = false;
            pid_set_enabled(PID_DEPTH, false);
            
            // 启用所有姿态PID并锁定当前姿态
            lock_current_attitude();
            pid_set_enabled(PID_YAW, true);
            pid_set_enabled(PID_ROLL, true);
            
            printf("Control Mode: ATTITUDE HOLD\r\n");
            printf("  ✅ Attitude PID: Enhanced (locked to current)\r\n");
            printf("  ❌ Depth PID: Disabled\r\n");
            break;
        }
        
        case CONTROL_FULL_AUTO: {
            // 全自动模式：启用所有PID控制
            g_depth_lock_enabled = true;
            pid_set_enabled(PID_DEPTH, true);
            pid_set_enabled(PID_YAW, true);
            pid_set_enabled(PID_ROLL, true);
            
            // 如果目标值无效，设为当前值
            if (g_pid_controllers[PID_DEPTH].setpoint == 0.0f) {
                float current_depth = get_current_depth();
                g_pid_controllers[PID_DEPTH].setpoint = current_depth;
            }
            if (!g_pid_controllers[PID_YAW].enabled || !g_pid_controllers[PID_ROLL].enabled) {
                lock_current_attitude();
            }
            
            printf("Control Mode: FULL AUTO\r\n");
            printf("  ✅ Attitude PID: Active\r\n");
            printf("  ✅ Depth PID: Active (Target: %.2fm)\r\n", g_pid_controllers[PID_DEPTH].setpoint);
            break;
        }
        
        default: {
            // 未知模式，回退到手动模式
            printf("Warning: Unknown control mode, falling back to MANUAL\r\n");
            set_control_mode(CONTROL_MANUAL);
            return;
        }
    }
    
    // 如果模式发生变化，重置相关PID状态避免积分饱和
    if (prev_mode != mode) {
        // 重置刚启用的PID控制器
        if (g_pid_controllers[PID_DEPTH].enabled) {
            pid_reset(PID_DEPTH);
        }
        if (g_pid_controllers[PID_YAW].enabled) {
            pid_reset(PID_YAW);
        }
        if (g_pid_controllers[PID_ROLL].enabled) {
            pid_reset(PID_ROLL);
        }
        
        printf("Mode switched from %d to %d - PID states reset\r\n", prev_mode, mode);
    }
}

/**
 * @brief 设置姿态目标值
 */
void set_attitude_targets(float yaw, float roll)
{
    // 角度归一化
    yaw = normalize_angle(yaw);
    roll = normalize_angle(roll);
    
    // 设置PID目标值
    g_pid_controllers[PID_YAW].setpoint = yaw;
    g_pid_controllers[PID_ROLL].setpoint = roll;
    
    // 确保PID控制器启用
    pid_set_enabled(PID_YAW, true);
    pid_set_enabled(PID_ROLL, true);
    
    printf("Attitude targets set: Yaw=%.1f°, Roll=%.1f°\r\n", yaw, roll);
}

/**
 * @brief 设置深度目标值
 */
void set_depth_target(float depth)
{
    if (depth < 0.0f) depth = 0.0f;
    if (depth > 20.0f) depth = 20.0f; // 安全深度限制
    
    g_pid_controllers[PID_DEPTH].setpoint = depth;
    
    // 如果当前不在深度控制模式，自动切换
    if (!g_depth_lock_enabled) {
        if (g_current_control_mode == CONTROL_MANUAL) {
            set_control_mode(CONTROL_DEPTH_HOLD);
        } else if (g_current_control_mode == CONTROL_ATTITUDE_HOLD) {
            set_control_mode(CONTROL_FULL_AUTO);
        }
    }
    
    printf("Depth target set to %.2fm\r\n", depth);
}

static float run_yaw_cascade(float current_yaw, float current_rate)
{
    pid_set_input(PID_YAW, current_yaw);
    float rate_target = pid_compute_ex(&g_pid_controllers[PID_YAW], true);
    g_yaw_rate_pid.setpoint = rate_target;
    g_yaw_rate_pid.input = current_rate;
    return pid_compute_ex(&g_yaw_rate_pid, false);
}

static float run_roll_cascade(float current_roll, float current_rate)
{
    pid_set_input(PID_ROLL, current_roll);
    float rate_target = pid_compute_ex(&g_pid_controllers[PID_ROLL], true);
    g_roll_rate_pid.setpoint = rate_target;
    g_roll_rate_pid.input = current_rate;
    return pid_compute_ex(&g_roll_rate_pid, false);
}

static void sync_yaw_loop(float current_yaw, float current_rate)
{
    float locked_yaw = normalize_angle(current_yaw);
    g_pid_controllers[PID_YAW].setpoint = locked_yaw;
    g_pid_controllers[PID_YAW].input = locked_yaw;
    pid_reset_instance(&g_pid_controllers[PID_YAW]);
    g_yaw_rate_pid.setpoint = 0.0f;
    g_yaw_rate_pid.input = current_rate;
    pid_reset_instance(&g_yaw_rate_pid);
}

static void sync_roll_loop(float current_roll, float current_rate)
{
    float locked_roll = normalize_angle(current_roll);
    g_pid_controllers[PID_ROLL].setpoint = locked_roll;
    g_pid_controllers[PID_ROLL].input = locked_roll;
    pid_reset_instance(&g_pid_controllers[PID_ROLL]);
    g_roll_rate_pid.setpoint = 0.0f;
    g_roll_rate_pid.input = current_rate;
    pid_reset_instance(&g_roll_rate_pid);
}

void pid_remote_update(uint8_t channel_id, float kp, float ki, float kd)
{
    switch (channel_id) {
        case PID_REMOTE_YAW_OUTER:
            apply_remote_pid_values(&g_pid_controllers[PID_YAW], kp, ki, kd, "Yaw Outer");
            break;
        case PID_REMOTE_YAW_RATE:
            apply_remote_pid_values(&g_yaw_rate_pid, kp, ki, kd, "Yaw Rate");
            break;
        case PID_REMOTE_ROLL_OUTER:
            apply_remote_pid_values(&g_pid_controllers[PID_ROLL], kp, ki, kd, "Roll Outer");
            break;
        case PID_REMOTE_ROLL_RATE:
            apply_remote_pid_values(&g_roll_rate_pid, kp, ki, kd, "Roll Rate");
            break;
        case PID_REMOTE_DEPTH:
            apply_remote_pid_values(&g_pid_controllers[PID_DEPTH], kp, ki, kd, "Depth Hold");
            break;
        default:
            printf("[PID UPDATE] Unknown channel %u (KP=%.4f KI=%.4f KD=%.4f)\r\n", channel_id, kp, ki, kd);
            break;
    }
}

static float get_current_yaw_rate(void)
{
    return msg.stcGyro[2];
}

static float get_current_roll_rate(void)
{
    return msg.stcGyro[0];
}

float get_current_yaw_angle(void) {
    return msg.stcAngle[2];
}

float get_current_roll_angle(void) {
    return msg.stcAngle[0];
}

float get_current_pitch_angle(void) {
    return msg.stcAngle[1];
}
