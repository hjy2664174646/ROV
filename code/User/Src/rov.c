#include "rov.h"

#include "pid_control.h"

#include "protocol.h"
#include "bsp_dwt.h"
#include "main.h"

#include <math.h>



// 澶栭儴鍙橀噺澹版槑

extern msgStruct_t msg;



rov_motion_t g_rov_motion = {0};
static float g_thruster_outputs[THRUSTER_COUNT];
static uint32_t g_thruster_ramp_ms = 0;

#define THRUSTER_OUTPUT_TASK_PERIOD_MS 10U
#define CONTROL_SIGNAL_TIMEOUT_MS      1000U
#define THRUSTER_FAILSAFE_HOLD_MS      250U

static const float k_thruster_deadzone_fwd_duty[THRUSTER_COUNT] = {8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f};
static const float k_thruster_deadzone_rev_duty[THRUSTER_COUNT] = {8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f};
static uint32_t g_signal_lost_ms = 0;


static float limit_thruster_value(float value);
static inline float clampf(float value, float min_value, float max_value)
{
    if (value > max_value) return max_value;
    if (value < min_value) return min_value;
    return value;
}
static inline float signf(float value)
{
    return (value > 0.0f) ? 1.0f : (value < 0.0f ? -1.0f : 0.0f);
}

static float select_slew_rate(float current_duty, float target_duty)
{
    float current_abs = fabsf(current_duty);
    float target_abs = fabsf(target_duty);
    if (current_duty == 0.0f) {
        return (target_duty >= 0.0f) ? THRUSTER_SLEW_FWD_ACCEL_DUTY_PER_S : THRUSTER_SLEW_REV_ACCEL_DUTY_PER_S;
    }
    if (target_duty == 0.0f) {
        return (current_duty >= 0.0f) ? THRUSTER_SLEW_FWD_DECEL_DUTY_PER_S : THRUSTER_SLEW_REV_DECEL_DUTY_PER_S;
    }
    if ((current_duty > 0.0f && target_duty > 0.0f) || (current_duty < 0.0f && target_duty < 0.0f)) {
        if (target_abs > current_abs) {
            return (current_duty > 0.0f) ? THRUSTER_SLEW_FWD_ACCEL_DUTY_PER_S : THRUSTER_SLEW_REV_ACCEL_DUTY_PER_S;
        }
        return (current_duty > 0.0f) ? THRUSTER_SLEW_FWD_DECEL_DUTY_PER_S : THRUSTER_SLEW_REV_DECEL_DUTY_PER_S;
    }
    return (current_duty > 0.0f) ? THRUSTER_SLEW_FWD_DECEL_DUTY_PER_S : THRUSTER_SLEW_REV_DECEL_DUTY_PER_S;
}


static float counter_to_duty(float counter_value)
{
    if (THRUSTER_MAX_OFFSET <= 0.0f) {
        return 0.0f;
    }
    float offset = THRUSTER_PWM_CENTER_VALUE - counter_value;
    float normalized = offset / THRUSTER_MAX_OFFSET;
    return normalized * THRUSTER_OUTPUT_LIMIT_DUTY;
}

static uint16_t pwm_from_float(float value)
{
    float limited = limit_thruster_value(value);
    return (uint16_t)lroundf(limited);
}

static float apply_thruster_deadzone_compensation(uint8_t thruster_index, float duty)
{
    float max_duty = THRUSTER_OUTPUT_LIMIT_DUTY;
    if (thruster_index >= THRUSTER_COUNT || max_duty <= 0.0f) {
        return 0.0f;
    }
    if (fabsf(duty) < 0.01f) {
        return 0.0f;
    }

    float sign = signf(duty);
    float abs_duty = fabsf(duty);
    float deadzone = (sign > 0.0f) ? k_thruster_deadzone_fwd_duty[thruster_index]
                                   : k_thruster_deadzone_rev_duty[thruster_index];
    if (deadzone >= max_duty) {
        deadzone = max_duty - 0.5f;
    }
    if (deadzone < 0.0f) {
        deadzone = 0.0f;
    }

    float scaled = deadzone + (abs_duty / max_duty) * (max_duty - deadzone);
    if (scaled > max_duty) {
        scaled = max_duty;
    }
    return sign * scaled;
}

