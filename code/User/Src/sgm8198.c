/* Includes ------------------------------------------------------------------*/

#include "sgm8198.h"
#include "protocol.h"
/******************************************************************************************/

extern ADC_HandleTypeDef hadc1;
extern uint16_t BAT_Cur_Value;
extern msgStruct_t msg;


uint8_t ADC1_flag = 0;			/* 电池电压电流采样完成标识 */
uint16_t ADC_Value[100];

void get_batCV_task(void *argument)
{
	uint8_t i ;
	uint32_t ad1,ad2 ;
	float voltage,current;
	while (1)
	{
		
		osDelay(500) ;
		for(i = 0,ad1 = 0, ad2 = 0; i<100;)
		{
				ad1 += ADC_Value[i++] ;
				ad2 += ADC_Value[i++] ;
		}
		ad1 /=50 ;
	  ad2 /=50 ;
		
//		printf("\r\n******** ADC DMA Example ********\r\n\r\n");
//		printf("adc1 = %d\r\n",ad1);
		voltage = (float)ad1 * 3.3 * 13 / 4096 ;
		msg.voltage = voltage;
//		printf("BAT vol = %.2f V \r\n",voltage);
		

//		printf("adc2 = %d\r\n",ad2);
		current = (float)(ad2 *  3.3 / 4096 - 2.5)* 1000 / 50;
		msg.current = current;
//		printf("BAT cur = %.2f A \r\n",current);
	}
}

