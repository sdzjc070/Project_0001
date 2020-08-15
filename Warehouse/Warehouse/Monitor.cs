using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Warehouse
{
    public partial class Monitor : Form
    {
        public Monitor()
        {
            InitializeComponent();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            this.Visible = false;
            userControl11.Visible = true;
        }

        private void Monitor_Load(object sender, EventArgs e)
        {

        }

        private void Monitor_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Dispose();
        }

    }
}