static bool should_hold_last_thruster_command(uint32_t now_ms)
{
    if (!g_rov_motion.motion_enabled) {
        g_signal_lost_ms = 0;
        return false;
    }

    bool signal_recent = g_control_state.data_valid &&
        ((now_ms - g_control_state.last_update_time) <= CONTROL_SIGNAL_TIMEOUT_MS);
    if (signal_recent) {
        g_signal_lost_ms = 0;
        return false;
    }

    if (g_control_state.last_update_time == 0U) {
        return false;
    }

    if (g_signal_lost_ms == 0U) {
        g_signal_lost_ms = now_ms;
    }

    return ((now_ms - g_signal_lost_ms) < THRUSTER_FAILSAFE_HOLD_MS);
}



/* USER CODE BEGIN Header_StartrovTask */

/**

  * @brief  Function implementing the rovTask thread.

  * @param  argument: Not used

  * @retval None

 	鍗犵┖姣旇缃弬鑰?https://blog.csdn.net/weixin_43866583/article/details/149065217

  	棰戠巼50Hz,鍛ㄦ湡20ms,

	1.0-1.5涓烘

	1.5-2.0涓哄弽

	1.5ms鍗犵┖姣?

	鍋滆浆		1.5/20*800 = 60	--> 0

	姝ｆ弧杞?		1/20*800 = 40 --> 100

	鍙嶆弧杞?		2/20*800 = 80 --> -100

	counter = -0.2*duty+60.

	duty = (counter-60)*(-5)

	涓轰簡鏂逛究璁剧疆,PWM鍒濆鍖栨椂,闇€瑕佽缃鏁板€间负60,

  */

/* USER CODE END Header_StartrovTask */

void StartrovTask(void *argument)

{

  /* USER CODE BEGIN StartrovTask */

    // 杈撳嚭闄愬箙鐢卞畯 THRUSTER_OUTPUT_LIMIT_DUTY 鎺у埗

    // 鍒濆鍖朠ID鍜岃繍鍔ㄦ帶鍒?

    pid_init();

    rov_motion_init();

	

	/* ROV */

	HAL_TIM_PWM_Start(&htim1,TIM_CHANNEL_1); // 宸﹀墠鎺ㄨ繘鍣?

	HAL_TIM_PWM_Start(&htim1,TIM_CHANNEL_2); // 宸︿腑鎺ㄨ繘鍣?

	HAL_TIM_PWM_Start(&htim1,TIM_CHANNEL_3); // 宸﹀悗鎺ㄨ繘鍣?

	HAL_TIM_PWM_Start(&htim1,TIM_CHANNEL_4); // 鍙冲墠鎺ㄨ繘鍣?

	HAL_TIM_PWM_Start(&htim4,TIM_CHANNEL_1); // 鍙充腑鎺ㄨ繘鍣?

	HAL_TIM_PWM_Start(&htim4,TIM_CHANNEL_2); // 鍙冲悗鎺ㄨ繘鍣?
	HAL_TIM_PWM_Start(&htim4,TIM_CHANNEL_3); // Light PWM
	HAL_TIM_PWM_Start(&htim4,TIM_CHANNEL_4); // Servo1 PWM
	HAL_TIM_PWM_Start(&htim9,TIM_CHANNEL_1); // Servo2 PWM
	HAL_TIM_PWM_Start(&htim9,TIM_CHANNEL_2); // Servo3 PWM

	HAL_Delay(1000);                                         

	/* RGB */

	HAL_TIM_PWM_Start(&htim3,TIM_CHANNEL_1);	
	HAL_TIM_PWM_Start(&htim3,TIM_CHANNEL_3);
	HAL_TIM_PWM_Start(&htim3,TIM_CHANNEL_4);
	buzzer_init();
	apply_thruster_outputs();

  /* Infinite loop */
	for(;;)
	{
		apply_thruster_outputs();
		rov_motion_update();
		osDelay(THRUSTER_OUTPUT_TASK_PERIOD_MS);
	}

  /* USER CODE END StartrovTask */

}



