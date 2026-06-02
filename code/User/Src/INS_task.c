#include "INS_task.h"
#include "BMI088driver.h"
#include "mahony_filter.h"
#include "protocol.h"
#include "spi.h"
#include "main.h"
#include "bsp_dwt.h"
#include <math.h>

#ifndef IMU_HEAT_CONTROL_ENABLE
#define IMU_HEAT_CONTROL_ENABLE 0
#endif

#ifndef HAVE_INS_AXIS
#define X 0
#define Y 1
#define Z 2
#endif

extern SPI_HandleTypeDef hspi2;
extern msgStruct_t msg;

void BodyFrameToEarthFrame(const float *vecBF, float *vecEF, float *q);
void EarthFrameToBodyFrame(const float *vecEF, float *vecBF, float *q);

static struct MAHONY_FILTER_t mahony_filter;
INS_t INS = {0};
static uint32_t INS_DWT_Count;
static float ins_dt = 0.0f;
static float ins_time_ms = 0.0f;
static int stop_time = 0;
static Axis3f Gyro;
static Axis3f Accel;
static const float gravity[3] = {0.0f, 0.0f, 9.81f};

static void INS_Init(void)
{
    mahony_init(&mahony_filter, 1.0f, 0.000f, 0.001f);
    mahony_filter.exInt = 0.0f;
    mahony_filter.eyInt = 0.0f;
    mahony_filter.ezInt = 0.0f;
    INS.AccelLPF = 0.0089f;
    ins_time_ms = 0.0f;
    stop_time = 0;
}

/**
 * @brief Read the BMI088 and feed Mahony filter so msgStruct stays up to date
 */
int i=0;
void INS_task(void *argument)
{
    HAL_GPIO_WritePin(HEAT_GPIO_Port, HEAT_Pin, GPIO_PIN_RESET);
    // DWT_Init expects MHz (see DM-balance: DWT_Init(480))
    DWT_Init(SystemCoreClock / 1000000);
    // Keep CS high and give BMI088 time to power up before init.
    HAL_GPIO_WritePin(SPI2_Accel_CS_GPIO_Port, SPI2_Accel_CS_Pin, GPIO_PIN_SET);
    HAL_GPIO_WritePin(SPI2_Gyro_CS_GPIO_Port, SPI2_Gyro_CS_Pin, GPIO_PIN_SET);
    HAL_Delay(200);
    BMI088_Init(&hspi2, 0);//第一次上电时，改成1进入校准（保持水平静止放置10s左右，会打印offset），读出参数后填上，然后改成0
#if IMU_HEAT_CONTROL_ENABLE
    HAL_GPIO_WritePin(HEAT_GPIO_Port, HEAT_Pin, GPIO_PIN_SET);
#endif
    printf("BMI088 offsets loaded: Gyro=%.6f %.6f %.6f | Accel=%.6f %.6f %.6f\\n",
           BMI088.GyroOffset[0],
           BMI088.GyroOffset[1],
           BMI088.GyroOffset[2],
           BMI088.AccelOffset[0],
           BMI088.AccelOffset[1],
           BMI088.AccelOffset[2]);

    INS_Init();

    const float rad_to_deg = RAD2DEG;

    while (1)
    {
        ins_dt = DWT_GetDeltaT(&INS_DWT_Count);
        mahony_filter.dt = ins_dt;

        BMI088_Read(&BMI088);

        INS.Accel[X] = BMI088.Accel[X];
        INS.Accel[Y] = BMI088.Accel[Y];
        INS.Accel[Z] = BMI088.Accel[Z];
        Accel.x = BMI088.Accel[0];
        Accel.y = BMI088.Accel[1];
        Accel.z = BMI088.Accel[2];

        INS.Gyro[X] = BMI088.Gyro[X];
        INS.Gyro[Y] = BMI088.Gyro[Y];
        INS.Gyro[Z] = BMI088.Gyro[Z];
        Gyro.x = BMI088.Gyro[0];
        Gyro.y = BMI088.Gyro[1];
        Gyro.z = BMI088.Gyro[2];

        mahony_filter.mahony_input(&mahony_filter, Gyro, Accel);
        mahony_filter.mahony_update(&mahony_filter);
        mahony_filter.mahony_output(&mahony_filter);
        mahony_filter.RotationMatrix_update(&mahony_filter);

        // 直接把惯性加速度（m/s²）送给消息系统，后端还可以自行减去重力
        msg.stcAcc[0] = Accel.x;
        msg.stcAcc[1] = Accel.y;
        msg.stcAcc[2] = Accel.z;

        // 角速度将输出为度/秒以保持与上层 PID 协议兼容
        msg.stcGyro[0] = Gyro.x * rad_to_deg;
        msg.stcGyro[1] = Gyro.y * rad_to_deg;
        msg.stcGyro[2] = Gyro.z * rad_to_deg;

        float roll_rad = mahony_filter.roll;
        float pitch_rad = mahony_filter.pitch;
        float yaw_rad = mahony_filter.yaw;
        float roll_deg = roll_rad * rad_to_deg;
        float pitch_deg = pitch_rad * rad_to_deg;
        float yaw_deg = yaw_rad * rad_to_deg;

        msg.stcAngle[0] = roll_deg;
        msg.stcAngle[1] = pitch_deg;
        msg.stcAngle[2] = yaw_deg;

        INS.q[0] = mahony_filter.q0;
        INS.q[1] = mahony_filter.q1;
        INS.q[2] = mahony_filter.q2;
        INS.q[3] = mahony_filter.q3;

        float gravity_b[3];
        EarthFrameToBodyFrame(gravity, gravity_b, INS.q);

        for (uint8_t i = 0; i < 3; i++)
        {
            float alpha = INS.AccelLPF;
            INS.MotionAccel_b[i] = (INS.Accel[i] - gravity_b[i]) * ins_dt / (alpha + ins_dt)
                                  + INS.MotionAccel_b[i] * alpha / (alpha + ins_dt);
        }
        BodyFrameToEarthFrame(INS.MotionAccel_b, INS.MotionAccel_n, INS.q);

        // 给绝对系加速度加死区，避免微小震动影响后续逻辑
        if (fabsf(INS.MotionAccel_n[0]) < 0.02f)
        {
            INS.MotionAccel_n[0] = 0.0f;
        }
        if (fabsf(INS.MotionAccel_n[1]) < 0.02f)
        {
            INS.MotionAccel_n[1] = 0.0f;
        }
        if (fabsf(INS.MotionAccel_n[2]) < 0.04f)
        {
            INS.MotionAccel_n[2] = 0.0f;
            stop_time++;
        }
        else
        {
            stop_time = 0;
        }

        // 稳定 3 秒后才把 Mahony 的姿态输出写入 INS 结构，同时保持航向累加
        if (ins_time_ms > 3000.0f)
        {
            INS.Roll = roll_rad;
            INS.Pitch = pitch_rad;
            INS.Yaw = yaw_rad;
            INS.v_n += INS.MotionAccel_n[1] * ins_dt;
            INS.x_n += INS.v_n * ins_dt;
            INS.ins_flag = 1;

            float yaw_diff = INS.Yaw - INS.YawAngleLast;
            // 姿态角跨越 ±π 时，维护一个整周计数，保持 YawTotalAngle 单调
            if (yaw_diff > 3.1415926f)
            {
                INS.YawRoundCount--;
            }
            else if (yaw_diff < -3.1415926f)
            {
                INS.YawRoundCount++;
            }
            INS.YawTotalAngle = 6.283f * INS.YawRoundCount + INS.Yaw;
            INS.YawAngleLast = INS.Yaw;
        }
        else
        {
            ins_time_ms += ins_dt * 1000.0f;
        }

        msg.stcMag[0] = 0.0f;
        msg.stcMag[1] = 0.0f;
        msg.stcMag[2] = 0.0f;
				
				#if 0
				if(i++ == 100)
				{
					printf("pitch=%.3f,roll=%.3f,yaw=%.3f\r\n",msg.stcAngle[1],msg.stcAngle[0],msg.stcAngle[2]);
					printf("accx=%.3f,accy=%.3f,accz=%.3f\r\n",msg.stcAcc[0],msg.stcAcc[1],msg.stcAcc[2]);
					printf("gyrox=%.3f,gyroy=%.3f,gyroz=%.3f\r\n",msg.stcGyro[0],msg.stcGyro[1],msg.stcGyro[2]);
					i=0;
				}
				#endif
        osDelay(1);
    }
}

