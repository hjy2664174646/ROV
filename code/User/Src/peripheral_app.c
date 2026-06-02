#include "peripheral_app.h"
#include "protocol.h"

/* 加入以下代码, 支持printf函数, 而不需要选择 use MicroLIB */

#if 1
#if (__ARMCC_VERSION >= 6010050)            /* 使用AC6编译器时 */
__asm(".global __use_no_semihosting\n\t");  /* 声明不使用半主机模式 */
__asm(".global __ARM_use_no_argv \n\t");    /* AC6下需要声明main函数为无参数格式，否则部分例程可能出现半主机模式 */

#else
/* 使用AC5编译器时, 要在这里定义__FILE 不使用半主机模式 */
#pragma import(__use_no_semihosting)

struct __FILE
{
    int handle;
    /* Whatever you require here. If the only file you are using is */
    /* standard output using printf() for debugging, no file handling */
    /* is required. */
};

#endif

/* 不使用半主机模式，至少需要重定义_ttywrch\_sys_exit\_sys_command_string函数,以同时兼容AC6和AC5模式 */
int _ttywrch(int ch)
{
    ch = ch;
    return ch;
}

/* 定义_sys_exit()以避免使用半主机模式 */
void _sys_exit(int x)
{
    x = x;
}

char *_sys_command_string(char *cmd, int len)
{
    return NULL;
}

/* FILE 在 stdio.h里面定义. */
FILE __stdout;

/* 重定义fputc函数, printf函数最终会通过调用fputc输出字符串到串口 */
int fputc(int ch, FILE *f)
{
    HAL_UART_Transmit(&huart8,(uint8_t *)&ch,1,10);
    return ch;
}
#endif

int rovduty;
SemaphoreHandle_t get_Duty_semaphore;
UART_REC userUart;

extern msgStruct_t msg;
extern uint8_t msgArr[38 * 4]; 

HAL_SD_CardInfoTypeDef  SDCardInfo;           
extern SD_HandleTypeDef hsd;


void usart_init(void)
{
	HAL_UARTEx_ReceiveToIdle_DMA(&huart2, userUart.userUart2.BuffTemp,sizeof(userUart.userUart2.BuffTemp));	// 开启DMA空闲中断
	HAL_UARTEx_ReceiveToIdle_DMA(&huart3, userUart.userUart3.BuffTemp,sizeof(userUart.userUart3.BuffTemp));	// 开启DMA空闲中断
	HAL_UARTEx_ReceiveToIdle_DMA(&huart4, userUart.userUart4.BuffTemp,sizeof(userUart.userUart4.BuffTemp)); 	// 开启DMA空闲中断
	HAL_UARTEx_ReceiveToIdle_DMA(&huart7, userUart.userUart7.BuffTemp,sizeof(userUart.userUart7.BuffTemp)); 	// 开启DMA空闲中断
	HAL_UARTEx_ReceiveToIdle_DMA(&huart8, userUart.userUart8.BuffTemp,sizeof(userUart.userUart8.BuffTemp)); 	// 开启DMA空闲中断
}

/** 
  * 函数功能: 通过print串口接收消息,显示并解析消息 				
  * 
  *功能描述: 	暂定消息只解析接收到"set"开头的消息,格式为:"set,duty"
  *				duty为占空比,只能为整数,范围:-100~100,其中0对应的是1.5ms的占空比,此时不转
  *				消息解析后,根据x值,转发给不同的串口,消息以","号为分割符,方便解析
  *				实测空转:正转需要设置12及以上,反转需要-10及以下,但是正转起转后,设置10也可以转
**/ 