/**
 * @brief 閫熷害鍊艰浆鎹负鎺ㄨ繘鍣ㄥ亸绉婚噺 (淇濇寔-5~5鏄犲皠鍒?100%~100%)
 * @param speed 閫熷害鍊?(-5.0 ~ +5.0)
 * @return 鎺ㄨ繘鍣ㄥ亸绉婚噺 (-20 ~ +20)
 * @note 鏄犲皠鍏崇郴淇濇寔涓嶅彉锛歴peed * 4 = 鍋忕Щ閲?

 */

static float speed_to_thruster_offset(float speed)
{
    const float max_speed = 5.0f;
    speed = clampf(speed, -max_speed, max_speed);

    if (THRUSTER_MAX_OFFSET == 0.0f) 
		{
        return 0.0f;
    }
    float normalized = speed / max_speed;
    return normalized * THRUSTER_MAX_OFFSET;
}

/**
 * @brief 闄愬埗鎺ㄨ繘鍣ㄥ€艰寖鍥达紙杈撳嚭闄愬箙锛?
 */

static float limit_thruster_value(float value)
{
    return clampf(value, THRUSTER_MIN_VALUE, THRUSTER_MAX_VALUE);
}

/**
 * @brief 鍓嶈繘杩愬姩鎺у埗 (浣跨敤4涓枩缃帹杩涘櫒)
 * @param speed 鍓嶈繘閫熷害 (-5.0 ~ +5.0)
 */
void motion_forward(float speed)
{

		speed *= 0.3f;  // 闄嶄綆鍒?0%閫熷害
    float offset = speed_to_thruster_offset(speed);
    float horizontal_component = offset * 0.707f; // cos(45?)
 
    // 4涓枩缃帹杩涘櫒鐨勫墠杩涘垎閲?(45搴︽帹杩涘櫒鐨勫墠杩涘垎閲?
    g_rov_motion.thruster_values[THRUSTER_LEFT_FRONT] += horizontal_component;
    g_rov_motion.thruster_values[THRUSTER_LEFT_REAR] -= horizontal_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_FRONT] += horizontal_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_REAR] -= horizontal_component;

    //printf("Forward: %.2f (offset: %.2f)", speed, offset);
}



/**
 * @brief 鍚庨€€杩愬姩鎺у埗
 * @param speed 鍚庨€€閫熷害 (0 ~ 5.0)
 */
void motion_backward(float speed)
{
    motion_forward(-speed); // 鍙嶅悜鍓嶈繘
}

/**
 * @brief 鍙冲钩绉昏繍鍔ㄦ帶鍒?(浣跨敤4涓枩缃帹杩涘櫒)
 * @param speed 鍙崇Щ閫熷害 (0 ~ 5.0)
 */
void motion_right(float speed)
{
	  speed *= 0.3f;  // 闄嶄綆鍒?0%閫熷害
    float offset = speed_to_thruster_offset(speed);
    float lateral_component = offset * 0.707f; // sin(45?)

    // 鏍规嵁45搴︽帹杩涘櫒閰嶇疆瀹炵幇鍙崇Щ
    // 宸﹀墠鍜屽彸鍚庢帹杩涘櫒浜х敓鍚戝彸鐨勫垎閲?
    // 宸﹀悗鍜屽彸鍓嶆帹杩涘櫒浜х敓鍚戝乏鐨勫垎閲忥紝鎵€浠ュ弽鍚?
    g_rov_motion.thruster_values[THRUSTER_LEFT_FRONT] += lateral_component;
    g_rov_motion.thruster_values[THRUSTER_LEFT_REAR] += lateral_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_FRONT] -= lateral_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_REAR] -= lateral_component;

    //printf("Right: %.2f (offset: %d)\r\n", speed, offset);
}



