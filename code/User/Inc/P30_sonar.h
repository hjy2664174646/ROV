/**
 ******************************************************************************
 * @file    P30_sonar.h
 * @brief   P30声呐测距模块头文件（简化版）
 ******************************************************************************
 */

#ifndef __P30_SONAR_H
#define __P30_SONAR_H

#ifdef __cplusplus
extern "C" {
#endif

#include "stm32f4xx_hal.h"
#include <stdint.h>
#include "FreeRTOS.h"
#include "task.h"

/**
 * @brief P30数据读取任务函数
 * @param argument 任务参数
 */
void P30_Task(void *argument);

#ifdef __cplusplus
}
#endif

#endif /* __P30_SONAR_H */
