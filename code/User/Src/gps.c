/* Includes ------------------------------------------------------------------*/
#include "gps.h"
#include "minmea.h"
#include "peripheral_app.h"
#include "protocol.h"
#include <math.h>

extern msgStruct_t msg;
extern UART_REC userUart;
static uint8_t i = 0;

static void encode_gps_coordinate(double value, int32_t *integer_part, int32_t *fractional_part)
{
    double abs_value = fabs(value);
    int32_t degrees = (int32_t)floor(abs_value);
    double fraction = abs_value - degrees;
    int32_t scaled = (int32_t)round(fraction * 1000000.0);
    if (scaled >= 1000000)
    {
        scaled -= 1000000;
        degrees += 1;
    }
    int sign = (value < 0.0) ? -1 : 1;
    if (sign < 0 && degrees == 0 && scaled > 0)
    {
        *integer_part = 0;
        *fractional_part = -scaled;
    }
    else
    {
        *integer_part = degrees * sign;
        *fractional_part = scaled * sign;
    }
}

/******************************************************************************************/
int parse_nmea(const char *gnssdata,uint16_t len)
{
	static int count = 0;		/* 定位计数 */
	//uint8_t *modifyData = mymalloc(SRAM12, 100);
    switch (minmea_sentence_id(gnssdata, false))
    {
  
    case MINMEA_SENTENCE_RMC:
    {
        struct minmea_sentence_rmc frame;
		
        if (minmea_parse_rmc(&frame, gnssdata))
        {
        	#if 0
            printf("$xxRMC: raw coordinates and speed: (%d/%d,%d/%d) %d.%d,course:%d,date:%d-%d-%d,time:%d:%d:%d.%d,variation:%d.%d,valid:%d\r\n",
                             frame.latitude.value, frame.latitude.scale,
                             frame.longitude.value, frame.longitude.scale,
                             frame.speed.value, frame.speed.scale,
                             frame.course.value,frame.course.scale,
                             frame.date.year,frame.date.month,frame.date.day,
                             frame.time.hours,frame.time.minutes,frame.time.seconds,frame.time.microseconds,
                             frame.variation.value,frame.variation.scale,frame.valid);
			#endif
			if(frame.latitude.value > 1 && frame.longitude.value > 1)// && gnss_init_step >= 1)		/* 经纬度要有实际的值才赋值 */
			{
				msg.timestamp = (float)(frame.time.hours * 3600 + frame.time.minutes * 60 + frame.time.seconds + (float)(frame.time.microseconds / 1000));
				double lat_value = minmea_tocoord(&frame.latitude);
				double lon_value = minmea_tocoord(&frame.longitude);
				encode_gps_coordinate(lat_value, &msg.GPS.Lat_z, &msg.GPS.Lat_x);
				encode_gps_coordinate(lon_value, &msg.GPS.Lon_z, &msg.GPS.Lon_x);
				msg.GPS.speed = (frame.speed.value + (float)(frame.speed.scale / 1000)) * 1852 / 3600;	/* 节转换成m/s */
			}
        }
        else
        {
            printf("$xxRMC sentence is not parsed\r\n");
        }
    }
    break;
    case MINMEA_SENTENCE_GGA:
    {
        struct minmea_sentence_gga frame;
        if (minmea_parse_gga(&frame, gnssdata))
        {
            printf("$xxGGA: fix quality: %d\r\n", frame.fix_quality);
			if(frame.latitude.value > 1 && frame.longitude.value > 1)		/* 经纬度要有实际的值才赋值 */
			{
				msg.GPS.height = frame.height.value + (float)(frame.height.scale) / 100;
			}
        }
        else
        {
            printf("$xxGGA sentence is not parsed\r\n");
        }
    }
    break;
	case MINMEA_SENTENCE_GSA:
    {
        struct minmea_sentence_gsa frame;
        if (minmea_parse_gsa(&frame, gnssdata))
        {
			if(frame.hdop.value != 99 && frame.hdop.scale != 99)		
			{
				msg.gps_status = (frame.hdop.value + (float)(frame.hdop.scale / 100)) / 100;
			}
        }
        else
        {
            printf("$xxGSA sentence is not parsed\r\n");
        }
    }
    break;
	#if 0
    case MINMEA_SENTENCE_GST:
    {
        struct minmea_sentence_gst frame;
        if (minmea_parse_gst(&frame, gnssdata))
        {
            printf("$xxGST: raw latitude,longitude and altitude error deviation: (%d/%d,%d/%d,%d/%d)\r\n",
                             frame.latitude_error_deviation.value, frame.latitude_error_deviation.scale,
                             frame.longitude_error_deviation.value, frame.longitude_error_deviation.scale,
                             frame.altitude_error_deviation.value, frame.altitude_error_deviation.scale);
            printf("$xxGST fixed point latitude,longitude and altitude error deviation\r\n"
                                           " scaled to one decimal place: (%d,%d,%d)\r\n",
                             minmea_rescale(&frame.latitude_error_deviation, 10),
                             minmea_rescale(&frame.longitude_error_deviation, 10),
                             minmea_rescale(&frame.altitude_error_deviation, 10));
            printf("$xxGST floating point degree latitude, longitude and altitude error deviation: (%f,%f,%f)\r\n",
                             minmea_tofloat(&frame.latitude_error_deviation),
                             minmea_tofloat(&frame.longitude_error_deviation),
                             minmea_tofloat(&frame.altitude_error_deviation));
        }
        else
        {
            printf("$xxGST sentence is not parsed\r\n");
        }
    }
    break;
    case MINMEA_SENTENCE_GSV:
    {
        struct minmea_sentence_gsv frame;
        if (minmea_parse_gsv(&frame, gnssdata))
        {
            printf("$xxGSV: message %d of %d\r\n", frame.msg_nr, frame.total_msgs);
            printf("$xxGSV: satellites in view: %d\r\n", frame.total_sats);
            for (int i = 0; i < 4; i++)
                printf("$xxGSV: sat nr %d, elevation: %d, azimuth: %d, CN: %d db\r\n",
                                 frame.sats[i].nr,
                                 frame.sats[i].elevation,
                                 frame.sats[i].azimuth,
                                 frame.sats[i].snr);
        }
        else
        {
            printf("$xxGSV sentence is not parsed\r\n");
        }
    }
    break;
    case MINMEA_SENTENCE_VTG:
    {
        struct minmea_sentence_vtg frame;
        if (minmea_parse_vtg(&frame, gnssdata))
        {
            printf("$xxVTG: true track degrees = %f\r\n",
                             minmea_tofloat(&frame.true_track_degrees));
            printf("        magnetic track degrees = %f\r\n",
                             minmea_tofloat(&frame.magnetic_track_degrees));
            printf("        speed knots = %f\r\n",
                             minmea_tofloat(&frame.speed_knots));
            printf("        speed kph = %f\r\n",
                             minmea_tofloat(&frame.speed_kph));
        }
        else
        {
            printf("$xxVTG sentence is not parsed\r\n");
        }
    }
    break;
    case MINMEA_SENTENCE_ZDA:
    {
        struct minmea_sentence_zda frame;
        if (minmea_parse_zda(&frame, gnssdata))
        {
            printf("$xxZDA: %d:%d:%d %02d.%02d.%d UTC%+03d:%02d\r\n",
                             frame.time.hours,
                             frame.time.minutes,
                             frame.time.seconds,
                             frame.date.day,
                             frame.date.month,
                             frame.date.year,
                             frame.hour_offset,
                             frame.minute_offset);
        }
        else
        {
            printf("$xxZDA sentence is not parsed\r\n");
        }
    }
    break;
    case MINMEA_INVALID:
    {
        printf("$xxxxx sentence is not valid\r\n");
        return -1;
    }
    break;
	#endif
    default:
    {
        //printf("$xxxxx sentence is not parsed\r\n");
    }
    break;
    }
    return 0;
}

