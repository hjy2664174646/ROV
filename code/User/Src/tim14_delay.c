#include "tim14_delay.h"

/**
 * @brief  基于TIM14的微秒级延时函数
 * @param  us: 延时微秒数，最大65535us
 * @retval None
 */
void DelayUs(uint32_t us)
{
    if (us == 0) return;
    
    // 确保不超过最大可延时值
    if (us > 65535)
        us = 65535;
    
    // 停止定时器
    HAL_TIM_Base_Stop(&htim14);
    
    // 清零计数器
    __HAL_TIM_SET_COUNTER(&htim14, 0);
    
    // 启动定时器
    HAL_TIM_Base_Start(&htim14);
    
    // 等待计数器达到目标值，期间允许任务切换
    while (__HAL_TIM_GET_COUNTER(&htim14) < us)
    {
        taskYIELD(); // 让出CPU给其他任务
    }
    
    // 停止定时器
    HAL_TIM_Base_Stop(&htim14);
}

/**
 * @brief  基于TIM14的毫秒级延时函数(使用微秒延时实现)
 * @param  ms: 延时毫秒数
 * @retval None
 */
void DelayMs(uint32_t ms)
{
    for (uint32_t i = 0; i < ms; i++)
    {
        DelayUs(1000); // 1ms = 1000us
    }
}
    