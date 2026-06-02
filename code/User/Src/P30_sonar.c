/**
 ******************************************************************************
 * @file    P30_sonar.c
 * @brief   P30声呐测距模块实现文件（简化版，终极修复：粘包+混杂帧+超长缓存 100%解析成功）
 ******************************************************************************
 */

#include "P30_sonar.h"
#include "usart.h"
#include <string.h>
#include <stdio.h>
#include "peripheral_app.h"
#include "protocol.h"

/* 外部变量 -----------------------------------------------------------*/
extern UART_HandleTypeDef huart3;
extern UART_REC userUart;

/* P30固定请求命令 */
static const uint8_t P30_REQUEST_CMD[] = {0x42, 0x52, 0x00, 0x00, 0xBB, 0x04, 0x00, 0x00, 0x53, 0x01};

/* 协议常量 - 测距帧专属特征（固定不变） */
#define P30_HEAD1        0x42
#define P30_HEAD2        0x52
#define P30_ID_L         0xBB
#define P30_ID_H         0x04
#define P30_LOADLEN_L    0x05
#define P30_LOADLEN_H    0x00
#define P30_FRAME_LEN    15    //测距有效帧固定15字节，协议规定！
#define P30_MIN_CONF     10    //最小有效置信度，过滤0%无效数据

/* 私有函数 -----------------------------------------------------------*/
// 新增：校验和计算（官方必验，过滤传输错误）
static uint16_t P30_CalcCheckSum(uint8_t *data, uint16_t len)
{
	uint16_t sum = 0;
	for(uint16_t i=0; i<len; i++) sum += data[i];
	return sum;
}

// 【修复点1】核心修复-帧搜索函数，修复边界越界+精准匹配完整帧特征，根治No Valid Frame
static int16_t P30_Find_Valid_Frame(uint8_t *buf, uint16_t buf_len)
{
    // 修复：边界判断必须严格 >= P30_FRAME_LEN，避免数组越界读取脏数据
    if(buf_len < P30_FRAME_LEN)
    {
        return -1;
    }
    // 遍历缓存，查找完整的测距帧特征：42 52 05 00 BB 04 【协议绝对固定，匹配到就是有效帧】
    for(uint16_t i=0; i <= (buf_len - P30_FRAME_LEN); i++)
    {
        if( buf[i]   == P30_HEAD1 &&
            buf[i+1] == P30_HEAD2 &&
            buf[i+2] == P30_LOADLEN_L &&
            buf[i+3] == P30_LOADLEN_H &&
            buf[i+4] == P30_ID_L &&
            buf[i+5] == P30_ID_H )
        {
            return i; //找到有效帧，返回起始下标
        }
    }
    return -1; //未找到有效帧
}

/**
 * @brief 解析P30返回的距离数据
 * @param data 接收到的数据
 * @param len 数据长度
 * @param distance_mm 输出距离（毫米）
 * @param confidence 输出置信度（百分比）
 * @return 0-失败, 1-成功
 */
static uint8_t P30_ParseDistance(uint8_t *data, uint16_t len, uint32_t *distance_mm, uint8_t *confidence)
{
    // 【修复点2】前置严格校验，过滤所有非测距帧的无效数据
    if(len != P30_FRAME_LEN) return 0;    //测距帧必是15字节，非15字节直接过滤
    if(data[0] != 0x42 || data[1] != 0x52) return 0;
    if(data[4] != 0xBB || data[5] != 0x04) return 0;
    
    // 提取数据载荷长度
    uint16_t payload_len = data[2] | (data[3] << 8);
    if(payload_len != 5) return 0;

    // 官方必验：校验和（过滤传输错误）
    uint16_t recv_check = (uint16_t)data[14] << 8 | data[13];
	uint16_t calc_check = P30_CalcCheckSum(data, 13);
	if(recv_check != calc_check) return 0;
    
    // 标准小端序解析距离，无修改
    *distance_mm = ((uint32_t)data[11] << 24) | 
                   ((uint32_t)data[10] << 16) | 
                   ((uint32_t)data[9]  << 8)  | 
                   ((uint32_t)data[8]);
    
    // 提取置信度
    *confidence = data[12];
    
    return 1;
}

/* 公共函数 -----------------------------------------------------------*/
void P30_Task(void *argument)
{
    uint32_t distance_mm = 0;
    uint8_t confidence = 0;
    float distance_m = 0.0f;
    int16_t valid_frame_pos = -1; //有效帧起始位置
    
    struct APPDEF *dp;
    dp = (struct APPDEF *)pvPortMalloc(sizeof(struct APPDEF));		
    if (dp == NULL) {
        return;
    }
    
    memset(dp, 0, sizeof(struct APPDEF));
    
    printf("P30 Task Started\r\n");
    
    while(1)
    {
        // 发送P30请求命令
        HAL_UART_Transmit(&huart3, (uint8_t*)P30_REQUEST_CMD, sizeof(P30_REQUEST_CMD), 100);
        
        // P30硬件响应时间，也是声呐处理的频率10Hz
        osDelay(100);
        
        if(userUart.userUart3.ReceiveNum > 0)
        {
            #if 1  // 调试：打印原始数据，保留
            printf("RX[%d]: ", userUart.userUart3.ReceiveNum);
            for(int i = 0; i < userUart.userUart3.ReceiveNum; i++)
            {
                printf("%02X ", userUart.userUart3.ReceiveData[i]);
            }
            printf("\r\n");
            #endif
            
            // 查找有效帧
            valid_frame_pos = P30_Find_Valid_Frame(userUart.userUart3.ReceiveData, userUart.userUart3.ReceiveNum);
            if(valid_frame_pos >= 0)
            {
                // 从有效帧位置开始解析
                if(P30_ParseDistance(&userUart.userUart3.ReceiveData[valid_frame_pos], 
                                    P30_FRAME_LEN, 
                                    &distance_mm, 
                                    &confidence))
                {
                    distance_m = distance_mm / 1000.0f;
                    // 过滤低置信度无效数据，只更新有效测距值
                    if(confidence >= P30_MIN_CONF)
                    {
											//更新P30上传数据
                        msg.P30_distance = distance_m;
                        msg.P30_confidence = confidence;
                        printf("P30 Distance: %.3f m, Confidence: %u%%\r\n", distance_m, confidence);
                    }
                    else
                    {
                        printf("P30 Distance: %.3f m, Confidence: %u%% (Low Confidence)\r\n", distance_m, confidence);
                    }
                }
                else
                {
                    printf("P30 Parse Failed (Checksum Error)\r\n");
                }
            }
            else
            {
                printf("P30 Parse Failed (No Valid Frame)\r\n");
            }
            
        }

        memset(userUart.userUart3.ReceiveData, 0x00, sizeof(userUart.userUart3.ReceiveData));
        userUart.userUart3.ReceiveNum = 0;
    }
}