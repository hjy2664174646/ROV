/* Includes ------------------------------------------------------------------*/
#include "protocol.h"
#include "peripheral_app.h"
#include "rov.h"
/******************************************************************************************/
// 外部变量声明
extern struct Control rov;
extern msgStruct_t msg;
extern UART_REC userUart; 
extern control_mode_t g_current_control_mode;
extern pid_controller_t g_pid_controllers[PID_COUNT];
// 协议模块全局状态
msgStruct_t msg;
uint8_t msgArr[TOTAL_WORD * 4] = {0}; 
control_state_t g_control_state = {0};
protocol_parser_t g_protocol_parser = {0};
remote_control_frame_t g_received_frame;
button_state_t g_power_button_state = {0};

#define SERVO_PWM_MIN      40U
#define SERVO_PWM_CENTER   60U
#define SERVO_PWM_MAX      80U
#define SERVO_PWM_STEP     1U
// Camera tilt (SERVO3) config for 270deg servo, mapped by pulse width:
// pulse_ms = 0.5 + (angle_deg / 270) * 2.0
// angle_deg = (pulse_ms - 0.5) * 135
// Here we use "tilt angle" around forward direction:
// tilt = 0deg -> forward, +up / -down (or invert by changing step sign below).
#define CAMERA_SERVO_TRAVEL_DEG   270.0f
#define CAMERA_PULSE_MIN_US       500U
#define CAMERA_PULSE_MAX_US       2500U
#define CAMERA_HOME_ANGLE_DEG     135.0f   // absolute servo angle for "forward" position
#define CAMERA_TILT_LIMIT_DEG     50.0f    // allowed range: [-45, +45]
#define CAMERA_TILT_STEP_DEG      2.0f

static uint16_t s_servo1_pwm = SERVO_PWM_CENTER;
static uint16_t s_servo2_pwm = SERVO_PWM_CENTER;
static uint16_t s_servo3_pwm = SERVO_PWM_CENTER;
static float s_camera_tilt_deg = 0.0f;

static uint16_t clamp_servo_pwm(int32_t pwm)
{
    if (pwm < (int32_t)SERVO_PWM_MIN) return SERVO_PWM_MIN;
    if (pwm > (int32_t)SERVO_PWM_MAX) return SERVO_PWM_MAX;
    return (uint16_t)pwm;
}

static float clamp_camera_tilt_deg(float deg)
{
    if (deg > CAMERA_TILT_LIMIT_DEG) return CAMERA_TILT_LIMIT_DEG;
    if (deg < -CAMERA_TILT_LIMIT_DEG) return -CAMERA_TILT_LIMIT_DEG;
    return deg;
}

static uint16_t camera_tilt_deg_to_pwm(float tilt_deg)
{
    float limited_tilt = clamp_camera_tilt_deg(tilt_deg);
    float abs_angle = CAMERA_HOME_ANGLE_DEG + limited_tilt;
    if (abs_angle < 0.0f) abs_angle = 0.0f;
    if (abs_angle > CAMERA_SERVO_TRAVEL_DEG) abs_angle = CAMERA_SERVO_TRAVEL_DEG;

    float pulse_us_f = CAMERA_PULSE_MIN_US +
        (abs_angle / CAMERA_SERVO_TRAVEL_DEG) * (CAMERA_PULSE_MAX_US - CAMERA_PULSE_MIN_US);
    if (pulse_us_f < 0.0f) pulse_us_f = 0.0f;

    uint32_t pulse_us = (uint32_t)(pulse_us_f + 0.5f);
    uint32_t arr = __HAL_TIM_GET_AUTORELOAD(&htim9);
    uint32_t counts = (pulse_us * (arr + 1U) + 10000U) / 20000U; // 20ms period at 50Hz
    if (counts > arr) counts = arr;
    return (uint16_t)counts;
}

static void apply_servo_pwm_outputs(void)
{
    __HAL_TIM_SET_COMPARE(&htim4, TIM_CHANNEL_4, s_servo1_pwm); // SERVO1_PWM
    __HAL_TIM_SET_COMPARE(&htim9, TIM_CHANNEL_1, s_servo2_pwm); // SERVO2_PWM
    __HAL_TIM_SET_COMPARE(&htim9, TIM_CHANNEL_2, s_servo3_pwm); // SERVO3_PWM
}

