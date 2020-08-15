using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warehouse
{
    class FacStatus
    {
        public string fac_addr;//料仓地址
        public int status;//料仓状态
        public FacStatus(string fac_addr, int status)
        {
            this.fac_addr = fac_addr;
            this.status = status;
        }
    }
}