/**
 * @brief 宸﹀钩绉昏繍鍔ㄦ帶鍒?
 * @param speed 宸︾Щ閫熷害 (0 ~ 5.0)
 */
void motion_left(float speed)
{
    motion_right(-speed); // 鍙嶅悜鍙崇Щ
}



/**
 * @brief 涓婃诞杩愬姩鎺у埗 (浣跨敤2涓瀭鐩存帹杩涘櫒)
 * @param speed 涓婃诞閫熷害 (0 ~ 5.0)
 */
void motion_up(float speed)
{
    // 闄嶄綆鎵嬪姩涓婃诞閫熷害鍒?0%
    speed *= 0.5f;
    float offset = speed_to_thruster_offset(-speed); // ????

    // 鍙湁鍨傜洿鎺ㄨ繘鍣ㄥ弬涓庝笂娴?
    g_rov_motion.thruster_values[THRUSTER_LEFT_MID] += offset;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_MID] += offset;
}



/**
 * @brief 涓嬫綔杩愬姩鎺у埗 (浣跨敤2涓瀭鐩存帹杩涘櫒)
 * @param speed 涓嬫綔閫熷害 (0 ~ 5.0)
 */
void motion_down(float speed)
{
    float offset = speed_to_thruster_offset(speed) * 0.5f;
    // 鍙湁鍨傜洿鎺ㄨ繘鍣ㄥ弬涓庝笅娼?
    g_rov_motion.thruster_values[THRUSTER_LEFT_MID] += offset;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_MID] += offset;
}


/**
 * @brief 鍙宠浆杩愬姩鎺у埗 (浣跨敤4涓枩缃帹杩涘櫒)
 * @param speed 鍙宠浆閫熷害 (0 ~ 5.0)
 */
void motion_turn_right(float speed)
{
    float offset = speed_to_thruster_offset(speed);
    float turn_component = offset * 0.6f; // ??????
	
    // 閫氳繃宸姩鎺у埗瀹炵幇鍙宠浆
    // 宸︿晶鎺ㄨ繘鍣ㄥ鍔犳帹鍔涳紝鍙充晶鎺ㄨ繘鍣ㄥ噺灏戞帹鍔?
    g_rov_motion.thruster_values[THRUSTER_LEFT_FRONT] += turn_component;
    g_rov_motion.thruster_values[THRUSTER_LEFT_REAR] -= turn_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_FRONT] -= turn_component;
    g_rov_motion.thruster_values[THRUSTER_RIGHT_REAR] += turn_component;

}



/**
 * @brief 宸﹁浆杩愬姩鎺у埗
 * @param speed 宸﹁浆閫熷害 (0 ~ 5.0)
 */
void motion_turn_left(float speed)
{
    motion_turn_right(-speed); // 鍙嶅悜鍙宠浆
}



/**
 * @brief 鍋滄鎵€鏈夎繍鍔?
 */
void motion_stop(void)
{
    clear_all_thrusters();
    printf("Motion STOP\r\n");
}


/**
 * @brief 娓呴浂鎵€鏈夋帹杩涘櫒
 */

void clear_all_thrusters(void)
{
	for (int i = 0; i < THRUSTER_COUNT; i++) 
  {
			g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE;
	}
}


/**
 * @brief 鍒濆鍖朢OV杩愬姩鎺у埗
 */
