/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * File Name          : freertos.c
  * Description        : Code for freertos applications
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2025 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */

/* Includes ------------------------------------------------------------------*/
#include "FreeRTOS.h"
#include "task.h"
#include "main.h"
#include "cmsis_os.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include "gps.h"
#include "icm45686.h"
#include "ms5837.h"
#include "peripheral_app.h"
#include "protocol.h"
#include "qmc5883p.h"
#include "rov.h"
#include "sgm8198.h"
#include "P30_sonar.h"
#include "INS_task.h"
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */
#if INCLUDE_uxTaskGetStackHighWaterMark
#define STACK_USED_DETECT  
#endif

#ifdef STACK_USED_DETECT
void StackDetect_Task( void * pvParameters );
#define OS_THREAD_ID_TO_TASK_HANDLE(osId)  ((TaskHandle_t)(osId))

#endif

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
/* USER CODE BEGIN Variables */


/* Definitions for rovTask */
osThreadId_t rovTaskId;
const osThreadAttr_t rovTask_attributes = {
  .name = "rovTask",
  .stack_size = 1024 * 2,
  .priority = (osPriority_t) osPriorityAboveNormal,
};
/* Definitions for printTask */
osThreadId_t printTaskId;
const osThreadAttr_t printTask_attributes = {
  .name = "printTask",
  .stack_size = 512 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};

/* Definitions for ADCTask */
osThreadId_t ADCTaskId;
const osThreadAttr_t ADCTask_attributes = {
  .name = "ADCTask",
  .stack_size = 512 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};

/* Definitions for GPSTask */
osThreadId_t GPSTaskId;
const osThreadAttr_t GPSTask_attributes = {
  .name = "GPSTask",
  .stack_size = 1024 * 2,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};

/* Definitions for SDTask */
osThreadId_t SDTaskId;
const osThreadAttr_t SDTask_attributes = {
  .name = "SDTask",
  .stack_size = 1024 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};

/* Definitions for msgTask */
osThreadId_t msgTaskId;
const osThreadAttr_t msgTask_attributes = {
  .name = "msgTask",
  .stack_size = 512 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};

/* Definitions for msgTask */
osThreadId_t StackDetectId;
const osThreadAttr_t StackDetectTask_attributes = {
  .name = "StackDetectTask",
  .stack_size = 512 * 2,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};
/* Definitions for ms5837Task */
osThreadId_t ms5837TaskId;
const osThreadAttr_t ms5837Task_attributes = {
.name = "ms5837Task",
.stack_size = 512 * 4,
.priority = (osPriority_t) osPriorityAboveNormal1,
};
/* Definitions for remote_control_task */
osThreadId_t remote_control_TaskId;
const osThreadAttr_t remote_control_Task_attributes = {
.name = "remote_control_task",
.stack_size = 512 * 4,
.priority = (osPriority_t) osPriorityAboveNormal1,
};
/* Definitions for P30_Task */
osThreadId_t P30_TaskId;
const osThreadAttr_t P30_Task_attributes = {
  .name = "P30_Task",
  .stack_size = 512 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};
/* Definitions for INS_task */
osThreadId_t INS_TaskId;
const osThreadAttr_t INS_Task_attributes = {
  .name = "INS_Task",
  .stack_size = 512 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal1,
};
/* USER CODE END Variables */
/* Definitions for startTask */
osThreadId_t startTaskHandle;
const osThreadAttr_t startTask_attributes = {
  .name = "startTask",
  .stack_size = 1024 * 4,
  .priority = (osPriority_t) osPriorityAboveNormal,
};

/* Private function prototypes -----------------------------------------------*/
/* USER CODE BEGIN FunctionPrototypes */


/* USER CODE END FunctionPrototypes */

void StarTaskEntry(void *argument);

void MX_FREERTOS_Init(void); /* (MISRA C 2004 rule 8.1) */

/**
  * @brief  FreeRTOS initialization
  * @param  None
  * @retval None
  */
