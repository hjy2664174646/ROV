#ifndef GLOBAL_DATA_H
#define GLOBAL_DATA_H

#ifdef __cplusplus
extern "C" {
#endif
#include "FreeRTOS.h"
#include "task.h"
#include "main.h"
#include "cmsis_os.h"
#include "tim.h"
#include <stdio.h>
#include "FreeRTOS.h"
#include "task.h"
#include "semphr.h"
#include "usart.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "adc.h"
#include "dma.h"
#include "fatfs.h"
#include "i2c.h"
#include "sdio.h"
#include "spi.h"
#include "tim.h"
#include "usart.h"
#include "usb_otg.h"
#include "gpio.h"
#include "tim14_delay.h"

#define TOTAL_WORD 37			/* 协议有多少word,一个word四个字节 */
#define USART_REC_LEN   1024                     /* 定义最大接收字节数 512 */
#define USART_EN_RX     1                       /* 使能接收:0:禁止接收.1:允许接收 */
#define RXBUFFERSIZE    1                       /* 缓存大小 */
#define MAX_FRAME_LEN 	1024
#define YES 1
#define NO (!YES)
#define TEST_SD  0	/* 测试SD卡,只打印,不写SD内部 */


#ifdef __cplusplus
}
#endif

#endif /* GLOBAL_DATA_H */

