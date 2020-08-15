using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MathWorks.MATLAB.NET.Arrays;
using SanWei;
using text12;



namespace Warehouse
{
   
    public class ConvetPoint
    {
        public float[] x;
        public float[] y;
        public float[] z;
        public ConvetPoint()
        {
            x = new float[300];
            y = new float[300];
            z = new float[300];
        }

    }
    //顶置二维数据
    public class TopCheckData
    {
        public float CalcHeight;//用于计算的高度值,测量距离乘以三角函数后的高度
        public float CalcRadius;//用于计算的半径,测量距离乘以三角函数后的半径数据
        public float MeansureLength;//当前测量值
        public float LeftLength;//距离左边的距离
        public float Angle;
        public TopCheckData()
        { 
            CalcHeight = new float ();
            CalcRadius = new float ();
            MeansureLength = new float ();
            LeftLength = new float();
            Angle = new float();
        }


    }
    public partial class analysisData : Form
    {

        public string time;
        public string binName;
        public string binVol;
        public string binState;
        public float WareHigh;//立筒仓高度
        public float redius = 0;//半径
        public float fangchang = 0;
        public float fangkuan = 0;
        public float fangzuo = 0;
        private Mutex file_mutex = new Mutex();//文件互斥锁
        private Mutex recnum_mutex = new Mutex();//rec_num互斥锁
        static int V_POINT_NUM = 500;//planepoints行
        static int H_POINT_NUM = 500;//planepoints列
        static float BACK_BORDER = 0.01f;
        static float FRONT_BORDER = 0.99f;
        static float NEIGHBOR_RATIO = 1.02f;
        static float DIFF_RATIO = 1.02f;
        static float RADIUS_BUFFER = 0.3f;
        static float MAX_ANGLE = 80f;
        float PI = 3.1415926F;
        int Dflag=0;//表示顶置大图标志位

        public string setTime
        {
            get
            {
                return this.time;
            }
            set
            {
                this.time = value;
            }
        }
        public string setName
        {
            get
            {
                return this.binName;
            }
            set
            {
                this.binName = value;
            }
        }
        public string setVol
        {
            get
            {
                return this.binVol;
            }
            set
            {
                this.binVol = value;
            }
        }
        public string setState
        {
            get
            {
                return this.binState;
            }
            set
            {
                this.binState = value;
            }
        }
        private List<int> angle_list = new List<int>();
        private string distance = "";

        static double CHECK_PERCENT_VALUE = 0.07;    //数据检测过滤前后点阈值
        static int CHECK_DIFF_VALUE = 2;//细过滤的阀值
        public CheckData[] MeansureValue;//用于计算过滤
        public CheckData[] OriginalValue;//原始值
        public TopCheckData[] front_arr;//前向数组
        public TopCheckData[] back_arr;//前向数组
        public TopCheckData[] MeasureValueTop;//过滤以后汇总数组
        public TopCheckData MeasureValue_Temp;
        public TopCheckData[] OriginalValue1;
        public int final_count;
        float diameter;
        float height_total = 0;
        float SZ = 0;
        float top_height = 0.3F;
        float stepangle = 0.3F;//步进角

        float data_x_max = 0;
        float data_y_max = 0;
        float data_x_max_m = 0;
        float data_y_max_m = 0;

        float data_x_max_c = 0;
        float data_y_max_c = 0;
        float x_init = 0;
        int point_num;//原始点的个数
        string backData = "";//回传数据
        string backAn = "";//回传的角度

        string zhijing;
        string heigh;
        string xiazhui;
        string shangzhui;
        string midu;
        string Margin;
        string Top;
        string Wheelbas;
        string jaiodu;
        string paint = ""; //扫描的点数
        string fangchang1;


        string type = "";
        string backAll = "";



        string k = "";//比例校验
        string b = "";//加减校验
        int f_num = 0;
        int b_num = 0;

        private MySqlConn mscA = new MySqlConn();//新建数据库连接

        DaTu dt;
        DaTu2 dt2;
        public analysisData()
        {

            InitializeComponent();
            this.Text = "料仓测量数据模拟显示";

        }

        private void analysisData_Load(object sender, EventArgs e)
        {

            //在Load中改变panel的背景，背景会在最底层。如果在其他地方修改，背景会在最上层，从而覆盖了料仓曲线


            try
            {
                this.label2.Text = binName;
                string sql = "select * from bininfo where BinName = '" + binName + "'";
                MySqlDataReader rd = mscA.getDataFromTable(sql);
                //DataBase db = new DataBase();//连接数据库
                //db.command.CommandText = "select * from [bininfo] where [BinName] = '" + binName + "'";
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                while (rd.Read())
                {

                    zhijing = rd["Diameter"].ToString().Trim();
                    heigh = rd["CylinderH"].ToString().Trim();
                    xiazhui = rd["PyramidH"].ToString().Trim();
                    shangzhui=rd["UpperH"].ToString().Trim();
                    midu = rd["Density"].ToString().Trim();
                    Margin = rd["Margin"].ToString().Trim();
                    Top = rd["BinTop"].ToString().Trim();
                    Wheelbas = rd["Wheelbase"].ToString().Trim();
                    fangchang1= rd["UpperH"].ToString().Trim();

                    k = rd["KValue"].ToString().Trim();
                    b = rd["Bvalue"].ToString().Trim();

                    type = rd["type"].ToString().Trim();//获取到类型，根据类型来进行回传分析

                    //this.label2.Text = zhijing + "---盘库算法:" + binState + "---仓壁距离：" + Margin + "---仓高度值:" + heigh + "---顶高：" + Top + "---步进角：" + jaiodu;
                    this.label2.Text = this.binName;//料仓名
                    this.label4.Text = heigh + "米";
                    this.label6.Text = zhijing + "米";

                    string sf = "";
                    if (binState.Equals("1"))
                    {
                        sf = "半径";
                        this.label13.Text = sf + "算法";
                    }
                    if (binState.Equals("2"))
                    {
                        sf = "直径";
                        this.label13.Text = sf + "算法";
                    }
                    if (binState.Equals("0"))
                    {
                        sf = "满仓";
                        this.label13.Text = sf + "算法";
                    }
                    if (binState.Equals("3"))
                    {
                        sf = "值为负值";
                        this.label13.Text = sf;
                    }
                    this.label14.Text = xiazhui + "米";
                    this.label18.Text = midu + "吨/立方米";
                    this.label16.Text = Margin + "米";//仓壁距离
                    this.label17.Text = Top + "米";



                    this.label35.Text = k;
                    this.label36.Text = b;

                }
                rd.Close();

                //查返回的回传数据
                //DataBase db1 = new DataBase();//连接数据库
                //db1.command.CommandText = "select * from Factory.dbo.bindata where [Volume] = '" + binVol + "' and [DateTime] = '" + time + "'";
                //db1.command.Connection = db1.connection;
                //db1.Dr = db1.command.ExecuteReader();

                sql = "select * from bindata where format(Volume ,2) = format( '" + binVol + "' ,2) and DateTime = '" + time + "'";
                rd = mscA.getDataFromTable(sql);
                while (rd.Read())
                {
                    backData = rd["BackData"].ToString().Trim();
                    backAn = rd["BackAn"].ToString().Trim();//回传单的角度
                    backAll = rd["BackAll"].ToString().Trim();



                    paint = rd["PrintNum"].ToString().Trim();//获取点数
                    string[] p = paint.Split(',');
                    jaiodu = rd["Jd"].ToString().Trim();
                    this.label26.Text = p[0];
                    this.label28.Text = p[1];
                    this.label30.Text = p[2];//总点个数
                    point_num = Convert.ToInt32(p[2]);
                    this.label32.Text = jaiodu + "度";



                }
                rd.Close();



                if (type.Equals("2"))
                {


                    string str_x_init = Margin;//仓壁距离值
                    float margin = Convert.ToSingle(Margin) * 100;
                    float f2 = Convert.ToSingle(heigh);//筒高
                    float f3 = Convert.ToSingle(xiazhui);//下锥高
                    float f5 = Convert.ToSingle(Top);//顶高
                    string str_height_total = heigh;//仓高度值
                    string str_top_height = Top;//顶高
                    float f4 = Convert.ToSingle(shangzhui);//上锥高
                    SZ = f4* 100;
                    //分别表示前向点数和后向点数

                    label9.Text = "上锥高度";
                    label15.Text = fangchang1 + "米";

                    int sum = 0;//原始点数组的总数

                   // write("backAll" + backAll);

                    string[] str = backAll.Split(';');


                    //获取前向点和后向点的个数
                    for (int i = 0; i < str.Length - 1; i++)
                    {
                        string[] s = str[i].Split('+');
                        //write("s[0]" + s[0]);
                        int angle_int = Convert.ToInt32(s[0]);
                        //write("angle_int" + angle_int);
                        string flag = Convert.ToString(angle_int, 2);
                        //write("***************" + flag);
                        //write("flag.Length"+ flag.Length); 
                        if (flag.Length == 8)//后向角度
                        {


                            b_num++;
                        }
                        else//前向角度
                        {
                            f_num++;
                        }

                    }

                    //write("后向点数" + b_num);

                    //write("前向点数" + f_num);


                    //初始化数组
                    front_arr = new TopCheckData[f_num];
                    for (int i = 0; i < f_num; i++)
                    {
                        front_arr[i] = new TopCheckData();
                    }

                    back_arr = new TopCheckData[b_num];
                    for (int i = 0; i < b_num; i++)
                    {
                        back_arr[i] = new TopCheckData();
                    }

                    point_num = b_num + f_num;

                    //初始化原始数据数组
                    OriginalValue1 = new TopCheckData[point_num];
                    for (int i = 0; i < point_num; i++)
                    {
                        OriginalValue1[i] = new TopCheckData();//每一个开辟地址空间
                    }

                    MeasureValueTop = new TopCheckData[50];
                    for (int i = 0; i < 50; i++)
                    {
                        MeasureValueTop[i] = new TopCheckData();//每一个开辟地址空间
                    }

                    f_num = 0;
                    b_num = 0;
                    point_num = 0;

                    for (int i = 0; i < str.Length - 1; i++)
                    {
                        string[] s = str[i].Split('+');
                        int angle_int = Convert.ToInt32(s[0]);
                        string flag = Convert.ToString(angle_int, 2);
                        //write("margin" + margin);
                        if (flag.Length == 8)//后向角度
                        {
                            //处理后向角度
                            string del = flag.Substring(1, flag.Length - 1);
                            //write("del" + del);
                            del = 0 + del;
                            int angle = (int)Convert.ToInt64(del, 2);
                            //write("angle" + angle);

                            back_arr[b_num].Angle = angle;
                            back_arr[b_num].MeansureLength = Convert.ToSingle(s[1]);
                            back_arr[b_num].LeftLength = margin - Convert.ToSingle(s[1]) * (float)(Math.Sin(angle * Math.PI / 180));
                            back_arr[b_num].CalcRadius = margin - Convert.ToSingle(s[1]) * (float)(Math.Sin(angle * Math.PI / 180));
                            back_arr[b_num].CalcHeight = Convert.ToSingle(s[1]) * (float)(Math.Cos(angle * Math.PI / 180));
                            write("b_num" + b_num + "*" + "back_arr[b_num].Angle" + back_arr[b_num].Angle+ "back_arr[b_num].MeansureLength"+ back_arr[b_num].MeansureLength+ "back_arr[b_num].LeftLength"+ back_arr[b_num].LeftLength+ " back_arr[b_num].CalcRadius" + back_arr[b_num].CalcRadius+ "  back_arr[b_num].CalcHeight" + back_arr[b_num].CalcHeight);
                            //write(back_arr[b_num].MeansureLength + "back_arr[b_num].LeftLength");

                            //OriginalValue1[point_num].Angle = angle;
                            //OriginalValue1[point_num].MeansureLength = Convert.ToSingle(s[1]);
                            //OriginalValue1[point_num].LeftLength = margin - Convert.ToSingle(s[1]) * (float)(Math.Sin(angle * Math.PI / 180));
                            //OriginalValue1[point_num].CalcRadius = margin - Convert.ToSingle(s[1]) * (float)(Math.Sin(angle * Math.PI / 180));
                            //OriginalValue1[point_num].CalcHeight = Convert.ToSingle(s[1]) * (float)(Math.Cos(angle * Math.PI / 180));
                            //write("angle后向" + angle + "*" + " OriginalValue1[point_num].CalcRadius" + OriginalValue1[point_num].CalcRadius+ "OriginalValue1[point_num].CalcHeight" + OriginalValue1[point_num].CalcHeight);
                            b_num++;
                            point_num++;

                        }
                        else//前向角度
                        {
                            front_arr[f_num].Angle = Convert.ToSingle(s[0]);
                            front_arr[f_num].MeansureLength = Convert.ToSingle(s[1]);
                            front_arr[f_num].LeftLength = (Convert.ToSingle(s[1]) * (float)(Math.Sin(Convert.ToSingle(s[0]) * Math.PI / 180))) + margin;
                            front_arr[f_num].CalcRadius = (Convert.ToSingle(s[1]) * (float)(Math.Sin(Convert.ToSingle(s[0]) * Math.PI / 180))) + margin;
                            front_arr[f_num].CalcHeight = Convert.ToSingle(s[1]) * (float)(Math.Cos(Convert.ToSingle(s[0]) * Math.PI / 180));
                            //write("f_num" + f_num + "*" + "front_arr[f_num].Angle " + front_arr[f_num].Angle+ "front_arr[f_num].MeansureLength"+ front_arr[f_num].MeansureLength+ "front_arr[f_num].LeftLength "+ front_arr[f_num].LeftLength+ "front_arr[f_num].CalcRadius"+ front_arr[f_num].CalcRadius+ "front_arr[f_num].CalcHeight"+ front_arr[f_num].CalcHeight);

                            //OriginalValue1[point_num].Angle = Convert.ToSingle(s[0]);
                            //OriginalValue1[point_num].MeansureLength = Convert.ToSingle(s[1]);
                            //OriginalValue1[point_num].LeftLength = Convert.ToSingle(s[1]) * (float)(Math.Sin(Convert.ToSingle(s[0]) * Math.PI / 180)) + margin;
                            //OriginalValue1[point_num].CalcRadius = Convert.ToSingle(s[1]) * (float)(Math.Sin(Convert.ToSingle(s[0]) * Math.PI / 180)) + margin;
                            //OriginalValue1[point_num].CalcHeight = OriginalValue1[i].MeansureLength * (float)(Math.Cos(Convert.ToSingle(s[0]) * Math.PI / 180));
                            //write("angle===前向==" + Convert.ToSingle(s[0]) + "*" + "  OriginalValue1[point_num].CalcRadius" + OriginalValue1[point_num].CalcRadius+ " OriginalValue1[point_num].CalcHeight" + OriginalValue1[point_num].CalcHeight);
                            f_num++;
                            point_num++;
                        }

                    }



                    for(int i = 0; i < b_num; i++)
                    {
                        OriginalValue1[i] = back_arr[i];

                    }

                    for (int i = 0; i < b_num -1; i++){
                        for(int j= b_num - 1; j > i; j--)
                        {
                            if(OriginalValue1[j-1].Angle < OriginalValue1[j].Angle)
                            {
                                TopCheckData temp = OriginalValue1[j - 1];
                                OriginalValue1[j - 1] = OriginalValue1[j];
                                OriginalValue1[j] = temp;
                            }
                        }
                    }
                    write("排序后");

                    write("b_num" + b_num+ "f_num" + f_num);

 

                    for(int i = 0; i < f_num; i++)
                    {
                       // write("front_arr" + i+ " front_arr[f_num].Angle" + front_arr[i].Angle);
                        OriginalValue1[i+ b_num] = front_arr[i];
                    }





                    for (int i = 0; i < point_num; i++)
                    {
                        //write("OriginalValue1[point_num].Angle" + OriginalValue1[i].Angle + "OriginalValue1[point_num].MeansureLength" + OriginalValue1[i].MeansureLength + "OriginalValue1[point_num].LeftLength" + OriginalValue1[i].LeftLength + "OriginalValue1[point_num].CalcRadius" + OriginalValue1[i].CalcRadius + " OriginalValue1[point_num].CalcHeight" + OriginalValue1[i].CalcHeight);
                    }




                    try
                    {
                        if (f3 < 0.1)
                        {
                            //判断是平底仓还是锥底仓,更改画图背景
                            this.panel2.BackgroundImage = global::Warehouse.Properties.Resources.ping01;
                            this.panel1.BackgroundImage = global::Warehouse.Properties.Resources.ping01;
                        }
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("设置料仓背景图出错：错误" + ee.ToString());
                    }





                    string stepangle_str = jaiodu;//步进角
                    try
                    {
                        diameter = float.Parse(zhijing) * 100;//直径
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
                        height_total = (f2 + f3 + f4) * 100;//总高度
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

                    //write("back_arr长度" + back_arr.Length + "b_num" + b_num);
                    //write("front_arr长度" + front_arr.Length);
                   // write("diameter长度" + diameter);

                    //后向数据过滤
                    Process_back(back_arr, b_num);


                    //前向数组过滤
                    Process_front(front_arr, f_num);



                    //for (int i = 0; i < point_num; i++)
                    //{
                    //    if (OriginalValue1[i].CalcRadius > data_x_max)
                    //    {
                    //        data_x_max = OriginalValue1[i].CalcRadius;
                    //    }

                    //    if ((height_total - f5 * 100 - OriginalValue1[i].CalcHeight) > data_y_max)
                    //    {
                    //        data_y_max = height_total - f5 * 100 - OriginalValue1[i].CalcHeight;
                    //    }
                    //}

                    //write("data_x_max" + data_x_max + "data_y_max" + data_y_max);

                    //write("final_count********" + final_count);



                    for (int i = 0; i < final_count; i++)
                    {
                       // write("MeasureValueTop[i].Angle" + MeasureValueTop[i].Angle + "MeasureValueTop[i].MeansureLength" + MeasureValueTop[i].MeansureLength + "MeasureValueTop[i].CalcRadius" + MeasureValueTop[i].CalcRadius + "MeasureValueTop[i].CalcHeight" + MeasureValueTop[i].CalcHeight + "MeasureValueTop[i].LeftLength" + MeasureValueTop[i].LeftLength);
                    }

 


                    DataCheckTop(MeasureValueTop.Length);


                    for (int i = 0; i < final_count; i++)
                    {
                        //write("MeasureValueTop[i].Angle" + MeasureValueTop[i].Angle + "MeasureValueTop[i].MeansureLength" + MeasureValueTop[i].MeansureLength + "MeasureValueTop[i].CalcRadius" + MeasureValueTop[i].CalcRadius + "MeasureValueTop[i].CalcHeight" + MeasureValueTop[i].CalcHeight + "MeasureValueTop[i].LeftLength" + MeasureValueTop[i].LeftLength);
                    }

                    //MessageBox.Show("height_total " + height_total);
                    for (int i = 0; i < final_count; i++)
                    {

                        if (MeasureValueTop[i].CalcRadius > data_x_max_m)
                        {
                            data_x_max_m = MeasureValueTop[i].CalcRadius;
                        }
                        if ((height_total - f5 * 100 - MeasureValueTop[i].CalcHeight) > data_y_max_m)
                        {
                            data_y_max_m = height_total - f5*100 - MeasureValueTop[i].CalcHeight;
                        }
                    }

                    // write("data_x_max_m" + data_x_max_m);

                    // write("data_y_max_m" + data_y_max_m);

                    if (diameter > data_x_max_m)
                    {
                        data_x_max_m = diameter;
                    }

                    if (height_total > data_y_max_m)
                    {
                        data_y_max_m = height_total;
                    }
                }
                else if (type.Equals("0")|| type.Equals("1")|| type.Equals("4"))
                {

                    this.label15.Text = Wheelbas + "米";
                    //MessageBox.Show("侧置直径");
                    if (type.Equals("1")|| type.Equals("4"))
                    {
                        //清空回传信息
                        backAn = "";
                        backData = "";

                        string[] readArray = new string[2];
                        //Log("获取到的3D点数据是str = " + backAll);
                        string[] pint = backAll.Split(';');
                        int len1 = pint.Length;
                        int a = len1 - 1;
                        //MessageBox.Show("a" + a);
                        //Log("点个数是str = " + a);
                        float[] xvalue = new float[len1 - 1];
                        float[] yvalue = new float[len1 - 1];
                        float[] zvalue = new float[len1 - 1];
                        try
                        {
                            for (int i = 0; i < len1 - 1; i++)
                            {
                                string[] p = pint[i].Split('+');
                                if (p[1].Equals("0"))//水平角度是0
                                {
                                    backData += p[2] + ",";
                                    //设置角度
                                    int jiaojiao = int.Parse(p[0]) / int.Parse(jaiodu);



                                    backAn += jiaojiao + ",";
                                }

                            }
                        }
                        catch (Exception ee)
                        {
                            Log(ee.ToString());
                        }
                    }




                    backData = backData.Substring(0, backData.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单//backData = "809,796,784,774,766,758,752,747,744,741,739,737,1036,735,736,740,748,760,779,806,849,913,990,1085,1220";
                    int len = backData.Split(',').Length;
                    //MessageBox.Show("len" + len);
                    backAn = backAn.Substring(0, backAn.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单

                    //Log("接收到的回传数据!!!!!!!!!!!!!!!!：" + backData);
                    //Log("接收到的角度数据!!!!!!!!!!!!!!!!：" + backAn);


                    //string str_distance = "178,177,177,177,177,178,179,180,181,183,184,186,189,191,194,197,200,204,207,212,217,217,229,303,243,242,261,271,271,289,327,344,365,389,414,446,482,529";//测量距离数据。
                    string str_distance = backData;//测量距离数据。
                    write("测量距离数据" + backData);
                    string str_x_init = Margin;//仓壁距离值
                    float f2 = Convert.ToSingle(heigh);//筒高
                    float f3 = Convert.ToSingle(xiazhui);//下锥高
                    string str_height_total = heigh;//仓高度值
                    string str_top_height = Top;//顶高
                    write("仓壁距离值" + str_x_init + "筒高" + f2 + "下锥高" + f3 + "顶高" + str_top_height + "仓高度值" + str_height_total);


                    try
                    {
                        if (f3 < 0.1)
                        {
                            //判断是平底仓还是锥底仓,更改画图背景
                            this.panel2.BackgroundImage = global::Warehouse.Properties.Resources.ping01;
                            this.panel1.BackgroundImage = global::Warehouse.Properties.Resources.ping01;
                        }
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("设置料仓背景图出错：错误" + ee.ToString());
                    }





                    string stepangle_str = jaiodu;//步进角
                    try
                    {
                        diameter = float.Parse(zhijing) * 100;//直径
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
                        height_total = (f2 + f3) * 100;//总高度
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
                    //获取到的测量长度的数组
                    string[] distance_strarray = str_distance.Split(',');
                    write("长度=" + distance_strarray.Length + "直径：" + diameter + ",仓壁距离值:" + x_init + ",有效高度：" + height_total + ",顶高：" + top_height + ",旋转角度：" + stepangle);


                    string[] anal = backAn.Split(',');

                    //为了防止回传数据时的错误。进行两个数组的选择排序，每次选择最小的放在最前面。
                    //进行排序
                    for (int i = 0; i < anal.Length; i++)
                    {
                        int min = int.Parse(anal[i]);//定义当前的是最小的
                        int sit = i;
                        for (int j = i + 1; j < anal.Length; j++)
                        {
                            int pnum = int.Parse(anal[j]);
                            if (min > pnum)//如果有比他还小的，记录最小的下标，
                            {
                                min = pnum;
                                sit = j;
                            }

                        }
                        //两个数组进行排序
                        string temp = anal[i];
                        anal[i] = anal[sit];
                        anal[sit] = temp;


                        string temp1 = distance_strarray[i];
                        distance_strarray[i] = distance_strarray[sit];
                        distance_strarray[sit] = temp1;
                    }

                    string str = "";
                    for (int i = 0; i < anal.Length; i++)
                    {
                        str += anal[i] + ",";
                    }
                    //write("拼接的字符串==" + str);

                    //MessageBox.Show(len + "\r\n" + anal.Length);
                    for (int i = 0; i < anal.Length; i++)
                    {
                        int xiaojiao = int.Parse(anal[i]);//每次的那个值
                                                          //Log("角度：" + xiaojiao);
                        angle_list.Add(xiaojiao);
                    }
                    //初始化原始数据数组
                    OriginalValue = new CheckData[distance_strarray.Length];
                    //初始化过滤数据数组
                    MeansureValue = new CheckData[distance_strarray.Length];

                    for (int i = 0; i < distance_strarray.Length; i++)
                    {
                        OriginalValue[i] = new CheckData();//每一个开辟地址空间
                        MeansureValue[i] = new CheckData();
                    }
                    for (int i = 0; i < distance_strarray.Length; i++)
                    {
                        try
                        {
                            //Trim()删除字符串头部及尾部出现的空格，删除的过程为从外到内，直到碰到一个非空格的字符为止，所以不管前后有多少个连续的空格都会被删除掉
                            OriginalValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                            MeansureValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                        }
                        catch
                        {
                            return;
                        }
                    }
                    //angle_list放着角度*stepangle（自己设置的角度）
                    if (angle_list.Count != 0)
                    {
                        for (int i = 0; i < distance_strarray.Length; i++)
                        //将i改为真正获取的角度值
                        {
                            OriginalValue[i].CalcRadius = OriginalValue[i].MeansureLength * (float)(Math.Sin((angle_list[i] * stepangle) * Math.PI / 180));// + x_init。不应该加上边距
                            if (OriginalValue[i].CalcRadius > data_x_max_c)
                            {
                                data_x_max_c = OriginalValue[i].CalcRadius;
                            }

                            OriginalValue[i].CalcHeight = OriginalValue[i].MeansureLength * (float)(Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
                            if ((height_total - OriginalValue[i].CalcHeight) > data_y_max_c)
                            {
                                data_y_max_c = height_total - OriginalValue[i].CalcHeight;
                            }
                        }
                    }

                    ////将处理的数据传给矫正数组
                    for (int i = 0; i < distance_strarray.Length; i++)
                    {
                        MeansureValue[i].CalcHeight = OriginalValue[i].CalcHeight;
                        MeansureValue[i].MeansureLength = OriginalValue[i].MeansureLength;
                        MeansureValue[i].CalcRadius = OriginalValue[i].CalcRadius;
                    }

                    //进行二维数据过滤
                    DataCheck(MeansureValue.Length);
                    for (int i = 0; i < MeansureValue.Length; i++)
                    {
                        write(i + "高度值" + MeansureValue[i].CalcHeight);
                    }
                    //write("*************************原始值");

                    for (int i = 0; i < OriginalValue.Length; i++)
                    {
                        write(i + "原始高度值" + OriginalValue[i].CalcHeight);
                    }
                    //MessageBox.Show(MeansureValue.Length + "");
                    //write(MeansureValue.Length+"************************");
                    //write("原始最大"+" data_x_max_c" + data_x_max_c + "data_y_max_c" + data_y_max_c);

                    if (diameter > data_x_max_c)
                    {
                        data_x_max_c = diameter;
                    }

                    if (height_total > data_y_max_c)
                    {
                        data_y_max_c = height_total;
                    }
                    //write(" data_x_max_c" + data_x_max_c + "data_y_max_c" + data_y_max_c);
                }

            }
            catch (Exception ea)
            {
                MessageBox.Show("数据没有及时回传！请再盘一次" + ea.ToString());
                this.Close();
            }
        }

        //顶置直径绘图方法
        private void panel1Paint()
        {

            Graphics g = panel1.CreateGraphics();

            //float x_max = panel1.Width - 10;
            //float y_max = panel1.Height - 10;
            float x_max = panel1.Width;
            float y_max = panel1.Height;
            //MessageBox.Show(" x_max" + x_max + " y_max" + y_max);
            //画直径示意线
            Pen p = new Pen(Color.Green, 2);

            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
            //画边框线
            p = new Pen(Color.Black, 1);
            //p.DashStyle =
            //g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
            //画中心线
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

            //画上锥线
            float y0 = y_max - (height_total -SZ)* (y_max / data_y_max_m);
            g.DrawLine(p, new PointF(0, y0), new PointF(x_max , y0));
            //write("height_total -SZ" + (height_total - SZ));


            Font drawfont = new Font("宋体", 8);
            SolidBrush drawbrush = new SolidBrush(Color.Black);
            //如果是负值，则不显示图像
            if (binState.Equals("3"))
            {
                return;
            }

     


            //for(int i=0;i< OriginalValue1.Length; i++)
            //{
            //    write("画图前"+"OriginalValue1[j ].Angle" + OriginalValue1[i].Angle + "OriginalValue1[j - 1].MeansureLength" + OriginalValue1[i].MeansureLength + "OriginalValue1[j - 1].CalcRadius" + OriginalValue1[i].CalcRadius + "OriginalValue1[i].CalcHeight"+ OriginalValue1[i].CalcHeight);
            //}

            write("y_max" + y_max);
            //画一次过滤数据
            for (int i = 0; i < OriginalValue1.Length; i++)
            {
                //write("height_total" + height_total + "top_height" + top_height);
                float x1 = OriginalValue1[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max_m);
                float y1 = height_total - top_height - OriginalValue1[i].CalcHeight;
                //write("高度" + OriginalValue1[i].CalcHeight);
                y1 = y_max - y1 * (y_max / data_y_max_m);
                //y1 = y_max - y1 * (y_max / 2600);
                //write("x1坐标" + x1 + "y1坐标" + y1 + "OriginalValue1[i].CalcHeight" + OriginalValue1[i].CalcHeight+ "OriginalValue1[i].CalcRadius"+ OriginalValue1[i].CalcRadius + "data_x_max_m" + data_x_max_m + "data_y_max_m" + data_y_max_m);

                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x1, y1, 2, 2);
                if (i + 1 < OriginalValue1.Length)
                {
                    float x2 = OriginalValue1[i+1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max_m);
                    float y2 = height_total - top_height - OriginalValue1[i + 1].CalcHeight;
                    //y2 = y_max - y2 * (y_max / data_y_max_m);
                    y2 = y_max - y2 * (y_max / 2600);
                    //write("后向x2坐标" + x2 + "后向y2坐标" + y2);

                    g.FillEllipse(brush, x2, y2, 2, 2);
                    p = new Pen(Color.Red, 1);
                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));
                }

                //str += OriginalValue1[i].CalcHeight + "," + OriginalValue1[i].CalcRadius + "\r\n";
            }



        }

        //顶置直径过滤绘图
        private void panel2Paint()
        {

            //DataCheck(MeansureValue.Length);
            Graphics g = panel2.CreateGraphics();

            float x_max = panel2.Width;
            float y_max = panel2.Height;


            //画直径示意线
            Pen p = new Pen(Color.Green, 2);

            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
            //画边框线
            p = new Pen(Color.Black, 1);
            //p.DashStyle =
            //g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
            //画中心线
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

            //画上锥线
            float y0 = y_max - (height_total - SZ) * (y_max / data_y_max_m);
            g.DrawLine(p, new PointF(0, y0), new PointF(x_max, y0));

            //write("height_total -SZ=====" + (height_total - SZ));

            Font drawfont = new Font("宋体", 8);
            SolidBrush drawbrush = new SolidBrush(Color.Black);
            //画一次原始数据

            //write("过滤以后");
            //for(int i = 0; i < MeasureValueTop.Length; i++)
            //{
            //    write("MeasureValueTop[i].Angle" + MeasureValueTop[i].Angle + "MeasureValueTop[i].CalcRadius" + MeasureValueTop[i].CalcRadius + "MeasureValueTop[i].CalcHeight" + MeasureValueTop[i].CalcHeight);
            //}

            //画二次检验线
            string str = "";
            //如果是负值，则不显示图像
            if (binState.Equals("3"))
            {
                return;
            }
            //画一次过滤数据
            for (int i = 0; i < final_count; i++)
            {
                //MessageBox.Show("height_total" + height_total + "top_height" + top_height);
                float x1 = MeasureValueTop[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max_m);
                float y1 = height_total - top_height - MeasureValueTop[i].CalcHeight;
                y1 = y_max - y1 * (y_max / data_y_max_m);
                //write("后向x1坐标" + x1 + "后向y1坐标" + y1);

                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x1, y1, 2, 2);
                if (i + 1 < final_count)
                {
                    float x2 = MeasureValueTop[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max_m);
                    float y2 = height_total - top_height - MeasureValueTop[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max_m);
                    //write("后向x2坐标" + x2 + "后向y2坐标" + y2);

                    g.FillEllipse(brush, x2, y2, 2, 2);
                    p = new Pen(Color.Red, 1);
                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));
                }

                str += MeasureValueTop[i].CalcHeight + "," + MeasureValueTop[i].CalcRadius + "\r\n";
            }