void rov_motion_init(void)
{
    // 鍒濆鍖栨帹杩涘櫒涓哄仠杞姸鎬?
    for (int i = 0; i < THRUSTER_COUNT; i++) 
	  {
        g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE;
        g_thruster_outputs[i] = THRUSTER_CENTER_VALUE;
    }
    g_rov_motion.motion_enabled = true;
    g_rov_motion.last_update_time = HAL_GetTick();
    g_thruster_ramp_ms = g_rov_motion.last_update_time;
    printf("ROV Motion Control Initialized (6-Thruster Config)\r\n");
}

/**
 * @brief 搴旂敤鎺ㄨ繘鍣ㄥ€煎埌纭欢
 */
void apply_thruster_outputs(void)
{
    uint32_t now_ms = HAL_GetTick();
    bool hold_last_command = should_hold_last_thruster_command(now_ms);
    if (!g_rov_motion.motion_enabled) {
        clear_all_thrusters();
    } else if (!hold_last_command &&
               (!g_control_state.data_valid) &&
               (g_control_state.last_update_time != 0U) &&
               ((now_ms - g_control_state.last_update_time) > CONTROL_SIGNAL_TIMEOUT_MS)) {
        clear_all_thrusters();
    }

    uint16_t pwm_outputs[THRUSTER_COUNT];
    if (g_thruster_ramp_ms == 0) {
        for (int i = 0; i < THRUSTER_COUNT; i++) {
            g_thruster_outputs[i] = THRUSTER_CENTER_VALUE;
        }
        g_thruster_ramp_ms = now_ms;
    }
    uint32_t delta_ms = now_ms - g_thruster_ramp_ms;
    if (delta_ms > 100) delta_ms = 100;
    float dt_s = delta_ms / 1000.0f;
    g_thruster_ramp_ms = now_ms;

    for (int i = 0; i < THRUSTER_COUNT; i++)
    {
        float target = limit_thruster_value(g_rov_motion.thruster_values[i]);
        float target_duty = counter_to_duty(target);
        target_duty = apply_thruster_deadzone_compensation((uint8_t)i, target_duty);
        float current = g_thruster_outputs[i];
        float current_duty = counter_to_duty(current);
        float rate = select_slew_rate(current_duty, target_duty);
        float max_delta = rate * dt_s;
        float delta_duty = target_duty - current_duty;
        float new_duty = target_duty;
        if (max_delta > 0.0f && fabsf(delta_duty) > max_delta) {
            new_duty = current_duty + signf(delta_duty) * max_delta;
        }
        float new_counter = duty2counter(new_duty);
        g_thruster_outputs[i] = new_counter;
        pwm_outputs[i] = pwm_from_float(new_counter);
        msg.thruster[i] = (int32_t)roundf(counter_to_duty(new_counter));
    }
    __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_1, pwm_outputs[THRUSTER_LEFT_FRONT]);
    __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_2, pwm_outputs[THRUSTER_LEFT_MID]);
    __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_3, pwm_outputs[THRUSTER_LEFT_REAR]);
    __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_4, pwm_outputs[THRUSTER_RIGHT_FRONT]);
    __HAL_TIM_SET_COMPARE(&htim4, TIM_CHANNEL_1, pwm_outputs[THRUSTER_RIGHT_MID]);
    __HAL_TIM_SET_COMPARE(&htim4, TIM_CHANNEL_2, pwm_outputs[THRUSTER_RIGHT_REAR]);
}

/**
 * @brief 浣胯兘/绂佺敤杩愬姩鎺у埗
 * @param enabled true=浣胯兘, false=绂佺敤
 */
void set_motion_enabled(bool enabled)
{
    g_rov_motion.motion_enabled = enabled;

    if (!enabled) 
		{
        motion_stop();
        apply_thruster_outputs();
    }

    printf("Motion %s\r\n", enabled ? "ENABLED" : "DISABLED");
}


/**
 * @brief 鑾峰彇鎺ㄨ繘鍣ㄧ姸鎬?
 * @param thruster_index 鎺ㄨ繘鍣ㄧ储寮?
 * @return 鎺ㄨ繘鍣≒WM鍊?
 */