/**
 * @brief          Transform 3dvector from BodyFrame to EarthFrame
 * @param[1]       vector in BodyFrame
 * @param[2]       vector in EarthFrame
 * @param[3]       quaternion
 */
void BodyFrameToEarthFrame(const float *vecBF, float *vecEF, float *q)
{
    vecEF[0] = 2.0f * ((0.5f - q[2] * q[2] - q[3] * q[3]) * vecBF[0] +
                       (q[1] * q[2] - q[0] * q[3]) * vecBF[1] +
                       (q[1] * q[3] + q[0] * q[2]) * vecBF[2]);

    vecEF[1] = 2.0f * ((q[1] * q[2] + q[0] * q[3]) * vecBF[0] +
                       (0.5f - q[1] * q[1] - q[3] * q[3]) * vecBF[1] +
                       (q[2] * q[3] - q[0] * q[1]) * vecBF[2]);

    vecEF[2] = 2.0f * ((q[1] * q[3] - q[0] * q[2]) * vecBF[0] +
                       (q[2] * q[3] + q[0] * q[1]) * vecBF[1] +
                       (0.5f - q[1] * q[1] - q[2] * q[2]) * vecBF[2]);
}

/**
 * @brief          Transform 3dvector from EarthFrame to BodyFrame
 * @param[1]       vector in EarthFrame
 * @param[2]       vector in BodyFrame
 * @param[3]       quaternion
 */
void EarthFrameToBodyFrame(const float *vecEF, float *vecBF, float *q)
{
    vecBF[0] = 2.0f * ((0.5f - q[2] * q[2] - q[3] * q[3]) * vecEF[0] +
                       (q[1] * q[2] + q[0] * q[3]) * vecEF[1] +
                       (q[1] * q[3] - q[0] * q[2]) * vecEF[2]);

    vecBF[1] = 2.0f * ((q[1] * q[2] - q[0] * q[3]) * vecEF[0] +
                       (0.5f - q[1] * q[1] - q[3] * q[3]) * vecEF[1] +
                       (q[2] * q[3] + q[0] * q[1]) * vecEF[2]);

    vecBF[2] = 2.0f * ((q[1] * q[3] + q[0] * q[2]) * vecEF[0] +
                       (q[2] * q[3] - q[0] * q[1]) * vecEF[1] +
                       (0.5f - q[1] * q[1] - q[2] * q[2]) * vecEF[2]);
}