            //MessageBox.Show(str);

        }

        private void panel3Paint()
        {
            Graphics g = panel1.CreateGraphics();

            //float x_max = panel1.Width - 10;
            //float y_max = panel1.Height - 10;
            float x_max = panel1.Width;
            float y_max = panel1.Height;
            //画直径示意线
            Pen p = new Pen(Color.Green, 2);

            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
            //画边框线
            p = new Pen(Color.Black, 1);
            //p.DashStyle =
            //g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
            //画中心线
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

            Font drawfont = new Font("宋体", 8);
            SolidBrush drawbrush = new SolidBrush(Color.Black);
            //如果是负值，则不显示图像
            if (binState.Equals("3"))
            {
                return;
            }
            //画一次原始数据
            for (int i = 0; i < OriginalValue.Length; i++)
            {
                float x1 = OriginalValue[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max_c);
                float y1 = height_total - top_height - OriginalValue[i].CalcHeight;//实际的测量出的
                y1 = y_max - y1 * (y_max / data_y_max_c);
                //write("测量点二维坐标" + "X1111坐标：" + x1 + "Y1111坐标：" + y1 + "/n");
                //计算第一个点的坐标
                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x1, y1, 2, 2);

                //System.Console.WriteLine("y1:{0}\n", y1);
                if (i + 1 < OriginalValue.Length)
                {
                    float x2 = OriginalValue[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max_c);
                    float y2 = height_total - top_height - OriginalValue[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max_c);
                   // write("测量点二维坐标" + "X2222坐标：" + x2 + "Y2222坐标：" + y2);
                    //g.FillEllipse(brush, x2, y2, 2, 2);

                    p = new Pen(Color.Blue, 1);

                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));//将两个点连线

                }
            }

        }

        private void panel4Paint()
        {
            //DataCheck(MeansureValue.Length);
            Graphics g = panel2.CreateGraphics();

            float x_max = panel2.Width;
            float y_max = panel2.Height;


            //画直径示意线
            Pen p = new Pen(Color.Green, 2);

            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
            //画边框线
            p = new Pen(Color.Black, 1);
            //p.DashStyle =
            //g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
            //画中心线
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

            Font drawfont = new Font("宋体", 8);
            SolidBrush drawbrush = new SolidBrush(Color.Black);
            //画一次原始数据

            //画二次检验线
            string str = "";
            //如果是负值，则不显示图像
            if (binState.Equals("3"))
            {
                return;
            }
            //画一次过滤数据
            for (int i = 0; i < MeansureValue.Length; i++)
            {
                float x1 = MeansureValue[i].CalcRadius;
                x1 = x1 * (x_max / data_x_max_c);
                float y1 = height_total - top_height - MeansureValue[i].CalcHeight;
                y1 = y_max - y1 * (y_max / data_y_max_c);
                //write("过滤测量点二维坐标" + "X1111坐标：" + x1 + "Y1111坐标：" + y1 + "/n");

                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x1, y1, 2, 2);
                if (i + 1 < MeansureValue.Length)
                {
                    float x2 = MeansureValue[i + 1].CalcRadius;
                    x2 = x2 * (x_max / data_x_max_c);
                    float y2 = height_total - top_height - MeansureValue[i + 1].CalcHeight;
                    y2 = y_max - y2 * (y_max / data_y_max_c);
                    //write("过滤测量点二维坐标" + "X2222坐标：" + x2 + "Y2222坐标：" + y2);

                    g.FillEllipse(brush, x2, y2, 2, 2);
                    p = new Pen(Color.Red, 1);
                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));
                }

                //str += MeansureValue[i].CalcHeight + "," + MeansureValue[i].CalcRadius + "\r\n";
            }


            //MessageBox.Show(str);

        }



        //“使用此数据计算”原始数据
        private void button1_Click(object sender, EventArgs e)
        {
            if (binState.Equals("3"))
            {
                return;
            }
            //MessageBox.Show("计算原始数据");
            //已知了是否是半径直径，有测量参数的数据，有步进角，有测量点的个数
            CheckData[] dataValue = new CheckData[OriginalValue.Length];
            for (int n = 0; n < dataValue.Length; n++)
            {
                dataValue[n] = new CheckData();//深拷贝，必须每个都开辟地址空间
            }
            //dataValue = OriginalValue;//原始数据是厘米为单位的，这里将他们改成米为单位的
            //for(int j = 0; j < OriginalValue.Length; j++)
            string str = "";
            for (int i = 0; i < dataValue.Length; i++)
            {
                dataValue[i].CalcHeight = OriginalValue[i].CalcHeight / 100;
                dataValue[i].CalcRadius = OriginalValue[i].CalcRadius / 100;
                dataValue[i].MeansureLength = OriginalValue[i].MeansureLength / 100;
                str += dataValue[i].MeansureLength + "," + dataValue[i].CalcHeight + "," + dataValue[i].CalcRadius + "\r\n";

            }
            //MessageBox.Show(str);
            float f1 = Convert.ToSingle(zhijing);//直径
            float f2 = Convert.ToSingle(heigh);//筒高
            float f3 = Convert.ToSingle(xiazhui);//下锥高

            //扫描仪距顶高度
            float f4 = Convert.ToSingle(Top);//
            //边距
            float f5 = Convert.ToSingle(Margin);//
            float f6 = Convert.ToSingle(Wheelbas);//轴距
            float f7 = Convert.ToSingle(jaiodu);//轴距
            float f8 = Convert.ToSingle(midu);


            //需要输入筒仓的参数，通过数据库中提取出来的
            WarehouseStructType wareData = new WarehouseStructType(
            f1,
            (f1 / 2F),
            f2,
            f3,
            f4,
            f5,
            f6,
            f7);

            //CalV cv = new CalV();
            //查询数据，找出k，b值
            if (k == null || k == "" || b == null || b == "")
            {
                k = "100";
                b = "0";
            }

            float kk = float.Parse(k);
            float bb = float.Parse(b);

            //MessageBox.Show("K的值是==" + kk + ",b的值是" + bb);

            float v = kk * VolumeCalculate(int.Parse(binState), dataValue.Length, f2 + f3 - f4, dataValue, wareData) / 100 + bb;//除以100是将k值除以100
            label24.Text = v + "立方米";
            label25.Text = v * f8 + "吨";

            for (int i = 0; i < dataValue.Length; i++)             //也可以用sizeof()写
            {
                dataValue[i] = null;//为空回收空间
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {


            //MessageBox.Show("计算过滤数据");

            CheckData[] dataValue = new CheckData[MeansureValue.Length];
            for (int n = 0; n < dataValue.Length; n++)
            {
                dataValue[n] = new CheckData();//深拷贝，必须每个都开辟地址空间
            }
            string str = "";
            //DataCheck(MeansureValue.Length);
            for (int i = 0; i < dataValue.Length; i++)
            {
                dataValue[i].CalcHeight = MeansureValue[i].CalcHeight / 100;
                dataValue[i].CalcRadius = MeansureValue[i].CalcRadius / 100;
                dataValue[i].MeansureLength = MeansureValue[i].MeansureLength / 100;

                str += dataValue[i].MeansureLength + "," + dataValue[i].CalcHeight + ",半径=" + dataValue[i].CalcRadius + "\r\n";

            }
            //MessageBox.Show(str);
            float f1 = Convert.ToSingle(zhijing);//直径

            float f2 = Convert.ToSingle(heigh);//筒高
            float f3 = Convert.ToSingle(xiazhui);//下锥高
            //扫描仪距顶高度
            float f4 = Convert.ToSingle(Top);//
            //边距
            float f5 = Convert.ToSingle(Margin);//
            float f6 = Convert.ToSingle(Wheelbas);//轴距
            float f7 = Convert.ToSingle(jaiodu);//轴距
            float f8 = Convert.ToSingle(midu);
            //MessageBox.Show("直径是==" + f1);

            //需要输入筒仓的参数，通过数据库中提取出来的
            WarehouseStructType wareData = new WarehouseStructType(
            f1,
            (f1 / 2F),
            f2,
            f3,
            f4,
            f5,
            f6,
            f7);

            //CalV cv = new CalV();
            //查询数据，找出k，b值
            if (k == null || k == "" || b == null || b == "")
            {
                k = "100";
                b = "0";
            }

            float kk = float.Parse(k);
            float bb = float.Parse(b);

            //MessageBox.Show("K的值是==" + kk + ",b的值是" + bb);

            float v = kk * VolumeCalculate(int.Parse(binState), dataValue.Length, f2 + f3 - f4, dataValue, wareData) / 100 + bb;//除以100是将k值除以100
            label24.Text = v + "立方米";
            label25.Text = v * f8 + "吨";

            for (int i = 0; i < dataValue.Length; i++)             //也可以用sizeof()写
            {
                dataValue[i] = null;
            }
        }
        /**
        * @brief  数据有效性检验
          * @param  angle 全部测量点个数
        * @retval none
      */
        void DataCheck(int angle)
        {
            int i;
            float average = 0;
            int ave_heightdiff = 0;
            int pointcount = 0;
            int ErrorCount = 0;

            

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeansureValue[i].CalcHeight;//距仓顶高度
            }
            average /= angle;//平均高度

            //计算平均高度差
            for (i = 1; i < angle; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight);
                pointcount++;
            }
            ave_heightdiff = ave_heightdiff / pointcount;




            ////进行数据检验，粗过滤
            for (i = 1; i < angle; i++)
            {
                //MessageBox.Show("MeansureValue[i]*************for" + MeansureValue[i].CalcRadius);
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
                    //MessageBox.Show("MeansureValue[i].CalcRadius" + MeansureValue[i].CalcRadius + "MeansureValue[i - 1].CalcRadius" + MeansureValue[i - 1].CalcRadius);
                    ReplaceValues(i);
                    //MessageBox.Show("MeansureValue[i]*************" + MeansureValue[i].CalcRadius);
                }
            }

            //进行数据滤波，细过滤
            for (i = 1; i < angle; i++)
            {

                if (i != angle - 1)
                {

                    if (Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                        Math.Abs(MeansureValue[i + 1].CalcHeight - MeansureValue[i].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                       (MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) * (MeansureValue[i + 1].CalcHeight - MeansureValue[i].CalcHeight) < 0)
                    {
                        ErrorCount++;

                        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;
                        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;
                        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;

                    }

                }
                else
                {
                    if (i - 2 >= 0)
                    {
                        if (Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE&&(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) * (MeansureValue[i - 1].CalcHeight - MeansureValue[i - 2].CalcHeight) < 0)
                        {
                            ErrorCount++;

                            ReplaceValues(i);

                        }
                    }
                    else
                    {
                        if (Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                        {
                            ErrorCount++;

                            ReplaceValues(i);

                        }

                    }
                }

            }
        }

        /// <summary>
        /// 顶置版本DataCheck
        /// </summary>
        /// <param name="angle"></param>
        void DataCheckTop(int angle)
        {
            int i;
            float average = 0;
            int ave_heightdiff = 0;
            int pointcount = 0;
            int ErrorCount = 0;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeasureValueTop[i].CalcHeight;//距仓顶高度
            }
            average /= angle;//平均高度

            //计算平均高度差
            for (i = 1; i < angle; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight);
                pointcount++;
            }
            ave_heightdiff = ave_heightdiff / pointcount;



      

            ////进行数据检验，粗过滤
            for (i = 1; i < angle; i++)
            {

                if ((MeasureValueTop[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
                    || (MeasureValueTop[i].CalcHeight > MeasureValueTop[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
                {
                    ReplaceValuesTop(i);
                }

                if ((MeasureValueTop[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
                    || (MeasureValueTop[i].CalcHeight < MeasureValueTop[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
                {

                    //ReplaceValuesTop(i);
                }

                //判断半径，正常情况半径越来越大
                if (MeasureValueTop[i].CalcRadius < MeasureValueTop[i - 1].CalcRadius)//半径比前一个小，错误
                {

                    ReplaceValuesTop(i);
  
                }
            }



            //进行数据滤波，细过滤
            for (i = 1; i < angle; i++)
            {

                if (i != angle - 1)
                {

                    if (Math.Abs(MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                        Math.Abs(MeasureValueTop[i + 1].CalcHeight - MeasureValueTop[i].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                       (MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight) * (MeasureValueTop[i + 1].CalcHeight - MeasureValueTop[i].CalcHeight) < 0)
                    {
                        ErrorCount++;

                        MeasureValueTop[i].MeansureLength = (MeasureValueTop[i - 1].MeansureLength + MeasureValueTop[i + 1].MeansureLength) / 2;
                        MeasureValueTop[i].CalcHeight = (MeasureValueTop[i - 1].CalcHeight + MeasureValueTop[i + 1].CalcHeight) / 2;
                        MeasureValueTop[i].CalcRadius = (MeasureValueTop[i - 1].CalcRadius + MeasureValueTop[i + 1].CalcRadius) / 2;

                    }

                }
                else
                {
                    if (i - 2 >= 0)
                    {
                        if (Math.Abs(MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE && (MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight) * (MeasureValueTop[i - 1].CalcHeight - MeasureValueTop[i - 2].CalcHeight) < 0)
                        {
                            ErrorCount++;

                            ReplaceValuesTop(i);

                        }
                    }
                    else
                    {
                        if (Math.Abs(MeasureValueTop[i].CalcHeight - MeasureValueTop[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                        {
                            ErrorCount++;

                            ReplaceValuesTop(i);

                        }

                    }
                }

            }



        }


        /// <summary>
        /// 后向数组的过滤
        /// </summary>
        /// <param name="MeasureValue_Back">后向数组</param>
       void Process_back(TopCheckData[] MeasureValue_Back,int num)
       {
            int index, index2;
            double computeangle;


            for (index = 0; index <= num-1; index++)
            {

                if (MeasureValue_Back[index].LeftLength > diameter * BACK_BORDER)
                {
                    //MessageBox.Show("后向数组"+index);
                    if (index != num-1)
                    {
                        if (MeasureValue_Back[index].LeftLength != MeasureValue_Back[index + 1].LeftLength)
                        {
                            computeangle = Math.Atan((double)(Math.Abs(MeasureValue_Back[index + 1].CalcHeight - MeasureValue_Back[index].CalcHeight) /
                                Math.Abs(MeasureValue_Back[index + 1].LeftLength - MeasureValue_Back[index].LeftLength))) * 57.2958;
                            //MessageBox.Show("computeangle" + computeangle);
                            if (computeangle >= 85)
                            { 

                                continue;
                            }
                            else
                            {
                                if (index > 1)
                                {
                                    if (MeasureValue_Back[index].LeftLength >= MeasureValue_Back[index - 1].LeftLength)
                                        continue;
                                    else
                                        //MessageBox.Show("后向加入到MeasureValueTop"+final_count);
                                        MeasureValueTop[final_count++] = MeasureValue_Back[index];
                                }
                                else
                                    MeasureValueTop[final_count++] = MeasureValue_Back[index];
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        computeangle = (Math.Atan((double)(Math.Abs(MeasureValue_Back[index].CalcHeight - MeasureValue_Back[index - 1].CalcHeight) /
                                Math.Abs(MeasureValue_Back[index].LeftLength - MeasureValue_Back[index - 1].LeftLength))) * 57.2958)/Math.PI*180;
                        if (computeangle >= 85 || (MeasureValue_Back[index].LeftLength >= MeasureValue_Back[index - 1].LeftLength))
                    
                            continue;
                        else
                            MeasureValueTop[final_count++] = MeasureValue_Back[index];

                    }


                }
                else
                    continue;

            }
            if (final_count > 1)
            {
                for (index = 0; index < final_count - 1; index++)
                {
                    for (index2 = 0; index2 < final_count - 1 - index; index2++)
                    {
                        if (MeasureValueTop[index2].LeftLength > MeasureValueTop[index2 + 1].LeftLength)
                        {
                            MeasureValue_Temp = MeasureValueTop[index2];
                            MeasureValueTop[index2] = MeasureValueTop[index2 + 1];
                            MeasureValueTop[index2 + 1] = MeasureValue_Temp;

                        }
                    }

                }
            }

        }

        /// <summary>
        /// 前向数组过滤
        /// </summary>
        /// <param name="MeasureValue_Back">前向数组</param>
        /// <param name="num">数组个数</param>
        void Process_front(TopCheckData[] MeasureValue_Front, int num)
        {
            int index, index2;
            double computeangle;
            for (index = 0; index < num-1; index++)
            {
                //write("前向index" + index);

                if (MeasureValue_Front[index].LeftLength < diameter * FRONT_BORDER)
                {
                    if (index != num-1)
                    {
                        if (MeasureValue_Front[index].LeftLength != MeasureValue_Front[index + 1].LeftLength)
                        {
                            computeangle = Math.Atan((double)(Math.Abs(MeasureValue_Front[index + 1].CalcHeight - MeasureValue_Front[index].CalcHeight) /
                                Math.Abs(MeasureValue_Front[index + 1].LeftLength - MeasureValue_Front[index].LeftLength))) * 57.2958;
                            if (computeangle >= 85)
                            { 

                                continue;
                            }
                            else
                            {
                                if (index > 0)
                                {
                                    if (MeasureValue_Front[index].LeftLength <= MeasureValue_Front[index - 1].LeftLength)
                                        continue;
                                    else
                                        MeasureValueTop[final_count++] = MeasureValue_Front[index];
                                }
                                else
                                    MeasureValueTop[final_count++] = MeasureValue_Front[index];
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        computeangle = (Math.Atan((double)(Math.Abs(MeasureValue_Front[index].CalcHeight - MeasureValue_Front[index - 1].CalcHeight) /
                                Math.Abs(MeasureValue_Front[index].LeftLength - MeasureValue_Front[index - 1].LeftLength))) * 57.2958)/Math.PI*180;
                        if (computeangle >= 85 || (MeasureValue_Front[index].LeftLength <= MeasureValue_Front[index - 1].LeftLength))
                            continue;
                        else
                            MeasureValueTop[final_count++] = MeasureValue_Front[index];

                    }


                }
                else
                    continue;
            } 
            if (final_count > 1)
            {
                for (index = 0; index < final_count - 1; index++)
                {
                    for (index2 = 0; index2 < final_count - 1 - index; index2++)
                    {
                        if (MeasureValueTop[index2].LeftLength > MeasureValueTop[index2 + 1].LeftLength)
                        {
                            MeasureValue_Temp = MeasureValueTop[index2];
                            MeasureValueTop[index2] = MeasureValueTop[index2 + 1];
                            MeasureValueTop[index2 + 1] = MeasureValue_Temp;

                        }
                    }

                }
            }

        }


        /// <summary>
        /// 用于三维过滤算法的直径上点的过滤
        /// </summary>
        /// <param name="angle"></param>
        void DataCheck1(int angle, MeasureValueStructType[] zhijing)
        {
            int num = 0;
            int i;
            float average = 0;
            int ave_heightdiff = 0;
            int pointcount = 0;
            int ErrorCount = 0;
            MeasureValueStructType[] MeasureValue = zhijing;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeasureValue[i].CalcHeight;//距仓顶高度
            }
            //MessageBox.Show(angle + "*************8");
            average /= angle;//平均高度


            //计算平均高度差
            for (i = 1; i < angle - 1; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight);
                pointcount++;
            }
            //MessageBox.Show(pointcount + "***pointcount ");
            ave_heightdiff = ave_heightdiff / pointcount;

            ////进行数据检验，粗过滤
            for (i = 1; i < angle; i++)
            {
                if ((MeasureValue[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
                    || (MeasureValue[i].CalcHeight > MeasureValue[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
                {
                    ReplaceValues1(i, MeasureValue);
                }

                if ((MeasureValue[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
                    || (MeasureValue[i].CalcHeight < MeasureValue[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
                {

                    ReplaceValues1(i, MeasureValue);
                }

                //判断半径，正常情况半径越来越大
                if (MeasureValue[i].CalcRadius < MeasureValue[i - 1].CalcRadius)//半径比前一个小，错误
                {
                    ReplaceValues1(i, MeasureValue);
                }
            }

            //进行数据滤波，细过滤
            for (i = 1; i < angle; i++)
            {
                //if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
                //    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
                //{

                //    if (i == angle - 1)//最后一个点，使用覆盖
                //    {
                //        ReplaceValues(i);
                //    }
                //    else//其余点使用平均
                //    {
                //        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
                //        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                //        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                //    }
                //}

                //if (Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                //{


                //    if (i == angle - 1)
                //    {
                //        ReplaceValues(i);
                //    }
                //    else
                //    {
                //          MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
                //          MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                //          MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                //    }


                //}
                if (i != angle - 1)
                {

                    if (Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                        Math.Abs(MeasureValue[i + 1].CalcHeight - MeasureValue[i].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE &&
                       (MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) * (MeasureValue[i + 1].CalcHeight - MeasureValue[i].CalcHeight) < 0)
                    {
                        ErrorCount++;

                        MeasureValue[i].MeasureLength = (MeasureValue[i - 1].MeasureLength + MeasureValue[i + 1].MeasureLength) / 2;
                        MeasureValue[i].CalcHeight = (MeasureValue[i - 1].CalcHeight + MeasureValue[i + 1].CalcHeight) / 2;
                        MeasureValue[i].CalcRadius = (MeasureValue[i - 1].CalcRadius + MeasureValue[i + 1].CalcRadius) / 2;

                    }

                }
                else
                {
                    if (i - 2 >= 0)
                    {
                        if (Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE && (MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) * (MeasureValue[i - 1].CalcHeight - MeasureValue[i - 2].CalcHeight) < 0)
                        {
                            ErrorCount++;

                            ReplaceValues1(i, MeasureValue);

                        }
                    }
                    else
                    {
                        if (Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                        {
                            ErrorCount++;

                            ReplaceValues1(i, MeasureValue);

                        }

                    }
                }

            }
            // MessageBox.Show("******************");
        }


        //没有调用过
        public CheckData[] DataCheck(int angle, CheckData[] MeansureValue)
        {
            int num = 0;
            int i;
            float average = 0;
            int ave_heightdiff = 0;
            int pointcount = 0;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeansureValue[i].CalcHeight;//距仓顶高度
            }
            average /= angle;//平均高度

            for (i = 1; i < angle-1; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight);
                pointcount++;
            }
            ave_heightdiff = ave_heightdiff / pointcount;

            ////进行数据检验，粗过滤
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
                //if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
                //    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
                                                                                                                   //{

                    //    if (i == angle - 1)//最后一个点，使用覆盖
                    //    {
                    //        ReplaceValues(i);
                    //    }
                    //    else//其余点使用平均
                    //    {
                    //MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
                    //MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                    //MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                    //    }
                    //}
                    if (Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                    {


                    if (i == angle - 1)
                    {
                        ReplaceValues(i);
                    }
                    else
                    {
                        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
                        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                    }


                }

            }


            return MeansureValue;
        }

        /// <summary>
        /// 二维过滤算法中直径的调用
        /// </summary>
        /// <param name="i"></param>
        private void ReplaceValues(int i)
        {
            //float f1 = Convert.ToSingle(fangchang1) * 100;//直径
            float f1 = Convert.ToSingle(zhijing) * 100;//直径
            float f5 = Convert.ToSingle(Margin) * 100;//
            if (i > 0)
            {
                MeansureValue[i].CalcHeight = MeansureValue[i - 1].CalcHeight;

                MeansureValue[i].MeansureLength = (float)(MeansureValue[i].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));

                MeansureValue[i].CalcRadius = (float)(MeansureValue[i].MeansureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°

                ////如果校正后的半径距离大于直径，需要使其等于直径-边距，使这个点不参与计算。
                if (MeansureValue[i].CalcRadius > f1 - f5)
                    MeansureValue[i].CalcRadius = f1 - f5;
            }

        }
        /// <summary>
        /// </summary>
        /// <param name="i"></param>
        private void ReplaceValuesTop(int i)
        {
            float f1 = Convert.ToSingle(zhijing) * 100;//直径
            float f5 = Convert.ToSingle(Margin) * 100;//
            if (i > 0)
            {
                MeasureValueTop[i].CalcHeight = MeasureValueTop[i - 1].CalcHeight;

                MeasureValueTop[i].MeansureLength = (float)(MeasureValueTop[i].CalcHeight / Math.Cos(MeasureValueTop[i].Angle* Math.PI / 180));

                MeasureValueTop[i].CalcRadius = (float)(MeasureValueTop[i].MeansureLength * Math.Sin(MeasureValueTop[i].Angle* Math.PI / 180)); //(Math.PI / 180) 为1°

                ////如果校正后的半径距离大于直径，需要使其等于直径-边距，使这个点不参与计算。
                if (MeasureValueTop[i].CalcRadius > f1 - f5)
                    MeasureValueTop[i].CalcRadius = f1 - f5;
            }

        }
        /// <summary>
        /// 三维过滤算法中直径的调用
        /// </summary>
        /// <param name="i"></param>
        private void ReplaceValues1(int i, MeasureValueStructType[] zhijing)
        {
            MeasureValueStructType[] MeasureValue = zhijing;
            //float f1 = fangchang;//直径
            float f1 = Convert.ToSingle(zhijing) * 100;//直径
            //MessageBox.Show("三维方长" + f1);
            float f5 = Convert.ToSingle(Margin) * 100;//
            if (i > 0)
            {
                MeasureValue[i].CalcHeight = MeasureValue[i - 1].CalcHeight;
                MeasureValue[i].MeasureLength = (float)(MeasureValue[i].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
                MeasureValue[i].CalcRadius = (float)(MeasureValue[i].MeasureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°

                ////如果校正后的半径距离大于直径，需要使其等于直径-边距，使这个点不参与计算。
                if (MeasureValue[i].CalcRadius > f1 - f5)
                    MeasureValue[i].CalcRadius = f1 - f5;
            }




        }

        //没用调用过
        private void angle(string filename)
        {
            if (true)
            {
                distance = "";//长度的字符串
                angle_list.Clear();
                int i = 0;
                FileStream aFile = new FileStream(System.Windows.Forms.Application.StartupPath + "\\back_data\\" + filename + ".txt", FileMode.Open);
                StreamReader sr = new StreamReader(aFile);
                string line = "";
                int isfirst = 1;//判断是否是第一行，若不是，需要在前面添加','
                while ((line = sr.ReadLine()) != null)
                {
                    if (isfirst == 0)
                        distance += ",";
                    string[] data = line.Split(' ');
                    distance += data[1];
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
                sr.Close();
                aFile.Close();
            }
        }

        private void analysisData_Paint(object sender, PaintEventArgs e)
        {
            //MessageBox.Show("type" + type);
            if (type.Equals("2"))
            {
                //MessageBox.Show("顶置直径绘图");
                panel1Paint();

                panel2Paint();
            }else if (type.Equals("0") || type.Equals("1")|| type.Equals("4"))
            {
                //MessageBox.Show("侧置直径绘图");
                //write("height_total" + height_total + "top_height" + top_height);

                panel3Paint();
                panel3.BackgroundImage = global::Warehouse.Properties.Resources.top;


                panel4Paint();
                panel4.BackgroundImage = global::Warehouse.Properties.Resources.top;
            }

        }
        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }
        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }


        private void panel5_Paint(object sender, PaintEventArgs e)
        {

        }

        //原始数据点击看大图
        private void button4_Click(object sender, EventArgs e)
        {
            if (type.Equals("2"))
            {
                new Thread(DaTu_show).Start();//开启新的线程来开启新的窗体

            }else if (type.Equals("0"))
            {
                new Thread(DaTu2_show).Start();//开启新的线程来开启新的窗体
            }
            
        }
        private void DaTu_show()
        {
            MethodInvoker meth = new MethodInvoker(show_cqinfo);
            BeginInvoke(meth);
        }

        private void DaTu2_show()
        {
            MethodInvoker meth = new MethodInvoker(show2_cqinfo);
            BeginInvoke(meth);
        }
        private void show_cqinfo()
        {
            dt = new DaTu();
            dt.setX = data_x_max_m;//向下一个窗体传参数
            dt.setY = data_y_max_m;
            dt.setSZ = SZ;
            dt.setDate = OriginalValue1;
            dt.setH = height_total;
            dt.setIsBlue = "blue";
            dt.Show();
        }

        private void show2_cqinfo()
        {
            dt2 = new DaTu2();
            dt2.setX = data_x_max_c;//向下一个窗体传参数
            dt2.setY = data_y_max_c;
            dt2.setDate = OriginalValue;
            dt2.setH = height_total;
            dt2.setIsBlue = "blue";
            dt2.Show();
        }
        //过滤数据点击看大图
        private void button5_Click(object sender, EventArgs e)
        {
            if (type.Equals("2"))
            {
                Dflag = 1;
                new Thread(DaTu_show2).Start();//开启新的线程来开启新的窗体
            }
            else if (type.Equals("0"))
            {
                new Thread(DaTu_show3).Start();//开启新的线程来开启新的窗体
            }
            
        }
        private void DaTu_show2()
        {
            MethodInvoker meth = new MethodInvoker(show_cqinfo2);
            BeginInvoke(meth);
        }
        private void show_cqinfo2()
        {
            dt = new DaTu();
            dt.setX = data_x_max_m;//向下一个窗体传参数
            dt.setY = data_y_max_m;
            dt.setSZ = SZ;
            dt.setCount = final_count;
            dt.setFlag = Dflag;
            dt.setDate = MeasureValueTop;
            dt.setH = height_total;
            dt.setIsBlue = "red";
            dt.Show();
        }

        private void DaTu_show3()
        {
            MethodInvoker meth = new MethodInvoker(show3_cqinfo2);
            BeginInvoke(meth);
        }

        private void show3_cqinfo2()
        {
            dt2 = new DaTu2();
            dt2.setX = data_x_max_c;//向下一个窗体传参数
            dt2.setY = data_y_max_c;
            dt2.setDate = MeansureValue;
            dt2.setH = height_total;
            dt2.setIsBlue = "blue";
            dt2.Show();
        }


        private void Log(string v)
        {
            string info = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"), v);
            string path = "C://Mqtt//MyWareHouseLog.txt";//"C://Mqtt//Point1.txt"
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(info);
            }
        }
        //state 指算法：0是满仓，1是半径算法，2是直径算法
        //num 指点的个数
        //RelativeHeight ：整个料仓绝对的高度
        //MeasureValue ;传入的测量点的数据
        //wareData: 料仓的整体参数
        private float VolumeCalculate(int state, int num, float RelativeHeight, CheckData[] MeasureValue, WarehouseStructType wareData)
        {
            try
            {
                int i, EffectiveAngle;//EffectiveAngle对于半径计算法，就是有效值的位置，对于直径计算法就是中心点的位置
                float Volume1 = 0, Volume2 = 0; //体积1半径计算值，体积2中心点另一边直径计算值
                float CalcPercent;//计算体积的权重比例
                float[] ObjectHeight = new float[180];//仓库实体物料高度
                float[] ObjectRadius = new float[180];//仓库实体物料半径
                for (int j = 0; j < ObjectRadius.Length; j++)
                {
                    ObjectHeight[j] = 0F;
                    ObjectRadius[j] = 0F;
                }

                EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置

                if ((state == 1) || (state == 2))
                {//先计算需要的参数
                 //数据的处理
                    EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置
                    //MessageBox.Show("中心点时==" + EffectiveAngle);
                    for (i = 0; i < num; i++)
                    {
                        //计算实体物料高度
                        if (MeasureValue[i].CalcHeight < RelativeHeight)//比实际高度小
                        {
                            ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;
                        }
                        else//大于实际高度
                        {
                            //MessageBox.Show("点==" + i + "大于实际高度");
                            if (i == 0)
                            {
                                //SetRescanFlag(1);//重盘使能
                            }
                            else
                            {
                                //MessageBox.Show("第" + i + "个点大于实际高度");
                                //ReplaceValues(i);//使用前一个点覆盖
                                //ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//再计算
                                ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//相对高度减去测量后的那个点的垂直长度就是物料距离地面的高度(相对高度 ，柱体高 - 顶高 +下锥高，虚拟化为这个柱体的高度)
                            }
                        }


                        //计算半径代入值
                        if (MeasureValue[i].CalcRadius < wareData.ColumnDiameter - wareData.Margin)//比实际直径小
                        {
                            ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
                        }
                        else//大于实际直径
                        {
                            if (i == 0)
                            {
                                //SetRescanFlag(1);//重盘使能
                            }
                            else
                            {
                                //MessageBox.Show("第" + i + "个点大于实际高度");
                                //MessageBox.Show("点==" + i + "大于实际直径。。。"+MeasureValue[i].CalcRadius);
                                ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
                            }
                        }
                    }
                }

                if (state == 0)//满仓，直接根据第一个点计算
                {
                    //计算柱体体积
                    Volume1 = CalculateV(RelativeHeight - MeasureValue[0].CalcHeight,
                                                                wareData.ColumnRadius,
                                                                RelativeHeight - MeasureValue[0].CalcHeight,
                                                                0);
                    Volume1 = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
                }
                else//使用直径或者半径算法
                {
                    //MessageBox.Show("使用半径直径算法");
                    //使用中心线扫描侧的点计算整个体积
                    //计算最外圈体积
                    Volume1 += CalculateV(ObjectHeight[0],
                                                                wareData.ColumnRadius,
                                                                ObjectHeight[0],
                                                                ObjectRadius[0]);

                    //依次计算内圈体积
                    for (i = 0; i < EffectiveAngle - 1; i++)
                        Volume1 += CalculateV(ObjectHeight[i],
                                                                   ObjectRadius[i],
                                                                   ObjectHeight[i + 1],
                                                                   ObjectRadius[i + 1]);
                    //计算中心柱体体积
                    Volume1 += CalculateV(ObjectHeight[EffectiveAngle - 1],
                                                                ObjectRadius[EffectiveAngle - 1],
                                                                ObjectHeight[EffectiveAngle - 1],
                                                                0);

                    //使用中心线扫描侧的另一边的点计算整个体积		
                    if ((num != EffectiveAngle) && (state == 2))//直径算法才会用到2是直径算法
                    {
                        //MessageBox.Show("使用另一侧的点计算");
                        //计算中心柱体体积
                        Volume2 += CalculateV(ObjectHeight[EffectiveAngle],
                                                                    ObjectRadius[EffectiveAngle],
                                                                    ObjectHeight[EffectiveAngle],
                                                                    0);

                        //依次计算内圈体积
                        for (i = EffectiveAngle; i < num - 1; i++)
                            Volume2 += CalculateV(ObjectHeight[i],
                                                                        ObjectRadius[i],
                                                                        ObjectHeight[i + 1],
                                                                        ObjectRadius[i + 1]);

                        //计算最外圈体积
                        Volume2 += CalculateV(ObjectHeight[num - 1],
                                                                    wareData.ColumnRadius,
                                                                    ObjectHeight[num - 1],
                                                                    ObjectRadius[num - 1]);
                    }

                    if (state == 2)//直径
                    {
                        CalcPercent = ((float)EffectiveAngle) / ((float)num);//计算权重比例，以扫描点的个数为准
                                                                             //分别计算加权后的体积，并减去下锥空余部分的体积，就是结果
                        wareData.Volume = Volume1 * CalcPercent + Volume2 * (1 - CalcPercent) - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
                        //MessageBox.Show("左半边的体积：" + Volume1 + "     权重：" + CalcPercent + "  右半边的体积：" + Volume2    +"   下锥体积："+ PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3);
                    }
                    if (state == 1)//半径
                    {
                        wareData.Volume = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("出错了：" + e.ToString());
            }

            return wareData.Volume;

        }
        private int DataScreen(CheckData[] MeasureValue, WarehouseStructType wareData, int num)//返回扫到中心点时的角度
        {
            int i;
            for (i = 0; i < num; i++)
            {
                if (MeasureValue[i].CalcRadius > wareData.ColumnRadius - wareData.Margin)//当测量的长度计算出来的半径.对于直径算法，会直接返回那个中心点的编号
                    break;
            }
            return i;//如果循环了所以点，都没有大于直径的值，说明这个算法是半径算法，最后一个点就是中心点，所以返回最后一个点的下标
        }
        /**
          * @brief  计算空心圆柱体体积
          * @param  H1 第一个采样点高度
          * @param  R1 第一个采样点半径
          * @param  H2 第二个采样点高度
          * @param  R2 第二个采样点半径
          * @retval 环状体体积
        */
        public float CalculateV(float H1, float R1, float H2, float R2)
        {
            float H;
            H = (H1 + H2) / 2;
            if (R1 > R2)
                return PI * H * (R1 * R1 - R2 * R2);
            else
                return PI * H * (R2 * R2 - R1 * R1);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
        float HighSum;//算Z值时候得总和
        int[] pointArray = new int[1500];
        //int[] pointArray1 = new int[360];


        //跳转三维图像按钮
        private void button6_Click(object sender, EventArgs e)
        {
            //if (type.Equals("侧置平扫") || type.Equals("顶置平扫"))
            if (type.Equals("4") || type.Equals("1"))
            {
                //MessageBox.Show("平扫测量计算");
                ///////////////////////////////////////////////////////////////////////
                //添加柱状图
                //Example8_8_1.Form1 form3D = new Example8_8_1.Form1();
                //form3D.setTime = time;//向下一个窗体传参数
                string info = "";
                string print = "";//回传点的信息
                string infodata = "";//用于排列数据

                string zhijing = "";
                string heigh = "";
                string xiazhui = "";
                string midu = "";
                string margin = "";
                string top = "";
                string zhoujv = "";
                string binstata = "";
                string fbian = "";
                string fkuan = "";
                string fzuo = "";
                int j = 0;//表示点数
                float Height;//用于计算的料仓高度
                float Xiazhui;//用于计算的下锥高度

                string bjj = "";//步进角
                string spj = "";//水平角
                if (type.Equals("1"))
                {
                    try
                    {

                        MySqlConn mscA = new MySqlConn();//新建数据库连接
                        string sql = "select * from bindata where DateTime = '" + time + "'";
                        MySqlDataReader rd = mscA.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            info = rd["BackAll"].ToString().Trim();
                            print = rd["PrintNum"].ToString().Trim();//获取点数
                            binstata = rd["Algorithm"].ToString().Trim();
                            bjj = rd["Jd"].ToString().Trim();
                            midu = rd["MiDu"].ToString().Trim();
                        }
                        rd.Close();
                        mscA.Close();
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("画图查询数据库出错=" + ee.ToString());
                    }


                    try
                    {
                        MySqlConn mscB = new MySqlConn();//新建数据库连接
                        string sql1 = "select * from bininfo where BinName = '" + binName + "'";
                        MySqlDataReader rd = mscB.getDataFromTable(sql1);
                        while (rd.Read())
                        {

                            zhijing = rd["Diameter"].ToString().Trim();
                            heigh = rd["CylinderH"].ToString().Trim();
                            xiazhui = rd["PyramidH"].ToString().Trim();
                            spj = rd["HAngle"].ToString().Trim();
                            margin = rd["Margin"].ToString().Trim();
                            top = rd["BinTop"].ToString().Trim();
                            zhoujv = rd["Wheelbase"].ToString().Trim();
                            //fbian = rd["Fbian"].ToString().Trim();
                            //fkuan = rd["Fkuan"].ToString().Trim();
                            //fzuo = rd["Fzuobian"].ToString().Trim();
                            infodata = binName + "+" + zhijing + "+" + heigh + "+" + xiazhui + "+" + midu + "+" + margin + "+" + top + "+" + zhoujv + "+" + binstata + "+" + print + "+" + bjj + "+" + spj;
                        }
                        rd.Close();
                        mscB.Close();
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("查询料仓信息出错：" + ee.ToString());
                    }

                    Height = Convert.ToSingle(heigh);//从数据库中读料仓高度
                    Xiazhui = Convert.ToSingle(xiazhui);//从数据库中读下锥
                    ColumnRadius = Convert.ToSingle(zhijing) / 2;//从数据库中读半径

                    redius = ColumnRadius * 100;

                    string[] readArray = new string[2];

                    string str = info;
                    //string str= "0+0+1598;5+0+1579;15+0+1499;10+0+1531;20+0+1483;25+0+1481;30+0+1507;35+0+1595;40+0+1810;45+0+2158;0+15+1597;0+30+971;0+45+891;0+60+908;0+75+951;0+-15+1600;0+-30+1613;0+-45+1613;0+-60+1611;0+-75+1603;5+15+1588;5+30+1600;5+45+1612;5+60+1618;5+75+1075;5+-15+1573;5+-30+1573;5+-45+1576;5+-60+1579;5+-75+1586;10+15+1543;10+30+1562;10+45+1586;10+60+1615;10+-15+1525;10+75+1627;10+-30+1530;10+-45+1543;10+-60+1562;10+-75+1584;15+15+1518;15+30+1553;15+45+1591;15+60+1629;15+75+1635;15+-15+1493;15+-30+1507;15+-60+1576;15+-45+1535;15+-75+1624;20+15+1516;20+30+1567;20+45+1622;20+60+1684;20+75+1279;20+-15+1476;20+-30+1502;20+-45+1552;20+-60+1617;20+-75+1689;25+15+1535;25+30+1609;25+45+1688;25+60+1754;25+75+1754;25+-15+1472;25+-30+1522;25+-45+1603;25+-60+1689;25+-75+1671;30+15+1589;30+30+1691;30+45+1784;30+60+1713;30+-15+1494;30+75+1713;30+-30+1588;30+-45+1699;30+-60+1813;30+-75+1417;35+15+1701;35+30+1822;35+45+1924;35+60+1487;35+75+1487;35+-15+1593;35+-30+1712;35+-45+1874;35+-60+1881;40+15+1912;40+30+2037;40+60+1332;40+45+1957;40+75+1332;40+-15+1831;40+-30+1958;40+-45+2110;45+30+2182;45+15+2226;45+45+1779;45+60+1210;45+75+1210;45+-15+2195;45+-30+2275;";
                    //MessageBox.Show("获取到的3D点数据是str = " + str);
                    string[] pint = str.Split(';');
                    int len = pint.Length;
                    int a = len - 1;
                    ConvetPoint b1 = new ConvetPoint();
                    ConvetPoint b2 = new ConvetPoint();
                    HighSum = (Height + Xiazhui) * 100;
                    //MessageBox.Show("*************88" + len);
                    //MessageBox.Show("点个数是str = " + a);
                    //float[] xvalue = new float[len - 1];
                    //float[] yvalue = new float[len - 1];
                    //float[] zvalue = new float[len - 1];
                    //Log("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                    try
                    {

                        for (int i = 0; i < len - 1; i++)
                        {
                            string[] p = pint[i].Split('+');
                            //write(p[0] + "*" + p[1] + "*" + p[2]);
                            b1.x[i] = (AngleOfX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            b1.y[i] = (AngleOfY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            //b1.x[i] = (AngleOfFX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            //b1.y[i] = (AngleOfFY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            b1.z[i] = ((AngleOfZ(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2])))) / 100;
                            //write("垂直角"+p[0]+"水平角"+p[1]+"length:"+Convert.ToSingle(p[2])/100 + "高度"+b1.z[i]);
                            b2.x[i] = (int)(b1.x[i] * 10000);
                            b2.y[i] = (int)(b1.y[i] * 10000);
                            b2.z[i] = (int)(b1.z[i] * 10000);
                            //write("******X值"+b1.x[i]+"*******Y值"+b1.y[i]+"*******Z值"+b1.z[i]);
                            //write("I的值" + i);
                            j++;

                        }

                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show(ee.ToString() + "");
                    }
                    //MessageBox.Show(len + "");
                    //MessageBox.Show(j + "*");

                    try
                    {
                        //定义Matlab矩阵类，Real表示实数，最后俩参数表示列数和行数
                        MWNumericArray arr = new MWNumericArray(MWArrayComplexity.Real, j, 3);

                        int k = 1;//控制矩阵行数
                        for (int i = 0; i < j; i++)
                        {
                            //int k = i / 3 + 1;//控制矩阵行数
                            arr[k, 1] = b2.x[i];
                            arr[k, 2] = b2.y[i];
                            arr[k, 3] = b2.z[i];
                            k++;

                        }

                        write(arr + "");
                        //Class9 ts = new Class9();
                        //ts.text9(arr);
                        //write(arr + "");
                        //Class11 ts = new Class11();
                        //ts.text11(arr);
                        Class12 ts = new Class12();
                        ts.text12(arr);
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show(ee + "");
                    }

                }
                else if (type.Equals("4")){

                    try
                    {

                        MySqlConn mscA = new MySqlConn();//新建数据库连接
                        string sql = "select * from bindata where DateTime = '" + time + "'";
                        MySqlDataReader rd = mscA.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            info = rd["BackAll"].ToString().Trim();
                            print = rd["PrintNum"].ToString().Trim();//获取点数
                            binstata = rd["Algorithm"].ToString().Trim();
                            bjj = rd["Jd"].ToString().Trim();
                            midu = rd["MiDu"].ToString().Trim();
                        }
                        rd.Close();
                        mscA.Close();
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("方仓画图查询数据库出错=" + ee.ToString());
                    }

                    try
                    {
                        MySqlConn mscB = new MySqlConn();//新建数据库连接
                        string sql1 = "select * from bininfo where BinName = '" + binName + "'";
                        MySqlDataReader rd = mscB.getDataFromTable(sql1);
                        while (rd.Read())
                        {

                            zhijing = rd["Diameter"].ToString().Trim();
                            heigh = rd["CylinderH"].ToString().Trim();
                            xiazhui = rd["PyramidH"].ToString().Trim();
                            spj = rd["HAngle"].ToString().Trim();
                            margin = rd["Margin"].ToString().Trim();
                            top = rd["BinTop"].ToString().Trim();
                            zhoujv = rd["Wheelbase"].ToString().Trim();
                            fbian = rd["Fbian"].ToString().Trim();
                            fkuan = rd["Fkuan"].ToString().Trim();
                            fzuo = rd["Fzuobian"].ToString().Trim();
                            infodata = binName + "+" + zhijing + "+" + heigh + "+" + xiazhui + "+" + midu + "+" + margin + "+" + top + "+" + zhoujv + "+" + binstata + "+" + print + "+" + bjj + "+" + spj;
                        }
                        rd.Close();
                        mscB.Close();
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show("方仓查询料仓信息出错：" + ee.ToString());
                    }

                    Height = Convert.ToSingle(heigh);//从数据库中读料仓高度
                    Xiazhui = Convert.ToSingle(xiazhui);//从数据库中读下锥
                    ColumnRadius = Convert.ToSingle(zhijing) / 2;//从数据库中读半径

                    redius = ColumnRadius * 100;

                    fangchang = Convert.ToSingle(fbian);
                    fangkuan = Convert.ToSingle(fkuan);
                    fangzuo = Convert.ToSingle(fzuo);

                    fangchang = fangchang * 100;
                    fangkuan = fangkuan * 100;
                    fangzuo = fangzuo * 100;

                    string[] readArray = new string[2];

                    string str = info;
                    string[] pint = str.Split(';');
                    int len = pint.Length;
                    int a = len - 1;
                    ConvetPoint b1 = new ConvetPoint();
                    ConvetPoint b2 = new ConvetPoint();
                    HighSum = (Height + Xiazhui) * 100;

                    try
                    {

                        for (int i = 0; i < len - 1; i++)
                        {
                            string[] p = pint[i].Split('+');
                            //write(p[0] + "*" + p[1] + "*" + p[2]);
                            //b1.x[i] = (AngleOfX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            //b1.y[i] = (AngleOfY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100 ;
                            b1.x[i] = (AngleOfFX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            b1.y[i] = (AngleOfFY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                            b1.z[i] = ((AngleOfZ(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2])))) / 100;
                            //write("垂直角"+p[0]+"水平角"+p[1]+"length:"+Convert.ToSingle(p[2])/100 + "高度"+b1.z[i]);
                            b2.x[i] = (int)(b1.x[i] * 10000);
                            b2.y[i] = (int)(b1.y[i] * 10000);
                            b2.z[i] = (int)(b1.z[i] * 10000);
                            //write("******X值"+b1.x[i]+"*******Y值"+b1.y[i]+"*******Z值"+b1.z[i]);
                            //write("I的值" + i);
                            j++;

                        }

                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show(ee.ToString() + "");
                    }

                    try
                    {
                        //定义Matlab矩阵类，Real表示实数，最后俩参数表示列数和行数
                        MWNumericArray arr = new MWNumericArray(MWArrayComplexity.Real, j, 3);

                        int k = 1;//控制矩阵行数
                        for (int i = 0; i < j; i++)
                        {
                            //int k = i / 3 + 1;//控制矩阵行数
                            arr[k, 1] = b2.x[i];
                            arr[k, 2] = b2.y[i];
                            arr[k, 3] = b2.z[i];
                            k++;

                        }
  
                        Class12 ts = new Class12();
                        ts.text12(arr);
                    }
                    catch (Exception ee)
                    {
                        MessageBox.Show(ee + "");
                    }

                }
              
            }
            else
            {
                MessageBox.Show("直径测量计算,无法查看三维图像");

            }
        }
        /// <summary>
        /// 角度转换为X坐标
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度(单位：厘米)</param>
        /// <returns></returns>
        public float AngleOfX(float Angle1, float Angle2, float Length)
        {

            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Xcoor;

            if (Angle2 > 0)//角度为正值时
            {
                Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180));
            }
            else
            {//角度为负值
                Angle2 = -Angle2;
                Xcoor = -(float)(L * Math.Sin(Angle2 * Math.PI / 180));
            }
            return Xcoor;


        }
        /// <summary>
        /// 计算方仓X坐标
        /// </summary>
        /// <param name="Angle1"></param>
        /// <param name="Angle2"></param>
        /// <param name="Length"></param>
        /// <returns></returns>
        public float AngleOfFX(float Angle1, float Angle2, float Length)
        {

            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Xcoor;


            if (fangkuan/2 > fangzuo)//先判断左边距与方宽一半的大小
            {
                if (Angle2 > 0)//角度为正值时
                {
                    //Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180)+(fangkuan/2-fangzuo));
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) - (fangzuo - fangkuan / 2));
                }
                else
                {//角度为负值
                    Angle2 = -Angle2;
                    //Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) + (fangkuan / 2 - fangzuo));
                    Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) - (fangzuo - fangkuan / 2));
                }

                return Xcoor;
            }
            else if(fangkuan / 2 < fangzuo)
            {
                if (Angle2 > 0)//角度为正值时
                {
                    // Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) - (fangzuo-fangkuan/2));
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) - (fangzuo - fangkuan / 2));
                }
                else
                {//角度为负值
                    Angle2 = -Angle2;
                    //Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) - (fangzuo - fangkuan / 2));
                    Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) - (fangzuo - fangkuan / 2));
                }
                return Xcoor;
            }
            else
            {
                if (Angle2 > 0)//角度为正值时
                {
                    //Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180));
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) - (fangzuo - fangkuan / 2));
                }
                else
                {//角度为负值
                    Angle2 = -Angle2;
                    //Xcoor = -(float)(L * Math.Sin(Angle2 * Math.PI / 180));
                    Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) - (fangzuo - fangkuan / 2));
                }
                return Xcoor;
            }


        }

        /// <summary>
        /// 角度转换为Y坐标
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度(单位：厘米)</param>
        /// <returns></returns>
        public float AngleOfY(float Angle1, float Angle2, float Length)
        {

            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Ycoor = 0.0f;

            if (Angle2 > 0)//右边的点
            {
                if ((L * (Math.Cos(Angle2 * Math.PI / 180))) > redius)
                {
                    Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - redius;
                }
                else
                {
                    Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - redius;
                }
            }
            else//左边的点
            {
                Angle2 = 0 - Angle2;
                if ((L * (Math.Cos(Angle2 * Math.PI / 180))) > redius)
                {
                    Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - redius;
                }
                else
                {
                    Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - redius;
                }
            }
            float a = (float)(L * (Math.Cos(Angle2 * Math.PI / 180)));
            float c = (float)(Math.Cos(Angle2 * Math.PI / 180));
            string b = "" + a;
            string d = "" + c;
            //Log(" !!! =  Angle1垂直角度 = " + Angle1 + ".Angle水平角度 =  " + Angle2 + ". z 测量长度= " + Length + ".计算L= "+L+ " .(Math.Cos(Angle2)) = "+d+"..初始y坐标" + b  +  "..Ycoor = " + Ycoor);
            return Ycoor;
        }


        /// <summary>
        /// 计算方仓Y坐标
        /// </summary>
        /// <param name="Angle1"></param>
        /// <param name="Angle2"></param>
        /// <param name="Length"></param>
        /// <returns></returns>
        public float AngleOfFY(float Angle1, float Angle2, float Length)
        {

            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Ycoor = 0.0f;

            if (Angle2 > 0)//右边的点
            {
                
               Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - fangchang/2;
                    
            }
            else//左边的点
            {
                Angle2 = 0 - Angle2;
                Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - fangchang/2;

            }
            float a = (float)(L * (Math.Cos(Angle2 * Math.PI / 180)));
            float c = (float)(Math.Cos(Angle2 * Math.PI / 180));
            string b = "" + a;
            string d = "" + c;
            //Log(" !!! =  Angle1垂直角度 = " + Angle1 + ".Angle水平角度 =  " + Angle2 + ". z 测量长度= " + Length + ".计算L= "+L+ " .(Math.Cos(Angle2)) = "+d+"..初始y坐标" + b  +  "..Ycoor = " + Ycoor);
            return Ycoor;
        }

        /// <summary>
        /// 角度转化为Z值
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度(单位：厘米)</param>
        /// <returns></returns>
        public float AngleOfZ(float Angle1, float Angle2, float Length)
        {
            float A1 = Angle1;
            float A2 = Angle2;
            float L = Length;
            float Zcoor;

            Zcoor = (float)(HighSum- L * Math.Cos(A1 * Math.PI / 180));
            //write(HighSum + "HighSum++++++++++++");
            //Zcoor = (float)(L * Math.Cos(A1 * Math.PI / 180));


            return Zcoor;

        }/// <summary>
        /// 通过clockwise计算坐标值
        /// </summary>
        /// <param name="Angle1"></param>
        /// <param name="Angle2"></param>
        /// <param name="Length"></param>
        /// <param name="clockwise"></param>
        /// <returns></returns>
        public float AngleOfX1(float Angle1, float Angle2, float Length, int clockwise)
        {
            float beta = Angle2;
            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Xcoor;
            if (clockwise == 0)
                beta = -beta;
            if (beta > 0)//角度为正值时
            {
                Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180));
            }
            else
            {//角度为负值

                Xcoor = -(float)(L * Math.Sin(Angle2 * Math.PI / 180));
            }
            return Xcoor;


        }

        public float AngleOfFX1(float Angle1, float Angle2, float Length, int clockwise)
        {
            float beta = Angle2;
            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Xcoor;
            if (clockwise == 0)//左边的点
                beta = -beta;
            if (fangkuan / 2 > fangzuo)//先判断左边距与方宽一半的大小
            {
                if (beta > 0)//角度为正值时
                {
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) + (fangkuan / 2 - fangzuo));
                }
                else
                {//角度为负值
                    Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) + (fangkuan / 2 - fangzuo));
                }

                return Xcoor;
            }
            else if (fangkuan / 2 < fangzuo)
            {
                if (beta > 0)//角度为正值时
                {
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180) - (fangzuo - fangkuan / 2));
                }
                else
                {//角度为负值
                    Xcoor = (-(float)(L * Math.Sin(Angle2 * Math.PI / 180)) - (fangzuo - fangkuan / 2));
                }
                return Xcoor;
            }
            else
            {
                if (beta > 0)//角度为正值时
                {
                    Xcoor = (float)(L * Math.Sin(Angle2 * Math.PI / 180));
                }
                else
                {//角度为负值
                    Xcoor = -(float)(L * Math.Sin(Angle2 * Math.PI / 180));
                }
                return Xcoor;
            }


        }

        public float StepAngle ;//步进角
        public int planestepangle;//水平角

        public struct MeasureValueStructType
        {
            internal float MeasureLength; //直径上点的测量距离
            internal float CalcHeight;     //测量高度
            internal float CalcRadius;     //测量半径
            internal int angle;
        }

        public struct Planepoint
        {
            internal UInt32 alpha;//平面扫描时的垂直角度
            internal UInt32 beta;//平面扫描时的水平角度
            internal float length;//测量距离
            internal UInt32 clockwise;//旋转方向，1为顺时针，0为逆时针
            internal UInt32 valid;//是否为有效数据，1为有效，可以用来计算体积，0为无效，相当于去除这个点
        }

       
        

        public float ColumnDiameter;//料仓直径
        public float ColumnRadius;  //料仓半径
        public float ColumnHeight;  //料仓高度
        public float TopHeight;//设备顶距
        public float VertebralHeight;//下锥高度
        public float Margin1 ;//设备边距

        int zjnum = 0;//直径点个数


        //过滤以后的三维图像
        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                if (type.Equals("4") || type.Equals("1"))
                {

                    string info = "";
                    string print = "";//回传点的信息
                    string infodata = "";//用于排列数据

                    string zhijing = "";
                    string heigh = "";
                    string xiazhui = "";
                    string midu = "";
                    string margin = "";
                    string top = "";
                    string zhoujv = "";
                    string binstata = "";
                    string fbian = "";
                    string fkuan = "";
                    string fzuo = "";
                    string id = "";
                    int j = 0;

                    string bjj = "";//步进角
                    string spj = "";//水平角
                    zjnum = 0;

                    if (type.Equals("1")){
                        try
                        {
                            MySqlConn mscA = new MySqlConn();//新建数据库连接
                            string sql = "select * from bindata where DateTime = '" + time + "'";
                            MySqlDataReader rd = mscA.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                info = rd["BackAll"].ToString().Trim();
                                print = rd["PrintNum"].ToString().Trim();//获取点数
                                binstata = rd["Algorithm"].ToString().Trim();
                                bjj = rd["Jd"].ToString().Trim();
                                midu = rd["MiDu"].ToString().Trim();

                            }
                            rd.Close();
                            mscA.Close();
                        }
                        catch (Exception ee)
                        {
                            MessageBox.Show("画图查询数据库出错=" + ee.ToString());
                        }
 ;

                    }else if (type.Equals("4")){

                        try
                        {
                            MySqlConn mscA = new MySqlConn();//新建数据库连接
                            string sql = "select * from bindata where DateTime = '" + time + "'";
                            MySqlDataReader rd = mscA.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                info = rd["BackAll"].ToString().Trim();
                                print = rd["PrintNum"].ToString().Trim();//获取点数
                                binstata = rd["Algorithm"].ToString().Trim();
                                bjj = rd["Jd"].ToString().Trim();
                                midu = rd["MiDu"].ToString().Trim();

                            }
                            rd.Close();
                            mscA.Close();
                        }
                        catch (Exception ee)
                        {
                            MessageBox.Show("画图查询数据库出错=" + ee.ToString());
                        }
     
                        try
                        {
                            MySqlConn mscB = new MySqlConn();//新建数据库连接
                            string sql1 = "select * from bininfo where BinName = '" + binName + "'";
                            MySqlDataReader rd = mscB.getDataFromTable(sql1);
                            while (rd.Read())
                            {
                                id = rd["BinID"].ToString().Trim();
                                zhijing = rd["Diameter"].ToString().Trim();
                                heigh = rd["CylinderH"].ToString().Trim();
                                xiazhui = rd["PyramidH"].ToString().Trim();
                                margin = rd["Margin"].ToString().Trim();
                                spj = rd["HAngle"].ToString().Trim();
                                top = rd["BinTop"].ToString().Trim();
                                zhoujv = rd["Wheelbase"].ToString().Trim();
                                fbian = rd["Fbian"].ToString().Trim();
                                fkuan = rd["Fkuan"].ToString().Trim();
                                fzuo = rd["Fzuobian"].ToString().Trim();
                                infodata = binName + "+" + zhijing + "+" + heigh + "+" + xiazhui + "+" + midu + "+" + margin + "+" + top + "+" + zhoujv + "+" + binstata + "+" + print + "+" + bjj + "+" + spj;
                            }
                            rd.Close();
                            mscB.Close();
                        }
                        catch (Exception ee)
                        {
                            MessageBox.Show("查询料仓信息出错：" + ee.ToString());
                        }

                        fangchang= Convert.ToSingle(fbian);
                        fangkuan= Convert.ToSingle(fkuan);
                        fangzuo= Convert.ToSingle(fzuo);


                        fangchang = fangchang * 100;//单位厘米
                        fangkuan = fangkuan * 100;
                        fangzuo = fangzuo * 100;



                    }






    
                    ColumnDiameter = Convert.ToSingle(zhijing);//从数据库中读直径

                    ColumnRadius = Convert.ToSingle(zhijing) / 2;//从数据库中读半径

                    ColumnHeight = Convert.ToSingle(heigh);//从数据库中读料仓高度

                    TopHeight = Convert.ToSingle(top);//从数据库中读顶距

                    //TopHeight = 0.65f;
                    VertebralHeight = Convert.ToSingle(xiazhui);//从数据库中读下锥

                    Margin1 = Convert.ToSingle(margin);//从数据库中读边距


                    StepAngle = Convert.ToSingle(bjj);//从数据库中读步进角


                    planestepangle = Convert.ToInt32(spj);//从数据库中读水平角




                    redius = ColumnRadius*100;//将数据库中读到半径赋值



         
                    MessageBox.Show("读取数据库");


                    MeasureValueStructType[] MeasureValue = new MeasureValueStructType[100];   //保存直径上的测量点
                    Planepoint[,] planepoints = new Planepoint[V_POINT_NUM, H_POINT_NUM];     //保存非直径上的测量点


                    String str1 = info;
                    //String str1 = "0+0+1598;5+0+1579;15+0+1499;10+0+1531;20+0+1483;25+0+1481;30+0+1507;35+0+1595;40+0+1810;45+0+2158;0+15+1597;0+30+971;0+45+891;0+60+908;0+75+951;0+-15+1600;0+-30+1613;0+-45+1613;0+-60+1611;0+-75+1603;5+15+1588;5+30+1600;5+45+1612;5+60+1618;5+75+1075;5+-15+1573;5+-30+1573;5+-45+1576;5+-60+1579;5+-75+1586;10+15+1543;10+30+1562;10+45+1586;10+60+1615;10+-15+1525;10+75+1627;10+-30+1530;10+-45+1543;10+-60+1562;10+-75+1584;15+15+1518;15+30+1553;15+45+1591;15+60+1629;15+75+1635;15+-15+1493;15+-30+1507;15+-60+1576;15+-45+1535;15+-75+1624;20+15+1516;20+30+1567;20+45+1622;20+60+1684;20+75+1279;20+-15+1476;20+-30+1502;20+-45+1552;20+-60+1617;20+-75+1689;25+15+1535;25+30+1609;25+45+1688;25+60+1754;25+75+1754;25+-15+1472;25+-30+1522;25+-45+1603;25+-60+1689;25+-75+1671;30+15+1589;30+30+1691;30+45+1784;30+60+1713;30+-15+1494;30+75+1713;30+-30+1588;30+-45+1699;30+-60+1813;30+-75+1417;35+15+1701;35+30+1822;35+45+1924;35+60+1487;35+75+1487;35+-15+1593;35+-30+1712;35+-45+1874;35+-60+1881;40+15+1912;40+30+2037;40+60+1332;40+45+1957;40+75+1332;40+-15+1831;40+-30+1958;40+-45+2110;45+30+2182;45+15+2226;45+45+1779;45+60+1210;45+75+1210;45+-15+2195;45+-30+2275;";
                    String[] pint = str1.Split(';');
                    //PointRead[] point = new PointRead[pint.Length];
                    int dindex = 0;
                    int mainindex = 0;
                    int pindex = 0;
                    int tempindex = 0;
                    HighSum = (ColumnHeight + VertebralHeight- TopHeight) *100;

                    //循环将planepoints中的值初始化
                    for (int i = 0; i < V_POINT_NUM; i++)
                    {
                        for (int p = 0; p < H_POINT_NUM; p++)
                        {
                            planepoints[i, p].alpha = 0;
                            planepoints[i, p].beta = 0;
                            planepoints[i, p].clockwise = 0;
                            planepoints[i, p].length = 0;
                            planepoints[i, p].valid = 0;
                        }
                    }
                    for (int i = 0; i < pint.Length - 1; i++)
                    {

                        String[] str2 = new String[3];//str2[0]=垂直角度 str2[1]=水平角度 str2[2]=Length
                        str2 = pint[i].Split('+');
                        //MessageBox.Show(str2[0] + "*"+str2[1] + "*" +str2[2]);
                        //str[1]为零时，即水平角为零，表示为直径上的点
                        if (str2[1] == "0")
                        {
                            dindex = int.Parse(str2[0]) / (int)StepAngle;
                            MeasureValue[dindex].MeasureLength = float.Parse(str2[2]) ;//单位厘米
                            //write(dindex + "---------->" + MeasureValue[dindex].MeasureLength);
                            zjnum++;

                        }
                        else
                        {
                            mainindex = int.Parse(str2[0]) / (int)StepAngle;
                            //右边的点,角度为正的，顺时针clockwise为1
                            if (float.Parse(str2[1]) > 0)
                            {
                                pindex = int.Parse(str2[1]) / planestepangle - 1;
                                planepoints[mainindex, pindex].alpha = (UInt32)int.Parse(str2[0]);
                                planepoints[mainindex, pindex].clockwise = 1;
                                planepoints[mainindex, pindex].beta = (UInt32)int.Parse(str2[1]);
                                planepoints[mainindex, pindex].length = float.Parse(str2[2]) ;//单位厘米
                                write(planepoints[mainindex, pindex].alpha+"垂直角"+ planepoints[mainindex, pindex].beta +"水平角"+ planepoints[mainindex, pindex].length+"存入legth的值" + "clockwise"+1);
                                planepoints[mainindex, pindex].valid = 1;

                            }


                        }
                    }

                    for (mainindex = 0; mainindex <= zjnum ; mainindex++)
                        for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                        {
                            if (planepoints[mainindex, pindex].valid == 0)
                            {
                                planepoints[mainindex, pindex].alpha = 255;//???????
                                break;
                            }
                        }


                    for (int i = 0; i < pint.Length - 1; i++)
                    {
                        String[] str2 = new String[3];//str[0]=水平角度 str[1]=垂直角度 str[2]=Length
                        str2 = pint[i].Split('+');
                        //左边的点,角度为负，逆时针clockwise为0
                        if (float.Parse(str2[1]) < 0)
                        {
                            mainindex = int.Parse(str2[0]) / (int)StepAngle;
                            for (int tempindex1 = 0; tempindex1 < 20; tempindex1++)
                            {
                                if (planepoints[mainindex, tempindex1].valid == 0)
                                {
                                    planepoints[mainindex, tempindex1].alpha = (UInt32)int.Parse(str2[0]);
                                    planepoints[mainindex, tempindex1].clockwise = 0;
                                    planepoints[mainindex, tempindex1].beta = (UInt32)(-int.Parse(str2[1]));
                                    planepoints[mainindex, tempindex1].length = float.Parse(str2[2]);//单位厘米
                                    write(planepoints[mainindex, tempindex1].alpha + "垂直角" + planepoints[mainindex, tempindex1].beta + "水平角" + planepoints[mainindex, tempindex1].length + "存入legth的值" + "clockwise" + 0);
                                    planepoints[mainindex, tempindex1].valid = 1;
                                    break;

                                }
                            }


                        }
                    }

                    for (mainindex = 0; mainindex <= zjnum ; mainindex++)
                        for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                        {
                            if (planepoints[mainindex, pindex].valid == 0)
                            {
                                planepoints[mainindex, pindex].alpha = 255;
                                break;
                            }
                        }



                    //直径上的点过滤
                    DataCheck1(zjnum, MeasureValue);



                    //两侧的点过滤，调用两次checkplanepoints进行过滤
                    if (type.Equals("1"))
                    {
                        MessageBox.Show("平面扫描");
                        checkplanepoints(planepoints, MeasureValue);

                        checkplanepoints(planepoints, MeasureValue);

                        ConvetPoint a3 = new ConvetPoint();///控制直径点传入矩阵的值
                        ConvetPoint a4 = new ConvetPoint();//存储计算以后的直径点
                        for (int i = 0; i <= zjnum; i++)
                        {
                            //if (MeasureValue[i].MeasureLength == 0)
                            //{
                            //    break;
                            //}
                            //write(i * 5 + "*" + 0 + "*" + MeasureValue[i].MeasureLength);
                            //write("****垂直角"+i*StepAngle + "******水平角"+0+"******Length"+MeasureValue[i].MeasureLength+"****高度"+a4.z[i]);
                            a4.x[i] = (AngleOfX(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;
                            a4.y[i] = (AngleOfY(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;//为了使数据在中间
                                                                                                        //a4.x[i] = (AngleOfFX(i * StepAngle, 0, MeasureValue[i].MeasureLength )) / 100;//算坐标后单位为米
                                                                                                        //a4.y[i] = (AngleOfFY(i * StepAngle, 0, MeasureValue[i].MeasureLength )) / 100;
                            a4.z[i] = ((AngleOfZ(i * StepAngle, 0, MeasureValue[i].MeasureLength))) / 100;
                            //write(MeansureValue[i].MeansureLength + "*"+a4.x[i]+"*"+a4.y[i]+"*"+a4.z[i]+"*");
                            a3.x[i] = (int)(a4.x[i] * 10000);
                            a3.y[i] = (int)(a4.y[i] * 10000);
                            a3.z[i] = (int)(a4.z[i] * 10000);

                            //write(a4.x[i] + " " + a4.y[i] + " " + a4.z[i]);
                        }
                        ///write("zjcnum5********************" + zjcnum5);


                        ConvetPoint a1 = new ConvetPoint();//控制计算完以后的值
                        ConvetPoint a2 = new ConvetPoint();//控制传入矩阵的值
                        int c = 0;//获取非直径上点的个数
                        int d = 0;//控制计算以后下标
                        for (int i = 0; i < 30; i++)
                        {
                            for (int k = 0; k < 20; k++)
                            {
                                //255表示无效值
                               
                                if (planepoints[i, k].alpha == 255)
                                {
                                    break;
                                }
                                if (planepoints[i, k].valid == 1)
                                {
                                    //write(planepoints[i, k].alpha + "#垂直角" + planepoints[i, k].beta + "#水平角" + planepoints[i, k].length+ "#Length" + planepoints[i,k].clockwise);
                                    a1.x[d] = (AngleOfX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length, (int)planepoints[i, k].clockwise)) / 100;
                                    a1.y[d] = (AngleOfY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length)) / 100;//为了使数据在中间
                                                                                                                                          //a1.x[d] = (AngleOfFX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length , (int)planepoints[i, k].clockwise)) / 100;//计算坐标后单位为米
                                                                                                                                          //a1.y[d] = (AngleOfFY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length )) / 100;//为了使数据在中间
                                    a1.z[d] = ((AngleOfZ(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length))) / 100;
                                    a2.x[c] = (int)(a1.x[d] * 10000);//数据扩大10000倍，便于加入矩阵
                                    a2.y[c] = (int)(a1.y[d] * 10000);
                                    a2.z[c] = (int)(a1.z[d] * 10000);
                                    //write();
                                    //write(a1.x[c] + " " + a1.y[c] + " " + a1.z[c]);
                                    c++;
                                    d++;
                                }
                            }
                        }
                        //MessageBox.Show(c + "非直径上的点");

                        //for(int i = 0; i < a; i++)
                        //{
                        //    write(a3.z[i]+"");
                        //}
                        //write("***********************************************");
                        //write("直径上点的值zjnum：" + zjnum);
                        try
                        {
                            //定义Matlab矩阵类，Real表示实数，最后俩参数表示列数和行数
                            MWNumericArray arr = new MWNumericArray(MWArrayComplexity.Real, c + zjnum - 1, 3);
                            int k = 1;//控制矩阵行
                            for (int i = 0; i < c; i++)
                            {
                                //循环加入非直径上的点
                                arr[k, 1] = a2.x[i];
                                arr[k, 2] = a2.y[i];
                                arr[k, 3] = a2.z[i];
                                //write(arr[k, 1] + " "+arr[k, 2] + " " + arr[k, 3]);
                                //write("高度值：" + a2.z[i] / 10000);
                                k++;

                            }
                            //write(c + "1");
                            for (int i = 0; i < zjnum - 1; i++)
                            {

                                //循环加入直径上的点
                                //if ((a3.x[i] == 0 && a3.y[i] == -92000 && a3.z[i] == 167100)|| (a3.x[i] == 0 && a3.y[i] == 0 && a3.z[i] == 0))
                                //{
                                //    break;
                                //}
                                arr[k, 1] = a3.x[i];
                                arr[k, 2] = a3.y[i];
                                arr[k, 3] = a3.z[i];
                                //write("高度值：" + a3.z[i] / 10000);
                                //write(arr[k, 1] + " " + arr[k, 2] + " " + arr[k, 3]);
                                k++;
                            }

                            //write(arr + "");
                            //Class9 ts = new Class9();
                            //ts.text9(arr);


                            Class12 ts = new Class12();
                            ts.text12(arr);
                        }
                        catch (Exception ee)
                        {
                            MessageBox.Show(ee + "");
                        }



                    }
                    else if (type.Equals("4"))
                    {
        

                        checkplanepoints1(planepoints, MeasureValue);

                        checkplanepoints1(planepoints, MeasureValue);

                        ConvetPoint a3 = new ConvetPoint();///控制直径点传入矩阵的值
                        ConvetPoint a4 = new ConvetPoint();//存储计算以后的直径点
                        for (int i = 0; i <= zjnum; i++)
                        {
                            //if (MeasureValue[i].MeasureLength == 0)
                            //{
                            //    break;
                            //}
                            //write(i * 5 + "*" + 0 + "*" + MeasureValue[i].MeasureLength);
                            //write("****垂直角"+i*StepAngle + "******水平角"+0+"******Length"+MeasureValue[i].MeasureLength+"****高度"+a4.z[i]);
                            //a4.x[i] = (AngleOfX(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;
                            //a4.y[i] = (AngleOfY(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;//为了使数据在中间
                            a4.x[i] = (AngleOfFX(i * StepAngle, 0, MeasureValue[i].MeasureLength )) / 100;//算坐标后单位为米
                            a4.y[i] = (AngleOfFY(i * StepAngle, 0, MeasureValue[i].MeasureLength )) / 100;
                            a4.z[i] = ((AngleOfZ(i * StepAngle, 0, MeasureValue[i].MeasureLength))) / 100;
                            //write(MeansureValue[i].MeansureLength + "*"+a4.x[i]+"*"+a4.y[i]+"*"+a4.z[i]+"*");
                            a3.x[i] = (int)(a4.x[i] * 10000);
                            a3.y[i] = (int)(a4.y[i] * 10000);
                            a3.z[i] = (int)(a4.z[i] * 10000);

                            //write(a4.x[i] + " " + a4.y[i] + " " + a4.z[i]);
                        }
                        ///write("zjcnum5********************" + zjcnum5);


                        ConvetPoint a1 = new ConvetPoint();//控制计算完以后的值
                        ConvetPoint a2 = new ConvetPoint();//控制传入矩阵的值
                        int c = 0;//获取非直径上点的个数
                        int d = 0;//控制计算以后下标
                        for (int i = 0; i < 30; i++)
                        {
                            for (int k = 0; k < 20; k++)
                            {
                                write("planepoints[i, k].valid" + planepoints[i, k].valid + "+++++垂直角" + planepoints[i, k].alpha + "+++++++水平角" + planepoints[i, k].beta + "++++++length" + planepoints[i, k].length + "+++++++++高度" + a1.z[d] + "++++++clockwise" + planepoints[i, k].clockwise);
                                //255表示无效值
                                if (planepoints[i, k].alpha == 255)
                                {
                                    break;
                                }
                                if (planepoints[i, k].valid == 1)
                                {
                                    //write(planepoints[i, k].alpha + "#垂直角" + planepoints[i, k].beta + "#水平角" + planepoints[i, k].length+ "#Length" + planepoints[i,k].clockwise);
                                    //a1.x[d] = (AngleOfX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length, (int)planepoints[i, k].clockwise)) / 100;
                                    //a1.y[d] = (AngleOfY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length)) / 100;//为了使数据在中间
                                    a1.x[d] = (AngleOfFX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length , (int)planepoints[i, k].clockwise)) / 100;//计算坐标后单位为米
                                    a1.y[d] = (AngleOfFY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length )) / 100;//为了使数据在中间
                                    a1.z[d] = ((AngleOfZ(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length))) / 100;
                                    a2.x[c] = (int)(a1.x[d] * 10000);//数据扩大10000倍，便于加入矩阵
                                    a2.y[c] = (int)(a1.y[d] * 10000);
                                    a2.z[c] = (int)(a1.z[d] * 10000);
                                    //write("+++++垂直角" + planepoints[i, k].alpha + "+++++++水平角" + planepoints[i, k].beta + "++++++length" + planepoints[i, k].length + "+++++++++高度" + a1.z[d] + "++++++clockwise" + planepoints[i, k].clockwise);
                                    //write(a1.x[c] + " " + a1.y[c] + " " + a1.z[c]);
                                    c++;
                                    d++;
                                }
                            }
                        }
                        //MessageBox.Show(c + "非直径上的点");

                        //for(int i = 0; i < a; i++)
                        //{
                        //    write(a3.z[i]+"");
                        //}
                        //write("***********************************************");
                        //write("直径上点的值zjnum：" + zjnum);
                        try
                        {
                            //定义Matlab矩阵类，Real表示实数，最后俩参数表示列数和行数
                            MWNumericArray arr = new MWNumericArray(MWArrayComplexity.Real, c + zjnum - 1, 3);
                            int k = 1;//控制矩阵行
                            for (int i = 0; i < c; i++)
                            {
                                //循环加入非直径上的点
                                arr[k, 1] = a2.x[i];
                                arr[k, 2] = a2.y[i];
                                arr[k, 3] = a2.z[i];
                                //write(arr[k, 1] + " "+arr[k, 2] + " " + arr[k, 3]);
                                //write("高度值：" + a2.z[i] / 10000);
                                k++;

                            }
                            //write(c + "1");
                            for (int i = 0; i < zjnum - 1; i++)
                            {

                                //循环加入直径上的点
                                //if ((a3.x[i] == 0 && a3.y[i] == -92000 && a3.z[i] == 167100)|| (a3.x[i] == 0 && a3.y[i] == 0 && a3.z[i] == 0))
                                //{
                                //    break;
                                //}
                                arr[k, 1] = a3.x[i];
                                arr[k, 2] = a3.y[i];
                                arr[k, 3] = a3.z[i];
                                //write("高度值：" + a3.z[i] / 10000);
                                //write(arr[k, 1] + " " + arr[k, 2] + " " + arr[k, 3]);
                                k++;
                            }

                            //write(arr + "");
                            //Class9 ts = new Class9();
                            //ts.text9(arr);


                            Class12 ts = new Class12();
                            ts.text12(arr);
                        }
                        catch (Exception ee)
                        {
                            MessageBox.Show(ee + "");
                        }
                    }

                }
                else
                {
                    MessageBox.Show("直径测量计算,无法查看三维图像");

                }
            }catch(Exception ee)
            {
                //this.Close();
                MessageBox.Show(ee.ToString() + "");
            }
        }

        void checkplanepoints(Planepoint[,] point, MeasureValueStructType[] mm)
        {  //add to programme
            //try
            //{
                int count = 0;
                int mainindex, pindex;
                float height_sum = 0, height_average;
                float height_diff_sum = 0;
                float height_diff, height_diff_average;
                float temp_height, temp_radius, heighta, heightb, heightc, heightd, heightsum, nheight, temp_height_diff, nheightdiff_sum, nheightdiff_average;
                float temp_ndiff;
                float ndiff_sum = 0, ndiff_average;
                int prev_index, cur_index, ndiff_count = 0, point_count;
                int heightcount;
                float Height = ColumnHeight - TopHeight + VertebralHeight;
                Planepoint[,] planepoints = point;
                MeasureValueStructType[] MeasureValue = mm;



                for (mainindex = 0; mainindex <= zjnum ; mainindex++)
                {
                    //compute average height
                    height_sum = 0;
                    height_diff_sum = 0;
                    count = 0;
                    ndiff_sum = 0;
                    prev_index = -1;
                    cur_index = -1;
                    ndiff_count = 0;
                    //首先计算平面上某次平面扫描的平均高度
                    for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                    {
                        if (planepoints[mainindex, pindex].alpha != 255)
                        {
                            if (planepoints[mainindex, pindex].valid == 1)
                            {
                                height_sum += computeheight(mainindex, pindex, planepoints);
                                //height_diff_sum += Math.Abs(computeheight(mainindex, pindex) - height_average);
                                cur_index = pindex;
                                if (prev_index >= 0)
                                {
                                    if (planepoints[mainindex, prev_index].clockwise == planepoints[mainindex, cur_index].clockwise)
                                    {
                                        ndiff_sum += Math.Abs(computeheight(mainindex, cur_index, planepoints) - computeheight(mainindex, prev_index, planepoints));
                                        ndiff_count++;
                                    }
                                }
                                prev_index = cur_index;
                                count++;
                            }


                        }
                        else
                            break;
                    }
                    height_average = height_sum / count;
                    ndiff_average = ndiff_sum / ndiff_count;
                    count = 0;
                    for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                    {
                        if (planepoints[mainindex, pindex].alpha != 255)
                        {
                            if (planepoints[mainindex, pindex].valid == 1)
                            {
                                height_diff_sum += Math.Abs(computeheight(mainindex, pindex, planepoints) - height_average);
                                count++;
                            }
                        }else
                            break;
                    }
                    //求得平均高度
                    //height_average = height_sum / count;
                    height_diff_average = height_diff_sum / count;
                    //count = 0;
                    //printf("mainindex:%d,height average:%5.2f\n", mainindex, height_average);
                    prev_index = -1;
                    cur_index = -1;
                    point_count = 0;
                    //然后对这次扫描的每一个点，判断是否需要进行数据处理
                    for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                    {
                        if (planepoints[mainindex, pindex].alpha != 255)
                        {
                      
                            if (planepoints[mainindex, pindex].valid == 1)
                            {
                                //MessageBox.Show("mainindex:" + mainindex + "!!!!!!!!!!!pindex" + pindex);
                                point_count++;
                                temp_height = computeheight(mainindex, pindex, planepoints);
                                temp_radius = computeradius(mainindex, pindex, planepoints);
                                temp_height_diff = temp_height - height_average;
                                //printf("mainindex:%d,pindex:%d,height:%5.2f\n", mainindex, pindex, temp_height);
                                if (point_count == 1)
                                {
                                    prev_index = pindex;
                                }
                                if (temp_radius > redius) //如果此点的半径超出实际半径，则标记为无效
                                {
                                    //MessageBox.Show("mainindex:" + mainindex + "pindex"+ pindex);
                                    planepoints[mainindex, pindex].valid = 0;
                                    // if (planepoints[mainindex, pindex].clockwise == 1)
                                    //Console.WriteLine(" planepoints bigger than radius alpha:{0,2} beta:{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                    // write(" planepoints bigger than radius alpha:{0,2} beta:{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                    // else
                                    //Console.WriteLine(" planepoints bigger than radius alpha:{0,2} beta:-{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                    // write(" planepoints bigger than radius alpha:{0,2} beta:-{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                    //printf("point planepoints[%d][%d] is invalid\n", mainindex, pindex);
                                    continue;
                                }

                                cur_index = pindex;
                                // if (temp_height_diff >= 2 * height_diff_average)
                                //if (temp_height >= 2 * height_average)
                                //{//如果该点的高度大于2倍的平均高度，则处理
                                if (temp_radius >= (1 - RADIUS_BUFFER) * redius && temp_radius <= redius)
                                {
                                    if (point_count > 1)
                                    {
                                        //write("!!!!!!mainindex:" + mainindex + "!!!!!!!!!!!prev_index ==" + prev_index + "cur_index ==" + cur_index+ "point_count"+ point_count);
                                        if (planepoints[mainindex, prev_index].clockwise == planepoints[mainindex, cur_index].clockwise)
                                        {
                                            temp_ndiff = temp_height - computeheight(mainindex, prev_index, planepoints);
                                            //write("垂直角：" + planepoints[mainindex, prev_index].alpha + "水平角" + planepoints[mainindex, prev_index].beta + "legth" + planepoints[mainindex, prev_index].length+ "clockwise:" + planepoints[mainindex, cur_index].clockwise);

                                            if (Math.Abs(temp_ndiff) > DIFF_RATIO * ndiff_average)
                                            {
                                                planepoints[mainindex, pindex].length = (Height - computeheight(mainindex, prev_index, planepoints)) / (float)Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180);
                                            }
                                            else if (Math.Abs(temp_height_diff) > DIFF_RATIO * height_diff_average)
                                            {
                                                planepoints[mainindex, pindex].length = (Height - height_average) / (float)Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180);
                                            }
                                        }
                                        //如果该点半径位于边缘缓冲区，可能为打到仓壁上的点，标记为无效
                                        //planepoints[mainindex, pindex].valid = 0;
                                        //if (planepoints[mainindex, pindex].clockwise == 1)
                                        //Console.WriteLine(" planepoints bigger than average alpha:{0,2} beta:{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                        //write(" planepoints bigger than average alpha:{0,2} beta:{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                        // else
                                        //Console.WriteLine(" planepoints bigger than average alpha:{0,2} beta:-{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                        //write(" planepoints bigger than average alpha:{0,2} beta:-{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                        //printf("point planepoints[%d][%d] is invalid\n", mainindex, pindex);
                                    }

                                }
                                else
                                {
                                    //如果不是大于平均高度的2倍，则根据周边四个点的高度值进行判断并处理
                                    //nheight = neighborheight(mainindex,pindex);
                                    heighta = leftheight(mainindex, pindex, planepoints, MeasureValue);
                                    heightb = rightheight(mainindex, pindex, planepoints, MeasureValue);
                                    heightc = topheight(mainindex, pindex, planepoints, MeasureValue);
                                    heightd = bottomheight(mainindex, pindex, planepoints, MeasureValue);
                                    heightcount = 0;
                                    heightsum = 0;
                                   // heightsum += temp_height;
                                   // heightcount++;
                                    if (heighta != 0)
                                    {
                                        heightsum += heighta;
                                        heightcount++;
                                    }
                                    if (heightb != 0)
                                    {
                                        heightsum += heightb;
                                        heightcount++;
                                    }
                                    if (heightc != 0)
                                    {
                                        heightsum += heightc;
                                        heightcount++;
                                    }
                                    if (heightd != 0)
                                    {
                                        heightsum += heightd;
                                        heightcount++;
                                    }
                                    if (heightcount > 0)
                                    {
                                        nheight = heightsum / heightcount;
                                        nheightdiff_sum = 0;
                                       // nheightdiff_sum += Math.Abs(temp_height - nheight);
                                        if (heighta != 0)
                                        {
                                            nheightdiff_sum += Math.Abs(heighta - nheight);

                                        }
                                        if (heightb != 0)
                                        {
                                            nheightdiff_sum += Math.Abs(heightb - nheight);
                                        }
                                        if (heightc != 0)
                                        {
                                            nheightdiff_sum += Math.Abs(heightc - nheight);
                                        }
                                        if (heightd != 0)
                                        {
                                            nheightdiff_sum += Math.Abs(heightd - nheight);
                                        }
                                        nheightdiff_average = nheightdiff_sum / heightcount;
                                        //temp_height_diff = Math.Abs(temp_height - nheightdiff_average);
                                        temp_height_diff = Math.Abs(temp_height - nheight);
                                    if (temp_height_diff > NEIGHBOR_RATIO * nheightdiff_average)
                                        {
                                            //if (temp_height > 2 * heighta && temp_height > 2 * heightb && temp_height > 2 * heightc && temp_height > 2 * heightd)
                                            //{
                                            planepoints[mainindex, pindex].length = (Height - nheight) / (float)(Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180));
                                            //printf("smoothe2 mainindex:%d,pindex:%d,height:%5.2f\n", mainindex, pindex, Height - planepoints[mainindex][pindex].length * cos(planepoints[mainindex][pindex].alpha * PI / 180));
                                            // }

                                        }

                                        // printf("nheight:%5.2f\n",nheight);
                                        //  if(isneedchange(mainindex,pindex)){
                                        //   planepoints[mainindex][pindex].length = (Height-nheight)/cos(planepoints[mainindex][pindex].alpha*PI/180);
                                        //    printf("smoothe2 mainindex:%d,pindex:%d,height:%5.2f\n",mainindex,pindex,Height-planepoints[mainindex][pindex].length*cos(planepoints[mainindex][pindex].alpha*PI/180));
                                        // }

                                    }
                                }
                                prev_index = cur_index;
                            }

                        }
                        else
                            break;
                    }

                }
        }


        void checkplanepoints1(Planepoint[,] point, MeasureValueStructType[] mm)
        {  //add to programme
           //try
           //{
            int count = 0;
            int mainindex, pindex;
            float height_sum = 0, height_average;
            float height_diff_sum = 0;
            float height_diff, height_diff_average;
            float temp_height, temp_radius, heighta, heightb, heightc, heightd, heightsum, nheight, temp_height_diff, nheightdiff_sum, nheightdiff_average;
            float temp_x, temp_y, temp_clockwise,temp1_x,temp1_y, temp1_clockwise;
            float temp_ndiff;
            float temp_angle;
            float ndiff_sum = 0, ndiff_average;
            int prev_index, cur_index, ndiff_count = 0, point_count;
            int heightcount;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            Planepoint[,] planepoints = point;
            MeasureValueStructType[] MeasureValue = mm;



            for (mainindex = 0; mainindex <= zjnum; mainindex++)
            {
                //compute average height
                height_sum = 0;
                height_diff_sum = 0;
                count = 0;
                ndiff_sum = 0;
                prev_index = -1;
                cur_index = -1;
                ndiff_count = 0;
                //首先计算平面上某次平面扫描的平均高度
                for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                {
                    if (planepoints[mainindex, pindex].alpha != 255)
                    {
                        if (planepoints[mainindex, pindex].valid == 1)
                        {
                            height_sum += computeheight(mainindex, pindex, planepoints);
                            //height_diff_sum += Math.Abs(computeheight(mainindex, pindex) - height_average);
                            cur_index = pindex;
                            if (prev_index >= 0)
                            {
                                if (planepoints[mainindex, prev_index].clockwise == planepoints[mainindex, cur_index].clockwise)
                                {
                                    ndiff_sum += Math.Abs(computeheight(mainindex, cur_index, planepoints) - computeheight(mainindex, prev_index, planepoints));
                                    ndiff_count++;
                                }
                            }
                            prev_index = cur_index;
                            count++;
                        }


                    }
                    else
                        break;
                }
                height_average = height_sum / count;
                ndiff_average = ndiff_sum / ndiff_count;
                count = 0;
                for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                {
                    if (planepoints[mainindex, pindex].alpha != 255)
                    {
                        if (planepoints[mainindex, pindex].valid == 1)
                        {
                            height_diff_sum += Math.Abs(computeheight(mainindex, pindex, planepoints) - height_average);
                            count++;
                        }
                    }
                    else
                        break;
                }
                //求得平均高度
                //height_average = height_sum / count;
                height_diff_average = height_diff_sum / count;
                //count = 0;
                //printf("mainindex:%d,height average:%5.2f\n", mainindex, height_average);
                prev_index = -1;
                cur_index = -1;
                point_count = 0;
                //然后对这次扫描的每一个点，判断是否需要进行数据处理
                for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                {
                    if (planepoints[mainindex, pindex].alpha != 255)
                    {

                        if (planepoints[mainindex, pindex].valid == 1)
                        {
                            //MessageBox.Show("mainindex:" + mainindex + "!!!!!!!!!!!pindex" + pindex);
                            point_count++;
                            temp_height = computeheight(mainindex, pindex, planepoints);
                            //temp_radius = computeradius(mainindex, pindex, planepoints);
                            temp_x = computex(mainindex, pindex, planepoints);
                            temp_y = computey(mainindex, pindex, planepoints);
                            //MessageBox.Show("temp_x" + temp_x + "fangchang" + fangchang + "fangkuan" + fangkuan);
                            temp_clockwise = planepoints[mainindex,pindex].clockwise;
                            if (pindex - 1 > 0)
                            {
                                temp1_clockwise = planepoints[mainindex, pindex-1].clockwise;
                                if(temp1_clockwise == temp_clockwise)
                                {
                                    temp1_x = computex(mainindex, pindex-1, planepoints);
                                    temp1_y = computey(mainindex, pindex-1, planepoints);
                                    temp_angle =(float)( Math.Atan(Math.Abs(temp1_y - temp_y) / Math.Abs(temp1_x - temp_x)) * 180 / Math.PI);
                                    if(temp_angle > 70)
                                    {
                                       
                                        planepoints[mainindex, pindex].valid = 0;
                                        continue;
                                    }


                                }


                            }
                            temp_height_diff = temp_height - height_average;
                            //printf("mainindex:%d,pindex:%d,height:%5.2f\n", mainindex, pindex, temp_height);
                            if (point_count == 1)
                            {
                                prev_index = pindex;
                            }
                            //if (temp_radius > ColumnRadius) //如果此点的半径超出实际半径，则标记为无效
                            if ((temp_clockwise == 1 && temp_x >= 0.9*(fangkuan - fangzuo)) || (temp_clockwise == 0 && temp_x >= 0.9*fangzuo) || temp_y >= 0.9*fangchang) // 3?3?·?2?·??§
                            {
                                

                                planepoints[mainindex, pindex].valid = 0;

                                continue;
                            }

                            cur_index = pindex;
                            // if (temp_height_diff >= 2 * height_diff_average)
                            //if (temp_height >= 2 * height_average)
                            //{//如果该点的高度大于2倍的平均高度，则处理
                            //if (temp_radius >= (1 - RADIUS_BUFFER) * ColumnRadius && temp_radius <= ColumnRadius)
                            if ((temp_clockwise == 1 && temp_x >= (1 - RADIUS_BUFFER) * (fangkuan - fangzuo) && temp_x <= (fangkuan - fangzuo)) || (temp_clockwise == 0 && temp_x >= (1 - RADIUS_BUFFER) * fangzuo && temp_x <= fangzuo))
                            {
                                
                                if (point_count > 1)
                                    {
                                        //write("!!!!!!mainindex:" + mainindex + "!!!!!!!!!!!prev_index ==" + prev_index + "cur_index ==" + cur_index+ "point_count"+ point_count);
                                        if (planepoints[mainindex, prev_index].clockwise == planepoints[mainindex, cur_index].clockwise)
                                        {
                                            temp_ndiff = temp_height - computeheight(mainindex, prev_index, planepoints);
                                            //write("垂直角：" + planepoints[mainindex, prev_index].alpha + "水平角" + planepoints[mainindex, prev_index].beta + "legth" + planepoints[mainindex, prev_index].length+ "clockwise:" + planepoints[mainindex, cur_index].clockwise);

                                            if (Math.Abs(temp_ndiff) > DIFF_RATIO * ndiff_average)
                                            {
                                                planepoints[mainindex, pindex].length = (Height - computeheight(mainindex, prev_index, planepoints)) / (float)Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180);
                                            }
                                            else if (Math.Abs(temp_height_diff) > DIFF_RATIO * height_diff_average)
                                            {
                                                planepoints[mainindex, pindex].length = (Height - height_average) / (float)Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180);
                                            }
                                        }
                                        //如果该点半径位于边缘缓冲区，可能为打到仓壁上的点，标记为无效
                                        //planepoints[mainindex, pindex].valid = 0;
                                        //if (planepoints[mainindex, pindex].clockwise == 1)
                                        //Console.WriteLine(" planepoints bigger than average alpha:{0,2} beta:{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                        //write(" planepoints bigger than average alpha:{0,2} beta:{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                        // else
                                        //Console.WriteLine(" planepoints bigger than average alpha:{0,2} beta:-{1,2} is invalid", planepoints[mainindex, pindex].alpha, planepoints[mainindex, pindex].beta);
                                        //write(" planepoints bigger than average alpha:{0,2} beta:-{1,2} is invalid" + planepoints[mainindex, pindex].alpha + "*"+planepoints[mainindex, pindex].beta);
                                        //printf("point planepoints[%d][%d] is invalid\n", mainindex, pindex);
                                    }

                                
                            }
                            else
                            {
                                //如果不是大于平均高度的2倍，则根据周边四个点的高度值进行判断并处理
                                //nheight = neighborheight(mainindex,pindex);
                                heighta = leftheight(mainindex, pindex, planepoints, MeasureValue);
                                heightb = rightheight(mainindex, pindex, planepoints, MeasureValue);
                                //heightb = 1;
                                heightc = topheight(mainindex, pindex, planepoints, MeasureValue);
                                heightd = bottomheight(mainindex, pindex, planepoints, MeasureValue);
                                heightcount = 0;
                                heightsum = 0;
                                // heightsum += temp_height;
                                // heightcount++;
                                if (heighta != 0)
                                {
                                    heightsum += heighta;
                                    heightcount++;
                                }
                                if (heightb != 0)
                                {
                                    heightsum += heightb;
                                    heightcount++;
                                }
                                if (heightc != 0)
                                {
                                    heightsum += heightc;
                                    heightcount++;
                                }
                                if (heightd != 0)
                                {
                                    heightsum += heightd;
                                    heightcount++;
                                }
                                if (heightcount > 0)
                                {
                                    nheight = heightsum / heightcount;
                                    nheightdiff_sum = 0;
                                    // nheightdiff_sum += Math.Abs(temp_height - nheight);
                                    if (heighta != 0)
                                    {
                                        nheightdiff_sum += Math.Abs(heighta - nheight);

                                    }
                                    if (heightb != 0)
                                    {
                                        nheightdiff_sum += Math.Abs(heightb - nheight);
                                    }
                                    if (heightc != 0)
                                    {
                                        nheightdiff_sum += Math.Abs(heightc - nheight);
                                    }
                                    if (heightd != 0)
                                    {
                                        nheightdiff_sum += Math.Abs(heightd - nheight);
                                    }
                                    nheightdiff_average = nheightdiff_sum / heightcount;
                                    //temp_height_diff = Math.Abs(temp_height - nheightdiff_average);
                                    temp_height_diff = Math.Abs(temp_height - nheight);
                                    if (temp_height_diff > NEIGHBOR_RATIO * nheightdiff_average)
                                    {
                                        //if (temp_height > 2 * heighta && temp_height > 2 * heightb && temp_height > 2 * heightc && temp_height > 2 * heightd)
                                        //{
                                        planepoints[mainindex, pindex].length = (Height - nheight) / (float)(Math.Cos(planepoints[mainindex, pindex].alpha * PI / 180));
                                        //printf("smoothe2 mainindex:%d,pindex:%d,height:%5.2f\n", mainindex, pindex, Height - planepoints[mainindex][pindex].length * cos(planepoints[mainindex][pindex].alpha * PI / 180));
                                        // }

                                    }

                                    // printf("nheight:%5.2f\n",nheight);
                                    //  if(isneedchange(mainindex,pindex)){
                                    //   planepoints[mainindex][pindex].length = (Height-nheight)/cos(planepoints[mainindex][pindex].alpha*PI/180);
                                    //    printf("smoothe2 mainindex:%d,pindex:%d,height:%5.2f\n",mainindex,pindex,Height-planepoints[mainindex][pindex].length*cos(planepoints[mainindex][pindex].alpha*PI/180));
                                    // }

                                }
                            }
                                prev_index = cur_index;
                            }

                        }
                        else
                            break;
                }

             }

        }



        float computeradius(int mainindex, int pindex, Planepoint[,] ss)
        {//计算平面某点的半径
            float l, lx, ly, radius;
            Planepoint[,] planepoints = ss;
            l = planepoints[mainindex, pindex].length * (float)(Math.Sin(planepoints[mainindex, pindex].alpha * Math.PI / 180));
            lx = l * (float)(Math.Sin(planepoints[mainindex, pindex].beta * Math.PI / 180));
            ly = l * (float)(Math.Cos(planepoints[mainindex, pindex].beta * Math.PI / 180));
            ly = Math.Abs(ColumnRadius - Margin1 - ly);
            radius = (float)Math.Sqrt(lx * lx + ly * ly);
            return radius;
        }

        float computeheight(int mainindex, int pindex, Planepoint[,] ss)
        {//计算平面上某点的真正高度
            Planepoint[,] planepoints = ss;
            float height;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            height = planepoints[mainindex, pindex].length * (float)(Math.Cos(planepoints[mainindex, pindex].alpha * Math.PI / 180));
            return Height - height;

        }

        float leftheight(int mainindex, int pindex,Planepoint[,] ss, MeasureValueStructType[] mm)
        {//计算平面上某点的左边点的高度，没有，则返回0
            Planepoint[,] planepoints = ss;
            MeasureValueStructType[] MeasureValue = mm;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            UInt32 clockwise;
            clockwise = planepoints[mainindex, pindex].clockwise;
            if (clockwise == 1)//clockwise points
            {
                if (Math.Abs(planepoints[mainindex, pindex].beta) == planestepangle)//the most left of clockwise plane points
                {
                    ///printf("left point:alhpa %4.0f\n", StepAngle * mainindex);
                    return Height - MeasureValue[mainindex].MeasureLength * (float)(Math.Cos(planepoints[mainindex, pindex].alpha * Math.PI / 180));
                }
                else
                {
                    if (planepoints[mainindex, pindex - 1].valid == 1)
                    {
                        //printf("left point:mainindex: %d,pindex: %d\n", mainindex, pindex - 1);
                        return computeheight(mainindex, pindex - 1, planepoints);

                    }
                    else
                        return 0;
                }
            }
            else
            { //anti-clockwise
                if (Math.Abs(planepoints[mainindex, pindex].beta) + planestepangle > 80)//is last point
                    return 0;
                else
                {
                    if (planepoints[mainindex, pindex + 1].valid == 1)
                    {
                        //printf("left point:mainindex: %d,pindex: %d\n", mainindex, pindex + 1);
                        return computeheight(mainindex, pindex + 1, planepoints);

                    }
                    else
                        return 0;

                }


            }

        }

        float rightheight(int mainindex, int pindex, Planepoint[,] ss, MeasureValueStructType[] mm)
        {//计算平面上某点的右边点的高度，没有，则返回0
            Planepoint[,] planepoints = ss;
            MeasureValueStructType[] MeasureValue = mm;
            UInt32 clockwise;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            //MessageBox.Show("##############"+mainindex+"*"+pindex);
            clockwise = planepoints[mainindex+1, pindex+1].clockwise;
            if (clockwise == 0)//anti-clockwise points
            {
                if (Math.Abs(planepoints[mainindex, pindex].beta) == planestepangle)//the most right of anticlockwise plane points
                {
                    //printf("right point:alhpa %4.0f\n", StepAngle * mainindex);
                    return Height - MeasureValue[mainindex].MeasureLength * (float)(Math.Cos(planepoints[mainindex, pindex].alpha * Math.PI / 180));
                }
                else
                {
                    if (planepoints[mainindex, pindex - 1].valid == 1)
                    {
                        //printf("right point:mainindex: %d,pindex: %d\n", mainindex, pindex - 1);
                        return computeheight(mainindex, pindex - 1, planepoints);

                    }
                    else
                        return 0;
                }
            }
            else
            { //clockwise
                if (Math.Abs(planepoints[mainindex, pindex].beta) + planestepangle > 80)//is last point
                    return 0;
                else
                {
                    if (planepoints[mainindex, pindex + 1].valid == 1)
                    {
                        //printf("right point:mainindex: %d,pindex: %d\n", mainindex, pindex + 1);
                        return computeheight(mainindex, pindex + 1, planepoints);

                    }
                    else
                        return 0;

                }


            }

        }

        float topheight(int mainindex, int pindex,Planepoint[,] ss, MeasureValueStructType[] mm)
        {//计算平面上某点的上边点的高度，没有，则返回0
            Planepoint[,] planepoints = ss;
            MeasureValueStructType[] MeasureValue = mm;
            UInt32 clockwise,beta;
            int index;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            clockwise = planepoints[mainindex, pindex].clockwise;
            beta = planepoints[mainindex,pindex].beta;
            index = 0;
            if (mainindex + 1 <= zjnum)
            {
                while (planepoints[mainindex + 1,index].alpha != 255)
                {
                    if (planepoints[mainindex + 1,index].beta == beta && planepoints[mainindex + 1,index].clockwise == clockwise && planepoints[mainindex + 1,index].valid == 1)
                        return computeheight(mainindex + 1, index, planepoints);
                    index++;


                }
                beta = (UInt32)(beta - planestepangle);
                if (beta == 0)
                {
                    return Height - MeasureValue[mainindex + 1].MeasureLength * (float)(Math.Cos((mainindex + 1) * StepAngle * Math.PI / 180));
                }
                index = 0;
                while (planepoints[mainindex + 1,index].alpha != 255)
                {
                    if (planepoints[mainindex + 1,index].beta == beta && planepoints[mainindex + 1,index].clockwise == clockwise && planepoints[mainindex + 1,index].valid == 1)
                        return computeheight(mainindex + 1, index, planepoints);
                    index++;


                }
                beta = planepoints[mainindex,pindex].beta;
                beta = (UInt32)(beta + planestepangle);
                if (beta > MAX_ANGLE)
                {
                    return 0;
                }
                index = 0;
                while (planepoints[mainindex + 1,index].alpha != 255)
                {
                    if (planepoints[mainindex + 1,index].beta == beta && planepoints[mainindex + 1,index].clockwise == clockwise && planepoints[mainindex + 1,index].valid == 1)
                        return  computeheight(mainindex + 1, index, planepoints);
                    index++;


                }
                return 0;
            }
            else
                return 0;

            //if (mainindex + 1 <= zjnum - 1)
            //{
            //    if (Math.Abs(planepoints[mainindex + 1, pindex].beta) == planestepangle)
            //    {
            //        if (planepoints[mainindex + 1, pindex].valid == 1)
            //        {

               //         return computeheight(mainindex + 1, pindex, planepoints);
            //        }
            //        else
            //        {

            //            return Height - MeasureValue[mainindex + 1].MeasureLength * (float)(Math.Cos((mainindex + 1) * StepAngle * Math.PI / 180));
            //        }

            //    }
            //    else
            //    {

            //        //write("*************mainindex" +mainindex+"++++++++++pindex"+pindex);
            //        if (planepoints[mainindex + 1, pindex].valid == 1)
            //        {
            //            //write("里面*************" + (mainindex + 1) + (pindex - 1) + "++++++++++");
            //            return computeheight(mainindex + 1, pindex, planepoints);
 
            //        }
            //        else if (planepoints[mainindex + 1, pindex - 1].valid == 1)
            //        {

            //            return computeheight(mainindex + 1, pindex - 1, planepoints);
            //        }
            //        else
            //        {
            //            if (Math.Abs(planepoints[mainindex + 1, pindex].beta) + planestepangle > 80)
            //                return 0;
            //            else
            //            {
            //                if (planepoints[mainindex + 1, pindex + 1].valid == 1)
            //                {

            //                    return computeheight(mainindex + 1, pindex + 1, planepoints);
            //                }
            //                else
            //                    return 0;

            //            }

            //        }

            //    }
            //}
            //else
            //    return 0;

        }

        float bottomheight(int mainindex, int pindex, Planepoint[,] ss, MeasureValueStructType[] mm)
        {//计算平面上某点的下边点的高度，没有，则返回0
            Planepoint[,] planepoints = ss;
            MeasureValueStructType[] MeasureValue = mm;
            UInt32 clockwise, beta;
            int index;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            clockwise = planepoints[mainindex, pindex].clockwise;
            beta = planepoints[mainindex, pindex].beta;
            index = 0;
            if (mainindex - 1 >= 0)
            {
                while (planepoints[mainindex - 1, index].alpha != 255)
                {
                    if (planepoints[mainindex - 1, index].beta == beta && planepoints[mainindex - 1, index].clockwise == clockwise && planepoints[mainindex - 1, index].valid == 1)
                        return computeheight(mainindex - 1, index, planepoints);
                    index++;


                }
                beta = (UInt32)(beta - planestepangle);
                if (beta == 0)
                {
                    return Height - MeasureValue[mainindex - 1].MeasureLength * (float)(Math.Cos((mainindex - 1) * StepAngle * Math.PI / 180));
                }
                index = 0;
                while (planepoints[mainindex - 1, index].alpha != 255)
                {
                    if (planepoints[mainindex - 1, index].beta == beta && planepoints[mainindex - 1, index].clockwise == clockwise && planepoints[mainindex - 1, index].valid == 1)
                        return computeheight(mainindex - 1, index, planepoints);
                    index++;


                }
                beta = planepoints[mainindex, pindex].beta;
                beta = (UInt32)(beta + planestepangle);
                if (beta > MAX_ANGLE)
                {
                    return 0;
                }
                index = 0;
                while (planepoints[mainindex - 1, index].alpha != 255)
                {
                    if (planepoints[mainindex - 1, index].beta == beta && planepoints[mainindex - 1, index].clockwise == clockwise && planepoints[mainindex - 1, index].valid == 1)
                        return computeheight(mainindex - 1, index, planepoints);
                    index++;


                }
                return 0;
            }
            else
                return 0;
            //if (mainindex - 1 >= 0)
            //{
            //    if (Math.Abs(planepoints[mainindex - 1, pindex].beta) == planestepangle)
            //    {
            //        if (planepoints[mainindex - 1, pindex].valid == 1)
            //        {
            //            //printf("bottom point:mainindex: %d,pindex: %d\n", mainindex - 1, pindex);
            //            return computeheight(mainindex - 1, pindex, planepoints);
            //        }
            //        else
            //        {
            //            //printf("bottom point:mainindex: %d\n", mainindex - 1);
             //           return Height - MeasureValue[mainindex - 1].MeasureLength * (float)(Math.Cos((mainindex - 1) * StepAngle * Math.PI / 180));
            //        }
            //    }
            //    else
            //    {
            //        if (planepoints[mainindex - 1, pindex].valid == 1)
            //        {
            //            //printf("bottom point:mainindex: %d,pindex: %d\n", mainindex - 1, pindex);
            //            return computeheight(mainindex - 1, pindex, planepoints);
            //        }
            //        else if (planepoints[mainindex - 1, pindex - 1].valid == 1)
            //        {
            //            //printf("bottom point:mainindex: %d,pindex: %d\n", mainindex - 1, pindex - 1);
            //            return computeheight(mainindex - 1, pindex - 1, planepoints);
            //        }
            //        else
            //        {
            //            if (Math.Abs(planepoints[mainindex - 1, pindex].beta) + planestepangle > 80)
            //                return 0;
            //            else
            //            {
            //                if (planepoints[mainindex - 1, pindex + 1].valid == 1)
            //                {
            //                    //printf("bottom point:mainindex: %d,pindex: %d\n", mainindex - 1, pindex + 1);
            //                    return computeheight(mainindex - 1, pindex + 1, planepoints);
            //                }
            //                else
            //                    return 0;

            //            }

            //        }
            //    }
            //}
            //else
            //    return 0;
        }

        float computex(int mainindex, int pindex, Planepoint[,] ss)
        {
            Planepoint[,] planepoints = ss;
            float l, lx, ly, radius;
            l = planepoints[mainindex,pindex].length * (float)(Math.Sin(planepoints[mainindex,pindex].alpha * PI / 180));
            //MessageBox.Show("planepoints[mainindex,pindex].length" + planepoints[mainindex, pindex].length);
            lx = l * (float)(Math.Sin(planepoints[mainindex,pindex].beta * PI / 180));
            return lx;

        }
        float computey(int mainindex, int pindex, Planepoint[,] ss)
        {
            Planepoint[,] planepoints = ss;
            float l, lx, ly, radius;
            l = planepoints[mainindex,pindex].length * (float)(Math.Sin(planepoints[mainindex,pindex].alpha * PI / 180));
            lx = l * (float)(Math.Sin(planepoints[mainindex,pindex].beta * PI / 180));
            ly = l * (float)(Math.Cos(planepoints[mainindex,pindex].beta * PI / 180));
            ly += Margin1;
            return ly;


        }

        /// <summary>
        /// 字符串转16进制字节数组
        /// 十六进制转成十进制
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] strToToHexByte(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }

        private string StringToBinary(string str)
        {
            byte[] data = Encoding.Unicode.GetBytes(str);
            StringBuilder sb = new StringBuilder(data.Length * 8);
            foreach (byte item in data)
            {
                sb.Append(Convert.ToString(item, 2).PadLeft(8, '0'));
            }
            return sb.ToString();
        }

        public static void write(string html)
        {
            FileStream fileStream = new FileStream( "D:\\dayin6.txt" , FileMode.Append);
            StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.Default);
            streamWriter.Write(html + "\r\n");
            streamWriter.Flush();
            streamWriter.Close();
            fileStream.Close();
        }
        //public void FenArray(string str)
        //{
        //    int j = 0;
        //    String str1 = str;
        //    string[] pint = str1.Split(';');
        //    point= new PointRead[pint.Length];
        //    for (int i = 0; i < pint.Length - 1; i++)
        //    {
        //        if (pint[i] != null)
        //        {
        //            try
        //            {
        //                string[] str2 = new string[3];
        //                str2 = pint[i].Split('+');
        //                if(str2[1] == "0")
        //                {

        //                    MeasureValue[j].MeasureLength = Convert.ToSingle(str2[2])/100;
        //                    j++;
        //                }
        //                else
        //                {

        //                }
        //            }
        //            catch (Exception ee)
        //            {
        //                MessageBox.Show(ee.ToString() + "");
        //            }

        //        }
        //        else
        //        {
        //            MessageBox.Show("该对象为空");
        //        }

        //    }

        //} 


        //    private unsafe int Readdata(char* input)
        //    {
        //        char* ptr;
        //        char* ptr1;
        //        int dindex = 0, pindex = 0, mainindex = 0;
        //        int alpha=0, beta=0, length=0;
        //        int count = 0;
        //        ptr = input;
        //        ptr1 = input;
        //        while (true)
        //        {
        //            while (*ptr1 != ';' && *ptr1 != '\n')
        //            {
        //                ptr1++;

        //            }
        //            if (*ptr1 == ';' || *ptr1 == '\n')
        //            {
        //                //sscanf(ptr, "%d+%d+%d", &alpha, &beta, &length);
        //                string str = new string(ptr);
        //                string[] pint = str.Split(';');

        //                //alpha = Convert.ToInt32(pint1[0]);
        //                //beta = Convert.ToInt32(pint1[1]);
        //                //length = Convert.ToInt32(pint[2]);
        //                //printf("alpha:%d,beta:%d,length:%d\n", alpha, beta, length);
        //                if (beta == 0)
        //                {
        //                    dindex = alpha / StepAngle;
        //                    MeasureValue[dindex].MeasureLength = (float)length / 100;
        //                    //MeasureValue[dindex].CalcHeight = (float)length / 100 * Cos(alpha * PI / 180);
        //                    MeasureValue[dindex].CalcHeight = (float)length / 100 * (float)Math.Cos(alpha * Math.PI / 180);
        //                    MeasureValue[dindex].CalcRadius = (float)length / 100 * (float)Math.Sin(alpha * Math.PI / 180);

        //                }
        //                else
        //                {
        //                    mainindex = alpha / StepAngle;
        //                    if (beta > 0)
        //                    {

        //                        pindex = beta / planestepangle - 1;
        //                        //planepoints[mainindex][pindex].alpha = (byte)alpha;
        //                        planepoints[mainindex, pindex].alpha = (byte)alpha;

        //                        //planepoints[mainindex][pindex].clockwise = 1;
        //                        planepoints[mainindex, pindex].clockwise = 1;

        //                        //planepoints[mainindex][pindex].beta = (byte)beta;
        //                        //planepoints[mainindex][pindex].length = (float)length / 100;
        //                        //planepoints[mainindex][pindex].valid = 1;

        //                        planepoints[mainindex, pindex].beta = (byte)beta;
        //                        planepoints[mainindex, pindex].length = (float)length / 100;
        //                        planepoints[mainindex, pindex].valid = 1;
        //                    }

        //                }
        //                ptr = ++ptr1;
        //                count++;
        //            }
        //            if (*ptr == '\n')
        //                break;
        //        }

        //        for (mainindex = 0; mainindex <= TotalScanAngle / StepAngle; mainindex++)
        //            for (pindex = 0; pindex < 20; pindex++)
        //            {
        //                //if (planepoints[mainindex][pindex].valid == 0)
        //                if (planepoints[mainindex, pindex].valid == 0)
        //                {
        //                    //planepoints[mainindex][pindex].alpha = 255;
        //                    planepoints[mainindex, pindex].alpha = 255;
        //                    break;
        //                }
        //            }
        //        return count;

        //    }

        //    private unsafe int Readdata2(char* input)
        //    {
        //        char* ptr;
        //        char* ptr1;
        //        int dindex = 0, pindex = 0, mainindex = 0, tempindex = 0;
        //        int alpha=0, beta=0, length=0;
        //        int count = 0;
        //        ptr = input;
        //        ptr1 = input;
        //        while (true)
        //        {
        //            while (*ptr1 != ';' && *ptr1 != '\n')
        //            {
        //                ptr1++;

        //            }
        //            if (*ptr1 == ';' || *ptr1 == '\n')
        //            {
        //                //sscanf(ptr, "%d+%d+%d", &alpha, &beta, &length);
        //                //printf("alpha:%d,beta:%d,length:%d\n",alpha,beta,length);
        //                if (beta < 0)
        //                {
        //                    mainindex = alpha / StepAngle;
        //                    for (tempindex = 0; tempindex < 20; tempindex++)
        //                    {
        //                        //if (planepoints[mainindex][tempindex].valid == 0)
        //                        if (planepoints[mainindex, tempindex].valid == 0)
        //                        {
        //                            //planepoints[mainindex][tempindex].alpha = (byte)alpha;
        //                            //planepoints[mainindex][tempindex].clockwise = 0;
        //                            //planepoints[mainindex][tempindex].beta = (byte)-beta;
        //                            //planepoints[mainindex][tempindex].length = (float)length / 100;
        //                            //planepoints[mainindex][tempindex].valid = 1;

        //                            planepoints[mainindex, tempindex].alpha = (byte)alpha;

        //                            planepoints[mainindex, tempindex].clockwise = 0;

        //                            planepoints[mainindex, tempindex].beta = (byte)-beta;
        //                            planepoints[mainindex, tempindex].length = (float)length / 100;
        //                            planepoints[mainindex, tempindex].valid = 1;
        //                            break;

        //                        }
        //                    }
        //                }
        //                ptr = ++ptr1;
        //                count++;
        //            }
        //            if (*ptr == '\n')
        //                break;
        //        }
        //        for (mainindex = 0; mainindex <= TotalScanAngle / StepAngle; mainindex++)
        //            for (pindex = 0; pindex < 20; pindex++)
        //            {
        //                //if (planepoints[mainindex][pindex].valid == 0)
        //                if (planepoints[mainindex, pindex].valid == 0)
        //                {
        //                    //planepoints[mainindex][pindex].alpha = 255;
        //                    planepoints[mainindex, pindex].alpha = 255;
        //                    break;
        //                }
        //            }
        //        return count;
        //    }

        private void panel2_Paint_1(object sender, PaintEventArgs e)
        {

        }
        //}

        //void Log(string str)    // 记录服务启动  
        //{
        //    file_mutex.WaitOne();
        //    {
        //        string info = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"), str);
        //        string path = "C://Mqtt//Myshow.txt";//"C://Mqtt//PointData.txt"
        //        using (StreamWriter sw = File.AppendText(path))
        //        {
        //            sw.WriteLine(info);
        //            //关闭
        //            sw.Close();
        //        }
        //    }
        //    file_mutex.ReleaseMutex();

        //}


    }
}










