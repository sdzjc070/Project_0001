using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class ToDo
    {
        public int actionType(JsonInfo jif)
        {
            Type t = new Type();

            int res = -1;
            string act = jif.getActionType();
            foreach (var itme in t.getType())
            {
                if (act.Equals(itme.Key))
                {
                    return itme.Value;
                }
            }
            return res;
        }
    }
}
