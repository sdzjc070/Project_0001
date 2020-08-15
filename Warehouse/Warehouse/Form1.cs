using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Warehouse
{
    public class CheckData
    {
        public float CalcHeight;//用于计算的高度值,测量距离乘以三角函数后的高度
        public float CalcRadius;//用于计算的半径,测量距离乘以三角函数后的半径数据
        public float MeansureLength;//当前测量值
        public CheckData()
        {
            CalcHeight = new float();
            CalcRadius = new float();
            MeansureLength = new float();

        }
    }
    public partial class Form1 : Form
    {
        private List<int> angle_list = new List<int>();

        static double CHECK_PERCENT_VALUE = 0.07;    //数据检测过滤前后点阈值
        public CheckData[] MeansureValue;//用于计算过滤
        public CheckData[] OriginalValue;//原始值
        float diameter;
        float height_total = 0;
        float top_height = 0.3F;
        float stepangle = 0.3F;//步进角
        public Form1()
        {
            InitializeComponent();
            this.Text = "料仓测量数据模拟显示";
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

            //showForm form = new showForm();
            //form.Show();
            this.Refresh();
            Graphics g = panel1.CreateGraphics();

            float x_max = panel1.Width - 10;
            float y_max = panel1.Height - 10;
            System.Console.WriteLine("x-max:{0}\n", x_max);
            System.Console.WriteLine("y-max:{0}\n", y_max);
            float data_x_max = 0;
            float data_y_max = 0;
            float x_init = 0;


            string str_distance = textBox1.Text;//测量距离数据
            //string str_height = textBox2.Text;
            string str_x_init = textBox2.Text;
            string str_height_total = textBox4.Text;
            string str_top_height = textBox5.Text;

            string stepangle_str = textBox6.Text;

            try
            {
                diameter = float.Parse(textBox3.Text) * 100;
            }
            catch
            {
                MessageBox.Show("请输入有效直径");
                return;
            }
            try
            {
                x_init = float.Parse(str_x_init) * 100;
            }
            catch
            {
                MessageBox.Show("请输入有效距仓壁距离值");
                return;
            }
            try
            {
                height_total = float.Parse(str_height_total) * 100;
            }
            catch
            {
                MessageBox.Show("请输入有效料仓高度值");
                return;
            }
            try
            {
                top_height = float.Parse(str_top_height) * 100;
            }
            catch
            {
                MessageBox.Show("请输入有效料仓高度值");
                return;
            }

            try
            {
                stepangle = float.Parse(stepangle_str);
                //MessageBox.Show("步进角度==="+ stepangle);
            }
            catch
            {
                MessageBox.Show("请输入有效料旋转角度");
                return;
            }
            string[] distance_strarray = str_distance.Split(',');
            MessageBox.Show("长度=" + distance_strarray.Length + "直径：" + diameter + ",仓壁距离值:" + x_init + ",有效高度：" + height_total + ",顶高：" + top_height + ",旋转角度：" + stepangle);
            OriginalValue = new CheckData[distance_strarray.Length];
            MeansureValue = new CheckData[distance_strarray.Length];
            for (int i = 0; i < distance_strarray.Length; i++)
            {
                OriginalValue[i] = new CheckData();
                MeansureValue[i] = new CheckData();
            }

            //float[] height_array = new float[height_strarray.Length];
            for (int i = 0; i < distance_strarray.Length; i++)
            {
                try
                {
                    OriginalValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                    MeansureValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                }
                catch
                {
                    MessageBox.Show("距离数据有不合法数据！");
                    return;
                }
            }
            //angle_list放着角度*stepangle（自己设置的角度）
            if (angle_list.Count != 0)
            {
                for (int i = 0; i < distance_strarray.Length; i++)
                //将i改为真正获取的角度值
                {
                    OriginalValue[i].CalcRadius = OriginalValue[i].MeansureLength * (float)(Math.Sin((angle_list[i] * stepangle) * Math.PI / 180)) + x_init;
                    if (OriginalValue[i].CalcRadius > data_x_max)
                    {
                        data_x_max = OriginalValue[i].CalcRadius;
                    }
                    OriginalValue[i].CalcHeight = OriginalValue[i].MeansureLength * (float)(Math.Cos(angle_list[i] * stepangle * Math.PI / 180));

                    if ((height_total - OriginalValue[i].CalcHeight) > data_y_max)
                    {
                        data_y_max = height_total - OriginalValue[i].CalcHeight;
                    }
                }
            }

            //二次校验的高度和距离
            for (int i = 0; i < distance_strarray.Length; i++)
            {
                MeansureValue[i].CalcHeight = OriginalValue[i].CalcHeight;
                MeansureValue[i].MeansureLength = OriginalValue[i].MeansureLength;
                MeansureValue[i].CalcRadius = OriginalValue[i].CalcRadius;
            }

            ////二次校验的半径
            //if (angle_list.Count != 0)
            //{
            //    for (int i = 0; i < distance_strarray.Length; i++)
            //    //将i改为真正获取的角度值
            //    {
            //        MeansureValue[i].CalcRadius = MeansureValue[i].MeansureLength * (float)(Math.Sin(angle_list[i] * stepangle * Math.PI / 180)) + x_init;
            //        if (MeansureValue[i].CalcRadius > data_x_max)
            //        {
            //            data_x_max = MeansureValue[i].CalcRadius;
            //        }
            //        if (MeansureValue[i].CalcRadius > data_x_max)
            //        {
            //            data_x_max = MeansureValue[i].CalcRadius;
            //        }
            //        MeansureValue[i].CalcHeight = MeansureValue[i].MeansureLength * (float)(Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
            //        if ((height_total - MeansureValue[i].CalcHeight) > data_y_max)
            //        {
            //            data_y_max = (height_total - MeansureValue[i].CalcHeight);
            //        }
            //    }
            //}




            if (diameter > data_x_max)
            {
                data_x_max = diameter;
            }

            if (height_total > data_y_max)
            {
                data_y_max = height_total;
            }


            DataCheck(MeansureValue.Length);

            //画直径示意线
            Pen p = new Pen(Color.Green, 2);

            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
            //画边框线
            p = new Pen(Color.Black, 1);
            //p.DashStyle =
            g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
            //画中心线
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

            Font drawfont = new Font("宋体", 8);
            SolidBrush drawbrush = new SolidBrush(Color.Black);
            //画一次校验线
            for (int i = 0; i < OriginalValue.Length; i++)
            {
                //String drawstring = "(" + radius_array[i] + "," + height_array[i] + ")";
                float x1 = OriginalValue[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max);
                float y1 = height_total - OriginalValue[i].CalcHeight;
                y1 = y_max - y1 * (y_max / data_y_max);
                System.Console.WriteLine("y1:{0}\n", y1);
                //g.DrawString(drawstring, drawfont, drawbrush, new PointF(x1, y1));
                if (i + 1 < OriginalValue.Length)
                {
                    float x2 = OriginalValue[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max);
                    float y2 = height_total - OriginalValue[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max);
                    p = new Pen(Color.Blue, 2);

                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));

                }
            }
            Thread.Sleep(500);
            //画二次检验线
            for (int i = 0; i < MeansureValue.Length; i++)
            {
                //String drawstring = "(" + radius2_array[i] + "," + height2_array[i] + ")";
                float x1 = MeansureValue[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max);
                float y1 = height_total - MeansureValue[i].CalcHeight;
                y1 = y_max - y1 * (y_max / data_y_max);
                //System.Console.WriteLine("y1:{0}\n", y1);
                //g.DrawString(drawstring, drawfont, drawbrush, new PointF(x1, y1));
                if (i + 1 < MeansureValue.Length)
                {
                    float x2 = MeansureValue[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max);
                    float y2 = height_total - MeansureValue[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max);
                    p = new Pen(Color.Red, 2);

                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));


                }
            }

        }

        /**
          * @brief  数据有效性检验
	        * @param  angle 总测量角度值
          * @retval none
        */
        void DataCheck(int angle)
        {
            int i;
            float average = 0;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeansureValue[i].CalcHeight;
            }
            average /= angle;

            //进行数据检验，粗过滤
            for (i = 1; i < angle; i++)
            {
                if ((MeansureValue[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
                    || (MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
                {
                    ReplaceValues(i);
                }

                if ((MeansureValue[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
                {
                    ReplaceValues(i);
                }

                //判断半径，正常情况半径越来越大
                if (MeansureValue[i].CalcRadius < MeansureValue[i - 1].CalcRadius)//半径比前一个小，错误
                {
                    ReplaceValues(i);
                }
            }

            //进行数据滤波，细过滤
            for (i = 1; i < angle; i++)
            {
                if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
                {
                    if (i == angle - 1)//最后一个点，使用覆盖
                    {
                        ReplaceValues(i);
                    }
                    else//其余点使用平均
                    {
                        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
                        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                    }
                }
            }
        }

        private void ReplaceValues(int i)
        {
            MeansureValue[i].MeansureLength = (float)(MeansureValue[i - 1].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));

            MeansureValue[i].CalcRadius = (float)(MeansureValue[i].MeansureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°
            MeansureValue[i].CalcHeight = MeansureValue[i - 1].CalcHeight;

        }
        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();   //显示选择文件对话框 
            openFileDialog1.InitialDirectory = System.Windows.Forms.Application.StartupPath + "\\back_data";
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = "";
                angle_list.Clear();
                int i = 0;
                StreamReader sr = File.OpenText(openFileDialog1.FileName);
                string line = "";
                int isfirst = 1;//判断是否是第一行，若不是，需要在前面添加','
                while ((line = sr.ReadLine()) != null)
                {
                    if (isfirst == 0)
                        textBox1.AppendText(",");
                    string[] data = line.Split(' ');
                    textBox1.AppendText(data[1]);
                    try
                    {
                        angle_list.Add(Int32.Parse(data[0]));
                        //textBox2.AppendText(angle_list[i].ToString());
                        i++;
                    }
                    catch (Exception exc)
                    {

                    }
                    isfirst = 0;
                }

                //for (int j = 0; j < angle_list.Count; j++)
                //{
                //    textBox2.AppendText(angle_list[j].ToString() + ",");
                //}
                //this.textBox1.Text = openFileDialog1.FileName;     //显示文件路径 
            }

        }
    }
}
