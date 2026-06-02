/* Includes ------------------------------------------------------------------*/

#include "ms5837.h"
#include "protocol.h"
/******************************************************************************************/

extern msgStruct_t msg;

// 1. GPIO模拟I2C底层函数（PB8=SCL，PB9=SDA）
#define SCL_PIN    GPIO_PIN_8
#define SDA_PIN    GPIO_PIN_9
#define I2C_PORT   GPIOB

// SCL引脚输出高电平
static void I2C_SCL_High(void) 
{
    HAL_GPIO_WritePin(I2C_PORT, SCL_PIN, GPIO_PIN_SET);
    DelayUs(1);
}

// SCL引脚输出低电平
static void I2C_SCL_Low(void) 
{
    HAL_GPIO_WritePin(I2C_PORT, SCL_PIN, GPIO_PIN_RESET);
    DelayUs(1);
}

// SDA引脚设置为输出
static void I2C_SDA_Output(void) 
{
    GPIO_InitTypeDef GPIO_InitStruct = {0};
    GPIO_InitStruct.Pin = SDA_PIN;
    GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
    GPIO_InitStruct.Pull = GPIO_NOPULL;
    GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
    HAL_GPIO_Init(I2C_PORT, &GPIO_InitStruct);
}

// SDA引脚设置为输入
static void I2C_SDA_Input(void) 
{
    GPIO_InitTypeDef GPIO_InitStruct = {0};
    GPIO_InitStruct.Pin = SDA_PIN;
    GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
    GPIO_InitStruct.Pull = GPIO_NOPULL;
    HAL_GPIO_Init(I2C_PORT, &GPIO_InitStruct);
}

// SDA引脚输出高电平
static void I2C_SDA_High(void) 
{
    HAL_GPIO_WritePin(I2C_PORT, SDA_PIN, GPIO_PIN_SET);
    DelayUs(1);
}

// SDA引脚输出低电平
static void I2C_SDA_Low(void) 
{
    HAL_GPIO_WritePin(I2C_PORT, SDA_PIN, GPIO_PIN_RESET);
    DelayUs(1);
}

// 读取SDA引脚电平
static uint8_t I2C_SDA_Read(void) 
{
    uint8_t val = HAL_GPIO_ReadPin(I2C_PORT, SDA_PIN);
    DelayUs(1);
    return val;
}

// I2C发送起始条件（S：SCL高电平时，SDA从高变低）
static void I2C_Start(void) 
{
    I2C_SDA_Output();
    I2C_SDA_High();
    I2C_SCL_High();
    DelayUs(2);
    I2C_SDA_Low();
    DelayUs(2);
    I2C_SCL_Low(); // 拉低SCL，准备发送数据
}

// I2C发送停止条件（P：SCL高电平时，SDA从低变高）
static void I2C_Stop(void) 
{
    I2C_SDA_Output();
    I2C_SCL_Low();
    I2C_SDA_Low();
    DelayUs(2);
    I2C_SCL_High();
    DelayUs(2);
    I2C_SDA_High();
    DelayUs(2);
}

// I2C等待从机ACK响应
static HAL_StatusTypeDef I2C_WaitAck(void) 
{
    uint8_t timeout = 0;
    I2C_SDA_Input(); // 切换SDA为输入，等待从机拉低
    I2C_SDA_High();
    DelayUs(1);
    I2C_SCL_High();
    DelayUs(1);
    
    while(I2C_SDA_Read() == 1) 
	{
        timeout++;
        if(timeout > 100) 	 // 超时判定（约100us）
		{
            I2C_SCL_Low();
            return HAL_ERROR;
        }
    }
    I2C_SCL_Low(); // 拉低SCL，结束ACK检测
    return HAL_OK;
}

// I2C发送ACK响应
static void I2C_SendAck(void) 
{
    I2C_SDA_Output();
    I2C_SDA_Low();
    DelayUs(1);
    I2C_SCL_High();
    DelayUs(2);
    I2C_SCL_Low();
    DelayUs(1);
}

