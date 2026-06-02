#ifndef _INS_TASK_H
#define _INS_TASK_H

#include <stdint.h>

typedef struct
{
    float q[4];
    float Gyro[3];
    float Accel[3];
    float MotionAccel_b[3];
    float MotionAccel_n[3];
    float AccelLPF;
    float xn[3];
    float yn[3];
    float zn[3];
    float atanxz;
    float atanyz;
    float Roll;
    float Pitch;
    float Yaw;
    float YawTotalAngle;
    float YawAngleLast;
    float YawRoundCount;
    float v_n;
    float x_n;
    uint8_t ins_flag;
} INS_t;

extern INS_t INS;

void INS_task(void *argument);

#endif
