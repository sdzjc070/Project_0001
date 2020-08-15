using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    class eqIdDo
    {
        private string uid = "";//用户ID
        private string eqid = "";//设备ID
        private string eqstate = "";//状态
        public  eqIdDo(string uid,string eqid,string eqstate)
        {
            this.eqid = eqid;
            this.uid = uid;
            this.eqstate = eqstate;
        }
        public string getEqid()
        {
            return this.eqid;
        }
        public string getUid()
        {
            return this.uid;
        }
        public string getEqState()
        {
            return this.eqstate;
        }
        public void setEqState(string eqstate)
        {
            this.eqstate = eqstate;
        }
    }
}
