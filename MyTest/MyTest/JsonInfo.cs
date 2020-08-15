using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class JsonInfo
    {
        private string result = "";
        private string userinfo = "";
        private string actionType = "";
        private string data = "";
        public JsonInfo(string result, string userinfo, string actionType, string data)
        {
            this.result = result;
            this.userinfo = userinfo;
            this.actionType = actionType;
            this.data = data;
        }
        public string getResult()
        {
            return this.result;
        }
        public string getUserinfo()
        {
            return this.userinfo;
        }
        public string getActionType()
        {
            return this.actionType;
        }
        public string getData()
        {
            return this.data;
        }
    }
}