void uart8_task(void *argument)
{
	int i,getHead = 0;
	uint16_t recv_cal_sum = 0,send_cal_sum = 0,recv_sum,recv_len_data,recv_len;
	char *str,*token,print_cmd[256] = {0};  
    const char delim[] = ","; 
	  
	while(1)
	{
		//等待接收队列数据
		if (userUart.userUart8.ReceiveNum > 0)
		{
			recv_len = userUart.userUart8.ReceiveNum;
			#if 0
			printf("U8:");
			#if 1
			printf("%s",userUart.userUart8.ReceiveData);
			#else
			for(i = 0; i < userUart.userUart8.ReceiveNum;i++)
				printf("0x%02x ",userUart.userUart8.ReceiveData[i]);
			printf("\r\n");
				
			#endif
			#endif
			str = userUart.userUart8.ReceiveData;
		  token = strtok(str, delim);   	// 第一次调用 strtok，传入要分割的字符串和分隔符
		  while (token != NULL) 			// 循环调用 strtok，传入 NULL 和分隔符，直到没有更多的标记 
			{  
				if(getHead == 0)
				{
					if(strcmp(token,"set") == 0)
					{
						printf("%s\n", token);  
			      token = strtok(NULL, delim); // 第二次调用 strtok , token 指向 duty
						getHead = 1;
					}
					else
					{
						break;
					}
				}
				else if(getHead == 1)
				{
					printf("motor_duty is = %s\n", token);  
					rovduty = atoi(token);
		      token = strtok(NULL, delim); 
					getHead = 2;
				}
		  }
			if(getHead == 2)
			{
//				if(rovduty >= -100 && rovduty <= 100)
//				{
//					rovduty = 300 - rovduty;
//				}
//				else 
//				{
//					rovduty = 300;
//				}

				getHead = 0;
				memset(print_cmd, 0, 256);
				xSemaphoreGive(get_Duty_semaphore);	/* 发送信号量,调用设置占空比调速任务 */
			}
			
			userUart.userUart8.ReceiveNum = 0; 
		}
		osDelay(1000);
	}
}

FATFS fs; //工作空间
FIL fil; // 文件项目

void printf_sdcard_info(void)
{
	uint64_t CardCap;      	//SD卡容量
	HAL_SD_CardCIDTypeDef SDCard_CID; 

	HAL_SD_GetCardCID(&hsd,&SDCard_CID);	//获取CID
	HAL_SD_GetCardInfo(&hsd,&SDCardInfo);                    //获取SD卡信息
	CardCap=(uint64_t)(SDCardInfo.LogBlockNbr)*(uint64_t)(SDCardInfo.LogBlockSize);	//计算SD卡容量
	switch(SDCardInfo.CardType)
	{
		case CARD_SDSC:
		{
			if(SDCardInfo.CardVersion == CARD_V1_X)
				printf("Card Type:SDSC V1\r\n");
			else if(SDCardInfo.CardVersion == CARD_V2_X)
				printf("Card Type:SDSC V2\r\n");
		}
		break;
		case CARD_SDHC_SDXC:printf("Card Type:SDHC\r\n");break;
		default:break;
	}	
		
    printf("Card ManufacturerID: %d \r\n",SDCard_CID.ManufacturerID);				//制造商ID	
 	printf("CardVersion:         %d \r\n",(uint32_t)(SDCardInfo.CardVersion));		//卡版本号
	printf("Class:               %d \r\n",(uint32_t)(SDCardInfo.Class));		    //
 	printf("Card RCA(RelCardAdd):%d \r\n",SDCardInfo.RelCardAdd);					//卡相对地址
	printf("Card BlockNbr:       %d \r\n",SDCardInfo.BlockNbr);						//块数量
 	printf("Card BlockSize:      %d \r\n",SDCardInfo.BlockSize);					//块大小
	printf("LogBlockNbr:         %d \r\n",(uint32_t)(SDCardInfo.LogBlockNbr));		//逻辑块数量
	printf("LogBlockSize:        %d \r\n",(uint32_t)(SDCardInfo.LogBlockSize));		//逻辑块大小
	printf("Card Capacity:       %d MB\r\n",(uint32_t)(CardCap>>20));				//卡容量
}
int InitFatFs(void)
{
	int retSD,retry_count = 0;
	while(1)
	{
		retSD = f_mount(&fs,"0:",1);	//SD卡挂载

    	printf("stat=%d\r\n",retSD);
		
	    if(retSD == 0) 
	    {
			printf("磁盘挂载成功\r\n");
			break;
	    }	
		else
		{
			retry_count++;
			printf("磁盘挂载失败,重试第%d次\r\n",retry_count);
			if(retry_count >= 10)
			{
				retry_count = 0;
				f_mount(NULL,"0:",1);	//SD卡卸载
				break;
			}
			osDelay(500);
		}
	  
	}
	return retSD;
}