/**
 * @brief 上位机遥测发送任务，周期 50Hz
 */
void msg_task(void *argument)
{
    msg.head = 0x56AA55AA;  // 帧头
    msg.tail = 0x58AA57AA;  // 帧尾
    uint16_t i;
    uint32_t checksum = 0;

    uint8_t txBuffer[sizeof(msg)];  // 串口发送缓冲区
    
    while(1)
    {    
			// === 调试输出：发送前检查遥测字段 ===
				#if 0
        printf("Before memcpy:\n");
        printf("GPS.speed: %d\n", msg.GPS.speed);
        printf("GPS.Lon_z: %d\n", msg.GPS.Lon_z);
        printf("GPS.height: %d\n", msg.GPS.Lon_x);
				printf("GPS.height: %d\n", msg.GPS.Lat_z);
				printf("GPS.height: %d\n", msg.GPS.Lat_x);
				#endif
        // 先复制一份结构体内容到发送缓冲区
        memcpy(txBuffer, &msg, sizeof(msg));
        
        // 重新计算校验和，跳过帧头 4 字节和末尾 8 字节
        checksum = 0;
        for(i = 4; i < sizeof(msg) - 8; i++)
        {
            checksum += txBuffer[i]; 
        }  
        msg.checksum = checksum;  // 更新校验和字段
        
        // 校验和更新后再次复制，保证发送内容一致
        memcpy(txBuffer, &msg, sizeof(msg));
        
        HAL_UART_Transmit(&huart2, txBuffer, sizeof(msg), 1000);
        
        osDelay(20);
    }
}


uint16_t time_miniseconds = 0;
/**
 * @brief 遥控协议主任务，100Hz 轮询串口并分发控制命令
 */
void remote_control_task(void *argument)
{
    // === 初始化 ===
    protocol_init();
    pid_init();
    
    // 打印系统启动横幅
    printf("\r\n==================================================\r\n");
    printf("        ROV CONTROL SYSTEM STARTED\r\n");
    printf("        Status: STANDBY\r\n");
    printf("        Switch PC control to ACTIVE to start\r\n");
    printf("==================================================\r\n\r\n");
    
    // 初始状态设为待机
    g_control_state.data_valid = false;
    g_control_state.auto_lock_enabled = false;  // 默认待机
    
    // 运行统计
    uint32_t loop_count = 0;
    uint32_t error_count = 0;
    uint32_t last_status_print = HAL_GetTick();
    
    while (1) 
    {
        uint32_t current_time = HAL_GetTick();
        
        // === 1. 处理串口接收数据 ===
        if (userUart.userUart2.ReceiveNum > 0) 
        {
            // 调试输出：打印收到的原始字节流
            #if 0  // 调试开关：打印原始串口数据
            printf("RX[%d]: ", userUart.userUart2.ReceiveNum);
            for (int i = 0; i < userUart.userUart2.ReceiveNum; i++) 
            {
                printf("%02X ", userUart.userUart2.ReceiveData[i]);
            }
            printf("\r\n");
            #endif
            
            // 解析收到的数据流
            process_received_data(userUart.userUart2.ReceiveData, 
                                userUart.userUart2.ReceiveNum);
            
            // 清空接收长度计数
            userUart.userUart2.ReceiveNum = 0;
        }
        
        // === 2. 执行运动控制与外设控制 ===
        if (g_control_state.data_valid) {
            // 执行推进器控制
            execute_motion_control(&g_control_state);
            
            // Servo/camera control should remain responsive to gamepad keys.
            handle_camera_control(g_control_state.last_key);
            
            // 同步更新灯光亮度
            control_lighting(g_control_state.light_level);
        }
        
    // === 3. 检查控制信号超时 ===
        check_control_timeout();

        // === 4. 每 10 秒输出一次系统状态 ===
        if (current_time - last_status_print > 10000) {
            printf("\r\n[SYSTEM STATUS]\r\n");
            printf("  Runtime: %lu seconds\r\n", current_time / 1000);
            printf("  Loop count: %lu\r\n", loop_count);
            printf("  Error count: %lu\r\n", error_count);
            printf("  System state: %s\r\n",
                   g_control_state.auto_lock_enabled ? "ACTIVE" : "STANDBY");

            if (g_control_state.auto_lock_enabled) {
                float current_yaw = get_current_yaw_angle();
                float current_roll = get_current_roll_angle();
                float current_depth = get_current_depth();

                printf("  Current: Yaw=%.1f deg Roll=%.1f deg Depth=%.2fm\r\n",
                       current_yaw, current_roll, current_depth);
                printf("  Targets: Yaw=%.1f deg Roll=%.1f deg",
                       g_pid_controllers[PID_YAW].setpoint,
                       g_pid_controllers[PID_ROLL].setpoint);
                if (g_pid_controllers[PID_DEPTH].enabled) {
                    printf(" Depth=%.2fm", g_pid_controllers[PID_DEPTH].setpoint);
                }
                printf("\r\n");
            } else {
                printf("  All controls disabled, tracking current attitude\r\n");
            }

            printf("  Communication: %s\r\n\r\n",
                   g_control_state.data_valid ? "OK" : "LOST");

            last_status_print = current_time;
        }

        // === 5. 通信错误监测 ===
        if (g_protocol_parser.error_count > error_count + 10) {
            printf("[WARNING] Communication errors detected: %lu\r\n",
                   g_protocol_parser.error_count);
            error_count = g_protocol_parser.error_count;
        }

        // === 6. 任务周期延时 ===
        loop_count++;
        time_miniseconds += 10;
        if (time_miniseconds >= 1000) time_miniseconds = 0;
        osDelay(10);
    }
}