// I2C发送NACK响应
static void I2C_SendNack(void) 
{
    I2C_SDA_Output();
    I2C_SDA_High();
    DelayUs(1);
    I2C_SCL_High();
    DelayUs(2);
    I2C_SCL_Low();
    DelayUs(1);
}

// I2C发送1字节数据
static HAL_StatusTypeDef I2C_SendByte(uint8_t data) 
{
    uint8_t i;
    I2C_SDA_Output();
    for(i = 0; i < 8; i++) 
	{
        I2C_SCL_Low();
        DelayUs(1);
        // 高位先发送
        if((data & 0x80) != 0) I2C_SDA_High();
        else I2C_SDA_Low();
        data <<= 1;
        DelayUs(1);
        I2C_SCL_High();
        DelayUs(2);
    }
    I2C_SCL_Low();
    return I2C_WaitAck(); // 等待从机ACK
}

// I2C读取1字节数据（带ACK/NACK控制）
static uint8_t I2C_ReadByte(uint8_t ack) 
{
    uint8_t i, data = 0;
    I2C_SDA_Input();
    for(i = 0; i < 8; i++)
	{
        I2C_SCL_Low();
        DelayUs(2);
        I2C_SCL_High();
        DelayUs(1);
        data <<= 1;
        if(I2C_SDA_Read() == 1) data |= 0x01;
        DelayUs(1);
    }
    I2C_SCL_Low();
    // 发送ACK/NACK
    if(ack == 1) 
    {
		I2C_SendAck();
    }
    else
    {
		I2C_SendNack();
    }
    return data;
}


// 2. MS5837-30BA核心驱动函数
/**
 * @brief  传感器初始化（复位+读取校准系数）
 * @param  dev: 传感器设备结构体指针
 * @param  osr: 过采样率选择
 * @retval HAL状态
 */
HAL_StatusTypeDef MS5837_Init(MS5837_Dev_TypeDef *dev, MS5837_OSR_TypeDef osr) 
{
    uint8_t i;
    uint16_t prom_data;
    
    // 初始化GPIO（SCL/SDA均为推挽输出，初始高电平）
    #if 0
    GPIO_InitTypeDef GPIO_InitStruct = {0};
    GPIO_InitStruct.Pin = SCL_PIN | SDA_PIN;
    GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
    GPIO_InitStruct.Pull = GPIO_NOPULL;
    GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
    HAL_GPIO_Init(I2C_PORT, &GPIO_InitStruct);
	#endif
    I2C_SCL_High();
    I2C_SDA_High();
    vTaskDelay(pdMS_TO_TICKS(10)); // 等待GPIO稳定
    
    // 1. 发送复位命令（）
    I2C_Start();
    if(I2C_SendByte(MS5837_ADDR_WRITE) != HAL_OK) return HAL_ERROR;
    if(I2C_SendByte(MS5837_CMD_RESET) != HAL_OK) return HAL_ERROR;
    I2C_Stop();
    vTaskDelay(pdMS_TO_TICKS(2)); // 复位后需等待≥1ms（）
    
    // 2. 读取PROM校准系数（7个16位数据，地址0~6）（）
    dev->osr = osr;
    for(i = 0; i < 7; i++) 
	{
        I2C_Start();
        if(I2C_SendByte(MS5837_ADDR_WRITE) != HAL_OK) return HAL_ERROR;
        if(I2C_SendByte(MS5837_CMD_PROM_READ + (i << 1)) != HAL_OK) return HAL_ERROR; // 地址左移1位（16位数据）
        
        I2C_Start(); // 重复起始条件
        if(I2C_SendByte(MS5837_ADDR_READ) != HAL_OK) return HAL_ERROR;
        // 读取16位数据（高位+低位）
        prom_data = (I2C_ReadByte(1) << 8) | I2C_ReadByte(0);
        I2C_Stop();
        
        // 存储校准系数
        switch(i)
		{
            case 0: dev->calib.W0 = prom_data; break;
            case 1: dev->calib.W1 = prom_data; break;
            case 2: dev->calib.W2 = prom_data; break;
            case 3: dev->calib.W3 = prom_data; break;
            case 4: dev->calib.W4 = prom_data; break;
            case 5: dev->calib.W5 = prom_data; break;
            case 6: dev->calib.W6 = prom_data; break;
            default: break;
        }
    }
    
    // 可选：CRC校验（参考的CRC-4算法）
    // 此处省略CRC校验，如需添加可调用单独的CRC计算函数
    return HAL_OK;
}

