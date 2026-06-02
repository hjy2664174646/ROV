#ifndef _GPS_H
#define _GPS_H

#ifdef __cplusplus
extern "C" {
#endif

#include "global_data.h"

void gps_task(void *argument);
void setGPS();

#ifdef __cplusplus
}
#endif

#endif 