/**
 * @brief Get current GPS timestamp as a formatted string
 */
void get_timestamp_string(char* buffer, size_t buffer_size)
{
    if (msg.timestamp > 0) {
        float total_seconds = msg.timestamp;
        int hours = (int)(total_seconds / 3600);
        int minutes = (int)((total_seconds - hours * 3600) / 60);
        int seconds = (int)(total_seconds - hours * 3600 - minutes * 60);
        snprintf(buffer, buffer_size, "%02d:%02d:%02d:%d",
                 hours, minutes, seconds, time_miniseconds);
    } else {
        uint32_t timestamp_ms = time_miniseconds;
        snprintf(buffer, buffer_size, "%lu.%03lu s (no GPS)",
                 timestamp_ms / 1000, timestamp_ms % 1000);
    }
}

/**
 * @brief Protocol init
 */
void protocol_init(void)
{
    memset(&g_control_state, 0, sizeof(control_state_t));
    memset(&g_protocol_parser, 0, sizeof(protocol_parser_t));
    memset(&g_power_button_state, 0, sizeof(button_state_t));
    g_power_button_state.system_active = false;
    g_control_state.data_valid = false;
    g_control_state.target_depth_m = 0.0f;
    g_protocol_parser.frame_sync = false;
    s_servo1_pwm = SERVO_PWM_CENTER;
    s_servo2_pwm = SERVO_PWM_CENTER;
    s_camera_tilt_deg = 0.0f;
    s_servo3_pwm = camera_tilt_deg_to_pwm(s_camera_tilt_deg);
    apply_servo_pwm_outputs();
    buzzer_init();
    printf("Protocol initialized\r\n");
}

/**
 * @brief Convert joystick value to speed (-5.0 ~ +5.0)
 */
float joystick_to_speed(uint16_t joystick_value, bool is_y_axis)
{
    float speed;
    int32_t offset;
    uint32_t center_value;
    float max_offset;

    if (is_y_axis) {
        center_value = JOYSTICK_CENTER_Y;
        max_offset = (joystick_value > center_value) ? (65535 - center_value) : center_value;
    } else {
        center_value = JOYSTICK_CENTER_X;
        max_offset = (joystick_value > center_value) ? (65535 - center_value) : center_value;
    }

    offset = (int32_t)joystick_value - center_value;
    speed = ((float)offset / max_offset) * 5.0f;
    return speed;
}

/**
 * @brief Calculate checksum
 */
uint32_t calculate_checksum(const uint8_t *data, uint16_t length)
{
    uint32_t sum = 0;
    for (uint16_t i = 4; i < length - 8; i++) {
        sum += data[i];
    }
    return sum;
}

/**
 * @brief Validate frame
 */
