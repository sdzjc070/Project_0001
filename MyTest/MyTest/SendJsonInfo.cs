using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class SendJsonInfo
    {
        public string result; //对应动作的结果
        public string sender;// 发送方订阅的topic
        public string actionType;
        public string data;
        public SendJsonInfo(string result, string sender, string actionType, string data)
        {
            this.result = result;
            this.sender = sender;
            this.actionType = actionType;
            this.data = data;

        }

        public SendJsonInfo()
        {


        }
    }
}