uint16_t get_thruster_value(thruster_index_t thruster_index)
{
    if (thruster_index < THRUSTER_COUNT) 
		{
        return pwm_from_float(g_rov_motion.thruster_values[thruster_index]);
    }
    return pwm_from_float(THRUSTER_CENTER_VALUE);
}


/**
 * @brief 鎵撳嵃鎺ㄨ繘鍣ㄧ姸鎬?

 */
void print_thruster_status(void)
{

    printf("Thrusters: LF=%.2f LM=%.2f LR=%.2f RF=%.2f RM=%.2f RR=%.2f Motion:%s\r\n",

	 g_rov_motion.thruster_values[THRUSTER_LEFT_FRONT],
	 g_rov_motion.thruster_values[THRUSTER_LEFT_MID],
	 g_rov_motion.thruster_values[THRUSTER_LEFT_REAR],
	 g_rov_motion.thruster_values[THRUSTER_RIGHT_FRONT],
	 g_rov_motion.thruster_values[THRUSTER_RIGHT_MID],
	 g_rov_motion.thruster_values[THRUSTER_RIGHT_REAR],
	 g_rov_motion.motion_enabled ? "ON" : "OFF");
}



/**
 * @brief ROV杩愬姩鎺у埗浠诲姟涓诲嚱鏁?
 */
void rov_motion_update(void)
{
    // Long signal loss should eventually clear the motion command buffer.
    uint32_t current_time = HAL_GetTick();
    if ((g_control_state.last_update_time != 0U) &&
        ((current_time - g_control_state.last_update_time) >
         (CONTROL_SIGNAL_TIMEOUT_MS + THRUSTER_FAILSAFE_HOLD_MS + 1000U))) {
        motion_stop();
        printf("Motion timeout, emergency stop\r\n");
    }
}


/**
 * @brief 绱ф€ュ仠姝?
 */
void emergency_stop(void)
{
    set_motion_enabled(false);
    printf("EMERGENCY STOP ACTIVATED\r\n");
}



/**
 * @brief 娴嬭瘯鎵€鏈夋帹杩涘櫒
 */
void test_all_thrusters(void)
{
    printf("Testing all thrusters...\r\n");

    const char* thruster_names[] = {
        "Left Front", "Left Mid", "Left Rear",
        "Right Front", "Right Mid", "Right Rear"

    };

    for (int i = 0; i < THRUSTER_COUNT; i++) {

        clear_all_thrusters();
        g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE + 5; // 灏忓箙搴︽祴璇?
        apply_thruster_outputs();
        printf("Testing %s thruster\r\n", thruster_names[i]);
        HAL_Delay(2000);
    }

    motion_stop();
    apply_thruster_outputs();
    printf("Thruster test completed\r\n");
}

void debug_print_thruster_outputs(void)
{
    printf("=== Thruster Debug Info ===\r\n");
    printf("Power Limit: %.0f%%\r\n", THRUSTER_OUTPUT_LIMIT_DUTY);

    for (int i = 0; i < THRUSTER_COUNT; i++) {
        float pwm_value = limit_thruster_value(g_rov_motion.thruster_values[i]);
        float duty = counter_to_duty(pwm_value);
        printf("Thruster %d: PWM=%.2f Duty=%.1f%%\r\n", i, pwm_value, duty);
    }

    printf("========================\r\n");
}

/**

 * @brief 鍚戞寚瀹氭帹杩涘櫒娣诲姞杈撳嚭鍊硷紙绱姞妯″紡锛?
 * @param thruster 鎺ㄨ繘鍣ㄧ储寮?
 * @param value 杈撳嚭鍊?(-5.0 ~ +5.0)
 */