bool validate_frame(const remote_control_frame_t *frame)
{
    if (frame->frame_head[0] != FRAME_HEAD_0 || frame->frame_head[1] != FRAME_HEAD_1 ||
        frame->frame_head[2] != FRAME_HEAD_2 || frame->frame_head[3] != FRAME_HEAD_3) {
        return false;
    }

    if (frame->frame_tail[0] != FRAME_TAIL_0 || frame->frame_tail[1] != FRAME_TAIL_1 ||
        frame->frame_tail[2] != FRAME_TAIL_2 || frame->frame_tail[3] != FRAME_TAIL_3) {
        return false;
    }

    uint32_t calculated_sum = calculate_checksum((const uint8_t*)frame, PROTOCOL_FRAME_SIZE);
    if (calculated_sum != frame->checksum) {
        printf("Checksum error: calc=0x%08X, recv=0x%08X\r\n",
               calculated_sum, frame->checksum);
        return false;
    }

    return true;
}

static bool handle_pid_remote_frame(const remote_control_frame_t *frame)
{
    if (frame->remote_key != PID_FRAME_CODE || frame->auto_stable != PID_FRAME_SIGNATURE) {
        return false;
    }

    const uint8_t *raw = (const uint8_t *)frame;
    float kp = 0.0f, ki = 0.0f, kd = 0.0f;
    memcpy(&kp, raw + 7, sizeof(float));
    memcpy(&ki, raw + 11, sizeof(float));
    memcpy(&kd, raw + 15, sizeof(float));
    uint8_t channel = frame->auto_lock;

    pid_remote_update(channel, kp, ki, kd);

    char timestamp_str[32];
    get_timestamp_string(timestamp_str, sizeof(timestamp_str));
    printf("[PID REMOTE] Channel=%u KP=%.4f KI=%.4f KD=%.4f @ %s\r\n", channel, kp, ki, kd, timestamp_str);
    return true;
}

/**
 * @brief Parse control frame
 */
void parse_control_frame(const remote_control_frame_t *frame, control_state_t *state)
{
    char timestamp_str[32];
    get_timestamp_string(timestamp_str, sizeof(timestamp_str));

    if (handle_pid_remote_frame(frame)) {
        return;
    }

    // 1. Joystick values
    uint16_t lateral = frame->lateral_move;
    uint16_t vertical = frame->forward_move;
    uint16_t yaw = frame->yaw_control;
    uint16_t depth = frame->depth_control;

    state->lateral_speed = joystick_to_speed(lateral, 0);
    state->forward_speed = -joystick_to_speed(vertical, 1);
    state->yaw_speed = joystick_to_speed(yaw, 0);
    state->vertical_speed = -joystick_to_speed(depth, 1);

    // 2. Depth control
    static uint8_t last_depth_switch = 0xFF;
    if (frame->auto_depth_switch != last_depth_switch && last_depth_switch != 0xFF) {
        printf("[DEPTH_CONTROL] Mode changed: %s to %s at GPS time: %s",
               last_depth_switch ? "AUTO" : "MANUAL",
               frame->auto_depth_switch ? "AUTO" : "MANUAL",
               timestamp_str);
        if (frame->auto_depth_switch) {
            printf(" (Target: %.2fm)", frame->target_depth);
        }
        printf("\r\n");
    }
    last_depth_switch = frame->auto_depth_switch;

    if (frame->auto_depth_switch) {
        state->auto_depth_enabled = true;
        state->target_depth_m = frame->target_depth;
        if (state->target_depth_m < 0.0f) state->target_depth_m = 0.0f;
        if (state->target_depth_m > 20.0f) state->target_depth_m = 20.0f;
    } else {
        state->auto_depth_enabled = false;
    }

    // 3. Power switch from host (direct state)
    static uint8_t last_power_switch = 0xFF;
    uint8_t requested_state = frame->auto_lock ? 0x01 : 0x00;

    if (last_power_switch != 0xFF && requested_state != last_power_switch) {
        printf("[POWER] State change: %s -> %s at GPS time: %s\r\n",
               last_power_switch ? "ACTIVE" : "STANDBY",
               requested_state ? "ACTIVE" : "STANDBY",
               timestamp_str);
        printf("[POWER] auto_lock raw=%u\r\n", frame->auto_lock);
        if (requested_state) {
            buzzer_beep(1800, 80);
            osDelay(50);
            buzzer_beep(2500, 140);
        } else {
            buzzer_beep(1500, 120);
            osDelay(60);
            buzzer_beep(900, 180);
        }
    }

    last_power_switch = requested_state;
    g_power_button_state.system_active = (requested_state == 0x01);
    g_power_button_state.button_pressed = false;
    g_power_button_state.long_press_detected = false;
    g_power_button_state.press_start_time = 0;
    g_power_button_state.last_button_state = requested_state;

    state->auto_lock_enabled = g_power_button_state.system_active;

    // 4. Other control flags (auto-stable removed)
    state->auto_stable_enabled = false;

    // 5. Camera and lighting
    state->last_key = (remote_key_t)frame->remote_key;
    {
        static uint8_t last_remote_key = 0xFF;
        if (frame->remote_key != last_remote_key) {
            printf("[KEY] remote_key=0x%02X\r\n", frame->remote_key);
            last_remote_key = frame->remote_key;
        }
    }

    static uint8_t last_light_level = 0xFF;
    if (frame->light_brightness != last_light_level && last_light_level != 0xFF) {
        printf("[LIGHT] Brightness changed: %d to %d at GPS time: %s\r\n",
               last_light_level, frame->light_brightness, timestamp_str);
    }
    last_light_level = frame->light_brightness;
    state->light_level = frame->light_brightness;

    // 6. Update state timestamp
    state->data_valid = true;
    state->last_update_time = HAL_GetTick();
}