int creat_file(char * filename)
{
    int retSD = f_open(&fil, filename, FA_READ | FA_WRITE | FA_OPEN_APPEND); //打开文件，权限包括创建、写（如果没有该文件，会创建该文件）,每次写都写到文件尾
//    if(retSD == FR_OK)
//    {
//		printf("\r\ncreater file sucess!!! \r\n");
//    }
//    else 
//   	{
//		printf("\r\ncreater file error : %d\r\n",retSD);
//    }
	return retSD;
    //f_close(&fil); //关闭该文件
    //HAL_Delay(100);
}
void write_file(char * data,uint32_t len)
{
    uint32_t byteswritten;
    /*##-3- Write data to the text files ###############################*/
    int retSD = f_write(&fil, data, len, (void *)&byteswritten);
//    if(retSD)
//   	{
//        printf(" write file error : %d\r\n",retSD);
//    }
//    else
//    {
//        printf(" write file sucess!!!,len= [%d]\r\n",byteswritten);
//    }
	#if 0
    /*##-4- Close the open text files ################################*/
    retSD = f_close(&fil);
//    if(retSD)
//        printf(" close error : %d\r\n",retSD);
//    else
//        printf(" close sucess!!! \r\n");
	#endif
}

HAL_StatusTypeDef Seek_file()
{
	FRESULT res;
	// 将文件指针移动到文件末尾（避免覆盖历史数据）
	res = f_lseek(&fil, f_size(&fil));
	if (res != FR_OK) 
	{
//		printf("File Seek Failed! Error: %d\r\n", res);
		f_close(&fil);
		f_mount(NULL,"0:",1);	//SD卡卸载
		return HAL_ERROR;
	}
	else
	{
		return HAL_OK;
	}
}

void sd_task(void *argument)
{
	uint8_t write_cnt = 0,retry_cnt = 0;
	uint16_t i;
	FRESULT res;
	int retSD;
	char filename[] = "test.txt";
	
	uint8_t writeMsg[TOTAL_WORD * 20 + 1] = {0};
	#if TEST_SD

	#else
	printf_sdcard_info();
	SDInit:if(InitFatFs() == 0)
	{
		retSD = creat_file(filename);
	}
	#endif
	osDelay(5000);		/* 延时一会,等待其他传感器工作再写 */
	
	while(1)
	{
		#if TEST_SD
		for(i = 0; i < TOTAL_WORD * 4 - 1; i++)
		{
			sprintf(writeMsg + i * 5,"0x%02x,",msgArr[i]);			/* 写数据 */
		}
		sprintf(writeMsg + ((TOTAL_WORD * 4 ) - 1) * 5,"0x%02x\r\n",msgArr[TOTAL_WORD * 4 - 1]);	/* 写最后的一个字节的数据 */
		
		printf("%s:%s",__FUNCTION__,writeMsg);
		osDelay(20);
		
		#else
		if(retSD == 0)
		{
			for(i = 0; i < TOTAL_WORD * 4 - 1; i++)
			{
				sprintf(writeMsg + i * 5,"0x%02x,",msgArr[i]);			/* 写数据 */
			}
			sprintf(writeMsg + ((TOTAL_WORD * 4 ) - 1) * 5,"0x%02x\r\n",msgArr[TOTAL_WORD * 4 - 1]);	/* 写最后的一个字节的数据 */
			
			if(Seek_file() == HAL_OK)
			{
				write_file(writeMsg,sizeof(writeMsg));

				write_cnt++;
				if (write_cnt >= SYNC_INTERVAL_TIMES) 
				{
			        res = f_sync(&fil); // 同步缓存到SD卡物理扇区
			        if (res == FR_OK) 
					{
			            write_cnt = 0; // 重置计数器
			            //printf("Sync OK\r\n", res);
			        } 
					else 
					{
			            printf("Sync Failed! Error: %d\r\n", res);
			            // 同步失败不影响后续写入，仅记录错误
			        }
			    }
				retry_cnt = 0;
			}
			else
			{
				osDelay(1000);
				retry_cnt++;
				if(retry_cnt < 10)
				{
					goto SDInit;
				}
			}
			
			osDelay(20);
		}
		else
		{
//			printf("SD creat file error = %d\r\n",retSD);
			osDelay(1000);
			retSD = creat_file(filename);
		}
		#endif
	}
}