void add_thruster_output(thruster_index_t thruster, float value)
{
    if (thruster >= THRUSTER_COUNT) {
        return;
    }

    float max_duty = THRUSTER_OUTPUT_LIMIT_DUTY;
    if (max_duty <= 0.0f) {
        return;
    }

    float duty_delta = (value / 5.0f) * max_duty;
    float current_duty = counter_to_duty(g_rov_motion.thruster_values[thruster]);
    float new_duty = clampf(current_duty + duty_delta, -max_duty, max_duty);

    g_rov_motion.thruster_values[thruster] = duty2counter(new_duty);
}

float duty2counter(float duty)
{
    float max_duty = THRUSTER_OUTPUT_LIMIT_DUTY;
    if (max_duty <= 0.0f) {
        max_duty = 1.0f;
    }
    duty = clampf(duty, -max_duty, max_duty);

    float normalized = duty / max_duty;
    float offset = normalized * THRUSTER_MAX_OFFSET;
    float counter = THRUSTER_PWM_CENTER_VALUE - offset;
    return limit_thruster_value(counter);
}

/**
 * @brief 鎺ㄨ繘鍣ㄥ惎鍔ㄩ煶鏁?- 鎵€鏈夋帹杩涘櫒蹇€熻剦鍐?
 */
void thruster_startup_beep(void)
{
    printf("Thruster startup sequence initiated...\r\n");

    // 淇濆瓨褰撳墠鍊?
    float saved_values[THRUSTER_COUNT];
    for (int i = 0; i < THRUSTER_COUNT; i++) 
	  {
        saved_values[i] = g_rov_motion.thruster_values[i];
    }
    // 鑴夊啿搴忓垪锛氱煭淇冪殑鍚姩闊?
    for (int pulse = 0; pulse < 2; pulse++) 
		{  // 涓ゆ鑴夊啿
        // 鎵€鏈夋帹杩涘櫒鍚屾椂灏忓箙搴﹀惎鍔?
        for (int i = 0; i < THRUSTER_COUNT; i++) 
			  {
            g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE + 8; // 灏忓箙搴︽杞?

        }
        apply_thruster_outputs();
        HAL_Delay(100);  // 鎸佺画100ms

        // 鍥炲埌鍋滆浆
        for (int i = 0; i < THRUSTER_COUNT; i++) 
				{
            g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE;
        }
        apply_thruster_outputs();
        HAL_Delay(80);   // 闂撮殧80ms

    }
    // 鎭㈠鍘熷€?
    for (int i = 0; i < THRUSTER_COUNT; i++) 
		{
        g_rov_motion.thruster_values[i] = saved_values[i];
    }
    apply_thruster_outputs();
    printf("Thruster startup complete.\r\n");
}



/**
 * @brief 鎺ㄨ繘鍣ㄥ緟鏈洪煶鏁?- 娓愬急涓嬮檷闊宠皟
 */
void thruster_standby_beep(void)
{
    printf("Thruster standby sequence initiated...\r\n");
    // 淇濆瓨褰撳墠鍊?
    float saved_values[THRUSTER_COUNT];
    for (int i = 0; i < THRUSTER_COUNT; i++) 
	  {
        saved_values[i] = g_rov_motion.thruster_values[i];
    }  

    // 娓愬急鐨勪笅闄嶉煶璋冿細浠庡己鍒板急
    uint8_t amplitudes[] = {8, 6, 4};  // 閫愭笎鍑忓急鐨勫箙搴?
    uint16_t durations[] = {100, 100, 150};  // 鎸佺画鏃堕棿

    for (int step = 0; step < 3; step++) 
		{
        for (int i = 0; i < THRUSTER_COUNT; i++) 
			  {
            g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE + amplitudes[step];
        }
        apply_thruster_outputs();
        HAL_Delay(durations[step]);
        // 鐭殏鍋滈】
        for (int i = 0; i < THRUSTER_COUNT; i++) 
				{
            g_rov_motion.thruster_values[i] = THRUSTER_CENTER_VALUE;
        }
        apply_thruster_outputs();
        HAL_Delay(50);
    }

    // 鎭㈠鍘熷€?
    for (int i = 0; i < THRUSTER_COUNT; i++) 
		{
        g_rov_motion.thruster_values[i] = saved_values[i];
    }
    apply_thruster_outputs();
    printf("Thruster standby complete.\r\n");
}