/**
 * @brief Process received data
 */
void process_received_data(const uint8_t *data, uint16_t length)
{
    protocol_parser_t *parser = &g_protocol_parser;
    
    for (uint16_t i = 0; i < length; i++) 
		{
        if (!parser->frame_sync) 
				{
            // 逐字节查找帧头
            parser->rx_buffer[3] = data[i];
            
            if (parser->rx_buffer[0] == FRAME_HEAD_0 && parser->rx_buffer[1] == FRAME_HEAD_1 &&
                parser->rx_buffer[2] == FRAME_HEAD_2 && parser->rx_buffer[3] == FRAME_HEAD_3) 
						{
                parser->frame_sync = true;
                parser->rx_index = 4;
								#if 0
                printf("Frame sync found\r\n");
								#endif
            } 
						else 
						{
                // 未命中帧头，滑动窗口继续搜索
                memmove(parser->rx_buffer, &parser->rx_buffer[1], 3);
            }
        } 
				else 
				{
            // 已同步后继续接收整帧
            if (parser->rx_index < sizeof(parser->rx_buffer)) 
						{
                parser->rx_buffer[parser->rx_index++] = data[i];
                
                // 收满一帧后进行校验和解析
                if (parser->rx_index >= PROTOCOL_FRAME_SIZE) 
								{
                    memcpy(&g_received_frame, parser->rx_buffer, sizeof(remote_control_frame_t));
                    
                    if (validate_frame(&g_received_frame)) 
										{
                        parse_control_frame(&g_received_frame, &g_control_state);
                        parser->frame_count++;
                    } 
										else 
										{
                        parser->error_count++;
//                        printf("Frame validation failed\r\n");
                    }
                    
                    // 处理完成后复位解析状态机
                    parser->frame_sync = false;
                    parser->rx_index = 0;
                    memset(parser->rx_buffer, 0, sizeof(parser->rx_buffer));
                }
            } 
						else 
						{ 
                // 缓冲区越界，放弃本帧并累计错误次数
                parser->frame_sync = false;
                parser->rx_index = 0;
                parser->error_count++;
            }
        }
    }
}


/**************************************************************************************************/
/***************************            鏉╂劕濮╅幒褍鍩楅柈銊ュ瀻           *************************************/
/**************************************************************************************************/

/**
 * @brief 执行运动控制流程，包括待机/激活切换、深度锁定和手动控制分发
 */
