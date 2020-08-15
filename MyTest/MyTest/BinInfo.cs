using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class BinInfo
    {
        //想要转成json的形式，对象属性必须是public
        public string bid;//id
        public string bname;//应答指令码
        public string Diameter ="";
        public string CylinderH = "";
        public string PyramidH = "";
        public string Density = "";
        public string Margin = "";
        public string BinTop = "";
        public string Wheelbase = "";
        public string Angle = "";
        public string Vol = "";
        public string Weight = "";
        public string Temp = "";
        public string Hum = "";
        public string DateTime = "";
        public string Algorithm = "";
        public string PrintNum = "";
        public string Qual = "";
        public string MiDu = "";


        public BinInfo(string bid, string bname, string Diameter, string CylinderH, string PyramidH, string Density)
        {
            this.bid = bid;
            this.bname = bname;
            this.Diameter = Diameter;
            this.CylinderH = CylinderH;
            this.PyramidH = PyramidH;
            this.Density = Density;
        }

        public BinInfo(string bid, string bname, string Diameter, string CylinderH, string PyramidH, string MiDu, string Margin, string BinTop, string Wheelbase, string Angle, 
            string Vol, string Weight, string Temp, string Hum, string DateTime, string Algorithm, string PrintNum, string Qual)
        {
            this.bid = bid;
            this.bname = bname;
            this.Diameter = Diameter;
            this.CylinderH = CylinderH;
            this.PyramidH = PyramidH;
            this.MiDu = MiDu;
            this.Margin = Margin;
            this.BinTop = BinTop;
            this.Wheelbase = Wheelbase;
            this.Angle = Angle;
            this.Vol = Vol;
            this.Weight = Weight;
            this.Temp = Temp;
            this.Hum = Hum;
            this.DateTime = DateTime;
            this.Algorithm = Algorithm;
            this.PrintNum = PrintNum;
            this.Qual = Qual;
        }
        public BinInfo( string bname, string Diameter, string CylinderH, string PyramidH, string MiDu, string Margin, string BinTop, string Wheelbase, string Angle,
            string DateTime, string Algorithm, string PrintNum)
        {
            this.bname = bname;
            this.Diameter = Diameter;
            this.CylinderH = CylinderH;
            this.PyramidH = PyramidH;
            this.MiDu = MiDu;
            this.Margin = Margin;
            this.BinTop = BinTop;
            this.Wheelbase = Wheelbase;
            this.Angle = Angle;
            this.DateTime = DateTime;
            this.Algorithm = Algorithm;
            this.PrintNum = PrintNum;
        }
    }
}