/**
 * @brief  读取原始ADC值（D1=压力，D2=温度）
 * @param  cmd: 转换命令（D1/D2）
 * @param  osr: 过采样率
 * @retval 24位原始ADC值
 */
static uint32_t MS5837_ReadADC(uint8_t cmd, MS5837_OSR_TypeDef osr)
{
    uint32_t adc_data = 0;
    uint8_t conv_time = 0;
    
    // 1. 发送转换命令
    I2C_Start();
    I2C_SendByte(MS5837_ADDR_WRITE);
    I2C_SendByte(cmd);
    I2C_Stop();
    
    // 2. 等待转换完成（根据OSR选择最大转换时间，）
    switch(osr) 
	{
        case MS5837_OSR_256:  conv_time = 1;    break; // 0.6ms→取1ms
        case MS5837_OSR_512:  conv_time = 2;    break; // 1.17ms→取2ms
        case MS5837_OSR_1024: conv_time = 3;    break; // 2.28ms→取3ms
        case MS5837_OSR_2048: conv_time = 5;    break; // 4.54ms→取5ms
        case MS5837_OSR_4096: conv_time = 10;   break; // 9.04ms→取10ms
        case MS5837_OSR_8192: conv_time = 20;   break; // 18.08ms→取20ms
        default: conv_time = 10; break;
    }
    vTaskDelay(pdMS_TO_TICKS(conv_time));
    
    // 3. 读取ADC数据（24位：高8位+中8位+低8位）（）
    I2C_Start();
    I2C_SendByte(MS5837_ADDR_WRITE);
    I2C_SendByte(MS5837_CMD_ADC_READ);
    I2C_Start();
    I2C_SendByte(MS5837_ADDR_READ);
    
    adc_data = (uint32_t)I2C_ReadByte(1) << 16; // 高8位
    adc_data |= (uint32_t)I2C_ReadByte(1) << 8;  // 中8位
    adc_data |= (uint32_t)I2C_ReadByte(0);       // 低8位（最后1字节发NACK）
    I2C_Stop();
    
    return adc_data;
}

/**
 * @brief  读取并计算最终压力与温度（二阶补偿）
 * @param  dev: 传感器设备结构体指针
 * @retval HAL状态
 */
