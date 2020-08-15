using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Warehouse
{
    public partial class SetWenDu : Form
    {
        public string model = "";
        public string name = "";
        public string id = "";
        public string Fcode = "";
        public TransCoding tc = new TransCoding();
        public string setFcode
        {
            get
            {
                return this.Fcode;
            }
            set
            {
                this.Fcode = value;
            }
        }
        public string setModel
        {
            get
            {
                return this.model;
            }
            set
            {
                this.model = value;
            }
        }
        public string setName
        {
            get
            {
                return this.name;
            }
            set
            {
                this.name = value;
            }
        }
        public string setId
        {
            get
            {
                return this.id;
            }
            set
            {
                this.id = value;
            }
        }
        public SetWenDu()
        {
            InitializeComponent();
          
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MainForm mf = new MainForm();
            string res = "";
            if (model.Equals("1"))
            {
                res = tc.DataWenDuAndShiJian(Fcode, this.id, "44", "0020");
            }
            else
            {
                //res = tc.DataWenDuAndShiJian(Fcode, id, "44", "0301");
            }
           
            //mf.sendCode(res, this.id);
        }

        private void SetWenDu_Load(object sender, EventArgs e)
        {
            String[] arr = new String[] { "时间", "温度"};
            for (int i = 0; i < arr.Length; i++)
            {
                comboBox1.Items.Add(arr[i]);
            }
            //下面两种方法都可以为ComboBox赋初试选中值
            //comboBox1.SelectedIndex = 0;
            comboBox1.SelectedItem = "时间";
            //if (model.Equals("1"))
            //{
            //    this.label1.Text = "料仓： " +  name +  " 设置温度：";
            //}
            //if(model.Equals("2"))
            //{
            //    this.label1.Text = "料仓： " + name +  " 设置时间：";
            //}
        }
    }
}
