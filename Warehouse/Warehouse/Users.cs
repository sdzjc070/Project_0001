using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Warehouse
{
    public class Users
    {
        public string name;
        public string admin;
        public Users()
        {
            name = "";
            admin = "";
        }
        public void logout()
        {
            this.name = "";
            this.admin = "";
        }
    }
}
