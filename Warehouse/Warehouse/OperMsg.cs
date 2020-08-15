using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warehouse
{
    /// <summary>
    /// 盘库、清洁镜头信息类
    /// </summary>
    class OperMsg
    {
        public string fac_num;//料仓编号
        public string time;//自动操作时间
        public string date;//日期
        public string operation;//操作类型
        public int state;//状态

        public OperMsg(string fac_num, string time, string date, string operation, int state)
        {
            this.fac_num = fac_num;
            this.time = time;
            this.date = date;
            this.operation = operation;
            this.state = state;
        }
        public string Out()
        {
            return this.fac_num + " " + this.time + " " + this.date + " " + this.operation + "\r\n";
        }
    }
}
