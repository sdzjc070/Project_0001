using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warehouse
{
    class JsonMsg
    {
        public string type { get; set; }
        public string data { get; set; }
        //public string type = "";
        //public string data = "";
        public JsonMsg(string type, string data)
        {
            this.type = type;
            this.data = data;
        }
        //public string getType()
        //{
        //    return this.type;
        //}
        //public string getData()
        //{
        //    return this.data;
        //}
    }
}