//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Data;
//using System.Drawing;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace Warehouse
//{
//    public partial class analysisData : Form
//    {

//        public string time;
//        public string binName;
//        public string binVol;
//        public string binState;
//        float PI = 3.1415926F;
//        public string setTime
//        {
//            get
//            {
//                return this.time;
//            }
//            set
//            {
//                this.time = value;
//            }
//        }
//        public string setName
//        {
//            get
//            {
//                return this.binName;
//            }
//            set
//            {
//                this.binName = value;
//            }
//        }
//        public string setVol
//        {
//            get
//            {
//                return this.binVol;
//            }
//            set
//            {
//                this.binVol = value;
//            }
//        }
//        public string setState
//        {
//            get
//            {
//                return this.binState;
//            }
//            set
//            {
//                this.binState = value;
//            }
//        }
//        private List<int> angle_list = new List<int>();
//        private string distance = "";

//        static double CHECK_PERCENT_VALUE = 0.07;    //数据检测过滤前后点阈值
//        public CheckData[] MeansureValue;//用于计算过滤
//        public CheckData[] OriginalValue;//原始值
//        float diameter;
//        float height_total = 0;
//        float top_height = 0.3F;
//        float stepangle = 0.3F;//步进角

//        float data_x_max = 0;
//        float data_y_max = 0;
//        float x_init = 0;
//        string backData = "";
//        string backAn = "";//回传的角度

