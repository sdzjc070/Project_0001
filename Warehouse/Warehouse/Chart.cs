using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Warehouse
{
    public partial class Chart : Form
    {
        

        public Chart()
        {
           
        }


        /// <summary>
        /// 根据BinName找BinID
        /// </summary>
        /// <returns></returns>
        private string selectID(string str)
        {
            string sql = "select * from [bininfo] where [BinName] = '" + str + "'";
            try
            {
                DataBase db = new DataBase();
                db.command.CommandText = sql;
                db.command.Connection = db.connection;
                string ret = "";
                db.Dr = db.command.ExecuteReader();
                while (db.Dr.Read())
                {
                    ret = db.Dr["BinID"].ToString();
                }
                db.Dr.Close();
                return ret;
            }
            catch (SqlException se)
            {
                string message_error = se.ToString();
                string path = System.Windows.Forms.Application.StartupPath;
                FileStream fs = new FileStream(path + "\\log.txt", FileMode.Create | FileMode.Append);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine("错误信息是：" + message_error + " 时间是：" + DateTime.Now.ToString());
                sw.Flush();
                sw.Close();
                fs.Close();
                return "error";
            }
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            chart1.Series["变量"].Points.Clear();
            if (comboBox2.Text.Equals(""))
            {
                MessageBox.Show("请选择要查询的料仓","提示");
            }
            else if (comboBox1.Text.Equals(""))
            {
                MessageBox.Show("请选择要查询的内容","提示");
            }
            else
            {
                string variable = "";//确定要查什么
                if (comboBox1.Text.Equals("温度"))
                    variable = "Temp";
                else if (comboBox1.Text.Equals("湿度"))
                    variable = "Hum";
                else if (comboBox1.Text.Equals("体积"))
                    variable = "Volume";
                else if (comboBox1.Text.Equals("重量"))
                    variable = "Weight";
                string id = selectID(comboBox2.Text);
                DateTime time_start = new DateTime();
                DateTime time_end = new DateTime();

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
                            MessageBox.Show("在查询料仓编号时数据库连接失败","提示");
                        }
                        else
                        {
                            chart1.Series["变量"].LegendText = comboBox1.Text;

                            //设置坐标轴名称
                            chart1.ChartAreas["ChartArea1"].AxisX.Title = "时间";
                            switch (comboBox1.Text)
                            {
                                case "温度":
                                    chart1.ChartAreas["ChartArea1"].AxisY.Title = "摄氏度";
                                    break;
                                case "体积":
                                    chart1.ChartAreas["ChartArea1"].AxisY.Title = "立方米";
                                    break;
                                case "湿度":
                                    chart1.ChartAreas["ChartArea1"].AxisY.Title = "百分比";
                                    break;
                                case "重量":
                                    chart1.ChartAreas["ChartArea1"].AxisY.Title = "吨";
                                    break;
                            }

                            string sql = "select * from [bindata] where [BinID] = " + id + " and [DateTime]>='" + time_start.ToString("d") + "' and [DateTime]<='" + time_end.ToString("d") + "'";
                            DataBase db = new DataBase();
                            db.command.CommandText = sql;
                            db.command.Connection = db.connection;

                            db.Dr = db.command.ExecuteReader();
                            while (db.Dr.Read())
                            {
                                if (db.Dr[variable].ToString().Equals("") == false)
                                {
                                    DateTime d = DateTime.Parse(db.Dr["DateTime"].ToString());
                                    float f = float.Parse(db.Dr[variable].ToString());
                                    chart1.Series["变量"].Points.AddXY(d.ToString("MM/dd HH:mm:ss"), (int)f);
                                }
                            }

                            db.Dr.Close();
                            db.Close();
                            if (chart1.Series["变量"].Points.Count == 0)
                            {
                                if (DateTime.Compare(time_start, time_end) < 0)
                                    MessageBox.Show("没有数据可供显示", "提示");
                            }
                        }
                        
                    }
                    else
                    {
                        MessageBox.Show("没有此料仓，请重新输入","提示");
                    }
                    
                }
                catch (FormatException exc)
                {
                    MessageBox.Show("时间格式输入有误", "提示");
                }
                
            }
        }

        private void Chart_Load(object sender, EventArgs e)
        {
            //comboBox2属性设置
            Thread fac_thread = new Thread(getFactory);
            fac_thread.Start();

            //ChartArea1属性设置
            //设置网格颜色
            chart1.ChartAreas["ChartArea1"].AxisX.MajorGrid.LineColor = Color.LightGray;
            chart1.ChartAreas["ChartArea1"].AxisY.MajorGrid.LineColor = Color.LightGray;
            //设置坐标轴名称
            chart1.ChartAreas["ChartArea1"].AxisX.Title = "变量";
            chart1.ChartAreas["ChartArea1"].AxisY.Title = "数值";
            //启用变量显示
            chart1.ChartAreas["ChartArea1"].Area3DStyle.Enable3D = false;

            //series属性设置
            //设置显示类型--线性
            chart1.Series["变量"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            //设置坐标轴Value显示类型
            chart1.Series["变量"].XValueType = ChartValueType.Time;
            //是否显示标签的数值
            chart1.Series["变量"].IsValueShownAsLabel = true;

            //设置标记图案
            chart1.Series["变量"].MarkerStyle = MarkerStyle.Circle;
            //设置图案颜色
            chart1.Series["变量"].Color = Color.Green;
            //设置图案的宽度
            chart1.Series["变量"].BorderWidth = 3;
            chart1.ChartAreas[0].AxisX.Interval = 1;   //设置X轴坐标的间隔为1
            chart1.ChartAreas[0].AxisX.IntervalOffset = 1;  //设置X轴坐标偏移为1
            chart1.ChartAreas[0].AxisX.LabelStyle.IsStaggered = true;   //设置是否交错显示,比如数据多的时间分成两行来显示




            chart1.ChartAreas["ChartArea1"].AxisX.ScrollBar.IsPositionedInside = false;//设置滚动条是在外部显示

            chart1.ChartAreas["ChartArea1"].AxisX.ScrollBar.Size = 20;//设置滚动条的宽度

            chart1.ChartAreas["ChartArea1"].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;//滚动条只显示向前的按钮，主要是为了不显示取消显示的按钮

            chart1.ChartAreas["ChartArea1"].AxisX.ScaleView.Size = 10;//设置图表可视区域数据点数，说白了一次可以看到多少个X轴区域

            chart1.ChartAreas["ChartArea1"].AxisX.ScaleView.MinSize = 1;//设置滚动一次，移动几格区域

            chart1.ChartAreas["ChartArea1"].AxisX.Interval = 1;//设置X轴的间隔，设置它是为了看起来方便点，也就是要每个X轴的记录都显示出来

            //chart1.ChartAreas["ChartArea1"].AxisX.Minimum = 1;//X轴起始点
            //chart1.ChartAreas["ChartArea1"].AxisX.Maximum = 100;//X轴结束点，一般这个是应该在后台设置的，
            //对于我而言，是用的第一列作为X轴，那么有多少行，就有多少个X轴的刻度，所以最大值应该就等于行数；

            //该值设置大了，会在后边出现一推空白，设置小了，会出后边多出来的数据在图表中不显示，所以最好是在后台根据你的数据列来设置.

        }

        private void getFactory(object obj)
        {
            try
            {
                string sql = "select * from [bininfo]";
                DataBase db = new DataBase();
                db.command.CommandText = sql;
                db.command.Connection = db.connection;
                db.Dr = db.command.ExecuteReader();
                while (db.Dr.Read())
                {
                    comboBox2.Items.Add(db.Dr["BinName"].ToString());
                }
                db.Dr.Close();
                db.Close();
                if (comboBox2.Items.Count > 0)
                {
                    comboBox2.Text = comboBox2.Items[0].ToString();
                }

            }
            catch (SqlException se)
            {
                MessageBox.Show("数据库异常","提示");
            }
            
        }

        private void Chart_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Dispose();
        }

        private void Chart_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Dispose();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

    }
}
