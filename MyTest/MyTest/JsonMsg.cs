﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class JsonMsg
    {
        public string type = "";
        public string data = "";
        public JsonMsg(string type, string data)
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