//        string zhijing ;
//        string heigh ;
//        string xiazhui;
//        string midu;
//        string Margin;
//        string Top;
//        string Wheelbas;
//        string jaiodu;
//        string paint = ""; //扫描的点数

//        string k = "";
//        string b = "";

//        DaTu dt ;
//        public analysisData()
//        {

//            InitializeComponent();
//            this.Text = "料仓测量数据模拟显示";

//        }

//        private void analysisData_Load(object sender, EventArgs e)
//        {
//            try
//            {
//                this.label2.Text = binName;

//                DataBase db = new DataBase();//连接数据库
//                db.command.CommandText = "select * from [bininfo] where [BinName] = '" + binName + "'";
//                db.command.Connection = db.connection;
//                db.Dr = db.command.ExecuteReader();
//                while (db.Dr.Read())
//                {

//                    zhijing = db.Dr["Diameter"].ToString().Trim();
//                    heigh = db.Dr["CylinderH"].ToString().Trim();
//                    xiazhui = db.Dr["PyramidH"].ToString().Trim();
//                    midu = db.Dr["Density"].ToString().Trim();
//                    Margin = db.Dr["Margin"].ToString().Trim();
//                    Top = db.Dr["BinTop"].ToString().Trim();
//                    Wheelbas = db.Dr["Wheelbase"].ToString().Trim();

