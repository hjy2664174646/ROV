#ifndef _TIM14_DELAY_H
#define _TIM14_DELAY_H

#ifdef __cplusplus
extern "C" {
#endif


#include "tim.h"
#include "FreeRTOS.h"
#include "task.h"

void DelayUs(uint32_t us);
void DelayMs(uint32_t ms);


#ifdef __cplusplus
}
#endif

#endif 

