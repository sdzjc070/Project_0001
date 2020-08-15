
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTest
{
    public class ConvetPoint
    {
        public float[] x;
        public float[] y;
        public float[] z;
        public ConvetPoint()
        {
            x = new float[200];
            y = new float[200];
            z = new float[200];
        }

    }
    class analysisData
    {
        public CheckData[] MeansureValue;//用于计算过滤
        public CheckData[] OriginalValue;//原始值
        public List<int> angle_list = new List<int>();
        float stepangle = 0.3F;//步进角
        float PI = 3.1415926F;
        static double CHECK_PERCENT_VALUE = 0.07D;    //数据检测过滤前后点阈值
        static int CHECK_DIFF_VALUE = 2;//细过滤的阀值
        static int V_POINT_NUM = 500;//planepoints行
        static int H_POINT_NUM = 500;//planepoints列
        static float NEIGHBOR_RATIO = 1.02f;
        static float DIFF_RATIO = 1.02f;
        static float RADIUS_BUFFER = 0.3f;
        static float MAX_ANGLE = 80f;
        public float fangchang = 0;
        public float fangkuan = 0;
        public float fangzuo = 0;
        public float redius = 0;//半径


        //全局变量
        public float ColumnDiameter;//料仓直径
        public float ColumnRadius;  //料仓半径
        public float ColumnHeight;  //料仓高度
        public float TopHeight;//设备顶距
        public float VertebralHeight;//下锥高度
        public float Margin1;//设备边距

        public float StepAngle;//步进角
        public int planestepangle;//水平角


        public struct MeasureValueStructType
        {
            internal float MeasureLength; //直径上点的测量距离
            internal float CalcHeight;     //测量高度
            internal float CalcRadius;     //测量半径
#pragma warning disable CS0649 // 从未对字段“analysisData.MeasureValueStructType.angle”赋值，字段将一直保持其默认值 0
            internal int angle;
#pragma warning restore CS0649 // 从未对字段“analysisData.MeasureValueStructType.angle”赋值，字段将一直保持其默认值 0
        }

        public struct Planepoint
        {
            internal UInt32 alpha;//平面扫描时的垂直角度
            internal UInt32 beta;//平面扫描时的水平角度
            internal float length;//测量距离
            internal UInt32 clockwise;//旋转方向，1为顺时针，0为逆时针
            internal UInt32 valid;//是否为有效数据，1为有效，可以用来计算体积，0为无效，相当于去除这个点
        }

        private MeasureValueStructType[] MeasureValue = new MeasureValueStructType[100];   //保存直径上的测量点
        private Planepoint[,] planepoints = new Planepoint[V_POINT_NUM, H_POINT_NUM];     //保存非直径上的测量点

        int zjnum = 0;

        string zhijing = "";
        string Margin = "";

        public string analysis(string str)//传过来的是binname，体积和时间
        {

            string[] arr = str.Split(',');//传过来的点的值
            string name = arr[0];
            string vol = arr[1];
            string time = arr[2];
            Log("接收到的name:" + name + "," + vol + "," + time);
            
            string heigh = "";
            string xiazhui = "";
            string midu = "";
            
            string realName = "";
            string Top = "";
            string Wheelbas = "";
            string jaiodu = "";
            string paint = ""; //扫描的点数
            string sf = "";//算法
            //
            string backData = "";//回传的数据
            string backAn = "";//回传的角度
            string backAll = "";//回传的所有信息

            string type = "";//料仓的类型

            float redius = 0.0f;//半径

            string[] distance_strarray;

            float diameter;
            float height_total = 0;
            float top_height = 0.3F;

            string zhongliang = "";
            string wendu = "";
            string shidu = "";

            float data_x_max = 0;//x轴最大长度
            float data_y_max = 0;//y周最大高度
            float x_init = 0;

            string res1 = "";
            string res2 = "";
            string k = "";
            string b = "";


            try
            {
                Log("查询料仓信息");
               
                string sql = "select * from bininfo where BinID = '" + name + "'";
                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rd = msc1.getDataFromTable(sql);
                //DataBase db = new DataBase();//查询料仓信息
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                while (rd.Read())
                {
                    realName = rd["BinName"].ToString().Trim();
                    zhijing = rd["Diameter"].ToString().Trim();
                    heigh = rd["CylinderH"].ToString().Trim();
                    xiazhui = rd["PyramidH"].ToString().Trim();
                    Margin = rd["Margin"].ToString().Trim();
                    Top = rd["BinTop"].ToString().Trim();
                    Wheelbas = rd["Wheelbase"].ToString().Trim();
                    
                    k = rd["KValue"].ToString().Trim();
                    b = rd["Bvalue"].ToString().Trim();

                    type = rd["type"].ToString();//料仓类型

                }
                rd.Close();
                Log("realName=" + realName);
                Log("vol=" + vol);
                Log("time=" + time);
                Log("type料仓类型 = " + type);
                //查询回传数据
                sql = "select * from bindata where format(Volume ,2) = format( '" + vol + "' ,2) and DateTime = '" + time + "'";
                Log("sql语句=" + sql);
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlDataReader rd1 = msc1.getDataFromTable(sql);
                while (rd1.Read())
                {
                    midu = rd1["MiDu"].ToString().Trim();//获取密度
                    paint = rd1["PrintNum"].ToString().Trim();//获取点数
                   zhongliang = rd1["Weight"].ToString().Trim();//获取点数
                    wendu = rd1["Temp"].ToString().Trim();//获取点数
                    shidu = rd1["Hum"].ToString().Trim();//获取点数

                    sf = rd1["Algorithm"].ToString().Trim();//获取点数
                    jaiodu = rd1["Jd"].ToString().Trim();
                    Log("接收到的角度：" + jaiodu);
                    backData = rd1["BackData"].ToString().Trim();//回传的数据
                    backAn= rd1["BackAn"].ToString().Trim();//回传单的角度

                    backAll = rd1["BackAll"].ToString().Trim();


                    if (backData.Length == 0 || backData == null)//如果没有回传数据，就发送空的信息
                    {
                        Log("回传信息。无数据！！！");
                        //BinInfo temp = new BinInfo(realName, zhijing, heigh, xiazhui, midu, Margin, Top, Wheelbas, jaiodu, time, sf, paint);
                        BinInfo temp = new BinInfo(name, realName, zhijing, heigh, xiazhui, midu, Margin, Top, Wheelbas, jaiodu,
            vol, zhongliang, wendu, shidu, time, sf, paint, "");
                        getXY tempXY = new getXY(temp, "", "");
                        //组成json
                        string tempinfo = JsonConvert.SerializeObject(tempXY);
                        return tempinfo;

                    }

                    

                }
                Log("backData=" + backData);
                Log("backAll=" + backAll);
                rd1.Close();

                //进行计算
                
                string str_x_init = Margin;//仓壁距离值
                Log("获得到的Margin:" + str_x_init);
                Log("获得到的heigh:" + heigh);
                float f2 = Convert.ToSingle(heigh);//筒高
                Log("获得到的f2:" + f2);
                Log("xiazhui" + xiazhui);
                float f3 = Convert.ToSingle(xiazhui);//下锥高
                Log("下锥+++++");
                string str_height_total = heigh;//仓高度值
                string str_top_height = Top;//顶高
                string stepangle_str = jaiodu;//步进角
                                              //获取参数

                diameter = float.Parse(zhijing) * 100;//直径

                x_init = float.Parse(str_x_init) * 100;

                height_total = (f2 + f3) * 100;//总高度
#pragma warning disable CS0219 // 变量“x_max”已被赋值，但从未使用过它的值
                float x_max = 111;//画图框的宽
#pragma warning restore CS0219 // 变量“x_max”已被赋值，但从未使用过它的值
                float y_max = (f2 + f3) * 100;//画图框的高
                top_height = float.Parse(str_top_height) * 100;

                stepangle = float.Parse(stepangle_str);



                //if (type.Equals("侧置平扫") || type.Equals("顶置平扫"))//基础数据获取到了以后如果是平扫，就用平扫算法，坐标计算3D...计算完以后直接返回
                if(type.Equals("1")|| type.Equals("4"))
                {
                    string res3D = "";
                    redius = Convert.ToSingle(zhijing) / 2 * 100;

                    string Info3D = backAll;
                    Log("获取到的3D点数据是str = " + Info3D);
                    Log("半径为 = " + redius);


                    //设置函数返回点坐标
                    res3D = get3D_1(Info3D, redius,time,name, heigh, xiazhui,type);//没有进行过滤算法的结果点

                    Log("执行完get3D_1");


                    //组装数据
                    BinInfo bif1 = new BinInfo(name, realName, zhijing, heigh, xiazhui, midu, Margin, Top, Wheelbas, jaiodu,
                vol, zhongliang, wendu, shidu, time, sf, paint, "");

                    res1 = res3D;

                    //进行过滤算法的店
                    //res2 = res1;
                    Log("执行get3D_2");
                    res2 = get3D_2(Info3D, redius, time, name,type);

                    //string[] a = res2.Split(';');
                    //res2 = "";
                    ////Log("一共点数===" + a.Length*2 +"        总大小==" + res2.Length); 
                    //for(int m = 0; m <2000; m++)
                    //{
                    //    res2 += a[m];
                    //}
                    //Log("res2=======" + res2);
                    //res2 = res3D;//此处应该是数据过滤之后的点坐标

                    getXY data1 = new getXY(bif1, res1, res2);

                    //组成json
                    string datalast1 = JsonConvert.SerializeObject(data1);
                    //Log("最终的数据集合：-------：" + datalast1);

                    //数据全部制空，回收缓存
                    bif1 = null;
                    res1 = res2 = "";
                    data1 = null;

                    Log("3D数据返回=");
                    return datalast1;
                }




                backData = backData.Substring(0, backData.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单
                backAn = backAn.Substring(0, backAn.Length - 1); //m.Length 为m总共的长度，要去掉最后一位只需要-1就可以了。很简单

                Log("接收到的回传数据：" + backData);
                Log("接收到的角度数据：" + backAn);


                int len = backData.Split(',').Length;
                string[] anal = backAn.Split(',');


                Log("接收到的数据的长度：" + len);
                Log("接收到的角度的长度：" + anal.Length);



                //将数据排序
                distance_strarray = backData.Split(',');

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
                    //角度互换
                    string temp = anal[i];
                    anal[i] = anal[sit];
                    anal[sit] = temp;

                    //测量数据互换
                    string temp1 = distance_strarray[i];
                    distance_strarray[i] = distance_strarray[sit];
                    distance_strarray[sit] = temp1;
                }
                string teststr = "",teststr1 = "";
                for (int i = 0; i < anal.Length; i++)
                {
                    teststr += distance_strarray[i] + ",";
                    teststr1 += anal[i] + ",";
                }
                Log("排序以后测量数据：" + teststr);
                Log("排序以后角度数据：" + teststr1);



                //int JDzhi = int.Parse(jaiodu);


                for (int i = 0; i < len; i++)
                {
                    int xiaojiao = int.Parse(anal[i]);//每次的那个值

                    //Log("角度：" + xiaojiao);
                    angle_list.Add(xiaojiao);//获取点的编号，乘上角度就是步进角
                }

                //distance_strarray = backData.Split(',');//获取 长度数组

                //计算
                OriginalValue = new CheckData[distance_strarray.Length];//原始数据的值
                MeansureValue = new CheckData[distance_strarray.Length];//过滤数据
                for (int i = 0; i < distance_strarray.Length; i++)
                {
                    OriginalValue[i] = new CheckData();
                    MeansureValue[i] = new CheckData();
                }
                for (int i = 0; i < distance_strarray.Length; i++)
                {
                    try
                    {
                        OriginalValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                        MeansureValue[i].MeansureLength = float.Parse(distance_strarray[i].Trim());
                    }
                    catch
                    {

                        Log("analysisData 113行出错");
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

                        OriginalValue[i].CalcHeight = OriginalValue[i].MeansureLength * (float)(Math.Cos(angle_list[i] * stepangle * Math.PI / 180));//计算的是测量点到顶端的距离
                        if ((height_total - OriginalValue[i].CalcHeight) > data_y_max)
                        {
                            data_y_max = height_total - OriginalValue[i].CalcHeight;
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
                if (diameter > data_x_max)
                {
                    data_x_max = diameter;
                }

                if (height_total > data_y_max)
                {
                    data_y_max = height_total;
                }


                //计算点的坐标
                for (int i = 0; i < OriginalValue.Length; i++)//原始数据点的坐标
                {
                    float x1 = OriginalValue[i].CalcRadius;
                    //float x = (x1 * (x_max / data_x_max));
                    int x = (int) x1 ;
                    float y1 = height_total - OriginalValue[i].CalcHeight;
                    int y = (int) y1;
                    //计算出的点的坐标
                    res1 = res1 + x + "," + y + ";";//坐标点1

                }
                Log(res1);

                DataCheck(MeansureValue.Length);

                for (int i = 0; i < MeansureValue.Length; i++)//过滤数据点的坐标
                {
                    float x1 = MeansureValue[i].CalcRadius;
                    int x = (int)(x1);
                   
                    float y1 = height_total - MeansureValue[i].CalcHeight;
                    int y = (int)( y1);
                    res2 = res2 + x + "," + y + ";";//坐标点2
                }
                Log(res2);


                //组装数据
                BinInfo bif = new BinInfo(name, realName, zhijing, heigh, xiazhui, midu, Margin, Top, Wheelbas, jaiodu,
            vol, zhongliang, wendu, shidu, time, sf, paint, "");
                getXY data = new getXY(bif, res1, res2);

                //组成json
                string datalast = JsonConvert.SerializeObject(data);
                //Log("最终的数据集合：----：" + datalast);

                //数据全部制空，回收缓存
                bif = null;
                res1 = res2 = "";
                data = null;

                return datalast;



            }
            catch(Exception e)
            {
                Log("数据分析出错:" + e.ToString());
                
            }

            string res = "";
            //获取
            return res;

        }

        float HighSum;//算Z值时候得总和
        public string get3D_1(string info, float redius, string time, string binName, string heigh, string xiazhui,string type)
        {
            string strC = "";//标准格式的字符串（x,y,z;）

            int j = 0;//表示点数
            string[] readArray = new string[2];

			Log("info"+info);
            string str = info;
            string[] pint = str.Split(';');
            int len = pint.Length;
            int a = len - 1;


            ConvetPoint b1 = new ConvetPoint();
            ConvetPoint b2 = new ConvetPoint();

            if (type.Equals("1")) { 


                ColumnHeight = Convert.ToSingle(heigh);//从数据库中读料仓高度
                                                   // Log(ColumnHeight + "料仓高度");
                VertebralHeight = Convert.ToSingle(xiazhui);//从数据库中读下锥
                                                        // Log(ColumnHeight + "下锥高度");
                HighSum = (ColumnHeight + VertebralHeight) * 100;//计算下锥加上筒仓高


                Log("redius" + redius);
                Log("HighSum " + HighSum);
                try
                {

                    for (int i = 0; i < len - 1; i++)
                    {
                        string[] p = pint[i].Split('+');
                        b1.x[i] = (AngleOfX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                        b1.y[i] = (AngleOfY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]), redius)) / 100;
                        b1.z[i] = ((AngleOfZ(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2])))) / 100;
                        j++;

                    }

                }
                catch (Exception ee)
                {
                    Log("计算xyz坐标出错：" + ee.ToString() + "");
                }


            }else if (type.Equals("4"))
            {



                string fbian = "";
                string fkuan = "";
                string fzuo = "";

                try
                {
                    MySqlConn mscB = new MySqlConn();//新建数据库连接
                    string sql1 = "select * from bininfo where BinName = '" + binName + "'";
                    MySqlDataReader rd = mscB.getDataFromTable(sql1);
                    while (rd.Read())
                    {

  
                        fbian = rd["Fbian"].ToString().Trim();
                        fkuan = rd["Fkuan"].ToString().Trim();
                        fzuo = rd["Fzuobian"].ToString().Trim();
                        //infodata = binName + "+" + zhijing + "+" + heigh + "+" + xiazhui + "+" + midu + "+" + margin + "+" + top + "+" + zhoujv + "+" + binstata + "+" + print + "+" + bjj + "+" + spj;
                    }
                    rd.Close();
                    mscB.Close();
                }
                catch (Exception ee)
                {
                    Log("方仓查询料仓信息出错：" + ee.ToString());
                }


                ColumnHeight = Convert.ToSingle(heigh);//从数据库中读料仓高度
                                                       // Log(ColumnHeight + "料仓高度");
                VertebralHeight = Convert.ToSingle(xiazhui);//从数据库中读下锥
                                                            // Log(ColumnHeight + "下锥高度");
                HighSum = (ColumnHeight + VertebralHeight) * 100;//计算下锥加上筒仓高
                                                                 //Log(HighSum+"*******************");

                fangchang = Convert.ToSingle(fbian);
                fangkuan = Convert.ToSingle(fkuan);
                fangzuo = Convert.ToSingle(fzuo);

                fangchang = fangchang * 100;
                fangkuan = fangkuan * 100;
                fangzuo = fangzuo * 100;



                try
                {

                    for (int i = 0; i < len - 1; i++)
                    {
                        string[] p = pint[i].Split('+');
                        b1.x[i] = (AngleOfFX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                        b1.y[i] = (AngleOfFY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                        b1.z[i] = ((AngleOfZ(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2])))) / 100;
                        //Log("高度值************************" +b1.z[i]);
                        //b2.x[i] = (int)(b1.x[i] * 10000);
                        //b2.y[i] = (int)(b1.y[i] * 10000);
                        //b2.z[i] = (int)(b1.z[i] * 10000);
                        //write(i + "");
                        j++;

                    }

                }
                catch (Exception ee)
                {
                    Log("计算xyz坐标出错：" + ee.ToString() + "");
                }

            }

            try
            {

                String strB = "";//用于循环拼接的字符串

                //每次三个数循环，拼接成一个标准字符串
                for (int s = 0; s < len - 1; s++)
                {
                    strB = b1.x[s] + "," + b1.y[s] + "," + b1.z[s] + ";";
                    strC = strC + strB;

                }
                Log(strC + "");
            }
            catch (Exception ee)
            {
                Log("matlab调用出错：" + ee + "");
                return "";
            }
            return strC;
        }
        //过滤算法计算3D点 
        public string get3D_2(string info, float redius, string time,string binID,string type)
        {

            String strC="" ;
            if (type.Equals("4") || type.Equals("1"))
            {


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
 
                string id = "";
                int j = 0;


                string bjj = "";//步进角
                string spj = "";//水平角
                zjnum = 0;

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
                        Log("画图查询数据库出错=" + ee.ToString());
                    }
                    //MessageBox.Show(bjj + "########################");

                }



                try
                {
                    MySqlConn mscB = new MySqlConn();//新建数据库连接
                    string sql1 = "select * from bininfo where BinID = '" + binID + "'";
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

                        infodata = binID + "+" + zhijing + "+" + heigh + "+" + xiazhui + "+" + midu + "+" + margin + "+" + top + "+" + zhoujv + "+" + binstata + "+" + print + "+" + bjj + "+" + spj;
                    }
                    rd.Close();
                    mscB.Close();
                }
                catch (Exception ee)
                {
                   Log("查询料仓信息出错：" + ee.ToString());
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



                redius = ColumnRadius * 100;//将数据库中读到半径赋值




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
                HighSum = (ColumnHeight + VertebralHeight - TopHeight) * 100;


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
                        MeasureValue[dindex].MeasureLength = float.Parse(str2[2]);//单位厘米
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
                            planepoints[mainindex, pindex].length = float.Parse(str2[2]);//单位厘米
                                                                                         //write(planepoints[mainindex, pindex].alpha+"垂直角"+ planepoints[mainindex, pindex].beta +"水平角"+ planepoints[mainindex, pindex].length+"存入legth的值" + "clockwise"+1);
                            planepoints[mainindex, pindex].valid = 1;

                        }


                    }
                }

                for (mainindex = 0; mainindex <= zjnum; mainindex++)
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
                                                                                                 //write(planepoints[mainindex, tempindex1].alpha + "垂直角" + planepoints[mainindex, tempindex1].beta + "水平角" + planepoints[mainindex, tempindex1].length + "存入legth的值" + "clockwise" + 0);
                                planepoints[mainindex, tempindex1].valid = 1;
                                break;

                            }
                        }


                    }
                }

                for (mainindex = 0; mainindex <= zjnum; mainindex++)
                    for (pindex = 0; pindex < H_POINT_NUM; pindex++)
                    {
                        if (planepoints[mainindex, pindex].valid == 0)
                        {
                            planepoints[mainindex, pindex].alpha = 255;
                            break;
                        }
                    }

   



                //直径上的点过滤
                DataCheck1(zjnum);
  




                //两侧的点过滤，调用两次checkplanepoints进行过滤
                if (type.Equals("1"))
                {

                    checkplanepoints(planepoints, MeasureValue,redius);

                    checkplanepoints(planepoints, MeasureValue,redius);

                }
 

                ConvetPoint a3 = new ConvetPoint();///控制直径点传入矩阵的值
                ConvetPoint a4 = new ConvetPoint();//存储计算以后的直径点
                String strB = "";//用于循环拼接的字符串

                for (int i = 0; i < zjnum; i++)
                {
                    a4.x[i] = (AngleOfX(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;
                    a4.y[i] = (AngleOfY(i * StepAngle, 0, MeasureValue[i].MeasureLength, redius)) / 100;
                    a4.z[i] = ((AngleOfZ(i * StepAngle, 0, MeasureValue[i].MeasureLength))) / 100;
                    //a4.x[i] = (AngleOfFX(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;//算坐标后单位为米
                    //a4.y[i] = (AngleOfFY(i * StepAngle, 0, MeasureValue[i].MeasureLength)) / 100;
                    //a4.z[i] = ((AngleOfZ(i * StepAngle, 0, MeasureValue[i].MeasureLength))) / 100;
                    a3.x[i] = (int)(a4.x[i] * 10000);
                    a3.y[i] = (int)(a4.y[i] * 10000);
                    a3.z[i] = (int)(a4.z[i] * 10000);



                    strB = a4.x[i] + "," + a4.y[i] + "," + a4.z[i] + ";";
                    Log("s = " + strB);
                    strC = strC + strB;

                }



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
                            a1.x[d] = (AngleOfX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length, (int)planepoints[i, k].clockwise)) / 100;
                            a1.y[d] = (AngleOfY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length, redius)) / 100;//为了使数据在中间
                            a1.z[d] = ((AngleOfZ(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length))) / 100;
                            //a1.x[d] = (AngleOfFX1(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length, (int)planepoints[i, k].clockwise)) / 100;//计算坐标后单位为米
                            //a1.y[d] = (AngleOfFY(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length)) / 100;//为了使数据在中间
                            //a1.z[d] = ((AngleOfZ(planepoints[i, k].alpha, planepoints[i, k].beta, planepoints[i, k].length))) / 100;
                            a2.x[c] = (int)(a1.x[d] * 10000);//数据扩大10000倍，便于加入矩阵
                            a2.y[c] = (int)(a1.y[d] * 10000);
                            a2.z[c] = (int)(a1.z[d] * 10000);


                            strB = a1.x[d] + "," + a1.y[d] + "," + a1.z[d] + ";";
                            Log("s = " + strB);
                            strC = strC + strB;
                            c++;
                            d++;

                        }
                    }
                }
                Log("打印完成");

            

            }
            return strC;


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
            int ave_heightdiff = 0;
            int pointcount = 0;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeansureValue[i].CalcHeight;
            }
            average /= angle;

            for (i = 1; i < angle - 1; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeansureValue[i].CalcHeight - MeansureValue[i - 1].CalcHeight);
                pointcount++;
            }
            ave_heightdiff = ave_heightdiff / pointcount;

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
                //if ((MeansureValue[i].CalcHeight > MeansureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
                //    || (MeansureValue[i].CalcHeight < MeansureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小

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
        }

        /// <summary>
        /// 用于过滤算法的直径过滤
        /// </summary>
        /// <param name="angle"></param>
        void DataCheck1(int angle)
        {
#pragma warning disable CS0219 // 变量“num”已被赋值，但从未使用过它的值
            int num = 0;
#pragma warning restore CS0219 // 变量“num”已被赋值，但从未使用过它的值
            int i;
            float average = 0;
            int ave_heightdiff = 0;
            int pointcount = 0;

            //计算垂直高度平均值
            for (i = 0; i < angle; i++)
            {
                average += MeasureValue[i].CalcHeight;//距仓顶高度
            }
            average /= angle;//平均高度

            for (i = 1; i < angle - 1; i++)
            {
                ave_heightdiff += (int)Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight);
                pointcount++;
            }
            ave_heightdiff = ave_heightdiff / pointcount;

            ////进行数据检验，粗过滤
            for (i = 1; i < angle; i++)
            {
                if ((MeasureValue[i].CalcHeight > average * 3)                         //条件1：大于3倍平均值
                    || (MeasureValue[i].CalcHeight > MeasureValue[i - 1].CalcHeight * 2)) //条件2：比前一个值的2倍还大
                {
                    ReplaceValues1(i);
                }

                if ((MeasureValue[i].CalcHeight < average / 3)                         //条件1：小于均值的1/3
                    || (MeasureValue[i].CalcHeight < MeasureValue[i - 1].CalcHeight / 2)) //条件2：比前一个值的1/2还小
                {

                    ReplaceValues1(i);
                }

                //判断半径，正常情况半径越来越大
                if (MeasureValue[i].CalcRadius < MeasureValue[i - 1].CalcRadius)//半径比前一个小，错误
                {
                    ReplaceValues1(i);
                }
            }

            //进行数据滤波，细过滤
            for (i = 1; i < angle; i++)
            {
                //if ((MeasureValue[i].CalcHeight > MeasureValue[i - 1].CalcHeight * (1 + CHECK_PERCENT_VALUE))//比前一个值的1.07倍大
                //    || (MeasureValue[i].CalcHeight < MeasureValue[i - 1].CalcHeight * (1 - CHECK_PERCENT_VALUE)))//比前一个0.93倍小
                //{

                //    if (i == angle - 1)//最后一个点，使用覆盖
                //    {
                //        ReplaceValues1(i);
                //    }
                //    else//其余点使用平均
                //    {
                //        MeasureValue[i].MeasureLength = (MeasureValue[i - 1].MeasureLength + MeasureValue[i + 1].MeasureLength) / 2;//使用前后均值替换
                //        MeasureValue[i].CalcHeight = (MeasureValue[i - 1].CalcHeight + MeasureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                //        MeasureValue[i].CalcRadius = (MeasureValue[i - 1].CalcRadius + MeasureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                //    }
                //}

                if (Math.Abs(MeasureValue[i].CalcHeight - MeasureValue[i - 1].CalcHeight) > ave_heightdiff * CHECK_DIFF_VALUE)
                {


                    if (i == angle - 1)
                    {
                        ReplaceValues1(i);
                    }
                    else
                    {
                        MeasureValue[i].MeasureLength = (MeasureValue[i - 1].MeasureLength + MeasureValue[i + 1].MeasureLength) / 2;//使用前后均值替换
                        MeasureValue[i].CalcHeight = (MeasureValue[i - 1].CalcHeight + MeasureValue[i + 1].CalcHeight) / 2;//使用前后均值替换
                        MeasureValue[i].CalcRadius = (MeasureValue[i - 1].CalcRadius + MeasureValue[i + 1].CalcRadius) / 2;//使用前后均值替换
                    }


                }
            }
            // MessageBox.Show("******************");
        }

        private void ReplaceValues(int i)
        {
            //MeansureValue[i].CalcHeight = MeansureValue[i - 1].CalcHeight;
            //MeansureValue[i].MeansureLength = (float)(MeansureValue[i - 1].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
            //MeansureValue[i].CalcRadius = (float)(MeansureValue[i].MeansureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°
            //float f1 = Convert.ToSingle(zhijing) * 100;//直径
            //float f5 = Convert.ToSingle(Margin) * 100;//
            //if (i > 0)
            //{
                MeansureValue[i].CalcHeight = MeansureValue[i - 1].CalcHeight;
                MeansureValue[i].MeansureLength = (float)(MeansureValue[i].CalcHeight / Math.Cos(angle_list[i] * stepangle * Math.PI / 180));
                MeansureValue[i].CalcRadius = (float)(MeansureValue[i].MeansureLength * Math.Sin(angle_list[i] * stepangle * Math.PI / 180)); //(Math.PI / 180) 为1°

                //////如果校正后的半径距离大于直径，需要使其等于直径-边距，使这个点不参与计算。
                //if (MeansureValue[i].CalcRadius > f1 - f5)
                //    MeansureValue[i].CalcRadius = f1 - f5;
            //}


        }


        /// <summary>
        /// 直径过滤算法中的调用
        /// </summary>
        /// <param name="i"></param>
        private void ReplaceValues1(int i)
        {
            float f1 = Convert.ToSingle(zhijing) * 100;//直径
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

        private void Log(string v)
        {
            string info = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"), v);
            string path = "C://Mqtt//MyTestLog.txt";//"C://Mqtt//MyTestLog.txt"
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(info);
            }
        }




        /// <summary>
        /// 角度转换为X坐标
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度</param>
        /// <returns></returns>
        public float AngleOfX(float Angle1, float Angle2, float Length)
        {

            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Xcoor;

            //if (A2 == 0)
            //{
            //    Xcoor = redius - (float)(L * Math.Sin(A1));

            //}
            //else
            //{
            //    Xcoor = redius - (float)((L * Math.Sin(A1)) * (Math.Cos(A2)));
            //}

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


            if (fangkuan / 2 > fangzuo)//先判断左边距与方宽一半的大小
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
            else if (fangkuan / 2 < fangzuo)
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
        /// <summary>
        /// 角度转换为Y坐标
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度</param>
        /// <returns></returns>
        public float AngleOfY(float Angle1, float Angle2, float Length,float redius)
        {
            float L = (float)(Math.Sin(Angle1 * Math.PI / 180) * Length);
            float Ycoor = 0.0f;

            //if (A2 == 0)
            //{
            //    Ycoor = 0;

            //}
            //else
            //{
            //    Ycoor = (float)((L * Math.Sin(A1)) * (Math.Sin(A2)));
            //}
            if (Angle2 > 0)
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
            else
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

                Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - fangchang / 2;

            }
            else//左边的点
            {
                Angle2 = 0 - Angle2;
                Ycoor = (float)(L * (Math.Cos(Angle2 * Math.PI / 180))) - fangchang / 2;

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
        /// <param name="Length">长度</param>
        /// <returns></returns>
        public float AngleOfZ(float Angle1, float Angle2, float Length)
        {
            float A1 = Angle1;
            float A2 = Angle2;
            float L = Length;
            float Zcoor;

            Zcoor = (float)(HighSum - L * Math.Cos(A1 * Math.PI / 180));


            return Zcoor;

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

        //3D过滤算法
        void checkplanepoints(Planepoint[,] point, MeasureValueStructType[] mm, float redius1)
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
            redius = redius1;



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

        float leftheight(int mainindex, int pindex, Planepoint[,] ss, MeasureValueStructType[] mm)
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
            clockwise = planepoints[mainindex + 1, pindex + 1].clockwise;
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

        float topheight(int mainindex, int pindex, Planepoint[,] ss, MeasureValueStructType[] mm)
        {//计算平面上某点的上边点的高度，没有，则返回0
            Planepoint[,] planepoints = ss;
            MeasureValueStructType[] MeasureValue = mm;
            UInt32 clockwise, beta;
            int index;
            float Height = ColumnHeight - TopHeight + VertebralHeight;
            clockwise = planepoints[mainindex, pindex].clockwise;
            beta = planepoints[mainindex, pindex].beta;
            index = 0;
            if (mainindex + 1 <= zjnum)
            {
                while (planepoints[mainindex + 1, index].alpha != 255)
                {
                    if (planepoints[mainindex + 1, index].beta == beta && planepoints[mainindex + 1, index].clockwise == clockwise && planepoints[mainindex + 1, index].valid == 1)
                        return computeheight(mainindex + 1, index, planepoints);
                    index++;


                }
                beta = (UInt32)(beta - planestepangle);
                if (beta == 0)
                {
                    return Height - MeasureValue[mainindex + 1].MeasureLength * (float)(Math.Cos((mainindex + 1) * StepAngle * Math.PI / 180));
                }
                index = 0;
                while (planepoints[mainindex + 1, index].alpha != 255)
                {
                    if (planepoints[mainindex + 1, index].beta == beta && planepoints[mainindex + 1, index].clockwise == clockwise && planepoints[mainindex + 1, index].valid == 1)
                        return computeheight(mainindex + 1, index, planepoints);
                    index++;


                }
                beta = planepoints[mainindex, pindex].beta;
                beta = (UInt32)(beta + planestepangle);
                if (beta > MAX_ANGLE)
                {
                    return 0;
                }
                index = 0;
                while (planepoints[mainindex + 1, index].alpha != 255)
                {
                    if (planepoints[mainindex + 1, index].beta == beta && planepoints[mainindex + 1, index].clockwise == clockwise && planepoints[mainindex + 1, index].valid == 1)
                        return computeheight(mainindex + 1, index, planepoints);
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
    }
}