//                    k = db.Dr["KValue"].ToString().Trim();
//                    b = db.Dr["Bvalue"].ToString().Trim();

//                    //this.label2.Text = len.ToString() + "---盘库算法:" + binState + "---仓壁距离：" + Margin + "---仓高度值:" + heigh + "---顶高：" + Top + "---步进角：" + jaiodu;
//                    this.label2.Text = this.binName;//料仓名
//                    this.label4.Text = heigh + "米";
//                    this.label6.Text = zhijing + "米";

//                    string sf = "";
//                    if (binState.Equals("1"))
//                    {
//                        sf = "半径";
//                        this.label13.Text = sf + "算法";
//                    }
//                    if (binState.Equals("2"))
//                    {
//                        sf = "直径";
//                        this.label13.Text = sf + "算法";
//                    }
//                    if(binState.Equals("0"))
//                    {
//                        sf = "满仓";
//                        this.label13.Text = sf + "算法";
//                    }
//                    if (binState.Equals("3"))
//                    {
//                        sf = "值为负值";
//                        this.label13.Text = sf ;
//                    }
//                    //this.label13.Text = sf + "算法";
//                    this.label14.Text = xiazhui + "米";
//                    this.label18.Text = midu + "吨/立方米";
//                    this.label16.Text = Margin + "米";//仓壁距离
//                    this.label17.Text = Top + "米";
//                    this.label15.Text = Wheelbas + "米";