HAL_StatusTypeDef MS5837_ReadData(MS5837_Dev_TypeDef *dev) 
{
    uint32_t D1, D2;
    int64_t dT, temperature, OFF, SENS;
    int64_t Ti = 0, OFFi = 0, SENSi = 0;
    int64_t P;
    
    // 1. 读取原始值（D1=压力，D2=温度）
    D1 = MS5837_ReadADC(MS5837_D1_CMD(dev->osr), dev->osr); // 压力原始值
    D2 = MS5837_ReadADC(MS5837_D2_CMD(dev->osr), dev->osr); // 温度原始值

    // 2. 一阶补偿计算
    dT = (int64_t)D2 - ((int64_t)dev->calib.W5 * 256);                      // 温度偏差
    temperature = 2000 + ((int64_t)dT * (int64_t)dev->calib.W6) / 8388608;  // 一阶温度 (0.01℃)
    OFF = ((int64_t)dev->calib.W2 * 65536) + (((int64_t)dev->calib.W4 * dT) / 128);  // 一阶偏移
    SENS = ((int64_t)dev->calib.W1 * 32768) + (((int64_t)dev->calib.W3 * dT) / 256); // 一阶灵敏度

    // 3. 二阶温度补偿
    if (temperature < 2000)         // 低温补偿 (<20℃)
    { 
        Ti = (3 * dT * dT) / 8589934592LL;
        OFFi = (3 * (temperature - 2000) * (temperature - 2000)) / 8;
        SENSi = (5 * (temperature - 2000) * (temperature - 2000)) / 8;
        
        if (temperature < -1500)    // 极低温追加补偿 (<-15℃)
        {
            OFFi += 7 * (temperature + 1500) * (temperature + 1500);
            SENSi += 4 * (temperature + 1500) * (temperature + 1500);
        }
    } 
    else                           // 高温补偿 (≥20℃)
    { 
        Ti = (2 * dT * dT) / 144115188075855872LL;
        OFFi = (1 * (temperature - 2000) * (temperature - 2000)) / 16;
        SENSi = 0;
    }

    // 4. 应用二阶补偿
    temperature -= Ti;
    OFF -= OFFi;
    SENS -= SENSi;
    
    // 5. 计算最终压力（关键修正：除数改为8192）
    P = (((int64_t)D1 * SENS) / 2097152 - OFF) / 8192;  // 结果单位：0.1mbar
    
    // 6. 转换为实际单位
    dev->temperature = (float)temperature / 100.0f;  // ℃
    dev->pressure = (float)P / 10.0f;                 // mbar (绝对压力)
    
    // 7. 计算表压和水深
    dev->gauge_pressure = dev->pressure - dev->atmospheric_pressure;
    dev->water_depth = MS5837_CalculateDepth(dev);
    
    return HAL_OK;
}

/**
 * @brief  大气压校准函数
 * @param  dev: 传感器设备结构体指针
 * @retval HAL状态
 */
HAL_StatusTypeDef MS5837_CalibrateAtmosphericPressure(MS5837_Dev_TypeDef *dev)
{
    HAL_StatusTypeDef status;
    float pressure_sum = 0;
    uint8_t samples = 10;
    
    printf("开始大气压校准，请确保传感器在空气中...\n");
    
    // 取10次测量的平均值
    for(uint8_t i = 0; i < samples; i++) 
		{
        // 临时读取压力（不更新水深）
        uint32_t D1, D2;
        int64_t dT, temperature, OFF, SENS;
        int64_t Ti = 0, OFFi = 0, SENSi = 0;
        int64_t P;
        
        D1 = MS5837_ReadADC(MS5837_D1_CMD(dev->osr), dev->osr);
        D2 = MS5837_ReadADC(MS5837_D2_CMD(dev->osr), dev->osr);
        
        dT = (int64_t)D2 - ((int64_t)dev->calib.W5 * 256);
        temperature = 2000 + ((int64_t)dT * (int64_t)dev->calib.W6) / 8388608;
        OFF = ((int64_t)dev->calib.W2 * 65536) + (((int64_t)dev->calib.W4 * dT) / 128);
        SENS = ((int64_t)dev->calib.W1 * 32768) + (((int64_t)dev->calib.W3 * dT) / 256);
        
        if (temperature < 2000) 
				{
            Ti = (3 * dT * dT) / 8589934592LL;
            OFFi = (3 * (temperature - 2000) * (temperature - 2000)) / 8;
            SENSi = (5 * (temperature - 2000) * (temperature - 2000)) / 8;
            if (temperature < -1500) {
                OFFi += 7 * (temperature + 1500) * (temperature + 1500);
                SENSi += 4 * (temperature + 1500) * (temperature + 1500);
            }
        } 
				else 
				{
            Ti = (2 * dT * dT) / 144115188075855872LL;
            OFFi = (1 * (temperature - 2000) * (temperature - 2000)) / 16;
            SENSi = 0;
        }
        
        temperature -= Ti;
        OFF -= OFFi;
        SENS -= SENSi;
        P = (((int64_t)D1 * SENS) / 2097152 - OFF) / 8192;
        
        pressure_sum += (float)P / 10.0f;
        vTaskDelay(pdMS_TO_TICKS(100));
    }
    
    dev->atmospheric_pressure = pressure_sum / samples;
    
    printf("大气压校准完成: %.2f mbar\n", dev->atmospheric_pressure);
    return HAL_OK;
}

