using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class Type
    {
        public Dictionary<string, int> getType()
        {
            Dictionary<string, int> type = new Dictionary<string, int>();
            type.Add("inquireOnLineList", 1);//查询在线料仓
            type.Add("inquireStateAndDo", 2);//盘库
            type.Add("inquireHistoryList", 3);//查询料仓的历史数据
            type.Add("inquireHistoryListByDate", 4);//根据时间查历史数据
            type.Add("inquireCancel", 5);//取消盘库
            type.Add("inquireBinInfo", 6);//获取料仓详情
            type.Add("inquireClear", 7);//请求清洁镜头
            type.Add("inquireXY", 8);//获取料仓详情
            type.Add("inquireLongHistory", 9);//获取料仓详情

            type.Add("inquireGroup", 10);//获取分组列表
            type.Add("inquireOnLineListByGroup", 11);//根据分组列表获取料仓
            type.Add("inquireOnJiaoZhun", 12);//远程垂直校准
            type.Add("inquireAngle", 13);//获取垂直角度

            return type;
        }
    }
}