void execute_motion_control(const control_state_t *state)
{
		char timestamp_str[32];
    // 数据无效时不执行任何控制
    if (!state->data_valid) 
    {
        return;
    }
    
    // === 根据上位机电源/解锁状态判断系统是否激活 ===
    bool system_active = state->auto_lock_enabled;
    
    // 记录上一次系统状态，用于检测待机/激活切换
    static bool last_system_state = false;  // 默认待机
    static bool state_change_handled = false;
    
    if (system_active != last_system_state) 
		{
        if (!system_active) 
				{
					// === 进入待机模式 ===
					printf("\r\n==================================================\r\n");
					printf("        ENTERING STANDBY MODE\r\n");
					printf("        All controls disabled\r\n");
					printf("        Tracking current attitude\r\n");
					printf("==================================================\r\n\r\n");
					
					// 提示音在电源开关变化时已经处理
					// standby tone handled when power switch changes
				
					// 关闭全部 PID 并清空内部状态
					for (int i = 0; i < PID_COUNT; i++) 
					{
						g_pid_controllers[i].enabled = false;
						pid_reset((pid_index_t)i);
					}
					
					// 清空推进器目标
					clear_all_thrusters();
        } 
				else 
				{
					// === 进入激活模式 ===
					printf("\r\n==================================================\r\n");
					printf("        ENTERING ACTIVE MODE\r\n");
					printf("        All controls enabled\r\n");
					printf("        Initializing control systems\r\n");
					printf("==================================================\r\n\r\n");
				
					// 提示音在电源开关变化时已经处理
					// startup tone handled when power switch changes
				
					// 进入激活态时锁定当前姿态为目标值
					float current_yaw = get_current_yaw_angle();
					float current_roll = get_current_roll_angle();
					
					// 将当前姿态写入 PID 设定值
					g_pid_controllers[PID_YAW].setpoint = current_yaw;
					g_pid_controllers[PID_ROLL].setpoint = current_roll;
					
					// 使能姿态环 PID
					pid_set_enabled(PID_YAW, true);
					pid_set_enabled(PID_ROLL, true);
					
					// 如果启用了深度锁定，则把当前深度作为初始目标
					if (state->auto_depth_enabled) 
					{
							float current_depth = get_current_depth();
							g_pid_controllers[PID_DEPTH].setpoint = current_depth;
							pid_set_enabled(PID_DEPTH, true);
					}

					printf("Initial targets set: Yaw=%.1f deg Roll=%.1f deg\r\n", current_yaw, current_roll);
			}
				
			last_system_state = system_active;
			state_change_handled = true;
    }
    
    // === 待机模式：仅跟踪当前姿态和深度，不输出推进器 ===
    if (!system_active) {
        // 读取当前姿态和深度
        float current_yaw = get_current_yaw_angle();
				float current_roll = get_current_roll_angle();
        float current_depth = get_current_depth();
        
        // 持续跟踪当前状态，避免重新激活时目标值突变
        g_pid_controllers[PID_YAW].setpoint = current_yaw;
        g_pid_controllers[PID_ROLL].setpoint = current_roll;
        
        if (state->auto_depth_enabled) 
				{
            g_pid_controllers[PID_DEPTH].setpoint = current_depth;
        }
        
        // 清空推进器目标
        clear_all_thrusters();
        // 定期输出待机状态信息
        static uint32_t last_standby_print = 0;
        uint32_t current_time = HAL_GetTick();
        if (current_time - last_standby_print > 2000) {
//            printf("[STANDBY] Tracking: Yaw=%.1f deg Roll=%.1f deg Depth=%.2fm\r\n",
//                   current_yaw, current_roll, current_depth);
            last_standby_print = current_time;
        }
        
        return;  // 待机模式下不再执行后续控制
    }
    
    // === 激活模式：处理深度锁定和手动控制输入 ===
    
    // 监测深度锁定开关变化
    static bool last_auto_depth_state = false;
    if (state->auto_depth_enabled != last_auto_depth_state) {
        if (state->auto_depth_enabled) {
            printf("[DEPTH] Enabling auto depth at %.2fm at GPS time: %s\r\n", 
                   state->target_depth_m, timestamp_str);
            set_depth_lock_state(true, state->target_depth_m);
        } else {
            printf("[DEPTH] Switching to manual control at GPS time: %s\r\n", 
                   timestamp_str);
            set_depth_lock_state(false, 0.0f);
        }
        last_auto_depth_state = state->auto_depth_enabled;
    } else if (state->auto_depth_enabled) {
        // 自动深度模式下允许在线更新目标深度
        if (g_pid_controllers[PID_DEPTH].enabled && 
            fabs(g_pid_controllers[PID_DEPTH].setpoint - state->target_depth_m) > 0.1f) {
            get_timestamp_string(timestamp_str, sizeof(timestamp_str));
            g_pid_controllers[PID_DEPTH].setpoint = state->target_depth_m;
            printf("[DEPTH] Target updated to %.2fm at GPS time: %s\r\n", 
                   state->target_depth_m, timestamp_str);
        }
    }
    
    // 读取手动控制量
    float manual_lateral = state->lateral_speed;
    float manual_forward = state->forward_speed; 
    float manual_vertical = state->vertical_speed;
    float manual_yaw_rate = state->yaw_speed;
    
    // 对输入量做限幅
    manual_lateral = fmaxf(-5.0f, fminf(5.0f, manual_lateral));
    manual_forward = fmaxf(-5.0f, fminf(5.0f, manual_forward));
    manual_vertical = fmaxf(-5.0f, fminf(5.0f, manual_vertical));
    manual_yaw_rate = fmaxf(-3.0f, fminf(3.0f, manual_yaw_rate));
    
    // 执行分层控制器
    execute_layered_control(manual_lateral, manual_forward, manual_vertical, manual_yaw_rate);
    
    // 调试输出：打印当前控制输入
    #if 0  // 鐠佸彞璐?瀵偓閸氼垵鐨?
    static uint32_t last_debug_print = 0;
    uint32_t current_time = HAL_GetTick();
    if (current_time - last_debug_print > 1000) 
		{
			printf("[ACTIVE] L=%.1f F=%.1f V=%.1f Y=%.1f | ",  manual_lateral, manual_forward, manual_vertical, manual_yaw_rate);
			printf("Depth=%s(%.2fm)\r\n",
						 state->auto_depth_enabled ? "AUTO" : "MAN",
						 state->target_depth_m);
			last_debug_print = current_time;
    }
    #endif
}