/**
 * @brief  设置水质类型
 * @param  dev: 传感器设备结构体指针
 * @param  water_type: 水质类型
 */
void MS5837_SetWaterType(MS5837_Dev_TypeDef *dev, MS5837_WaterType_TypeDef water_type)
{
    dev->water_type = water_type;
    if(water_type == MS5837_WATER_FRESH) {
        printf("设置为淡水模式 (密度: %.0f kg/m³)\n", MS5837_FRESH_WATER_DENSITY);
    } else {
        printf("设置为海水模式 (密度: %.0f kg/m³)\n", MS5837_SEA_WATER_DENSITY);
    }
}

/**
 * @brief  手动设置大气压基准值
 * @param  dev: 传感器设备结构体指针
 * @param  pressure_mbar: 大气压值 (mbar)
 */
void MS5837_SetAtmosphericPressure(MS5837_Dev_TypeDef *dev, float pressure_mbar)
{
    dev->atmospheric_pressure = pressure_mbar;
    printf("大气压设置为: %.2f mbar\n", pressure_mbar);
}

/**
 * @brief  计算水深
 * @param  dev: 传感器设备结构体指针
 * @retval 水深 (米)
 */
float MS5837_CalculateDepth(MS5837_Dev_TypeDef *dev)
{
    return MS5837_PressureToDepth(dev->gauge_pressure, dev->water_type);
}

/**
 * @brief  根据表压和水质类型计算水深
 * @param  gauge_pressure_mbar: 表压 (mbar)
 * @param  water_type: 水质类型
 * @retval 水深 (米)
 */
float MS5837_PressureToDepth(float gauge_pressure_mbar, MS5837_WaterType_TypeDef water_type)
{
    // 如果表压为负值，说明在水面上方，返回0
    if (gauge_pressure_mbar <= 0) {
        return 0.0f;
    }
    
    // 转换为帕斯卡 (1 mbar = 100 Pa)
    float gauge_pressure_pa = gauge_pressure_mbar * 100.0f;
    
    // 根据水质选择密度
    float density = (water_type == MS5837_WATER_FRESH) ? 
                    MS5837_FRESH_WATER_DENSITY : MS5837_SEA_WATER_DENSITY;
    
    // 计算水深: P = ρgh → h = P/(ρg)
    float depth = gauge_pressure_pa / (density * MS5837_GRAVITY_ACCELERATION);
    
    return depth;
}

/**
 * @brief  验证校准系数是否有效
 * @param  dev: 传感器设备结构体指针
 * @retval HAL状态
 */
HAL_StatusTypeDef MS5837_ValidateCalibration(MS5837_Dev_TypeDef *dev)
{
    // 检查关键校准系数是否为0或0xFFFF（表示读取失败）
    if(dev->calib.W1 == 0 || dev->calib.W1 == 0xFFFF ||
       dev->calib.W2 == 0 || dev->calib.W2 == 0xFFFF ||
       dev->calib.W5 == 0 || dev->calib.W5 == 0xFFFF ||
       dev->calib.W6 == 0 || dev->calib.W6 == 0xFFFF) {
        printf("错误: 校准系数无效!\n");
        return HAL_ERROR;
    }
    
    printf("校准系数验证通过\n");
    return HAL_OK;
}

