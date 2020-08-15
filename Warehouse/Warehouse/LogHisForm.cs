using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
namespace Warehouse
{
    public partial class LogHisForm : Form
    {
        DataTable dtInfo = new DataTable();
        MySqlConn msc = new MySqlConn();
        public LogHisForm()
        {
            InitializeComponent();
        }

        private void LogHisForm_Load(object sender, EventArgs e)
        {
            //comboBox2属性设置
            Thread fac_thread = new Thread(getFactory);
            fac_thread.Start();
        }

        private void getFactory(object obj)
        {
            try
            {
                string sql = "select * from bininfo";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlDataReader rd = msc.getDataFromTable(sql);
                while (rd.Read())
                {
                    comboBox2.Items.Add(rd["BinName"].ToString());
                }
                rd.Close();

                if (comboBox2.Items.Count > 0)
                {
                    comboBox2.Text = comboBox2.Items[0].ToString();
                }

            }
            catch (SqlException se)
            {
                MessageBox.Show("数据库异常", "提示");
            }

        }
        /// <summary>
        /// 根据BinName找BinID
        /// </summary>
        /// <returns></returns>
        private string selectID(string str)
        {
            string sql = "select * from bininfo where BinName = '" + str + "'";
            try
            {
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlDataReader rd = msc.getDataFromTable(sql);
                string ret = "";
                
                while (rd.Read())
                {
                    ret = rd["BinID"].ToString();
                }
                rd.Close();

                return ret;
            }
            catch (SqlException se)
            {
                MessageBox.Show("数据库异常", "提示");
                return "error";
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string id = selectID(comboBox2.Text);
            DateTime time_start = new DateTime();
            DateTime time_end = new DateTime();
            string start = "";
            string end = "";

            try
            {
                time_start = Convert.ToDateTime(dateTimePicker1.Text);
                time_end = (Convert.ToDateTime(dateTimePicker2.Text)).AddDays(1);





                if (DateTime.Compare(time_start, time_end) > 0)
                    MessageBox.Show("请输入正确的时间范围", "提示");

                if (id.Equals("") == false)
                {
                    if (id.Equals("error"))
                    {
                        MessageBox.Show("在查询料仓编号时数据库连接失败", "提示");
                    }
                    else
                    {
                       
                        //验证时间格式
                        string[] arr = time_start.ToString().Split('-');
                        string[] arr1 = time_end.ToString().Split('-');
                        //MessageBox.Show("长度：" + arr.Length);
                        if (arr.Length == 3)//说明格式不正确
                        {
                            start = arr[0] + '/' + arr[1] + '/' + arr[2];
                            end = arr1[0] + '/' + arr1[1] + '/' + arr1[2];
                        }
                        else
                        {
                            start = time_start.ToString("yyyy/MM/dd HH:mm:ss");
                            end = time_end.ToString("yyyy/MM/dd HH:mm:ss");

                        }
                        try
                        {
                            string sql = "select * from binlog where Address = " + id + " and Time between'" + start + "' and '" + end + "'  order by Time desc";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.sda.SelectCommand = db.command;

                            //DataSet ds = new DataSet();
                            //db.sda.Fill(ds, "ds");
                            //this.dataGridView1.DataSource = ds.Tables[0];


                            MySqlConnection conn = msc.GetConn();
                            MySqlDataAdapter sda = new MySqlDataAdapter(sql, conn);//获取数据表
                            //DataTable table = new DataTable();
                            DataSet ds = new DataSet();
                            sda.Fill(ds, "ds");//填充数据库
                            this.dataGridView1.DataSource = ds.Tables[0];
                        }catch(Exception ee)
                        {
                            MessageBox.Show("查询数据库错误：" + ee.ToString());
                        }
                        
                    }

                }
                else
                {
                    MessageBox.Show("没有此料仓，请重新输入", "提示");
                }

            }
            catch (FormatException exc)
            {
                MessageBox.Show(exc.ToString());
                MessageBox.Show("时间格式输入有误", "提示");
            }

        }

        private void LogHisForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Dispose();
        }

        private void LogHisForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Dispose();
        }
        //private void InitDataSet()
        //{
        //         //设置页面行数



        //    LoadData();
        //}
        //private void LoadData()
        //{
        //    int nStartPos = 0;   //当前页面开始记录行
        //    int nEndPos = dtInfo.Rows.Count;     //当前页面结束记录行

        //    DataTable dtTemp = dtInfo.Clone();   //克隆DataTable结构框架
        //    //从元数据源复制记录行
        //    for (int i = nStartPos; i < nEndPos; i++)
        //    {
        //        if (dtInfo.Rows.Count > 0)
        //        {//判读表中是否有内容
        //            dtTemp.ImportRow(dtInfo.Rows[i]);
        //        }

        //    }
        //    bdsInfo.DataSource = dtTemp;
        //    bdnInfo.BindingSource = bdsInfo;
        //    dataGridView1.DataSource = bdsInfo;
        //}

    }
}