void MX_FREERTOS_Init(void) {
  /* USER CODE BEGIN Init */

	SDTaskId  = osThreadNew(sd_task, NULL, &SDTask_attributes);
  	GPSTaskId  = osThreadNew(gps_task, NULL, &GPSTask_attributes);
  	rovTaskId = osThreadNew(StartrovTask, NULL, &rovTask_attributes);
 	printTaskId = osThreadNew(uart8_task, NULL, &printTask_attributes);
  	ADCTaskId = osThreadNew(get_batCV_task, NULL, &ADCTask_attributes);
	msgTaskId = osThreadNew(msg_task, NULL, &msgTask_attributes);
	ms5837TaskId = osThreadNew(MS5837_Task, NULL, &ms5837Task_attributes);
	remote_control_TaskId = osThreadNew(remote_control_task, NULL, &remote_control_Task_attributes);
	P30_TaskId = osThreadNew(P30_Task, NULL, &P30_Task_attributes);
	INS_TaskId = osThreadNew(INS_task, NULL, &INS_Task_attributes);
  /* USER CODE END Init */

  /* USER CODE BEGIN RTOS_MUTEX */
  /* add mutexes, ... */
  /* USER CODE END RTOS_MUTEX */

  /* USER CODE BEGIN RTOS_SEMAPHORES */
  /* add semaphores, ... */
  /* USER CODE END RTOS_SEMAPHORES */

  /* USER CODE BEGIN RTOS_TIMERS */
  /* start timers, add new ones, ... */
  /* USER CODE END RTOS_TIMERS */

  /* USER CODE BEGIN RTOS_QUEUES */
  /* add queues, ... */
  /* USER CODE END RTOS_QUEUES */

  /* Create the thread(s) */
  /* creation of startTask */
  startTaskHandle = osThreadNew(StarTaskEntry, NULL, &startTask_attributes);

  /* USER CODE BEGIN RTOS_THREADS */
  	#ifdef STACK_USED_DETECT
	StackDetectId = osThreadNew(StackDetect_Task, NULL, &msgTask_attributes);
	#endif
  /* USER CODE END RTOS_THREADS */

  /* USER CODE BEGIN RTOS_EVENTS */
  /* add events, ... */
  /* USER CODE END RTOS_EVENTS */

}

/* USER CODE BEGIN Header_StarTaskEntry */
/**
  * @brief  Function implementing the startTask thread.
  * @param  argument: Not used
  * @retval None
  */
/* USER CODE END Header_StarTaskEntry */
void StarTaskEntry(void *argument)
{
  /* USER CODE BEGIN StarTaskEntry */
  osDelay(2000);

  setGPS();

  /* Infinite loop */
  for(;;)
  {
    osDelay(1000);
  }
  /* USER CODE END StarTaskEntry */
}

/* Private application code --------------------------------------------------*/
/* USER CODE BEGIN Application */

#ifdef STACK_USED_DETECT
void StackDetect_Task( void * pvParameters )   
{    
	UBaseType_t uxHighWaterMark;
	TaskHandle_t rovTaskHandle,printTaskHandle,ADCTaskHandle,GPSTaskHandle,SDTaskHandle,
				 msgTaskHandle,startTaskHandle2,currentHandle,ms5837TaskHandle;  // FreeRTOS原生句柄
	while(1)      
	{        
		rovTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(rovTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(rovTaskHandle); 
//		printf("rovTask    free %d\r\n",uxHighWaterMark);
		
		printTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(printTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(printTaskHandle); 
//		printf("printTask  free %d\r\n",uxHighWaterMark);  

		ADCTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(ADCTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(ADCTaskHandle); 
//		printf("ADCTask    free %d\r\n",uxHighWaterMark);  

		GPSTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(GPSTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(GPSTaskHandle); 
//		printf("GPSTask    free %d\r\n",uxHighWaterMark);  

		SDTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(SDTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(SDTaskHandle); 
//		printf("SDTask     free %d\r\n",uxHighWaterMark);  

		msgTaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(msgTaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(msgTaskHandle); 
//		printf("msgTask    free %d\r\n",uxHighWaterMark);  

		ms5837TaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(ms5837TaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(ms5837TaskHandle); 
//		printf("ms5837Task free %d\r\n",uxHighWaterMark); 

		ms5837TaskHandle = OS_THREAD_ID_TO_TASK_HANDLE(ms5837TaskId);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(ms5837TaskHandle); 


		startTaskHandle2 = OS_THREAD_ID_TO_TASK_HANDLE(startTaskHandle);
		uxHighWaterMark = uxTaskGetStackHighWaterMark(startTaskHandle2); 
//		printf("startTask  free %d\r\n",uxHighWaterMark);  

		
		currentHandle = OS_THREAD_ID_TO_TASK_HANDLE(osThreadGetId());
		uxHighWaterMark = uxTaskGetStackHighWaterMark(currentHandle); 
//		printf("curTask    free %d\r\n",uxHighWaterMark);  
		
		osDelay(1000);                                                                
	}    
}
#endif

/* USER CODE END Application */