//                    this.label35.Text = k;
//                    this.label36.Text = b;

//                }
//                db.Dr.Close();
//                db.Close();

//                //查返回的回传数据
//                DataBase db1 = new DataBase();//连接数据库
//                db1.command.CommandText = "select * from Factory.dbo.bindata where [Volume] = '" + binVol + "' and [DateTime] = '" + time + "'";
//                db1.command.Connection = db1.connection;
//                db1.Dr = db1.command.ExecuteReader();
//                while (db1.Dr.Read())
//                {

//                    backData = db1.Dr["BackData"].ToString().Trim();

//                    backAn = db1.Dr["BackAn"].ToString().Trim();//回传单的角度


//                    paint = db1.Dr["PrintNum"].ToString().Trim();//获取点数
//                    string[] p = paint.Split(',');
//                    jaiodu = db1.Dr["Jd"].ToString().Trim();
//                    this.label26.Text = p[0];
//                    this.label28.Text = p[1];
//                    this.label30.Text = p[2];
//                    this.label32.Text = jaiodu + "度";



//                }
//                db1.Dr.Close();
//                db1.Close();



//                backData = backData.Substring(0, backData.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单//backData = "809,796,784,774,766,758,752,747,744,741,739,737,1036,735,736,740,748,760,779,806,849,913,990,1
//5,1220";
//                int len = backData.Split(',').Length;
//                backAn = backAn.Substring(0, backAn.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单

