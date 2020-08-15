using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warehouse
{
    class CalV
    {
        //int WareHouseState = 1;//判断仓库的状态，0空仓；1正常仓位，使用直径计算；2扫描角度大于70度，使用半径计算；3，垂直距离小于1米，满仓
        //float StepAngle = 1;//步进角度
        //CheckData[] MeasureValue = new CheckData[180];//激光测距结构体,用于记录回传数据
        //WarehouseStructType wareData = new WarehouseStructType(
        //    13.75F,
        //(13.75F / 2F),
        //19.4F,
        //4.5F,
        //0.21F,
        //0.28F,
        //0.06F,
        //0F);

        float PI = 3.1415926F;
        /**
          * @brief  中心点判断
	        * @param  angle 总测量角度值，扫描点的个数
          * @retval 中心点位置
        */
        public int DataScreen(CheckData[] MeasureValue, WarehouseStructType wareData, int num)//返回扫到中心点时的角度
        {
            int i;
            for (i = 0; i < num; i++)
            {
                if (MeasureValue[i].CalcRadius > wareData.ColumnRadius - wareData.Margin)//当测量的长度计算出来的半径.对于直径算法，会直接返回那个中心点的编号
                    break;
            }
            return i;//如果循环了所以点，都没有大于直径的值，说明这个算法是半径算法，最后一个点就是中心点，所以返回最后一个点的下标
        }
        /**
          * @brief  计算空心圆柱体体积
          * @param  H1 第一个采样点高度
          * @param  R1 第一个采样点半径
          * @param  H2 第二个采样点高度
          * @param  R2 第二个采样点半径
          * @retval 环状体体积
        */
        public float CalculateV(float H1, float R1, float H2, float R2)
        {
            float H;
            H = (H1 + H2) / 2;
            if (R1 > R2)
                return PI * H * (R1 * R1 - R2 * R2);
            else
                return PI * H * (R2 * R2 - R1 * R1);
        }
        /**
          * @brief  仓库物料体积计算
	        * @param  state计算模式
	        * @param  num总测量的点的个数
	        * @param  RelativeHeight相对高度
          * @retval none
        */
        public float VolumeCalculate(int state,int num,float RelativeHeight, CheckData[] MeasureValue, WarehouseStructType wareData)
        {
            int i, EffectiveAngle;//EffectiveAngle对于半径计算法，就是有效值的位置，对于直径计算法就是中心点的位置
            float Volume1 = 0, Volume2 = 0; //体积1半径计算值，体积2中心点另一边直径计算值
            float CalcPercent;//计算体积的权重比例
            float[] ObjectHeight = new float[180];//仓库实体物料高度
            float[] ObjectRadius = new float[180];//仓库实体物料半径
            for(int j = 0; j < ObjectRadius.Length; j++)
            {
                ObjectHeight[j] = 0F;
                ObjectRadius[j] = 0F;
            }
            //EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置，那个中心点的角度???

            //if ((state == 1) || (state == 2))
            //{//先计算需要的参数（打在粮食上的点距离地面的高度和距离整个筒仓中心点的距离）
            // //数据的处理
            //    EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置，那个中心点的角度???

            //    for (i = 0; i < num; i++)
            //    {
            //        ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//相对高度减去测量后的那个点的垂直长度就是物料距离地面的高度(相对高度 ，柱体高 - 顶高 +下锥高，虚拟化为这个柱体的高度)
            //        ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
            //    }
            //}

            //if (state == 3)//满仓，直接根据第一个点计算
            //{

            //    //计算柱体体积
            //    Volume1 = CalculateV(RelativeHeight - MeasureValue[0].CalcHeight,
            //                                                wareData.ColumnRadius,
            //                                                RelativeHeight - MeasureValue[0].CalcHeight,
            //                                                0);
            //    wareData.Volume = (float)(Volume1 - 2.094395 * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius); //统一处理，挖去下锥以外的部分
            //}
            //else//使用直径或者半径算法
            //{
            //    //使用中心线扫描侧的点计算整个体积
            //    //计算最外圈体积
            //    Volume1 += CalculateV(ObjectHeight[0],
            //                                                wareData.ColumnRadius,
            //                                                ObjectHeight[0],
            //                                                ObjectRadius[0]);

            //    //依次计算内圈体积
            //    for (i = 0; i < EffectiveAngle - 1; i++)//计算第一个测量点到最后一个点直的体积
            //        Volume1 += CalculateV(ObjectHeight[i],
            //                                                   ObjectRadius[i],
            //                                                   ObjectHeight[i + 1],
            //                                                   ObjectRadius[i + 1]);
            //    //计算中心柱体体积，计算最后一个点到中心点的圆柱体积
            //    Volume1 += CalculateV(ObjectHeight[EffectiveAngle-1],
            //                                                ObjectRadius[EffectiveAngle-1],
            //                                                ObjectHeight[EffectiveAngle-1],
            //                                                0);
            //    int angle = num - 1;
            //    //使用中心线扫描侧的另一边的点计算整个体积		
            //    if ((angle != EffectiveAngle) && (state == 1))//直径算法才会用到,如果输入的总扫描角度不等于中心点的扫描角度时，这个时候是扫了整个直径
            //    {
            //        //计算中心柱体体积，中心点到中心点下一个点直接的圆柱体积
            //        Volume2 += CalculateV(ObjectHeight[EffectiveAngle + 1],
            //                                                    ObjectRadius[EffectiveAngle + 1],
            //                                                    ObjectHeight[EffectiveAngle + 1],
            //                                                    0);

            //        //依次计算内圈体积，中心点的下一个点到最后一个点直接的体积
            //        for (i = EffectiveAngle + 1; i <= angle - 1; i++)
            //            Volume2 += CalculateV(ObjectHeight[i],
            //                                                        ObjectRadius[i],
            //                                                        ObjectHeight[i + 1],
            //                                                        ObjectRadius[i + 1]);

            //        //计算最外圈体积。最后一个点到仓壁之间的体积
            //        Volume2 += CalculateV(ObjectHeight[angle],
            //                                                    wareData.ColumnRadius,
            //                                                    ObjectHeight[angle],
            //                                                    ObjectRadius[angle]);
            //    }
            //    if (state == 1)//直径
            //    {
            //        CalcPercent = ((float)EffectiveAngle) / ((float)angle);//计算权重比例，以扫描点的个数为准
            //                                                               //分别计算加权后的体积，并减去下锥空余部分的体积，就是结果
            //        wareData.Volume = (float)(Volume1 * CalcPercent + Volume2 * (1 - CalcPercent) - 2.094395 * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius); //统一处理，挖去下锥以外的部分
            //    }
            //    if (state == 2)//半径
            //    {
            //        wareData.Volume = (float)(Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2/3); //统一处理，挖去下锥以外的部分
            //    }



            EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置

            if ((state == 1) || (state == 2))
            {//先计算需要的参数
             //数据的处理
                EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置

                for (i = 0; i < num; i++)
                {
                    //计算实体物料高度
                    if (MeasureValue[i].CalcHeight < RelativeHeight)//比实际高度小
                    {
                        ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;
                    }
                    else//大于实际高度
                    {
                        if (i == 0)
                        {
                            //SetRescanFlag(1);//重盘使能
                        }
                        else
                        {
                            //ReplaceValues(i);//使用前一个点覆盖
                            //ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//再计算
                            ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//相对高度减去测量后的那个点的垂直长度就是物料距离地面的高度(相对高度 ，柱体高 - 顶高 +下锥高，虚拟化为这个柱体的高度)
                        }
                    }
                   
                   
                                                                                                                     //计算半径代入值
                    if (MeasureValue[i].CalcRadius < wareData.ColumnRadius - wareData.Margin)//比实际直径小
                    {
                        ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
                    }
                    else//大于实际直径
                    {
                        if (i == 0)
                        {
                            //SetRescanFlag(1);//重盘使能
                        }
                        else
                        {
                            ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
                        }
                    }
                }
            }

            if (state == 3)//满仓，直接根据第一个点计算
            {
                //计算柱体体积
                Volume1 = CalculateV(RelativeHeight - MeasureValue[0].CalcHeight,
                                                            wareData.ColumnRadius,
                                                            RelativeHeight - MeasureValue[0].CalcHeight,
                                                            0);
                Volume1 = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
            }
            else//使用直径或者半径算法
            {
                //使用中心线扫描侧的点计算整个体积
                //计算最外圈体积
                Volume1 += CalculateV(ObjectHeight[0],
                                                            wareData.ColumnRadius,
                                                            ObjectHeight[0],
                                                            ObjectRadius[0]);

                //依次计算内圈体积
                for (i = 0; i < EffectiveAngle - 1; i++)
                    Volume1 += CalculateV(ObjectHeight[i],
                                                               ObjectRadius[i],
                                                               ObjectHeight[i + 1],
                                                               ObjectRadius[i + 1]);
                //计算中心柱体体积
                Volume1 += CalculateV(ObjectHeight[EffectiveAngle - 1],
                                                            ObjectRadius[EffectiveAngle - 1],
                                                            ObjectHeight[EffectiveAngle - 1],
                                                            0);

                //使用中心线扫描侧的另一边的点计算整个体积		
                if ((num != EffectiveAngle) && (state == 1))//直径算法才会用到
                {
                    //计算中心柱体体积
                    Volume2 += CalculateV(ObjectHeight[EffectiveAngle],
                                                                ObjectRadius[EffectiveAngle],
                                                                ObjectHeight[EffectiveAngle],
                                                                0);

                    //依次计算内圈体积
                    for (i = EffectiveAngle; i < num - 1; i++)
                        Volume2 += CalculateV(ObjectHeight[i],
                                                                    ObjectRadius[i],
                                                                    ObjectHeight[i + 1],
                                                                    ObjectRadius[i + 1]);

                    //计算最外圈体积
                    Volume2 += CalculateV(ObjectHeight[num - 1],
                                                                wareData.ColumnRadius,
                                                                ObjectHeight[num - 1],
                                                                ObjectRadius[num - 1]);
                }
                if (state == 1)//直径
                {
                    CalcPercent = ((float)EffectiveAngle) / ((float)num);//计算权重比例，以扫描点的个数为准
                                                                         //分别计算加权后的体积，并减去下锥空余部分的体积，就是结果
                    wareData.Volume = Volume1 * CalcPercent + Volume2 * (1 - CalcPercent) - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
                }
                if (state == 2)//半径
                {
                    wareData.Volume = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
                }
            }
            return wareData.Volume;

        }
            

    }

}
