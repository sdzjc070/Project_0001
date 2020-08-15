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
    public partial class DaTu2 : Form
    {
        public float data_x_max;
        public float data_y_max;
        public CheckData[] OriginalValue;
        public float height_total;
        public string isBlue;

        public float setX
        {
            get
            {
                return this.data_x_max;
            }
            set
            {
                this.data_x_max = value;
            }
        }
        public string setIsBlue
        {
            get
            {
                return this.isBlue;
            }
            set
            {
                this.isBlue = value;
            }
        }
        public float setY
        {
            get
            {
                return this.data_y_max;
            }
            set
            {
                this.data_y_max = value;
            }
        }
        public float setH
        {
            get
            {
                return this.height_total;
            }
            set
            {
                this.height_total = value;
            }
        }
        public CheckData[] setDate
        {
            get
            {
                return this.OriginalValue;
            }
            set
            {
                this.OriginalValue = value;
            }
        }
        public DaTu2()
        {

            InitializeComponent();

        }

        private void DaTu_Load(object sender, EventArgs e)
        {
            MessageBox.Show("" + OriginalValue.Length);

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }


        private void panel1Paint()
        {
            Graphics g = panel1.CreateGraphics();

            float x_max = panel1.Width - 10;
            float y_max = panel1.Height - 10;
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
            //画一次原始数据
            for (int i = 0; i < OriginalValue.Length; i++)
            {
                float x1 = OriginalValue[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max);
                float y1 = height_total - OriginalValue[i].CalcHeight;//实际的测量出的
                y1 = y_max - y1 * (y_max / data_y_max);
                //计算第一个点的坐标
                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x1, y1, 2, 2);

                //System.Console.WriteLine("y1:{0}\n", y1);
                if (i + 1 < OriginalValue.Length)
                {
                    float x2 = OriginalValue[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max);
                    float y2 = height_total - OriginalValue[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max);
                    //g.FillEllipse(brush, x2, y2, 2, 2);

                    if (isBlue.Equals("blue"))
                    {
                        p = new Pen(Color.Blue, 1);
                    }
                    else
                    {
                        p = new Pen(Color.Red, 1);
                    }


                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));//将两个点连线

                }
            }
        }

        private void DaTu_Paint(object sender, PaintEventArgs e)//窗体的画图时间
        {
            panel1Paint();
        }
    }
}