void buzzer_init(void)
{
    HAL_GPIO_WritePin(ALARM_GPIO_Port, ALARM_Pin, GPIO_PIN_RESET);
    DWT_Init(SystemCoreClock / 1000000);
}

void buzzer_beep(uint16_t freq_hz, uint16_t duration_ms)
{
    if (freq_hz == 0 || duration_ms == 0)
    {
        return;
    }

    const float half_period = 0.5f / (float)freq_hz;
    const float total_time_s = duration_ms / 1000.0f;
    const uint32_t toggles = (uint32_t)(total_time_s * freq_hz * 2.0f);

    for (uint32_t i = 0; i < toggles; i++)
    {
        HAL_GPIO_TogglePin(ALARM_GPIO_Port, ALARM_Pin);
        DWT_Delay(half_period);
    }
    HAL_GPIO_WritePin(ALARM_GPIO_Port, ALARM_Pin, GPIO_PIN_RESET);
}

/**

 * @brief 妯粴鎺у埗鍑芥暟 - 閫氳繃宸﹀彸鍨傜洿鎺ㄨ繘鍣ㄥ樊鍔ㄥ疄鐜?

 * @param roll_value 妯粴鎺у埗鍊?(-5.0 ~ +5.0)

 *                   姝ｅ€硷細鏈哄櫒浜哄悜鍙虫í婊氾紙鍙充晶涓嬫矇锛?

 *                   璐熷€硷細鏈哄櫒浜哄悜宸︽í婊氾紙宸︿晶涓嬫矇锛?

 * @note 浣跨敤LEFT_MID鍜孯IGHT_MID涓や釜鍨傜洿鎺ㄨ繘鍣ㄧ殑宸姩鎺ㄥ姏浜х敓妯粴鍔涚煩

 */

void motion_roll(float roll_value)
{

    // 杈撳叆鑼冨洿妫€鏌ュ拰闄愬埗

    if (roll_value > 5.0f) roll_value = 5.0f;
    if (roll_value < -5.0f) roll_value = -5.0f;

    // 姝诲尯澶勭悊锛岄伩鍏嶅井灏忔姈鍔?
    if (fabs(roll_value) < 0.01f)  return;
    // 妯粴鎺у埗寮哄害绯绘暟锛堝彲鏍规嵁瀹為檯鏁堟灉璋冩暣锛?
    float roll_strength = 0.2f; // 20% 寮哄害
    // 璁＄畻宸姩鎺ㄥ姏
    float differential_thrust = roll_value * roll_strength;

    // 妯粴鎺у埗閫昏緫锛?
    // 姝ｅ€?鍙虫í婊?: 宸︽帹杩涘櫒鍚戜笅鎺紝鍙虫帹杩涘櫒鍚戜笂鎺?鎴栧噺灏忓悜涓嬫帹鍔?
    // 璐熷€?宸︽í婊?: 鍙虫帹杩涘櫒鍚戜笅鎺紝宸︽帹杩涘櫒鍚戜笂鎺?鎴栧噺灏忓悜涓嬫帹鍔?
    add_thruster_output(THRUSTER_LEFT_MID, differential_thrust);    // 宸﹀瀭鐩存帹杩涘櫒
    add_thruster_output(THRUSTER_RIGHT_MID, -differential_thrust);  // 鍙冲瀭鐩存帹杩涘櫒

    #if 0  // 璋冭瘯淇℃伅寮€鍏?
    printf("Roll Control: %.2f -> LM=%+.2f, RM=%+.2f\r\n", roll_value, differential_thrust, -differential_thrust);
    #endif

}



