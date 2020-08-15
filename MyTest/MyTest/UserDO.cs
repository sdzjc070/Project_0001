using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class UserDO
    {
        public string uid = "";
        public List<eqIdDo> eq= new List<eqIdDo>(); 

        public UserDO(string uid,List<eqIdDo> eq)
        {
            this.uid = uid;
            this.eq = eq;
        }
        public string getUid()
        {
            return this.uid;
        }
        public List<eqIdDo> getEqList()
        {
            return this.eq;
        }
    }
}