//                //Log("接收到的回传数据：" + backData);
//                //Log("接收到的角度数据：" + backAn);


//                //string str_distance = "178,177,177,177,177,178,179,180,181,183,184,186,189,191,194,197,200,204,207,212,217,217,229,303,243,242,261,271,271,289,327,344,365,389,414,446,482,529";//测量距离数据。
//                string str_distance = backData;//测量距离数据。
//                string str_x_init = Margin;//仓壁距离值
//                float f2 = Convert.ToSingle(heigh);//筒高
//                float f3 = Convert.ToSingle(xiazhui);//下锥高
//                string str_height_total = heigh;//仓高度值
//                string str_top_height = Top;//顶高

//                string stepangle_str = jaiodu;//步进角
//                try
//                {
//                    diameter = float.Parse(zhijing) * 100;//直径
//                }
//                catch
//                {
//                    MessageBox.Show("请输入有效直径");
//                    return;
//                }
//                try
//                {
//                    x_init = float.Parse(str_x_init) * 100;
//                }
//                catch
//                {
//                    MessageBox.Show("请输入有效距仓壁距离值");
//                    return;
//                }
//                try
//                {
//                    height_total = (f2 + f3) * 100;//总高度
//                }
//                catch
//                {
//                    MessageBox.Show("请输入有效料仓高度值");
//                    return;
//                }
//                try
//                {
//                    top_height = float.Parse(str_top_height) * 100;
//                }
//                catch
//                {
//                    MessageBox.Show("请输入有效料仓高度值");
//                    return;
//                }

//                try
//                {
//                    stepangle = float.Parse(stepangle_str);
//                    //MessageBox.Show("步进角度==="+ stepangle);
//                }
//                catch
//                {
//                    MessageBox.Show("请输入有效料旋转角度");
//                    return;
//                }
//                //获取到的测量长度的数组
//                string[] distance_strarray = str_distance.Split(',');
//                //MessageBox.Show("长度=" + distance_strarray.Length + "直径：" + diameter + ",仓壁距离值:" + x_init + ",有效高度：" + height_total + ",顶高：" + top_height + ",旋转角度：" + stepangle);


//                string[] anal = backAn.Split(',');

//                //为了防止回传数据时的错误。进行两个数组的选择排序，每次选择最小的放在最前面。
//                //进行排序
//                for(int i = 0; i < anal.Length; i++)
//                {
//                    int min = int.Parse(anal[i]);//定义当前的是最小的
//                    int sit = i;
//                    for(int j = i+1; j < anal.Length; j++)
//                    {
//                        int pnum = int.Parse(anal[j]);
//                        if (min > pnum)//如果有比他还小的，记录最小的下标，
//                        {
//                            min = pnum;
//                            sit = j;
//                        }

//                    }
//                    //两个数组进行排序
//                    string temp = anal[i];
//                    anal[i] = anal[sit];
//                    anal[sit] = temp;


//                    string temp1 = distance_strarray[i];
//                    distance_strarray[i] = distance_strarray[sit];
//                    distance_strarray[sit] = temp1;
//                }




//                string str = "";
//                for(int i = 0; i < anal.Length; i++)
//                {
//                    str += anal[i] + ",";
//                }
//                //MessageBox.Show("拼接的字符串==" + str);

//                //MessageBox.Show(len + "\r\n" + anal.Length);
//                for (int i = 0; i < anal.Length; i++)
//                {
//                    int xiaojiao = int.Parse(anal[i]);//每次的那个值
//                                                      //Log("角度：" + xiaojiao);
//                    angle_list.Add(xiaojiao);
//                }

//                OriginalValue = new CheckData[distance_strarray.Length];
//                MeansureValue = new CheckData[distance_strarray.Length];
//                for (int i = 0; i < distance_strarray.Length; i++)
//                {
//                    OriginalValue[i] = new CheckData();//每一个开辟地址空间
//                    MeansureValue[i] = new CheckData();
//                }
//                for (int i = 0; i < distance_strarray.Length; i++)
//                {
//                    try
//                    {
//                        OriginalValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
//                        MeansureValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
//                    }
//                    catch
//                    {
//                        return;
//                    }
//                }
//                //angle_list放着角度*stepangle（自己设置的角度）
//                if (angle_list.Count != 0)
//                {
//                    for (int i = 0; i < distance_strarray.Length; i++)
//                    //将i改为真正获取的角度值
//                    {
//                        OriginalValue[i].CalcRadius = OriginalValue[i].MeansureLength * (float)(Math.Sin((angle_list[i] * stepangle) * Math.PI / 180));// + x_init。不应该加上边距
//                        if (OriginalValue[i].CalcRadius > data_x_max)
//                        {
//                            data_x_max = OriginalValue[i].CalcRadius;
//                        }

//                        OriginalValue[i].CalcHeight = OriginalValue[i].MeansureLength * (float)(Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
//                        if ((height_total - OriginalValue[i].CalcHeight) > data_y_max)
//                        {
//                            data_y_max = height_total - OriginalValue[i].CalcHeight;
//                        }
//                    }
//                }

//                ////将处理的数据传给矫正数组
//                for (int i = 0; i < distance_strarray.Length; i++)
//                {
//                    MeansureValue[i].CalcHeight = OriginalValue[i].CalcHeight;
//                    MeansureValue[i].MeansureLength = OriginalValue[i].MeansureLength;
//                    MeansureValue[i].CalcRadius = OriginalValue[i].CalcRadius;
//                }

//                DataCheck(MeansureValue.Length);
//                if (diameter > data_x_max)
//                {
//                    data_x_max = diameter;
//                }

//                if (height_total > data_y_max)
//                {
//                    data_y_max = height_total;
//                }
//            }
//            catch(Exception ea)
//            {
//                MessageBox.Show("数据没有及时回传！请再盘一次" + ea.ToString());
//                this.Close();
//            }
//        }
//        private void panel1Paint()
//        {
//            Graphics g = panel1.CreateGraphics();

//            float x_max = panel1.Width - 10;
//            float y_max = panel1.Height - 10;