unsigned char getXorRet(const unsigned char *pData, unsigned int Length) 
{ 
    unsigned char result = 0; 
    unsigned int i = 0; 
 
    if((NULL == pData) || (Length < 1)) 
    { 
        return 0; 
    } 
    for(i = 0; i < Length; i++) 
    { 
        result ^= *(pData + i); 
    } 
 
    return result; 
}

static void setLocaConfig(uint8_t conf)
{
	uint8_t checksum;
	uint8_t checksum_s[5],fre_s[3] = "\0";
	uint8_t cmd[] = "PCAS03,";
	uint8_t cmd_send[64]="\0";

	strcat(cmd_send,"$PQTMCFGCNST,W,");				/* 拼接起始符 */
	
	if(conf & 0x01)
	{
		strcat(cmd_send,"1,");					/* 拼接GPS配置 */
	}
	else
	{
		strcat(cmd_send,"0,");					/* 拼接GPS配置 */
	}
	if(conf & 0x02)
	{
		strcat(cmd_send,"1,");					/* 拼接GLONASS配置 */
	}
	else
	{
		strcat(cmd_send,"0,");					/* 拼接GLONASS配置 */
	}
	if(conf & 0x04)
	{
		strcat(cmd_send,"1,");					/* 拼接Galileo配置 */
	}
	else
	{
		strcat(cmd_send,"0,");					/* 拼接Galileo配置 */
	}
	if(conf & 0x08)
	{
		strcat(cmd_send,"1,");					/* 拼接BDS配置 */
	}
	else
	{
		strcat(cmd_send,"0,");					/* 拼接BDS配置 */
	}
		if(conf & 0x10)
	{
		strcat(cmd_send,"1,");					/* 拼接QZSS配置 */
	}
	else
	{
		strcat(cmd_send,"0,");					/* 拼接QZSS配置 */
	}
	if(conf & 0x20)
	{
		strcat(cmd_send,"1");					/* 拼接NavIC配置 */
	}
	else
	{
		strcat(cmd_send,"0");					/* 拼接NavIC配置 */
	}
	
	checksum = getXorRet(cmd_send + 1,strlen(cmd_send) - 1);	/* 计算校验值 */	
	sprintf(checksum_s,"*%02X\r\n",checksum);	/* 校验结果加间隔符和结束符转为字符串 */
	strcat(cmd_send,checksum_s);				/* 拼接校验值等 */
	
	HAL_UART_Transmit(&huart4,cmd_send,strlen(cmd_send),100	);		/* 下发指令 */
	printf("%s",cmd_send);			/* 打印提示 */
}

