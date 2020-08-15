using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class Rebinlist
    {
        public string bid;//id
        public string bname;//名字
        public string bstate;////料仓编号
        public Boolean isChecked;
        public Rebinlist(string bid, string bname, string bstate)
        {
            this.bid = bid;
            this.bname = bname;
            this.bstate = bstate;
            this.isChecked = false;
        }
    }
}
