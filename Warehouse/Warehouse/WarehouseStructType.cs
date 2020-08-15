using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warehouse
{
    class WarehouseStructType
    {
        public float ColumnDiameter;//柱体直径
        public float ColumnRadius;  //半径
        public float ColumnHeight;  //柱体高度
        public float VertebralHeight;//下锥高度
        public float TopHeight;//扫描仪距顶高度
        public float Margin;//边距
        public float AxisLength;//轴距
        public float Volume;  //体积

        public  WarehouseStructType(float ColumnDiameter, float ColumnRadius, float ColumnHeight, float VertebralHeight, float TopHeight,float Margin, float AxisLength, float Volume)
        {
            this.ColumnDiameter = ColumnDiameter;
            this.ColumnRadius = ColumnRadius;
            this.ColumnHeight = ColumnHeight;
            this.VertebralHeight = VertebralHeight;
            this.TopHeight = TopHeight;
            this.Margin = Margin;
            this.AxisLength = AxisLength;
            this.Volume = Volume;
        }
    }

  
    
}
