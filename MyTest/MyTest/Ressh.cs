using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class Ressh
    {
        //想要转成json的形式，对象属性必须是public
        public string bid;//id
        public string bname;//应答指令码
        public string vol = "";////
        public string weight = "";////
        public string temp = "";////
        public string hum = "";////
        public string DFcode;
        public string DGnumber;
        public string dateTime;
        public string zhiliang;
        public string dens = "";
        public Ressh(string bid, string bname, string vol, string weight, string temp, string hum, string dateTime)
        {
            this.bid = bid;
            this.bname = bname;
            this.vol = vol;
            this.weight = weight;
            this.temp = temp;
            this.hum = hum;
            this.dateTime = dateTime;
        }

        public Ressh(string bid, string bname, string vol, string weight, string temp, string hum, string DFcode, string DGnumber, string zhiliang, string dateTime, string dens)
        {
            this.bid = bid;
            this.bname = bname;
            this.vol = vol;
            this.weight = weight;
            this.temp = temp;
            this.hum = hum;
            this.DFcode = DFcode;
            this.DGnumber = DGnumber;
            this.zhiliang = zhiliang;
            this.dateTime = dateTime;
            this.dens = dens;
        }
    }
}