/**
 * @brief 打印当前系统控制状态
 */
void print_system_control_status(void)
{
    printf("\r\n=== System Control Status ===\r\n");
    
    // 打印分层控制器状态
    print_layered_control_status();
    
    // 打印遥控协议状态
    printf("Remote Control:\r\n");
    printf("  Data Valid: %s\r\n", g_control_state.data_valid ? "YES" : "NO");
    printf("  Auto Depth: %s\r\n", g_control_state.auto_depth_enabled ? "ON" : "OFF");
    printf("  Auto Lock: %s\r\n", g_control_state.auto_lock_enabled ? "ON" : "OFF");
    
    // 打印推进器输出状态
    print_thruster_status();
    
    printf("============================\r\n");
}



/**
 * @brief 根据遥控按键控制机械臂和摄像头舵机
 */
void handle_camera_control(remote_key_t key)
{
    static uint32_t last_step_ms = 0;
    uint32_t now_ms = HAL_GetTick();
    const uint32_t step_interval_ms = 60U;

    if (key == KEY_NONE) {
        return;
    }
    if ((now_ms - last_step_ms) < step_interval_ms) {
        return;
    }
    last_step_ms = now_ms;

    switch (key) {
        case KEY_ARM_ROTATE_LEFT:
        case KEY_CAMERA_LEFT:
            s_servo1_pwm = clamp_servo_pwm((int32_t)s_servo1_pwm - SERVO_PWM_STEP);
            apply_servo_pwm_outputs();
            printf("Arm joint2 rotate left, SERVO1=%u\r\n", s_servo1_pwm);
            break;

        case KEY_ARM_ROTATE_RIGHT:
        case KEY_CAMERA_RIGHT:
            s_servo1_pwm = clamp_servo_pwm((int32_t)s_servo1_pwm + SERVO_PWM_STEP);
            apply_servo_pwm_outputs();
            printf("Arm joint2 rotate right, SERVO1=%u\r\n", s_servo1_pwm);
            break;

        case KEY_ARM_GRAB:
            s_servo2_pwm = clamp_servo_pwm((int32_t)s_servo2_pwm - SERVO_PWM_STEP);
            apply_servo_pwm_outputs();
            printf("Arm grab, SERVO2=%u\r\n", s_servo2_pwm);
            break;

        case KEY_ARM_RELEASE:
            s_servo2_pwm = clamp_servo_pwm((int32_t)s_servo2_pwm + SERVO_PWM_STEP);
            apply_servo_pwm_outputs();
            printf("Arm release, SERVO2=%u\r\n", s_servo2_pwm);
            break;

        case KEY_CAMERA_UP:
            s_camera_tilt_deg = clamp_camera_tilt_deg(s_camera_tilt_deg - CAMERA_TILT_STEP_DEG);
            s_servo3_pwm = camera_tilt_deg_to_pwm(s_camera_tilt_deg);
            apply_servo_pwm_outputs();
            printf("Camera tilt up, angle=%.1f deg, SERVO3=%u\r\n", s_camera_tilt_deg, s_servo3_pwm);
            break;

        case KEY_CAMERA_DOWN:
            s_camera_tilt_deg = clamp_camera_tilt_deg(s_camera_tilt_deg + CAMERA_TILT_STEP_DEG);
            s_servo3_pwm = camera_tilt_deg_to_pwm(s_camera_tilt_deg);
            apply_servo_pwm_outputs();
            printf("Camera tilt down, angle=%.1f deg, SERVO3=%u\r\n", s_camera_tilt_deg, s_servo3_pwm);
            break;

        default:
            break;
    }
}


