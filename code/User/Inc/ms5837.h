#ifndef __MS5837_H
#define __MS5837_H

#ifdef __cplusplus
extern "C" {
#endif

#include "stm32f4xx_hal.h"
#include "FreeRTOS.h"
#include "task.h"
#include "semphr.h"
#include "global_data.h"

// MS5837-30BA I2C地址（写：0xEC，读：0xED）
#define MS5837_ADDR_WRITE    0xEC
#define MS5837_ADDR_READ     0xED

// 命令定义
#define MS5837_CMD_RESET     0x1E    // 复位命令
#define MS5837_CMD_ADC_READ  0x00    // ADC读取命令
#define MS5837_CMD_PROM_READ 0xA0    // PROM读取命令（需拼接地址）

// 过采样率（OSR）对应D1/D2转换命令
typedef enum {
    MS5837_OSR_256  = 0,
    MS5837_OSR_512  = 1,
    MS5837_OSR_1024 = 2,
    MS5837_OSR_2048 = 3,
    MS5837_OSR_4096 = 4,
    MS5837_OSR_8192 = 5
} MS5837_OSR_TypeDef;

#define MS5837_D1_CMD(osr)  (0x40 + (osr << 1))  // D1（压力）转换命令
#define MS5837_D2_CMD(osr)  (0x50 + (osr << 1))  // D2（温度）转换命令

// 校准系数结构体（W0~W6，W0含CRC）
typedef struct {
    uint16_t W0;
    uint16_t W1;
    uint16_t W2;
    uint16_t W3;
    uint16_t W4;
    uint16_t W5;
    uint16_t W6;
} MS5837_CalibData_TypeDef;

// 水质类型枚举（新增）
typedef enum {
    MS5837_WATER_FRESH = 0,  // 淡水 (1000 kg/m³)
    MS5837_WATER_SEA   = 1   // 海水 (1025 kg/m³)
} MS5837_WaterType_TypeDef;

// 传感器数据结构体（扩展版）
typedef struct {
    float pressure;           // 绝对压力值（单位：mbar）
    float temperature;        // 温度值（单位：℃）
    float water_depth;        // 水深值（单位：米）- 新增
    float gauge_pressure;     // 表压值（单位：mbar）- 新增
    MS5837_CalibData_TypeDef calib; // 校准系数
    MS5837_OSR_TypeDef osr;         // 当前过采样率
    float atmospheric_pressure;      // 大气压基准值（单位：mbar）- 新增
    MS5837_WaterType_TypeDef water_type; // 水质类型 - 新增
} MS5837_Dev_TypeDef;

// 常量定义
#define MS5837_STANDARD_ATMOSPHERIC_PRESSURE  1013.25f  // 标准大气压 (mbar)
#define MS5837_FRESH_WATER_DENSITY           1000.0f    // 淡水密度 (kg/m³)
#define MS5837_SEA_WATER_DENSITY             1025.0f    // 海水密度 (kg/m³)
#define MS5837_GRAVITY_ACCELERATION          9.80665f   // 重力加速度 (m/s²)

// 基础函数声明
HAL_StatusTypeDef MS5837_Init(MS5837_Dev_TypeDef *dev, MS5837_OSR_TypeDef osr);
HAL_StatusTypeDef MS5837_ReadData(MS5837_Dev_TypeDef *dev);
void MS5837_Task(void *argument); // FreeRTOS任务函数

// 水深测量相关函数声明（新增）
HAL_StatusTypeDef MS5837_CalibrateAtmosphericPressure(MS5837_Dev_TypeDef *dev);
void MS5837_SetWaterType(MS5837_Dev_TypeDef *dev, MS5837_WaterType_TypeDef water_type);
void MS5837_SetAtmosphericPressure(MS5837_Dev_TypeDef *dev, float pressure_mbar);
float MS5837_CalculateDepth(MS5837_Dev_TypeDef *dev);
float MS5837_PressureToDepth(float gauge_pressure_mbar, MS5837_WaterType_TypeDef water_type);

// 工具函数声明（新增）
HAL_StatusTypeDef MS5837_ValidateCalibration(MS5837_Dev_TypeDef *dev);
void MS5837_PrintCalibration(MS5837_Dev_TypeDef *dev);
void MS5837_PrintSensorData(MS5837_Dev_TypeDef *dev);

#ifdef __cplusplus
}
#endif

#endif /* __MS5837_H */