/**
 * @brief  打印校准系数
 * @param  dev: 传感器设备结构体指针
 */
void MS5837_PrintCalibration(MS5837_Dev_TypeDef *dev)
{
    printf("=== MS5837 校准系数 ===\n");
    printf("W0 (厂家+CRC): %u (0x%04X)\n", dev->calib.W0, dev->calib.W0);
    printf("W1 (SENS_T1):  %u (0x%04X)\n", dev->calib.W1, dev->calib.W1);
    printf("W2 (OFF_T1):   %u (0x%04X)\n", dev->calib.W2, dev->calib.W2);
    printf("W3 (TCS):      %u (0x%04X)\n", dev->calib.W3, dev->calib.W3);
    printf("W4 (TCO):      %u (0x%04X)\n", dev->calib.W4, dev->calib.W4);
    printf("W5 (T_REF):    %u (0x%04X)\n", dev->calib.W5, dev->calib.W5);
    printf("W6 (TEMPSENS): %u (0x%04X)\n", dev->calib.W6, dev->calib.W6);
    printf("=====================\n");
}

/**
 * @brief  打印传感器数据
 * @param  dev: 传感器设备结构体指针
 */
void MS5837_PrintSensorData(MS5837_Dev_TypeDef *dev)
{
    printf("=== MS5837 传感器数据 ===\n");
    printf("temp: %.2f°C\n", dev->temperature);  // 温度
    printf("pressure: %.2f mbar\n", dev->pressure);	// 绝对压力
    printf("atmospheric_pressure: %.2f mbar\n", dev->atmospheric_pressure);	// 大气压基准
    printf("gauge_pressure: %.2f mbar\n", dev->gauge_pressure);	// 表压
    printf("water_depth: %.3f m\n", dev->water_depth);	// 水深
    printf("water type: %s\n", (dev->water_type == MS5837_WATER_FRESH) ? "淡水" : "海水"); // 水质
    printf("========================\n");
}

// FreeRTOS任务函数
void MS5837_Task(void *argument) 
{
    MS5837_Dev_TypeDef ms5837;
    HAL_StatusTypeDef status;
    uint8_t calibration_done = 0;
    
    // 1. 初始化传感器（保持你原来的初始化函数）
    status = MS5837_Init(&ms5837, MS5837_OSR_4096);
    if (status != HAL_OK) 
    {
        printf("MS5837 初始化失败!\n");
        while(1) {
            osDelay(1000);
        }
    }
    
    // 2. 验证和打印校准系数
    if(MS5837_ValidateCalibration(&ms5837) != HAL_OK) {
        while(1) {
            osDelay(1000);
        }
    }
    MS5837_PrintCalibration(&ms5837);
    
    // 3. 设置默认参数
    MS5837_SetWaterType(&ms5837, MS5837_WATER_FRESH);  // 默认淡水
    MS5837_SetAtmosphericPressure(&ms5837, MS5837_STANDARD_ATMOSPHERIC_PRESSURE);
    
    printf("MS5837 初始化成功! 开始数据采集...\n");
    
    // 4. 主循环
    while(1) 
    {
        status = MS5837_ReadData(&ms5837);
        if (status == HAL_OK) 
        {
            // 更新全局数据
            msg.temp = ms5837.temperature;
            msg.pressure = ms5837.water_depth;
//            msg.water_depth = ms5837.water_depth;  // 如果你的msg结构体有这个字段
            
            // 打印详细数据
            //MS5837_PrintSensorData(&ms5837);
            
            // 首次校准提醒
            if(!calibration_done && ms5837.pressure > 900 && ms5837.pressure < 1100) 
						{
                printf("提示: 如需精确测深，建议调用 MS5837_CalibrateAtmosphericPressure() 进行校准\n");
                calibration_done = 1;
            }
        } 
        else 
        {
            printf("MS5837 读取失败!\n");
        }
        
        osDelay(250); // 200ms采样一次
    }
}

