#ifndef _PERIPHERAL_APP_H
#define _PERIPHERAL_APP_H

#ifdef __cplusplus
extern "C" {
#endif

#include "global_data.h"

// 配置参数
#define SYNC_INTERVAL_TIMES 10       // 每10次写入触发一次f_sync（即200ms同步一次）

typedef struct
{
	uint16_t  ReceiveNum;			 			// 接收字节数，只要字节数>0，即为接收到新一帧数据
#if USE_MALLOC
	uint8_t   *ReceiveData; 		// 接收到的数据
	uint8_t   *BuffTemp;		   	// 临时缓存，在DMA空闲中断中将把一帧数据复制到ReceivedData[ ]   
#else
	uint8_t   ReceiveData[USART_REC_LEN]; 		// 接收到的数据
	uint8_t   BuffTemp[USART_REC_LEN];		   	// 临时缓存，在DMA空闲中断中将把一帧数据复制到ReceivedData[ ]   
#endif
	
}xUART_Typedef;

typedef struct
{
	xUART_Typedef userUart2;
	xUART_Typedef userUart3;
	xUART_Typedef userUart4;
	xUART_Typedef userUart7;
	xUART_Typedef userUart8;
}UART_REC;

// 批量数据结构体（示例：可替换为实际业务数据）
typedef struct {
    uint32_t timestamp;  // 时间戳（如系统运行毫秒数）
    uint16_t sensor1;    // 传感器1数据
    uint16_t sensor2;    // 传感器2数据
    uint8_t  reserve[24];// 预留字段（凑32字节，适配批量写入）
} SD_BatchData_TypeDef;

// 函数声明
HAL_StatusTypeDef SD_Init(void);                  // SD卡初始化（含FatFS挂载）
HAL_StatusTypeDef SD_BatchWrite(SD_BatchData_TypeDef *data); // 单次批量写入
void SD_WriteTask(void const *argument);          // FreeRTOS写入任务（20ms周期）

void usart_init(void);
void uart8_task(void *argument);
void sd_task(void *argument);

#ifdef __cplusplus
}
#endif

#endif 