/******************************************************************************
 * 函  数： HAL_UARTEx_RxEventCallback
 * 功  能： DMA+空闲中断回调函数
 * 参  数： UART_HandleTypeDef  *huart   // 触发的串口
 *          uint16_t             Size    // 接收字节
 * 返回值： 无
 * 备  注： 1：这个是回调函数，不是中断服务函数,使用技巧：使用CubeMX生成的工程中，中断服务函数已被CubeMX安排妥当，我们只管重写回调函数
 *          2：触发条件：当DMA接收到指定字节数时，或产生空闲中断时，硬件就会自动调用本回调函数，无需进行人工调用;
 *          2：必须使用这个函数名称，因为它在CubeMX生成时，已被写好了各种函数调用,此函数弱定义(在stm32xx_hal_uart.c的底层); 不要在原弱定义中增添代码，这里是重写本函数
 *          3：无需进行中断标志的清理，它在被调用前，已有清中断的操作;
 *          4：生成的所有DMA+空闲中断服务函数，都会统一调用这个函数，以引脚编号作参数
 *          5：判断参数传进来的引脚编号，即可知道是哪个串口接收收了多少字节
******************************************************************************/
void HAL_UARTEx_RxEventCallback(UART_HandleTypeDef *huart, uint16_t Size)
{
    if (huart == &huart2)                                                                    // 判断串口
    {
        __HAL_UNLOCK(huart);                                                                 // 解锁串口状态
        userUart.userUart2.ReceiveNum  = Size;                                               // 把接收字节数，存入结构体xUSART1.ReceiveNum，以备使用
		memset(userUart.userUart2.ReceiveData, 0, sizeof(userUart.userUart2.ReceiveData));                     // 清0前一帧的接收数据
		//SCB_InvalidateDCache();
		//SCB_InvalidateDCache_by_Addr(userUart.userUart2.BuffTemp,Size);
		memcpy(userUart.userUart2.ReceiveData, userUart.userUart2.BuffTemp, Size);                            // 把新数据，从临时缓存中，复制到xUSART1.ReceiveData[], 以备使用
        HAL_UARTEx_ReceiveToIdle_DMA(&huart2, userUart.userUart2.BuffTemp, sizeof(userUart.userUart2.BuffTemp));   // 再次开启DMA空闲中断; 每当接收完指定长度，或者产生空闲中断时，就会来到这里
    }
	if (huart == &huart3)                                                                    
    {
        __HAL_UNLOCK(huart);                                                                
        userUart.userUart3.ReceiveNum  = Size;                                               
		memset(userUart.userUart3.ReceiveData, 0, sizeof(userUart.userUart3.ReceiveData));                   
		//SCB_InvalidateDCache_by_Addr(userUart.userUart3.BuffTemp,Size);
		memcpy(userUart.userUart3.ReceiveData, userUart.userUart3.BuffTemp, Size); 
        HAL_UARTEx_ReceiveToIdle_DMA(&huart3, userUart.userUart3.BuffTemp, sizeof(userUart.userUart3.BuffTemp));   
    }
	else if (huart == &huart4)                                                             
    {	
        __HAL_UNLOCK(huart);                                                                
        userUart.userUart4.ReceiveNum  = Size;                                               
        memset(userUart.userUart4.ReceiveData, 0, sizeof(userUart.userUart4.ReceiveData));                         
        //SCB_InvalidateDCache_by_Addr(userUart.userUart4.BuffTemp,Size);
		memcpy(userUart.userUart4.ReceiveData, userUart.userUart4.BuffTemp, Size);                                 
        HAL_UARTEx_ReceiveToIdle_DMA(&huart4, userUart.userUart4.BuffTemp, sizeof(userUart.userUart4.BuffTemp));     
    }
	else if (huart == &huart7)                                                                   
    {

        __HAL_UNLOCK(huart);                                                              
        userUart.userUart7.ReceiveNum  = Size;                                               
        memset(userUart.userUart7.ReceiveData, 0, sizeof(userUart.userUart7.ReceiveData));                        
        //SCB_InvalidateDCache_by_Addr(userUart.userUart7.BuffTemp,Size);
		memcpy(userUart.userUart7.ReceiveData, userUart.userUart7.BuffTemp, Size); 
        HAL_UARTEx_ReceiveToIdle_DMA(&huart7, userUart.userUart7.BuffTemp, sizeof(userUart.userUart7.BuffTemp));   
    }
	else if (huart == &huart8)  
	{		
        __HAL_UNLOCK(huart);                                                                
		userUart.userUart8.ReceiveNum  = Size;                                               
        memset(userUart.userUart8.ReceiveData, 0, sizeof(userUart.userUart8.ReceiveData));                         
        //SCB_InvalidateDCache_by_Addr(userUart.userUart8.BuffTemp,Size);
		memcpy(userUart.userUart8.ReceiveData, userUart.userUart8.BuffTemp, Size);                                 
        HAL_UARTEx_ReceiveToIdle_DMA(&huart8, userUart.userUart8.BuffTemp, sizeof(userUart.userUart8.BuffTemp));  
    }
}

#if 0
void HAL_ADC_ConvCpltCallback(ADC_HandleTypeDef* hadc)
{
	if(hadc == &hadc1)
	{
		ADC1_flag=1;
		HAL_ADC_Stop_DMA(hadc);
	}
}

#endif