static void setLocaOutput(uint8_t type,uint8_t fre)
{
	uint8_t checksum;
	uint8_t checksum_s[5],fre_s[3] = {0};
	uint8_t cmd_send[64]= {0};
	
	strcat(cmd_send,"$PQTMCFGMSGRATE,W,");		/* 拼接起始符 */
	switch (type)
	{
		case 0:		/* RMC */
			strcat(cmd_send,"RMC,");				/* 拼接输出格式 */
			break;
		case 1:		/* GGA */
			strcat(cmd_send,"GGA,");				/* 拼接输出格式 */
			break;
		case 2:		/* GSV */
			strcat(cmd_send,"GSV,");				/* 拼接输出格式 */
			break;
		case 3:		/* GSA */
			strcat(cmd_send,"GSA,");				/* 拼接输出格式 */
			break;
		case 4:		/* VTG */
			strcat(cmd_send,"VTG,");				/* 拼接输出格式 */
			break;
		case 5:		/* GLL */
			strcat(cmd_send,"GLL,");				/* 拼接输出格式 */
			break;
		case 6:		/* PQTMTAR */
			strcat(cmd_send,"PQTMTAR,");			/* 拼接输出格式 */
			break;
		case 7:		/* PQTMANTENNASTATUS */
			strcat(cmd_send,"PQTMANTENNASTATUS,");	/* 拼接输出格式 */
			break;
		default:break;
	}
	
	sprintf(fre_s,"%d",fre);						/* 频率数值转为字符串 */	
	strcat(cmd_send,fre_s);							/* 拼接频率字段 */

	if(type == 6)
	{
		strcat(cmd_send,",1"); 						/* 拼接语句版本 */
	}
	if(type == 7)
	{
		strcat(cmd_send,",2"); 						/* 拼接语句版本 */
	}
	
	checksum = getXorRet(cmd_send + 1,strlen(cmd_send) - 1);	/* 计算校验值 */	
	sprintf(checksum_s,"*%02X\r\n",checksum);	/* 校验结果加间隔符和结束符转为字符串 */
	strcat(cmd_send,checksum_s);				/* 拼接校验值等 */
	
	HAL_UART_Transmit(&huart4,cmd_send,strlen(cmd_send),100	);		/* 下发指令 */
	printf("%s",cmd_send);			/* 打印提示 */
}