//            //画直径示意线
//            Pen p = new Pen(Color.Green, 2);

//            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
//            //画边框线
//            p = new Pen(Color.Black, 1);
//            //p.DashStyle =
//            g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
//            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
//            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
//            //画中心线
//            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
//            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

//            Font drawfont = new Font("宋体", 8);
//            SolidBrush drawbrush = new SolidBrush(Color.Black);
//            //画一次原始数据

//            //如果是负值，则不显示图像
//            if (binState.Equals("3"))
//            {
//                return;
//            }


//            for (int i = 0; i < OriginalValue.Length; i++)
//            {
//                float x1 = OriginalValue[i].CalcRadius;
//                x1 = x1 * (x_max / data_x_max);
//                float y1 = height_total - top_height - OriginalValue[i].CalcHeight;//。总高-顶高-测量出来的三角形的高度=打点到地面的实际距离。实际的测量出的
//                y1 = y_max - y1 * (y_max / data_y_max);
//                //计算第一个点的坐标
//                SolidBrush brush = new SolidBrush(Color.Black);
//                g.FillEllipse(brush, x1, y1, 2, 2);

//                //System.Console.WriteLine("y1:{0}\n", y1);
//                if (i + 1 < OriginalValue.Length)
//                {
//                    float x2 = OriginalValue[i + 1].CalcRadius;
//                    x2 = x2 * (x_max / data_x_max);
//                    float y2 = height_total - top_height-OriginalValue[i + 1].CalcHeight;
//                    y2 = y_max - y2 * (y_max / data_y_max);
//                    //g.FillEllipse(brush, x2, y2, 2, 2);

//                    p = new Pen(Color.Blue, 1);

//                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));//将两个点连线

//                }
//            }

//        }
//        private void panel2Paint()
//        {
//            //DataCheck(MeansureValue.Length);
//            Graphics g = panel2.CreateGraphics();

//            float x_max = panel2.Width - 10;
//            float y_max = panel2.Height - 10;


//            //画直径示意线
//            Pen p = new Pen(Color.Green, 2);

//            g.DrawLine(p, new PointF(0, y_max), new PointF(x_max, y_max));
//            //画边框线
//            p = new Pen(Color.Black, 1);
//            //p.DashStyle =
//            g.DrawLine(p, new PointF(0, 0), new PointF(0, y_max));
//            g.DrawLine(p, new PointF(0, 0), new PointF(x_max, 0));
//            g.DrawLine(p, new PointF(x_max, 0), new PointF(x_max, y_max));
//            //画中心线
//            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
//            g.DrawLine(p, new PointF(x_max / 2, 0), new PointF(x_max / 2, y_max));

//            Font drawfont = new Font("宋体", 8);
//            SolidBrush drawbrush = new SolidBrush(Color.Black);
//            //画一次原始数据
//            if (binState.Equals("3"))
//            {
//                return;
//            }
//            //画二次检验线
//            string str = "";
//            for (int i = 0; i < MeansureValue.Length; i++)
//            {
//                float x1 = MeansureValue[i].CalcRadius;
//                x1 = x1 * (x_max / data_x_max);
//                float y1 = height_total - top_height - MeansureValue[i].CalcHeight;
//                y1 = y_max - y1 * (y_max / data_y_max);
//                SolidBrush brush = new SolidBrush(Color.Black);
//                g.FillEllipse(brush, x1, y1, 2, 2);
//                if (i + 1 < MeansureValue.Length)
//                {
//                    float x2 = MeansureValue[i + 1].CalcRadius;
//                    x2 = x2 * (x_max / data_x_max);
//                    float y2 = height_total - top_height - MeansureValue[i + 1].CalcHeight;
//                    y2 = y_max - y2 * (y_max / data_y_max);
//                    g.FillEllipse(brush, x2, y2, 2, 2);
//                    p = new Pen(Color.Red, 1);
//                    g.DrawLine(p, new PointF(x1, y1), new PointF(x2, y2));
//                }

//                //str += MeansureValue[i].CalcHeight + "," + MeansureValue[i].CalcRadius + "\r\n";
//            }

//            //MessageBox.Show(str);

//        }





//        private void button1_Click(object sender, EventArgs e)
//        {
//            //MessageBox.Show("计算原始数据");
//            //已知了是否是半径直径，有测量参数的数据，有步进角，有测量点的个数
//            CheckData[] dataValue = new CheckData[OriginalValue.Length];
//            for(int n = 0; n < dataValue.Length; n++)
//            {
//                dataValue[n] = new CheckData();//深拷贝，必须每个都开辟地址空间
//            }
//            //dataValue = OriginalValue;//原始数据是厘米为单位的，这里将他们改成米为单位的
//            //for(int j = 0; j < OriginalValue.Length; j++)
//            string str = "";
//            for(int i = 0; i < dataValue.Length; i++)
//            {
//                dataValue[i].CalcHeight = OriginalValue[i].CalcHeight / 100;
//                dataValue[i].CalcRadius = OriginalValue[i].CalcRadius / 100;
//                dataValue[i].MeansureLength = OriginalValue[i].MeansureLength / 100;
//                str += dataValue[i].MeansureLength + "," + dataValue[i].CalcHeight + "," + dataValue[i].CalcRadius + "\r\n";

//            }
//            //MessageBox.Show(str);
//            float f1 = Convert.ToSingle(zhijing);//直径
//            float f2 = Convert.ToSingle(heigh);//筒高
//            float f3 = Convert.ToSingle(xiazhui);//下锥高

//            //扫描仪距顶高度
//            float f4 = Convert.ToSingle(Top);//
//            //边距
//            float f5 = Convert.ToSingle(Margin);//
//            float f6 = Convert.ToSingle(Wheelbas);//轴距
//            float f7 = Convert.ToSingle(jaiodu);//轴距
//            float f8 = Convert.ToSingle(midu);


//            //需要输入筒仓的参数，通过数据库中提取出来的
//            WarehouseStructType wareData = new WarehouseStructType(
//            f1,
//            (f1 / 2F),
//            f2,
//            f3,
//            f4,
//            f5,
//            f6,
//            f7);

//            //CalV cv = new CalV();
//            //查询数据，找出k，b值
//            if(k == null || k == "" || b == null || b == "")
//            {
//                k = "100";
//                b = "0";
//            }

//            float kk = float.Parse(k);
//            float bb = float.Parse(b);

//            //MessageBox.Show("K的值是==" + kk + ",b的值是" + bb);

//            float v = kk * VolumeCalculate(int.Parse(binState), dataValue.Length, f2 + f3 - f4, dataValue, wareData) / 100 + bb;//除以100是将k值除以100
//            label24.Text = v + "立方米";
//            label25.Text = v * f8 + "吨";

//            for (int i = 0; i < dataValue.Length; i++)             //也可以用sizeof()写
//            {
//                dataValue[i] = null;
//            }
//        }
//        private void button2_Click(object sender, EventArgs e)
//        {

//            //MessageBox.Show("计算过滤数据");

//            CheckData[] dataValue = new CheckData[MeansureValue.Length];
//            for (int n = 0; n < dataValue.Length; n++)
//            {
//                dataValue[n] = new CheckData();//深拷贝，必须每个都开辟地址空间
//            }
//            string str = "";
//            //DataCheck(MeansureValue.Length);
//            for (int i = 0; i < dataValue.Length; i++)
//            {
//                dataValue[i].CalcHeight = MeansureValue[i].CalcHeight / 100;
//                dataValue[i].CalcRadius = MeansureValue[i].CalcRadius / 100;
//                dataValue[i].MeansureLength = MeansureValue[i].MeansureLength / 100;

//                str += dataValue[i].MeansureLength +"," + dataValue[i].CalcHeight + ",半径=" + dataValue[i].CalcRadius + "\r\n";

//            }
//            //MessageBox.Show(str);
//            float f1 = Convert.ToSingle(zhijing);//直径

//            float f2 = Convert.ToSingle(heigh);//筒高
//            float f3 = Convert.ToSingle(xiazhui);//下锥高
//            //扫描仪距顶高度
//            float f4 = Convert.ToSingle(Top);//
//            //边距
//            float f5 = Convert.ToSingle(Margin);//
//            float f6 = Convert.ToSingle(Wheelbas);//轴距
//            float f7 = Convert.ToSingle(jaiodu);//轴距
//            float f8 = Convert.ToSingle(midu);
//            //MessageBox.Show("直径是==" + f1);

//            //需要输入筒仓的参数，通过数据库中提取出来的
//            WarehouseStructType wareData = new WarehouseStructType(
//            f1,
//            (f1 / 2F),
//            f2,
//            f3,
//            f4,
//            f5,
//            f6,
//            f7);

//            //CalV cv = new CalV();
//            //查询数据，找出k，b值
//            if (k == null || k == "" || b == null || b == "")
//            {
//                k = "100";
//                b = "0";
//            }

//            float kk = float.Parse(k);
//            float bb = float.Parse(b);

//            //MessageBox.Show("K的值是==" + kk + ",b的值是" + bb);

//            float v = kk * VolumeCalculate(int.Parse(binState), dataValue.Length, f2 + f3 - f4, dataValue, wareData) / 100 + bb;//除以100是将k值除以100
//            label24.Text = v + "立方米";
//            label25.Text = v * f8 + "吨";

//            for (int i = 0; i < dataValue.Length; i++)             //也可以用sizeof()写
//            {
//                dataValue[i] = null;
//            }
//        }
//        /**
//        * @brief  数据有效性检验
//          * @param  angle 全部测量点个数
//        * @retval none
//      */
//        void DataCheck(int angle)
//        {
//            int num = 0;
//            int i;
//            float average = 0;

//            //计算垂直高度平均值
//            for (i = 0; i < angle; i++)
//            {
//                average += MeansureValue[i].CalcHeight;//距仓顶高度
//            }
//            average /= angle;//平均高度

//            ////进行数据检验，粗过滤
//            for (i = 1; i < angle; i++)
//            {
//                if ((MeansureValue[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
//                    || (MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
//                {
//                    ReplaceValues(i);
//                }

//                if ((MeansureValue[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
//                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
//                {

//                    ReplaceValues(i);
//                }

//                //判断半径，正常情况半径越来越大
//                if (MeansureValue[i].CalcRadius < MeansureValue[i - 1].CalcRadius)//半径比前一个小，错误
//                {
//                    ReplaceValues(i);
//                }
//            }

//            //进行数据滤波，细过滤
//            for (i = 1; i < angle; i++)
//            {
//                if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
//                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
//                {

//                    if (i == angle - 1)//最后一个点，使用覆盖
//                    {
//                        ReplaceValues(i);
//                    }
//                    else//其余点使用平均
//                    {
//                        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
//                        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
//                        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
//                    }
//                }
//            }
//        }
//        public CheckData[] DataCheck(int angle, CheckData[] MeansureValue)
//        {
//            int num = 0;
//            int i;
//            float average = 0;

//            //计算垂直高度平均值
//            for (i = 0; i < angle; i++)
//            {
//                average += MeansureValue[i].CalcHeight;//距仓顶高度
//            }
//            average /= angle;//平均高度

//            ////进行数据检验，粗过滤
//            for (i = 1; i < angle; i++)
//            {
//                if ((MeansureValue[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
//                    || (MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
//                {
//                    ReplaceValues(i);
//                }

//                if ((MeansureValue[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
//                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
//                {

//                    ReplaceValues(i);
//                }

//                //判断半径，正常情况半径越来越大
//                if (MeansureValue[i].CalcRadius < MeansureValue[i - 1].CalcRadius)//半径比前一个小，错误
//                {
//                    ReplaceValues(i);
//                }
//            }

//            //进行数据滤波，细过滤
//            for (i = 1; i < angle; i++)
//            {
//                if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
//                    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
//                {

//                    if (i == angle - 1)//最后一个点，使用覆盖
//                    {
//                        ReplaceValues(i);
//                    }
//                    else//其余点使用平均
//                    {
//                        MeansureValue[i].MeansureLength = (MeansureValue[i - 1].MeansureLength + MeansureValue[i + 1].MeansureLength) / 2;//使用前后均值替换
//                        MeansureValue[i].CalcHeight = (MeansureValue[i - 1].CalcHeight + MeansureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
//                        MeansureValue[i].CalcRadius = (MeansureValue[i - 1].CalcRadius + MeansureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
//                    }
//                }
//            }


//            return MeansureValue;
//        }
//        private void ReplaceValues(int i)
//        {
//            float f1 = Convert.ToSingle(zhijing) * 100;//直径
//            float f5 = Convert.ToSingle(Margin) * 100;//
//            if (i > 0)
//            {
//                MeansureValue[i].CalcHeight = MeansureValue[i - 1].CalcHeight;
//                MeansureValue[i].MeansureLength = (float)(MeansureValue[i].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
//                MeansureValue[i].CalcRadius = (float)(MeansureValue[i].MeansureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°

//                ////如果校正后的半径距离大于直径，需要使其等于直径-边距，使这个点不参与计算。
//                if (MeansureValue[i].CalcRadius > f1 - f5)
//                    MeansureValue[i].CalcRadius = f1 - f5;
//            }




//        }

//        private void angle(string filename)
//        {
//            if (true)
//            {
//                distance = "";//长度的字符串
//                angle_list.Clear();
//                int i = 0;
//                FileStream aFile = new FileStream(System.Windows.Forms.Application.StartupPath + "\\back_data\\" + filename + ".txt", FileMode.Open);
//                StreamReader sr = new StreamReader(aFile);
//                string line = "";
//                int isfirst = 1;//判断是否是第一行，若不是，需要在前面添加','
//                while ((line = sr.ReadLine()) != null)
//                {
//                    if (isfirst == 0)
//                        distance += ",";
//                    string[] data = line.Split(' ');
//                    distance += data[1];
//                    try
//                    {
//                        angle_list.Add(Int32.Parse(data[0]));
//                        //textBox2.AppendText(angle_list[i].ToString());
//                        i++;
//                    }
//                    catch (Exception exc)
//                    {

//                    }
//                    isfirst = 0;
//                }
//                sr.Close();
//                aFile.Close();
//            }
//        }

//        private void analysisData_Paint(object sender, PaintEventArgs e)
//        {
//            panel1Paint();

//            panel2Paint();
//        }
//        private void button3_Click(object sender, EventArgs e)
//        {
//            this.Close();
//        }

//        private void panel1_Paint(object sender, PaintEventArgs e)
//        {

//        }


//        //原始数据点击看大图
//        private void button4_Click(object sender, EventArgs e)
//        {
//            new Thread(DaTu_show).Start();//开启新的线程来开启新的窗体
//        }
//        private void DaTu_show()
//        {
//            MethodInvoker meth = new MethodInvoker(show_cqinfo);
//            BeginInvoke(meth);
//        }
//        private void show_cqinfo()
//        {
//            dt = new DaTu();
//            dt.setX = data_x_max;//向下一个窗体传参数
//            dt.setY = data_y_max;
//            dt.setDate = OriginalValue;
//            dt.setH = height_total;
//            dt.setIsBlue = "blue";
//            dt.Show();
//        }
//        //过滤数据点击看大图
//        private void button5_Click(object sender, EventArgs e)
//        {
//            new Thread(DaTu_show2).Start();//开启新的线程来开启新的窗体
//        }
//        private void DaTu_show2()
//        {
//            MethodInvoker meth = new MethodInvoker(show_cqinfo2);
//            BeginInvoke(meth);
//        }
//        private void show_cqinfo2()
//        {
//            dt = new DaTu();
//            dt.setX = data_x_max;//向下一个窗体传参数
//            dt.setY = data_y_max;
//            dt.setDate = MeansureValue;
//            dt.setH = height_total;
//            dt.setIsBlue = "red";
//            dt.Show();
//        }

//        private void Log(string v)
//        {
//            string info = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"), v);
//            string path = "C://Mqtt//MyWareHouseLog.txt";//"C://Mqtt//MyTestLog.txt"
//            using (StreamWriter sw = File.AppendText(path))
//            {
//                sw.WriteLine(info);
//            }
//        }
//        //state 指算法：0是满仓，1是半径算法，2是直径算法
//        //num 指点的个数
//        //RelativeHeight ：整个料仓绝对的高度
//        //MeasureValue ;传入的测量点的数据
//        //wareData: 料仓的整体参数
//        private float VolumeCalculate(int state, int num, float RelativeHeight, CheckData[] MeasureValue, WarehouseStructType wareData)
//        {
//            try
//            {
//                int i, EffectiveAngle;//EffectiveAngle对于半径计算法，就是有效值的位置，对于直径计算法就是中心点的位置
//                float Volume1 = 0, Volume2 = 0; //体积1半径计算值，体积2中心点另一边直径计算值
//                float CalcPercent;//计算体积的权重比例
//                float[] ObjectHeight = new float[180];//仓库实体物料高度
//                float[] ObjectRadius = new float[180];//仓库实体物料半径
//                for (int j = 0; j < ObjectRadius.Length; j++)
//                {
//                    ObjectHeight[j] = 0F;
//                    ObjectRadius[j] = 0F;
//                }

//                EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置

//                if ((state == 1) || (state == 2))
//                {//先计算需要的参数
//                 //数据的处理
//                    EffectiveAngle = DataScreen(MeasureValue, wareData, num);//找到半径中点位置
//                    //MessageBox.Show("中心点时==" + EffectiveAngle);
//                    for (i = 0; i < num; i++)
//                    {
//                        //计算实体物料高度
//                        if (MeasureValue[i].CalcHeight < RelativeHeight)//比实际高度小
//                        {
//                            ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;
//                        }
//                        else//大于实际高度
//                        {
//                            //MessageBox.Show("点==" + i + "大于实际高度");
//                            if (i == 0)
//                            {
//                                //SetRescanFlag(1);//重盘使能
//                            }
//                            else
//                            {
//                                MessageBox.Show("第" + i + "个点大于实际高度");
//                                //ReplaceValues(i);//使用前一个点覆盖
//                                //ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//再计算
//                                ObjectHeight[i] = RelativeHeight - MeasureValue[i].CalcHeight;//相对高度减去测量后的那个点的垂直长度就是物料距离地面的高度(相对高度 ，柱体高 - 顶高 +下锥高，虚拟化为这个柱体的高度)
//                            }
//                        }


//                        //计算半径代入值
//                        if (MeasureValue[i].CalcRadius < wareData.ColumnDiameter - wareData.Margin)//比实际直径小
//                        {
//                            ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
//                        }
//                        else//大于实际直径
//                        {
//                            if (i == 0)
//                            {
//                                //SetRescanFlag(1);//重盘使能
//                            }
//                            else
//                            {
//                                MessageBox.Show("第" + i + "个点大于实际高度");
//                                //MessageBox.Show("点==" + i + "大于实际直径。。。"+MeasureValue[i].CalcRadius);
//                                ObjectRadius[i] = Math.Abs(wareData.ColumnRadius - wareData.Margin - MeasureValue[i].CalcRadius);//求绝对值.舱体半径-设备安装距离仓壁的距离-测量点的距离=以中心点为圆心的半径
//                            }
//                        }
//                    }
//                }

//                if (state == 0)//满仓，直接根据第一个点计算
//                {
//                    //计算柱体体积
//                    Volume1 = CalculateV(RelativeHeight - MeasureValue[0].CalcHeight,
//                                                                wareData.ColumnRadius,
//                                                                RelativeHeight - MeasureValue[0].CalcHeight,
//                                                                0);
//                    Volume1 = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
//                }
//                else//使用直径或者半径算法
//                {
//                    //MessageBox.Show("使用半径直径算法");
//                    //使用中心线扫描侧的点计算整个体积
//                    //计算最外圈体积
//                    Volume1 += CalculateV(ObjectHeight[0],
//                                                                wareData.ColumnRadius,
//                                                                ObjectHeight[0],
//                                                                ObjectRadius[0]);

//                    //依次计算内圈体积
//                    for (i = 0; i < EffectiveAngle - 1; i++)
//                        Volume1 += CalculateV(ObjectHeight[i],
//                                                                   ObjectRadius[i],
//                                                                   ObjectHeight[i + 1],
//                                                                   ObjectRadius[i + 1]);
//                    //计算中心柱体体积
//                    Volume1 += CalculateV(ObjectHeight[EffectiveAngle - 1],
//                                                                ObjectRadius[EffectiveAngle - 1],
//                                                                ObjectHeight[EffectiveAngle - 1],
//                                                                0);

//                    //使用中心线扫描侧的另一边的点计算整个体积		
//                    if ((num != EffectiveAngle) && (state == 2))//直径算法才会用到2是直径算法
//                    {
//                        //MessageBox.Show("使用另一侧的点计算");
//                        //计算中心柱体体积
//                        Volume2 += CalculateV(ObjectHeight[EffectiveAngle],
//                                                                    ObjectRadius[EffectiveAngle],
//                                                                    ObjectHeight[EffectiveAngle],
//                                                                    0);

//                        //依次计算内圈体积
//                        for (i = EffectiveAngle; i < num - 1; i++)
//                            Volume2 += CalculateV(ObjectHeight[i],
//                                                                        ObjectRadius[i],
//                                                                        ObjectHeight[i + 1],
//                                                                        ObjectRadius[i + 1]);

//                        //计算最外圈体积
//                        Volume2 += CalculateV(ObjectHeight[num - 1],
//                                                                    wareData.ColumnRadius,
//                                                                    ObjectHeight[num - 1],
//                                                                    ObjectRadius[num - 1]);
//                    }

//                    if (state == 2)//直径
//                    {
//                        CalcPercent = ((float)EffectiveAngle) / ((float)num);//计算权重比例，以扫描点的个数为准
//                                                                             //分别计算加权后的体积，并减去下锥空余部分的体积，就是结果
//                        wareData.Volume = Volume1 * CalcPercent + Volume2 * (1 - CalcPercent) - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
//                        //MessageBox.Show("左半边的体积：" + Volume1 + "     权重：" + CalcPercent + "  右半边的体积：" + Volume2    +"   下锥体积："+ PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3);
//                    }
//                    if (state == 1)//半径
//                    {
//                        wareData.Volume = Volume1 - PI * wareData.VertebralHeight * wareData.ColumnRadius * wareData.ColumnRadius * 2 / 3; //统一处理，挖去下锥以外的部分
//                    }
//                }
//            }
//            catch(Exception e)
//            {
//                MessageBox.Show("出错了：" + e.ToString());
//            }

//            return wareData.Volume;

//        }
//        private int DataScreen(CheckData[] MeasureValue, WarehouseStructType wareData, int num)//返回扫到中心点时的角度
//        {
//            int i;
//            for (i = 0; i < num; i++)
//            {
//                if (MeasureValue[i].CalcRadius > wareData.ColumnRadius - wareData.Margin)//当测量的长度计算出来的半径.对于直径算法，会直接返回那个中心点的编号
//                    break;
//            }
//            return i;//如果循环了所以点，都没有大于直径的值，说明这个算法是半径算法，最后一个点就是中心点，所以返回最后一个点的下标
//        }
//        /**
//          * @brief  计算空心圆柱体体积
//          * @param  H1 第一个采样点高度
//          * @param  R1 第一个采样点半径
//          * @param  H2 第二个采样点高度
//          * @param  R2 第二个采样点半径
//          * @retval 环状体体积
//        */
//        public float CalculateV(float H1, float R1, float H2, float R2)
//        {
//            float H;
//            H = (H1 + H2) / 2;
//            if (R1 > R2)
//                return PI * H * (R1 * R1 - R2 * R2);
//            else
//                return PI * H * (R2 * R2 - R1 * R1);
//        }
//    }
//}
