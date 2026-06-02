#include "BMI088Middleware.h"
#include "main.h"
#include <stdio.h>

SPI_HandleTypeDef *BMI088_SPI;

static volatile uint8_t bmi088_dma_tx;
static volatile uint8_t bmi088_dma_rx;
static volatile uint8_t bmi088_dma_done;

void BMI088_ACCEL_NS_L(void)
{
    HAL_GPIO_WritePin(SPI2_Accel_CS_GPIO_Port, SPI2_Accel_CS_Pin, GPIO_PIN_RESET);
}
void BMI088_ACCEL_NS_H(void)
{
    HAL_GPIO_WritePin(SPI2_Accel_CS_GPIO_Port, SPI2_Accel_CS_Pin, GPIO_PIN_SET);
}

void BMI088_GYRO_NS_L(void)
{
    HAL_GPIO_WritePin(SPI2_Gyro_CS_GPIO_Port, SPI2_Gyro_CS_Pin, GPIO_PIN_RESET);
}
void BMI088_GYRO_NS_H(void)
{
    HAL_GPIO_WritePin(SPI2_Gyro_CS_GPIO_Port, SPI2_Gyro_CS_Pin, GPIO_PIN_SET);
}

uint8_t BMI088_read_write_byte(uint8_t txdata)
{
    uint8_t rx_data = 0;
#if BMI088_USE_SPI_DMA
    bmi088_dma_tx = txdata;
    bmi088_dma_rx = 0;
    bmi088_dma_done = 0;
    HAL_StatusTypeDef dma_status = HAL_SPI_TransmitReceive_DMA(BMI088_SPI, (uint8_t *)&bmi088_dma_tx, (uint8_t *)&bmi088_dma_rx, 1);
    printf("[BMI088 DMA] start status=%d err=%lu\n", dma_status, HAL_SPI_GetError(BMI088_SPI));
    if (dma_status != HAL_OK)
    {
        printf("[BMI088 DMA] transmit fail status=%d error=%lu\n", dma_status, HAL_SPI_GetError(BMI088_SPI));
        return 0xFF;
    }
    uint32_t timeout = 0x2000;
    while (bmi088_dma_done == 0 && timeout != 0)
    {
        __NOP();
        timeout--;
    }
    if (timeout == 0)
    {
        printf("[BMI088 DMA] timeout waiting for callback\n");
        return 0xFF;
    }
    return (uint8_t)bmi088_dma_rx;
#else
    HAL_SPI_TransmitReceive(BMI088_SPI, &txdata, &rx_data, 1, 1000);
    return rx_data;
#endif
}

void HAL_SPI_TxRxCpltCallback(SPI_HandleTypeDef *hspi)
{
    if (hspi == BMI088_SPI)
    {
        bmi088_dma_done = 1;
    }
}

void HAL_SPI_ErrorCallback(SPI_HandleTypeDef *hspi)
{
    if (hspi == BMI088_SPI)
    {
        bmi088_dma_done = 1;
        printf("[BMI088 DMA] Error callback err=%lu\n", HAL_SPI_GetError(hspi));
    }
}