/**
 * @brief Modified control_lighting function with GPS timestamp
 */
void control_lighting(uint8_t brightness)
{
    static uint8_t last_brightness = 255;
    
    if (brightness != last_brightness && brightness <= 10) {
        // 50Hz PWM dimming with full-duty range:
        // brightness 0 -> 0%, brightness 10 -> 100%
        uint32_t arr = __HAL_TIM_GET_AUTORELOAD(&htim4);
        uint16_t pwm_value = (uint16_t)((brightness * arr) / 10U);
        
        if (pwm_value > arr) {
            pwm_value = (uint16_t)arr;
        }

        // LIGHT_PWM is TIM4_CH3
        __HAL_TIM_SET_COMPARE(&htim4, TIM_CHANNEL_3, pwm_value);
        
        printf("Light brightness=%u/10, CCR=%u\r\n", brightness, pwm_value);
        last_brightness = brightness;
    }
}


/**
 * @brief 检查遥控数据是否超时
 */
void check_control_timeout(void)
{
    uint32_t current_time = HAL_GetTick();
    
    if (g_control_state.data_valid && (current_time - g_control_state.last_update_time) > 1000) 
		{
        
        // 超时后将控制状态标记为无效，由下层输出任务执行失联保护
        g_control_state.data_valid = false;
        g_control_state.timeout_count++;

        printf("Control timeout! Emergency stop.\r\n");
    }
}



/**
 * @brief 根据上位机按钮状态更新系统激活/待机状态
 * @param button_state 上位机按钮值，0 表示待机，1 表示激活
 * @return `true` 表示系统处于激活状态，`false` 表示系统处于待机状态
 */
bool process_power_button(uint8_t button_state)
{
    uint8_t requested_state = (button_state == 0x01) ? 0x01 : 0x00;

    if (requested_state != g_power_button_state.last_button_state) {
        char timestamp_str[32];
        get_timestamp_string(timestamp_str, sizeof(timestamp_str));
        printf("[POWER] PC switch: %s at GPS time: %s\r\n",
               requested_state ? "ACTIVE" : "STANDBY", timestamp_str);
    }

    g_power_button_state.system_active = (requested_state == 0x01);
    g_power_button_state.button_pressed = false;
    g_power_button_state.long_press_detected = false;
    g_power_button_state.press_start_time = 0;
    g_power_button_state.last_button_state = requested_state;

    return g_power_button_state.system_active;
}

/**
 * @brief 检查按钮状态是否异常卡住，必要时自动复位
 */
void check_button_timeout(void)
{
    if (g_power_button_state.button_pressed) {
        uint32_t current_time = HAL_GetTick();
        uint32_t press_duration = current_time - g_power_button_state.press_start_time;
        
        // 如果按钮状态持续超过 10 秒，认为状态异常，自动清除
        if (press_duration > 10000) {
            printf("WARNING: Button timeout, resetting button state\r\n");
            g_power_button_state.button_pressed = false;
            g_power_button_state.long_press_detected = false;
        }
    }
}