void setGPS()
{
	#if 1
	char setBaseline[] = "$PQTMCFGBLD,W,0.22*59";	/* 设置基线 */
	char saveCmd[] = "$PQTMSAVEPAR*5A";				/* 保存配置 */
	
	HAL_UART_Transmit(&huart4, setBaseline, sizeof(setBaseline), 100);
	osDelay(500);

	setLocaOutput(0,1);		/* 输出RMC格式 */
	osDelay(500);
	for(i = 0; i < 7; i++)	/* 其他格式都不输出 */
	{
		setLocaOutput(i + 1,0);
		osDelay(500);
	}
	i= 0;
	setLocaConfig(0x37);	/* 所有星系全部用来定位 */
	osDelay(500);
	HAL_UART_Transmit(&huart4, saveCmd, sizeof(saveCmd), 100);
	osDelay(500);
	#else
	char defaultCmd[] = "$PQTMRESTOREPAR*13";
	HAL_UART_Transmit(&huart4, defaultCmd, sizeof(defaultCmd), 100);
	osDelay(500);
	#endif
	
}

void gps_task(void *argument)
{
	int i;
	uint16_t recv_len;
	uint8_t new_line_flag = 0;
	
	while(1)
	{
		//等待接收队列数据
		if (userUart.userUart4.ReceiveNum > 0)
		{
			#if 1
			printf("U4:");
			#if 1
			#if 1
			printf("%s",userUart.userUart4.ReceiveData);
			printf("\r\n");
			#else
			for(i = 0; i < userUart.userUart4.ReceiveNum;i++)
				printf("%c",userUart.userUart4.ReceiveData[i]);
			#endif
			#else
			for(i = 0; i < userUart.userUart4.ReceiveNum;i++)
				printf("%02x ",userUart.userUart4.ReceiveData[i]);
			printf("\r\n");
				
			#endif
			#endif
			recv_len = userUart.userUart4.ReceiveNum;
			#if 1
        	if (recv_len)
        	{
        		uint8_t *p = userUart.userUart4.ReceiveData;
        		do
        		{
        			new_line_flag = 0;
        			for (i = 0; i < recv_len; i++)
        			{
        				if ('\n' == *(p+i))
        				{
        					if (i >= 6 && ('\r' == *(p+i-1)))
        					{
            					new_line_flag = 1;
            					*(p+i) = 0;
								
            					if (!parse_nmea((const char *)p,i + 1))
            					{
            					}
        					}
							p += (i + 1);
							recv_len -= (i + 1);
        					break;
        				}
        			}
        		}while(new_line_flag);
        	}
			#endif
			userUart.userUart4 .ReceiveNum = 0; 
		}
		osDelay(500);
	}
}

