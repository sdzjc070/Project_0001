using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class CheckData
    {
        public float CalcHeight;//用于计算的高度值,测量距离乘以三角函数后的高度
        public float CalcRadius;//用于计算的半径,测量距离乘以三角函数后的半径数据
        public float MeansureLength;//当前测量值
        public CheckData()
        {
            CalcHeight = new float();
            CalcRadius = new float();
            MeansureLength = new float();
        }
    }
}
