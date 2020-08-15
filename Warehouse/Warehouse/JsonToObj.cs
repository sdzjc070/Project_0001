using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Warehouse
{
    class JsonToObj
    {
        public string type = "";
        public string data = "";
        public JsonToObj(string type, string data)
        {
            this.type = type;
            this.data = data;
        }
        public string getType()
        {
            return this.type;
        }
        public string getData()
        {
            return this.data;
        }
    }
}
