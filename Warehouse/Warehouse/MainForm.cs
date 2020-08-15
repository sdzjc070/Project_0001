using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO.Ports;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Update;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DLLFullPrint;
using MySql.Data.MySqlClient;
using text12;



using MathWorks.MATLAB.NET.Arrays;

namespace Warehouse
{
    public partial class MainForm : Form
    {

        public static int TIME = 2;//向状态表中添加的时间
        private static int TIME_WAIT = 2;//半双工等待时间
        private static int s_Produce = 3600;//时间戳时间

        Users curr_user = new Users();
        Time f = new Time();//不适用动态创建的原因是：添加定时功能时间的时候需要使用它的一个变量

        public TransCoding Data = new TransCoding();
        private int pageSize = 0;     //每页显示行数
        private int nMax = 0;         //总记录数s
        private int pageCount = 0;    //页数＝总记录数/每页显示行数
        private int pageCurrent = 0;   //当前页号
        private int nCurrent = 0;      //当前记录行

        private int chartCount = 0;//图像总页数
        private int chartCurrent = 0;//图像当前页数
        DataSet ds = new DataSet();
        DataTable dtInfo = new DataTable();

        private CircularQueue<string> cirQueue = new CircularQueue<string>(3000);//循环队列，用于处理接收到的数据
        private List<FacMessage> list_status = new List<FacMessage>();//将回应信息加入到这个链表中
        private System.Timers.Timer t_monitor;//用于监控料仓时定时向中控发送请求获取料的高度的定时器
        System.Media.SoundPlayer player = new System.Media.SoundPlayer();
        private int alarm = 0;//超过预警阈值的料仓数量
        private int SqlConnect = 0;//判断是否连接上数据库
        private int ins_num = 1;//用于记录指令状态的指令序号
        private Mutex list_mutex = new Mutex();//用于更改链表时进行异步操作的互斥锁

        private Mutex file_mutex = new Mutex();//文件互斥锁
        private string backdata_path = "";//回传数据的存储路径

        private List<FacMessage> send_ins = new List<FacMessage>();//盘库时，每30秒发送一次查询状态函数，这个链表标记哪个料仓需要查询
        private List<FacMessage> searchdata_ins = new List<FacMessage>();//存放查询盘库结果指令

        private string writeToFile_buffer = "";//回传数据缓冲区
        private int recv_num = 0;//接收到回传数据的个数

        private string adddress = "";//记录查询指令是否接收到

        /*定义一个发送指令队列oper_ins，主要应用于盘库、监控、清洁镜头指令的发送检测
         * 用法：点击盘库（清洁镜头，监控）按钮后，先发送查询指令
         * 但是程序不能等待查询指令回复之后再做出相应的操作
         * 因此，只能将盘库（清洁镜头、监控）指令添加到循环队列中，而不发送
         * 当查询指令回复结果后，根据回复信息，进行下一步操作
         * 例如，若接收到“料仓无操作”信息，则在此队列中找出关于这个料仓要进行何种操作的指令，发送出去
         * 若接收到“料仓正忙”信息，则提示用户料仓的当前状态，并将这个队列中关于这个料仓的所有的操作指令删除
         * 此队列中，仅包含监控、清洁镜头、监控这三类指令
         */
        private Queue<FacMessage> oper_ins = new Queue<FacMessage>();

        //指令发送队列
        private Queue<FacMessage> sendIns_queue = new Queue<FacMessage>();

        //目的指令缓冲区
        private Queue<FacMessage> aim_ins = new Queue<FacMessage>();//将查询指令的目的指令放入这个缓冲区中，发送完查询指令后将目的指令放入oper_ins中

        private System.Timers.Timer t_status;//用于遍历状态链表的定时器

        private List<Clean> clean_list = new List<Clean>();//用于存放哪些料仓正在清洁镜头

        private bool isplaying = false;//判断是否正在播放音乐

        private int port_mask = 0;//屏蔽串口

        private BackData[] backdata = new BackData[600];//回传数据
        private int back_complet = -1;//是否回传完成

        private int port_isopen = 0;

        static private Form_Login form_login = null;

        private int flag_threadout = 1;//退出登录时，将这个标识改为0，所有的线程将阻塞

        private int it_oper = 0;//盘库列表的索引
        private List<FacMessage> CalcVol_list = new List<FacMessage>();//正在盘库链表

        private List<OperMsg> Auto_list = new List<OperMsg>();//定时链表
        private int timer1_mask = 0;

        private List<FacMessage> NoAckList = new List<FacMessage>();//记录未接收到的指令次数,其中的节点存两个信息，料仓编号和未接收到指令的次数

        private int msgBoxNum = 0;//MessageBox编号，每弹出一个MessageBox自增1，大于2000变为0



        private string rowDateTime = "";
        private string rowBinName = "";
        private string rowBinVol = "";
        private string rowBinState = "";
        private string backLengthData = "";
        private string jumpToAn = "";//用于历史记录显示中的右击传值
        public int numxuanzhong = 0;
        public int xuanOne = 0;
        public string tempxuan = "";
        public string numxuan = "";



        /// <summary>
        /// /////////////////////////////////////////////
        /// 进程间通信
        /// </summary>
        MessageQueue mq;//mian发送
        MessageQueue mq1;//mqtt发送
        int alterPanKuNum = 0;
        int isOkChuankou = -2;
        int back = 0;

        string correntWenDo = "";//当前查询的


        MySqlConn msc1 ;//数据库连接

        analysisData cqform;


        private LogHisForm lhf = new LogHisForm();

        bool isRequireWenDu = false;
        int ispk = 0;

        public float redius = 750f;//半径


        public MainForm(string name, string admin, Form_Login frm)
        {
            InitializeComponent();
            curr_user.name = name;
            curr_user.admin = admin;
            form_login = frm;
            //System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
        }

        public MainForm()
        {
        }

        /// <summary>
        /// 软件载入时初始化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {//打开这个窗体时执行
            //Console.WriteLine("fdsfdsf");
            label7.Text = curr_user.name;
            if (curr_user.admin.Equals("1"))
            {//根据用户权限来处理空间的显示或者隐藏
                label7.ForeColor = Color.Red;
                普通用户管理ToolStripMenuItem.Visible = true;
                管理员密码修改ToolStripMenuItem.Visible = true;
                test2ToolStripMenuItem.Visible = true;
                testToolStripMenuItem.Visible = true;
                添加料仓ToolStripMenuItem.Visible = true;
                更改名称ToolStripMenuItem.Visible = true;
                串口选择ToolStripMenuItem.Visible = true;
                软件升级ToolStripMenuItem.Visible = true;
                服务器设置ToolStripMenuItem.Visible = true;
                数据库管理ToolStripMenuItem.Visible = true;
                开启回传数据ToolStripMenuItem.Visible = true;
                获取基础信息ToolStripMenuItem.Visible = true;
                直接查询ToolStripMenuItem.Visible = false;
            }
            else
            {
                label7.ForeColor = Color.Black;
                普通用户管理ToolStripMenuItem.Visible = false;
                管理员密码修改ToolStripMenuItem.Visible = true;
                test2ToolStripMenuItem.Visible = false;
                testToolStripMenuItem.Visible = false;
                添加料仓ToolStripMenuItem.Visible = false;
                更改名称ToolStripMenuItem.Visible = false;
                串口选择ToolStripMenuItem.Visible = false;
                软件升级ToolStripMenuItem.Visible = false;
                服务器设置ToolStripMenuItem.Visible = false;
                数据库管理ToolStripMenuItem.Visible = false;
                开启回传数据ToolStripMenuItem.Visible = false;
                获取基础信息ToolStripMenuItem.Visible = false;
                直接查询ToolStripMenuItem.Visible = false;
            }
            try
            {//检测数据库是否可以连接
                msc1 = new MySqlConn();//数据库连接
                SqlConnect = 1;
            }
            catch (SqlException se)
            {
                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                thread_file.Start(se.ToString());
                new Thread(new ParameterizedThreadStart(showBox)).Start("数据库连接异常\r\n");

            }
            LoadAllMQ();//加载消息队列
            string contosql = conTosql();
            if (contosql.Equals("1"))
            {//检测到数据库中的表已经创建齐全
                SqlConnect = 1;
            }
            else
            {//创建未创建的数据表
                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                thread_file.Start("数据表没有创建齐全" + contosql.ToString());

            }


            string path = System.Windows.Forms.Application.StartupPath;
            if (File.Exists(path + "\\serialPort.txt") == false)//创建保存串口信息的文件
                File.Create(path + "\\serialPort.txt").Close();

            StreamReader sr = new StreamReader(path + "\\serialPort.txt", Encoding.Default);
            String line = sr.ReadLine();
            if (line == null)
            {
                //MessageBox.Show("请先设置串口", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请先设置串口");

                new Thread(display_noport).Start();
            }
            else
            {
                //向mq发送信息，通过服务检查串口是否正常
                JsonMsg js = new JsonMsg("1", "isChuankouOk");
                string sendMqttInfo = JsonConvert.SerializeObject(js);
                WriteMQ(sendMqttInfo);

                
                Thread connectPortLive = new Thread(connectPort);//解析数据
                connectPortLive.Start();



                //接收一次串口是否正常的信息
                isOkChuankou = RecChuanKouMsg(mq1);
                if (isOkChuankou == 1)
                {
                    //MessageBox.Show("串口正常");
                    port_isopen = 1;
                }
                else if (isOkChuankou == 0)
                {
                    //new Thread(new ParameterizedThreadStart(showBox)).Start("串口连接不正常，请重启软件。或插拔串口，重启服务");//
                    richTextBox1.AppendText(DateTime.Now.ToString() + "串口连接不正常，请重启软件。或插拔串口，重启服务" + "\r\n\r\n");
                }
                else if (isOkChuankou == -1)
                {
                    //当串口信息连接不上的时候，进行提示
                    new Thread(new ParameterizedThreadStart(showBox)).Start("服务没有打开");//串口设置已失效，请重新设置
                }
                //开启接收数据的线程
                Thread recMsg = new Thread(RecMsg);//接收指令线程
                recMsg.Start(mq1);
                //string[] serial = line.Split('+');//文件中串口信息按照"+"分隔
                //try
                //{
                //    //this.serialPort1 = new System.IO.Ports.SerialPort(this.components);
                //    serialPort1.PortName = serial[0];
                //    //this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
                //    serialPort1.BaudRate = int.Parse(serial[1]);
                //    serialPort1.Open();
                //    port_isopen = 1;


                //    //刷新ToolStripMenuItem.PerformClick();
                //}
                //catch (Exception exc)
                //{
                //    new Thread(new ParameterizedThreadStart(showBox)).Start("串口设置已失效，请重新设置");
                //}
                Thread thread_takeData = new Thread(takeData);//解析数据
                thread_takeData.Start();
                if (port_isopen == 1)
                {//串口正常
                    Thread load_fac = new Thread(OpenMainForm);//启动时显示料仓线程
                    load_fac.Start();
                }
                else
                {
                    new Thread(display_noport).Start();
                }
            }
            sr.Close();
            //新建监控窗口，但是不显示
            monitor = new Monitor();

            //asc.controllInitializeSize(this);
            t_monitor = new System.Timers.Timer(30000);//定时器，每隔30秒向中控获取监控获得的高度值
            //这个定时器常开，打开以后不关闭
            t_monitor.Elapsed += new System.Timers.ElapsedEventHandler(inquire_height);
            t_monitor.AutoReset = true;
            t_monitor.Enabled = false;


            if (SqlConnect == 1)
            {
                t_status = new System.Timers.Timer(1000);//状态链表使用的定时器，判断是否超时
                t_status.Elapsed += new System.Timers.ElapsedEventHandler(getStatus);
                t_status.AutoReset = true;
                t_status.Enabled = true;
                timer1.Enabled = true;//清洁镜头时间的定时器，默认关闭，数据库可以连接时打开

                Thread sendins_thread = new Thread(SendIns);//发送指令线程
                sendins_thread.Start();


                //加载定时信息线程
                Thread loadAuto = new Thread(LoadAuto);
                loadAuto.Start();

                //new Thread(StartKiller).Start();

            }

            //启动时候加载一下图画
            try
            {     
                Class12 ts2 = new Class12();
                //ts2.text12(arr1);
                ThreadStart start = new ThreadStart(ts2.text12);
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.ToString() + "");
            }

        }
        private void connectPort()
        {
            int time = 2;
            while (true)
            {
                Thread.Sleep(1000);
                if (time < 0 && isOkChuankou == -2)//超过了存活期限，且没有接收到串口信息
                {
                    MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                    //"确定要退出吗？"是对话框的显示信息，"退出系统"是对话框的标题
                    //默认情况下，如MessageBox.Show("确定要退出吗？")只显示一个“确定”按钮。
                    DialogResult dr = MessageBox.Show("服务器没有启动，请关闭软件重启服务，点击确定退出系统");

                    if (dr == DialogResult.OK)//如果点击“确定”按钮
                    {
                        System.Environment.Exit(0);
                    }

                    else//如果点击“取消”按钮
                    {
                        System.Environment.Exit(0);
                    }
                    break;
                }
                if (isOkChuankou != -2)
                {
                    break;
                }
                time--;

            }
        }


        /// <summary>
        /// 加载或者新建两个消息队列，
        /// </summary>
        private void LoadAllMQ()
        {
            //新建消息循环队列或连接到已有的消息队列,用于Main发送

            string path = ".\\Private$\\MSMQDemo";
            if (MessageQueue.Exists(path))
            {
                mq = new MessageQueue(path);
                //mq.GetAllMessages();
                mq.Purge();
                richTextBox1.AppendText("软件开启" + "\r\n");
            }
            else
            {
                richTextBox1.AppendText("新建了列表" + "\r\n");
                mq = MessageQueue.Create(path);
            }


            //新建消息循环队列或连接到已有的消息队列，用于main接收
            string path1 = ".\\Private$\\MSMQDemo1";
            if (MessageQueue.Exists(path1))
            {
                mq1 = new MessageQueue(path1);
                //mq1.GetAllMessages();
                mq1.Purge();
                richTextBox1.AppendText("软件开启" + "\r\n");
            }
            else
            {
                richTextBox1.AppendText("新建了列表" + "\r\n");
                mq1 = MessageQueue.Create(path1);
            }


        }
        private void RecMsg(object obj)
        {
            MessageQueue mq1 = (MessageQueue)obj;
            // Receive message, 同步的Receive方法阻塞当前执行线程，直到一个message可以得到
            while (flag_threadout == 1)
            {
                System.Messaging.Message message = mq1.Receive();

                message.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(string) });

                //richTextBox1.AppendText("消息队列 "+message.Body.ToString());

                String info = message.Body.ToString();
                //richTextBox1.AppendText(info);


                JObject jo = (JObject)JsonConvert.DeserializeObject(info);//调用程序NetonJson，将传过来的string转为json对象
                string typeMsg = jo["type"].ToString();//获取返回信息的类型
                string dataMsg = jo["data"].ToString();//用户的请求是什么

                if (typeMsg.Equals("1"))//串口信息标志
                {
                    if (dataMsg.Equals("1"))
                    {
                        port_isopen = 1;
                        richTextBox1.AppendText(DateTime.Now.ToString() + "串口连接不正常，请重启软件。或插拔串口，重启服务" + "\r\n\r\n");
                        //new Thread(new ParameterizedThreadStart(showBox)).Start("串口连接不正常，请重启软件。或插拔串口，重启服务");//
                    }
                }
                else if (typeMsg.Equals("2"))//数据信息标志
                {
                    data_buffer += dataMsg;//将获取到的数据放入缓存池
                    
                }
                else if (typeMsg.Equals("3"))//"请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试"
                {
                    //MessageBox.Show("数据库连接失败", d["MB_Title"]);
                    new Thread(new ParameterizedThreadStart(showBox)).Start("数据库连接失败");
                }

                //Thread thread_JiaXiMsg = new Thread(JiaXiMsg);//接收串口指令的字符的线程
                //thread_JiaXiMsg.Start();
            }
        }
        private int RecChuanKouMsg(object obj)
        {
            try
            {
                MessageQueue mq1 = (MessageQueue)obj;
                // Receive message, 同步的Receive方法阻塞当前执行线程，直到一个message可以得到
                int i = 2;
                while (true)
                {
                    System.Messaging.Message message = mq1.Receive();
                    message.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(string) });
                    //richTextBox1.AppendText("串口单独接收的信息" + message.Body.ToString());

                    String info = message.Body.ToString();

                    JObject jo = (JObject)JsonConvert.DeserializeObject(info);//调用程序NetonJson，将传过来的string转为json对象
                    string typeMsg = jo["type"].ToString();//获取返回信息的类型
                    string dataMsg = jo["data"].ToString();//用户的请求是什么

                    //new Thread(new ParameterizedThreadStart(showBox)).Start("接收到了消息" + dataMsg);//串口设置已失效，请重新设置
                    if (typeMsg.Equals("1"))
                    {
                        int msg = 0;
                        if (dataMsg.Equals("1"))
                        {
                            msg = 1;
                        }
                        else
                        {
                            msg = 0;
                        }
                        return msg;
                    }


                }
            }catch(Exception ee)
            {
                MessageBox.Show("解析获取串口信息的消息队列出错：" + ee.ToString());
                return 1;
            }
            
    }
        private void WriteMQ(string info)
        {
            try
            {
                System.Messaging.Message message = new System.Messaging.Message();
                message.Body = info.Trim();
                message.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(string) });
                mq.Send(message);
            }catch(Exception ee)
            {
                MessageBox.Show("消息队列信息发送错误：" + ee.ToString());
            }
            
        }
        /// <summary>
        /// 弹出MessageBox，在线程中执行，避免阻塞当前线程
        /// </summary>
        /// <param name="obj"></param>
        private void showBox(object obj)
        {
            string message = (string)obj;
            if (msgBoxNum >= 2000)
            {
                msgBoxNum = 0;
            }
            MyMessageBox MymsgBox = new MyMessageBox(message, (msgBoxNum++).ToString());
            MymsgBox.ShowBox();
        }

        /// <summary>
        /// 加载定时信息。。一开始加载窗体的时候就将所有的定时操作加载出来
        /// </summary>
        /// <param name="obj"></param>
        private void LoadAuto(object obj)
        {
            timer1_mask = 1;
            Auto_list.Clear();
            string sql = "select * from binauto";
            try
            {
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlConn msqc = new MySqlConn();
                MySqlDataReader rd = msqc.getDataFromTable(sql);
                while (rd.Read())
                {
                    OperMsg msg = new OperMsg(rd["BinID"].ToString(), rd["Time"].ToString(), rd["Date"].ToString(), rd["Operation"].ToString(), 0);
                    Auto_list.Add(msg);
                }
                f.change = 0;
                timer1_mask = 0;

                //db.Dr.Close();
                //db.Close();
                rd.Close();
                msqc.Close();
            }
            catch (Exception e) {
                MessageBox.Show("加载定时时间出错");
               
            }
            finally
            {
               
            }
        }

        private void display_noport(object obj)
        {//窗口连接不上串口时，将所有料仓表显示在不在线列表
            if (checkedListBox1.Items.Count != 0)
            {
                checkedListBox1.Items.Clear();
            }
            if (checkedListBox2.Items.Count != 0)
                checkedListBox2.Items.Clear();

            if (comboBox4.Text.Equals(""))
            {
                string sql = "select * from config";
                MySqlConn msqc = new MySqlConn();
                MySqlDataReader rdr = msqc.getDataFromTable(sql);
                while (rdr.Read())
                {
                    comboBox4.Items.Add(rdr["FactoryID"].ToString());

                }
                rdr.Close();
                msqc.Close();
                if (comboBox4.Items.Count > 0)
                {
                    comboBox4.Text = comboBox4.Items[0].ToString();
                }
                else
                {
                    //MessageBox.Show("未保存厂区码", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("未保存厂区码");
                }
            }
            try
            {
                //串口不正常时的内容
                getGroupList();

                string groupname = comboBoxGroup.Text.Trim();



                string sql = "select * from bininfo where Gid = (select Gid from groupinfo where Gname = '" + groupname + "')";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rdr = ms.getDataFromTable(sql);//直接对象引用

                while (rdr.Read())
                {
                    Thread.Sleep(20);
                    checkedListBox2.Items.Remove(rdr["BinName"].ToString());
                    checkedListBox2.Items.Add(rdr["BinName"].ToString());
                }
                SortCheckedList(checkedListBox2);

                rdr.Close();
                ms.Close();
            }
            catch (Exception exc)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");

            }

        }

        /// <summary>
        /// 发送指令函数
        /// </summary>
        /// <param name="obj"></param>
        private void SendIns(object obj)
        {
            while (flag_threadout == 1)
            {


                Thread.Sleep(30);//指令发送间隔
                if (sendIns_queue.Count != 0)
                {

                    Thread.Sleep(20);
                    FacMessage ele = sendIns_queue.Dequeue();
                    if (ele.ProduceTime >= 0)//时间每一秒减减，如果过了这个时间，就只取指令不发指令
                    {
                        serialPort_WriteLine(ele);
                    }
                    else
                    {//当产生时间<0
                        if (ele.ins_answer.Equals("21"))
                        {//并且指条指令是查询指令时，应该将目的指令一并删除
                            aim_ins.Dequeue();
                        }
                    }
                } 
            }
        }

        /// <summary>
        /// 测试数据库中的数据表是否全，如果不全则补全不存在的数据表
        /// </summary>
        /// <returns></returns>
        private string conTosql()
        {
            int sum_table = 8;//一共需要8个表，存在一个就减减..再加上一个由于mqtt的code
            string[] tables = { "bininfo", "binauto", "bindata", "binlog", "config", "server", "user", "mqttcode" };
            string sql = "select table_name from information_schema.tables where table_schema='factory'";//获取数据库Factory中的和数据表
                                                                                                         //DataBase db = new DataBase();
                                                                                                         //db.command.CommandText = sql;
                                                                                                         //db.command.Connection = db.connection;
                                                                                                         //db.Dr = db.command.ExecuteReader();
            MySqlConn ms = new MySqlConn();
            MySqlDataReader rd = ms.getDataFromTable(sql);

            while (rd.Read())
            {
                for (int i = 0; i < tables.Length; i++)
                {
                    if (rd["table_name"].ToString().Equals(tables[i]))//若有对应的表名，说明存在表
                    {
                        tables[i] = "";
                        sum_table--;
                    }
                }
            }
            rd.Close();
            ms.Close();
            //db.Dr.Close();
            //db.Close();
            //MessageBox.Show(""+sum_table);
            if (sum_table == 0)
            {
                return "1";
            }
            else
            {
                string table = "";
                for (int i = 0; i < 8; i++)
                {
                    if (tables[i].Equals("") != true)
                    {
                        table += (tables[i] + "|");
                        //DataBase database = new DataBase();
                        string sql_createT = "";
                        string sql_initdata = "";
                        if (tables[i].Equals("binauto"))
                        {
                            sql_createT = "create table `binauto` (`BinID` int null, `Time` varchar(255), " +
                                "`Date` int, `BinName` varchar(255), `Operation` varchar(255));";
                        }
                        else if (tables[i].Equals("bindata"))
                        {
                            sql_createT = "create table `bindata` (`BinID` int, `Volume` float, `Weight` float, " +
                                "`Temp` float, `Hum` float, `DateTime` varchar(255),`Algorithm` varchar(255),`PrintNum` varchar(255),`Quality` varchar(255),`BackData` varchar(2000) ,`MiDu` float,`Jd` float,`BackAn` varchar(2000));";
                        }
                        else if (tables[i].Equals("bininfo"))
                        {
                            sql_createT = "create table `bininfo` (`BinID` int, `BinName` varchar(255), `Diameter` float, " +
                                "`CylinderH` float, `PyramidH` float, `Density` float,`Margin` float,`BinTop` float,`Wheelbase` float,`Angle` float,`KValue` varchar(255),`Bvalue` varchar(255));";
                        }
                        else if (tables[i].Equals("binlog"))
                        {
                            sql_createT = "create table `binlog` (`Address` int, `Dataytpe` varchar(255),`Data` varchar(255), " +
                                "`Message` varchar(255), `Time` varchar(255));";
                        }
                        else if (tables[i].Equals("config"))
                        {
                            sql_createT = "create table config (DistrictID varchar(255), FactoryID varchar(255));";
                            sql_initdata = "insert into config values('0102', '00001')";

                        }
                        else if (tables[i].Equals("server"))
                        {
                            sql_createT = "create table server (ServerIp varchar(255), ServerPort int, " +
                                "UpdateServ varchar(255), DataServ varchar(255));";
                        }
                        else if (tables[i].Equals("user"))
                        {
                            sql_createT = "create table user (UserName varchar(255), PassWord varchar(255), Admin int);";
                            sql_initdata = "insert into user values('root', '123','1')";
                        }
                        else if (tables[i].Equals("mqttcode"))
                        {
                            sql_createT = "create table mqttcode (MqttCode varchar(255), Code varchar(255));";
                            sql_initdata = "insert into mqttcode values('1', '3700000000')";
                        }
                        MySqlConn ms1 = new MySqlConn();
                        int iRet = ms1.nonSelect(sql_createT);
                        ms1.Close();
                        if (iRet == 1)
                        {
                            richTextBox1.AppendText("建表执行成功");
                        }
                        else
                        {
                            richTextBox1.AppendText("建表执行失败");
                        }

                        if (sql_initdata.Equals("") != true)
                        {
                            //database.command.CommandText = sql_initdata;
                            //database.command.ExecuteNonQuery();
                            MySqlConn ms2 = new MySqlConn();
                            int iRet2 = ms2.nonSelect(sql_initdata);
                            ms2.Close();
                            if (iRet2 == 1) richTextBox1.AppendText("插入数据成功");
                            else richTextBox1.AppendText("插入数据失败");
                        }
                    }
                }
                //MessageBox.Show("数据库表格已完善", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("数据库表格已完善");

                return sum_table.ToString() + "|" + table;
            }
        }


        private void method_file(object obj)
        {//数据库连接不上时，将这个错误信息添加到日志文件中
            string message_error = obj.ToString();
            string path = System.Windows.Forms.Application.StartupPath;
            FileStream fs = new FileStream(path + "\\log.txt", FileMode.Create | FileMode.Append);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine("错误信息是：" + message_error + " 时间是：" + DateTime.Now.ToString());
            sw.Flush();
            sw.Close();
            fs.Close();
        }

        private void getStatus(object sender, System.Timers.ElapsedEventArgs e)
        {//循环获取状态函数，在定时器中实现
            foreach (FacMessage FacInfo in sendIns_queue)
            {//遍历发送指令队列，并将指令的产生时间加一
                FacInfo.ProduceTime--;
            }

            for (int i = 0; i < list_status.Count; i++)
            {
                if (list_status[i].sign_answer == false)
                {//表示未接收到回应
                    list_status[i].life_time--;
                    //Console.WriteLine(list_status[i].ins_num + ": lifetime " + list_status[i].life_time);
                    if (list_status[i].life_time <= 0)
                    {//超时未响应，创建线程处理
                        string msg = list_status[i].message;
                        FacMessage ele = new FacMessage(list_status[i].ins_num, list_status[i].ins_answer,
                            list_status[i].fac_num, list_status[i].sign_answer,
                            list_status[i].life_time, list_status[i].message,
                            list_status[i].instruction, list_status[i].resend - 1, list_status[i].ProduceTime);
                        new Thread(new ParameterizedThreadStart(statusTimeout)).Start(ele);
                        //new Thread(new ParameterizedThreadStart(statusTimeout)).Start(i);
                        /*
                         不能传递下标索引来删除节点的原因：
                         * 当状态链表的最大长度是5时，状态链表最大的索引下标是4
                         * 当下标4传递到线程时，可能list_status[0]处理完成，节点已经删除
                         * 而此时状态链表list_status的长度为4
                         * 再通过list_status.Remove(4)来删除节点，会出现下标索引越界，
                         * 因而会出现异常
                         * 而传递节点后，每次删除节点时，会再互斥锁中遍历节点
                         * 然后确定唯一的符合要求的节点将其删除
                         * 定不会出现下标索引越界问题
                         */
                    }
                }
                else
                {//状态标志为true，表示已经接收到回应并做出了处理，可以删除节点
                    list_mutex.WaitOne();
                    list_status.RemoveAt(i);
                    list_mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// 超时处理函数
        /// </summary>
        /// <param name="index"></param>
        private void statusTimeout(object index)
        {
            try
            {
                FacMessage fac = (FacMessage)index;
                list_mutex.WaitOne();//再删除节点之前加锁
                //Console.WriteLine("i = "+ i+" sum = "+list_status.Count+" Time: "+ DateTime.Now);
                //list_status.RemoveAt(i);
                for (int i = 0; i < list_status.Count; i++)
                {
                    if (fac.fac_num.Equals(list_status[i].fac_num) && fac.ins_answer.Equals(list_status[i].ins_answer))
                    {
                        list_mutex.WaitOne();
                        list_status.RemoveAt(i);
                        list_mutex.ReleaseMutex();
                    }
                }
                list_mutex.ReleaseMutex();//删除节点之后解锁

                int FacExist = 0;//是否是第一次未收到指令
                for (int i = NoAckList.Count - 1; i >= 0; i--)
                {//遍历未收到指令的链表
                    if (NoAckList[i].fac_num.Equals(fac.fac_num))
                    {//如果找到说明不是第一次未接收到指令，
                        FacExist = 1;
                        NoAckList[i].life_time++;

                        if (NoAckList[i].life_time >= 10)
                        {//如果是第十次未接收到指令，将设备设置为离线，删除NoAckList节点，也不用接着查状态获取盘库数据
                            //for (int j = send_ins.Count - 1; j >= 0; j--)
                            //{
                            //    if (send_ins[j].fac_num.Equals(NoAckList[i].fac_num.PadLeft(2, '0')))
                            //    {//如果这个料仓在正在盘库链表中，则停止发送查询结果指令并删除
                            //        send_ins.RemoveAt(j);
                            //        break;
                            //    }

                            //}
                            //for (int j = CalcVol_list.Count - 1; i >= 0; i--)
                            //{//盘库进度显示列表删除
                            //    if (NoAckList[i].fac_num.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                            //    {
                            //        CalcVol_list.RemoveAt(i);
                            //        break;
                            //    }
                            //}
                            checkedListBox1.Items.Remove(getName(NoAckList[i].fac_num));
                            //checkedListBox2.Items.Remove(getName(NoAckList[i].fac_num));
                            //checkedListBox2.Items.Add(getName(NoAckList[i].fac_num));
                            if (checkedListBox2.Items.Contains(getName(NoAckList[i].fac_num)) == false)
                            {
                                checkedListBox2.Items.Add(getName(NoAckList[i].fac_num));
                            }
                            SortCheckedList(checkedListBox2);
                            //checkedListBox1.Sorted = true;

                            NoAckList.RemoveAt(i);
                        }
                    }
                }
                if (0 == FacExist)
                {
                    NoAckList.Add(new FacMessage(fac.fac_num, 1, 0));
                }


                if (fac.ins_answer.Equals("01"))
                {//如果01号指令没有回应，说明料仓不存在或不在线，将其添加到不在线列表中

                    string fac_name = getName(fac.fac_num);
                    if (fac_name.Equals("") == false)
                    {
                        //先remove的目的时防止料仓重复
                        //Invoke(new MethodInvoker(delegate {
                        //    checkedListBox1.Items.Remove(fac_name);
                        //    checkedListBox2.Items.Remove(fac_name);
                        //    checkedListBox2.Items.Add(fac_name);
                        //}));
                    }
                    else
                    {
                        //MessageBox.Show("请输入有效的料仓编号", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请输入有效的料仓编号");

                    }

                }
                else if (fac.ins_answer.Equals("11"))
                {//如果查询温湿度指令没有恢复，就把查询温湿度按钮变为可用
                    查询温度ToolStripMenuItem.Enabled = true;
                }
                else if (fac.ins_answer.Equals("21"))
                {//表示查询状态指令没有回应，需要重发

                    if (fac.resend > 0)
                    {
                        aim_ins.Enqueue(oper_ins.Dequeue());
                        //richTextBox1.AppendText(" aim_ins.Enqueue(oper_ins.Dequeue()); \r\n\r\n");
                        sendIns_queue.Enqueue(fac);
                    }
                    else
                    {
                        oper_ins.Clear();
                        //richTextBox1.AppendText(" oper_ins.Clear();111111; \r\n\r\n");
                    }
                        

                }
                else if (fac.ins_answer.Equals("13"))
                {
                    if (fac.resend > 0)
                        sendIns_queue.Enqueue(fac);
                }

                string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                //DataBase db = new DataBase();

                string sql = "insert into binlog values('" + fac.fac_num + "', '回应超时', '" + fac.instruction + "', '" + fac.message + "', '" + time + "')";
                MySqlConn ms = new MySqlConn();
                int isR = ms.nonSelect(sql);
                ms.Close();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.command.ExecuteNonQuery();
                //db.Close();

            }
            catch (SqlException se)
            {
                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));//数据库异常存入文件
                thread_file.Start(se.ToString());
                //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
            }
        }
        private void InitDataSet1()
        {
           
            //判断有多少页
            if(checkedListBox2.CheckedItems.Count%6 == 0)
            {
                chartCount = (checkedListBox2.CheckedItems.Count) / 6;
            }
            else
            {
                chartCount = ((checkedListBox2.CheckedItems.Count) / 6) + 1;
            }

            chartCurrent = 1;//当前页数从1开始

            //MessageBox.Show(" chartCount" + chartCount);

        }


        private void InitDataSet()
        {
            pageSize = 18;      //设置页面行数
            nMax = dtInfo.Rows.Count;

            pageCount = (nMax / pageSize);    //计算出总页数

            if ((nMax % pageSize) > 0) pageCount++;

            pageCurrent = 1;    //当前页数从1开始
            nCurrent = 0;       //当前记录数从0开始

            LoadData();
        }
        private void LoadData()
        {
            int nStartPos = 0;   //当前页面开始记录行
            int nEndPos = 0;     //当前页面结束记录行

            DataTable dtTemp = dtInfo.Clone();   //克隆DataTable结构框架

            if (pageCurrent == pageCount)
                nEndPos = nMax;
            else
                nEndPos = pageSize * pageCurrent;

            nStartPos = nCurrent;

            lblPageCount.Text = pageCount.ToString();
            txtCurrentPage.Text = Convert.ToString(pageCurrent);

            //从元数据源复制记录行
            for (int i = nStartPos; i < nEndPos; i++)
            {
                if (dtInfo.Rows.Count > 0)
                {//判读表中是否有内容
                    dtTemp.ImportRow(dtInfo.Rows[i]);
                    nCurrent++;

                }

            }
            bdsInfo.DataSource = dtTemp;
            bdnInfo.BindingSource = bdsInfo;
            dataGridView1.DataSource = bdsInfo;
        }

        /// <summary>
        /// 显示料仓名称
        /// </summary>
        /// <param name="obj"></param>
        private void display(object obj)
        {//主要功能是向中控发送每一个料仓的查询指令
            Thread.Sleep(30);
            string factory = "";

            if (checkedListBox1.Items.Count != 0)
            {
                checkedListBox1.Items.Clear();
            }
            if (checkedListBox2.Items.Count != 0)
                checkedListBox2.Items.Clear();

            factory = comboBox4.Text;

            if (factory.Equals(""))
            {
                string sql = "select * from config";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rdr = ms.getDataFromTable(sql);
                while (rdr.Read())
                {
                    Invoke(new MethodInvoker(delegate
                    {
                        comboBox4.Items.Add(rdr["FactoryID"].ToString());
                    }));

                }
                rdr.Close();
                ms.Close();

                if (comboBox4.Items.Count > 0)
                {
                    Invoke(new MethodInvoker(delegate
                    {
                        comboBox4.Text = comboBox4.Items[0].ToString();
                        factory = comboBox4.Text;
                    }));
                }
                else
                {
                    //MessageBox.Show("未保存厂区码", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("未保存厂区码");

                }
            }

            if (factory.Equals("") != true)
            {
                string sql = "select * from bininfo";
                //DataBase db;
                Queue<FacMessage> send_queue = new Queue<FacMessage>();
                try
                {
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader r = ms.getDataFromTable(sql);

                    while (r.Read())
                    {//显示列表，向存在于数据库中所有的料仓发送"00"号指令，检测料仓是否存在
                        Thread.Sleep(10);
                        string id = r["BinID"].ToString();
                        string data = "";
                        Invoke(new MethodInvoker(delegate
                        {
                            data = Data.Data(comboBox4.Text, id, "00", "0000");
                        }));
                        send_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data, s_Produce));

                        //serialPort_WriteLine(data, new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data));
                    }
                    r.Close();
                    ms.Close();
                }
                catch (Exception exc)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(exc.ToString());
                    //MessageBox.Show("请检查数据库设置", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                }
                FacMessage ele;
                while (send_queue.Count != 0)
                {
                    ele = send_queue.Dequeue();
                    sendIns_queue.Enqueue(ele);
                }
            }


        }

        /// <summary>
        /// 刚开始打开界面时，将所有料仓添加到不在线列表
        /// </summary>
        /// <param name="obj"></param>
        private void OpenMainForm(object obj)
        {//主要功能是向中控发送每一个料仓的查询指令
            Thread.Sleep(30);
            string factory = "";

            if (checkedListBox1.Items.Count != 0)
            {
                checkedListBox1.Items.Clear();
            }
            if (checkedListBox2.Items.Count != 0)
                checkedListBox2.Items.Clear();

            factory = comboBox4.Text;
            try
            {
                if (factory.Equals(""))
                {
                    string sql = "select * from config";
                    MySqlConn msc2 = new MySqlConn();
                    MySqlDataReader rd = msc2.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        Invoke(new MethodInvoker(delegate
                        {
                            comboBox4.Items.Add(rd["FactoryID"].ToString());
                        }));

                    }
                    rd.Close();
                    msc2.Close();
                    if (comboBox4.Items.Count > 0)
                    {
                        Invoke(new MethodInvoker(delegate
                        {
                            comboBox4.Text = comboBox4.Items[0].ToString();
                            factory = comboBox4.Text;
                        }));
                    }
                    else
                    {
                        //MessageBox.Show("未保存厂区码", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("未保存厂区码");
                    }
                }
            }catch(Exception ee)
            {
                MessageBox.Show("查询厂区码出错：" + ee.ToString());
            }
            
            //MessageBox.Show("厂区码：  " + factory);
            if (factory.Equals("") != true)
            {
                //串口正常时查询分组
                getGroupList();//查询分组


                string groupname = comboBoxGroup.Text;

                //MessageBox.Show("*************" + groupname);
                string sql = "select * from bininfo where Gid = (select Gid from groupinfo where Gname = '" + groupname + "')";
                //DataBase db;
                Queue<FacMessage> send_queue = new Queue<FacMessage>();
                //MessageBox.Show("分组1111111111111");
                try
                {
                    MySqlConn msc2 = new MySqlConn();
                    MySqlDataReader rd = msc2.getDataFromTable(sql);
   

                    while (rd.Read())
                    {//显示列表，向存在于数据库中所有的料仓发送"00"号指令，检测料仓是否存在
                        Thread.Sleep(10);
                        string id = rd["BinID"].ToString();
                        checkedListBox2.Items.Remove(getName(id));
                        checkedListBox2.Items.Add(getName(id));//在一个方法中套用一个方法访问数据库需要重新新建连接
                        //serialPort_WriteLine(data, new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data));
                    }
                    //不在线料仓排序
                    //MessageBox.Show("分组22222222222222");
                    SortCheckedList(checkedListBox2);
                    rd.Close();
                    msc2.Close();
                }
                catch (Exception exc)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(exc.ToString());
                    //MessageBox.Show("请检查数据库设置", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置2" + exc.ToString());
                }
                // 向所有料仓发送查询在线指令
                OnlineCheak();

            }


        }

        /// <summary>
        /// 管理员密码修改功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param> 
        private void 管理员密码修改ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            groupBox_changepass.Visible = true;
            groupBox_adduser.Visible = false;
            groupBox_deleteuser.Visible = false;
            groupBox_init.Visible = false;
            textBox1.Text = "";
            textBox6.Text = "";
            textBox7.Text = "";
        }

        /// <summary>
        /// 编辑分组中的取消按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param> 
        private void buttonCanl_Click(object sender, EventArgs e)
        {
            groupBox_changeGroup.Visible = false;

            comboBoxSetGroup.Items.Clear();
            groupNameText.Text = "";
        }

        /// <summary>
        /// 编辑分组
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param> 
        private void groupSet_Click(object sender, EventArgs e)
        {
            groupBox_changeGroup.Visible = true;
            groupBox_changepass.Visible = false;
            groupBox_adduser.Visible = false;
            groupBox_deleteuser.Visible = false;
            groupBox_init.Visible = false;

            comboBoxSetGroup.Items.Clear();
            groupNameText.Text = "";


            try
            {
                //添加料仓设备
                //1、查询分组，将分组信息添加到comboboxGroup中
                string sqlGroup = "select * from groupinfo where gstate = '1'";

                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sqlGroup);
                while (rdGroup.Read())
                {
                    comboBoxSetGroup.Items.Add(rdGroup["Gname"].ToString());
                }
                rdGroup.Close();
                msc1.Close();
                if (comboBoxSetGroup.Items.Count > 0)
                {
                    comboBoxSetGroup.Text = comboBoxSetGroup.Items[0].ToString();
                }
            }
            catch (Exception ee)
            {
                show("修改分组时。查询料仓分组报错。 错误：" + ee.ToString());
            }

        }

        /// <summary>
        /// 添加分组功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonGroupAdd_Click(object sender, EventArgs e)
        {
            try
            {
                string name = groupNameText.Text.Trim();
                show("添加分组的名字为：" + name);
                if (name.Equals(""))
                {
                    show("分组名不能为空");
                    return;
                }

                if (IsGroupNameRe(name))
                {
                    show("组名重复！！！！！！请输入新组名");
                    return;
                }
                string sql = "select max(gid) as gid  from groupinfo ";
                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sql);
                if (rdGroup.Read())
                {
                    string maxid = rdGroup["gid"].ToString();
                    int id = int.Parse(maxid) + 1;
                    MySqlConn msc2 = new MySqlConn();
                    string addsql = "insert into groupinfo(Gid,Gname,Gstate) values(" + id + " ,'" + name + "' , '1') ";
                    int res = msc2.nonSelect(addsql);

                    if (res == 1)
                    {
                        show("添加分组成功");
                        //1、更改主页comboboxGroup
                        comboBoxGroup.Items.Add(name);

                        this.groupBox_changeGroup.Visible = false;


                    }
                    msc2.Close();
                }
                rdGroup.Close();
                msc1.Close();
            }
            catch (Exception ee)
            {
                show("添加分组出错。错误：" + ee.ToString());
            }



        }

        /// <summary>
        /// 分组修改名字
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConform_Click(object sender, EventArgs e)
        {
            try
            {
                string name = comboBoxSetGroup.Text;
                string newname = groupNameText.Text.Trim();

                if (newname.Equals(""))
                {
                    show("分组名不能为空");
                    return;
                }

                if (IsGroupNameRe(newname))
                {
                    show("组名重复！！！！！！请输入新组名");
                    return;
                }

                show("要修改的名字是：" + name);
                string sql = "update  groupinfo set Gname = '" + newname + "'  where Gname = '" + name + "' ";
                MySqlConn msc1 = new MySqlConn();
                int res = msc1.nonSelect(sql);
                if (res == 1)
                {
                    show("修改分组成功");
                    getGroupList();
                    this.groupBox_changeGroup.Visible = false;
                }
                msc1.Close();
            }
            catch (Exception ee)
            {
                show("添加分组出错。错误：" + ee.ToString());
            }

        }

        /// <summary>
        /// 添加分组删除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void groupDel_Click(object sender, EventArgs e)
        {
            try
            {
                string name = comboBoxSetGroup.Text.Trim();

                string sql1 = "delete from bininfo where Gid  =(select Gid from groupinfo where  Gname = '" + name + "' and Gstate = 1)";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                MySqlConn ms = new MySqlConn();
                int isR = ms.nonSelect(sql1);
                ms.Close();
                if (isR > 0)
                {
                    show("料仓与分组关系移除");
                }
                else
                {
                    show("移除料仓与分组关系出错");
                }

                show("要删除的名字是：" + name);
                string sql = "update  groupinfo set Gstate = '0'  where Gname = '" + name + "' and Gstate = 1";
                MySqlConn msc1 = new MySqlConn();
                int res = msc1.nonSelect(sql);
                if (res == 1)
                {
                    show("删除分组成功");


                    getGroupList();

                    this.groupBox_changeGroup.Visible = false;


                }
                msc1.Close();
            }
            catch (Exception ee)
            {
                show("添加分组出错。错误：" + ee.ToString());
            }

        }

        /// <summary>
        /// 取消修改按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button11_Click(object sender, EventArgs e)
        {
            groupBox_changepass.Visible = false;
        }

        /// <summary>
        /// 确认修改按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button10_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Equals(""))
            {
                //MessageBox.Show("请输入原密码", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请输入原密码");
            }
            else if (textBox6.Text.Equals(""))
            {
                MessageBox.Show("请输入新密码", "提示");
            }
            else if (textBox7.Text.Equals(""))
            {
                MessageBox.Show("请输入确认密码", "提示");
            }
            else
            {
                if (textBox6.Text.Equals(textBox7.Text) == false)
                {
                    MessageBox.Show("两次输入的密码不一致", "提示");
                }
                else
                {
                    try
                    {
                        string sql_find = "select * from user where UserName = '" + curr_user.name + "'";
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql_find);
                        string old_passwd = "";
                        while (rd.Read())
                        {
                            old_passwd = rd["PassWord"].ToString();
                            break;
                        }
                        rd.Close();
                        ms.Close();
                        if (old_passwd.Equals(textBox1.Text))
                        {
                            string sql = "update user set PassWord='" + textBox6.Text + "' where UserName ='" + curr_user.name + "';";
                            MySqlConn ms1 = new MySqlConn();
                            int res = ms1.nonSelect(sql);
                            ms1.Close();
                            if (res > 0)
                            {
                                MessageBox.Show("修改密码成功", "提示");
                            }
                            else
                            {
                                MessageBox.Show("修改密码失败", "提示");
                            }
                        }
                        else
                        {
                            MessageBox.Show("原密码输入错误", "提示");
                        }

                    }
                    catch (Exception exc)
                    {
                        //MessageBox.Show("请检查数据库连接设置", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                    }
                }
            }
        }

        /// <summary>
        /// 用户添加功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 用户添加ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button8.PerformClick();
            groupBox_changepass.Visible = false;
            groupBox_adduser.Visible = true;
            groupBox_deleteuser.Visible = false;
            groupBox_init.Visible = false;
            textBox8.Text = "";
            textBox9.Text = "";
            textBox10.Text = "";
        }

        /// <summary>
        /// 取消添加用户按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button13_Click(object sender, EventArgs e)
        {
            groupBox_adduser.Visible = false;
        }

        /// <summary>
        /// 确认添加用户按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button12_Click(object sender, EventArgs e)
        {
            if (textBox8.Text.Equals(""))
            {
                MessageBox.Show("请输入用户名", "提示");
            }
            else if (textBox9.Text.Equals(""))
            {
                MessageBox.Show("请输入密码", "提示");
            }
            else if (textBox10.Text.Equals(""))
            {
                MessageBox.Show("请确认密码", "提示");
            }
            else if (textBox10.Text.Equals(textBox9.Text) != true)
            {
                MessageBox.Show("两次输入的密码不一致", "提示");
            }
            else
            {
                try
                {
                    string sql = "select * from user";
                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rd = ms.getDataFromTable(sql);
                    bool isExit = false;
                    while (rd.Read())
                    {
                        //richTextBox1.AppendText(db.Dr["UserName"].ToString() + "   " + textBox8.Text + "\r\n");
                        if (rd["UserName"].ToString().Equals(textBox8.Text.Trim()))
                        {
                            isExit = true;
                            break;
                        }
                    }
                    rd.Close();
                    ms.Close();
                    if (isExit)
                    {
                        MessageBox.Show("用户已存在", "提示");
                    }
                    else
                    {
                        sql = "insert into user values('" + textBox8.Text.Trim() + "','" + textBox9.Text + "',0);";
                        MySqlConn ms2 = new MySqlConn();
                        int res = ms2.nonSelect(sql);
                        ms2.Close();
                        if (res > 0)
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "添加用户成功\r\n\r\n");
                        }
                        else
                        {
                            MessageBox.Show("添加失败", "提示");
                        }
                    }
                }
                catch (Exception exc)
                {
                    //MessageBox.Show("请检查数据库是否创建好", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                }


            }
        }

        /// <summary>
        /// 删除用户功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 用户删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button8.PerformClick();
            groupBox_deleteuser.Visible = true;
            groupBox_changepass.Visible = false;
            groupBox_adduser.Visible = false;
            groupBox_init.Visible = false;
            textBox12.Text = "";
            comboBox7.Items.Clear();
            try
            {
                string sql = "select * from user";
                //DataBase db = new DataBase();

                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                while (rd.Read())
                {
                    if (rd["UserName"].ToString().Equals("root"))
                        continue;
                    else
                    {
                        comboBox7.Items.Add(rd["UserName"].ToString());
                    }
                }
                rd.Close();
                ms.Close();
            }
            catch (Exception exc)
            {
                //MessageBox.Show("数据库连接失败", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("数据库连接失败");
            }
        }

        /// <summary>
        /// 取消删除用户功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button15_Click(object sender, EventArgs e)
        {
            groupBox_deleteuser.Visible = false;
        }

        /// <summary>
        /// 确认删除用户按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button14_Click(object sender, EventArgs e)
        {
            if (comboBox7.Text.Equals(""))
            {
                MessageBox.Show("请输入用户名", "提示");
            }
            else if (textBox12.Text.Equals(""))
            {
                MessageBox.Show("请输入管理员密码", "提示");
            }
            else
            {
                MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                DialogResult dr = MessageBox.Show("确认要删除用户 " + comboBox7.Text + " 吗？", "提示", messButton);
                if (dr == DialogResult.OK)
                {
                    try
                    {
                        string sql1 = "select * from user where UserName = 'root';";
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql1);
                        string str = "";
                        while (rd.Read())
                        {
                            str = rd["PassWord"].ToString();
                        }
                        rd.Close();
                        ms.Close();

                        if (str.Equals(textBox12.Text))
                        {
                            MySqlConn ms1 = new MySqlConn();
                            string sql = "delete [user] where UserName='" + comboBox7.Text + "';";
                            int res = ms1.nonSelect(sql);
                            ms1.Close();
                            if (res > 0)
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "删除用户成功\r\n\r\n");
                                groupBox_deleteuser.Visible = false;

                            }
                            else
                            {
                                MessageBox.Show("用户不存在", "提示");
                            }
                        }
                        else
                        {
                            MessageBox.Show("管理员密码错误", "提示");
                        }
                    }
                    catch (Exception exc)
                    {
                        //MessageBox.Show("请检查数据库是否创建好", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }


            }
        }

        /// <summary>
        /// 用户初始化功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 用户初始化ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button8.PerformClick();
            groupBox_deleteuser.Visible = false;
            groupBox_changepass.Visible = false;
            groupBox_adduser.Visible = false;
            groupBox_init.Visible = true;
            textBox14.Text = "";
            comboBox8.Items.Clear();
            try
            {
                string sql = "select * from user";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlConn msq = new MySqlConn();
                MySqlDataReader rd = msq.getDataFromTable(sql);
                while (rd.Read())
                {
                    if (rd["UserName"].ToString().Equals("root"))
                        continue;
                    comboBox8.Items.Add(rd["UserName"].ToString());
                }
                rd.Close();
                msq.Close();
            }
            catch (Exception exc)
            {
                //MessageBox.Show("请检查数据库设置", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
            }
        }

        /// <summary>
        /// 取消用户初始化按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button17_Click(object sender, EventArgs e)
        {
            groupBox_init.Visible = false;
        }

        /// <summary>
        /// 确认用户初始化按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button16_Click(object sender, EventArgs e)
        {
            if (comboBox8.Text.Equals(""))
            {
                MessageBox.Show("请输入用户名", "提示");
            }
            else if (textBox14.Text.Equals(""))
            {
                MessageBox.Show("请输入密码", "提示");
            }
            else
            {
                try
                {

                    string sql = "update user set PassWord='" + textBox14.Text + "' where UserName='" + comboBox8.Text.Trim() + "';";
                    MySqlConn ms = new MySqlConn();
                    int res = ms.nonSelect(sql);
                    ms.Close();
                    if (res > 0)
                    {
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "用户初始化成功\r\n\r\n");
                    }
                    else
                    {
                        MessageBox.Show("用户初始化失败", "提示");
                    }
                }
                catch (Exception exc)
                {
                    //MessageBox.Show("请检查数据库设置", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                }
            }

        }

        /// <summary>
        /// 串口发送指令函数
        /// </summary>
        /// <param name="facmsg"></param>
        private void serialPort_WriteLine(object facmsg)
        {
           
            FacMessage ele = (FacMessage)facmsg;
            //thread.Start(ins_answer);
            while (flag_threadout == 1)
            {//没有点击退出按钮
                Thread.Sleep(30);
                if (port_mask == 0)
                {//发出类似于盘库这样的操作指令时，需要先将发送函数阻塞，来发送盘库指令
                    if (oper_ins.Count == 0 && list_status.Count == 0)
                    {//操作指令队列为空（表示操作指令已经取出并发出）并且状态链表为空（所有发出去的指令需要都回应了或者超时处理了）
                        try
                        {
                            if (aim_ins.Count != 0)
                            {
                                if (ele.ins_answer.Equals("21") )
                                {
                                    oper_ins.Enqueue(aim_ins.Dequeue());
                                    //richTextBox1.AppendText("添加oper指令\r\n\r\n");
                                }
                            }
                            //将serialPort1.WriteLine注释掉，全部换成WriteMQ
                            //serialPort1.WriteLine(ele.instruction);
                            
                            //将要发送的指令,通过消息队列发送给mqtt发
                            JsonMsg js = new JsonMsg("2", ele.instruction);
                            string sendMqttInfo = JsonConvert.SerializeObject(js);
                            WriteMQ(sendMqttInfo);


                            ele.life_time = 2;
                            list_status.Add(ele);

                        }
                        catch (Exception exc)
                        {
                            if (ele.ins_answer.Equals("21"))
                            {
                                oper_ins.Clear();
                                //richTextBox1.AppendText("添加oper_ins.Clear();指令\r\n\r\n");
                                //oper_ins.Enqueue(aim_ins.Dequeue());
                            }
                            //MessageBox.Show("", "提示");
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试");
                        }
                        break;//发送指令后退出循环，指令一定会发出，因为有超时函数来清空状态链表和准备发送指令队列
                    }

                }

            }
        }



        /// <summary>
        /// 串口接收到数据功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //try
            //{
            //    int bytecount = serialPort1.BytesToRead;

            //    byte[] readBuffer = new byte[bytecount];
            //    serialPort1.Read(readBuffer, 0, readBuffer.Length);
            //    string readstr = Encoding.UTF8.GetString(readBuffer);
            //    //richTextBox1.AppendText(readstr+"\r\n");

            //    cirQueue.In(readstr);
            //    data_buffer += cirQueue.Out();
            //    cirQueue.Clear();

            //}
            //catch (Exception exc)
            //{
            //    richTextBox1.AppendText(exc.ToString() + "\r\n");
            //}
        }

        private string data_buffer = "";//从循环队列中取出的数据先存入缓冲区中
        private string data_take = "";//从数据缓冲区取出数据报进行处理
        /// <summary>
        /// 从循环队列中获取数据
        /// </summary>
        /// <param name="obj"></param>
        private void takeData(object obj)
        {
            try
            {
                while (flag_threadout == 1)
                {
                    //加sleep
                    Thread.Sleep(1000);

                    while (data_buffer.Equals("") != true)
                    {
                        int i = 0;//i记录出现":"的位置
                        for (i = 0; i < data_buffer.Length; i++)
                        {
                            if (data_buffer[i] == ':')
                                break;
                        }
                        int j = 0;//j记录出现"\n"的位置
                        for (j = i; j < data_buffer.Length; j++)
                        {
                            if (data_buffer[j] == '\n')
                            {
                                data_take = data_buffer.Substring(i, j - i + 1);//线程不断循环。当出现换行符时，说明一条指令发完了，就可以解析这个指令了

                                //richTextBox1.AppendText("获取到的一整条数据是！！" + data_take);
                                new Thread(new ParameterizedThreadStart(trans)).Start(data_take);


                                data_buffer = data_buffer.Remove(i, data_take.Length);
                                data_take = "";
                                break;
                            }
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                //richTextBox1.AppendText(exc.ToString()+"\r\n");
            }
        }
        /// <summary>
        /// 根据料仓编号（地址）获取料仓名称
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string getName(string id)
        {
            if (SqlConnect == 1)
            {
                string name = "";
                string sql = "select * from bininfo where BinID = " + id;

                try
                {
                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn msc2 = new MySqlConn();
                    MySqlDataReader rd = msc2.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        name = rd["BinName"].ToString();
                    }
                    rd.Close();
                    return name;
                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    return "";
                }

            }
            else
            {
                //MessageBox.Show("请检查数据库是否创建好", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
 
                return "";
            }

        }



        /// <summary>
        /// 对解析出来的指令进行操作
        /// </summary>
        /// <param name="obj"></param>
        private void trans(object obj)
        {


            try
            {
                string ins = obj.ToString();

                //show("获取到的指令=" + ins);
                string str = Data.decoding(ins);





                if (str.Length <= 1)
                {
                    return;
                }
                //richTextBox1.AppendText("获取到的s:" + str +"\r\n");
                string[] s = str.Split(' ');
                int equip = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);//料仓地址的十进制表示
                //show("s[2]****" + s[2]);
                string data = s[3];
                if (s[0].Equals(comboBox4.Text) != true)
                {
                    //MessageBox.Show("收到非本厂区数据，请更换通信频道", "提示");
                    new Thread(new ParameterizedThreadStart(showBox)).Start("收到非本厂区数据，请更换通信频道");
                    return;
                }

                new Thread(new ParameterizedThreadStart(setSign)).Start(equip.ToString() + "+" + s[2]);
                for (int i = NoAckList.Count - 1; i >= 0; i--)
                {
                    if (NoAckList[i].fac_num.PadLeft(2, '0').Equals(equip.ToString().PadLeft(2, '0')))
                    {
                        NoAckList.RemoveAt(i);
                        break;
                    }
                }
      
       
                //接收到指令后， 新建一个线程来处理状态链表，将应答标志改成true
                //richTextBox1.AppendText(equip.ToString() + "+" + s[2]+"\r\n");
                if (s[2].Equals("01"))
                {//回应仓库参数,添加料仓后回应

                  

                    if (SqlConnect == 1)
                    {
                        
                        adddress = s[1];//接收到查询指令,这个地址记录
                        //分别表示直径， 仓筒高度， 下锥高度， 物料密度
                        string Diameter = "", CylinderH = "", PyramidH = "", Density = "";
                        Diameter = data.Substring(0, 4);
                        CylinderH = data.Substring(4, 4);
                        PyramidH = data.Substring(8, 4);
                        Density = data.Substring(12, 4);
                        float diameter = float.Parse(Diameter);
                        float cylinderH = float.Parse(CylinderH);
                        float pyramidH = float.Parse(PyramidH);
                        float density = float.Parse(Density);
                        float diameterInSql = 0;
                        float cylinderHInSql = 0;
                        float pyramidHInsSql = 0;
                        float densityInSql = 0;

                        string addr_eq = "";//保存在数据库中的设备地址
                        string sql = "select * from bininfo where BinID=" + equip.ToString().PadLeft(2, '0');
                        try
                        {//检测数据库是否可以连接
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                addr_eq = rd["BinID"].ToString();
                                diameterInSql = float.Parse(rd["Diameter"].ToString());
                                cylinderHInSql = float.Parse(rd["CylinderH"].ToString());
                                pyramidHInsSql = float.Parse(rd["PyramidH"].ToString());
                                densityInSql = float.Parse(rd["Density"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            //conn.Close();
                            if (addr_eq.Length == 0)
                            {//表示数据库中没有这个料仓，需要向数据库中添加


                                string groupname = comboBoxGroup.Text;
                                string gid = "";

                                string sqlgid = "select Gid from groupinfo where Gname = '" + groupname + "'";
                                MySqlConn ms2 = new MySqlConn();
                                MySqlDataReader rdr2 = ms2.getDataFromTable(sqlgid);//直接对象引用

                                while (rdr2.Read())
                                {
                                    gid = rdr2["Gid"].ToString();
                                }
                                rdr2.Close();
                                ms2.Close();


                                //sql = "insert into bininfo (BinID, BinName, Diameter, CylinderH, PyramidH, Density) values(" + equip.ToString().PadLeft(2, '0') + ", " + equip.ToString().PadLeft(2, '0') + ", " + (diameter / 100).ToString() + ", " + (cylinderH / 100).ToString() + ", " + (pyramidH / 100).ToString() + ", " + (density / 1000).ToString() + ")";
                                sql = "insert into bininfo (BinID, BinName, Diameter, CylinderH, PyramidH, Density,Gid) values(" + equip.ToString().PadLeft(2, '0') + ", " + equip.ToString().PadLeft(2, '0') + ", " + (diameter / 100).ToString() + ", " + (cylinderH / 100).ToString() + ", " + (pyramidH / 100).ToString() + ", " + (density / 1000).ToString() + "," + gid + ")";
                                MySqlConn ms1 = new MySqlConn();
                                int res = ms1.nonSelect(sql);
                                ms1.Close();

                                if (res > 0)
                                {

                                    Invoke(new MethodInvoker(delegate
                                    {
                                        //先移除料仓，目的是避免料仓重复显示，
                                        //Remove在移除不存在的项时，不发生任何操作和异常
                                        //checkedListBox1.Items.Remove(getName(equip.ToString().PadLeft(2, '0')));
                                        checkedListBox2.Items.Remove(getName(equip.ToString().PadLeft(2, '0')));

                                        if (checkedListBox1.Items.Contains(getName(equip.ToString().PadLeft(2, '0'))) == false)
                                        {
                                            if (getGname(equip.ToString()).Equals(comboBoxGroup.Text))
                                            {//保证分组和料仓时关联的
                                                checkedListBox1.Items.Add(getName(equip.ToString().PadLeft(2, '0')));
                                            }


                                        }
                                        SortCheckedList(checkedListBox1);
                                    }));
                                }
                            }
                            else
                            {//如果有这个料仓，就设置参数,并在在线列表中显示
                                string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                                //if (!(diameter / 100).ToString().Equals(diameterInSql.ToString()))
                                //{//如果发现有不相等的情况，记录下时间
                                //    string saveMsg = diameterInSql.ToString() + "-->" + (diameter / 100).ToString();
                                //    DataBase dbSaveLog = new DataBase();
                                //    string sqlSaveLog = "insert into [binlog] values('" + equip.ToString() + "', '仓筒直径被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                //    dbSaveLog.command.CommandText = sqlSaveLog;
                                //    dbSaveLog.command.Connection = dbSaveLog.connection;
                                //    dbSaveLog.command.ExecuteNonQuery();
                                //    db.Close();
                                //}
                                //if (!(((cylinderH / 100).ToString()).Equals(cylinderHInSql.ToString())))
                                //{
                                //    string saveMsg = cylinderHInSql.ToString() + "-->" + (cylinderH / 100).ToString() + ".";
                                //    DataBase dbSaveLog = new DataBase();
                                //    string sqlSaveLog = "insert into [binlog] values('" + equip.ToString() + "', '仓筒高度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                //    dbSaveLog.command.CommandText = sqlSaveLog;
                                //    dbSaveLog.command.Connection = dbSaveLog.connection;
                                //    dbSaveLog.command.ExecuteNonQuery();
                                //    db.Close();
                                //}
                                //if (!(((pyramidH / 100).ToString()).Equals(pyramidHInsSql.ToString())))
                                //{
                                //    string saveMsg = pyramidHInsSql.ToString() + "-->" + (pyramidH / 100).ToString();
                                //    DataBase dbSaveLog = new DataBase();
                                //    string sqlSaveLog = "insert into [binlog] values('" + equip.ToString() + "', '下锥高度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                //    dbSaveLog.command.CommandText = sqlSaveLog;
                                //    dbSaveLog.command.Connection = dbSaveLog.connection;
                                //    dbSaveLog.command.ExecuteNonQuery();
                                //    db.Close();
                                //}
                                //if (!((density / 1000).ToString()).Equals(densityInSql.ToString()))
                                //{
                                //    string saveMsg = densityInSql.ToString() + "-->" + (density / 1000).ToString();
                                //    DataBase dbSaveLog = new DataBase();
                                //    string sqlSaveLog = "insert into [binlog] values('" + equip.ToString() + "', '物料密度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                //    dbSaveLog.command.CommandText = sqlSaveLog;
                                //    dbSaveLog.command.Connection = dbSaveLog.connection;
                                //    dbSaveLog.command.ExecuteNonQuery();
                                //    db.Close();
                                //}
                                //向数据库修改参数


                                parameter("Diameter", (diameter / 100).ToString(), equip.ToString().PadLeft(2, '0'));
                                parameter("CylinderH", (cylinderH / 100).ToString(), equip.ToString().PadLeft(2, '0'));
                                parameter("PyramidH", (pyramidH / 100).ToString(), equip.ToString().PadLeft(2, '0'));
                                parameter("Density", (density / 1000).ToString(), equip.ToString().PadLeft(2, '0'));

                                Invoke(new MethodInvoker(delegate
                                {
                                    //先移除料仓，目的是避免料仓重复显示，
                                    //Remove在移除不存在的项时，不发生任何操作和异常
                                    //checkedListBox1.Items.Remove(getName(equip.ToString().PadLeft(2, '0')));
                                    
                                    checkedListBox2.Items.Remove(getName(equip.ToString().PadLeft(2, '0')));
                                    if (checkedListBox1.Items.Contains(getName(equip.ToString().PadLeft(2, '0'))) == false)
                                    {
                                        if (getGname(equip.ToString()).Equals(comboBoxGroup.Text))//保证显示的料仓和分组时关联的
                                        {
                                            checkedListBox1.Items.Add(getName(equip.ToString().PadLeft(2, '0')));
                                        }


                                    }
                                    //SortCheckedList(checkedListBox1);

                                }));
                                //发送边距，顶高，轴距查询指令！！！！！！！！！！！！
                                //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "回复获取回复边距，顶高，轴距参数\r\n\r\n");
                                string d = Data.Data(comboBox4.Text, equip.ToString().PadLeft(2, '0'), "10", "0000");//发送的指令码是0A。。。转换成十进制是：10
                                sendIns_queue.Enqueue(new FacMessage(ins_num++, "0B", equip.ToString().PadLeft(2, '0'), false, 3, "获取回复边距，顶高，轴距参数", d, s_Produce));//控制箱的回应吗是“0B”
                            }

                

                        }
                        catch (Exception se)
                        {//如果数据库连接失败，则抛出异常.,并将异常写入文件中
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else
                    {//如果是一开始数据库没有连接好，抛出异常
                        //MessageBox.Show("请检查数据库是否创建好", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }



                }
                else if (s[2].Equals("03"))
                {//回应设置直径
                    float diameter = float.Parse(data.Substring(4, 4));

                    int value = parameter("Diameter", (diameter / 100).ToString(), equip.ToString());
                    if (value == 1)
                    {
                        Invoke(new MethodInvoker(delegate()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置直径成功\r\n\r\n");
                        }));
                    }
                }
                else if (s[2].Equals("05"))
                {//回应设置高度
                    float cylinderH = float.Parse(data.Substring(4, 4));

                    int value = parameter("CylinderH", (cylinderH / 100).ToString(), equip.ToString());
                    if (value == 1)
                    {
                        Invoke(new MethodInvoker(delegate()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置仓筒高度成功\r\n\r\n");
                        }));

                    }
                }
                else if (s[2].Equals("07"))
                {//回应设置下锥高度
                    float pyramidH = float.Parse(data.Substring(4, 4));
                    int value = parameter("PyramidH", (pyramidH / 100).ToString(), equip.ToString());
                    if (value == 1)
                    {
                        Invoke(new MethodInvoker(delegate()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置下锥高度成功\r\n\r\n");
                        }));

                    }
                }
                else if (s[2].Equals("09"))
                {//回应设置密度
                    float density = float.Parse(data.Substring(4, 4));
                    int value = parameter("Density", (density / 1000).ToString(), equip.ToString());
                    if (value == 1)
                    {
                        Invoke(new MethodInvoker(delegate()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置物料密度成功\r\n\r\n");
                        }));
                    }
                }
                else if (s[2].Equals("0B"))
                {//设备数据

                    
                    String[] datas = data.Split('+');
                    //richTextBox1.AppendText("回复边距，顶高，轴距参数" + data + "\r\n");
                    if (datas.Length != 3)
                    {
                        return;
                    }
                    String Margin = datas[0];//边距
                    String Top = datas[1];//顶高度
                    String Wheelbase = datas[2];//轴距

                    float M = Convert.ToSingle(Margin) / 100;
                    float T = Convert.ToSingle(Top) / 100;
                    float W = Convert.ToSingle(Wheelbase) / 100;
                    //richTextBox1.AppendText(M + " " + T + " " + W + "\r\n");
                    ///////////////////test add 
                    //都要跟新数据
                    parameter("Margin", M.ToString(), equip.ToString().PadLeft(2, '0'));
                    parameter("BinTop", T.ToString(), equip.ToString().PadLeft(2, '0'));
                    parameter("Wheelbase", W.ToString(), equip.ToString().PadLeft(2, '0'));

                    //richTextBox1.AppendText(DateTime.Now.ToString("G") + "回复了设备数据，发送了请求步进角的信息" + "\r\n\r\n");
                    //获取步进角
                    string d = Data.Data(comboBox4.Text, equip.ToString().PadLeft(2, '0'), "40", "0000");//查询读取/设定扫描步进角度发送的指令码是28。。。转换成十进制是：40
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "29", equip.ToString().PadLeft(2, '0'), false, 3, "获取步进角", d, s_Produce));//控制箱的回应吗是“0B”
                  
                }
                else if (s[2].Equals("0D"))
                {//设备中的串口数据
                    String[] datas = data.Split('+');
                    if (datas.Length != 3)
                    {
                        return;
                    }
                    String Speed = datas[0];
                    String Channel = datas[1];
                    String ModelAddress = datas[2];
                    //richTextBox1.AppendText("设备中的串口数据" + Speed + " " + Channel + " " + ModelAddress + "\r\n");

                    string path = System.Windows.Forms.Application.StartupPath;
                    FileStream fs = new FileStream(path + "\\com.txt", FileMode.Create | FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    sw.WriteLine(equip.ToString().PadLeft(2, '0') + "\t" + Speed + "\t" + Channel + "\t" + ModelAddress);
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                }
                else if (s[2].Equals("0F"))
                {//设备中的校正数据
                    richTextBox1.AppendText("垂直校准完成:" + "\r\n");
                    richTextBox1.AppendText("接收到信息");
                    String[] datas = data.Split('+');
                    if (datas.Length != 3)
                    {
                        return;
                    }
                    String k = datas[0];
                    String b = datas[1];

                    richTextBox1.AppendText("获取垂直校准数据:" + datas[2] + "\r\n");
                    int temp = 0;
                    int temp1 = 0;
                    int temp2 = 0;
                    //update数据库
                    temp = parameter("KValue", k, equip.ToString().PadLeft(2, '0'));
                    temp1 = parameter("Bvalue", b, equip.ToString().PadLeft(2, '0'));
                    temp2 = parameter("CAngle", datas[2], equip.ToString().PadLeft(2, '0'));
                    if (temp == 1 && temp1 == 1)
                    {
                        richTextBox1.AppendText("获取矫正数据k:" + k + ",数据b= " + b + "\r\n");
                        richTextBox1.AppendText("数据库修改成功\r\n");
                    }
                }

                else if (s[2].Equals("33"))
                {//设备类型
                    richTextBox1.AppendText("接收设备类型信息data ： "+ data + "\r\n");
                    string datas = data;
                    string info = "";
                    if (datas.Equals("0"))
                    {
                        richTextBox1.AppendText("侧置直径\r\n");
                        //info = "\"侧置直径\"";
                        info = 0+"";
                    }
                    else if (datas.Equals("1"))
                    {
                        richTextBox1.AppendText("侧置平扫\r\n");
                        //info = "\"侧置平扫\"";
                        info = 1+"";
                    }
                    else if (datas.Equals("2"))
                    {
                        richTextBox1.AppendText("顶置直径\r\n");
                        //info = "\"顶置直径\"";
                        info = 2+"";
                    }
                    else if (datas.Equals("3"))
                    {
                        richTextBox1.AppendText("顶置平扫\r\n");
                        //info = "\"顶置平扫\"";
                        info = 3+"";
                    }
                    else if (datas.Equals("4"))
                    {
                        richTextBox1.AppendText("侧置方形仓\r\n");
                        //info = "\"侧置方形仓\"";
                        info = 4+"";
             
                    }
                    else if (datas.Equals("5"))
                    {
                        richTextBox1.AppendText("顶置方形仓\r\n");
                        //info = "\"顶置方形仓\"";
                        info = 5+"";
                    }


                    int temp = 0;
                    temp= parameter("type", info, equip.ToString().PadLeft(2, '0'));
                    if (temp == 1 )
                    {
                        richTextBox1.AppendText("数据库修改成功\r\n");
                    }

                }
                else if (s[2].Equals("35"))
                {//设备类型
                    richTextBox1.AppendText("回复水平角度data ： " + data + "\r\n");
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    int temp = 0;
                    if (d1.Equals("0"))
                    {
                        richTextBox1.AppendText("获取水平角度\r\n");
                    }
                    else if (d1.Equals("1"))
                    {
                        richTextBox1.AppendText("设置水平角度成功\r\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("读取水平角度值出错\r\n");
                    }
                    temp = parameter("HAngle", d2, equip.ToString().PadLeft(2, '0'));
                   
                    if (temp == 1)
                    {
                        richTextBox1.AppendText("角度为" + d2 + "\r\n");
                        richTextBox1.AppendText("数据库修改成功\r\n");
                    }
                }
                //调整水平角度
                else if (s[2].Equals("37"))
                {
                    richTextBox1.AppendText("37回复调整水平角度data ： " + data + "\r\n");
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    //当前位置
                    if (d1.Equals("0"))
                    {
                        richTextBox1.AppendText("当前位置，中间\r\n");
                    }
                    else if (d1.Equals("1"))
                    {
                        richTextBox1.AppendText("当前位置，左边\r\n");
                    }
                    else if (d1.Equals("2"))
                    {
                        richTextBox1.AppendText("当前位置，右边\r\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("当前位置，未知\r\n");
                    }

                    //前一位置
                    if (d2.Equals("0"))
                    {
                        richTextBox1.AppendText("前一位置，中间\r\n");
                    }
                    else if (d2.Equals("1"))
                    {
                        richTextBox1.AppendText("前一位置，左边\r\n");
                    }
                    else if (d2.Equals("2"))
                    {
                        richTextBox1.AppendText("前一位置，右边\r\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("前一位置，未知\r\n");
                    }

                }
                else if (s[2].Equals("39"))
                {//设备类型
                    richTextBox1.AppendText("回复初始水平角度data ： " + data + "\r\n");
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    string d3 = datas[2];
                    int temp = 0;
                    if (d1.Equals("0"))
                    {
                        richTextBox1.AppendText("获取水平角度\r\n");
                    }
                    else if (d1.Equals("1"))
                    {
                        richTextBox1.AppendText("设置初始水平角度成功\r\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("读取初始水平角度值出错\r\n");
                    }

                    if (d3.Equals("1"))
                    {
                        richTextBox1.AppendText("初始水平角度为-" + d2 + "\r\n");
                    }
                    else
                    {
                        richTextBox1.AppendText("初始水平角度为" + d2 + "\r\n");
                    }

                }
                else if (s[2].Equals("41"))
                {//设备类型
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    if (d1.Equals("1"))
                    {
                        String fangbian = d2;
                        float Fb=float.Parse(fangbian);
                        MessageBox.Show(Fb + "收到方边");
                        int value = parameter("Fbian", (Fb / 100).ToString(), equip.ToString());
                        if(value == 1)
                        {
                            richTextBox1.AppendText("回复设置方仓边长 " + Fb/100 + "\r\n");
                        }
                        
                    }
                    else
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("Fbian", (Fb / 100).ToString(), equip.ToString());
                        if(value == 1)
                        {
                            richTextBox1.AppendText("回复基础方仓边长 " + Fb / 100 + "\r\n");
                        }

                    }
                     
                  

                }
                else if (s[2].Equals("43"))
                {//设备类型
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    if (d1.Equals("1"))
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("Fkuan", (Fb / 100).ToString(), equip.ToString());
                        if (value == 1)
                        {
                            richTextBox1.AppendText("回复设置方仓边宽 " + Fb / 100 + "\r\n");
                        }
        
                    }
                    else
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("Fkuan", (Fb / 100).ToString(), equip.ToString());
                        if(value == 1)
                        {
                            richTextBox1.AppendText("回复基础方仓边宽 " + Fb / 100 + "\r\n");
                        }

                    }

                }
                else if (s[2].Equals("45"))
                {//设备类型
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    if (d1.Equals("1"))
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("Fzuobian", (Fb / 100).ToString(), equip.ToString());
                        if(value == 1)
                        {
                            richTextBox1.AppendText("回复设置方仓左边距 " + Fb / 100 + "\r\n");
                        }

                    }
                    else
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("Fzuobian", (Fb / 100).ToString(), equip.ToString());
                        if(value == 1)
                        {
                            richTextBox1.AppendText("回复基础方仓左边距 " + Fb / 100 + "\r\n");
                        }
                        
                    }

                }
                else if (s[2].Equals("47"))
                {//设置上最高
                    string[] datas = data.Split('+');
                    string d1 = datas[0];
                    string d2 = datas[1];
                    if (d1.Equals("1"))
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("UpperH", (Fb / 100).ToString(), equip.ToString());
                        if (value == 1)
                        {
                            richTextBox1.AppendText("回复设置上锥 " + Fb / 100 + "\r\n");
                        }

                    }
                    else
                    {
                        String fangbian = d2;
                        float Fb = float.Parse(fangbian);
                        int value = parameter("UpperH", (Fb / 100).ToString(), equip.ToString());
                        if (value == 1)
                        {
                            richTextBox1.AppendText("回复基础上锥 " + Fb / 100 + "\r\n");
                        }

                    }

                }
                else if (s[2].Equals("49"))
                {//回应垂直校准
                    string[] datas = data.Split('+');
                    //richTextBox1.AppendText("datas0*" + datas[0] + "\r\n\r\n");
                    //richTextBox1.AppendText("datas1*" + datas[1] + "\r\n\r\n");
                    //richTextBox1.AppendText("datas2*" + datas[2] + "\r\n\r\n");
                    if (datas[0].Equals("100"))
                    {
                        richTextBox1.AppendText("垂直定位完成"  + "\r\n\r\n");
                    }
                    else
                    {
                        if(datas[1].Equals("01"))
                        {
                            richTextBox1.AppendText("垂直定位第" + datas[0] + "次" + "\r\n\r\n");
                            richTextBox1.AppendText("负" + datas[2]+"度"+"\r\n\r\n");
                        }
                        else if (datas[1].Equals("00")){
                            richTextBox1.AppendText("垂直定位第" + datas[0] + "次" + "\r\n\r\n");
                            richTextBox1.AppendText("正" + datas[2] + "度" + "\r\n\r\n");
                        }

                    }

                }else if (s[2].Equals("51"))
                {//回应水平定位状态

                    string[] datas = data.Split('+');
                    if (datas[0].Equals("00")){
                        //开始定位
                        richTextBox1.AppendText("三维设备开始定位" + "\r\n\r\n");
                        //richTextBox1.AppendText("负" + datas[2] + "度" + "\r\n\r\n");

                    }
                    else if(datas[0].Equals("01"))
                    {
                        //查询定位结果
                        richTextBox1.AppendText("三维设备查询定位结果" + "\r\n\r\n");
                    }
                    else if (datas[0].Equals("02"))
                    {
                        //定位通讯失败
                        richTextBox1.AppendText("定位通讯失败" + "\r\n\r\n");
                    }
                    else if (datas[0].Equals("03"))
                    {
                        //定位失败
                        richTextBox1.AppendText("定位失败" + "\r\n\r\n");
                    }
                    else if (datas[0].Equals("04"))
                    {
                        //定位成功
                        richTextBox1.AppendText("定位成功" + "\r\n\r\n");
                    }
                }
                else if (s[2].Equals("11"))
                {//回应温度湿度
                    //richTextBox1.AppendText("仓内温度：接收到的指令数据" + ins + "\r\n\r\n");
                    查询温度ToolStripMenuItem.Enabled = true;
                    string[] d = data.Split('+');
                    string id = Convert.ToInt32(s[1], 16).ToString();
                    string temp = d[0];//温度
                    string hum = d[1];//湿度
                    //richTextBox1.AppendText("获取到温度\r\n");

                    //MessageBox.Show("回应的温湿度值" + temp + "*****" + hum+ "******numxuanzhong"+ numxuanzhong);
                    if(checkedListBox1.CheckedItems.Count == 1)
                    {
                        label24.Text = temp + "  ℃";
                        label27.Text = hum + "  %";
                    }

                    if(xuanOne == 1)
                    {

                        label43.Text = temp + "  ℃";
                        
                        label36.Text = hum + "  %";

                    }


                    if (numxuanzhong == 2)
                    {
                        label43.Text = temp + "  ℃";
                        label36.Text = hum + "  %";
                        numxuanzhong--;
                        //MessageBox.Show("%%%%%%%%%%%55" + numxuanzhong);
                    }
                    else if(numxuanzhong == 1)
                    {
                        label40.Text = temp + "  ℃";
                        label42.Text = hum + "  %";
                        numxuanzhong--;
                    }

                    //string temp1 = ins[17] + "" + ins[18] + ""+str[19] + "" + str[20];//温度的整数部分
                    //int temp1_int = Int32.Parse(temp1, System.Globalization.NumberStyles.HexNumber);//温度整数部分转化为十进制
                    //temp1 = Convert.ToString(temp1_int, 2);//temp的二进制表示
                    //temp1 = temp1.PadLeft(8, '0');//温度二进制的格式化表示

                    if (isReWendu(equip, temp).Equals("error"))
                    {
                        Invoke(new MethodInvoker(delegate ()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "接收到温湿度信息并保存\r\n\r\n");
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "接收到的指令==" + ins + "；\r\n\r\n");
                        }));
                    }
                    else
                    {
                    
                    //if (temp.equals("0") && hum.equals("0"))
                    //    return;

                    DateTime now = DateTime.Now;
                    string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    //string sql = "insert into [bindata] (BinID, Temp, Hum, DateTime) values(" + id + ", " + temp + ", " + hum + ", '" + time + "')";
                   
                    try
                    {//检测数据库能否连接成功
                        //DataBase db = new DataBase();
                        //db.command.CommandText = sql;
                        //db.command.Connection = db.connection;
                        if (true)
                        {
                            Invoke(new MethodInvoker(delegate()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "接收到温湿度信息并保存\r\n\r\n");
                                //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "接收到的指令==" + ins + "；\r\n\r\n");
                            }));
                            //db.Close();
                        }
                        

                        }
                        catch (Exception se)
                    {//数据库连接失败时，抛出异常
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        //richTextBox1.AppendText(se.ToString() + "\r\n");
                    }
                   }
                }
                else if (s[2].Equals("31"))
                {
                    Invoke(new MethodInvoker(delegate ()
                    {
                        comboBox3.Visible = true;
                        label3.Visible = true;
                        groupBox2.Visible = true;
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "回传数据结束\r\n\r\n");
                    }));
                }
                else if (s[2].Equals("13"))
                {//回应确认 盘点库容
                    back = 0;//开始盘库的时候，back=0。接收到信息后设为-1
                    Invoke(new MethodInvoker(delegate()
                    {
                        comboBox3.Visible = true;
                        label3.Visible = true;
                        groupBox2.Visible = true;
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "开始盘库\r\n\r\n");
                        richTextBox1.AppendText("垂直定位开始\r\n\r\n");
                    }));
                    alterPanKuNum = 1;
                    string eq_name = getName(equip.ToString());
                    if (eq_name.Equals("") != true)
                    {
                        comboBox5.Items.Remove(eq_name);
                        comboBox6.Items.Remove(eq_name);
                        comboBox3.Items.Remove(eq_name);
                        comboBox3.Items.Add(eq_name);
                        if (comboBox3.Items.Count == 1)
                        {
                            comboBox3.Text = comboBox3.Items[0].ToString();
                        }
                        if (comboBox6.Items.Count == 0)
                        {
                            player.Stop();
                        }
                        //接收到盘库信息，将料仓信息加入链表中
                        FacMessage calc = new FacMessage(equip.ToString().PadLeft(2, '0'), 0, 1);
                        CalcVol_list.Add(calc);
                    }
                    string data_find = Data.Data(comboBox4.Text, equip.ToString().PadLeft(2, '0'), "32", "0000");

                    //确定要回复状态
                    send_ins.Add(new FacMessage(ins_num++, "21", equip.ToString().PadLeft(2, '0'), false, 120, "回应状态", data_find, 3, s_Produce - 3595));

                }
                else if (s[2].Equals("23"))
                {//盘库结果
                    //richTextBox1.AppendText("data" + data);
                    string weight = (data.Split('+'))[1];
                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    string e1 = (data.Split('+'))[2];//字节6
                    string e2 = (data.Split('+'))[3];//字节7
                    string e3 = (data.Split('+'))[4];//字节8
                    string model = (data.Split('+'))[5];//字节9
                    string moshi = "";
                    richTextBox1.AppendText("盘库完成\r\n" + "获得的模式是===" + model);

                   // richTextBox1.AppendText("字节678"+e1 + e2 + e3);

                    if (model.EndsWith("0"))
                    {
                        moshi = "满仓";
                    }
                    else if (model.EndsWith("1"))
                    {
                        moshi = "半径算法";
                    }
                    else if (model.EndsWith("2"))
                    {
                        moshi = "直径算法";
                    }
                    else if (model.EndsWith("3"))
                    {
                        moshi = "体积为负数";
                    }
                    if (isRe(equip, weight).Equals("error"))//确保盘一次库就只有一个显示盘库结果。当有重复的值的时候，返回error
                    {
                        //richTextBox1.AppendText("盘库完成错误！！！！！！！！！！，数据库中已经有了数据");
                        if (back!=-1)
                        {
                            
                            Invoke(new MethodInvoker(delegate ()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询到  " + getName(equip.ToString().PadLeft(2, '0')) + "  料仓数据并保存\r\n\r\n");
                                richTextBox1.AppendText("盘库的模式是：" + moshi + "\r\n");
                                richTextBox1.AppendText("测量过程中出错点个数：" + e1 + "\r\n");
                                richTextBox1.AppendText("数据检查中出错点个数：" + e2 + "\r\n");
                                richTextBox1.AppendText("测量过程中全部点个数：" + e3 + "\r\n\r\n");
                                back = -1;

                            }));

                            for (int i = send_ins.Count - 1; i >= 0; i--)
                            {//从后往前遍历，避免删除节点后数组越界
                                if (send_ins.Count == 0) break;
                                if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                {
                                    comboBox3.Items.Remove(getName(send_ins[i].fac_num));
                                    send_ins.RemoveAt(i);
                                    if (comboBox3.Items.Count == 0)
                                    {
                                        comboBox3.Visible = false;
                                        label3.Visible = false;
                                        //groupBox2.Visible = false;
                                        progressBar2.Value = 0;
                                        label19.Text = "0";
                                    }
                                    else
                                        comboBox3.Text = comboBox3.Items[0].ToString();
                                    //break;

                                }
                            }
                            for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                            {
                                if (CalcVol_list.Count == 0) break;
                                if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                                {
                                    CalcVol_list.RemoveAt(i);
                                    //break;
                                }
                            }

                        }
                    }
                    else
                    {
                        richTextBox1.AppendText("盘库完成，正确");
                        
                        string volume = (data.Split('+'))[0];
                        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        if (model.EndsWith("3"))
                        {
                            moshi = "体积为负数";
                            volume = "-" + volume;
                        }

                        //MessageBox.Show("获取数据data="+data + " ,volume = " + volume + "  ,weight=" + weight +",e1=" + e1+",e2="+e2+",e3="+e3);
                        float vol_f = float.Parse(volume);
                        float wei_f = float.Parse(weight);


                        float diameter = 0, cylinderh = 0, pyramidh = 0;

                        try
                        {

                            string sql = "select * from bininfo where BinID = " + equip;
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                diameter = float.Parse(rd["Diameter"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            sql = "select Weight from bindata where BinID = " + equip + " order by DateTime desc";
                            float LastWeight = 0;
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);
                            while (rd1.Read())
                            {
                                if (rd1["Weight"].ToString().Equals("") == false)
                                {//获取到第一个重量不为NULL的值，记录为最新数据,并且跳出循环
                                    LastWeight = float.Parse(rd1["Weight"].ToString());
                                    break;
                                }
                            }

                            rd1.Close();
                            ms1.Close();

                            float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                            if (Vol_ware <= vol_f)//出现错误时，进行报错
                            {
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "错误：盘库数据比仓库体积还大\r\n\r\n");
                                new Thread(new ParameterizedThreadStart(showBox)).Start("错误：盘库数据比仓库体积还大");
                            }
                            if ((LastWeight != 0 && Math.Abs(wei_f - LastWeight) > 10))//1
                            {//如果接收到的数据比仓库体积还大, 或者重量与最新数据相比相差10吨，就开启回传数据。在服务里面回传
                                //back = 0;
                                //richTextBox1.AppendText("错误回传,数据比仓库体积还大");
                                //string databack = Data.Data(comboBox4.Text, equip.ToString().PadLeft(2, '0'), "38", "0001");
                                //sendIns_queue.Enqueue(new FacMessage(1, "27", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "开启回传数据", databack, s_Produce - 3595));
                            }
                        }
                        catch (Exception e) { }

                        if (vol_f == 0)
                            return;
                        string id = Convert.ToInt32(s[1], 16).ToString();

                        try
                        {
                            string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            //在服务中保存信息
                            //string sql = "insert into [bindata] (BinID, Volume, Weight, DateTime) values(" + id + ", " + volume + ", " + weight + ", '" + time + "')";

                            //DataBase db_save = new DataBase();
                            //db_save.command.CommandText = sql;
                            //db_save.command.Connection = db_save.connection;

                            if (back != -1)
                            {
                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询到  " + getName(equip.ToString().PadLeft(2, '0')) + "  料仓数据并保存\r\n\r\n");
                                    richTextBox1.AppendText("盘库的模式是：" + moshi + "\r\n");
                                    richTextBox1.AppendText("测量过程中出错点个数：" + e1 + "\r\n");
                                    richTextBox1.AppendText("数据检查中出错点个数：" + e2 + "\r\n");
                                    richTextBox1.AppendText("测量过程中全部点个数：" + e3 + "\r\n\r\n");
                                    back = -1;

                                }));


                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {//从后往前遍历，避免删除节点后数组越界
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        comboBox3.Items.Remove(getName(send_ins[i].fac_num));
                                        send_ins.RemoveAt(i);
                                        if (comboBox3.Items.Count == 0)
                                        {
                                            comboBox3.Visible = false;
                                            label3.Visible = false;
                                            //groupBox2.Visible = false;
                                            progressBar2.Value = 0;
                                            label19.Text = "0";
                                        }
                                        else
                                            comboBox3.Text = comboBox3.Items[0].ToString();
                                        //break;

                                    }
                                }
                                for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                                {
                                    if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                                    {
                                        CalcVol_list.RemoveAt(i);
                                        //break;
                                    }
                                }

                            }

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            //MessageBox.Show("请检查数据库设置", "提示");
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置-2442");
                        }
                    }
                    //接收到了盘库结果就要删除节点
                    for (int i = send_ins.Count - 1; i >= 0; i--)
                    {//从后往前遍历，避免删除节点后数组越界
                        if (send_ins.Count == 0) break;
                        if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                        {
                            comboBox3.Items.Remove(getName(send_ins[i].fac_num));
                            send_ins.RemoveAt(i);
                            if (comboBox3.Items.Count == 0)
                            {
                                comboBox3.Visible = false;
                                label3.Visible = false;
                                //groupBox2.Visible = false;
                                progressBar2.Value = 0;
                                label19.Text = "0";
                            }
                            else
                                comboBox3.Text = comboBox3.Items[0].ToString();
                            //break;

                        }
                    }
                    for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                    {
                        if (CalcVol_list.Count == 0) break;
                        if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                        {
                            CalcVol_list.RemoveAt(i);
                            //break;
                        }
                    }

                }
                else if (s[2].Equals("17"))
                {//回应确认清洁镜头
                    string eq_name = getName(equip.ToString());

                    Invoke(new MethodInvoker(delegate()
                    {
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  开始清洁镜头\r\n\r\n");
                    }));
                    clean_list.Add(new Clean(equip.ToString(), eq_name, 150));

                    if (eq_name.Equals("") != true)
                    {
                        comboBox5.Items.Remove(eq_name);
                        comboBox6.Items.Remove(eq_name);
                        comboBox3.Items.Remove(eq_name);
                        comboBox5.Items.Add(eq_name);
                        if (comboBox5.Items.Count == 1)
                        {
                            comboBox5.Text = comboBox5.Items[0].ToString();
                        }
                        if (comboBox6.Items.Count == 0)
                        {
                            player.Stop();
                        }
                    }
                    FacMessage calc = new FacMessage(equip.ToString().PadLeft(2, '0'), 0, 2);
                    CalcVol_list.Add(calc);

                }

                else if (s[2].Equals("1B"))
                {//回复确认 进入或退出监控
                    if (data.Substring(6, 2).Equals("01"))
                    {//开始监控
                        Invoke(new MethodInvoker(delegate()
                        {
                            label34.Visible = true;
                            comboBox6.Visible = true;
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  进入监控\r\n\r\n");
                        }));
                        string eq_name = getName(equip.ToString());
                        comboBox6.Items.Remove(eq_name);
                        comboBox3.Items.Remove(eq_name);
                        comboBox5.Items.Remove(eq_name);
                        comboBox6.Items.Add(eq_name);
                        monitor.userControl11.Add(getName(equip.ToString()));
                        if (comboBox6.Items.Count == 1)
                        {
                            comboBox6.Text = comboBox6.Items[0].ToString();
                        }
                    }
                    else if (data.Substring(6, 2).Equals("00"))
                    {//退出监控
                        Invoke(new MethodInvoker(delegate()
                        {
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  退出监控\r\n\r\n");
                        }));
                        string eq_name = getName(equip.ToString());
                        //在监控状态栏删除节点，
                        comboBox6.Items.Remove(eq_name);
                        if (comboBox6.Items.Count == 0)
                        {
                            comboBox6.Visible = false;
                            label34.Visible = false;
                            player.Stop();
                        }
                        else
                            comboBox6.Text = comboBox6.Items[0].ToString();
                        string distance = monitor.userControl11.getHeight(getName(equip.ToString()));
                        try
                        {
                            //if (float.Parse(distance) / 100 < float.Parse(monitor.userControl11.getTxt(getName(equip.ToString()))))
                            if (monitor.userControl11.changeColor(getName(equip.ToString().PadLeft(2, '0'))))
                            {//关闭一个超过预警阈值的料仓，超过预警阈值的料仓个数就减一
                                alarm--;
                            }
                            if (alarm == 0)
                            {//如果监控的料仓没有值超过预警阈值，关闭音效
                                player.Stop();
                                isplaying = false;
                            }
                            if (comboBox6.Items.Count == 0)
                            {//点击退出监控，如果没有要监控的料仓，就把监控料仓的定时器关闭
                                t_monitor.Enabled = false;
                                player.Stop();
                            }
                            Invoke(new MethodInvoker(delegate
                            {
                                monitor.userControl11.Delete(getName(equip.ToString()));
                            }));

                        }
                        catch (FormatException exc)
                        {
                        }
                    }
                }
                else if (s[2].Equals("1D"))
                {//回复当前测量值
                    if (SqlConnect == 1)
                    {
                        //MessageBox.Show("1D" + s[3]);
                        string distance = data.Substring(4, 4);
                        if (float.Parse(distance) == 0)
                        {
                            return;
                        }

                        string name = getName(equip.ToString());

                        string sql = "select * from bininfo where BinName = '" + name + "'";
                        float height_sum = 0;
                        try
                        {
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.Dr = db.command.ExecuteReader();
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                height_sum = float.Parse(rd["CylinderH"].ToString()) + float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            //richTextBox1.AppendText(height_sum + "   " + distance + "\r\n");

                            try
                            {
                                monitor.userControl11.setValue(name, distance, (int)((height_sum * 100 - float.Parse(distance)) / height_sum));
                                //MessageBox.Show("distance" + distance);
                                //MessageBox.Show("height_sum" + height_sum);
                                //MessageBox.Show("equip" + equip);
                                if (float.Parse(distance) / 100 < float.Parse(monitor.userControl11.getTxt(getName(equip.ToString()))))
                                {//超过高度阈值进度条颜色变红， 响铃
                                    monitor.userControl11.setColor(getName(equip.ToString()));
                                    alarm++;
                                }
                                else
                                {

                                    if (monitor.userControl11.changeColor(getName(equip.ToString())))
                                    {//检测是否是改变了高度阈值
                                        alarm--;
                                        if (alarm <= 0)
                                        {
                                            player.Stop();
                                            isplaying = false;
                                        }
                                    }
                                    monitor.userControl11.setGreen(getName(equip.ToString()));
                                }

                                if (alarm > 0)
                                {
                                    if (isplaying == false)
                                    {
                                        player.SoundLocation = Application.StartupPath + "//alarm.wav";
                                        player.Load();
                                        player.PlayLooping();
                                        isplaying = true;
                                    }
                                }
                            }
                            catch (FormatException exc)
                            {
                                //MessageBox.Show("请确认已经进入监控状态", "提示");
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请确认已经进入监控状态");
                            }
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else
                    {
                        //MessageBox.Show("请检查数据库是否创建好", "提示");
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                }
                else if (s[2].Equals("1F"))
                {//回复确认  取消操作
                    if (send_ins.Count != 0)
                    {

                        for (int i = send_ins.Count - 1; i >= 0; i--)
                        {
                            if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                            {
                                send_ins.RemoveAt(i);
                            }
                        }
                    }
                    string fac_name = getName(equip.ToString());
                    comboBox3.Items.Remove(fac_name);//正在盘库列表删除该节点
                    comboBox5.Items.Remove(fac_name);//正在清洁镜头列表删除该节点
                    comboBox6.Items.Remove(fac_name);//正在监控列表删除该节点
                    if (comboBox3.Items.Count == 0)
                    {
                        comboBox3.Visible = false;
                        label3.Visible = false;
                        //groupBox2.Visible = false;
                        progressBar2.Value = 0;
                        label19.Text = "0";
                    }
                    if (comboBox5.Items.Count == 0)
                    {
                        try
                        {
                            comboBox5.Visible = false;
                            label33.Visible = false;
                            //groupBox2.Visible = false;
                        }
                        catch (Exception exc) { }
                    }
                    if (comboBox6.Items.Count == 0)
                    {
                        comboBox6.Visible = false;
                        label34.Visible = false;
                    }
                    if (monitor.userControl11.changeColor(fac_name))
                        alarm--;
                    if (alarm == 0 || comboBox6.Items.Count == 0)
                    {
                        player.Stop();
                        isplaying = false;
                    }
                    monitor.userControl11.Delete(fac_name);

                    for (int j = CalcVol_list.Count - 1; j >= 0; j--)
                    {
                        if (CalcVol_list[j].fac_num.PadLeft(2, '0').Equals(equip.ToString().PadLeft(2, '0')))
                        {
                            CalcVol_list.RemoveAt(j);
                            break;
                        }
                    }

                    //让文本框获取焦点，不过注释这行也能达到效果
                    richTextBox1.Focus();
                    //设置光标的位置到文本尾   
                    richTextBox1.Select(richTextBox1.TextLength, 0);
                    //滚动到控件光标处   
                    richTextBox1.ScrollToCaret();
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  已停止当前操作\r\n\r\n");

                }

                else if (s[2].Equals("21"))
                {//接收到工作状态
                    //ispk = 1;//是否要向oper_ins添加数据
                    //richTextBox1.AppendText("data = " +data +"\r\n\r\n");qingj
                    int issendins = 0;//判断是否发出了指令
                    string[] data_array = data.Split('+');
                    if (data_array.Length != 3)
                        return;
                    string complet = data_array[0];
                    
                    data = data_array[1];
                    string schedule = data_array[2];
                    int schedule_int = Int32.Parse(schedule);
                    //richTextBox1.AppendText("接收到21\r\nsendIns_list长度  "+sendIns_list.Count.ToString() + "\r\n");
                    int data_int = Int32.Parse(data, System.Globalization.NumberStyles.HexNumber);

                    //richTextBox1.AppendText("  接收到21   data_int = "+ data_int + " complet = " + complet + " \r\n\r\n");
                    
                    if (data_int == 2 && schedule_int == 100)
                    {
                        progressBar2.Value = 0;
                    }
                    //MessageBox.Show("正在盘库的设备数" + CalcVol_list.Count);
                    if(data_int != 3)//当正在清洁镜头时，在盘库就不会更新进度
                    {
                        //接收到状态后，将进度信息更改到正在盘库列表中
                        for (int i = 0; i < CalcVol_list.Count; i++)
                        {
                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                            {
                                CalcVol_list[i].life_time = schedule_int;
                            }
                        }
                    }
                   
                    FacMessage ele;
                    //richTextBox1.AppendText("oper_ins.Count ：  " + oper_ins.Count + "\r\n");
                    if (oper_ins.Count != 0 )
                    {
                        //richTextBox1.AppendText("接收到21\r\n complet：  " + complet + "\r\n");
                        //richTextBox1.AppendText("\r\n data_int：  " + data_int + "\r\n");
                        //richTextBox1.AppendText("\r\n schedule_int：  " + schedule_int + "\r\n");


                        ele = oper_ins.Dequeue();
                        //richTextBox1.AppendText("\r\n oper_ins 删除掉了： \r\n");
                        string fac_num = ele.fac_num.PadLeft(2, '0');

                        if (data_int != 2 && complet.Equals("01"))
                        {//表示盘库已经完成，可以获取数据

                            //string data_find = Data.Data(comboBox4.Text, equip.ToString().PadLeft(2, '0'), "34", "0000");
                            if (ele.ins_answer.Equals("23"))
                            {
                                port_mask = 1;//将发送函数屏蔽掉，
                                while (true)
                                {
                                    Thread.Sleep(30);
                                    if (list_status.Count == 0)
                                    {
                                        try
                                        {
                                            //serialPort1.WriteLine(ele.instruction);
                                            ////list_status.Add(new FacMessage(ins_num++, "23", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "查询结果", data));
                                            //将要发送的指令,通过消息队列发送给mqtt发
                                            JsonMsg js = new JsonMsg("2", ele.instruction);
                                            string sendMqttInfo = JsonConvert.SerializeObject(js);
                                            WriteMQ(sendMqttInfo);
                                            list_status.Add(ele);
                                            issendins = 1;
                                            break;
                                        }
                                        catch (Exception exc) { }

                                    }
                                }
                                port_mask = 0;//解除屏蔽
                            }

                            //return;
                        }
                        else if (data_int != 2 && complet.Equals("00"))
                        {

                            //richTextBox1.AppendText("接收到被取消盘库data_int != 2 && complet.Equals(00)");
                            if (send_ins.Count != 0)
                            {
                                //richTextBox1.AppendText("send_ins.Count != 0");
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {
                                    //richTextBox1.AppendText("send_ins[i].fac_num = "+send_ins[i].fac_num+ "....equip.ToString().PadLeft(2, '0')=" + equip.ToString().PadLeft(2, '0'));
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        //richTextBox1.AppendText("要停止了");
                                       //取消盘库直接删除节点
                                       comboBox3.Items.Remove(getName(send_ins[i].fac_num));

                                        for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                                        {
                                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                                            {
                                                CalcVol_list.RemoveAt(it);
                                                break;
                                            }
                                        }

                                        if (comboBox3.Items.Count == 0)
                                        {
                                            comboBox3.Visible = false;
                                            label3.Visible = false;
                                            //groupBox2.Visible = false;
                                            progressBar2.Value = 0;
                                            label19.Text = "0";
                                        }
                                        else
                                            comboBox3.Text = comboBox3.Items[0].ToString();

                                        String name = getName(send_ins[i].fac_num);

                                        send_ins.RemoveAt(i);

                                        issendins = 1;

                                        DateTime now = DateTime.Now;
                                        string sql = "insert into binlog values('" + equip.ToString() + "', '硬件故障', '" + ins + "', '客户端：盘库被取消', '" + now.ToString("yyyy/MM/dd HH:mm:ss") + "')";
                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.command.ExecuteNonQuery();
                                        //db.Close();
                                        MySqlConn ms = new MySqlConn();
                                        int isR = ms.nonSelect(sql);
                                        ms.Close();
                                        //MessageBox.Show("料仓 " + name + " 被取消盘库", "提示");
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("料仓"+name+" 被取消盘库");



                                        //让文本框获取焦点，不过注释这行也能达到效果
                                        richTextBox1.Focus();
                                        //设置光标的位置到文本尾   
                                        richTextBox1.Select(richTextBox1.TextLength, 0);
                                        //滚动到控件光标处   
                                        richTextBox1.ScrollToCaret();
                                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n料仓 " + name + " 被取消盘库" + "\r\n\r\n");

                                        //return;
                                    }
                                }

                            }
                        }
                        //for (int i = 0; i <sendIns_list.Count; i++)
                        if (issendins == 0)
                        {
                            if (fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                            {
                                //serialPort_WriteLine(ele);//直接发送，不往发送队列中添加
                                //richTextBox1.AppendText("进入到判断\r\n");
                                if (data_int == 0)
                                {//表示此料仓无操作，可以执行盘库等操作
                                    if (ele.instruction.Equals("delete"))
                                    {
                                        string fac_name = getName(fac_num);
                                        int d = delete(fac_name);
                                        if (d > 0)
                                        {
                                            Invoke(new MethodInvoker(delegate
                                            {
                                                //让文本框获取焦点，不过注释这行也能达到效果
                                                richTextBox1.Focus();
                                                //设置光标的位置到文本尾   
                                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                                //滚动到控件光标处   
                                                richTextBox1.ScrollToCaret();
                                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n删除了料仓  " + fac_name + "\r\n\r\n");
                                                if (d != 0)
                                                {
                                                    checkedListBox1.Items.Remove(fac_name);
                                                }
                                            }));
                                        }

                                    }
                                    else
                                    {
                                        //serialPort_WriteLine(ele);
                                        port_mask = 1;//将发送函数屏蔽掉，
                                        while (true)
                                        {
                                            Thread.Sleep(30);
                                            if (list_status.Count == 0)
                                            {
                                                try
                                                {
                                                    //serialPort1.WriteLine(ele.instruction);
                                                    //list_status.Add(ele);

                                                    //将要发送的指令,通过消息队列发送给mqtt发
                                                    JsonMsg js = new JsonMsg("2", ele.instruction);
                                                    string sendMqttInfo = JsonConvert.SerializeObject(js);
                                                    WriteMQ(sendMqttInfo);
                                                    list_status.Add(ele);
                                                    break;
                                                }
                                                catch (Exception exc) { }

                                            }
                                        }
                                        port_mask = 0;//解除屏蔽
                                        //serialPort1.WriteLine(ele.instruction);
                                        //list_status.Add(ele);
                                        //return;
                                    }


                                }

                                else if (data_int == 1)
                                {
                                    if (ele.ins_answer.Equals("1B"))
                                    {//退出监控状态
                                        string[] ins_send = Data.decoding(ele.instruction + "\n").Split(' ');
                                        string ins_data;
                                        if (ins_send.Length <= 1)
                                            return;
                                        ins_data = ins_send[3];
                                        if (Int32.Parse(ins_data, System.Globalization.NumberStyles.HexNumber) == 0)
                                        {//表示向正在监控的料仓发送退出监控指令

                                            //list_status.Add(ele);
                                            //serialPort1.WriteLine(ele.instruction);
                                            port_mask = 1;//将发送函数屏蔽掉，
                                            while (true)
                                            {
                                                Thread.Sleep(30);
                                                if (list_status.Count == 0)
                                                {
                                                    try
                                                    {
                                                        //serialPort1.WriteLine(ele.instruction);
                                                        //将要发送的指令,通过消息队列发送给mqtt发
                                                        JsonMsg js = new JsonMsg("2", ele.instruction);
                                                        string sendMqttInfo = JsonConvert.SerializeObject(js);
                                                        WriteMQ(sendMqttInfo);
                                                        list_status.Add(ele);
                                                        break;

                                                    }
                                                    catch (Exception exc)
                                                    {
                                                        break;
                                                    }
                                                }
                                            }
                                            port_mask = 0;//解除屏蔽
                                        }
                                        else
                                        {
                                            new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                        }
                                            //MessageBox.Show(, "提示");
                                    }
                                    else
                                    {
                                        new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                    }
                                        //MessageBox.Show(, "提示");
                                }
                                else if (data_int == 2)
                                {
                                    bool isoperating = false;
                                    for (int i = 0; i < send_ins.Count; i++)
                                    {
                                        if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                        {
                                            isoperating = true;
                                            break;
                                        }
                                    }
                                    //if(isoperating == false || ele.instruction.Equals("delete"))
                                    //richTextBox1.AppendText(isoperating.ToString() + "  " + ele.ins_answer+"  "+ele.instruction+"\r\n");
                                    //接收到的状态信息是2， 表示料仓正在进行盘库，isoperating为false是指人工进行了盘库
                                    //但是软件部分不知道，delete是指要进行删除料仓操作，需要提示用户料仓正在盘库
                                    if (isoperating == false || ele.instruction.Equals("delete")){
                                        new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在盘库， 请稍后操作示");
                                    }
                                        //MessageBox.Show();

                                    //料仓正在进行盘库，但是操作指令不是查询数据指令
                                    else if (isoperating == true && (ele.ins_answer.Equals("23") == false))
                                    {
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("料仓  " + getName(fac_num) + "  正在盘库， 请稍后操作");
                                    }
                                }
                                else if (data_int == 3)
                                {
                                    new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在清洁镜头， 请稍后操作");
                                }

                            }
                        }


                    }
                }
                else if (s[2].Equals("25"))
                {//回传数据的过程
                    //back = 0;
                    port_mask = 1;//屏蔽串口
                    back_complet = 2;
                    string[] d = data.Split('+');
                    //richTextBox1.AppendText("指令是：" + ins + "\r\n");
                    //richTextBox1.AppendText("角度是：" + d[0] + "\r\n 距离是：" + d[1] + "\r\n 进度是： " + d[2] + "\r\n");

                    if (backdata_path.Length <= 1)
                    {
                        backdata_path = "data.txt";
                    }
                    writeToFile_buffer += d[0] + " " + d[1] + " " + d[2] + "\r\n";

                    recv_num--;//在确认回传的时候收到的测量点数

                    backLengthData += d[1] + ",";//将要保存数据库的数据保存在字段中
                    int angle_int = Int32.Parse(d[0]);
                    backdata[angle_int].length = d[1];
                    backdata[angle_int].schedule = d[2];

                    if (recv_num == 0)
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n回传数据保存成功\r\n\r\n");
                    }

                }
                else if (s[2].Equals("27"))
                {//开始回传数据
                    for (int i = 0; i < 600; i++)
                    {
                        backdata[i] = new BackData();
                    }
               
                    Invoke(new MethodInvoker(delegate ()
                    {
                        comboBox3.Visible = true;
                        label3.Visible = true;
                        groupBox2.Visible = true;
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n开始回传数据" + "\r\n\r\n");
                    }));
                   
                    int data_int = Int32.Parse(data);
                    DateTime now = DateTime.Now;
                    backdata_path = "data_" + equip.ToString().PadLeft(2, '0') + "_" + now.Year.ToString().PadLeft(2, '0') + now.Month.ToString().PadLeft(2, '0') + now.Day.ToString().PadLeft(2, '0') + "_" + now.Hour.ToString().PadLeft(2, '0') + now.Minute.ToString().PadLeft(2, '0') + now.Second.ToString().PadLeft(2, '0') + "_"+data_int + ".txt";
                    recv_num = data_int;//传过来的点的个数
                    backLengthData = "";
                    
                }
                //回应步进角的数值
                else if (s[2].Equals("29"))
                {
                    

                    if (data.Equals("设置成功"))
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "步进角设置成功" + "\r\n\r\n");
                        //回应角度值并更新保存数据库
                    }
                    else
                    {
                        //获取到步进角的值。修改步进角度
                        //richTextBox1.AppendText(DateTime.Now.ToString("G") + "回复了不仅角度" + data + "\r\n\r\n");
                        parameter("Angle", data, equip.ToString().PadLeft(2, '0'));
                    }
                }
                //回应版本号
                else if (s[2].Equals("2B"))
                {


                    
                    string[] arr = data.Split('+');
                    
                    for(int i = 0; i< arr.Length; i++)
                    {
                        int temp = Convert.ToInt32(arr[i], 16);//将十六进制的数转为十进制
                        string alpha = ((char)temp).ToString();//将十进制ASCII码转为string
                        arr[i] = alpha;

                    }
                    string banBen = arr[0] + "." + arr[1] + "." + arr[2] + "." + arr[3];

                    new Thread(new ParameterizedThreadStart(showBox)).Start("中控版本信息: " + banBen);
                    //richTextBox1.AppendText("当前硬件的版本号："+banBen+ "\r\n");
                    //回应角度值并更新保存数据库

                }
                //温度板
                else if (s[2].Equals("2D"))
                {
                    //MessageBox.Show("获取到2D指令");
                    //richTextBox1.AppendText("测量设备：接收到的指令数据" + ins + "\r\n\r\n");
                    string[] arr = data.Split('+');
                    int temp0 = Convert.ToInt32(arr[0], 16);//将十六进制的数转为十进制 第0个字节。
                                                            //show("接收到的模式代码：" + temp0 +"/r/n/r/n");

                    if (temp0 == 0)
                    {
                        new Thread(new ParameterizedThreadStart(showBox)).Start("操作成功!!!!!");
                    }
                    else if (temp0 == 1)//回复设置温度，回复当前设置的加热温度
                    {
                        int temp1 = Convert.ToInt32(arr[1], 16);//将十六进制的数转为十进制 第1个字节。为数据
                        //show("接收到的数据：" + temp1 + "/r/n/r/n");
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置温度为" + temp1 + "  ℃ \r\n\r\n");
                    }
                    else if (temp0 == 2)//回复实时温度，回复测量设备内部温度
                    {   //因为温度已经转换成了十进制的了
                        richTextBox1.AppendText(DateTime.Now.ToString("G")+"\r\n"  + "测量设备温度为:" + arr[1]+ "  ℃\r\n\r\n");

                        if (correntWenDo.Equals(equip.ToString()))//用于区分实时查询和一次查询
                        {
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "查询实时温度设备id:" + correntWenDo + "  \r\n\r\n");
                            this.labelWenDuTest.Text = arr[1] + "  ℃";//给当前数据设置实时温度...用户在点击当前数据的时候，会发送查询温度指令，，接收温度指令以后，更新控件
                        }
                       
                    }
                    else if(temp0 == 3)//回复加热，回复电脑端加热操作.
                    {
                        //接收到3，说明开启加热，。。之后发送2号指令来进行实时温度查询
                        richTextBox1.AppendText(DateTime.Now.ToString("G")+"\r\n" + "开始加热" + "\r\n\r\n");
                        //开启线程，每隔10s去读取温度

                        if (isRequireWenDu == false)//保证只有在没有查询温度的时候才开启线程查询温度
                        {
                            isRequireWenDu = true;//可以开启10s查询温度
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "开启线程！！！！！" + "\r\n\r\n");
                            Thread toGetWenDu = new Thread(new ParameterizedThreadStart(getWenDo));
                            toGetWenDu.Start(equip.ToString());
                        }
                        else
                        {
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "正在查询温度请稍后" + "\r\n\r\n");
                        }


                    }
                    else if (temp0 == 4)//取消加热.取消加热，有多种情况
                    {
                        int temp1 = Convert.ToInt32(arr[1], 16);//将十六进制的数转为十进制 第1个字节。为数据
                        isRequireWenDu = false;//可以开启10s查询温度
                        //接收到3，说明开启加热，。。之后发送2号指令来进行实时温度查询
                        string res = "";
                        //show("接收到的数据：" + temp1 + "/r/n/r/n");
                        switch (temp1)
                        {
                            case 3:
                                res = "达到极限温度停止加热";
                                break;
                            case 2:
                                res = "加热时间太长停止加热";
                                break;
                            case 0:
                                res = "取消加热成功";
                                break;
                            default:
                                res = "完成加热";
                                break;
                        }
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n"+ res +"\r\n\r\n");
                        isRequireWenDu = false;//可以开启10s查询温度
                    }
                    else if(temp0 == 5)//回复加热时间
                    {
                        int temp1 = Convert.ToInt32(arr[1], 16);//将十六进制的数转为十进制 第1个字节。为数据
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置的时间为:" + temp1 + "\r\n\r\n");
                    }
                    //richTextBox1.AppendText("当前硬件的版本号："+banBen+ "\r\n");
                    else if (temp0 == 6)
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "正在加热，用户错误操作。\r\n\r\n");
                    }
                }
                ////回复设置传值角度
                else if (s[2].Equals("2F"))
                {
                    string[] arr = data.Split('+');
                    if (arr[0].Equals("00"))
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "回应设置垂直角度=" + arr[1] + "\r\n\r\n");
                        int temp2 = parameter("CAngle", arr[1], equip.ToString().PadLeft(2, '0'));
                        if(temp2 == 1)
                        {
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "更新数据库成功" + "\r\n\r\n");
                        }
                    }
                    else
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "回应设置垂直角度= -" + arr[1] + "\r\n\r\n");
                        int temp2 = parameter("CAngle", "-" +arr[1], equip.ToString().PadLeft(2, '0'));
                        if (temp2 == 1)
                        {
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "更新数据库成功" + "\r\n\r\n");
                        }
                    }
                    //int temp0 = Convert.ToInt32(arr[0], 16);//将十六进制的数转为十进制 第0个字节。
                    //int temp1 = Convert.ToInt32(arr[1], 16);//将十六进制的数转为十进制 第1个字节。为数据
                    //if (temp0 == 0)
                    //{
                    //    new Thread(new ParameterizedThreadStart(showBox)).Start("操作成功!!!!!");
                    //}
                    //else if (temp0 == 1)
                    //{
                    //    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "设置的时间为:" + temp1 + "\r\n\r\n");
                    //}

                    

                }



                //错误
                else if (s[2].Equals("FF"))
                {

                    DateTime now = DateTime.Now;
                    //string time = now.Year + "/" + now.Month + "/" + now.Day + " " +
                    //    now.Hour.ToString().PadLeft(2, '0') + ":" + now.Minute.ToString().PadLeft(2, '0') + ":" + now.Second.ToString().PadLeft(2, '0');
                    string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    //richTextBox1.AppendText("十六进制data："+ data+"\r\n");
                    int data_int = Int32.Parse(data);
                    //richTextBox1.AppendText("十进制data：" + data_int + "\r\n");
                    if (data_int == 0)
                    {
                        try
                        {
                            string sql = "insert into binlog values('" + equip.ToString() + "', '硬件故障', '" + ins + "', '错误码：00 激光头无回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;

                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  激光头无回应,请清洁镜头后重试");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 激光头无回应\r\n\r\n");


                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }


                    }
                    else if (data_int == 1)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：01 激光头回应数据异常', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  激光头回应数据异常,请清洁镜头后重试");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 激光头回应数据异常\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");

                        }


                    }
                    else if (data_int == 2)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,角度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：02 角度计无回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  角度计无回应,请相关技术人员检测");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 角度计无回应\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 3)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：03 内部温度计没回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  内部温度计无回应,请相关技术人员检测");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 内部温度计无回应\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 4)
                    {

                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：04 与测量设备485通讯失败', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            ////string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "', '与测量设备485通讯失败', '" + ins + "', '与测量设备485通讯失败', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  与测量设备485通讯失败（干扰导致CRC校验错误）");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 与测量设备485通讯失败（干扰导致CRC校验错误）\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 5)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：05 与测量设备485通讯失败（线缆断开，发出指令无回复）', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            ////string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "', '与测量设备485通讯失败', '" + ins + "', '与测量设备485通讯失败', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  与测量设备485通讯失败（线缆断开，发出指令无回复）");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 与测量设备485通讯失败\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 6)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：06  激光头未收到回波', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            ////string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "', '与测量设备485通讯失败', '" + ins + "', '与测量设备485通讯失败', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  错误码：06  激光头未收到回波");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 错误码：06  激光头未收到回波\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 7)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：07 激光头距离太近', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            ////string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "', '与测量设备485通讯失败', '" + ins + "', '与测量设备485通讯失败', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  错误码：07 激光头距离太近");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "错误码：07 激光头距离太近\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 8)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：08 电机485通讯故障', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  错误码：08 电机485通讯故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "错误码：08 电机485通讯故障\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 9)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：09 外部温度计没回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  外部温度计无回应,请相关技术人员检测");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 外部温度计无回应\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }
                    }
                    else if (data_int == 16)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,电机正忙或正在执行其他操作', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '错误码：10 电机正忙或正在执行其他操作', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  电机正忙或正在执行其他操作");
                            //MessageBox.Show(", "软件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 电机正忙\r\n\r\n");


                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 17)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  直径', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软甲故障', '" + ins + "', '错误码：11 没配置  直径', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  直径");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  直径\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 18)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：12 没配置  高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(equip.ToString().PadLeft(2, '0') + "  没配置  高度");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  高度\r\n\r\n");
                            //MessageBox.Show(getName(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));

                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 19)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  下锥高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：13  没配置  下锥高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  下锥高度");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  下锥高度\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 20)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  安装距离到顶高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：14  没配置  安装距离到顶高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  安装距离到顶高度");
                            //MessageBox.Show(", "软件故障");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  安装距离到顶高度\r\n\r\n");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 21)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：15 没配置  密度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }

                    }
                    else if (data_int == 32)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：32 没配置  方仓边长', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓边长");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓边长\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }

                    }
                    else if (data_int == 33)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：33 没配置  方仓边宽', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓边宽");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓边宽\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }

                    }
                    else if (data_int == 34)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：34 没配置  方仓左边距', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓左边距");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  方仓左边距\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }

                    }
                    else if (data_int == 24)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：18 没配置  边距', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  边距");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  边距\r\n\r\n");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else if (data_int == 25)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '错误码：25  没配置  轴距', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  轴距");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  没配置  轴距\r\n\r\n");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else if (data_int == 64)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：40  盘库过程报错：垂直测量失败，取消盘库', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();

                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  垂直测量失败，取消盘库");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  垂直测量失败，取消盘库\r\n\r\n");


                            //进行取消盘库操作
                            if (send_ins.Count != 0)
                            {
                                //richTextBox1.AppendText("send_ins.Count != 0");
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {
                                    //richTextBox1.AppendText("send_ins[i].fac_num = " + send_ins[i].fac_num + "....equip.ToString().PadLeft(2, '0')=" + equip.ToString().PadLeft(2, '0'));
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        //richTextBox1.AppendText("要停止了");
                                        //取消盘库直接删除节点
                                        comboBox3.Items.Remove(getName(send_ins[i].fac_num));

                                        for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                                        {
                                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                                            {
                                                CalcVol_list.RemoveAt(it);
                                                break;
                                            }
                                        }

                                        if (comboBox3.Items.Count == 0)
                                        {
                                            comboBox3.Visible = false;
                                            label3.Visible = false;
                                            //groupBox2.Visible = false;
                                            progressBar2.Value = 0;
                                            label19.Text = "0";
                                        }
                                        else
                                            comboBox3.Text = comboBox3.Items[0].ToString();

                                        String name = getName(send_ins[i].fac_num);

                                        send_ins.RemoveAt(i);


                                        DateTime now1 = DateTime.Now;
                                        string sql1 = "insert into binlog values('" + equip.ToString() + "', '垂直测量失败', '" + ins + "', '客户端：盘库被取消', '" + now1.ToString("yyyy/MM/dd HH:mm:ss") + "')";
                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.command.ExecuteNonQuery();
                                        //db.Close();
                                        MySqlConn ms1 = new MySqlConn();
                                        int isR1 = ms1.nonSelect(sql1);
                                        ms.Close();
                                        //MessageBox.Show("料仓 " + name + " 被取消盘库", "提示");
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("料仓" + name + " 被取消盘库");


                                        //让文本框获取焦点，不过注释这行也能达到效果
                                        richTextBox1.Focus();
                                        //设置光标的位置到文本尾   
                                        richTextBox1.Select(richTextBox1.TextLength, 0);
                                        //滚动到控件光标处   
                                        richTextBox1.ScrollToCaret();
                                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n料仓 " + name + " 被取消盘库" + "\r\n\r\n");

                                        //return;
                                    }
                                }

                            }



                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 65)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：41 盘库过程报错：累计测量失败10个点，取消盘库', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  盘库过程报错：累计测量失败10个点，取消盘库");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  盘库过程报错：累计测量失败10个点，取消盘库\r\n\r\n");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");

                            //MessageBox.Show(", "软件故障");

                            //进行取消盘库操作
                            if (send_ins.Count != 0)
                            {
                                //richTextBox1.AppendText("send_ins.Count != 0");
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {
                                    //richTextBox1.AppendText("send_ins[i].fac_num = " + send_ins[i].fac_num + "....equip.ToString().PadLeft(2, '0')=" + equip.ToString().PadLeft(2, '0'));
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        //richTextBox1.AppendText("要停止了");
                                        //取消盘库直接删除节点
                                        comboBox3.Items.Remove(getName(send_ins[i].fac_num));

                                        for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                                        {
                                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                                            {
                                                CalcVol_list.RemoveAt(it);
                                                break;
                                            }
                                        }

                                        if (comboBox3.Items.Count == 0)
                                        {
                                            comboBox3.Visible = false;
                                            label3.Visible = false;
                                            //groupBox2.Visible = false;
                                            progressBar2.Value = 0;
                                            label19.Text = "0";
                                        }
                                        else
                                            comboBox3.Text = comboBox3.Items[0].ToString();

                                        String name = getName(send_ins[i].fac_num);

                                        send_ins.RemoveAt(i);


                                        DateTime now1 = DateTime.Now;
                                        string sql1 = "insert into binlog values('" + equip.ToString() + "', '累计无法测量点10个', '" + ins + "', '客户端：盘库被取消', '" + now1.ToString("yyyy/MM/dd HH:mm:ss") + "')";
                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.command.ExecuteNonQuery();
                                        //db.Close();
                                        MySqlConn ms1 = new MySqlConn();
                                        int isR1 = ms1.nonSelect(sql1);
                                        ms.Close();
                                        //MessageBox.Show("料仓 " + name + " 被取消盘库", "提示");
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("料仓" + name + " 被取消盘库");


                                        //让文本框获取焦点，不过注释这行也能达到效果
                                        richTextBox1.Focus();
                                        //设置光标的位置到文本尾   
                                        richTextBox1.Select(richTextBox1.TextLength, 0);
                                        //滚动到控件光标处   
                                        richTextBox1.ScrollToCaret();
                                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n料仓 " + name + " 被取消盘库" + "\r\n\r\n");

                                        //return;
                                    }
                                }

                            }
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 66)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：42 盘库结果报错：负值过大', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   盘库结果报错：负值过大\r\n\r\n");
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "   盘库结果报错：负值过大");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");


                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 67)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：43 超过满仓体积', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  超过满仓体积");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   超过满仓体积\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                   


                    else if (data_int == 68)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：44 错误数据过多', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  错误数据过多");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   错误数据过多\r\n\r\n");
                            //MessageBox.Show(", "软件故障");


                            //进行取消盘库操作
                            if (send_ins.Count != 0)
                            {
                                //richTextBox1.AppendText("send_ins.Count != 0");
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {
                                    //richTextBox1.AppendText("send_ins[i].fac_num = " + send_ins[i].fac_num + "....equip.ToString().PadLeft(2, '0')=" + equip.ToString().PadLeft(2, '0'));
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        //richTextBox1.AppendText("要停止了");
                                        //取消盘库直接删除节点
                                        comboBox3.Items.Remove(getName(send_ins[i].fac_num));

                                        for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                                        {
                                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                                            {
                                                CalcVol_list.RemoveAt(it);
                                                break;
                                            }
                                        }

                                        if (comboBox3.Items.Count == 0)
                                        {
                                            comboBox3.Visible = false;
                                            label3.Visible = false;
                                            //groupBox2.Visible = false;
                                            progressBar2.Value = 0;
                                            label19.Text = "0";
                                        }
                                        else
                                            comboBox3.Text = comboBox3.Items[0].ToString();

                                        String name = getName(send_ins[i].fac_num);

                                        send_ins.RemoveAt(i);


                                        DateTime now1 = DateTime.Now;
                                        string sql1 = "insert into binlog values('" + equip.ToString() + "', '硬件故障', '" + ins + "', '客户端：盘库被取消', '" + now1.ToString("yyyy/MM/dd HH:mm:ss") + "')";
                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.command.ExecuteNonQuery();
                                        //db.Close();
                                        MySqlConn ms1 = new MySqlConn();
                                        int isR1 = ms1.nonSelect(sql1);
                                        ms.Close();
                                        //MessageBox.Show("料仓 " + name + " 被取消盘库", "提示");
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("料仓" + name + " 被取消盘库");


                                        //让文本框获取焦点，不过注释这行也能达到效果
                                        richTextBox1.Focus();
                                        //设置光标的位置到文本尾   
                                        richTextBox1.Select(richTextBox1.TextLength, 0);
                                        //滚动到控件光标处   
                                        richTextBox1.ScrollToCaret();
                                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n料仓 " + name + " 被取消盘库" + "\r\n\r\n");

                                        //return;
                                    }
                                }

                            }




                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 69)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：45 电机水平中间定位失败', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  电机水平中间定位失败");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   电机水平中间定位失败\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }catch(Exception ee)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(ee.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else if (data_int == 70)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：46 水平定位通讯失败', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  水平定位通讯失败");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   水平定位通讯失败\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (Exception ee)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(ee.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }
                    else if (data_int == 71)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误码：47 料仓物料可能结拱', '" + time + "')";
                            MySqlConn ms = new MySqlConn();
                            int isR = ms.nonSelect(sql);
                            ms.Close();
                            new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  料仓物料可能结拱");
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "   料仓物料可能结拱\r\n\r\n");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (Exception ee)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(ee.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                    }

                }
            }
            catch (Exception exc)
            {
                richTextBox1.AppendText(exc.ToString() + "\r\n");
            }

        }



        

        //对显示的料仓排序，根据不同的box
        private void SortCheckedList(CheckedListBox checkedListBox)
        {
           
            List<string> SortList = new List<string>();
            List<string> isCheckedName = new List<string>();
            for (int i = 0; i < checkedListBox.Items.Count; i++)
            {
                ////获取索引i是否勾选
                //checkedListBox1.GetItemChecked[i]
                ////设置索引i为勾选
                //  checkedListBox1.SetItemChecked(i, true);
                SortList.Add(checkedListBox.Items[i].ToString());
                if (checkedListBox.GetItemChecked(i))//判断控件是否是被勾选的。是的话将名字加入到集合中
                {
                    isCheckedName.Add(checkedListBox.Items[i].ToString());
                }
            }
            SortList.Sort();
            checkedListBox.Items.Clear();
            for (int i = 0; i < SortList.Count; i++)
            {
               checkedListBox.Items.Add(SortList[i]);
            }
            for (int i = 0; i < checkedListBox.Items.Count; i++)
            {
                if (isCheckedName.Contains(SortList[i]))//判断在选中状态集合中是否包含这个名字，是的话就勾选
                {
                    //richTextBox1.AppendText("料仓" + SortList[i] + "应该被勾选,i的值" + i + "\t\n\r\n");
                    checkedListBox.SetItemChecked(i, true);
                    
                }
                else
                {
                    checkedListBox.SetItemCheckState(i, CheckState.Indeterminate);
                }
            }

        }

        private void setSign(object obj)
        {
            //在接收到数据后，经过处理数据函数，已经将料仓编号和指令提取出来，并用‘+’分隔开
            //在这只需要按照‘+’切割就可以得到料仓编号和指令
            try
            {
                string[] receive = ((string)obj).Split('+');
                //show("receive 1" + receive[0] + "receive 1" + receive[1]);
                //接收到的设备地址转为十进制
                int equip_receive = Int32.Parse(receive[0], System.Globalization.NumberStyles.HexNumber);
                //接收到的指令码转为十进制
                int instruction_receive = Int32.Parse(receive[1], System.Globalization.NumberStyles.HexNumber);
                //richTextBox1.AppendText(equip_receive.ToString()+"  "+instruction_receive.ToString()+"-----\r\n");
                list_mutex.WaitOne();
                for (int i = 0; i < list_status.Count; i++)
                {
                    //状态链表中的设备地址转化为十进制
                    int equip_list = Int32.Parse(list_status[i].fac_num, System.Globalization.NumberStyles.HexNumber);
                    //状态链表中的指令码转化为十进制
                    int instruction_list = Int32.Parse(list_status[i].ins_answer.ToString(), System.Globalization.NumberStyles.HexNumber);

                    if (equip_list == equip_receive && instruction_list == instruction_receive)
                    {//只是将状态标志改为true，并不执行删除操作，删除操作统一在遍历链表的定时器中删除
                        list_status[i].sign_answer = true;
                    }

                }
            }catch(Exception e)
            {
               show("" + e);
            }

            list_mutex.ReleaseMutex();

        }
        /// <summary>
        /// 退出登录按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 退出登录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
            DialogResult dr = MessageBox.Show("确认要退出?", "提示", messButton);
            if (dr == DialogResult.OK)
            {
                curr_user.logout();
                if (form_login == null)
                    form_login = new Form_Login();
                form_login.Show();
                //Form_Login form_Login = new Form_Login();
                //form_Login.Show();
                serialPort1.Close();

                //form_login.Show();
                flag_threadout = 0;
                t_status.Enabled = false;
                Thread.Sleep(300);
                //while (sendIns_queue.Count != 0 || list_status.Count != 0) ;
                this.Dispose();
            }

        }

        private void show_login(object obj)
        {
            Application.Run(new Form_Login());
            this.Close();

        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            //asc.controlAutoSize(this);
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;  //不显示在系统任务栏
                notifyIcon1.Visible = true;  //托盘图标可见

            }
        }

        /// <summary>
        /// 多选框选中空白时检测
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (checkedListBox1.IndexFromPoint(
            checkedListBox1.PointToClient(System.Windows.Forms.Cursor.Position).X,
            checkedListBox1.PointToClient(System.Windows.Forms.Cursor.Position).Y) == -1)
            {
                e.NewValue = e.CurrentValue;
            }
        }


        /// <summary>
        /// checkoutlistbox右键点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkedListBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (sendIns_queue.Count != 0)
            {

            }
            if (e.Button == MouseButtons.Right)
            {
                int posindex = checkedListBox1.IndexFromPoint(new Point(e.X, e.Y));
                checkedListBox1.ContextMenuStrip = null;
                checkedListBox1.SelectedIndex = posindex;
                contextMenuStrip1.Show(checkedListBox1, new Point(e.X, e.Y));
                if (curr_user.admin.Equals("1"))
                {
                    if (checkedListBox1.Text.Equals(""))
                    {
                        test2ToolStripMenuItem.Visible = false;
                        刷新ToolStripMenuItem.Visible = true;
                        if (checkedListBox1.SelectedItems.Count == 0)
                        {
                            testToolStripMenuItem.Visible = false;
                        }
                        else
                            testToolStripMenuItem.Visible = true;

                        显示数据信息ToolStripMenuItem.Visible = true;
                        显示参数信息ToolStripMenuItem.Visible = true;
                        添加料仓ToolStripMenuItem.Visible = true;
                        更改名称ToolStripMenuItem.Visible = false;
                        显示盘库时间ToolStripMenuItem.Visible = true;
                        重试连接ToolStripMenuItem.Visible = false;
                    }
                    else
                    {
                        test2ToolStripMenuItem.Visible = true;
                        刷新ToolStripMenuItem.Visible = true;
                        if (checkedListBox1.CheckedItems.Count == 0)
                        {
                            testToolStripMenuItem.Visible = false;
                        }
                        else
                            testToolStripMenuItem.Visible = true;
                        显示数据信息ToolStripMenuItem.Visible = true;
                        显示参数信息ToolStripMenuItem.Visible = true;
                        添加料仓ToolStripMenuItem.Visible = true;
                        更改名称ToolStripMenuItem.Visible = true;
                        显示盘库时间ToolStripMenuItem.Visible = true;
                        重试连接ToolStripMenuItem.Visible = false;
                    }

                }
                else
                {
                    if (checkedListBox1.Text.Equals(""))
                    {
                        test2ToolStripMenuItem.Visible = false;
                        刷新ToolStripMenuItem.Visible = true;
                        testToolStripMenuItem.Visible = false;
                        显示数据信息ToolStripMenuItem.Visible = true;
                        显示参数信息ToolStripMenuItem.Visible = true;
                        添加料仓ToolStripMenuItem.Visible = false;
                        更改名称ToolStripMenuItem.Visible = false;
                        显示盘库时间ToolStripMenuItem.Visible = true;
                        重试连接ToolStripMenuItem.Visible = false;
                    }
                    else
                    {
                        test2ToolStripMenuItem.Visible = false;
                        刷新ToolStripMenuItem.Visible = true;
                        testToolStripMenuItem.Visible = false;
                        显示数据信息ToolStripMenuItem.Visible = true;
                        显示参数信息ToolStripMenuItem.Visible = true;
                        添加料仓ToolStripMenuItem.Visible = false;
                        更改名称ToolStripMenuItem.Visible = false;
                        显示盘库时间ToolStripMenuItem.Visible = true;
                        重试连接ToolStripMenuItem.Visible = false;
                    }
                }

            }
            //checkedListBox1.Refresh();
        }


        /// <summary>
        /// 右键点击弹出删除按钮功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.CheckedItems.Count != 0)
            {
                int del = 0;
                MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                DialogResult dr = MessageBox.Show("确认要删除" + checkedListBox1.CheckedItems.Count + "个料仓吗？", "提示", messButton);
                if (dr == DialogResult.OK)
                {
                    int d = 0;
                    for (int i = checkedListBox1.Items.Count - 1; i >= 0; i--)
                    {
                        //richTextBox1.AppendText("count:  " + checkedListBox1.Items.Count + "\n");
                        if (checkedListBox1.GetItemChecked(i))
                        {
                            string fac_name = checkedListBox1.Items[i].ToString();
                            string id = selectID(fac_name);
                            string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                            aim_ins.Enqueue(new FacMessage(ins_num++, "00", id, false, TIME_WAIT, "删除料仓", "delete", s_Produce));
                            sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, TIME, "删除料仓前查询状态", data_search, 3, s_Produce));

                        }
                    }

                }
            }

        }

        /// <summary>
        /// 删除料仓函数实现
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private int delete(string str)
        {
            int succ = 0;//表示成功删除多少个表中的数据

            string id = selectID(str);
            try
            {
                string sql = "delete from bindata where BinID = '" + id + "'";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                MySqlConn ms = new MySqlConn();
                int isR = ms.nonSelect(sql);
                ms.Close();
                if (isR > 0)
                {
                    succ += 1;
                }

                sql = "delete from binauto where BinName = '" + str + "'";
                MySqlConn ms1 = new MySqlConn();
                int isR1 = ms1.nonSelect(sql);
                ms1.Close();
                if (isR1 > 0)
                {
                    succ += 1;
                }
                sql = "delete from bininfo where BinName = '" + str + "'";
                MySqlConn ms2 = new MySqlConn();
                int isR2 = ms2.nonSelect(sql);
                ms2.Close();
                if (isR2 > 0)
                {
                    succ += 1;
                }

                return succ;

            }
            catch (Exception exc)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                //MessageBox.Show("请检查数据库设置", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("");
                return succ;
            }
        }

        /// <summary>
        /// 串口设置按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 串口选择ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            oper_ins.Clear();
            button8.PerformClick();
            groupBox_serial.Visible = true;
            comboBox1.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length != 0)
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    comboBox1.Items.Add(ports[i]);
                }
                comboBox1.Text = comboBox1.Items[0].ToString();
                comboBox2.Text = comboBox2.Items[0].ToString();
            }

        }

        /// <summary>
        /// 确定串口设置按钮点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            serialPort1.Close();
            try
            {
                //serialPort1 = new SerialPort(this.components);
                serialPort1.PortName = comboBox1.Text;
                //this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
                serialPort1.BaudRate = Int32.Parse(comboBox2.Text);
                serialPort1.Open();
                //Thread thread_takeData = new Thread(takeData);
                //thread_takeData.Start();

                port_isopen = 1;
                string path = System.Windows.Forms.Application.StartupPath;
                FileStream fs = new FileStream(path + "\\serialPort.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(serialPort1.PortName + "+" + serialPort1.BaudRate);
                sw.Flush();
                sw.Close();
                fs.Close();

                new Thread(new ParameterizedThreadStart(showBox)).Start(serialPort1.PortName + "   " + serialPort1.BaudRate.ToString());
                //MessageBox.Show(, "当前串口设置");
                groupBox_serial.Visible = false;
            }
            catch (Exception exc)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择有效的串口");
                //MessageBox.Show("", "提示");
            }


        }



        /// <summary>
        /// 显示详细信息按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 显示详细信息ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int SW = SystemInformation.WorkingArea.Width;
            int SH = SystemInformation.WorkingArea.Height;
            double SW_percent = (double)SW / (double)1366;
            double SH_percent = (double)SH / (double)738;
            bdnInfo.Visible = true;
            groupBox_pic.Visible = false;
            dataGridView1.Visible = true;
            button8.Location = new Point((int)((886) * SW_percent), button8.Location.Y);
            button8.Visible = true;
            //Thread thread_select = new Thread(new ParameterizedThreadStart(select));
            //thread_select.Start("config");
            select("config");
        }
        //选项数据库内容显示函数
        private void select(object obj)
        {
            string str = (string)obj;
            //if (checkedListBox1.CheckedItems.Count != 0 && checkedListBox2.CheckedItems.Count != 0)
            //{
            if (str.Equals("config"))
            {
                toolStripButton8.Visible = false;
                dataGridView1.DataSource = null;
                string sql = "select BinID,BinName,Diameter,CylinderH,PyramidH,Density,Margin,BinTop,Wheelbase,Angle,KValue,Bvalue,CAngle,(case type  when 0 then '侧置直径' when 1 then '侧置平扫' when 2 then '顶置直径' when 3 then '顶置平扫' when 4 then '侧置方仓'when 5 then '顶置方仓'else '未知类型' end) as type,HAngle,Fbian,Fkuan,Fzuobian,UpperH from bininfo where BinName=''";
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    if (checkedListBox1.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox1.Items[i].ToString() + "'";
                    }
                }
                for (int i = 0; i < checkedListBox2.Items.Count; i++)
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox2.Items[i].ToString() + "'";
                    }
                }
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;


                //db.sda.SelectCommand = db.command;
                ////DataTable dt = new DataTable();
                //DataSet ds = new DataSet();
                //db.sda.Fill(ds, "ds");
                ////dataGridView1.DataSource = dt;
                //dtInfo = ds.Tables[0];
                MySqlConn ms = new MySqlConn();
                MySqlConnection conn = ms.GetConn();
                MySqlDataAdapter sda = new MySqlDataAdapter(sql, conn);//获取数据表
                DataTable table = new DataTable();
                DataSet ds = new DataSet();
                
                sda.Fill(table);//填充数据库
                dtInfo = table;

                InitDataSet();
                dataGridView1.Columns[0].HeaderCell.Value = "料仓编号";
                dataGridView1.Columns[1].HeaderCell.Value = "料仓名称";
                dataGridView1.Columns[2].HeaderCell.Value = "仓筒直径";
                dataGridView1.Columns[3].HeaderCell.Value = "仓筒高度";
                dataGridView1.Columns[4].HeaderCell.Value = "下锥高度";
                dataGridView1.Columns[5].HeaderCell.Value = "物料密度";


                dataGridView1.Columns[6].HeaderCell.Value = "边距";
                dataGridView1.Columns[7].HeaderCell.Value = "顶高";
                dataGridView1.Columns[8].HeaderCell.Value = "轴距";
                dataGridView1.Columns[9].HeaderCell.Value = "步进角";
                dataGridView1.Columns[10].HeaderCell.Value = "K值";
                dataGridView1.Columns[11].HeaderCell.Value = "B值";
                dataGridView1.Columns[12].HeaderCell.Value = "垂直角";
                dataGridView1.Columns[13].HeaderCell.Value = "类型";
                dataGridView1.Columns[14].HeaderCell.Value = "水平角";
                dataGridView1.Columns[15].HeaderCell.Value = "方仓边长";
                dataGridView1.Columns[16].HeaderCell.Value = "方仓边宽";
                dataGridView1.Columns[17].HeaderCell.Value = "方仓左边距";
                dataGridView1.Columns[18].HeaderCell.Value = "上锥高度";
                ms.Close();
            }
            else if (str.Equals("data"))
            {
                toolStripButton8.Visible = false;
                dataGridView1.DataSource = null;
                string sql = "select BinName, Volume, Weight, Temp, Hum,MiDu,(case Quality  when 1 then '数据可靠' when 2 then '数据不一定可靠' when 3 then '数据不可靠' else '未评测' end) as Quality ,DateTime,Algorithm from bindata, bininfo where bindata.BinID in (" +
                "select BinID from bininfo where BinName=''";
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    if (checkedListBox1.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox1.Items[i].ToString() + "'";
                    }
                }
                for (int i = 0; i < checkedListBox2.Items.Count; i++)
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox2.Items[i].ToString() + "'";
                    }
                }
                sql += ") AND bininfo.BinID=bindata.BinID  order by DateTime desc";

                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;

                //db.sda.SelectCommand = db.command;
                ////DataTable dt = new DataTable();
                ////sda.Fill(dt);
                ////dataGridView1.DataSource = dt;
                //DataSet ds = new DataSet();//数据集
                //db.sda.Fill(ds, "ds");
                ////dataGridView1.DataSource = dt;
                //dtInfo = ds.Tables[0];
                MySqlConn ms = new MySqlConn();
                MySqlConnection conn = ms.GetConn();
                MySqlDataAdapter sda = new MySqlDataAdapter(sql, conn);//获取数据表
                DataTable table = new DataTable();
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;


                //db.sda.SelectCommand = db.command;
                //DataTable dt = new DataTable();
                DataSet ds = new DataSet();
                sda.Fill(table);//填充数据库
                //db.sda.Fill(ds, "ds");
                //dataGridView1.DataSource = dt;
                dtInfo = table;


                InitDataSet();
                dataGridView1.Columns[0].HeaderCell.Value = "料仓名称";
                dataGridView1.Columns[1].HeaderCell.Value = "物料体积(m³)";
                dataGridView1.Columns[2].HeaderCell.Value = "物料重量(吨)";
                dataGridView1.Columns[3].HeaderCell.Value = "料仓温度(℃)";
                dataGridView1.Columns[4].HeaderCell.Value = "料仓湿度(%RH)";

                dataGridView1.Columns[7].HeaderCell.Value = "时间日期";

                dataGridView1.Columns[8].HeaderCell.Value = "盘库类型";
                dataGridView1.Columns[8].Visible = false;
                dataGridView1.Columns[6].HeaderCell.Value = "质量评测";
                dataGridView1.Columns[5].HeaderCell.Value = "物料密度（吨/立方米）";
                //dataGridView1.Columns[5].Width = 70;
                dataGridView1.Columns[7].DefaultCellStyle.Format = "yy/MM/dd HH:mm:ss";
                dataGridView1.Columns[7].Width = 180;

                ms.Close();
            }
            else if (str.Equals("time"))
            {
                toolStripButton8.Visible = true;

                dataGridView1.DataSource = null;
                string sql = "select * from binauto where BinID in (" +
                "select BinID from bininfo where BinName=''";
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    if (checkedListBox1.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox1.Items[i] + "'";
                    }
                }
                for (int i = 0; i < checkedListBox2.Items.Count; i++)
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        sql += " or BinName='" + checkedListBox2.Items[i].ToString() + "'";
                    }
                }
                sql += ") order by Time asc";


                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;

                //db.sda.SelectCommand = db.command;
                ////DataTable dt = new DataTable();
                ////sda.Fill(dt);
                ////dataGridView1.DataSource = dt;
                //DataSet ds = new DataSet();
                //db.sda.Fill(ds, "ds");
                ////dataGridView1.DataSource = dt;
                //dtInfo = ds.Tables[0];
                MySqlConn ms = new MySqlConn();
                MySqlConnection conn = ms.GetConn();
                MySqlDataAdapter sda = new MySqlDataAdapter(sql, conn);//获取数据表
                DataTable table = new DataTable();
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;


                //db.sda.SelectCommand = db.command;
                //DataTable dt = new DataTable();
                DataSet ds = new DataSet();
                sda.Fill(table);//填充数据库
                //db.sda.Fill(ds, "ds");
                //dataGridView1.DataSource = dt;
                dtInfo = table;

                InitDataSet();
                dataGridView1.Columns[0].HeaderCell.Value = "料仓编号";
                dataGridView1.Columns[1].HeaderCell.Value = "时间";
                dataGridView1.Columns[2].HeaderCell.Value = "日期";
                dataGridView1.Columns[3].HeaderCell.Value = "料仓名称";
                dataGridView1.Columns[4].HeaderCell.Value = "操作类型";
                ms.Close();
            }


        }
        //　RowContextMenuStripNeeded事件处理方法 。历史记录表中DataGridView中的右击点击事件
        private void DataGridView1_CellMouseDown(Object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right && e.ColumnIndex == -1 && !dataGridView1.Rows[e.RowIndex].Cells["Volume"].Value.ToString().Equals(""))
                {
                    rowDateTime = "";
                    rowBinName = "";
                    rowBinVol = "";
                    rowBinState = "";
                    string Volume = dataGridView1.Rows[e.RowIndex].Cells["Volume"].Value.ToString();
                    string time = dataGridView1.Rows[e.RowIndex].Cells["DateTime"].Value.ToString();
                    string state = dataGridView1.Rows[e.RowIndex].Cells["Algorithm"].Value.ToString();


                    rowDateTime = time;
                    rowBinName = dataGridView1.Rows[e.RowIndex].Cells["BinName"].Value.ToString();
                    rowBinVol = Volume;

                    rowBinState = state;

                    contextMenuStrip3.Show(MousePosition.X, MousePosition.Y);//右击菜单出现

                    //System.Text.StringBuilder messageBoxCS = new System.Text.StringBuilder();
                    //messageBoxCS.AppendFormat("{0} = {1}", "ColumnIndex", e.ColumnIndex);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "RowIndex", e.RowIndex);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "Button", e.Button);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "Clicks", e.Clicks);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "X", e.X);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "Y", e.Y);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "Delta", e.Delta);
                    //messageBoxCS.AppendLine();
                    //messageBoxCS.AppendFormat("{0} = {1}", "Location", e.Location);
                    //messageBoxCS.AppendLine();
                    //MessageBox.Show(messageBoxCS.ToString(), "CellMouseDown Event");
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("此页面不能查看历史数据分析，请跳转到历史数据页面！");
            }


        }
        /// <summary>
        /// 退出表格按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button8_Click(object sender, EventArgs e)
        {
            groupBox_pic.Visible = false;
            dataGridView1.Visible = false;
            button8.Visible = false;
            bdnInfo.Visible = false;
            groupBox_pic.Controls.Clear();
   
        }


        /// <summary>
        /// 显示数据信息按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 显示数据信息ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int SW = SystemInformation.WorkingArea.Width;
            int SH = SystemInformation.WorkingArea.Height;
            double SW_percent = (double)SW / (double)1366;
            double SH_percent = (double)SH / (double)738;
            bdnInfo.Visible = true;
            groupBox_pic.Visible = false;
            dataGridView1.Visible = true;
            button8.Visible = true;
            button8.Location = new Point((int)(886 * SW_percent), button8.Location.Y);
            select("data");
        }
        /// <summary>
        /// 添加料仓输入框按键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (toolStripTextBox1.Text.Equals("") != true)
                {
                    try
                    {
                        string data = Data.Data(comboBox4.Text, toolStripTextBox1.Text, "00", "0000");
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "01", toolStripTextBox1.Text, false, TIME, "查询测试/添加料仓功能", data, s_Produce));

                        //向状态链表中添加状态信息，以此为例，接收到0x01指令时表示数据传输成功
                        if (ins_num > 2000)
                        {
                            ins_num = 1;
                        }
                    }
                    catch (Exception exc)
                    {
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请输入正确的料仓编号");
                        //MessageBox.Show("", "提示");
                    }

                    contextMenuStrip1.Visible = false;
                }
            }
        }

        /// <summary>
        /// 盘库按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            sendIns_queue.Clear();
            oper_ins.Clear();
            //richTextBox1.AppendText("盘库 oper_ins.Clear(); \r\n\r\n");
            list_status.Clear();

            //加上先发送指令查询料仓设备状态，然后检索当前状态列表


            if (checkedListBox1.CheckedItems.Count == 0)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓进行盘库");
                //MessageBox.Show("", "提示");
                return;
            }
            Queue<FacMessage> ins_queue = new Queue<FacMessage>();
            for (int i = checkedListBox1.Items.Count - 1; i >= 0; i--)
            {
                //bool isoperating = false;
                if (checkedListBox1.GetItemChecked(i))
                {
                    //MessageBox.Show(checkedListBox1.Items[i].ToString());
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                    string d = Data.Data(comboBox4.Text, id, "18", "0000");
                    aim_ins.Enqueue(new FacMessage(ins_num++, "13", id, false, 3, "盘库", d, s_Produce));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "手动盘库前查询状态", data_search, 3, s_Produce));
                    //richTextBox1.AppendText("发送盘库查询状态指令\r\n\r\n");
                    //向发送链表中添加此指令，但是不发送这条指令，发送的是查询指令

                }
            } //end for
        }

        /// <summary>
        /// 定时盘库按钮点击时间功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {//相当于显示时间窗体
            //创建时间线程，并保持一直打开状态

            Thread time = new Thread(method);
            time.Start();
        }

        /// <summary>
        /// 委托时间功能实现
        /// </summary>
        /// <param name="obj"></param>
        private void method(object obj)
        {
            MethodInvoker MethInvo = new MethodInvoker(showtime);
            BeginInvoke(MethInvo);

        }

        /// <summary>
        /// 显示计时窗体
        /// </summary>
        private void showtime()
        {
            f.Visible = true;
            f.Activate();
        }


        /// <summary>
        /// 菜单栏的定时盘库
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            button2.PerformClick();
        }


        /// <summary>
        /// 关闭窗体时将计时器关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
            f.Close();
        }

        /// <summary>
        /// 先检测这个定时器的屏蔽信号（自定义timer1_mask）是否为1,
        /// 然后检测是否更改了定时时间，比如添加删除定时时间
        /// 然后再对内存中的盘库时间列表进行遍历
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {//自动盘库定时器
            //SendMessage(Handle, WM_SYSCOMMAND, SC_MONITORPOWER, -1); //打开显示器;
            SystemSleepManagement.NoCloseScreen();

            if (timer1_mask == 1)
                return;
            back_complet--;
            if (back_complet == 0)
            {
                new Thread(save_backdata).Start();//回传信息的保存
            }

            if (f.change == 1)//是否修改了定时时间数据库，是的话重新加载数据库中的定时信息
            {
                Thread loadAuto = new Thread(LoadAuto);
                loadAuto.Start();
            }

            DateTime now = DateTime.Now;//获取当前时间

            try
            {
                for (int i = 0; i < Auto_list.Count; i++)
                {
                    //if (db.Dr["Date"].ToString().Equals("0"))
                    if (Auto_list[i].date.Equals("0"))//如果设置日期为0，说明是每天都进行的操作
                    {
                        DateTime time = DateTime.ParseExact(Auto_list[i].time.ToString(), "HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);//获取操作时间

                        if (time.Hour == now.Hour && time.Minute == now.Minute)//已经到达这个时间后
                        {
                            if (now.Second <= 10 && 0 == Auto_list[i].state)//Auto_list[i].state = 0 时才进行操作
                            {
                                if (Auto_list[i].operation.ToString().Equals("料仓盘库"))
                                {
                                    int IsSend = time_on(Auto_list[i].fac_num);//是否将指令发出,发出为1
                                    if (1 == IsSend)
                                        Auto_list[i].state = 1;
                                }
                                else if (Auto_list[i].operation.ToString().Equals("定时加热5分钟"))
                                {
                                    //MessageBox.Show("到达加热时间，需要开始加热了" );

                                    int IsSend = time_on_clean(Auto_list[i].fac_num.ToString(), Auto_list[i].operation.ToString());//是否将指令发出,发出为1
                                    if (1 == IsSend)
                                        Auto_list[i].state = 1;
                                }
                                else
                                {
                                    int IsSend = time_on_clean(Auto_list[i].fac_num.ToString(), Auto_list[i].operation.ToString());//是否将指令发出,发出为1
                                    if (1 == IsSend)
                                        Auto_list[i].state = 1;
                                }
                            }
                            if (now.Second > 40 && now.Second < 50)
                                Auto_list[i].state = 0;

                        }
                    }
                    else
                    {
                        DateTime time = DateTime.ParseExact(Auto_list[i].time.ToString(), "HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
                        if (Auto_list[i].date.PadLeft(2, '0').Equals(now.Day.ToString().PadLeft(2, '0')))
                        {
                            if (time.Hour == now.Hour && time.Minute == now.Minute)
                            {
                                if (now.Second <= 10 && 0 == Auto_list[i].state)
                                {
                                    if (Auto_list[i].operation.ToString().Equals("料仓盘库"))
                                    {
                                        int IsSend = time_on(Auto_list[i].fac_num.ToString());//是否将指令发出,发出为1
                                        if (1 == IsSend)
                                            Auto_list[i].state = 1;
                                    }
                                    else
                                    {
                                        int IsSend = time_on_clean(Auto_list[i].fac_num.ToString(), Auto_list[i].operation.ToString());//是否将指令发出,发出为1
                                        if (1 == IsSend)
                                            Auto_list[i].state = 1;
                                    }
                                }
                                if (now.Second > 40 && now.Second < 50)
                                    Auto_list[i].state = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception exc) { }


        }

        private int time_on_clean(string p1, string p2)
        {
            string id = p1;
            //发送指令查询料仓设备状态

            if (id.Length == 0)
                return 0;
            if (p2.Equals("镜头除尘"))
            {
                string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                string data = Data.Data(comboBox4.Text, id, "22", "0000");
                aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除尘", data, 0));
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "自动镜头除尘前查询状态", data_search, 3, s_Produce));
            }
            else if (p2.Equals("镜头除湿"))
            {
                string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                string data = Data.Data(comboBox4.Text, id, "22", "0001");
                aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除湿", data, 0));
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "自动镜头除湿前查询状态", data_search, 3, s_Produce));
            }
            else if (p2.Equals("定时加热5分钟"))//开启一分钟加热
            {
                string data_search = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0405");//模式4：按照时间加热。 加热5分钟
                //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "2E", id, false, 3, "按时间加热", data_search, 3, s_Produce));


                //string id = selectID(checkedListBox1.Items[i].ToString());
                //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0000");//按照设置温度加热.第二个字节加温度
                //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0401");//按时间加热。第二个字节加时间，加热时间1分钟
                //richTextBox1.AppendText("发送加热指令:" + data + "\r\n");
                //sendIns_queue.Enqueue(new FacMessage(ins_num++, "2E", id, false, TIME, "加热", data, s_Produce));//指令为返回的代码
            }
            return 1;
        }

        private void save_backdata(object obj)
        {
            file_mutex.WaitOne();
            {
                try
                {
                    string path = System.Windows.Forms.Application.StartupPath;
                    if (Directory.Exists(path + "\\back_data") == false)
                        Directory.CreateDirectory(path + "\\back_data");
                    FileStream fs = new FileStream(path + "\\back_data\\" + backdata_path, FileMode.Create | FileMode.Append);
                    if (fs != null)
                    {
                        StreamWriter sw = new StreamWriter(fs);
                        for (int i = 0; i < 200; i++)
                        {
                            if (backdata[i].length.Equals("") == false)
                            {
                                sw.Write(i.ToString() + " " + backdata[i].length + " " + backdata[i].schedule + "\r\n");

                            }
                        }
                        sw.Flush();
                        sw.Close();
                        fs.Close();
                        recv_num = 0;
                        Invoke(new MethodInvoker(delegate ()
                        {
                            comboBox3.Visible = true;
                            label3.Visible = true;
                            groupBox2.Visible = true;
                            //让文本框获取焦点，不过注释这行也能达到效果
                            richTextBox1.Focus();
                            //设置光标的位置到文本尾   
                            richTextBox1.Select(richTextBox1.TextLength, 0);
                            //滚动到控件光标处   
                            richTextBox1.ScrollToCaret();
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n回传数据成功\r\n\r\n");
                        }));

                    }
                    else
                    {
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n文件打开失败\r\n\r\n");
                    }
                }catch(Exception eee)
                {
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n文件打开失败\r\n"+ eee.ToString()+ "\r\n");
                }
                
            }

            port_mask = 0;
            file_mutex.ReleaseMutex();
        }


        private int time_on(string obj)
        {//定时盘库，到达设定时间自动发送盘库指令
            string id = (string)obj;
            //发送指令查询料仓设备状态

            if (id.Length == 0)
                return 0;

            string d = Data.Data(comboBox4.Text, id, "18", "0000");

            aim_ins.Enqueue(new FacMessage(0, "13", id, false, TIME_WAIT, "盘库", d, 3, s_Produce));
            string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
            //向发送链表中添加此指令，但是不发送这条指令，发送的是查询指令
            sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, TIME_WAIT, "自动盘库前查询状态", data_search, 3, s_Produce));

            if (ins_num > 2000)
            {
                ins_num = 1;
            }
            return 1;
        }

        /// <summary>
        /// 根据BinName找BinID
        /// </summary>
        /// <returns></returns>
        private string selectID(string str)
        {
            try//嵌套try catch 只要内层捕捉了错误，执行了catch，外层就不会再报错
            {
                string ret = "  ";
                if (SqlConnect == 1)
                {
                    MySqlConn msc2 = new MySqlConn();
                    string sql = "select * from bininfo where BinName = '" + str + "'";
                    MySqlDataReader rd = msc2.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        ret = rd["BinID"].ToString();
                    }
                    rd.Close();
                    msc2.Close();
                  
                    return ret;
                }
                else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                    return "";
                }
            }
            catch(Exception ee)
            {
                MessageBox.Show("selectID函数出错,错误：" + ee.ToString());
                return "";
            }
            
        }

        private int selectID1(string str)
        {
            try//嵌套try catch 只要内层捕捉了错误，执行了catch，外层就不会再报错
            {
                int ret=0;
                if (SqlConnect == 1)
                {
                    MySqlConn msc2 = new MySqlConn();
                    string sql = "select * from bininfo where BinName = '" + str + "'";
                    MySqlDataReader rd = msc2.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        ret = int.Parse(rd["BinID"].ToString());
                    }
                    rd.Close();
                    msc2.Close();
                    //MessageBox.Show("ret" + ret);
                    return ret;
                }
                else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                    return 0;
                }
            }
            catch (Exception ee)
            {
                MessageBox.Show("selectID函数出错,错误：" + ee.ToString());
                return -1;
            }

        }

        /// <summary>
        /// 修改数据库参数函数，当硬件修改成功之后就进行本地数据库的修改
        /// </summary>
        private int parameter(string type, string num, string binid)
        {
            int value = 0;//修改成功为1， 修改失败为0
            string sql = "update bininfo set " + type + " = " + num + " where BinID = " + binid;
            MySqlConn msc2 = new MySqlConn();
            try
            {

                int res = msc2.nonSelect(sql);
                if (res > 0)
                {
                    //contextMenuStrip1.Visible = false;
                    value = 1;
                }
                msc2.Close();
                return value;
            }
            catch (Exception se)
            {
                
                new Thread(new ParameterizedThreadStart(showBox)).Start("修改数据库错误。请检查数据库是否创建好");
                MessageBox.Show("sql = " + sql +".错误：" + se.ToString());
                richTextBox1.AppendText("sql = " + sql );
                return value;
            }

        }


        /// <summary>
        /// 修改数据库参数函数，当硬件修改成功之后就进行本地数据库的修改
        /// </summary>
        private int parameter1(string type, string num, string binid)
        {
            int value = 0;//修改成功为1， 修改失败为0
            //MessageBox.Show("SSSSSSSSSSSSSSSS");
           
            //ring sql = "update bindata set " + type + " = " + num + " where BinID = " + binid + "and DateTime ='"+rowDateTime+"'";
            string sql = "update bindata set " + type + " = " + num + " where DateTime = '" + rowDateTime+"'";
            MySqlConn msc2 = new MySqlConn();
            try
            {

                int res = msc2.nonSelect(sql);
                if (res > 0)
                {
                    //contextMenuStrip1.Visible = false;
                    value = 1;
                }
                msc2.Close();
                return value;
            }
            catch (Exception se)
            {

                new Thread(new ParameterizedThreadStart(showBox)).Start("修改数据库bindata错误请检查数据库是否创建好");
                MessageBox.Show("sql = " + sql + ".错误：" + se.ToString());
                richTextBox1.AppendText("sql = " + sql + ".错误：" + se.ToString());
                richTextBox1.AppendText("sql = " + sql);
                return value;
            }

        }

        /// <summary>
        /// 设置直径输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox2_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("没有管理员权限");
                    //MessageBox.Show("", "提示");
                    contextMenuStrip1.Visible = false;
                }
                else
                {
                    if (toolStripTextBox2.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox2.Text);
                            if (data >= 1 && data < 50)
                            {
                                string str = Data.Data(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "02", (data * 100).ToString());
                                serialPort_WriteLine(new FacMessage(ins_num++, "03", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置直径", str, s_Produce));

                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate()
                                {

                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "料仓设置了直径\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {

                                MessageBox.Show("直径的范围是0到50(不包含)米", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入直径信息,单位是米,小数点后保留2为有效数字", "提示");
                            contextMenuStrip1.Visible = false;
                        }

                    }
                    else
                    {//判断是否输入为空
                        MessageBox.Show("请输入要修改的参数", "提示");
                        contextMenuStrip1.Visible = false;
                    }
                }

            }


        }

        /// <summary>
        /// 设置高度输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox3_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox3.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox3.Text);
                            if (data >= 1 && data < 100)
                            {
                                string str = Data.Data(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "04", (data * 100).ToString());
                                //MessageBox.Show(str);
                                serialPort_WriteLine(new FacMessage(ins_num++, "05", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置高度", str, s_Produce));

                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "料仓设置了仓筒高度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("高度的范围是0到100(不包含)米", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入高度信息，单位是米，小数点后保留2为有效数字", "提示");
                            contextMenuStrip1.Visible = false;
                        }

                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                        contextMenuStrip1.Visible = false;
                    }
                }
            }
        }

        /// <summary>
        /// 设置下锥输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox5_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox5.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox5.Text);
                            if (data > 0 && data < 40)
                            {
                                string bid = selectID(checkedListBox1.SelectedItem.ToString());
                                if (bid.Equals(""))
                                {
                                    throw new Exception();
                                }
                                string str = Data.Data(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "06", (data * 100).ToString());
                                //MessageBox.Show(str);
                                serialPort_WriteLine(new FacMessage(ins_num++, "07", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置下锥高度", str, s_Produce));


                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "料仓设置了下锥高度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("下锥高度的范围是0（不包含）到40（不包含）米", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入下锥盖度信息，保留2位小数，单位是米", "提示");
                            contextMenuStrip1.Visible = false;
                        }

                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                        contextMenuStrip1.Visible = false;
                    }
                }
            }
        }

        /// <summary>
        /// 设置密度输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox4_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox4.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox4.Text);
                            string str = Data.Data(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "08", (data * 1000).ToString());
                            //MessageBox.Show("密度"+str);
                            serialPort_WriteLine(new FacMessage(ins_num++, "09", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置密度", str, s_Produce));

                            if (ins_num > 2000)
                            {
                                ins_num = 1;
                            }

                            Invoke(new MethodInvoker(delegate()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  料仓设置了密度\r\n\r\n");
                            }));
                            contextMenuStrip1.Visible = false;
                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入密度信息，单位是吨/m³,小数点后保留2为有效数字", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }

        /// <summary>
        /// 设置方仓边长输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox10_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox10.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox10.Text);
                            //MessageBox.Show("data"+data);
                            string str = Data.DataLong(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "64", (data * 100).ToString());
                            //MessageBox.Show("str"+str);
                            serialPort_WriteLine(new FacMessage(ins_num++, "41", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置方仓边长", str, s_Produce));

                            if (ins_num > 2000)
                            {
                                ins_num = 1;
                            }

                            Invoke(new MethodInvoker(delegate ()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  设置方仓边长\r\n\r\n");
                            }));
                            contextMenuStrip1.Visible = false;
                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入方仓边长信息，单位是吨/m³,小数点后保留2为有效数字", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }

        }
        /// <summary>
        /// 设置方仓边宽输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox11_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox11.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox11.Text);
                            string str = Data.DataLong(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "66", (data * 100).ToString());
                            //MessageBox.Show(str);
                            serialPort_WriteLine(new FacMessage(ins_num++, "43", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置方仓边宽", str, s_Produce));

                            if (ins_num > 2000)
                            {
                                ins_num = 1;
                            }

                            Invoke(new MethodInvoker(delegate ()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  设置方仓边宽\r\n\r\n");
                            }));
                            contextMenuStrip1.Visible = false;
                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入方仓边宽信息，单位是吨/m³,小数点后保留2为有效数字", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }

        }
        /// <summary>
        /// 设置方仓左边距输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox12_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox12.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox12.Text);
                            string str = Data.DataLong(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "68", (data * 100).ToString());
                            //MessageBox.Show(str);
                            serialPort_WriteLine(new FacMessage(ins_num++, "45", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置方仓左边距", str, s_Produce));

                            if (ins_num > 2000)
                            {
                                ins_num = 1;
                            }

                            Invoke(new MethodInvoker(delegate ()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  设置方仓左边距\r\n\r\n");
                            }));
                            contextMenuStrip1.Visible = false;
                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入左边框信息，单位是吨/m³,小数点后保留2为有效数字", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }

        }

        /// <summary>
        /// 设置上锥输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox13_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBox13.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBox13.Text);
                            string str = Data.DataLong(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "70", (data * 100).ToString());
                            //MessageBox.Show(str);
                            serialPort_WriteLine(new FacMessage(ins_num++, "47", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置上锥", str, s_Produce));

                            if (ins_num > 2000)
                            {
                                ins_num = 1;
                            }

                            Invoke(new MethodInvoker(delegate ()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  设置上锥\r\n\r\n");
                            }));
                            contextMenuStrip1.Visible = false;
                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入左边框信息，单位是吨/m³,小数点后保留2为有效数字", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }

        }
        /// <summary>
        /// 设置k值入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxKValue_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxKValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            int data = int.Parse(toolStripTextBoxKValue.Text);
                            if (data < 1 || data > 256)
                            {
                                MessageBox.Show("设置超出范围");
                            }
                            else
                            {
                                MessageBox.Show("设置的K值是：" + data);


                                string id = selectID(checkedListBox1.SelectedItem.ToString());//获取到料仓的id
                                string k = "";
                                string b = "";
                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {

                                    //DataBase db = new DataBase();
                                    //db.command.CommandText = sql;
                                    //db.command.Connection = db.connection;
                                    //db.Dr = db.command.ExecuteReader();
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        k = rd["KValue"].ToString();
                                        b = rd["Bvalue"].ToString();
                                    }
                                    //db.Dr.Close();
                                    rd.Close();
                                    ms.Close();
                                }
                                catch (Exception ee)
                                {
                                    MessageBox.Show("查询bininfo K和B错误");
                                }

                                MessageBox.Show("查询bininfo K:" + k + "b=" + b);

                                ////进行设置，发送指令
                                string str = Data.DataKValue(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "14", data.ToString(),b);

                                //MessageBox.Show(str);
                                serialPort_WriteLine(new FacMessage(ins_num++, "0F", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置k值", str, s_Produce));

                                //if (ins_num > 2000)
                                //{
                                //    ins_num = 1;
                                //}

                                //Invoke(new MethodInvoker(delegate ()
                                //{
                                //    //让文本框获取焦点，不过注释这行也能达到效果
                                //    richTextBox1.Focus();
                                //    //设置光标的位置到文本尾   
                                //    richTextBox1.Select(richTextBox1.TextLength, 0);
                                //    //滚动到控件光标处   
                                //    richTextBox1.ScrollToCaret();
                                //    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  料仓设置了新步距角\r\n\r\n");
                                //}));
                            }
                            contextMenuStrip1.Visible = false;

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入参数信息", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数!!!!!!!", "提示");
                    }
                }
            }
        }
        /// <summary>
        /// 设置b值入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxBValue_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxBValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            int data = int.Parse(toolStripTextBoxBValue.Text);
                            if (data > 127 || data < -127)
                            {
                                MessageBox.Show("设置加减校验超出范围");
                            }
                            else
                            {
                                //MessageBox.Show("设置值是：" + data);


                                string id = selectID(checkedListBox1.SelectedItem.ToString());//获取到料仓的id
                                string k = "";
                                string b = "";
                                string sql = "select * from  bininfo  where BinID = " + id;
                                try
                                {

                                    //DataBase db = new DataBase();
                                    //db.command.CommandText = sql;
                                    //db.command.Connection = db.connection;
                                    //db.Dr = db.command.ExecuteReader();
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        k = rd["KValue"].ToString();
                                        b = rd["Bvalue"].ToString();
                                    }
                                    rd.Close();
                                    ms.Close();
                                }catch(Exception ee)
                                {
                                    MessageBox.Show("查询bininfo K和B错误");
                                }

                                //MessageBox.Show("查询bininfo K" + k  +"b=" + b);
                                //进行设置，发送指令
                                string str = Data.DataBValue(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "14", data.ToString(),k);//设置b值，设置0x0E指令。

                                //MessageBox.Show(str);
                                serialPort_WriteLine(new FacMessage(ins_num++, "0F", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置b值", str, s_Produce));

                                //if (ins_num > 2000)
                                //{
                                //    ins_num = 1;
                                //}

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  料仓设置了新b值\r\n\r\n");
                                }));
                            }

                            contextMenuStrip1.Visible = false;

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入步距角信息", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }





        
        /// <summary>
        /// 设置矫垂直角度入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxCangleValue_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxCangleValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBoxCangleValue.Text);
                            if (data > -90 && data < 90)
                            {
                                string bid = selectID(checkedListBox1.SelectedItem.ToString());
                                if (bid.Equals(""))
                                {
                                    throw new Exception();
                                }
                                string str = Data.DataToCangle(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "46", (data * 100).ToString());
                                //MessageBox.Show(str);//回复指令
                                serialPort_WriteLine(new FacMessage(ins_num++, "47", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置垂直角度", str, s_Produce));


                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "料仓设置垂直角度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("垂直角度的范围是-90（不包含）到90（不包含）度", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入垂直角度，角度保留一位小数。信息" + fe.ToString(), "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }
        /// <summary>
        /// 设置调整水平角度入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextShuipingValue_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextShuipingValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextShuipingValue.Text);
                            if (data > -90 && data < 90)
                            {
                                MessageBox.Show("设置水平矫正角度");//回复指令
                                string bid = selectID(checkedListBox1.SelectedItem.ToString());
                                if (bid.Equals(""))
                                {
                                    throw new Exception();
                                }
                                MessageBox.Show(data.ToString());
                                string str = Data.DataToCangle(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "54", (data * 100).ToString());//0x36调整水平角度   


                                MessageBox.Show(str);//回复指令
                                serialPort_WriteLine(new FacMessage(ins_num++, "55", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "调整水平角度", str, s_Produce));


                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "调整水平角度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("水平矫正角度的范围是-90（不包含）到90（不包含）度", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入水平矫正角度，角度保留一位小数。信息" + fe.ToString(), "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数123", "提示");
                    }
                }
            }
        }


        

        /// <summary>
        /// 设置初始水平角度入框回车键事件功能实现！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextSetShuipingValue_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextSetShuipingValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextSetShuipingValue.Text);
                            if (data > -90 && data < 90)
                            {
                                MessageBox.Show("设置初始水平角度");//回复指令
                                string bid = selectID(checkedListBox1.SelectedItem.ToString());
                                if (bid.Equals(""))
                                {
                                    throw new Exception();
                                }
                                MessageBox.Show(data.ToString());
                                string str = Data.DataToStartShuipingAngle(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "56", (data * 100).ToString());//0x36调整水平角度   


                                MessageBox.Show(str);//回复指令


                                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                                serialPort_WriteLine(new FacMessage(ins_num++, "39", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "水平初始定位角度设置", str, s_Produce));


                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "设置水平角度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("水平矫正角度的范围是-90（不包含）到90（不包含）度", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入水平矫正角度，角度保留一位小数。信息" + fe.ToString(), "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数123", "提示");
                    }
                }
            }
        }


        /// <summary>
        /// 设置水平步进度入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxHangleValue_KeyUp(object sender, KeyEventArgs e)
        {
            




            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxHangleValue.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBoxHangleValue.Text);
                            if (data > 0 && data <30)
                            {
                                string bid = selectID(checkedListBox1.SelectedItem.ToString());

                                if (bid.Equals(""))
                                {
                                    throw new Exception();
                                }

                                
                                //判断是否可以设置水平角度
                                string sql = "select * from  bininfo  where BinID = " + bid;
                                string type = "";
                                try
                                {

                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        type = rd["type"].ToString();

                                    }
                                    rd.Close();
                                    ms.Close();
                                }
                                catch (Exception ee)
                                {
                                    MessageBox.Show("查询判断是否可以设置水平角度错误." + ee.ToString());
                                }

                                if (type.Equals("0") || type.Equals("2"))
                                {
                                    MessageBox.Show("设备不支持设置水平角度");
                                    return;
                                }
                                int a = (int)data;

                                string str = Data.DataSetHangle(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "52", a.ToString());
                                serialPort_WriteLine(new FacMessage(ins_num++, "35", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置垂直角度", str, s_Produce));


                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对 " + checkedListBox1.SelectedItem.ToString() + "料仓设置水平角度\r\n\r\n");
                                }));
                                contextMenuStrip1.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("垂直角度的范围是0（不包含）到30（不包含）度", "提示");
                                contextMenuStrip1.Visible = false;
                            }

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入水平角度，角度保留一位小数。信息" + fe.ToString(), "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }


        /// <summary>
        /// 按照温度加热入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxWenDu_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxWenDu.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            int data = int.Parse(toolStripTextBoxWenDu.Text);
                            if (data > 50 || data < 0)
                            {
                                MessageBox.Show("设置温度验超出范围");
                            }
                            else
                            {
                                string id = selectID(checkedListBox1.SelectedItem.ToString());//获取到料仓的id

                                string tem = ("" + data).PadLeft(2, '0');//
                                string res = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "00"+ tem);//按照温度加热！！！！！
                                sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "按照温度加热", res, s_Produce));//指令为返回的代码
                            }

                            contextMenuStrip1.Visible = false;

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确温度信息", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }
        /// <summary>
        /// 设置加热时间入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxHitTime_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxHitTime.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            int data = int.Parse(toolStripTextBoxHitTime.Text);
                            if (data >= 20 || data <= 0)
                            {
                                MessageBox.Show("设置时间超出范围");
                            }
                            else
                            {
                                string id = selectID(checkedListBox1.SelectedItem.ToString());//获取到料仓的id
                                string tem = ("" + data).PadLeft(2, '0');//设置加热时间
                                show( "按照" + data + "分钟，发送加热指令\r\n\r\n");
                                string res = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "04"+ tem);//设置加热的时间来加热
                                sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "设置加热的时间来加热", res, s_Produce));//指令为返回的代码
                            }

                            contextMenuStrip1.Visible = false;

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入时间信息", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }
        
        // <summary>
        /// 设置步距角输入框回车键事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBoxBuJv_KeyUp(object sender, KeyEventArgs e)
        {
            //if(checkedListBox1.SelectedItem != null)
            //{
            //    MessageBox.Show("选中设备id", "提示");
            //}
            //else
            //{
            //    MessageBox.Show("没有选中设备id", "提示");
            //}
            if (e.KeyCode == Keys.Return)
            {
                if (curr_user.admin.Equals("0"))
                {
                    MessageBox.Show("没有管理员权限", "提示");
                }
                else
                {
                    if (toolStripTextBoxBuJv.Text.Trim().Equals("") == false)
                    {
                        try
                        {
                            float data = float.Parse(toolStripTextBoxBuJv.Text) *100;
                            if (data > 2000 || data < 0.5)
                            {
                                MessageBox.Show("步进角度必须大于0.5，小于等于20度");
                            }
                            else
                            {
                                //进行设置，发送指令
                                string str = Data.DataBuJvJiao(comboBox4.Text, selectID(checkedListBox1.SelectedItem.ToString()), "40", data.ToString());

                                //MessageBox.Show(str);
                                serialPort_WriteLine(new FacMessage(ins_num++, "29", selectID(checkedListBox1.SelectedItem.ToString()), false, TIME, "设置步进角", str, s_Produce));

                                if (ins_num > 2000)
                                {
                                    ins_num = 1;
                                }

                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n对  " + checkedListBox1.SelectedItem.ToString() + "  料仓设置了新步距角\r\n\r\n");
                                }));
                            }
                           
                            contextMenuStrip1.Visible = false;

                        }
                        catch (Exception fe)
                        {
                            MessageBox.Show("请正确输入步距角信息", "提示");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入要修改的参数", "提示");
                    }
                }
            }
        }
        private void 显示盘库时间ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int SW = SystemInformation.WorkingArea.Width;
            int SH = SystemInformation.WorkingArea.Height;
            double SW_percent = (double)SW / (double)1366;
            double SH_percent = (double)SH / (double)738;
            bdnInfo.Visible = true;
            groupBox_pic.Visible = false;
            dataGridView1.Visible = true;
            button8.Location = new Point((int)(886 * SW_percent), button8.Location.Y);
            button8.Visible = true;
            //Thread thread_select = new Thread(new ParameterizedThreadStart(select));
            //thread_select.Start("time");
            select("time");
        }

        private void 刷新ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (curr_user.admin.Equals("1") || curr_user.admin.Equals("0"))
            {
                if (port_isopen == 1)
                {
                    checkedListBox1.Items.Clear();
                    checkedListBox2.Items.Clear();

                    if (port_isopen == 1)//串口正常时进行查询后，查看在线状态
                    {
                        try
                        {
                            string groupname = comboBoxGroup.Text;
                            string sql = "select * from bininfo where Gid = (select Gid from groupinfo where Gname = '" + groupname + "' and Gstate = 1)";
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rdr = ms.getDataFromTable(sql);//直接对象引用

                            while (rdr.Read())
                            {
                                Thread.Sleep(20);
                                checkedListBox2.Items.Remove(rdr["BinName"].ToString());
                                checkedListBox2.Items.Add(rdr["BinName"].ToString());
                            }
                            SortCheckedList(checkedListBox2);

                            rdr.Close();
                            ms.Close();
                        }
                        catch (Exception exc)
                        {
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置!!!!");

                        }
                        OnlineCheak();

                    }
                }
            }
        }
        

        private void kValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (curr_user.admin.Equals("1") || curr_user.admin.Equals("0"))
            {
                MessageBox.Show("要更改k");

            }
        }
        //托盘退出
        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();//直接close，会调用MainForm的Close方法
        }
        

        //数据评价点击事件
        private void toAnalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new Thread(Analy_show).Start();//开启新的线程来开启新的窗体
        }

        private void Analy_show()
        {
            MethodInvoker meth = new MethodInvoker(show_cqinfo);
            BeginInvoke(meth);
        }
        private void show_cqinfo()
        {

            string name = rowBinName;
            //判断是否可以设置水平角度
           
            string sql = "select * from  bininfo  where BinName = '" + rowBinName+"'";
            //MessageBox.Show(sql);
            string type = "";
            try
            {

                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                while (rd.Read())
                {
                    type = rd["type"].ToString();

                }
                rd.Close();
                ms.Close();
            }
            catch (Exception ee)
            {
                MessageBox.Show("查询判断是否可以设置水平角度错误." + ee.ToString());
            }

            //if (type.Equals("侧置平扫") || type.Equals("顶置平扫"))
            if(type.Equals("4"))   
            {
                MessageBox.Show("平扫测量计算");
                cqform = new analysisData();
                cqform.setTime = rowDateTime;//向下一个窗体传参数
                cqform.setName = rowBinName;
                cqform.setVol = rowBinVol;
                cqform.setState = rowBinState;
                cqform.Show();

                ///////////////////////////////////////////////////////////////////////
                ////添加柱状图
                //Example8_8_1.Form1 form3D = new Example8_8_1.Form1();
                //form3D.setTime = rowDateTime;//向下一个窗体传参数
                //string info = "";
                //string print = "";//回传点的信息
                //string infodata = "";//用于排列数据

                //string zhijing = "";
                //string heigh = "";
                //string xiazhui = "";
                //string midu = "";
                //string margin = "";
                //string top = "";
                //string zhoujv = "";
                //string binstata = "";

                //string bjj = "";//步进角
                //string spj = "";//水平角
                //try
                //{

                //    MySqlConn mscA = new MySqlConn();//新建数据库连接
                //    sql = "select * from bindata where DateTime = '" + rowDateTime + "'";
                //    MySqlDataReader rd = mscA.getDataFromTable(sql);
                //    while (rd.Read())
                //    {
                //        info = rd["BackAll"].ToString().Trim();
                //        print = rd["PrintNum"].ToString().Trim();//获取点数
                //        binstata = rd["Algorithm"].ToString().Trim();
                //        bjj = rd["Jd"].ToString().Trim();
                //        midu = rd["MiDu"].ToString().Trim();
                //    }
                //    rd.Close();
                //    mscA.Close();
                //}
                //catch (Exception ee)
                //{
                //    MessageBox.Show("画图查询数据库出错==" + ee.ToString());
                //}

                //try
                //{
                //    MySqlConn mscB = new MySqlConn();//新建数据库连接
                //    string sql1 = "select * from bininfo where BinName = '" + rowBinName + "'";
                //    MySqlDataReader rd = mscB.getDataFromTable(sql1);
                //    while (rd.Read())
                //    {

                //        zhijing = rd["Diameter"].ToString().Trim();
                //        heigh = rd["CylinderH"].ToString().Trim();
                //        xiazhui = rd["PyramidH"].ToString().Trim();
                //       spj = rd["HAngle"].ToString().Trim();
                //        margin = rd["Margin"].ToString().Trim();
                //        top = rd["BinTop"].ToString().Trim();
                //        zhoujv = rd["Wheelbase"].ToString().Trim();
                //        infodata = rowBinName+"+"+zhijing + "+"+ heigh+"+"+ xiazhui+"+"+ midu+"+"+ margin+"+"+ top+"+"+ zhoujv+"+"+binstata+"+"+print +"+"+bjj+"+"+spj;
                //    }
                //    rd.Close();
                //    mscB.Close();
                //}
                //catch(Exception ee)
                //{
                //    MessageBox.Show("查询料仓信息出错：" + ee.ToString());
                //}

                //MessageBox.Show("获取到的信息：" + infodata);
                //form3D.binInfo = info;//将回传数据传给画图软件
                //form3D.setName = infodata;
                //form3D.Show();
                //form3D.Visible = true;

                /////////////////////////////////////////////////////////////////////////





                //matlab画图
                //new Thread(Analy_3D).Start();

            }
            else
            {
                //MessageBox.Show("直径测量计算");
                cqform = new analysisData();
                cqform.setTime = rowDateTime;//向下一个窗体传参数
                cqform.setName = rowBinName;
                cqform.setVol = rowBinVol;
                cqform.setState = rowBinState;
                cqform.Show();
            }
        }
       
        //private void to3DshowToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    new Thread(Analy_3D).Start();//开启新的线程来开启新的窗体
        //}
        private void Analy_3D()
        {
            MethodInvoker meth = new MethodInvoker(show_3D);
            BeginInvoke(meth);
        }
        private void show_3D()
        {
            //matlab方法画图
            try
            {
                //查询数据库 获取回传数据
                string info = "";
                MySqlConn mscA = new MySqlConn();//新建数据库连接
                string sql = "select * from bindata where DateTime = '" + rowDateTime + "'";
                MySqlDataReader rd = mscA.getDataFromTable(sql);
                while (rd.Read())
                {
                    info = rd["BackAll"].ToString().Trim();
                }
                rd.Close();
                if (info.Equals(""))
                {
                    MessageBox.Show("数据没有回传成功");
                    return;
                }
                //info = "0+0+158;20+0+169;40+0+208;60+0+321;0+15+158;0+30+158;0+45+158;0+60+158;0+75+158;0+-15+158;0+-30+158;0+-45+158;0+-60+158;0+-75+158;20+15+169;20+30+169;20+45+169;20+60+169;20+75+170;20+-15+169;20+-30+169;20+-45+119;20+-60+120;20+-75+120;40+15+208;40+30+208;40+45+209;40+60+210;40+75+212;40+-15+196;40+-30+172;40+-45+104;40+-60+109;40+-75+150;60+15+321;60+30+321;60+45+323;60+60+326;60+75+320;60+-15+169;60+-30+151;60+-45+153;60+-60+174;60+-75+343;";
                string[] pint = info.Split(';');
                int len = pint.Length;
                int a = len - 1;
                MessageBox.Show("点个数是str = " + a);
                float[] xvalue = new float[len - 1];
                float[] yvalue = new float[len - 1];
                float[] zvalue = new float[len - 1];
                float[,] data = new float[a, 3];//a行，3列
                try
                {
                   
                    for (int i = 0; i < len - 1; i++)
                    {
                        float x, y, z = 0;
                        string[] p = pint[i].Split('+');
                        xvalue[i] = (AngleOfX(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                        yvalue[i] = (AngleOfY(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100 ;//为了使数据在中间
                        zvalue[i] = (AngleOfZ(Convert.ToSingle(p[0]), Convert.ToSingle(p[1]), Convert.ToSingle(p[2]))) / 100;
                        //Log("第  "+ i+"行  {" + xvalue[i] + "," + yvalue[i] + "," + zvalue[i] + "},");
                        data[i, 0] = xvalue[i];
                        data[i, 1] = yvalue[i];
                        data[i, 2] = zvalue[i];

                    }
                }
                catch (Exception ee)
                {
                    MessageBox.Show("计算坐标点出错" +ee.ToString());
                }

                //string str = "";
                //for (int i = 0; i < len - 1; i++)
                //{
                //    str += "第  " + i + "行 " + "{" + data[i, 0] + "," + data[i, 1] + "," + data[i, 2] +"},/r/n";
                //}

                //Log("str"  + str);



                timeplot.Print3D pp = new timeplot.Print3D();
                //MWArray A = (MWNumericArray)new double[,] { { 0, -7.5, 1.42 }, { 0, -6.921986, 1.411919 }, { 0, -6.163002, 1.406628 }, { 0, -4.720058, 1.395 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0, -7.5, 1.42 }, { 0.149601, -6.941681, 1.411919 }, { 0.289007, -6.999425, 1.411919 }, { 0.4087177, -7.091282, 1.411919 }, { 0.5005748, -7.210993, 1.411919 }, { 0.5616224, -7.349514, 1.402523 }, { -0.149601, -6.941681, 1.411919 }, { -0.289007, -6.999425, 1.411919 }, { -0.2877953, -7.212204, 1.881766 }, { -0.3554377, -7.294788, 1.872369 }, { -0.3964393, -7.393775, 1.872369 }, { 0.3460406, -6.208559, 1.406628 }, { 0.6684992, -6.342125, 1.406628 }, { 0.9499457, -6.550055, 1.398967 }, { 1.169008, -6.825073, 1.391307 }, { 1.316277, -7.147305, 1.375986 }, { -0.3260767, -6.283065, 1.498553 }, { -0.5527974, -6.542527, 1.682404 }, { -0.4727003, -7.0273, 2.203314 }, { -0.6067708, -7.149681, 2.165012 }, { -0.9313278, -7.250452, 1.850933 }, { 0.7195018, -4.814783, 1.395 }, { 1.389971, -5.0925, 1.395 }, { 1.977963, -5.522038, 1.385 }, { 2.445, -6.088379, 1.37 }, { 2.676852, -6.782739, 1.4 }, { -0.3788031, -6.086288, 2.155 }, { -0.6538492, -6.3675, 2.245 }, { -0.9369299, -6.56307, 2.235 }, { -1.305, -6.746558, 2.13 }, { -2.869251, -6.731186, 1.285 } };
                MWArray A = (MWNumericArray)data;
                pp.timeplot((MWNumericArray)A);
            }
            catch (Exception ee)
            {
                //MessageBox.Show(ee.ToString());
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
        /// 角度转换为Y坐标
        /// </summary>
        /// <param name="Angle1">垂直角度</param>
        /// <param name="Angle2">水平角度</param>
        /// <param name="Length">长度</param>
        /// <returns></returns>
        public float AngleOfY(float Angle1, float Angle2, float Length)
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

            Zcoor = (float)(300.0f - L * Math.Cos(A1 * Math.PI / 180));


            return Zcoor;

        }

        //设置数据可靠点击按钮
        private void Lel1ToolStripMenuItem_Click(object sender, EventArgs e)
        {


            if (setQuality("1"))
            {
                MessageBox.Show("数据可靠,保存成功！" + rowDateTime + "    " + rowBinVol);
            }
            else
            {
                MessageBox.Show("保存失败！" + rowDateTime + "    " + rowBinVol);
            }

        }
        //设置数据不一定可靠点击按钮
        private void Lel1ToolStripMenuItem_Click1(object sender, EventArgs e)
        {
            if (setQuality("2"))
            {
                MessageBox.Show("数据不一定可靠,保存成功！" + rowDateTime + "    " + rowBinVol);
            }
            else
            {
                MessageBox.Show("保存失败！" + rowDateTime + "    " + rowBinVol);
            }
        }
        //设置数据不可靠点击按钮
        private void Lel1ToolStripMenuItem_Click2(object sender, EventArgs e)
        {
            if (setQuality("3"))
            {
                MessageBox.Show("数据不可靠,保存成功！" + rowDateTime + "    " + rowBinVol);
            }
            else
            {
                MessageBox.Show("保存失败！" + rowDateTime + "    " + rowBinVol);
            }
        }

        private Boolean setQuality(string q)
        {
            try
            {
                string sql = "update bindata set Quality = '" + q + "' where format(Volume ,2) = format('"+ rowBinVol +"' ,2) and DateTime = '" + rowDateTime + "'";
                MySqlConn ms = new MySqlConn();
                int res = ms.nonSelect(sql);
                ms.Close();
                if (res > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e){
                richTextBox1.AppendText("数据库出错 +" + e.ToString() );
            }
            return false;
        }



        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {//菜单栏中的盘库按钮
            button1.PerformClick();
        }

        private void buttonToAnalysisData_Click1(object sender, EventArgs e)
        {
            //要跳转到数据分析页面
            rowDateTime = "";
            rowBinName = "";
            rowBinVol = "";
            rowBinState = "";

            int select_item1 = checkedListBox1.CheckedItems.Count;
            int select_item2 = checkedListBox2.CheckedItems.Count;
           // MessageBox.Show("*********select_item2" + select_item2);


            if (select_item1 != 1&& select_item2==0)
            {
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox1.CheckedItems[0].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
            else if (select_item2!=1 && select_item1==0)
            {
                //MessageBox.Show("*********" + checkedListBox2.CheckedItems.Count);
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox2.CheckedItems[0].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }

        }

        private void buttonToAnalysisData_Click2(object sender, EventArgs e)
        {
            //要跳转到数据分析页面
            rowDateTime = "";
            rowBinName = "";
            rowBinVol = "";
            rowBinState = "";

            int select_item1 = checkedListBox1.CheckedItems.Count;
            int select_item2 = checkedListBox2.CheckedItems.Count;
            //MessageBox.Show("*********select_item2" + select_item2);


            if (select_item1 != 1 && select_item2 == 0)
            {
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox1.CheckedItems[1].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
            else if (select_item2 != 1 && select_item1 == 0)
            {
                //MessageBox.Show("*********" + checkedListBox2.CheckedItems.Count);
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox2.CheckedItems[1].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
        }

        private void buttonToAnalysisData_Click3(object sender, EventArgs e)
        {
            //要跳转到数据分析页面
            rowDateTime = "";
            rowBinName = "";
            rowBinVol = "";
            rowBinState = "";

            int select_item1 = checkedListBox1.CheckedItems.Count;
            int select_item2 = checkedListBox2.CheckedItems.Count;
            //MessageBox.Show("*********select_item2" + select_item2);


            if (select_item1 != 1 && select_item2 == 0)
            {
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox1.CheckedItems[2].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
            else if (select_item2 != 1 && select_item1 == 0)
            {
                //MessageBox.Show("*********" + checkedListBox2.CheckedItems.Count);
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox2.CheckedItems[2].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
        }

        private void buttonToAnalysisData_Click4(object sender, EventArgs e)
        {
            //要跳转到数据分析页面
            rowDateTime = "";
            rowBinName = "";
            rowBinVol = "";
            rowBinState = "";

            int select_item1 = checkedListBox1.CheckedItems.Count;
            int select_item2 = checkedListBox2.CheckedItems.Count;
            //MessageBox.Show("*********select_item2" + select_item2);


            if (select_item1 != 1 && select_item2 == 0)
            {
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox1.CheckedItems[2].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
            else if (select_item2 != 1 && select_item1 == 0)
            {
                //MessageBox.Show("*********" + checkedListBox2.CheckedItems.Count);
                if (SqlConnect == 1)
                {
                    string id = selectID(checkedListBox2.CheckedItems[2].ToString());
                    correntWenDo = id;
                    rowBinName = getName(id);
                    try
                    {
                        //查询数据库，选取最新的数据
                        string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            rowBinVol = rd1["Volume"].ToString();
                            rowDateTime = rd1["DateTime"].ToString();
                            rowBinState = rd1["Algorithm"].ToString();
                            break;
                        }

                        rd1.Close();
                        ms1.Close();
                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                        //MessageBox.Show("", "提示");
                    }


                    new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                }
                else//判断数据库是否可以使用else
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //MessageBox.Show("", "提示");
                }

            }
        }

        private void buttonToAnalysisData_Click(object sender, EventArgs e)
        {
            //要跳转到数据分析页面
            rowDateTime = "";
            rowBinName = "";
            rowBinVol = "";
            rowBinState = "";
            
            //获取料仓id 个数
            int select_item = checkedListBox1.CheckedItems.Count + checkedListBox2.CheckedItems.Count;
            if (select_item != 1)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择一个料仓进行显示");
            }


            if (checkedListBox1.CheckedItems.Count == 1)
            {
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    if (checkedListBox1.GetItemChecked(i))
                    {
                        if (SqlConnect == 1)
                        {

                            string id = selectID(checkedListBox1.Items[i].ToString());//获取选中的料仓的料仓id

                            correntWenDo = id;
                            rowBinName = getName(id);
                            try
                            {
                                //查询数据库，选取最新的数据
                                string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    rowBinVol = rd1["Volume"].ToString();
                                    rowDateTime = rd1["DateTime"].ToString();
                                    rowBinState = rd1["Algorithm"].ToString();
                                    break;
                                }

                                rd1.Close();
                                ms1.Close();
                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                                //MessageBox.Show("", "提示");
                            }


                            new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                        }
                        else//判断数据库是否可以使用else
                        {
                            new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错，请检查数据库是否创建好");
                            //MessageBox.Show("", "提示");
                        }

                    }//判断是否被选中
                }//循环
            }
            else if (checkedListBox2.CheckedItems.Count == 1)//不在线料仓的查询
            {
                for (int i = 0; i < checkedListBox2.Items.Count; i++)
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        if (SqlConnect == 1)
                        {
                            string id = selectID(checkedListBox2.Items[i].ToString());
                            correntWenDo = id;
                            rowBinName = getName(id);
                            try
                            {
                                //查询数据库，选取最新的数据
                                string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    rowBinVol = rd1["Volume"].ToString();
                                    rowDateTime = rd1["DateTime"].ToString();
                                    rowBinState = rd1["Algorithm"].ToString();
                                    break;
                                }

                                rd1.Close();
                                ms1.Close();
                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                                //MessageBox.Show("", "提示");
                            }


                            new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

                        }
                        else//判断数据库是否可以使用else
                        {
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("", "提示");
                        }

                    }//判断是否被选中
                }//循环

            }


        }


        private void buttonToAnalysisData_Click5(object sender, EventArgs e)
        {
            if (SqlConnect == 1)
            {
                string id = selectID(checkedListBox1.CheckedItems[0].ToString());
                correntWenDo = id;
                rowBinName = getName(id);
                try
                {
                    //查询数据库，选取最新的数据
                    string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                    MySqlConn ms1 = new MySqlConn();
                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                    while (rd1.Read())
                    {
                        rowBinVol = rd1["Volume"].ToString();
                        rowDateTime = rd1["DateTime"].ToString();
                        rowBinState = rd1["Algorithm"].ToString();
                        break;
                    }

                    rd1.Close();
                    ms1.Close();
                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                    //MessageBox.Show("", "提示");
                }


                new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

            }
            else//判断数据库是否可以使用else
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                //MessageBox.Show("", "提示");
            }
        }

        private void buttonToAnalysisData_Click6(object sender, EventArgs e)
        {
            if (SqlConnect == 1)
            {
                string id = selectID(checkedListBox2.CheckedItems[0].ToString());
                correntWenDo = id;
                rowBinName = getName(id);
                try
                {
                    //查询数据库，选取最新的数据
                    string sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                    MySqlConn ms1 = new MySqlConn();
                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                    while (rd1.Read())
                    {
                        rowBinVol = rd1["Volume"].ToString();
                        rowDateTime = rd1["DateTime"].ToString();
                        rowBinState = rd1["Algorithm"].ToString();
                        break;
                    }

                    rd1.Close();
                    ms1.Close();
                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    new Thread(new ParameterizedThreadStart(showBox)).Start("读取料位详细信息出错" + se.ToString());
                    //MessageBox.Show("", "提示");
                }


                new Thread(Analy_show).Start();//开启新的线程来开启新的窗体

            }
            else//判断数据库是否可以使用else
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                //MessageBox.Show("", "提示");
            }
        }


        /// <summary>
        /// 同时选中在线与不在线
        /// </summary>
        /// <param name="SW"></param>
        /// <param name="SH"></param>
        private void ShowTwo2(double SW, double SH)
        {
            double SW_percent = SW;
            double SH_percent = SH;
            int kuan = 300;
            int tukuan = 36;
            //int n = num;
            //float bili = 30F / checkedListBox2.CheckedItems.Count;//字体比例
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            //MessageBox.Show("SW_percent" + SW_percent + "SH_percent" + SH_percent + "n" + checkedListBox2.CheckedItems.Count);
       


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    //MessageBox.Show("按钮大小：" + 260 * SW_percent + "++++++++" + (50 * SH_percent / checkedListBox2.CheckedItems.Count));
                    //label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    label.Font = new System.Drawing.Font("宋体", 15, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((150)), (int)((50 + (j + 1) * 50) * SH_percent));
                    //MessageBox.Show("按钮位置：" + kuan * SW_percent + "++++++++" + (50 + (j + 1) * 50) * SH_percent);
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    //MessageBox.Show("按钮大小：" + 260 * SW_percent + "++++++++" + (50 * SH_percent / checkedListBox2.CheckedItems.Count));
                    //label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    label.Font = new System.Drawing.Font("宋体", 15, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((550)), (int)((50 + (j + 1) * 50) * SH_percent));
                    //MessageBox.Show("按钮位置：" + kuan * SW_percent + "++++++++" + (50 + (j + 1) * 50) * SH_percent);
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }



            if ((checkedListBox2.CheckedItems.Count + checkedListBox1.CheckedItems.Count) == 2)
            {
                Label label1 = new Label();
                //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                label1.Location = new System.Drawing.Point((int)(22), (int)(80));
                label1.Size = new System.Drawing.Size((int)(100), (int)(25));
                label1.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label1.Text = checkedListBox1.CheckedItems[0].ToString();
                groupBox_pic.Controls.Add(label1);
                label1.BringToFront();


                Label label2 = new Label();
                //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                label2.Location = new System.Drawing.Point((int)(422), (int)(80));
                label2.Size = new System.Drawing.Size((int)(100), (int)(25));
                label2.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label2.Text = checkedListBox2.CheckedItems[0].ToString();
                groupBox_pic.Controls.Add(label2);
                label2.BringToFront();
            }


            if ((checkedListBox1.CheckedItems.Count + checkedListBox2.CheckedItems.Count) == 2)
            {

                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                bx.Location = new System.Drawing.Point((int)(15), (int)(100 * SH_percent));
                //MessageBox.Show("****************" + SW_percent + "!!!!!!!!!" + SH_percent);
                bx.Size = new System.Drawing.Size((int)(140), (int)(300 * SH_percent));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                progressBar10.Size = new System.Drawing.Size((int)(75), (int)(200));
                bx.Controls.Add(progressBar10);
                progressBar10.BringToFront();
                progressBar10.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar10.ForeColor = System.Drawing.Color.Orange;
                progressBar10.BackColor = System.Drawing.Color.Orange;
                progressBar10.TabIndex = 4;


                buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(500));
                buttonToAnalysisData1.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData1);
                buttonToAnalysisData1.Click += new System.EventHandler(this.buttonToAnalysisData_Click5);



                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                bx1.Location = new System.Drawing.Point((int)(15 + 375), (int)(100 * SH_percent));
                bx1.Size = new System.Drawing.Size((int)(140), (int)(300 * SH_percent));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);

                progressBar11.Size = new System.Drawing.Size((int)(75), (int)(200));
                bx1.Controls.Add(progressBar11);
                progressBar11.BringToFront();
                progressBar11.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar11.ForeColor = System.Drawing.Color.Orange;
                progressBar11.BackColor = System.Drawing.Color.Orange;
                progressBar11.TabIndex = 4;

                buttonToAnalysisData2.Location = new System.Drawing.Point((int)(400), (int)(500));
                buttonToAnalysisData2.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData2);
                buttonToAnalysisData2.Click += new System.EventHandler(this.buttonToAnalysisData_Click6);


            }
  


            if ((checkedListBox1.CheckedItems.Count + checkedListBox2.CheckedItems.Count) == 2)
            {
                Label label31 = new Label();
                Label label32 = new Label();
                Label label33 = new Label();
                Label label35 = new Label();


                Label label37 = new Label();
                Label label38 = new Label();
                Label label39 = new Label();
                Label label41 = new Label();



                label31.Size = new System.Drawing.Size((int)(120), (int)(50));
                label31.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label31.Location = new System.Drawing.Point((int)((290)), (int)((120)));
                label31.Text = "";
                label31.AutoSize = false;
                groupBox_pic.Controls.Add(label31);

                label32.Size = new System.Drawing.Size((int)(120), (int)(50));
                label32.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label32.Location = new System.Drawing.Point((int)((290)), (int)((180)));
                label32.Text = "";
                groupBox_pic.Controls.Add(label32);

                label33.Size = new System.Drawing.Size((int)(120), (int)(50));
                label33.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label33.Location = new System.Drawing.Point((int)((290)), (int)((240)));
                label33.Text = "";
                groupBox_pic.Controls.Add(label33);

                label43.Size = new System.Drawing.Size((int)(120), (int)(50));
                label43.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label43.Location = new System.Drawing.Point((int)((290)), (int)((300)));
                label43.Text = "";
                groupBox_pic.Controls.Add(label43);

                label35.Size = new System.Drawing.Size((int)(120), (int)(50));
                label35.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label35.Location = new System.Drawing.Point((int)((290)), (int)((360)));
                label35.Text = "";
                groupBox_pic.Controls.Add(label35);

                label36.Size = new System.Drawing.Size((int)(120), (int)(50));
                label36.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label36.Location = new System.Drawing.Point((int)((290)), (int)((420)));
                label36.Text = "";
                groupBox_pic.Controls.Add(label36);


                label37.Size = new System.Drawing.Size((int)(120), (int)(50));
                label37.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label37.Location = new System.Drawing.Point((int)((690)), (int)((120)));
                label37.Text = "";
                groupBox_pic.Controls.Add(label37);

                label38.Size = new System.Drawing.Size((int)(120), (int)(50));
                label38.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label38.Location = new System.Drawing.Point((int)((690)), (int)((180)));
                label38.Text = "";
                groupBox_pic.Controls.Add(label38);

                label39.Size = new System.Drawing.Size((int)(120), (int)(50));
                label39.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label39.Location = new System.Drawing.Point((int)((690)), (int)((240)));
                label39.Text = "";
                groupBox_pic.Controls.Add(label39);

                label40.Size = new System.Drawing.Size((int)(120), (int)(50));
                label40.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label40.Location = new System.Drawing.Point((int)((720)), (int)((300)));
                label40.Text = "";
                groupBox_pic.Controls.Add(label40);

                label41.Size = new System.Drawing.Size((int)(120), (int)(50));
                label41.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label41.Location = new System.Drawing.Point((int)((720)), (int)((360)));
                label41.Text = "";
                groupBox_pic.Controls.Add(label41);

                label42.Size = new System.Drawing.Size((int)(120), (int)(50));
                label42.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label42.Location = new System.Drawing.Point((int)((720)), (int)((420)));
                label42.Text = "";
                groupBox_pic.Controls.Add(label42);

                string type = "";
                string str0 = checkedListBox2.CheckedItems[0].ToString();
                string id0 = selectID(str0);
                string sql0 = "select * from bininfo where BinID = " + id0;

                MySqlConn ms0 = new MySqlConn();
                MySqlDataReader rd0 = ms0.getDataFromTable(sql0);
                while (rd0.Read())
                {
                    type = rd0["type"].ToString();

                }

                rd0.Close();
                ms0.Close();

                if (type.Equals("4"))
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox1.CheckedItems[0].ToString();
                    string id = selectID(str);


                    correntWenDo = id;


                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0201");//2为实时查询温度。
                                                                                              //show("发送的指令：" + data + "\r\n\r\n");
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询测量设备温度22222\r\n\r\n");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内温度", data, s_Produce));//指令为返回的代码


                    //查询仓内实时温度
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询仓内温度222222\r\n\r\n");
                    string data1 = Data.Data(comboBox4.Text, id, "16", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", data1, s_Produce));

                    xuanOne++;
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            fangchang = float.Parse(rd["Fbian"].ToString());
                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            //label34.Text = rd1["Temp"].ToString() + "℃";
                            //label36.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误同时", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0, fangchang1 = 0, fangkuan1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[0].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            fangchang1 = float.Parse(rd2["Fbian"].ToString());
                            fangkuan1 = float.Parse(rd2["Fkuan"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)(fangchang1 * fangkuan1 * cylinderh1 + (Math.PI * fangkuan1 * fangkuan1 * pyramidh1) / 12);
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label42.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误！！！！", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }
                else if(type.Equals("0")|| type.Equals("1"))
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox1.CheckedItems[0].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label35.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误SSSS", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[0].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label41.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误！！！！", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }

            }


        }



        /// <summary>
        /// 不在线多料仓显示三个和两个
        /// </summary>
        /// <param name="SW"></param>
        /// <param name="SH"></param>
        private void ShowTwo(double SW,double SH)
        {
            double SW_percent = SW;
            double SH_percent = SH;
            int kuan = 300;
            int tukuan = 36;
            //int n = num;
            float bili = 30F / checkedListBox2.CheckedItems.Count;//字体比例
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            //MessageBox.Show("SW_percent"+ SW_percent+ "SH_percent"+ SH_percent+"n"+ checkedListBox2.CheckedItems.Count);
            //string st = "";
            List<string> sl = new List<string>();
            for (int i = 0; i < checkedListBox2.CheckedItems.Count; i++)
            {
                //MessageBox.Show(checkedListBox2.CheckedItems[i].ToString());
                sl.Add(checkedListBox2.CheckedItems[i].ToString());

            }

            for (int i = 0; i < checkedListBox2.CheckedItems.Count; i++)
            {




                //MessageBox.Show("宽度" + 180 * SW_percent + "长度" + 300 * SH_percent);
                //MessageBox.Show("X" + tukuan * SW_percent + "Y" + 100 * SH_percent);
                if (checkedListBox2.CheckedItems.Count == 2)
                {
                    Label label1 = new Label();
                    //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                    label1.Location = new System.Drawing.Point((int)(22+i*400), (int)(80));
                    label1.Size = new System.Drawing.Size((int)(100), (int)(25));
                    label1.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label1.Text = checkedListBox2.CheckedItems[i].ToString();
                    groupBox_pic.Controls.Add(label1);
                    label1.BringToFront();
                }else if(checkedListBox2.CheckedItems.Count == 3)
                {
                    Label label1 = new Label();
                    //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                    label1.Location = new System.Drawing.Point((int)(30+(i*230)), (int)(60));
                    label1.Size = new System.Drawing.Size((int)(80), (int)(25));
                    label1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label1.Text = checkedListBox2.CheckedItems[i].ToString();
                    groupBox_pic.Controls.Add(label1);
                    label1.BringToFront();

                }



                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(260 * SW_percent), (int)(50 * SH_percent/ checkedListBox2.CheckedItems.Count));
                    //MessageBox.Show("按钮大小：" + 260 * SW_percent + "++++++++" + (50 * SH_percent / checkedListBox2.CheckedItems.Count));
                    //label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    label.Font = new System.Drawing.Font("宋体", bili, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((kuan * SW_percent-10)), (int)((50 + (j + 1) * 50) * SH_percent));
                    //MessageBox.Show("按钮位置：" + kuan * SW_percent + "++++++++" + (50 + (j + 1) * 50) * SH_percent);
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }
                kuan = kuan + 800;
                tukuan = tukuan + 750;

            }


            if (checkedListBox2.CheckedItems.Count == 2)
            {

                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                bx.Location = new System.Drawing.Point((int)(15), (int)(100 * SH_percent));
                bx.Size = new System.Drawing.Size((int)(140 ), (int)(300 * SH_percent));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                progressBar10.Size = new System.Drawing.Size((int)(75), (int)(200 ));
                bx.Controls.Add(progressBar10);
                progressBar10.BringToFront();
                progressBar10.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar10.ForeColor = System.Drawing.Color.Orange;
                progressBar10.BackColor = System.Drawing.Color.Orange;
                progressBar10.TabIndex = 4;

               
                buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(500));
                buttonToAnalysisData1.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData1);



                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                bx1.Location = new System.Drawing.Point((int)(15+375), (int)(100 * SH_percent));
                bx1.Size = new System.Drawing.Size((int)(140 ), (int)(300 * SH_percent));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);

                progressBar11.Size = new System.Drawing.Size((int)(75), (int)(200));
                bx1.Controls.Add(progressBar11);
                progressBar11.BringToFront();
                progressBar11.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar11.ForeColor = System.Drawing.Color.Orange;
                progressBar11.BackColor = System.Drawing.Color.Orange;
                progressBar11.TabIndex = 4;

                buttonToAnalysisData2.Location = new System.Drawing.Point((int)(400), (int)(500));
                buttonToAnalysisData2.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData2);


            }
            else if (checkedListBox2.CheckedItems.Count == 3)
            {

                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                bx.Location = new System.Drawing.Point((int)(40 * SW_percent), (int)(100 * SH_percent));
                bx.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                progressBar10.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(150 * SH_percent));
                bx.Controls.Add(progressBar10);
                progressBar10.BringToFront();
                progressBar10.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar10.ForeColor = System.Drawing.Color.Orange;
                progressBar10.BackColor = System.Drawing.Color.Orange;
                progressBar10.TabIndex = 4;

                buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(400));
                buttonToAnalysisData1.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData1);


                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                bx1.Location = new System.Drawing.Point((int)(50 * SW_percent + 250), (int)(100 * SH_percent));
                bx1.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);

                progressBar11.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(150 * SH_percent));
                bx1.Controls.Add(progressBar11);
                progressBar11.BringToFront();
                progressBar11.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar11.ForeColor = System.Drawing.Color.Orange;
                progressBar11.BackColor = System.Drawing.Color.Orange;
                progressBar11.TabIndex = 4;

                buttonToAnalysisData2.Location = new System.Drawing.Point((int)(275), (int)(400));
                buttonToAnalysisData2.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData2);


                PictureBox bx2 = new PictureBox();
                bx2.Image = global::Warehouse.Properties.Resources._001;
                bx2.Location = new System.Drawing.Point((int)(50 * SW_percent + 500), (int)(100 * SH_percent));
                bx2.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx2);

                progressBar12.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(150 * SH_percent));
                bx2.Controls.Add(progressBar12);
                progressBar12.BringToFront();
                progressBar12.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar12.ForeColor = System.Drawing.Color.Orange;
                progressBar12.BackColor = System.Drawing.Color.Orange;
                progressBar12.TabIndex = 4;

                buttonToAnalysisData3.Location = new System.Drawing.Point((int)(500), (int)(400));
                buttonToAnalysisData3.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData3);



            }


            if (checkedListBox2.CheckedItems.Count == 2)
            {
                Label label31 = new Label();
                Label label32 = new Label();
                Label label33 = new Label();
                Label label34 = new Label();
                Label label35 = new Label();
                Label label36 = new Label();


                Label label37 = new Label();
                Label label38 = new Label();
                Label label39 = new Label();
                Label label40 = new Label();
                Label label41 = new Label();
                Label label42 = new Label();


                label31.Size = new System.Drawing.Size((int)(120), (int)(50));
                label31.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label31.Location = new System.Drawing.Point((int)((290)), (int)((120)));
                label31.Text = "";
                label31.AutoSize = false;
                groupBox_pic.Controls.Add(label31);

                label32.Size = new System.Drawing.Size((int)(120), (int)(50));
                label32.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label32.Location = new System.Drawing.Point((int)((290)), (int)((180)));
                label32.Text = "";
                groupBox_pic.Controls.Add(label32);

                label33.Size = new System.Drawing.Size((int)(120), (int)(50));
                label33.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label33.Location = new System.Drawing.Point((int)((290)), (int)((240)));
                label33.Text = "";
                groupBox_pic.Controls.Add(label33);

                label34.Size = new System.Drawing.Size((int)(120), (int)(50));
                label34.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label34.Location = new System.Drawing.Point((int)((290)), (int)((300)));
                label34.Text = "";
                groupBox_pic.Controls.Add(label34);

                label35.Size = new System.Drawing.Size((int)(120), (int)(50));
                label35.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label35.Location = new System.Drawing.Point((int)((290)), (int)((360)));
                label35.Text = "";
                groupBox_pic.Controls.Add(label35);

                label36.Size = new System.Drawing.Size((int)(120), (int)(50));
                label36.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label36.Location = new System.Drawing.Point((int)((290)), (int)((420)));
                label36.Text = "";
                groupBox_pic.Controls.Add(label36);


                label37.Size = new System.Drawing.Size((int)(120), (int)(50));
                label37.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label37.Location = new System.Drawing.Point((int)((690)), (int)((120)));
                label37.Text = "";
                groupBox_pic.Controls.Add(label37);

                label38.Size = new System.Drawing.Size((int)(120), (int)(50));
                label38.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label38.Location = new System.Drawing.Point((int)((690)), (int)((180)));
                label38.Text = "";
                groupBox_pic.Controls.Add(label38);

                label39.Size = new System.Drawing.Size((int)(120), (int)(50));
                label39.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label39.Location = new System.Drawing.Point((int)((690)), (int)((240)));
                label39.Text = "";
                groupBox_pic.Controls.Add(label39);

                label40.Size = new System.Drawing.Size((int)(120), (int)(50));
                label40.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label40.Location = new System.Drawing.Point((int)((720)), (int)((300)));
                label40.Text = "";
                groupBox_pic.Controls.Add(label40);

                label41.Size = new System.Drawing.Size((int)(120), (int)(50));
                label41.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label41.Location = new System.Drawing.Point((int)((720)), (int)((360)));
                label41.Text = "";
                groupBox_pic.Controls.Add(label41);

                label42.Size = new System.Drawing.Size((int)(120), (int)(50));
                label42.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label42.Location = new System.Drawing.Point((int)((720)), (int)((420)));
                label42.Text = "";
                groupBox_pic.Controls.Add(label42);

                string type = "";
                string str0 = checkedListBox2.CheckedItems[0].ToString();
                string id0 = selectID(str0);
                string sql0 = "select * from bininfo where BinID = " + id0;

                MySqlConn ms0 = new MySqlConn();
                MySqlDataReader rd0 = ms0.getDataFromTable(sql0);
                while (rd0.Read())
                {
                    type = rd0["type"].ToString();

                }

                rd0.Close();
                ms0.Close();

                if (type.Equals("1")|| type.Equals("0"))
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0,fangchang=0,fangkuan=0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox2.CheckedItems[0].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        //float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label36.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误SSSS", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0,fangchang1=0,fangkuan1=0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[1].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        //float Vol_ware = (float)(fangchang1 * fangkuan1 * cylinderh1 + (Math.PI * fangkuan1 * fangkuan1 * pyramidh1) / 12);
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label42.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误！！！！", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }
                else
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox2.CheckedItems[0].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label35.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误SSSS", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[1].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label41.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误！！！！", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }

            }
            else if(checkedListBox2.CheckedItems.Count == 3)
            {
                Label label31 = new Label();
                Label label32 = new Label();
                Label label33 = new Label();
                Label label34 = new Label();
                Label label35 = new Label();
                Label label36 = new Label();


                Label label37 = new Label();
                Label label38 = new Label();
                Label label39 = new Label();
                Label label40 = new Label();
                Label label41 = new Label();
                Label label42 = new Label();


                Label label43 = new Label();
                Label label44 = new Label();
                Label label45 = new Label();
                Label label46 = new Label();
                Label label47 = new Label();
                Label label48 = new Label();

                label31.Size = new System.Drawing.Size((int)(120), (int)(50));
                label31.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label31.Location = new System.Drawing.Point((int)((180)), (int)((100)));
                label31.Text = "";
                label31.AutoSize = false;
                groupBox_pic.Controls.Add(label31);

                label32.Size = new System.Drawing.Size((int)(120), (int)(50));
                label32.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label32.Location = new System.Drawing.Point((int)((180)), (int)((150)));
                label32.Text = "";
                groupBox_pic.Controls.Add(label32);

                label33.Size = new System.Drawing.Size((int)(120), (int)(50));
                label33.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label33.Location = new System.Drawing.Point((int)((180)), (int)((200)));
                label33.Text = "";
                groupBox_pic.Controls.Add(label33);

                label34.Size = new System.Drawing.Size((int)(120), (int)(50));
                label34.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label34.Location = new System.Drawing.Point((int)((180)), (int)((250)));
                label34.Text = "";
                groupBox_pic.Controls.Add(label34);

                label35.Size = new System.Drawing.Size((int)(120), (int)(50));
                label35.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label35.Location = new System.Drawing.Point((int)((180)), (int)((300)));
                label35.Text = "";
                groupBox_pic.Controls.Add(label35);

                label36.Size = new System.Drawing.Size((int)(120), (int)(50));
                label36.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label36.Location = new System.Drawing.Point((int)((180)), (int)((350)));
                label36.Text = "";
                groupBox_pic.Controls.Add(label36);


                label37.Size = new System.Drawing.Size((int)(120), (int)(50));
                label37.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label37.Location = new System.Drawing.Point((int)((420)), (int)((100)));
                label37.Text = "";
                groupBox_pic.Controls.Add(label37);

                label38.Size = new System.Drawing.Size((int)(120), (int)(50));
                label38.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label38.Location = new System.Drawing.Point((int)((420)), (int)((150)));
                label38.Text = "";
                groupBox_pic.Controls.Add(label38);

                label39.Size = new System.Drawing.Size((int)(120), (int)(50));
                label39.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label39.Location = new System.Drawing.Point((int)((420)), (int)((200)));
                label39.Text = "";
                groupBox_pic.Controls.Add(label39);

                label40.Size = new System.Drawing.Size((int)(120), (int)(50));
                label40.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label40.Location = new System.Drawing.Point((int)((420)), (int)((250)));
                label40.Text = "";
                groupBox_pic.Controls.Add(label40);

                label41.Size = new System.Drawing.Size((int)(120), (int)(50));
                label41.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label41.Location = new System.Drawing.Point((int)((420)), (int)((300)));
                label41.Text = "";
                groupBox_pic.Controls.Add(label41);

                label42.Size = new System.Drawing.Size((int)(120), (int)(50));
                label42.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label42.Location = new System.Drawing.Point((int)((420)), (int)((350)));
                label42.Text = "";
                groupBox_pic.Controls.Add(label42);



                label43.Size = new System.Drawing.Size((int)(120), (int)(50));
                label43.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label43.Location = new System.Drawing.Point((int)((720)), (int)((100)));
                label43.Text = "";
                groupBox_pic.Controls.Add(label43);

                label44.Size = new System.Drawing.Size((int)(120), (int)(50));
                label44.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label44.Location = new System.Drawing.Point((int)((720)), (int)((150)));
                label44.Text = "";
                groupBox_pic.Controls.Add(label44);

                label45.Size = new System.Drawing.Size((int)(120), (int)(50));
                label45.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label45.Location = new System.Drawing.Point((int)((720)), (int)((200)));
                label45.Text = "";
                groupBox_pic.Controls.Add(label45);

                label46.Size = new System.Drawing.Size((int)(120), (int)(50));
                label46.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label46.Location = new System.Drawing.Point((int)((720)), (int)((250)));
                label46.Text = "";
                groupBox_pic.Controls.Add(label46);

                label47.Size = new System.Drawing.Size((int)(120), (int)(50));
                label47.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label47.Location = new System.Drawing.Point((int)((720)), (int)((300)));
                label47.Text = "";
                groupBox_pic.Controls.Add(label47);

                label48.Size = new System.Drawing.Size((int)(120), (int)(50));
                label48.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label48.Location = new System.Drawing.Point((int)((720)), (int)((350)));
                label48.Text = "";
                groupBox_pic.Controls.Add(label48);


                string str0 = checkedListBox2.CheckedItems[0].ToString();
                string id0 = selectID(str0);
                string sql0 = "select * from bininfo where BinID = " + id0;
                string type = "";
                MySqlConn ms0 = new MySqlConn();
                MySqlDataReader rd0 = ms0.getDataFromTable(sql0);
                while (rd0.Read())
                {
                    type = rd0["type"].ToString();


                }
                //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                rd0.Close();
                ms0.Close();

                if (type.Equals("4"))
                {

                    float diameter = 0, cylinderh = 0, pyramidh = 0,fangchang=0,fangkuan=0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox2.CheckedItems[0].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            fangchang = float.Parse(rd["Fbian"].ToString());
                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label35.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误31", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }



                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0,fangchang1=0,fangkuan1=0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[1].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            fangchang1 = float.Parse(rd2["Fbian"].ToString());
                            fangkuan1 = float.Parse(rd2["Fkuan"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)(fangchang1 * fangkuan1 * cylinderh1 + (Math.PI * fangkuan1 * fangkuan1 * pyramidh1) / 12);
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label41.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误32", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0, fangchang2 = 0, fangkuan2 = 0;
                    //ID6.Text = checkedListBox2.CheckedItems[2].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str2 = checkedListBox2.CheckedItems[2].ToString();
                    string id2 = selectID(str2);
                    string sql2 = "select * from bininfo where BinID = " + id2;
                    //MessageBox.Show("*****************************" + id2);
                    try
                    {
                        MySqlConn ms4 = new MySqlConn();
                        MySqlDataReader rd4 = ms4.getDataFromTable(sql2);
                        while (rd4.Read())
                        {
                            fangchang2 = float.Parse(rd4["Fbian"].ToString());
                            fangkuan2 = float.Parse(rd4["Fkuan"].ToString());
                            cylinderh2 = float.Parse(rd4["CylinderH"].ToString());
                            pyramidh2 = float.Parse(rd4["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter2 + "CylinderH" + cylinderh2 + "PyramidH" + pyramidh2);

                        rd4.Close();
                        ms4.Close();
                        float Vol_ware = (float)(fangchang2 * fangkuan2 * cylinderh2 + (Math.PI * fangkuan2 * fangkuan2 * pyramidh2) / 12);
                        label43.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms5 = new MySqlConn();
                        MySqlDataReader rd5 = ms5.getDataFromTable(sql2);

                        while (rd5.Read())
                        {
                            label44.Text = rd5["Volume"].ToString() + "m³";
                            label45.Text = rd5["Weight"].ToString() + "吨";
                            label46.Text = rd5["Temp"].ToString() + "℃";
                            label47.Text = rd5["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd5["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar12.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar12.Value = 0;
                                MessageBox.Show("数据有误33", "提示");
                            }
                            break;
                        }
                        rd5.Close();
                        ms5.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }
                else
                {

                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox2.CheckedItems[0].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label35.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误31", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }



                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox2.CheckedItems[1].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label41.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误32", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }


                    float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                    //ID6.Text = checkedListBox2.CheckedItems[2].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str2 = checkedListBox2.CheckedItems[2].ToString();
                    string id2 = selectID(str2);
                    string sql2 = "select * from bininfo where BinID = " + id2;
                    //MessageBox.Show("*****************************" + id2);
                    try
                    {
                        MySqlConn ms4 = new MySqlConn();
                        MySqlDataReader rd4 = ms4.getDataFromTable(sql2);
                        while (rd4.Read())
                        {
                            diameter2 = float.Parse(rd4["Diameter"].ToString());
                            cylinderh2 = float.Parse(rd4["CylinderH"].ToString());
                            pyramidh2 = float.Parse(rd4["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter2 + "CylinderH" + cylinderh2 + "PyramidH" + pyramidh2);

                        rd4.Close();
                        ms4.Close();
                        float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                        label43.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms5 = new MySqlConn();
                        MySqlDataReader rd5 = ms5.getDataFromTable(sql2);

                        while (rd5.Read())
                        {
                            label44.Text = rd5["Volume"].ToString() + "m³";
                            label45.Text = rd5["Weight"].ToString() + "吨";
                            label46.Text = rd5["Temp"].ToString() + "℃";
                            label47.Text = rd5["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd5["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar12.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar12.Value = 0;
                                MessageBox.Show("数据有误33", "提示");
                            }
                            break;
                        }
                        rd5.Close();
                        ms5.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }



            }


        }



        /// <summary>
        /// 在线多料仓显示三个和两个
        /// </summary>
        /// <param name="SW"></param>
        /// <param name="SH"></param>
        private void ShowTwo1(double SW, double SH)
        {
            double SW_percent = SW;
            double SH_percent = SH;
            int kuan = 0;
            int tukuan = 36;
            //int n = num;
            float bili = 30F / checkedListBox1.CheckedItems.Count;//字体比例
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            //MessageBox.Show("SW_percent" + SW_percent + "SH_percent" + SH_percent + "**********8n" + checkedListBox1.CheckedItems.Count);
            //string st = "";
            List<string> sl = new List<string>();
            for (int i = 0; i < checkedListBox1.CheckedItems.Count; i++)
            {
                //MessageBox.Show(checkedListBox2.CheckedItems[i].ToString());
                sl.Add(checkedListBox1.CheckedItems[i].ToString());

            }

            for (int i = 0; i < checkedListBox1.CheckedItems.Count; i++)
            {



                //MessageBox.Show("宽度" + 180 * SW_percent + "长度" + 300 * SH_percent);
                //MessageBox.Show("X" + tukuan * SW_percent + "Y" + 100 * SH_percent);
                if (checkedListBox1.CheckedItems.Count == 2)
                {
                    Label label1 = new Label();
                    //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                    label1.Location = new System.Drawing.Point((int)(50 + i * 480), (int)(80));
                    label1.Size = new System.Drawing.Size((int)(100), (int)(25));
                    label1.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label1.Text = checkedListBox1.CheckedItems[i].ToString();
                    groupBox_pic.Controls.Add(label1);
                    label1.BringToFront();
                }
                else if (checkedListBox1.CheckedItems.Count == 3)
                {
                    Label label1 = new Label();
                    //label1.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(20 * SH_percent));
                    label1.Location = new System.Drawing.Point((int)(30 + (i * 230)), (int)(60));
                    label1.Size = new System.Drawing.Size((int)(80), (int)(25));
                    label1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label1.Text = checkedListBox1.CheckedItems[i].ToString();
                    groupBox_pic.Controls.Add(label1);
                    label1.BringToFront();

                }



                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(120), (int)(50 * SH_percent / checkedListBox1.CheckedItems.Count));
                    //MessageBox.Show("按钮大小：" + 260 * SW_percent + "++++++++" + (50 * SH_percent / checkedListBox2.CheckedItems.Count));
                    //label.Size = new System.Drawing.Size((int)(130), (int)(25));
                    label.Font = new System.Drawing.Font("宋体", bili, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((130+kuan)), (int)((50 + (j + 1) * 50) * SH_percent));
                    //MessageBox.Show("按钮位置：" + kuan * SW_percent + "++++++++" + (50 + (j + 1) * 50) * SH_percent);
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }
                kuan = kuan + 500;
                tukuan = tukuan + 750;

            }

            if(checkedListBox1.CheckedItems.Count == 2)
            {

                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                bx.Location = new System.Drawing.Point((int)(15), (int)(100 * SH_percent));
                bx.Size = new System.Drawing.Size((int)(140), (int)(300 * SH_percent));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                progressBar10.Size = new System.Drawing.Size((int)(75), (int)(200));
                bx.Controls.Add(progressBar10);
                progressBar10.BringToFront();
                progressBar10.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar10.ForeColor = System.Drawing.Color.Orange;
                progressBar10.BackColor = System.Drawing.Color.Orange;
                progressBar10.TabIndex = 4;


                buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(500));
                buttonToAnalysisData1.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData1);



                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                bx1.Location = new System.Drawing.Point((int)(15+ 475), (int)(100 * SH_percent));
                bx1.Size = new System.Drawing.Size((int)(140), (int)(300 * SH_percent));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);

                progressBar11.Size = new System.Drawing.Size((int)(75 ), (int)(200));
                bx1.Controls.Add(progressBar11);
                progressBar11.BringToFront();
                progressBar11.Location = new System.Drawing.Point((int)(30), (int)(65));
                progressBar11.ForeColor = System.Drawing.Color.Orange;
                progressBar11.BackColor = System.Drawing.Color.Orange;
                progressBar11.TabIndex = 4;

                buttonToAnalysisData2.Location = new System.Drawing.Point((int)(465), (int)(500));
                buttonToAnalysisData2.Size = new System.Drawing.Size((int)(170), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData2);


            }
            else if(checkedListBox1.CheckedItems.Count == 3)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                bx.Location = new System.Drawing.Point((int)(40 * SW_percent), (int)(100 * SH_percent));
                bx.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                progressBar10.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(130 * SH_percent));
                bx.Controls.Add(progressBar10);
                progressBar10.BringToFront();
                progressBar10.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar10.ForeColor = System.Drawing.Color.Orange;
                progressBar10.BackColor = System.Drawing.Color.Orange;
                progressBar10.TabIndex = 4;

                buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(400));
                buttonToAnalysisData1.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData1);


                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                bx1.Location = new System.Drawing.Point((int)(50 * SW_percent + 250), (int)(100 * SH_percent));
                bx1.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);

                progressBar11.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(130 * SH_percent));
                bx1.Controls.Add(progressBar11);
                progressBar11.BringToFront();
                progressBar11.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar11.ForeColor = System.Drawing.Color.Orange;
                progressBar11.BackColor = System.Drawing.Color.Orange;
                progressBar11.TabIndex = 4;

                buttonToAnalysisData2.Location = new System.Drawing.Point((int)(275), (int)(400));
                buttonToAnalysisData2.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData2);


                PictureBox bx2 = new PictureBox();
                bx2.Image = global::Warehouse.Properties.Resources._001;
                bx2.Location = new System.Drawing.Point((int)(50 * SW_percent + 500), (int)(100 * SH_percent));
                bx2.Size = new System.Drawing.Size((int)(140 * SW_percent), (int)(300 * SH_percent));
                bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx2);

                progressBar12.Size = new System.Drawing.Size((int)(75 * SW_percent), (int)(150 * SH_percent));
                bx2.Controls.Add(progressBar12);
                progressBar12.BringToFront();
                progressBar12.Location = new System.Drawing.Point((int)(10), (int)(65));
                progressBar12.ForeColor = System.Drawing.Color.Orange;
                progressBar12.BackColor = System.Drawing.Color.Orange;
                progressBar12.TabIndex = 4;

                buttonToAnalysisData3.Location = new System.Drawing.Point((int)(500), (int)(400));
                buttonToAnalysisData3.Size = new System.Drawing.Size((int)(150), (int)(23));
                groupBox_pic.Controls.Add(buttonToAnalysisData3);
            }


          
     

            if (checkedListBox1.CheckedItems.Count == 2)
            {
                Label label31 = new Label();
                Label label32 = new Label();
                Label label33 = new Label();
                Label label35 = new Label();



                Label label37 = new Label();
                Label label38 = new Label();
                Label label39 = new Label();
                Label label41 = new Label();



                label31.Size = new System.Drawing.Size((int)(120), (int)(50));
                label31.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label31.Location = new System.Drawing.Point((int)((280)), (int)((115)));
                label31.Text = "";
                label31.AutoSize = false;
                groupBox_pic.Controls.Add(label31);

                label32.Size = new System.Drawing.Size((int)(120), (int)(50));
                label32.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label32.Location = new System.Drawing.Point((int)((280)), (int)((175)));
                label32.Text = "";
                groupBox_pic.Controls.Add(label32);

                label33.Size = new System.Drawing.Size((int)(120), (int)(50));
                label33.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label33.Location = new System.Drawing.Point((int)((280)), (int)((235)));
                label33.Text = "";
                groupBox_pic.Controls.Add(label33);

                label43.Size = new System.Drawing.Size((int)(120), (int)(50));
                label43.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label43.Location = new System.Drawing.Point((int)((280)), (int)((290)));
                label43.Text = "";
                groupBox_pic.Controls.Add(label43);

                label35.Size = new System.Drawing.Size((int)(120), (int)(50));
                label35.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label35.Location = new System.Drawing.Point((int)((280)), (int)((350)));
                label35.Text = "";
                groupBox_pic.Controls.Add(label35);

                label36.Size = new System.Drawing.Size((int)(120), (int)(50));
                label36.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label36.Location = new System.Drawing.Point((int)((280)), (int)((410)));
                label36.Text = "";
                groupBox_pic.Controls.Add(label36);


                label37.Size = new System.Drawing.Size((int)(120), (int)(50));
                label37.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label37.Location = new System.Drawing.Point((int)((800)), (int)((115)));
                label37.Text = "";
                groupBox_pic.Controls.Add(label37);

                label38.Size = new System.Drawing.Size((int)(120), (int)(50));
                label38.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label38.Location = new System.Drawing.Point((int)((800)), (int)((175)));
                label38.Text = "";
                groupBox_pic.Controls.Add(label38);

                label39.Size = new System.Drawing.Size((int)(120), (int)(50));
                label39.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label39.Location = new System.Drawing.Point((int)((800)), (int)((235)));
                label39.Text = "";
                groupBox_pic.Controls.Add(label39);

                label40.Size = new System.Drawing.Size((int)(120), (int)(50));
                label40.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label40.Location = new System.Drawing.Point((int)((800)), (int)((290)));
                label40.Text = "";
                groupBox_pic.Controls.Add(label40);

                label41.Size = new System.Drawing.Size((int)(120), (int)(50));
                label41.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label41.Location = new System.Drawing.Point((int)((800)), (int)((350)));
                label41.Text = "";
                groupBox_pic.Controls.Add(label41);

                label42.Size = new System.Drawing.Size((int)(120), (int)(50));
                label42.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label42.Location = new System.Drawing.Point((int)((800)), (int)((410)));
                label42.Text = "";
                groupBox_pic.Controls.Add(label42);



                //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                //string id = selectID(checkedListBox2.Items[k].ToString());
                string str = checkedListBox1.CheckedItems[0].ToString();
                string type = "";
                string id = selectID(str);
                string sql = "select * from bininfo where BinID = " + id;

                MySqlConn ms0 = new MySqlConn();
                MySqlDataReader rd0 = ms0.getDataFromTable(sql);
                while (rd0.Read())
                {
                    type = rd0["type"].ToString();
  
                }
                //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                rd0.Close();
                ms0.Close();

                if (type.Equals("1")|| type.Equals("0"))
                {
                    try
                    {

                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        string str0 = checkedListBox1.CheckedItems[0].ToString();
                        string id0 = selectID(str0);

                        correntWenDo = id0;


                        string data10 = Data.DataWenDuAndShiJian(comboBox4.Text, id0, "44", "0201");//2为实时查询温度。
                                                                                                 //show("发送的指令：" + data + "\r\n\r\n");
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询测量设备温度1111\r\n\r\n");
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id0, false, TIME, "查询仓内温度", data10, s_Produce));//指令为返回的代码


                        //查询仓内实时温度
                        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询仓内温度1111\r\n\r\n");
                        string data11 = Data.Data(comboBox4.Text, id0, "16", "0000");
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id0, false, TIME, "查询温湿度", data11, s_Produce));
                        numxuanzhong++;

                        string sql0 = "select * from bininfo where BinID = " + id0;
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql0);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        //float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            //label34.Text = rd1["Temp"].ToString() + "℃";
                            //label36.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误sss", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0, fangchang1 = 0, fangkuan1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox1.CheckedItems[1].ToString();
                    string id1 = selectID(str1);


                    correntWenDo = id1;


                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id1, "44", "0201");//2为实时查询温度。
                                                                                             //show("发送的指令：" + data + "\r\n\r\n");
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询测量设备温度22222\r\n\r\n");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id1, false, TIME, "查询仓内温度", data, s_Produce));//指令为返回的代码


                    //查询仓内实时温度
                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询仓内温度222222\r\n\r\n");
                    string data1 = Data.Data(comboBox4.Text, id1, "16", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id1, false, TIME, "查询温湿度", data1, s_Produce));

                    numxuanzhong++;

                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        //float Vol_ware = (float)(fangchang1 * fangkuan1 * cylinderh1 + (Math.PI * fangkuan1 * fangkuan1 * pyramidh1) / 12);
                        float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            //label40.Text = rd3["Temp"].ToString() + "℃";
                            //label42.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误!!!!", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }
                else if(type.Equals("4"))
                {
                    try
                    {

                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        string str0 = checkedListBox1.CheckedItems[0].ToString();
                        string id0 = selectID(str0);
                        string sql0 = "select * from bininfo where BinID = " + id0;
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql0);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        label31.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            label32.Text = rd1["Volume"].ToString() + "m³";
                            label33.Text = rd1["Weight"].ToString() + "吨";
                            label34.Text = rd1["Temp"].ToString() + "℃";
                            label35.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar10.Value = 0;
                                MessageBox.Show("数据有误sss", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str1 = checkedListBox1.CheckedItems[1].ToString();
                    string id1 = selectID(str1);
                    string sql1 = "select * from bininfo where BinID = " + id1;
                    // MessageBox.Show("*****************************"+id1);
                    try
                    {
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                        while (rd2.Read())
                        {
                            diameter1 = float.Parse(rd2["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                        rd2.Close();
                        ms2.Close();
                        float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                        label37.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                        while (rd3.Read())
                        {
                            label38.Text = rd3["Volume"].ToString() + "m³";
                            label39.Text = rd3["Weight"].ToString() + "吨";
                            label40.Text = rd3["Temp"].ToString() + "℃";
                            label41.Text = rd3["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd3["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar11.Value = 0;
                                MessageBox.Show("数据有误!!!!", "提示");
                            }
                            break;
                        }
                        rd3.Close();
                        ms3.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }
            }
            else if (checkedListBox2.CheckedItems.Count == 3)
            {
                Label label31 = new Label();
                Label label32 = new Label();
                Label label33 = new Label();
                Label label34 = new Label();
                Label label35 = new Label();
                Label label36 = new Label();


                Label label37 = new Label();
                Label label38 = new Label();
                Label label39 = new Label();
                Label label40 = new Label();
                Label label41 = new Label();
                Label label42 = new Label();


                Label label43 = new Label();
                Label label44 = new Label();
                Label label45 = new Label();
                Label label46 = new Label();
                Label label47 = new Label();
                Label label48 = new Label();

                label31.Size = new System.Drawing.Size((int)(120), (int)(50));
                label31.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label31.Location = new System.Drawing.Point((int)((180)), (int)((100)));
                label31.Text = "";
                label31.AutoSize = false;
                groupBox_pic.Controls.Add(label31);

                label32.Size = new System.Drawing.Size((int)(120), (int)(50));
                label32.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label32.Location = new System.Drawing.Point((int)((180)), (int)((150)));
                label32.Text = "";
                groupBox_pic.Controls.Add(label32);

                label33.Size = new System.Drawing.Size((int)(120), (int)(50));
                label33.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label33.Location = new System.Drawing.Point((int)((180)), (int)((200)));
                label33.Text = "";
                groupBox_pic.Controls.Add(label33);

                label34.Size = new System.Drawing.Size((int)(120), (int)(50));
                label34.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label34.Location = new System.Drawing.Point((int)((180)), (int)((250)));
                label34.Text = "";
                groupBox_pic.Controls.Add(label34);

                label35.Size = new System.Drawing.Size((int)(120), (int)(50));
                label35.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label35.Location = new System.Drawing.Point((int)((180)), (int)((300)));
                label35.Text = "";
                groupBox_pic.Controls.Add(label35);

                label36.Size = new System.Drawing.Size((int)(120), (int)(50));
                label36.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label36.Location = new System.Drawing.Point((int)((180)), (int)((350)));
                label36.Text = "";
                groupBox_pic.Controls.Add(label36);


                label37.Size = new System.Drawing.Size((int)(120), (int)(50));
                label37.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label37.Location = new System.Drawing.Point((int)((420)), (int)((100)));
                label37.Text = "";
                groupBox_pic.Controls.Add(label37);

                label38.Size = new System.Drawing.Size((int)(120), (int)(50));
                label38.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label38.Location = new System.Drawing.Point((int)((420)), (int)((150)));
                label38.Text = "";
                groupBox_pic.Controls.Add(label38);

                label39.Size = new System.Drawing.Size((int)(120), (int)(50));
                label39.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label39.Location = new System.Drawing.Point((int)((420)), (int)((200)));
                label39.Text = "";
                groupBox_pic.Controls.Add(label39);

                label40.Size = new System.Drawing.Size((int)(120), (int)(50));
                label40.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label40.Location = new System.Drawing.Point((int)((420)), (int)((250)));
                label40.Text = "";
                groupBox_pic.Controls.Add(label40);

                label41.Size = new System.Drawing.Size((int)(120), (int)(50));
                label41.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label41.Location = new System.Drawing.Point((int)((420)), (int)((300)));
                label41.Text = "";
                groupBox_pic.Controls.Add(label41);

                label42.Size = new System.Drawing.Size((int)(120), (int)(50));
                label42.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label42.Location = new System.Drawing.Point((int)((420)), (int)((350)));
                label42.Text = "";
                groupBox_pic.Controls.Add(label42);



                label43.Size = new System.Drawing.Size((int)(120), (int)(50));
                label43.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label43.Location = new System.Drawing.Point((int)((720)), (int)((100)));
                label43.Text = "";
                groupBox_pic.Controls.Add(label43);

                label44.Size = new System.Drawing.Size((int)(120), (int)(50));
                label44.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label44.Location = new System.Drawing.Point((int)((720)), (int)((150)));
                label44.Text = "";
                groupBox_pic.Controls.Add(label44);

                label45.Size = new System.Drawing.Size((int)(120), (int)(50));
                label45.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label45.Location = new System.Drawing.Point((int)((720)), (int)((200)));
                label45.Text = "";
                groupBox_pic.Controls.Add(label45);

                label46.Size = new System.Drawing.Size((int)(120), (int)(50));
                label46.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label46.Location = new System.Drawing.Point((int)((720)), (int)((250)));
                label46.Text = "";
                groupBox_pic.Controls.Add(label46);

                label47.Size = new System.Drawing.Size((int)(120), (int)(50));
                label47.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label47.Location = new System.Drawing.Point((int)((720)), (int)((300)));
                label47.Text = "";
                groupBox_pic.Controls.Add(label47);

                label48.Size = new System.Drawing.Size((int)(120), (int)(50));
                label48.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label48.Location = new System.Drawing.Point((int)((720)), (int)((350)));
                label48.Text = "";
                groupBox_pic.Controls.Add(label48);

                float diameter = 0, cylinderh = 0, pyramidh = 0;
                //ID1.Text = checkedListBox2.CheckedItems[3].ToString();
                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                //string id = selectID(checkedListBox2.Items[k].ToString());
                string str = checkedListBox1.CheckedItems[0].ToString();
                string id = selectID(str);
                string sql = "select * from bininfo where BinID = " + id;
                try
                {
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rd = ms.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        diameter = float.Parse(rd["Diameter"].ToString());
                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                        pyramidh = float.Parse(rd["PyramidH"].ToString());

                    }
                    //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                    rd.Close();
                    ms.Close();
                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                    label31.Text = Vol_ware.ToString() + "m³";
                    //MessageBox.Show("第一个图的ID" + id);


                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                    MySqlConn ms1 = new MySqlConn();
                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                    while (rd1.Read())
                    {
                        label32.Text = rd1["Volume"].ToString() + "m³";
                        label33.Text = rd1["Weight"].ToString() + "吨";
                        label34.Text = rd1["Temp"].ToString() + "℃";
                        label35.Text = rd1["Hum"].ToString() + "%";
                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                        if (vol_feed < Vol_ware)
                        {
                            progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                        }
                        else
                        {
                            progressBar10.Value = 0;
                            MessageBox.Show("数据有误", "提示");
                        }
                        break;
                    }
                    rd1.Close();
                    ms1.Close();

                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                }



                float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                //ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                //string id = selectID(checkedListBox2.Items[k].ToString());
                string str1 = checkedListBox1.CheckedItems[1].ToString();
                string id1 = selectID(str1);
                string sql1 = "select * from bininfo where BinID = " + id1;
                // MessageBox.Show("*****************************"+id1);
                try
                {
                    MySqlConn ms2 = new MySqlConn();
                    MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                    while (rd2.Read())
                    {
                        diameter1 = float.Parse(rd2["Diameter"].ToString());
                        cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                        pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                    }
                    //MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                    rd2.Close();
                    ms2.Close();
                    float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                    label37.Text = Vol_ware.ToString() + "m³";
                    //MessageBox.Show("第一个图的ID" + id);


                    sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                    MySqlConn ms3 = new MySqlConn();
                    MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                    while (rd3.Read())
                    {
                        label38.Text = rd3["Volume"].ToString() + "m³";
                        label39.Text = rd3["Weight"].ToString() + "吨";
                        label40.Text = rd3["Temp"].ToString() + "℃";
                        label41.Text = rd3["Hum"].ToString() + "%";
                        float vol_feed = float.Parse(rd3["Volume"].ToString());
                        if (vol_feed < Vol_ware)
                        {
                            progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                        }
                        else
                        {
                            progressBar11.Value = 0;
                            MessageBox.Show("数据有误", "提示");
                        }
                        break;
                    }
                    rd3.Close();
                    ms3.Close();

                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                }


                float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                //ID6.Text = checkedListBox2.CheckedItems[2].ToString();
                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                //string id = selectID(checkedListBox2.Items[k].ToString());
                string str2 = checkedListBox1.CheckedItems[2].ToString();
                string id2 = selectID(str2);
                string sql2 = "select * from bininfo where BinID = " + id2;
                //MessageBox.Show("*****************************" + id2);
                try
                {
                    MySqlConn ms4 = new MySqlConn();
                    MySqlDataReader rd4 = ms4.getDataFromTable(sql2);
                    while (rd4.Read())
                    {
                        diameter2 = float.Parse(rd4["Diameter"].ToString());
                        cylinderh2 = float.Parse(rd4["CylinderH"].ToString());
                        pyramidh2 = float.Parse(rd4["PyramidH"].ToString());

                    }
                    //MessageBox.Show("Diameter" + diameter2 + "CylinderH" + cylinderh2 + "PyramidH" + pyramidh2);

                    rd4.Close();
                    ms4.Close();
                    float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                    label43.Text = Vol_ware.ToString() + "m³";
                    //MessageBox.Show("第一个图的ID" + id);


                    sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                    MySqlConn ms5 = new MySqlConn();
                    MySqlDataReader rd5 = ms5.getDataFromTable(sql2);

                    while (rd5.Read())
                    {
                        label44.Text = rd5["Volume"].ToString() + "m³";
                        label45.Text = rd5["Weight"].ToString() + "吨";
                        label46.Text = rd5["Temp"].ToString() + "℃";
                        label47.Text = rd5["Hum"].ToString() + "%";
                        float vol_feed = float.Parse(rd5["Volume"].ToString());
                        if (vol_feed < Vol_ware)
                        {
                            progressBar12.Value = (int)((vol_feed / Vol_ware) * 100);
                        }
                        else
                        {
                            progressBar12.Value = 0;
                            MessageBox.Show("数据有误", "提示");
                        }
                        break;
                    }
                    rd5.Close();
                    ms5.Close();

                }
                catch (SqlException se)
                {
                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    thread_file.Start(se.ToString());
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                }


            }


        }



        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// 在线料仓显示四到六个
        /// </summary>
        /// <param name="SW"></param>
        /// <param name="SH"></param>
        /// <param name="num"></param>
        private void ShowFour1(double SW, double SH, int num)
        {
            double SW_percent = SW;
            double SH_percent = SH;
            int kuan = 369;
            int tukuan = 36;
            int n = num;
            float bili = 16F / n;//字体比例
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            //MessageBox.Show("SW_percent" + SW_percent + "SH_percent" + SH_percent + "n" + n);

            PictureBox bx = new PictureBox();
            bx.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx.Location = new System.Drawing.Point((int)(20), (int)(50));
            bx.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx);

            progressBar10.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx.Controls.Add(progressBar10);
            progressBar10.BringToFront();
            progressBar10.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar10.ForeColor = System.Drawing.Color.Orange;
            progressBar10.BackColor = System.Drawing.Color.Orange;
            progressBar10.TabIndex = 4;


            buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(200));
            buttonToAnalysisData1.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData1.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData1);



            ID61.Text = checkedListBox1.CheckedItems[0].ToString();
            groupBox_pic.Controls.Add(ID61);
            ID61.BringToFront();


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((140)), (int)((30 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }

            PictureBox bx1 = new PictureBox();
            bx1.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx1.Location = new System.Drawing.Point((int)(270), (int)(50));
            bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx1);


            progressBar11.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx1.Controls.Add(progressBar11);
            progressBar11.BringToFront();
            progressBar11.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar11.ForeColor = System.Drawing.Color.Orange;
            progressBar11.BackColor = System.Drawing.Color.Orange;
            progressBar11.TabIndex = 4;

            buttonToAnalysisData2.Location = new System.Drawing.Point((int)(290), (int)(200));
            buttonToAnalysisData2.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData2.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData2);



            ID62.Text = checkedListBox1.CheckedItems[1].ToString();
            groupBox_pic.Controls.Add(ID62);
            ID62.BringToFront();

            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((390)), (int)((30 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }



            PictureBox bx2 = new PictureBox();
            bx2.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx2.Location = new System.Drawing.Point((int)(530), (int)(50));
            bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx2);


            progressBar12.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx2.Controls.Add(progressBar12);
            progressBar12.BringToFront();
            progressBar12.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar12.ForeColor = System.Drawing.Color.Orange;
            progressBar12.BackColor = System.Drawing.Color.Orange;
            progressBar12.TabIndex = 4;

            buttonToAnalysisData3.Location = new System.Drawing.Point((int)(530), (int)(200));
            buttonToAnalysisData3.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData3.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData3);

            ID63.Text = checkedListBox1.CheckedItems[2].ToString();
            groupBox_pic.Controls.Add(ID63);
            ID63.BringToFront();

            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((650)), (int)((30 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }


            if (n == 4)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);

                progressBar13.Size = new System.Drawing.Size((int)(52), (int)(90));
                bx3.Controls.Add(progressBar13);
                progressBar13.BringToFront();
                progressBar13.Location = new System.Drawing.Point((int)(22), (int)(25));
                progressBar13.ForeColor = System.Drawing.Color.Orange;
                progressBar13.BackColor = System.Drawing.Color.Orange;
                progressBar13.TabIndex = 4;

                buttonToAnalysisData4.Location = new System.Drawing.Point((int)(16), (int)(400));
                buttonToAnalysisData4.Size = new System.Drawing.Size((int)(80), (int)(23));
                buttonToAnalysisData4.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(buttonToAnalysisData4);


                ID64.Text = checkedListBox1.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

            }
            else if (n == 5)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);

                ID64.Text = checkedListBox1.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx4 = new PictureBox();
                bx4.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx4.Location = new System.Drawing.Point((int)(270), (int)(250));
                bx4.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx4.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx4);

                ID65.Text = checkedListBox1.CheckedItems[4].ToString();
                groupBox_pic.Controls.Add(ID65);
                ID65.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }


            }
            else if (n == 6)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);


                ID64.Text = checkedListBox1.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx4 = new PictureBox();
                bx4.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx4.Location = new System.Drawing.Point((int)(270), (int)(250));
                bx4.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx4.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx4);

                ID65.Text = checkedListBox1.CheckedItems[4].ToString();
                groupBox_pic.Controls.Add(ID65);
                ID65.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx5 = new PictureBox();
                bx5.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx5.Location = new System.Drawing.Point((int)(530), (int)(250));
                bx5.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx5.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx5);


                ID66.Text = checkedListBox1.CheckedItems[5].ToString();
                groupBox_pic.Controls.Add(ID66);
                ID66.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((650)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

            }


            initLabel3(n);

        }

        /// <summary>
        /// 不在线料仓显示四个到六个
        /// </summary>
        /// <param name="SW"></param>
        /// <param name="SH"></param>
        /// <param name="num"></param>
        private void ShowFour(double SW, double SH, int num)
        {
            double SW_percent = SW;
            double SH_percent = SH;
            int kuan = 369;
            int tukuan = 36;
            int n = num;
            float bili = 16F / n;//字体比例
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            //MessageBox.Show("SW_percent" + SW_percent + "SH_percent" + SH_percent + "n" + n);

            PictureBox bx = new PictureBox();
            bx.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx.Location = new System.Drawing.Point((int)(20), (int)(50));
            bx.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx);

            progressBar10.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx.Controls.Add(progressBar10);
            progressBar10.BringToFront();
            progressBar10.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar10.ForeColor = System.Drawing.Color.Orange;
            progressBar10.BackColor = System.Drawing.Color.Orange;
            progressBar10.TabIndex = 4;


            buttonToAnalysisData1.Location = new System.Drawing.Point((int)(16), (int)(200));
            buttonToAnalysisData1.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData1.Font= new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData1);


            ID61.Text = checkedListBox2.CheckedItems[0].ToString();
            groupBox_pic.Controls.Add(ID61);
            ID61.BringToFront();


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((140)), (int)((30 + (j + 1) * 30) ));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }

            PictureBox bx1 = new PictureBox();
            bx1.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx1.Location = new System.Drawing.Point((int)(270), (int)(50));
            bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx1);

            progressBar11.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx1.Controls.Add(progressBar11);
            progressBar11.BringToFront();
            progressBar11.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar11.ForeColor = System.Drawing.Color.Orange;
            progressBar11.BackColor = System.Drawing.Color.Orange;
            progressBar11.TabIndex = 4;

            buttonToAnalysisData2.Location = new System.Drawing.Point((int)(290), (int)(200));
            buttonToAnalysisData2.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData2.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData2);


            ID62.Text = checkedListBox2.CheckedItems[1].ToString();
            groupBox_pic.Controls.Add(ID62);
            ID62.BringToFront();

            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((390)), (int)((30 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }



            PictureBox bx2 = new PictureBox();
            bx2.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx2.Location = new System.Drawing.Point((int)(530), (int)(50));
            bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx2);

            progressBar12.Size = new System.Drawing.Size((int)(52), (int)(90));
            bx2.Controls.Add(progressBar12);
            progressBar12.BringToFront();
            progressBar12.Location = new System.Drawing.Point((int)(22), (int)(25));
            progressBar12.ForeColor = System.Drawing.Color.Orange;
            progressBar12.BackColor = System.Drawing.Color.Orange;
            progressBar12.TabIndex = 4;

            buttonToAnalysisData3.Location = new System.Drawing.Point((int)(530), (int)(200));
            buttonToAnalysisData3.Size = new System.Drawing.Size((int)(80), (int)(23));
            buttonToAnalysisData3.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            groupBox_pic.Controls.Add(buttonToAnalysisData3);


            ID63.Text = checkedListBox2.CheckedItems[2].ToString();
            groupBox_pic.Controls.Add(ID63);
            ID63.BringToFront();

            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((650)), (int)((30 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }


            if (n == 4)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);



                progressBar13.Size = new System.Drawing.Size((int)(52), (int)(90));
                bx3.Controls.Add(progressBar13);
                progressBar13.BringToFront();
                progressBar13.Location = new System.Drawing.Point((int)(22), (int)(25));
                progressBar13.ForeColor = System.Drawing.Color.Orange;
                progressBar13.BackColor = System.Drawing.Color.Orange;
                progressBar13.TabIndex = 4;

                buttonToAnalysisData4.Location = new System.Drawing.Point((int)(16), (int)(400));
                buttonToAnalysisData4.Size = new System.Drawing.Size((int)(80), (int)(23));
                buttonToAnalysisData4.Font = new System.Drawing.Font("宋体", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(buttonToAnalysisData4);

                ID64.Text = checkedListBox2.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

            }
            else if(n == 5)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);

                ID64.Text = checkedListBox2.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx4 = new PictureBox();
                bx4.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx4.Location = new System.Drawing.Point((int)(270), (int)(250));
                bx4.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx4.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx4);
                
                ID65.Text = checkedListBox2.CheckedItems[4].ToString();
                groupBox_pic.Controls.Add(ID65);
                ID65.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }


            }
            else if(n == 6)
            {
                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(250));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);


                ID64.Text = checkedListBox2.CheckedItems[3].ToString();
                groupBox_pic.Controls.Add(ID64);
                ID64.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx4 = new PictureBox();
                bx4.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx4.Location = new System.Drawing.Point((int)(270), (int)(250));
                bx4.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx4.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx4);

                ID65.Text = checkedListBox2.CheckedItems[4].ToString();
                groupBox_pic.Controls.Add(ID65);
                ID65.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                PictureBox bx5 = new PictureBox();
                bx5.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx5.Location = new System.Drawing.Point((int)(530), (int)(250));
                bx5.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx5.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx5);

              
                ID66.Text = checkedListBox2.CheckedItems[5].ToString();
                groupBox_pic.Controls.Add(ID66);
                ID66.BringToFront();

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((650)), (int)((220 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

            }


            initLabel2(n);

        }

        private void initLabel2(int n)
        {
            int num = n;
            datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb11.Location = new System.Drawing.Point((int)((220)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb11);

            datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb12.Location = new System.Drawing.Point((int)((220)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb12);


            datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb13.Location = new System.Drawing.Point((int)((220)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb13);


            datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb14.Location = new System.Drawing.Point((int)((220)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb14);


            datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb15.Location = new System.Drawing.Point((int)((220)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb15);

            datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb16.Location = new System.Drawing.Point((int)((220)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb16);

            datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb21.Location = new System.Drawing.Point((int)((470)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb21);

            datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb22.Location = new System.Drawing.Point((int)((470)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb22);


            datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb23.Location = new System.Drawing.Point((int)((470)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb23);


            datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb24.Location = new System.Drawing.Point((int)((470)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb24);


            datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb25.Location = new System.Drawing.Point((int)((470)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb25);

            datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb26.Location = new System.Drawing.Point((int)((470)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb26);

            datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb31.Location = new System.Drawing.Point((int)((730)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb31);

            datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
            datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb32.Location = new System.Drawing.Point((int)((730)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb32);


            datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb33.Location = new System.Drawing.Point((int)((730)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb33);


            datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb34.Location = new System.Drawing.Point((int)((730)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb34);


            datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb35.Location = new System.Drawing.Point((int)((730)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb35);

            datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb36.Location = new System.Drawing.Point((int)((730)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb36);

            ///////////////////////////////////////////////////////////////////////////////////////////////////

            datalb41.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb41.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb41.Location = new System.Drawing.Point((int)((220)), (int)((220 + 30)));
            groupBox_pic.Controls.Add(datalb41);

            datalb42.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb42.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb42.Location = new System.Drawing.Point((int)((220)), (int)((220 + 60)));
            groupBox_pic.Controls.Add(datalb42);


            datalb43.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb43.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb43.Location = new System.Drawing.Point((int)((220)), (int)((220 + 90)));
            groupBox_pic.Controls.Add(datalb43);


            datalb44.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb44.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb44.Location = new System.Drawing.Point((int)((220)), (int)((220 + 120)));
            groupBox_pic.Controls.Add(datalb44);


            datalb45.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb45.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb45.Location = new System.Drawing.Point((int)((220)), (int)((220 + 150)));
            groupBox_pic.Controls.Add(datalb45);

            datalb46.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb46.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb46.Location = new System.Drawing.Point((int)((220)), (int)((220 + 180)));
            groupBox_pic.Controls.Add(datalb46);
            if (n==5)
            {
                datalb51.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb51.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb51.Location = new System.Drawing.Point((int)((470)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb51);

                datalb52.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb52.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb52.Location = new System.Drawing.Point((int)((470)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb52);


                datalb53.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb53.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb53.Location = new System.Drawing.Point((int)((470)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb53);


                datalb54.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb54.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb54.Location = new System.Drawing.Point((int)((470)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb54);


                datalb55.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb55.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb55.Location = new System.Drawing.Point((int)((470)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb55);

                datalb56.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb56.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb56.Location = new System.Drawing.Point((int)((470)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb56);

            }else if (n == 6)
            {
                datalb51.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb51.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb51.Location = new System.Drawing.Point((int)((470)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb51);

                datalb52.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb52.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb52.Location = new System.Drawing.Point((int)((470)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb52);


                datalb53.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb53.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb53.Location = new System.Drawing.Point((int)((470)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb53);


                datalb54.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb54.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb54.Location = new System.Drawing.Point((int)((470)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb54);


                datalb55.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb55.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb55.Location = new System.Drawing.Point((int)((470)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb55);

                datalb56.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb56.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb56.Location = new System.Drawing.Point((int)((470)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb56);

                datalb61.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb61.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb61.Location = new System.Drawing.Point((int)((730)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb61);

                datalb62.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb62.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb62.Location = new System.Drawing.Point((int)((730)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb62);


                datalb63.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb63.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb63.Location = new System.Drawing.Point((int)((730)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb63);


                datalb64.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb64.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb64.Location = new System.Drawing.Point((int)((730)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb64);


                datalb65.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb65.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb65.Location = new System.Drawing.Point((int)((730)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb65);

                datalb66.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb66.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb66.Location = new System.Drawing.Point((int)((730)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb66);


            }

            initData1(num);

        }

        /// <summary>
        /// 在线料仓初始化样式
        /// </summary>
        /// <param name="n"></param>
        private void initLabel3(int n)
        {
            int num = n;
            datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb11.Location = new System.Drawing.Point((int)((220)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb11);

            datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb12.Location = new System.Drawing.Point((int)((220)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb12);


            datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb13.Location = new System.Drawing.Point((int)((220)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb13);


            datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb14.Location = new System.Drawing.Point((int)((220)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb14);


            datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb15.Location = new System.Drawing.Point((int)((220)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb15);

            datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb16.Location = new System.Drawing.Point((int)((220)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb16);

            datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb21.Location = new System.Drawing.Point((int)((470)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb21);

            datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb22.Location = new System.Drawing.Point((int)((470)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb22);


            datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb23.Location = new System.Drawing.Point((int)((470)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb23);


            datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb24.Location = new System.Drawing.Point((int)((470)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb24);


            datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb25.Location = new System.Drawing.Point((int)((470)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb25);

            datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb26.Location = new System.Drawing.Point((int)((470)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb26);

            datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb31.Location = new System.Drawing.Point((int)((730)), (int)((30 + 30)));
            groupBox_pic.Controls.Add(datalb31);

            datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
            datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb32.Location = new System.Drawing.Point((int)((730)), (int)((30 + 60)));
            groupBox_pic.Controls.Add(datalb32);


            datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb33.Location = new System.Drawing.Point((int)((730)), (int)((30 + 90)));
            groupBox_pic.Controls.Add(datalb33);


            datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb34.Location = new System.Drawing.Point((int)((730)), (int)((30 + 120)));
            groupBox_pic.Controls.Add(datalb34);


            datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb35.Location = new System.Drawing.Point((int)((730)), (int)((30 + 150)));
            groupBox_pic.Controls.Add(datalb35);

            datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb36.Location = new System.Drawing.Point((int)((730)), (int)((30 + 180)));
            groupBox_pic.Controls.Add(datalb36);

            ///////////////////////////////////////////////////////////////////////////////////////////////////

            datalb41.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb41.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb41.Location = new System.Drawing.Point((int)((220)), (int)((220 + 30)));
            groupBox_pic.Controls.Add(datalb41);

            datalb42.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb42.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb42.Location = new System.Drawing.Point((int)((220)), (int)((220 + 60)));
            groupBox_pic.Controls.Add(datalb42);


            datalb43.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb43.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb43.Location = new System.Drawing.Point((int)((220)), (int)((220 + 90)));
            groupBox_pic.Controls.Add(datalb43);


            datalb44.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb44.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb44.Location = new System.Drawing.Point((int)((220)), (int)((220 + 120)));
            groupBox_pic.Controls.Add(datalb44);


            datalb45.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb45.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb45.Location = new System.Drawing.Point((int)((220)), (int)((220 + 150)));
            groupBox_pic.Controls.Add(datalb45);

            datalb46.Size = new System.Drawing.Size((int)(80), (int)(20));
            datalb46.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            datalb46.Location = new System.Drawing.Point((int)((220)), (int)((220 + 180)));
            groupBox_pic.Controls.Add(datalb46);
            if (n == 5)
            {
                datalb51.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb51.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb51.Location = new System.Drawing.Point((int)((470)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb51);

                datalb52.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb52.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb52.Location = new System.Drawing.Point((int)((470)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb52);


                datalb53.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb53.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb53.Location = new System.Drawing.Point((int)((470)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb53);


                datalb54.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb54.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb54.Location = new System.Drawing.Point((int)((470)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb54);


                datalb55.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb55.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb55.Location = new System.Drawing.Point((int)((470)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb55);

                datalb56.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb56.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb56.Location = new System.Drawing.Point((int)((470)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb56);

            }
            else if (n == 6)
            {
                datalb51.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb51.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb51.Location = new System.Drawing.Point((int)((470)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb51);

                datalb52.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb52.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb52.Location = new System.Drawing.Point((int)((470)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb52);


                datalb53.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb53.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb53.Location = new System.Drawing.Point((int)((470)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb53);


                datalb54.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb54.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb54.Location = new System.Drawing.Point((int)((470)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb54);


                datalb55.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb55.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb55.Location = new System.Drawing.Point((int)((470)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb55);

                datalb56.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb56.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb56.Location = new System.Drawing.Point((int)((470)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb56);

                datalb61.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb61.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb61.Location = new System.Drawing.Point((int)((730)), (int)((220 + 30)));
                groupBox_pic.Controls.Add(datalb61);

                datalb62.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb62.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb62.Location = new System.Drawing.Point((int)((730)), (int)((220 + 60)));
                groupBox_pic.Controls.Add(datalb62);


                datalb63.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb63.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb63.Location = new System.Drawing.Point((int)((730)), (int)((220 + 90)));
                groupBox_pic.Controls.Add(datalb63);


                datalb64.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb64.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb64.Location = new System.Drawing.Point((int)((730)), (int)((220 + 120)));
                groupBox_pic.Controls.Add(datalb64);


                datalb65.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb65.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb65.Location = new System.Drawing.Point((int)((730)), (int)((220 + 150)));
                groupBox_pic.Controls.Add(datalb65);

                datalb66.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb66.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb66.Location = new System.Drawing.Point((int)((730)), (int)((220 + 180)));
                groupBox_pic.Controls.Add(datalb66);


            }

            initData2(num);

        }

        private void initData1(int n)
        {
            int num = n;
            //MessageBox.Show("选中的数量" + num);
            int t = 0;
            if (SqlConnect == 1)
            {
                for(int i = 0; i < 3; i++)
                {
                    if (t == 0)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang=0,fangkuan=0;
                        ID1.Text = checkedListBox2.CheckedItems[0].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                        //string id = selectID(checkedListBox2.Items[k].ToString());
                        string str = checkedListBox2.CheckedItems[0].ToString();
                        string id = selectID(str);
                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());

                            }
                            //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb11.Text = Vol_ware.ToString() + "m³";
                            //MessageBox.Show("第一个图的ID" + id);


                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb12.Text = rd1["Volume"].ToString() + "m³";
                                datalb13.Text = rd1["Weight"].ToString() + "吨";
                                datalb14.Text = rd1["Temp"].ToString() + "℃";
                                datalb15.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar10.Value = 0;
                                    MessageBox.Show("数据有误41", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                        t++;

                    }
                    else if (t == 1)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        ID2.Text = checkedListBox2.CheckedItems[1].ToString();
                        //MessageBox.Show("K+++++++++++++值" + k);
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                        string str = checkedListBox2.CheckedItems[1].ToString();
                        //MessageBox.Show("第二个图str" + str);
                        string id = selectID(str);
                        //string id = selectID("8号筒仓");
                        //MessageBox.Show("第二个图的ID" + id);

                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb21.Text = Vol_ware.ToString() + "m³";




                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb22.Text = rd1["Volume"].ToString() + "m³";
                                datalb23.Text = rd1["Weight"].ToString() + "吨";
                                datalb24.Text = rd1["Temp"].ToString() + "℃";
                                datalb25.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar11.Value = 0;
                                    MessageBox.Show("数据有误42", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                        t++;

                    }
                    else if (t == 2)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        ID3.Text = checkedListBox2.CheckedItems[2].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                        string str = checkedListBox2.CheckedItems[2].ToString();
                        string id = selectID(str);
                        //string id = selectID("1号筒仓");

                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb31.Text = Vol_ware.ToString() + "m³";


                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb32.Text = rd1["Volume"].ToString() + "m³";
                                datalb33.Text = rd1["Weight"].ToString() + "吨";
                                datalb34.Text = rd1["Temp"].ToString() + "℃";
                                datalb35.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar12.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar12.Value = 0;
                                    MessageBox.Show("数据有误43", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                    }

                }



                if (num >= 4)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                    ID4.Text = checkedListBox2.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox2.CheckedItems[3].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            fangchang = float.Parse(rd["Fbian"].ToString());
                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        datalb41.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb42.Text = rd1["Volume"].ToString() + "m³";
                            datalb43.Text = rd1["Weight"].ToString() + "吨";
                            datalb44.Text = rd1["Temp"].ToString() + "℃";
                            datalb45.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar13.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar13.Value = 0;
                                MessageBox.Show("数据有误44", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                    if (num >= 5)
                    {
                        //
                        float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                        ID5.Text = checkedListBox2.CheckedItems[4].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                        //string id = selectID(checkedListBox2.Items[k].ToString());
                        string str1 = checkedListBox2.CheckedItems[4].ToString();
                        string id1 = selectID(str1);
                        string sql1 = "select * from bininfo where BinID = " + id1;
                       // MessageBox.Show("*****************************"+id1);
                        try
                        {
                            MySqlConn ms2 = new MySqlConn();
                            MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                            while (rd2.Read())
                            {
                                diameter1 = float.Parse(rd2["Diameter"].ToString());
                                cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                                pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                            }
                            //MessageBox.Show("Diameter" + diameter1 + "CylinderH"+ cylinderh1+ "PyramidH"+ pyramidh1);

                            rd2.Close();
                            ms2.Close();
                            float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                            datalb51.Text = Vol_ware.ToString() + "m³";
                            //MessageBox.Show("第一个图的ID" + id);


                            sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms3 = new MySqlConn();
                            MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                            while (rd3.Read())
                            {
                                datalb52.Text = rd3["Volume"].ToString() + "m³";
                                datalb53.Text = rd3["Weight"].ToString() + "吨";
                                datalb54.Text = rd3["Temp"].ToString() + "℃";
                                datalb55.Text = rd3["Hum"].ToString() + "%";
                                //float vol_feed = float.Parse(rd1["Volume"].ToString());
                                //if (vol_feed < Vol_ware)
                                //{
                                //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                //}
                                //else
                                //{
                                //    progressBar1.Value = 0;
                                //    MessageBox.Show("数据有误", "提示");
                                //}
                                break;
                            }
                            rd3.Close();
                            ms3.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                        if(num >= 6)
                        {
                            float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                            ID6.Text = checkedListBox2.CheckedItems[5].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                            //string id = selectID(checkedListBox2.Items[k].ToString());
                            string str2 = checkedListBox2.CheckedItems[5].ToString();
                            string id2 = selectID(str2);
                            string sql2 = "select * from bininfo where BinID = " + id2;
                            //MessageBox.Show("*****************************" + id2);
                            try
                            {
                                MySqlConn ms4 = new MySqlConn();
                                MySqlDataReader rd4 = ms4.getDataFromTable(sql2);
                                while (rd4.Read())
                                {
                                    diameter2 = float.Parse(rd4["Diameter"].ToString());
                                    cylinderh2 = float.Parse(rd4["CylinderH"].ToString());
                                    pyramidh2 = float.Parse(rd4["PyramidH"].ToString());

                                }
                                MessageBox.Show("Diameter" + diameter2 + "CylinderH" + cylinderh2 + "PyramidH" + pyramidh2);

                                rd4.Close();
                                ms4.Close();
                                float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                                datalb61.Text = Vol_ware.ToString() + "m³";
                                //MessageBox.Show("第一个图的ID" + id);


                                sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms5 = new MySqlConn();
                                MySqlDataReader rd5 = ms5.getDataFromTable(sql2);

                                while (rd5.Read())
                                {
                                    datalb62.Text = rd5["Volume"].ToString() + "m³";
                                    datalb63.Text = rd5["Weight"].ToString() + "吨";
                                    datalb64.Text = rd5["Temp"].ToString() + "℃";
                                    datalb65.Text = rd5["Hum"].ToString() + "%";
                                    //float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd5.Close();
                                ms5.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }

                        }
                    }

                }


            }

        }

        private void initData2(int n)
        {
            int num = n;
            //MessageBox.Show("选中的数量" + num);
            int t = 0;
            if (SqlConnect == 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (t == 0)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0,fangchang = 0,fangkuan = 0;
                        ID1.Text = checkedListBox1.CheckedItems[0].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                        //string id = selectID(checkedListBox2.Items[k].ToString());
                        string str = checkedListBox1.CheckedItems[0].ToString();
                        string id = selectID(str);
                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());

                            }
                            //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb11.Text = Vol_ware.ToString() + "m³";
                            //MessageBox.Show("第一个图的ID" + id);


                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb12.Text = rd1["Volume"].ToString() + "m³";
                                datalb13.Text = rd1["Weight"].ToString() + "吨";
                                datalb14.Text = rd1["Temp"].ToString() + "℃";
                                datalb15.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar10.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar10.Value = 0;
                                    MessageBox.Show("数据有误", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                        t++;

                    }
                    else if (t == 1)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        ID2.Text = checkedListBox1.CheckedItems[1].ToString();
                        //MessageBox.Show("K+++++++++++++值" + k);
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                        string str = checkedListBox1.CheckedItems[1].ToString();
                        //MessageBox.Show("第二个图str" + str);
                        string id = selectID(str);
                        //string id = selectID("8号筒仓");
                        //MessageBox.Show("第二个图的ID" + id);

                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb21.Text = Vol_ware.ToString() + "m³";




                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb22.Text = rd1["Volume"].ToString() + "m³";
                                datalb23.Text = rd1["Weight"].ToString() + "吨";
                                datalb24.Text = rd1["Temp"].ToString() + "℃";
                                datalb25.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar11.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar11.Value = 0;
                                    MessageBox.Show("数据有误", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                        t++;

                    }
                    else if (t == 2)
                    {
                        float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0;
                        ID3.Text = checkedListBox1.CheckedItems[2].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                        string str = checkedListBox1.CheckedItems[2].ToString();
                        string id = selectID(str);
                        //string id = selectID("1号筒仓");

                        string sql = "select * from bininfo where BinID = " + id;
                        try
                        {
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(sql);
                            while (rd.Read())
                            {
                                fangchang = float.Parse(rd["Fbian"].ToString());
                                fangkuan = float.Parse(rd["Fkuan"].ToString());
                                cylinderh = float.Parse(rd["CylinderH"].ToString());
                                pyramidh = float.Parse(rd["PyramidH"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                            datalb31.Text = Vol_ware.ToString() + "m³";


                            sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms1 = new MySqlConn();
                            MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                            while (rd1.Read())
                            {
                                datalb32.Text = rd1["Volume"].ToString() + "m³";
                                datalb33.Text = rd1["Weight"].ToString() + "吨";
                                datalb34.Text = rd1["Temp"].ToString() + "℃";
                                datalb35.Text = rd1["Hum"].ToString() + "%";
                                float vol_feed = float.Parse(rd1["Volume"].ToString());
                                if (vol_feed < Vol_ware)
                                {
                                    progressBar12.Value = (int)((vol_feed / Vol_ware) * 100);
                                }
                                else
                                {
                                    progressBar12.Value = 0;
                                    MessageBox.Show("数据有误", "提示");
                                }
                                break;
                            }
                            rd1.Close();
                            ms1.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }
                    }

                }



                if (num >= 4)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0, fangchang = 0, fangkuan = 0; 
                    ID1.Text = checkedListBox1.CheckedItems[3].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());
                    string str = checkedListBox1.CheckedItems[3].ToString();
                    string id = selectID(str);
                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            fangchang = float.Parse(rd["Fbian"].ToString());
                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                        datalb41.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb42.Text = rd1["Volume"].ToString() + "m³";
                            datalb43.Text = rd1["Weight"].ToString() + "吨";
                            datalb44.Text = rd1["Temp"].ToString() + "℃";
                            datalb45.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            if (vol_feed < Vol_ware)
                            {
                                progressBar13.Value = (int)((vol_feed / Vol_ware) * 100);
                            }
                            else
                            {
                                progressBar13.Value = 0;
                                MessageBox.Show("数据有误", "提示");
                            }
                            break;
                        }
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                    if (num >= 5)
                    {
                        //
                        float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                        ID5.Text = checkedListBox1.CheckedItems[4].ToString();
                        //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                        //string id = selectID(checkedListBox2.Items[k].ToString());
                        string str1 = checkedListBox1.CheckedItems[4].ToString();
                        string id1 = selectID(str1);
                        string sql1 = "select * from bininfo where BinID = " + id1;
                        // MessageBox.Show("*****************************"+id1);
                        try
                        {
                            MySqlConn ms2 = new MySqlConn();
                            MySqlDataReader rd2 = ms2.getDataFromTable(sql1);
                            while (rd2.Read())
                            {
                                diameter1 = float.Parse(rd2["Diameter"].ToString());
                                cylinderh1 = float.Parse(rd2["CylinderH"].ToString());
                                pyramidh1 = float.Parse(rd2["PyramidH"].ToString());

                            }
                            MessageBox.Show("Diameter" + diameter1 + "CylinderH" + cylinderh1 + "PyramidH" + pyramidh1);

                            rd2.Close();
                            ms2.Close();
                            float Vol_ware = (float)((diameter1 / 2) * (diameter1 / 2) * (3.14) * (pyramidh1 / 3 + cylinderh1));
                            datalb51.Text = Vol_ware.ToString() + "m³";
                            //MessageBox.Show("第一个图的ID" + id);


                            sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                            MySqlConn ms3 = new MySqlConn();
                            MySqlDataReader rd3 = ms3.getDataFromTable(sql1);

                            while (rd3.Read())
                            {
                                datalb52.Text = rd3["Volume"].ToString() + "m³";
                                datalb53.Text = rd3["Weight"].ToString() + "吨";
                                datalb54.Text = rd3["Temp"].ToString() + "℃";
                                datalb55.Text = rd3["Hum"].ToString() + "%";
                                //float vol_feed = float.Parse(rd1["Volume"].ToString());
                                //if (vol_feed < Vol_ware)
                                //{
                                //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                //}
                                //else
                                //{
                                //    progressBar1.Value = 0;
                                //    MessageBox.Show("数据有误", "提示");
                                //}
                                break;
                            }
                            rd3.Close();
                            ms3.Close();

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                        }

                        if (num >= 6)
                        {
                            float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                            ID6.Text = checkedListBox1.CheckedItems[5].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                            //string id = selectID(checkedListBox2.Items[k].ToString());
                            string str2 = checkedListBox1.CheckedItems[5].ToString();
                            string id2 = selectID(str2);
                            string sql2 = "select * from bininfo where BinID = " + id2;
                            //MessageBox.Show("*****************************" + id2);
                            try
                            {
                                MySqlConn ms4 = new MySqlConn();
                                MySqlDataReader rd4 = ms4.getDataFromTable(sql2);
                                while (rd4.Read())
                                {
                                    diameter2 = float.Parse(rd4["Diameter"].ToString());
                                    cylinderh2 = float.Parse(rd4["CylinderH"].ToString());
                                    pyramidh2 = float.Parse(rd4["PyramidH"].ToString());

                                }
                                MessageBox.Show("Diameter" + diameter2 + "CylinderH" + cylinderh2 + "PyramidH" + pyramidh2);

                                rd4.Close();
                                ms4.Close();
                                float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                                datalb61.Text = Vol_ware.ToString() + "m³";
                                //MessageBox.Show("第一个图的ID" + id);


                                sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms5 = new MySqlConn();
                                MySqlDataReader rd5 = ms5.getDataFromTable(sql2);

                                while (rd5.Read())
                                {
                                    datalb62.Text = rd5["Volume"].ToString() + "m³";
                                    datalb63.Text = rd5["Weight"].ToString() + "吨";
                                    datalb64.Text = rd5["Temp"].ToString() + "℃";
                                    datalb65.Text = rd5["Hum"].ToString() + "%";
                                    //float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd5.Close();
                                ms5.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }

                        }
                    }

                }


            }

        }


        private void ShowFive()
        {
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            BindingNavigator bt = new BindingNavigator();
 

            ToolStripButton toolStripButton16 = new ToolStripButton();
            ToolStripButton toolStripButton17 = new ToolStripButton();
            ToolStripButton toolStripButton18 = new ToolStripButton();
            ToolStripButton toolStripButton19 = new ToolStripButton();



            ToolStripTextBox toolStripTextBox18 = new ToolStripTextBox();

            ToolStripLabel lblPageCount1 = new ToolStripLabel();

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            bt.Items.AddRange(new System.Windows.Forms.ToolStripItem[]{
                toolStripButton18,
                toolStripButton17,
                toolStripTextBox18,
                lblPageCount1,
                toolStripButton16,
                toolStripButton19
            });

            toolStripTextBox18.Size = new System.Drawing.Size(50, 23);

            toolStripButton16.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton16.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton16.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton16.Name = "toolStripButton16";
            toolStripButton16.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton16.Text = "下一页";

            toolStripButton17.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton17.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton17.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton17.Name = "toolStripButton16";
            toolStripButton17.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton17.Text = "上一页";


            toolStripButton18.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton18.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton18.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton18.Name = "toolStripButton16";
            toolStripButton18.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton18.Text = "首页";


            toolStripButton19.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton19.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton19.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton19.Name = "toolStripButton16";
            toolStripButton19.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton19.Text = "最后一页";

            lblPageCount1.Name = "lblPageCount";
            lblPageCount1.Size = new System.Drawing.Size((int)(50), (int)(23));
            lblPageCount1.Text = "/5";


            //bt.Location = new System.Drawing.Point((int)(50), (int)(0));
            bt.Location = new System.Drawing.Point(0, 450);
            bt.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(bt_ItemClicked);
            //object o1 = bt.Location;
            //object o2 = bt.Size;
            //object o3 = groupBox_pic.Size;

            //MessageBox.Show("坐标" + o1 + "大小" + o2);
            //MessageBox.Show("坐标+++++++++++++" + o3);
            //bt.Location = new System.Drawing.Point(100,400);
            //bt.Size = new System.Drawing.Size(500,500);
            groupBox_pic.Controls.Add(bt);

            lblPageCount1.Text = chartCount.ToString();
            toolStripTextBox18.Text = Convert.ToString(chartCurrent);




            PictureBox bx = new PictureBox();
            bx.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx.Location = new System.Drawing.Point((int)(20), (int)(75));
            bx.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx);


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);
            }

            PictureBox bx1 = new PictureBox();
            bx1.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx1.Location = new System.Drawing.Point((int)(270), (int)(75));
            bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx1);


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((390)), (int)((55 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }



            PictureBox bx2 = new PictureBox();
            bx2.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx2.Location = new System.Drawing.Point((int)(530), (int)(75));
            bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx2);

            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((650)), (int)((55 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }



            PictureBox bx3 = new PictureBox();
            bx3.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx3.Location = new System.Drawing.Point((int)(20), (int)(275));
            bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx3);


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((140)), (int)((245 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }

            PictureBox bx4 = new PictureBox();
            bx4.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx4.Location = new System.Drawing.Point((int)(270), (int)(275));
            bx4.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx4.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx4);


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((390)), (int)((245 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }

            PictureBox bx5 = new PictureBox();
            bx5.Image = global::Warehouse.Properties.Resources._001;
            //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
            bx5.Location = new System.Drawing.Point((int)(530), (int)(275));
            bx5.Size = new System.Drawing.Size((int)(100), (int)(150));
            bx5.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            groupBox_pic.Controls.Add(bx5);


            for (int j = 0; j < 6; j++)
            {
                Label label = new Label();
                label.Text = zifu[j];
                label.Size = new System.Drawing.Size((int)(60), (int)(20));
                label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label.Location = new System.Drawing.Point((int)((650)), (int)((245 + (j + 1) * 30)));
                label.BringToFront();
                groupBox_pic.Controls.Add(label);

            }

            initLabel();


        }


        /// <summary>
        /// 不在线多于六个料仓的样式
        /// </summary>
        private void initLabel()
        {

                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

                datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb21.Location = new System.Drawing.Point((int)((450)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb21);
                
                datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb22.Location = new System.Drawing.Point((int)((450)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb22);


                datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb23.Location = new System.Drawing.Point((int)((450)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb23);


                datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb24.Location = new System.Drawing.Point((int)((450)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb24);


                datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb25.Location = new System.Drawing.Point((int)((450)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb25);

                datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb26.Location = new System.Drawing.Point((int)((450)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb26);

                datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb31.Location = new System.Drawing.Point((int)((710)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb31);
                
                datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
                datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb32.Location = new System.Drawing.Point((int)((710)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb32);


                datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb33.Location = new System.Drawing.Point((int)((710)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb33);


                datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb34.Location = new System.Drawing.Point((int)((710)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb34);


                datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb35.Location = new System.Drawing.Point((int)((710)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb35);

                datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb36.Location = new System.Drawing.Point((int)((710)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb36);

            ///////////////////////////////////////////////////////////////////////////////////////////////////

                datalb41.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb41.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb41.Location = new System.Drawing.Point((int)((200)), (int)((245 + 30)));
                groupBox_pic.Controls.Add(datalb41);

                datalb42.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb42.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb42.Location = new System.Drawing.Point((int)((200)), (int)((245 + 60)));
                groupBox_pic.Controls.Add(datalb42);


                datalb43.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb43.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb43.Location = new System.Drawing.Point((int)((200)), (int)((245 + 90)));
                groupBox_pic.Controls.Add(datalb43);


                datalb44.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb44.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb44.Location = new System.Drawing.Point((int)((200)), (int)((245 + 120)));
                groupBox_pic.Controls.Add(datalb44);


                datalb45.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb45.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb45.Location = new System.Drawing.Point((int)((200)), (int)((245 + 150)));
                groupBox_pic.Controls.Add(datalb45);

                datalb46.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb46.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb46.Location = new System.Drawing.Point((int)((200)), (int)((245 + 180)));
                groupBox_pic.Controls.Add(datalb46);

                datalb51.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb51.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb51.Location = new System.Drawing.Point((int)((450)), (int)((245 + 30)));
                groupBox_pic.Controls.Add(datalb51);
                
                datalb52.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb52.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb52.Location = new System.Drawing.Point((int)((450)), (int)((245 + 60)));
                groupBox_pic.Controls.Add(datalb52);


                datalb53.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb53.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb53.Location = new System.Drawing.Point((int)((450)), (int)((245 + 90)));
                groupBox_pic.Controls.Add(datalb53);


                datalb54.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb54.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb54.Location = new System.Drawing.Point((int)((450)), (int)((245 + 120)));
                groupBox_pic.Controls.Add(datalb54);


                datalb55.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb55.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb55.Location = new System.Drawing.Point((int)((450)), (int)((245 + 150)));
                groupBox_pic.Controls.Add(datalb55);

                datalb56.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb56.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb56.Location = new System.Drawing.Point((int)((450)), (int)((245 + 180)));
                groupBox_pic.Controls.Add(datalb56);

                datalb61.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb61.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb61.Location = new System.Drawing.Point((int)((710)), (int)((245 + 30)));
                groupBox_pic.Controls.Add(datalb61);
                
                datalb62.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb62.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb62.Location = new System.Drawing.Point((int)((710)), (int)((245 + 60)));
                groupBox_pic.Controls.Add(datalb62);


                datalb63.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb63.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb63.Location = new System.Drawing.Point((int)((710)), (int)((245 + 90)));
                groupBox_pic.Controls.Add(datalb63);


                datalb64.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb64.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb64.Location = new System.Drawing.Point((int)((710)), (int)((245 + 120)));
                groupBox_pic.Controls.Add(datalb64);


                datalb65.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb65.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb65.Location = new System.Drawing.Point((int)((710)), (int)((245 + 150)));
                groupBox_pic.Controls.Add(datalb65);

                datalb66.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb66.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb66.Location = new System.Drawing.Point((int)((710)), (int)((245 + 180)));
                groupBox_pic.Controls.Add(datalb66);

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);

                ID2.Location = new System.Drawing.Point((int)(280), (int)(55));
                ID2.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID2.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID2);

                ID3.Location = new System.Drawing.Point((int)(530), (int)(55));
                ID3.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID3.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID3);

                ID4.Location = new System.Drawing.Point((int)(30), (int)(255));
                ID4.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID4.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID4);

                ID5.Location = new System.Drawing.Point((int)(280), (int)(255));
                ID5.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID5.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID5);

                ID6.Location = new System.Drawing.Point((int)(530), (int)(255));
                ID6.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID6.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID6);

            LoadData1();

        }

        /// <summary>
        /// 不在线多于六个料仓的数据加载
        /// </summary>
        private void LoadData1()
        {
    
            int k= (chartCurrent-1)*6;
            //MessageBox.Show("跳转时候K值" + k);
            int s=checkedListBox2.CheckedItems.Count;//选中的数量
            if(s%6 == 0)
            {

                for (int i = 0; i < 6; i++)
                {
                    if (SqlConnect == 1)
                    {

                        //MessageBox.Show(checkedListBox2.CheckedItems.Count + "选中的数量");
                        //MessageBox.Show(checkedListBox2.GetItemText(checkedListBox2.Items[i]) + "++++++++++++" + checkedListBox2.GetItemChecked(i) + "**********" + i);
                        if (k % 6 == 0)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                            //string id = selectID(checkedListBox2.Items[k].ToString());

                            string str = checkedListBox2.CheckedItems[k].ToString();
                            string id = selectID(str);
                            // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());

                                }
                                //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb11.Text = Vol_ware.ToString() + "m³";
                                //MessageBox.Show("第一个图的ID" + id);


                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb12.Text = rd1["Volume"].ToString() + "m³";
                                    datalb13.Text = rd1["Weight"].ToString() + "吨";
                                    datalb14.Text = rd1["Temp"].ToString() + "℃";
                                    datalb15.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;

                        }
                        else if (k % 6 == 1)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show("K+++++++++++++值" + k);
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                            string str = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show("第二个图str" + str);
                            string id = selectID(str);
                            //string id = selectID("8号筒仓");
                            //MessageBox.Show("第二个图的ID" + id);

                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());
                                }
                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb21.Text = Vol_ware.ToString() + "m³";




                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb22.Text = rd1["Volume"].ToString() + "m³";
                                    datalb23.Text = rd1["Weight"].ToString() + "吨";
                                    datalb24.Text = rd1["Temp"].ToString() + "℃";
                                    datalb25.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;

                        }
                        else if (k % 6 == 2)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID3.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                            string str = checkedListBox2.CheckedItems[k].ToString();
                            string id = selectID(str);
                            //string id = selectID("1号筒仓");

                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());
                                }
                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb31.Text = Vol_ware.ToString() + "m³";


                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb32.Text = rd1["Volume"].ToString() + "m³";
                                    datalb33.Text = rd1["Weight"].ToString() + "吨";
                                    datalb34.Text = rd1["Temp"].ToString() + "℃";
                                    datalb35.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;

                        }
                        else if (k % 6 == 3)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID4.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第四个筒仓名字");
                            string str = checkedListBox2.CheckedItems[k].ToString();
                            string id = selectID(checkedListBox2.CheckedItems[k].ToString());
                            //string id = selectID("8号筒仓");


                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());
                                }
                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb41.Text = Vol_ware.ToString() + "m³";



                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb42.Text = rd1["Volume"].ToString() + "m³";
                                    datalb43.Text = rd1["Weight"].ToString() + "吨";
                                    datalb44.Text = rd1["Temp"].ToString() + "℃";
                                    datalb45.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;

                        }
                        else if (k % 6 == 4)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID5.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第五个筒仓名字");
                            string str = checkedListBox2.CheckedItems[k].ToString();
                            string id = selectID(str);
                            //string id = selectID("8号筒仓");

                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());
                                }
                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb51.Text = Vol_ware.ToString() + "m³";

                                //MessageBox.Show("ID" + id);


                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb52.Text = rd1["Volume"].ToString() + "m³";
                                    datalb53.Text = rd1["Weight"].ToString() + "吨";
                                    datalb54.Text = rd1["Temp"].ToString() + "℃";
                                    datalb55.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;

                        }
                        else if (k % 6 == 5)
                        {
                            float diameter = 0, cylinderh = 0, pyramidh = 0;
                            ID6.Text = checkedListBox2.CheckedItems[k].ToString();
                            //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第六个筒仓名字");
                            string str = checkedListBox2.CheckedItems[k].ToString();
                            string id = selectID(str);
                            //string id = selectID("8号筒仓");


                            string sql = "select * from bininfo where BinID = " + id;
                            try
                            {
                                MySqlConn ms = new MySqlConn();
                                MySqlDataReader rd = ms.getDataFromTable(sql);
                                while (rd.Read())
                                {
                                    diameter = float.Parse(rd["Diameter"].ToString());
                                    cylinderh = float.Parse(rd["CylinderH"].ToString());
                                    pyramidh = float.Parse(rd["PyramidH"].ToString());
                                }
                                rd.Close();
                                ms.Close();
                                float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                datalb61.Text = Vol_ware.ToString() + "m³";

                                //MessageBox.Show("ID" + id);


                                sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                MySqlConn ms1 = new MySqlConn();
                                MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                while (rd1.Read())
                                {
                                    datalb62.Text = rd1["Volume"].ToString() + "m³";
                                    datalb63.Text = rd1["Weight"].ToString() + "吨";
                                    datalb64.Text = rd1["Temp"].ToString() + "℃";
                                    datalb65.Text = rd1["Hum"].ToString() + "%";
                                    float vol_feed = float.Parse(rd1["Volume"].ToString());
                                    //if (vol_feed < Vol_ware)
                                    //{
                                    //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                    //}
                                    //else
                                    //{
                                    //    progressBar1.Value = 0;
                                    //    MessageBox.Show("数据有误", "提示");
                                    //}
                                    break;
                                }
                                rd1.Close();
                                ms1.Close();

                            }
                            catch (SqlException se)
                            {
                                Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                thread_file.Start(se.ToString());
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            }
                            k++;
                        }
                        else
                        {
                            break;
                        }


                    }

                }

            }
            else
            {
                if(chartCurrent == chartCount)
                {
                    //MessageBox.Show("数量"+s);
                    leastData();
                }
                else
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (SqlConnect == 1)
                        {

                            //MessageBox.Show(checkedListBox2.CheckedItems.Count + "选中的数量");
                            //MessageBox.Show(checkedListBox2.GetItemText(checkedListBox2.Items[i]) + "++++++++++++" + checkedListBox2.GetItemChecked(i) + "**********" + i);
                            if (k % 6 == 0)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                                //string id = selectID(checkedListBox2.Items[k].ToString());

                                string str = checkedListBox2.CheckedItems[k].ToString();
                                string id = selectID(str);
                                // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());

                                    }
                                    //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb11.Text = Vol_ware.ToString() + "m³";
                                    //MessageBox.Show("第一个图的ID" + id);


                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb12.Text = rd1["Volume"].ToString() + "m³";
                                        datalb13.Text = rd1["Weight"].ToString() + "吨";
                                        datalb14.Text = rd1["Temp"].ToString() + "℃";
                                        datalb15.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;

                            }
                            else if (k % 6 == 1)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show("K+++++++++++++值" + k);
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                                string str = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show("第二个图str" + str);
                                string id = selectID(str);
                                //string id = selectID("8号筒仓");
                                //MessageBox.Show("第二个图的ID" + id);

                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());
                                    }
                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb21.Text = Vol_ware.ToString() + "m³";




                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb22.Text = rd1["Volume"].ToString() + "m³";
                                        datalb23.Text = rd1["Weight"].ToString() + "吨";
                                        datalb24.Text = rd1["Temp"].ToString() + "℃";
                                        datalb25.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;

                            }
                            else if (k % 6 == 2)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID3.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                                string str = checkedListBox2.CheckedItems[k].ToString();
                                string id = selectID(str);
                                //string id = selectID("1号筒仓");

                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());
                                    }
                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb31.Text = Vol_ware.ToString() + "m³";


                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb32.Text = rd1["Volume"].ToString() + "m³";
                                        datalb33.Text = rd1["Weight"].ToString() + "吨";
                                        datalb34.Text = rd1["Temp"].ToString() + "℃";
                                        datalb35.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;

                            }
                            else if (k % 6 == 3)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID4.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第四个筒仓名字");
                                string str = checkedListBox2.CheckedItems[k].ToString();
                                string id = selectID(checkedListBox2.CheckedItems[k].ToString());
                                //string id = selectID("8号筒仓");


                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());
                                    }
                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb41.Text = Vol_ware.ToString() + "m³";



                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb42.Text = rd1["Volume"].ToString() + "m³";
                                        datalb43.Text = rd1["Weight"].ToString() + "吨";
                                        datalb44.Text = rd1["Temp"].ToString() + "℃";
                                        datalb45.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;

                            }
                            else if (k % 6 == 4)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID5.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第五个筒仓名字");
                                string str = checkedListBox2.CheckedItems[k].ToString();
                                string id = selectID(str);
                                //string id = selectID("8号筒仓");

                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());
                                    }
                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb51.Text = Vol_ware.ToString() + "m³";

                                    //MessageBox.Show("ID" + id);


                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb52.Text = rd1["Volume"].ToString() + "m³";
                                        datalb53.Text = rd1["Weight"].ToString() + "吨";
                                        datalb54.Text = rd1["Temp"].ToString() + "℃";
                                        datalb55.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;

                            }
                            else if (k % 6 == 5)
                            {
                                float diameter = 0, cylinderh = 0, pyramidh = 0;
                                ID6.Text = checkedListBox2.CheckedItems[k].ToString();
                                //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第六个筒仓名字");
                                string str = checkedListBox2.CheckedItems[k].ToString();
                                string id = selectID(str);
                                //string id = selectID("8号筒仓");


                                string sql = "select * from bininfo where BinID = " + id;
                                try
                                {
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        diameter = float.Parse(rd["Diameter"].ToString());
                                        cylinderh = float.Parse(rd["CylinderH"].ToString());
                                        pyramidh = float.Parse(rd["PyramidH"].ToString());
                                    }
                                    rd.Close();
                                    ms.Close();
                                    float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                    datalb61.Text = Vol_ware.ToString() + "m³";

                                    //MessageBox.Show("ID" + id);


                                    sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                    MySqlConn ms1 = new MySqlConn();
                                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                                    while (rd1.Read())
                                    {
                                        datalb62.Text = rd1["Volume"].ToString() + "m³";
                                        datalb63.Text = rd1["Weight"].ToString() + "吨";
                                        datalb64.Text = rd1["Temp"].ToString() + "℃";
                                        datalb65.Text = rd1["Hum"].ToString() + "%";
                                        float vol_feed = float.Parse(rd1["Volume"].ToString());
                                        //if (vol_feed < Vol_ware)
                                        //{
                                        //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                        //}
                                        //else
                                        //{
                                        //    progressBar1.Value = 0;
                                        //    MessageBox.Show("数据有误", "提示");
                                        //}
                                        break;
                                    }
                                    rd1.Close();
                                    ms1.Close();

                                }
                                catch (SqlException se)
                                {
                                    Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                    thread_file.Start(se.ToString());
                                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                }
                                k++;
                            }
                            else
                            {
                                break;
                            }


                        }

                    }
                }


            }


        }




        private void leastData()
        {
            groupBox_pic.Controls.Clear();
            ShowFive1();


            if (checkedListBox2.CheckedItems.Count % 6 == 1)
            {
                int k = checkedListBox2.CheckedItems.Count - 1;
                //MessageBox.Show("K" + k);
                if(SqlConnect == 1)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                    MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());

                    string str = checkedListBox2.CheckedItems[k].ToString();
                    string id = selectID(str);
                    // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb11.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb12.Text = rd1["Volume"].ToString() + "m³";
                            datalb13.Text = rd1["Weight"].ToString() + "吨";
                            datalb14.Text = rd1["Temp"].ToString() + "℃";
                            datalb15.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 2)
            {
                int k = checkedListBox2.CheckedItems.Count - 2;
                if (SqlConnect == 1)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                    MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());

                    string str = checkedListBox2.CheckedItems[k].ToString();
                    string id = selectID(str);
                    // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb11.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb12.Text = rd1["Volume"].ToString() + "m³";
                            datalb13.Text = rd1["Weight"].ToString() + "吨";
                            datalb14.Text = rd1["Temp"].ToString() + "℃";
                            datalb15.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("K+++++++++++++值" + k);
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                    string str1 = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("第二个图str" + str);
                    string id1 = selectID(str1);
                    //string id = selectID("8号筒仓");
                    //MessageBox.Show("第二个图的ID" + id);

                    string sql1 = "select * from bininfo where BinID = " + id1;
                    try
                    {
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql1);
                        while (rd1.Read())
                        {
                            diameter = float.Parse(rd1["Diameter"].ToString());
                            cylinderh = float.Parse(rd1["CylinderH"].ToString());
                            pyramidh = float.Parse(rd1["PyramidH"].ToString());
                        }
                        rd1.Close();
                        ms1.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb21.Text = Vol_ware.ToString() + "m³";




                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);

                        while (rd2.Read())
                        {
                            datalb22.Text = rd2["Volume"].ToString() + "m³";
                            datalb23.Text = rd2["Weight"].ToString() + "吨";
                            datalb24.Text = rd2["Temp"].ToString() + "℃";
                            datalb25.Text = rd2["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd2["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd2.Close();
                        ms2.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 3)
            {
                int k = checkedListBox2.CheckedItems.Count - 3;
                if (SqlConnect == 1)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                    MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());

                    string str = checkedListBox2.CheckedItems[k].ToString();
                    string id = selectID(str);
                    // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb11.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb12.Text = rd1["Volume"].ToString() + "m³";
                            datalb13.Text = rd1["Weight"].ToString() + "吨";
                            datalb14.Text = rd1["Temp"].ToString() + "℃";
                            datalb15.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("K+++++++++++++值" + k);
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                    string str1 = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("第二个图str" + str);
                    string id1 = selectID(str1);
                    //string id = selectID("8号筒仓");
                    //MessageBox.Show("第二个图的ID" + id);

                    string sql1 = "select * from bininfo where BinID = " + id1;
                    try
                    {
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql1);
                        while (rd1.Read())
                        {
                            diameter1 = float.Parse(rd1["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd1["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd1["PyramidH"].ToString());
                        }
                        rd1.Close();
                        ms1.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb21.Text = Vol_ware.ToString() + "m³";




                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);

                        while (rd2.Read())
                        {
                            datalb22.Text = rd2["Volume"].ToString() + "m³";
                            datalb23.Text = rd2["Weight"].ToString() + "吨";
                            datalb24.Text = rd2["Temp"].ToString() + "℃";
                            datalb25.Text = rd2["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd2["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd2.Close();
                        ms2.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                    ID3.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                    string str2 = checkedListBox2.CheckedItems[k].ToString();
                    string id2 = selectID(str2);
                    //string id = selectID("1号筒仓");

                    string sql2 = "select * from bininfo where BinID = " + id2;
                    try
                    {
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql2);
                        while (rd3.Read())
                        {
                            diameter2 = float.Parse(rd3["Diameter"].ToString());
                            cylinderh2 = float.Parse(rd3["CylinderH"].ToString());
                            pyramidh2 = float.Parse(rd3["PyramidH"].ToString());
                        }
                        rd3.Close();
                        ms3.Close();
                        float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                        datalb31.Text = Vol_ware.ToString() + "m³";


                        sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms4 = new MySqlConn();
                        MySqlDataReader rd4 = ms4.getDataFromTable(sql2);

                        while (rd4.Read())
                        {
                            datalb32.Text = rd4["Volume"].ToString() + "m³";
                            datalb33.Text = rd4["Weight"].ToString() + "吨";
                            datalb34.Text = rd4["Temp"].ToString() + "℃";
                            datalb35.Text = rd4["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd4["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd4.Close();
                        ms4.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                }

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 4)
            {
                int k = checkedListBox2.CheckedItems.Count - 4;
                if (SqlConnect == 1)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                    MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());

                    string str = checkedListBox2.CheckedItems[k].ToString();
                    string id = selectID(str);
                    // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb11.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb12.Text = rd1["Volume"].ToString() + "m³";
                            datalb13.Text = rd1["Weight"].ToString() + "吨";
                            datalb14.Text = rd1["Temp"].ToString() + "℃";
                            datalb15.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("K+++++++++++++值" + k);
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                    string str1 = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("第二个图str" + str);
                    string id1 = selectID(str1);
                    //string id = selectID("8号筒仓");
                    //MessageBox.Show("第二个图的ID" + id);

                    string sql1 = "select * from bininfo where BinID = " + id1;
                    try
                    {
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql1);
                        while (rd1.Read())
                        {
                            diameter1 = float.Parse(rd1["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd1["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd1["PyramidH"].ToString());
                        }
                        rd1.Close();
                        ms1.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb21.Text = Vol_ware.ToString() + "m³";




                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);

                        while (rd2.Read())
                        {
                            datalb22.Text = rd2["Volume"].ToString() + "m³";
                            datalb23.Text = rd2["Weight"].ToString() + "吨";
                            datalb24.Text = rd2["Temp"].ToString() + "℃";
                            datalb25.Text = rd2["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd2["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd2.Close();
                        ms2.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                    ID3.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                    string str2 = checkedListBox2.CheckedItems[k].ToString();
                    string id2 = selectID(str2);
                    //string id = selectID("1号筒仓");

                    string sql2 = "select * from bininfo where BinID = " + id2;
                    try
                    {
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql2);
                        while (rd3.Read())
                        {
                            diameter2 = float.Parse(rd3["Diameter"].ToString());
                            cylinderh2 = float.Parse(rd3["CylinderH"].ToString());
                            pyramidh2 = float.Parse(rd3["PyramidH"].ToString());
                        }
                        rd3.Close();
                        ms3.Close();
                        float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                        datalb31.Text = Vol_ware.ToString() + "m³";


                        sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms4 = new MySqlConn();
                        MySqlDataReader rd4 = ms4.getDataFromTable(sql2);

                        while (rd4.Read())
                        {
                            datalb32.Text = rd4["Volume"].ToString() + "m³";
                            datalb33.Text = rd4["Weight"].ToString() + "吨";
                            datalb34.Text = rd4["Temp"].ToString() + "℃";
                            datalb35.Text = rd4["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd4["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd4.Close();
                        ms4.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter3 = 0, cylinderh3 = 0, pyramidh3 = 0;
                    ID4.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第四个筒仓名字");
                    string str3 = checkedListBox2.CheckedItems[k].ToString();
                    string id3 = selectID(checkedListBox2.CheckedItems[k].ToString());
                    //string id = selectID("8号筒仓");


                    string sql3 = "select * from bininfo where BinID = " + id3;
                    try
                    {
                        MySqlConn ms5 = new MySqlConn();
                        MySqlDataReader rd5 = ms5.getDataFromTable(sql3);
                        while (rd5.Read())
                        {
                            diameter3 = float.Parse(rd5["Diameter"].ToString());
                            cylinderh3 = float.Parse(rd5["CylinderH"].ToString());
                            pyramidh3 = float.Parse(rd5["PyramidH"].ToString());
                        }
                        rd5.Close();
                        ms5.Close();
                        float Vol_ware = (float)((diameter3 / 2) * (diameter3 / 2) * (3.14) * (pyramidh3 / 3 + cylinderh3));
                        datalb41.Text = Vol_ware.ToString() + "m³";



                        sql3 = "select * from bindata where BinID = " + id3 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms6 = new MySqlConn();
                        MySqlDataReader rd6 = ms6.getDataFromTable(sql3);

                        while (rd6.Read())
                        {
                            datalb42.Text = rd6["Volume"].ToString() + "m³";
                            datalb43.Text = rd6["Weight"].ToString() + "吨";
                            datalb44.Text = rd6["Temp"].ToString() + "℃";
                            datalb45.Text = rd6["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd6["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd6.Close();
                        ms6.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    
                }

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 5)
            {
                int k = checkedListBox2.CheckedItems.Count - 5;
                if (SqlConnect == 1)
                {
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    ID1.Text = checkedListBox2.CheckedItems[k].ToString();
                    MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第一个筒仓名字");
                    //string id = selectID(checkedListBox2.Items[k].ToString());

                    string str = checkedListBox2.CheckedItems[k].ToString();
                    string id = selectID(str);
                    // MessageBox.Show("ID" + id+"K"+k+"************"+ checkedListBox2.CheckedItems[k].ToString());


                    string sql = "select * from bininfo where BinID = " + id;
                    try
                    {
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            diameter = float.Parse(rd["Diameter"].ToString());
                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                            pyramidh = float.Parse(rd["PyramidH"].ToString());

                        }
                        //MessageBox.Show("Diameter" + diameter + "CylinderH"+ cylinderh+ "PyramidH"+ pyramidh);

                        rd.Close();
                        ms.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb11.Text = Vol_ware.ToString() + "m³";
                        //MessageBox.Show("第一个图的ID" + id);


                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);

                        while (rd1.Read())
                        {
                            datalb12.Text = rd1["Volume"].ToString() + "m³";
                            datalb13.Text = rd1["Weight"].ToString() + "吨";
                            datalb14.Text = rd1["Temp"].ToString() + "℃";
                            datalb15.Text = rd1["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        //MessageBox.Show("datalb12.Text" + datalb12.Text + "datalb13.Text" + datalb13.Text + "datalb14.Text" + datalb14.Text + "datalb15.Text" + datalb15.Text);
                        rd1.Close();
                        ms1.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;


                    float diameter1 = 0, cylinderh1 = 0, pyramidh1 = 0;
                    ID2.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("K+++++++++++++值" + k);
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第二个筒仓名字");
                    string str1 = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show("第二个图str" + str);
                    string id1 = selectID(str1);
                    //string id = selectID("8号筒仓");
                    //MessageBox.Show("第二个图的ID" + id);

                    string sql1 = "select * from bininfo where BinID = " + id1;
                    try
                    {
                        MySqlConn ms1 = new MySqlConn();
                        MySqlDataReader rd1 = ms1.getDataFromTable(sql1);
                        while (rd1.Read())
                        {
                            diameter1 = float.Parse(rd1["Diameter"].ToString());
                            cylinderh1 = float.Parse(rd1["CylinderH"].ToString());
                            pyramidh1 = float.Parse(rd1["PyramidH"].ToString());
                        }
                        rd1.Close();
                        ms1.Close();
                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                        datalb21.Text = Vol_ware.ToString() + "m³";




                        sql1 = "select * from bindata where BinID = " + id1 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms2 = new MySqlConn();
                        MySqlDataReader rd2 = ms2.getDataFromTable(sql1);

                        while (rd2.Read())
                        {
                            datalb22.Text = rd2["Volume"].ToString() + "m³";
                            datalb23.Text = rd2["Weight"].ToString() + "吨";
                            datalb24.Text = rd2["Temp"].ToString() + "℃";
                            datalb25.Text = rd2["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd2["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd2.Close();
                        ms2.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter2 = 0, cylinderh2 = 0, pyramidh2 = 0;
                    ID3.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第三个筒仓名字");
                    string str2 = checkedListBox2.CheckedItems[k].ToString();
                    string id2 = selectID(str2);
                    //string id = selectID("1号筒仓");

                    string sql2 = "select * from bininfo where BinID = " + id2;
                    try
                    {
                        MySqlConn ms3 = new MySqlConn();
                        MySqlDataReader rd3 = ms3.getDataFromTable(sql2);
                        while (rd3.Read())
                        {
                            diameter2 = float.Parse(rd3["Diameter"].ToString());
                            cylinderh2 = float.Parse(rd3["CylinderH"].ToString());
                            pyramidh2 = float.Parse(rd3["PyramidH"].ToString());
                        }
                        rd3.Close();
                        ms3.Close();
                        float Vol_ware = (float)((diameter2 / 2) * (diameter2 / 2) * (3.14) * (pyramidh2 / 3 + cylinderh2));
                        datalb31.Text = Vol_ware.ToString() + "m³";


                        sql2 = "select * from bindata where BinID = " + id2 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms4 = new MySqlConn();
                        MySqlDataReader rd4 = ms4.getDataFromTable(sql2);

                        while (rd4.Read())
                        {
                            datalb32.Text = rd4["Volume"].ToString() + "m³";
                            datalb33.Text = rd4["Weight"].ToString() + "吨";
                            datalb34.Text = rd4["Temp"].ToString() + "℃";
                            datalb35.Text = rd4["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd4["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd4.Close();
                        ms4.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter3 = 0, cylinderh3 = 0, pyramidh3 = 0;
                    ID4.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第四个筒仓名字");
                    string str3 = checkedListBox2.CheckedItems[k].ToString();
                    string id3 = selectID(checkedListBox2.CheckedItems[k].ToString());
                    //string id = selectID("8号筒仓");


                    string sql3 = "select * from bininfo where BinID = " + id3;
                    try
                    {
                        MySqlConn ms5 = new MySqlConn();
                        MySqlDataReader rd5 = ms5.getDataFromTable(sql3);
                        while (rd5.Read())
                        {
                            diameter3 = float.Parse(rd5["Diameter"].ToString());
                            cylinderh3 = float.Parse(rd5["CylinderH"].ToString());
                            pyramidh3 = float.Parse(rd5["PyramidH"].ToString());
                        }
                        rd5.Close();
                        ms5.Close();
                        float Vol_ware = (float)((diameter3 / 2) * (diameter3 / 2) * (3.14) * (pyramidh3 / 3 + cylinderh3));
                        datalb41.Text = Vol_ware.ToString() + "m³";



                        sql3 = "select * from bindata where BinID = " + id3 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms6 = new MySqlConn();
                        MySqlDataReader rd6 = ms6.getDataFromTable(sql3);

                        while (rd6.Read())
                        {
                            datalb42.Text = rd6["Volume"].ToString() + "m³";
                            datalb43.Text = rd6["Weight"].ToString() + "吨";
                            datalb44.Text = rd6["Temp"].ToString() + "℃";
                            datalb45.Text = rd6["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd6["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd6.Close();
                        ms6.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }
                    k++;

                    float diameter4 = 0, cylinderh4 = 0, pyramidh4 = 0;
                    ID5.Text = checkedListBox2.CheckedItems[k].ToString();
                    //MessageBox.Show(checkedListBox2.CheckedItems[k].ToString() + "第五个筒仓名字");
                    string str4 = checkedListBox2.CheckedItems[k].ToString();
                    string id4 = selectID(str4);
                    //string id = selectID("8号筒仓");

                    string sql4 = "select * from bininfo where BinID = " + id4;
                    try
                    {
                        MySqlConn ms7 = new MySqlConn();
                        MySqlDataReader rd7 = ms7.getDataFromTable(sql4);
                        while (rd7.Read())
                        {
                            diameter4 = float.Parse(rd7["Diameter"].ToString());
                            cylinderh4 = float.Parse(rd7["CylinderH"].ToString());
                            pyramidh4 = float.Parse(rd7["PyramidH"].ToString());
                        }
                        rd7.Close();
                        ms7.Close();
                        float Vol_ware = (float)((diameter4 / 2) * (diameter4 / 2) * (3.14) * (pyramidh4 / 3 + cylinderh4));
                        datalb51.Text = Vol_ware.ToString() + "m³";

                        //MessageBox.Show("ID" + id);


                        sql4 = "select * from bindata where BinID = " + id4 + " AND Volume is not NULL order by DateTime desc";
                        MySqlConn ms8 = new MySqlConn();
                        MySqlDataReader rd8 = ms8.getDataFromTable(sql);

                        while (rd8.Read())
                        {
                            datalb52.Text = rd8["Volume"].ToString() + "m³";
                            datalb53.Text = rd8["Weight"].ToString() + "吨";
                            datalb54.Text = rd8["Temp"].ToString() + "℃";
                            datalb55.Text = rd8["Hum"].ToString() + "%";
                            float vol_feed = float.Parse(rd8["Volume"].ToString());
                            //if (vol_feed < Vol_ware)
                            //{
                            //    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                            //}
                            //else
                            //{
                            //    progressBar1.Value = 0;
                            //    MessageBox.Show("数据有误", "提示");
                            //}
                            break;
                        }
                        rd8.Close();
                        ms8.Close();

                    }
                    catch (SqlException se)
                    {
                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                        thread_file.Start(se.ToString());
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    }

                }

            }


        }



        private void ShowFive1()
        {
            String[] zifu = { "料仓体积", "物料体积", "物料重量", "料仓内温度", "设备温度", "湿度" };
            BindingNavigator bt = new BindingNavigator();


            ToolStripButton toolStripButton16 = new ToolStripButton();
            ToolStripButton toolStripButton17 = new ToolStripButton();
            ToolStripButton toolStripButton18 = new ToolStripButton();
            ToolStripButton toolStripButton19 = new ToolStripButton();



            ToolStripTextBox toolStripTextBox18 = new ToolStripTextBox();

            ToolStripLabel lblPageCount1 = new ToolStripLabel();

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            bt.Items.AddRange(new System.Windows.Forms.ToolStripItem[]{
                toolStripButton18,
                toolStripButton17,
                toolStripTextBox18,
                lblPageCount1,
                toolStripButton16,
                toolStripButton19
            });

            toolStripTextBox18.Size = new System.Drawing.Size(50, 23);

            toolStripButton16.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton16.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton16.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton16.Name = "toolStripButton16";
            toolStripButton16.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton16.Text = "下一页";

            toolStripButton17.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton17.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton17.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton17.Name = "toolStripButton16";
            toolStripButton17.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton17.Text = "上一页";


            toolStripButton18.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton18.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton18.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton18.Name = "toolStripButton16";
            toolStripButton18.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton18.Text = "首页";


            toolStripButton19.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            toolStripButton19.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButton6.Image")));
            toolStripButton19.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripButton19.Name = "toolStripButton16";
            toolStripButton19.Size = new System.Drawing.Size((int)(50), (int)(23));
            toolStripButton19.Text = "最后一页";

            lblPageCount1.Name = "lblPageCount";
            lblPageCount1.Size = new System.Drawing.Size((int)(50), (int)(23));
            lblPageCount1.Text = "/5";


            bt.Location = new System.Drawing.Point(0, 450);
            bt.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(bt_ItemClicked);

            groupBox_pic.Controls.Add(bt);

            lblPageCount1.Text = chartCount.ToString();
            toolStripTextBox18.Text = Convert.ToString(chartCurrent);



            if(checkedListBox2.CheckedItems.Count %6 == 1)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx.Location = new System.Drawing.Point((int)(20), (int)(75));
                bx.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);
                }

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);


                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 2)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx.Location = new System.Drawing.Point((int)(20), (int)(75));
                bx.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);
                }

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);


                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx1.Location = new System.Drawing.Point((int)(270), (int)(75));
                bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID2.Location = new System.Drawing.Point((int)(280), (int)(55));
                ID2.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID2.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID2);


                datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb21.Location = new System.Drawing.Point((int)((450)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb21);

                datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb22.Location = new System.Drawing.Point((int)((450)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb22);


                datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb23.Location = new System.Drawing.Point((int)((450)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb23);


                datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb24.Location = new System.Drawing.Point((int)((450)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb24);


                datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb25.Location = new System.Drawing.Point((int)((450)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb25);

                datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb26.Location = new System.Drawing.Point((int)((450)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb26);


            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 3)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx.Location = new System.Drawing.Point((int)(20), (int)(75));
                bx.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);
                }

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);


                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx1.Location = new System.Drawing.Point((int)(270), (int)(75));
                bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID2.Location = new System.Drawing.Point((int)(280), (int)(55));
                ID2.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID2.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID2);


                datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb21.Location = new System.Drawing.Point((int)((450)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb21);

                datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb22.Location = new System.Drawing.Point((int)((450)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb22);


                datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb23.Location = new System.Drawing.Point((int)((450)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb23);


                datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb24.Location = new System.Drawing.Point((int)((450)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb24);


                datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb25.Location = new System.Drawing.Point((int)((450)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb25);

                datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb26.Location = new System.Drawing.Point((int)((450)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb26);

                PictureBox bx2 = new PictureBox();
                bx2.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx2.Location = new System.Drawing.Point((int)(530), (int)(75));
                bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx2);

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((650)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID3.Location = new System.Drawing.Point((int)(530), (int)(55));
                ID3.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID3.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID3);

                datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb31.Location = new System.Drawing.Point((int)((710)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb31);

                datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
                datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb32.Location = new System.Drawing.Point((int)((710)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb32);


                datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb33.Location = new System.Drawing.Point((int)((710)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb33);


                datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb34.Location = new System.Drawing.Point((int)((710)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb34);


                datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb35.Location = new System.Drawing.Point((int)((710)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb35);

                datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb36.Location = new System.Drawing.Point((int)((710)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb36);


            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 4)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx.Location = new System.Drawing.Point((int)(20), (int)(75));
                bx.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);
                }

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);


                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx1.Location = new System.Drawing.Point((int)(270), (int)(75));
                bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID2.Location = new System.Drawing.Point((int)(280), (int)(55));
                ID2.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID2.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID2);


                datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb21.Location = new System.Drawing.Point((int)((450)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb21);

                datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb22.Location = new System.Drawing.Point((int)((450)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb22);


                datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb23.Location = new System.Drawing.Point((int)((450)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb23);


                datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb24.Location = new System.Drawing.Point((int)((450)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb24);


                datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb25.Location = new System.Drawing.Point((int)((450)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb25);

                datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb26.Location = new System.Drawing.Point((int)((450)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb26);

                PictureBox bx2 = new PictureBox();
                bx2.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx2.Location = new System.Drawing.Point((int)(530), (int)(75));
                bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx2);

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((650)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID3.Location = new System.Drawing.Point((int)(530), (int)(55));
                ID3.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID3.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID3);

                datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb31.Location = new System.Drawing.Point((int)((710)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb31);

                datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
                datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb32.Location = new System.Drawing.Point((int)((710)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb32);


                datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb33.Location = new System.Drawing.Point((int)((710)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb33);


                datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb34.Location = new System.Drawing.Point((int)((710)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb34);


                datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb35.Location = new System.Drawing.Point((int)((710)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb35);

                datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb36.Location = new System.Drawing.Point((int)((710)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb36);

                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(275));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((245 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID4.Location = new System.Drawing.Point((int)(30), (int)(255));
                ID4.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID4.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID4);


                datalb41.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb41.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb41.Location = new System.Drawing.Point((int)((200)), (int)((245 + 30)));
                groupBox_pic.Controls.Add(datalb41);

                datalb42.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb42.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb42.Location = new System.Drawing.Point((int)((200)), (int)((245 + 60)));
                groupBox_pic.Controls.Add(datalb42);


                datalb43.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb43.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb43.Location = new System.Drawing.Point((int)((200)), (int)((245 + 90)));
                groupBox_pic.Controls.Add(datalb43);


                datalb44.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb44.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb44.Location = new System.Drawing.Point((int)((200)), (int)((245 + 120)));
                groupBox_pic.Controls.Add(datalb44);


                datalb45.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb45.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb45.Location = new System.Drawing.Point((int)((200)), (int)((245 + 150)));
                groupBox_pic.Controls.Add(datalb45);

                datalb46.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb46.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb46.Location = new System.Drawing.Point((int)((200)), (int)((245 + 180)));
                groupBox_pic.Controls.Add(datalb46);

            }
            else if(checkedListBox2.CheckedItems.Count % 6 == 5)
            {
                PictureBox bx = new PictureBox();
                bx.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx.Location = new System.Drawing.Point((int)(20), (int)(75));
                bx.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);
                }

                ID1.Location = new System.Drawing.Point((int)(30), (int)(55));
                ID1.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID1.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID1);


                datalb11.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb11.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb11.Location = new System.Drawing.Point((int)((200)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb11);

                datalb12.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb12.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb12.Location = new System.Drawing.Point((int)((200)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb12);


                datalb13.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb13.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb13.Location = new System.Drawing.Point((int)((200)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb13);


                datalb14.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb14.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb14.Location = new System.Drawing.Point((int)((200)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb14);


                datalb15.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb15.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb15.Location = new System.Drawing.Point((int)((200)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb15);

                datalb16.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb16.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb16.Location = new System.Drawing.Point((int)((200)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb16);

                PictureBox bx1 = new PictureBox();
                bx1.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx1.Location = new System.Drawing.Point((int)(270), (int)(75));
                bx1.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx1);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((390)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID2.Location = new System.Drawing.Point((int)(280), (int)(55));
                ID2.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID2.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID2);


                datalb21.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb21.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb21.Location = new System.Drawing.Point((int)((450)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb21);

                datalb22.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb22.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb22.Location = new System.Drawing.Point((int)((450)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb22);


                datalb23.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb23.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb23.Location = new System.Drawing.Point((int)((450)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb23);


                datalb24.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb24.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb24.Location = new System.Drawing.Point((int)((450)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb24);


                datalb25.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb25.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb25.Location = new System.Drawing.Point((int)((450)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb25);

                datalb26.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb26.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb26.Location = new System.Drawing.Point((int)((450)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb26);

                PictureBox bx2 = new PictureBox();
                bx2.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx2.Location = new System.Drawing.Point((int)(530), (int)(75));
                bx2.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx2);

                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((650)), (int)((55 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID3.Location = new System.Drawing.Point((int)(530), (int)(55));
                ID3.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID3.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID3);

                datalb31.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb31.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb31.Location = new System.Drawing.Point((int)((710)), (int)((55 + 30)));
                groupBox_pic.Controls.Add(datalb31);

                datalb32.Size = new System.Drawing.Size((int)(40), (int)(20));
                datalb32.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb32.Location = new System.Drawing.Point((int)((710)), (int)((55 + 60)));
                groupBox_pic.Controls.Add(datalb32);


                datalb33.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb33.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb33.Location = new System.Drawing.Point((int)((710)), (int)((55 + 90)));
                groupBox_pic.Controls.Add(datalb33);


                datalb34.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb34.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb34.Location = new System.Drawing.Point((int)((710)), (int)((55 + 120)));
                groupBox_pic.Controls.Add(datalb34);


                datalb35.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb35.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb35.Location = new System.Drawing.Point((int)((710)), (int)((55 + 150)));
                groupBox_pic.Controls.Add(datalb35);

                datalb36.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb36.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb36.Location = new System.Drawing.Point((int)((710)), (int)((55 + 180)));
                groupBox_pic.Controls.Add(datalb36);

                PictureBox bx3 = new PictureBox();
                bx3.Image = global::Warehouse.Properties.Resources._001;
                //bx.Location = new System.Drawing.Point((int)(tukuan * SW_percent), (int)(50 * SH_percent));
                bx3.Location = new System.Drawing.Point((int)(20), (int)(275));
                bx3.Size = new System.Drawing.Size((int)(100), (int)(150));
                bx3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
                groupBox_pic.Controls.Add(bx3);


                for (int j = 0; j < 6; j++)
                {
                    Label label = new Label();
                    label.Text = zifu[j];
                    label.Size = new System.Drawing.Size((int)(60), (int)(20));
                    label.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                    label.Location = new System.Drawing.Point((int)((140)), (int)((245 + (j + 1) * 30)));
                    label.BringToFront();
                    groupBox_pic.Controls.Add(label);

                }

                ID4.Location = new System.Drawing.Point((int)(30), (int)(255));
                ID4.Size = new System.Drawing.Size((int)(100), (int)(25));
                ID4.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                groupBox_pic.Controls.Add(ID4);


                datalb41.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb41.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb41.Location = new System.Drawing.Point((int)((200)), (int)((245 + 30)));
                groupBox_pic.Controls.Add(datalb41);

                datalb42.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb42.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb42.Location = new System.Drawing.Point((int)((200)), (int)((245 + 60)));
                groupBox_pic.Controls.Add(datalb42);


                datalb43.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb43.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb43.Location = new System.Drawing.Point((int)((200)), (int)((245 + 90)));
                groupBox_pic.Controls.Add(datalb43);


                datalb44.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb44.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb44.Location = new System.Drawing.Point((int)((200)), (int)((245 + 120)));
                groupBox_pic.Controls.Add(datalb44);


                datalb45.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb45.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb45.Location = new System.Drawing.Point((int)((200)), (int)((245 + 150)));
                groupBox_pic.Controls.Add(datalb45);

                datalb46.Size = new System.Drawing.Size((int)(80), (int)(20));
                datalb46.Font = new System.Drawing.Font("宋体", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                datalb46.Location = new System.Drawing.Point((int)((200)), (int)((245 + 180)));
                groupBox_pic.Controls.Add(datalb46);


            }      
        }

        private void bt_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

            if (e.ClickedItem.Text == "上一页")
            {
                //MessageBox.Show("点击上一页");
                chartCurrent--;
                if (chartCurrent < 1)
                {
                    MessageBox.Show("已经是第一页，请点击“下一页”查看!");
                    chartCurrent = 1;
                    return;

                }
                groupBox_pic.Controls.Clear();
                ShowFive();

            }
            if (e.ClickedItem.Text == "下一页")
            {
                //MessageBox.Show("点击下一页");
                chartCurrent++;
                if (chartCurrent > chartCount)
                {
                    MessageBox.Show("已经是最后一页，请点击“上一页”查看！");
                    pageCurrent = chartCount;
                    return;
                }
                groupBox_pic.Controls.Clear();
                ShowFive();
            }

        }




        /// <summary>
        /// 料位监控按钮点击事件功能实现
        /// 图形化显示某一个料仓的情况
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {

            numxuanzhong = 0;
            groupBox_pic.Controls.Clear();
            InitDataSet1();
            int select_item = checkedListBox1.CheckedItems.Count + checkedListBox2.CheckedItems.Count;
            int select_item1 = checkedListBox1.CheckedItems.Count;
            int select_item2 = checkedListBox2.CheckedItems.Count;
            int SW = SystemInformation.WorkingArea.Width;
            int SH = SystemInformation.WorkingArea.Height;
            //numxuanzhong = checkedListBox1.CheckedItems.Count;
            //MessageBox.Show("SW" + SW+ "SH"+SH);
            double SW_percent = (double)SW / (double)1366;
            double SH_percent = (double)SH / (double)728;
            button8.PerformClick();
            button8.Visible = true;
            button8.Location = new Point((int)(800 * SW_percent), button8.Location.Y);//关闭按钮的位置
            groupBox_pic.Visible = true;//主页面是否可见


            //id = selectID(checkedListBox2.Items.ToString());



            float bili = 16F / select_item2;//字体比例

            //MessageBox.Show("select_item2" + select_item2);
            //不在线料仓
            if (select_item2 == 2)
            {
                SW_percent = SW_percent / 2;
                ShowTwo(SW_percent, SH_percent);
                
            }else if(select_item2 == 3)
            {
                SW_percent = SW_percent / 3;
                ShowTwo(SW_percent, SH_percent);
            }
            else if(select_item2 == 4)
            {
                SW_percent = SW_percent / 4;
                SH_percent = SH_percent / 4;
                ShowFour(SW_percent, SH_percent, select_item2);
            }
            else if (select_item2 == 5)
            {
                SW_percent = SW_percent / 5;
                SH_percent = SH_percent / 5;
                ShowFour(SW_percent, SH_percent, select_item2);
            }
            else if (select_item2 == 6)
            {
                SW_percent = SW_percent / 6;
                SH_percent = SH_percent / 6;
                ShowFour(SW_percent, SH_percent, select_item2);
            }else if (select_item2 >6)
            {
                ShowFive();

            }

            //在线料仓
            if (select_item1 == 2)
            {
                SW_percent = SW_percent / 2;
                ShowTwo1(SW_percent, SH_percent);

            }else if(select_item1 == 3)
            {
                SW_percent = SW_percent / 3;
                ShowTwo1(SW_percent, SH_percent);
            }
            else if (select_item1 == 4)
            {
                SW_percent = SW_percent / 4;
                SH_percent = SH_percent / 4;
                ShowFour1(SW_percent, SH_percent, select_item1);
            }


            //同时选择在线与不在料仓
            if (checkedListBox1.CheckedItems.Count != 0 && checkedListBox2.CheckedItems.Count != 0)
            {
                int num = checkedListBox1.CheckedItems.Count + checkedListBox2.CheckedItems.Count;
                if(num == 2)
                {
                    ShowTwo2(SW_percent, SH_percent);

                }
                else if(num == 3)
                {

                }else if(num == 4)
                {

                }

            }

            if ((checkedListBox1.CheckedItems.Count == 1 && checkedListBox2.CheckedItems.Count!=1)||(checkedListBox2.CheckedItems.Count == 1 && checkedListBox1.CheckedItems.Count != 1))
            {

                label5.Text = "0";
                label31.Text = "0";
                label21.Text = "0";
                label24.Text = "0";
                label27.Text = "0";
                labelWenDuTest.Text = "没接收到实时温度";


                groupBox_pic.Controls.Add(this.label27);
                groupBox_pic.Controls.Add(this.label24);
                groupBox_pic.Controls.Add(this.label21);
                groupBox_pic.Controls.Add(this.label31);
                groupBox_pic.Controls.Add(this.label5);
                groupBox_pic.Controls.Add(this.labelWenDuTest);

                groupBox_pic.Controls.Add(this.label29);
                groupBox_pic.Controls.Add(this.label26);
                groupBox_pic.Controls.Add(this.label23);
                groupBox_pic.Controls.Add(this.labelWenDu);



                groupBox_pic.Controls.Add(this.label20);
                groupBox_pic.Controls.Add(this.label30);
                groupBox_pic.Controls.Add(this.label4);
                groupBox_pic.Controls.Add(this.progressBar1);
                groupBox_pic.Controls.Add(this.pictureBox1);
                groupBox_pic.Controls.Add(this.buttonToAnalysisData);//添加元素


                if (checkedListBox1.CheckedItems.Count == 1)//在线料仓的查询
                {
                    查询温度ToolStripMenuItem.PerformClick();
                    for (int i = 0; i < checkedListBox1.Items.Count; i++)
                    {
                        if (checkedListBox1.GetItemChecked(i))
                        {
                            if (SqlConnect == 1)
                            {
                                //MessageBox.Show(checkedListBox1.Items[i].ToString());
                                float fangchang = 0, fangkuan = 0, pyramidh = 0, xiazhui = 0, cylinderh=0, diameter=0;
                                string type = "";
                                label29.Text = checkedListBox1.Items[i].ToString();

                                string id = selectID(checkedListBox1.Items[i].ToString());


                                correntWenDo = id;


                                string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0201");//2为实时查询温度。
                                                                                                         //show("发送的指令：" + data + "\r\n\r\n");
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询测量设备温度\r\n\r\n");
                                sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内温度", data, s_Produce));//指令为返回的代码


                                //查询仓内实时温度
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询仓内温度\r\n\r\n");
                                string data1 = Data.Data(comboBox4.Text, id, "16", "0000");
                                sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", data1, s_Produce));



                                string sql = "select * from bininfo where BinID = " + id;

                                MySqlConn ms0 = new MySqlConn();
                                MySqlDataReader rd0 = ms0.getDataFromTable(sql);
                                while (rd0.Read())
                                {
                                    type = rd0["type"].ToString();
                             
                                }
                                rd0.Close();
                                ms0.Close();

                                if (type.Equals("4"))
                                {
                                    //MessageBox.Show("执行方仓计算");
                                    try
                                    {

                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.Dr = db.command.ExecuteReader();
                                        MySqlConn ms = new MySqlConn();
                                        MySqlDataReader rd = ms.getDataFromTable(sql);
                                        while (rd.Read())
                                        {
                                            fangchang = float.Parse(rd["Fbian"].ToString());
                                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                                            pyramidh = float.Parse(rd["PyramidH"].ToString());
                                        }
                                        rd.Close();
                                        ms.Close();

                                        if (pyramidh < 0.1)
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._0011;
                                        }
                                        else
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._001;
                                        }

                                        float Vol_ware = (float)(fangchang*fangkuan*cylinderh+(Math.PI*fangkuan*fangkuan*pyramidh)/12);
                                        label31.Text = Vol_ware.ToString() + "  m³";
                                        //MessageBox.Show("id为=" + id);
                                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                        MySqlConn ms1 = new MySqlConn();
                                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);


                                        while (rd1.Read())
                                        {
                                            label5.Text = rd1["Volume"].ToString() + "  m³";
                                            label21.Text = rd1["Weight"].ToString() + "  吨";
                                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                                            if (vol_feed < Vol_ware)
                                            {
                                                if (vol_feed < 0)
                                                {
                                                    progressBar1.Value = 0;
                                                }
                                                else
                                                {
                                                    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                                }

                                            }
                                            else
                                            {
                                                progressBar1.Value = 0;
                                                MessageBox.Show("数据有误", "提示");
                                            }
                                            break;
                                        }

                                        rd1.Close();
                                        ms1.Close();
                                        //sql = "select * from [bindata] where [BinID] = " + id + "AND [Temp] is not NULL order by [DateTime] desc";
                                        //db.command.CommandText = sql;
                                        //db.Dr = db.command.ExecuteReader();
                                        //while (db.Dr.Read())
                                        //{
                                        //    label24.Text = db.Dr["Temp"].ToString() + "  ℃";
                                        //    label27.Text = db.Dr["Hum"].ToString() + "  %";

                                        //    break;
                                        //}




                                        if (label31.Text.Equals("0"))
                                        {
                                            MessageBox.Show("请检测参数设置，当前料仓体积为0", "提示");
                                        }

                                    }
                                    catch (SqlException se)
                                    {
                                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                        thread_file.Start(se.ToString());
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                        //MessageBox.Show("", "提示");
                                    }

                                }
                                else
                                {
                                    try
                                    {

                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.Dr = db.command.ExecuteReader();
                                        MySqlConn ms = new MySqlConn();
                                        MySqlDataReader rd = ms.getDataFromTable(sql);
                                        while (rd.Read())
                                        {
                                            diameter = float.Parse(rd["Diameter"].ToString());
                                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                                            pyramidh = float.Parse(rd["PyramidH"].ToString());
                                        }
                                        rd.Close();
                                        ms.Close();

                                        if (pyramidh < 0.1)
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._0011;
                                        }
                                        else
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._001;
                                        }

                                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                        label31.Text = Vol_ware.ToString() + "  m³";
                                        //MessageBox.Show("id为=" + id);
                                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                        MySqlConn ms1 = new MySqlConn();
                                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);


                                        while (rd1.Read())
                                        {
                                            label5.Text = rd1["Volume"].ToString() + "  m³";
                                            label21.Text = rd1["Weight"].ToString() + "  吨";
                                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                                            if (vol_feed < Vol_ware)
                                            {
                                                if (vol_feed < 0)
                                                {
                                                    progressBar1.Value = 0;
                                                }
                                                else
                                                {
                                                    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                                }

                                            }
                                            else
                                            {
                                                progressBar1.Value = 0;
                                                MessageBox.Show("数据有误", "提示");
                                            }
                                            break;
                                        }

                                        rd1.Close();
                                        ms1.Close();
                                        //sql = "select * from [bindata] where [BinID] = " + id + "AND [Temp] is not NULL order by [DateTime] desc";
                                        //db.command.CommandText = sql;
                                        //db.Dr = db.command.ExecuteReader();
                                        //while (db.Dr.Read())
                                        //{
                                        //    label24.Text = db.Dr["Temp"].ToString() + "  ℃";
                                        //    label27.Text = db.Dr["Hum"].ToString() + "  %";

                                        //    break;
                                        //}




                                        if (label31.Text.Equals("0"))
                                        {
                                            MessageBox.Show("请检测参数设置，当前料仓体积为0", "提示");
                                        }

                                    }
                                    catch (SqlException se)
                                    {
                                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                        thread_file.Start(se.ToString());
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                        //MessageBox.Show("", "提示");
                                    }
                                }
                            }
                            else//判断数据库是否可以使用else
                            {
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                //MessageBox.Show("", "提示");
                            }

                        }//判断是否被选中
                    }//循环
                }
                else if (checkedListBox2.CheckedItems.Count == 1)//不在线料仓的查询
                {
                   //essageBox.Show("", "提示****" + checkedListBox2.CheckedItems.Count);
                    for (int i = 0; i < checkedListBox2.Items.Count; i++)
                    {
                        if (checkedListBox2.GetItemChecked(i))
                        {
                            if (SqlConnect == 1)
                            {
                                //MessageBox.Show(checkedListBox1.Items[i].ToString());
                                float fangchang = 0, fangkuan = 0, pyramidh = 0, xiazhui = 0, cylinderh = 0, diameter=0;
                                string type = "";
                                label29.Text = checkedListBox2.Items[i].ToString();

                                string id = selectID(checkedListBox2.Items[i].ToString());
                                string sql = "select * from bininfo where BinID = " + id;


                                MySqlConn ms0 = new MySqlConn();
                                MySqlDataReader rd0 = ms0.getDataFromTable(sql);
                                while (rd0.Read())
                                {
                                    type = rd0["type"].ToString();

                                }
                                rd0.Close();
                                ms0.Close();

                                if(type.Equals("4"))
                                {
                                    //MessageBox.Show("执行方仓计算");
                                    try
                                    {

                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.Dr = db.command.ExecuteReader();
                                        MySqlConn ms = new MySqlConn();
                                        MySqlDataReader rd = ms.getDataFromTable(sql);
                                        while (rd.Read())
                                        {
                                            fangchang = float.Parse(rd["Fbian"].ToString());
                                            fangkuan = float.Parse(rd["Fkuan"].ToString());
                                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                                            pyramidh = float.Parse(rd["PyramidH"].ToString());
                                        }
                                        rd.Close();
                                        ms.Close();

                                        if (pyramidh < 0.1)
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._0011;
                                        }
                                        else
                                        {
                                            this.pictureBox1.Image = global::Warehouse.Properties.Resources._001;
                                        }

                                        float Vol_ware = (float)(fangchang * fangkuan * cylinderh + (Math.PI * fangkuan * fangkuan * pyramidh) / 12);
                                        label31.Text = Vol_ware.ToString() + "  m³";
                                        //MessageBox.Show("id为=" + id);
                                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                        MySqlConn ms1 = new MySqlConn();
                                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);


                                        while (rd1.Read())
                                        {
                                            label5.Text = rd1["Volume"].ToString() + "  m³";
                                            label21.Text = rd1["Weight"].ToString() + "  吨";
                                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                                            if (vol_feed < Vol_ware)
                                            {
                                                if (vol_feed < 0)
                                                {
                                                    progressBar1.Value = 0;
                                                }
                                                else
                                                {
                                                    progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                                }

                                            }
                                            else
                                            {
                                                progressBar1.Value = 0;
                                                MessageBox.Show("数据有误", "提示");
                                            }
                                            break;
                                        }

                                        rd1.Close();
                                        ms1.Close();
                                        //sql = "select * from [bindata] where [BinID] = " + id + "AND [Temp] is not NULL order by [DateTime] desc";
                                        //db.command.CommandText = sql;
                                        //db.Dr = db.command.ExecuteReader();
                                        //while (db.Dr.Read())
                                        //{
                                        //    label24.Text = db.Dr["Temp"].ToString() + "  ℃";
                                        //    label27.Text = db.Dr["Hum"].ToString() + "  %";

                                        //    break;
                                        //}




                                        if (label31.Text.Equals("0"))
                                        {
                                            MessageBox.Show("请检测参数设置，当前料仓体积为0", "提示");
                                        }

                                    }
                                    catch (SqlException se)
                                    {
                                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                        thread_file.Start(se.ToString());
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                        //MessageBox.Show("", "提示");
                                    }
                                }
                                else
                                {
                                    try
                                    {


                                        MySqlConn ms = new MySqlConn();
                                        MySqlDataReader rd = ms.getDataFromTable(sql);
                                        while (rd.Read())
                                        {
                                            diameter = float.Parse(rd["Diameter"].ToString());
                                            cylinderh = float.Parse(rd["CylinderH"].ToString());
                                            pyramidh = float.Parse(rd["PyramidH"].ToString());
                                        }
                                        rd.Close();
                                        ms.Close();
                                        float Vol_ware = (float)((diameter / 2) * (diameter / 2) * (3.14) * (pyramidh / 3 + cylinderh));
                                        label31.Text = Vol_ware.ToString() + "  m³";
                                        //MessageBox.Show(Vol_ware.ToString());

                                        sql = "select * from bindata where BinID = " + id + " AND Volume is not NULL order by DateTime desc";
                                        //MessageBox.Show(sql);
                                        //db.command.CommandText = sql;
                                        //db.Dr = db.command.ExecuteReader();
                                        MySqlConn ms1 = new MySqlConn();
                                        MySqlDataReader rd1 = ms1.getDataFromTable(sql);
                                        while (rd1.Read())
                                        {
                                            label5.Text = rd1["Volume"].ToString() + "  m³";
                                            label21.Text = rd1["Weight"].ToString() + "  吨";
                                            label24.Text = rd1["Temp"].ToString() + "  ℃";
                                            label27.Text = rd1["Hum"].ToString() + "  %";
                                            float vol_feed = float.Parse(rd1["Volume"].ToString());
                                            if (vol_feed < Vol_ware)
                                            {
                                                progressBar1.Value = (int)((vol_feed / Vol_ware) * 100);
                                            }
                                            else
                                            {
                                                progressBar1.Value = 0;
                                                MessageBox.Show("数据有误", "提示");
                                            }
                                            break;
                                        }
                                        rd1.Close();
                                        ms1.Close();


                                        //sql = "select * from bindata where BinID = " + id + "AND Temp is not NULL order by DateTime desc";
                                        //MySqlConn ms2 = new MySqlConn();
                                        //MySqlDataReader rd2 = ms2.getDataFromTable(sql);
                                        ////db.command.CommandText = sql;
                                        ////db.Dr = db.command.ExecuteReader();
                                        //while (rd2.Read())
                                        //{
                                        //    label24.Text = rd2["Temp"].ToString() + "  ℃";
                                        //    label27.Text =rd2["Hum"].ToString() + "  %";

                                        //    break;
                                        //}

                                        //if (label31.Text.Equals("0"))
                                        //{
                                        //    MessageBox.Show("请检测参数设置，当前料仓体积为0", "提示");
                                        //}
                                        //rd2.Close();
                                        //ms2.Close();
                                    }
                                    catch (SqlException se)
                                    {
                                        Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                                        thread_file.Start(se.ToString());
                                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                        //MessageBox.Show("", "提示");
                                    }
                                }


                            }
                            else//判断数据库是否可以使用else
                            {
                                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                                //MessageBox.Show("", "提示");
                            }

                        }//判断是否被选中
                    }//循环

                }
            }

            //}//判断是否选中一个
        }

        /// <summary>
        /// 库存查询按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            显示数据信息ToolStripMenuItem.PerformClick();
        }

        /// <summary>
        /// 查询温度按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 查询温度ToolStripMenuItem_Click(object sender, EventArgs e)
        {


            if (checkedListBox1.CheckedItems.Count == 0)
            {
                //MessageBox.Show("", "提示");
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓查询温湿度");
                return;
            }


            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data = Data.Data(comboBox4.Text, id, "16", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "11", id, false, TIME+5, "查询温湿度", data));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", data, s_Produce));

                }
            }
            查询温度ToolStripMenuItem.Enabled = false;
        }
        
        /// <summary>
        /// 加热板查询温度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WenDuToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (checkedListBox1.CheckedItems.Count == 0)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓查询测量端设置温度");
                return;
            }

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());


                    //string data = Data.Data(comboBox4.Text, id, "44", "0100");//查询温度。全0为。设置前两个字节
                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0040");//0为设置温度。。设置为40度。查询温度...00 字节0 + 00 字节1 + 00 字节2 + 0 字节3

                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0032");//按照设置温度加热，27度
                    //1为查询设置温度。查询温度...00 字节0 + 00 字节1 + 00 字节2 + 0 字节3

                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0200");//2为实时查询温度。

                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0300");//3为取消加热。

                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0301");//3为开始加热。
                    //show("发送的指令：" + data + "\r\n\r\n");
                    richTextBox1.AppendText("查询\r\n");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "11", id, false, TIME+5, "查询温湿度", data));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内温度", data, s_Produce));//指令为返回的代码

                }
            }
            //查询温度ToolStripMenuItem.Enabled = false;
        }
        /// <summary>
        /// 加热板查询温度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimeToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (checkedListBox1.CheckedItems.Count == 0)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓");
                return;
            }

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());

                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0201");//查询一次温度
                    //show("发送的指令：" + data + "\r\n\r\n");
                    richTextBox1.AppendText("查询\r\n");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询加热时间", data, s_Produce));//指令为返回的代码

                }
            }
            //查询温度ToolStripMenuItem.Enabled = false;
        }
        //开启加热。按照加热时间
        private void SetWenDuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.CheckedItems.Count == 0)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓查询摄像头内温度");
                return;
            }
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0000");//按照设置温度加热.第二个字节加温度
                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0401");//按时间加热。第二个字节加时间，加热时间1分钟
                    richTextBox1.AppendText("发送加热指令:"+ data + "\r\n");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2E", id, false, TIME, "加热", data, s_Produce));//指令为返回的代码
                }
            }

          
        }
        //设置时间
        private void SetTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetWenDu swd = new SetWenDu();
            swd.setModel = "2";
            swd.Show();
        }

        //取消加热
        private void CancelWenDuToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (checkedListBox1.CheckedItems.Count == 0)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请选择料仓查询摄像头内温度");
                return;
            }

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    //string data = Data.Data(comboBox4.Text, id, "44", "0100");//查询温度。全0为。设置前两个字节
                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0040");//0为设置温度。。设置为40度。查询温度...00 字节0 + 00 字节1 + 00 字节2 + 0 字节3

                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0100");//1为查询设置温度。查询温度...00 字节0 + 00 字节1 + 00 字节2 + 0 字节3

                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0200");//2为实时查询温度。

                    string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0100");//取消加热。
                    //show("发送的指令：" + data + "\r\n\r\n");
                    //string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0301");//3为开始加热。
                    //serialPort_WriteLine(new FacMessage(ins_num++, "11", id, false, TIME+5, "查询温湿度", data));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内温度", data, s_Produce));//指令为返回的代码

                }
            }
            //查询温度ToolStripMenuItem.Enabled = false;
        }
        /// <summary>
        /// 更改名称输入回车键功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripTextBox6_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Return)
                {
                    if (toolStripTextBox6.Text.Equals("") == false)
                    {
                        string id = selectID(checkedListBox1.SelectedItem.ToString());
                        string sql = "select * from bininfo";
                        bool isExist = false;
                        //DataBase db = new DataBase();
                        //db.command.CommandText = sql;
                        //db.command.Connection = db.connection;
                        //db.Dr = db.command.ExecuteReader();
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            if (rd["BinName"].Equals(toolStripTextBox6.Text))
                                isExist = true;
                        }
                        rd.Close();
                        ms.Close();
                        if (isExist == false)
                        {
                            sql = "update bininfo set BinName = '" + toolStripTextBox6.Text + "' where BinID = " + id.ToString();
                            MySqlConn ms1 = new MySqlConn();
                            int isR = ms1.nonSelect(sql);
                            ms1.Close();

                            if (isR > 0)
                            {
                                Invoke(new MethodInvoker(delegate ()
                                {
                                    //让文本框获取焦点，不过注释这行也能达到效果
                                    richTextBox1.Focus();
                                    //设置光标的位置到文本尾   
                                    richTextBox1.Select(richTextBox1.TextLength, 0);
                                    //滚动到控件光标处   
                                    richTextBox1.ScrollToCaret();
                                    richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n更改料仓名称成功\r\n\r\n");
                                }));



                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n修改料仓名成功\r\n\r\n");

                            }

                            sql = "update binauto set BinName = '" + toolStripTextBox6.Text + "' where BinID = " + id.ToString();
                            MySqlConn ms2 = new MySqlConn();
                            int isR1 = ms2.nonSelect(sql);
                            ms2.Close();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            checkedListBox1.Items.Remove(checkedListBox1.SelectedItem.ToString());
                            checkedListBox1.Items.Add(toolStripTextBox6.Text);
                            SortCheckedList(checkedListBox1);
                            contextMenuStrip1.Visible = false;
                            toolStripTextBox6.Text = "";
                            //Thread a = new Thread(display);
                            //a.Start();
                        }
                        else
                        {
                            MessageBox.Show("料仓名重复，请重新输入", "提示");
                            toolStripTextBox6.Text = "";
                        }

                    }
                    else
                    {
                        MessageBox.Show("请输入名称", "提示");
                    }

                }
            }
            catch(Exception ee)
            {
                MessageBox.Show("请选择料仓");
            }
            
        }

        /// <summary>
        /// 清洁镜头按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 清洁镜头ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        
        private void BroadToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        private void bdnInfo_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Text == "关闭")
            {
                button8.PerformClick();
            }
            if (e.ClickedItem.Text == "上一页")
            
            {
                pageCurrent--;
                if (pageCurrent <= 0)
                {
                    MessageBox.Show("已经是第一页，请点击“下一页”查看！");
                    pageCurrent = 1;
                    return;
                }
                else
                {
                    nCurrent = pageSize * (pageCurrent - 1);
                }

                LoadData();
            }
            if (e.ClickedItem.Text == "下一页")
            {
                pageCurrent++;
                if (pageCurrent > pageCount)
                {
                    MessageBox.Show("已经是最后一页，请点击“上一页”查看！");
                    pageCurrent = pageCount;
                    return;
                }
                else
                {
                    nCurrent = pageSize * (pageCurrent - 1);
                }
                LoadData();
            }
        }

        private void txtCurrentPage_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (int.Parse(txtCurrentPage.Text) <= pageCount)
                {
                    pageCurrent = int.Parse(txtCurrentPage.Text);
                    nCurrent = pageSize * (pageCurrent - 1);
                    LoadData();
                }
            }
        }

        private void 取消当前操作ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (checkedListBox1.CheckedItems.Count == 0)
            {
                MessageBox.Show("请选择料仓进行操作", "提示");
                return;
            }
            //for (int i = send_ins.Count - 1; i >= 0;i-- )
            //    send_ins.RemoveAt(i);
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    //MessageBox.Show("comboBox4.Text*" +comboBox4.Text+"*");
                    string data = Data.Data(comboBox4.Text, id, "30", "0000");
                    FacMessage facmes = new FacMessage(ins_num++, "1F", id, false, TIME, "取消当前操作", data, 3, s_Produce);
                    sendIns_queue.Enqueue(facmes);

                    if (ins_num > 2000)
                    {
                        ins_num = 1;
                    }

                }
            }
        }

        public Monitor monitor;
        /// <summary>
        /// 进入监控按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 进入监控ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //oper_ins.Clear();
            if (checkedListBox1.CheckedItems.Count == 0)
            {
                MessageBox.Show("请选择料仓进行监控", "提示");
                return;
            }

            t_monitor.Enabled = true;
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                    //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    string data = Data.Data(comboBox4.Text, id, "26", "0001");
                    aim_ins.Enqueue(new FacMessage(0, "1B", id, false, 6, "进入监控", data, s_Produce - 3595));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "料仓监控前查询状态", data_search, 3, s_Produce - 3595));
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));

                }
            }
        }

        private void show_monitor_method(object obj)
        {
            MethodInvoker meth = new MethodInvoker(show_monitor);
            BeginInvoke(meth);
        }

        private void show_monitor()
        {
            monitor.Visible = true;
        }

        private void inquire_height(object sender, System.Timers.ElapsedEventArgs e)
        {//向中控发送查询高度指令函数
            for (int i = 0; i < comboBox6.Items.Count; i++)
            {
                string data = Data.Data(comboBox4.Text, selectID(comboBox6.Items[i].ToString()), "28", "0000");
                serialPort_WriteLine(new FacMessage(ins_num++, "1D", selectID(comboBox6.Items[i].ToString()), false, TIME, "高度信息", data, s_Produce));

                if (ins_num > 2000)
                {
                    ins_num = 1;
                }

            }
        }

        private void 退出监控ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.CheckedItems.Count == 0)
            {
                MessageBox.Show("请选择料仓进行退出监控", "提示");
                return;
            }


            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data = Data.Data(comboBox4.Text, id, "26", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "1B", id, false, TIME, "退出监控状态", data, s_Produce));
                    if (ins_num > 2000)
                    {
                        ins_num = 1;
                    }

                }
            }
        }


        private void 导出数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog sfdExport = new SaveFileDialog();
                sfdExport.Filter = "文本文件|*.txt";
                if (sfdExport.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                FileStream fileStream = new FileStream(sfdExport.FileName, FileMode.Append);
                StreamWriter streamWriter = new StreamWriter(fileStream);



                int sumLine = 0;
                string sql  = "select * from bindata";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                while (rd.Read())
                {
                    sumLine++;
                    string BinID = rd["BinID"].ToString();
                    string Volume = rd["Volume"].ToString();
                    string Weight = rd["Weight"].ToString();
                    string Temp = rd["Temp"].ToString();
                    string Hum = rd["Hum"].ToString();
                    string DateTime = rd["DateTime"].ToString();
                    streamWriter.WriteLine(BinID + "|" + Volume + "|" + Weight + "|" + Temp + "|" + Hum + "|" + DateTime);
                }
                rd.Close();
                ms.Close();
                streamWriter.Flush();



                //DataBase db = new DataBase();

                //int sumLine = 0;
                //using (db.command = db.connection.CreateCommand())
                //{
                //    db.command.CommandText = "select * from bindata";
                //    using (db.Dr = db.command.ExecuteReader())
                //    {
                //        while (db.Dr.Read())
                //        {
                //            sumLine++;
                //            string BinID = db.Dr["BinID"].ToString();
                //            string Volume = db.Dr["Volume"].ToString();
                //            string Weight = db.Dr["Weight"].ToString();
                //            string Temp = db.Dr["Temp"].ToString();
                //            string Hum = db.Dr["Hum"].ToString();
                //            string DateTime = db.Dr["DateTime"].ToString();
                //            streamWriter.WriteLine(BinID + "|" + Volume + "|" + Weight + "|" + Temp + "|" + Hum + "|" + DateTime);
                //        }
                //        db.Dr.Close();
                //    }
                //    db.Close();
                //    streamWriter.Flush();

                //}

                //让文本框获取焦点，不过注释这行也能达到效果
                richTextBox1.Focus();
                //设置光标的位置到文本尾   
                richTextBox1.Select(richTextBox1.TextLength, 0);
                //滚动到控件光标处   
                richTextBox1.ScrollToCaret();
                richTextBox1.AppendText(System.DateTime.Now.ToString() + "\r\n导出成功！共导出数据:" + sumLine.ToString() + "\r\n\r\n");

            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败！错误信息：" + ex.Message);
            }
        }

        private ChartForm chart = new ChartForm();

        //显示图表窗体
        private void 图标显示ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread t_chart = new Thread(showchart_method);
            t_chart.Start();

        }
        
        private void showchart_method(object obj)
        {
            MethodInvoker meth = new MethodInvoker(show_chart);
            BeginInvoke(meth);
        }

        private void show_chart()
        {

            chart.Show();
            chart.Visible = true;
        }

        
        //显示日志记录框
        private void LogHisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //string path = Application.StartupPath + @"\MyConfig.INI";

            //IniWrite("title", "key", "1", path);
            //string info = IniReadValue("title", "key", path);
            //richTextBox1.AppendText(info);
            Thread t_chart = new Thread(showLogchart_method);
            t_chart.Start();

        }

        //显示日志记录框
        private void ExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {




        }

        //读取配置文件内容 
        public string IniReadValue(string section, string skey, string path)
        {
            StringBuilder temp = new StringBuilder(500);
            int i = GetPrivateProfileString(section, skey, "", temp, 500, path);
            return temp.ToString();
        }
        //写入配置文件内容 
        public void IniWrite(string section, string key, string value, string path)
        {
            WritePrivateProfileString(section, key, value, path);
        }

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string defVal, StringBuilder retVal, int size, string filePath);
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        private void showLogchart_method(object obj)
        {
            MethodInvoker meth = new MethodInvoker(show_Logchart);
            BeginInvoke(meth);
        }

        private void show_Logchart()
        {

            lhf.Show();
            lhf.Visible = true;
        }
        



        private void 打开监控界面ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread thread_monitor = new Thread(show_monitor_method);
            thread_monitor.Start();
        }

        private void 导入数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int sumLine = 0;//记录读取的行数
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "文本文档|*.txt";

            if (ofd.ShowDialog() != DialogResult.OK)
            {//如果没有选择文件直接返回
                return;
            }

            string fileName = ofd.FileName;
            try
            {
                DateTime startTime = DateTime.Now;

                StreamReader sr = new StreamReader(fileName, Encoding.Default);
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    sumLine++;
                    string[] strs = line.Split('|');
                    if (strs.Length != 6)//如果没有分隔成5个字符串，说明读取失败
                        continue;
                    for (int i = 0; i < strs.Length; i++)
                    {
                        if (strs[i] == "")
                            strs[i] = "NULL";
                    }
                    string BinID = strs[0];
                    string Volume = strs[1];
                    string Weight = strs[2];
                    string Temp = strs[3];
                    string Hum = strs[4];
                    string dateTime = strs[5];

                    string sql = "insert into bindata (BinID, Volume, Weight, Temp, Hum, DateTime)" +
                                            " values (" + BinID + ", " + Volume + ", " + Weight + ", " + Temp + ", " + Hum + ", '" + dateTime + "');";
                    MySqlConn ms = new MySqlConn();
                    int isR = ms.nonSelect(sql);
                    ms.Close();
                }
                TimeSpan ts = DateTime.Now - startTime;

                //让文本框获取焦点，不过注释这行也能达到效果
                richTextBox1.Focus();
                //设置光标的位置到文本尾   
                richTextBox1.Select(richTextBox1.TextLength, 0);
                //滚动到控件光标处   
                richTextBox1.ScrollToCaret();
                richTextBox1.AppendText(DateTime.Now.ToString() + "\r\n导入数据完成，共花费时间:" + ts.ToString() + "\r\n\r\n");
            }
            catch (Exception exc)
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                //MessageBox.Show("", "提示");
            }
        }

        private void 获取客户端版本ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializationUpdate iu = new InitializationUpdate();
            iu.NowVersion();
            //iu.DownloadCheckUpdateXml();
            //iu.LatestVersion();
            //for (int i = 0; i < checkedListBox1.Items.Count; i++)
            //{
            //    if (checkedListBox1.GetItemChecked(i))
            //    {
            //        //版本号查询
            //        string d = Data.Data(comboBox4.Text, "3", "42", "0000");//指令为0x2A。。。。转换成十进制是42
            //        sendIns_queue.Enqueue(new FacMessage(ins_num++, "2B", "3", false, 3, "查询软件版本", d, 3, s_Produce));

            //    }
            //}

            Version Version = iu.localversion;
            FileInfo finfo = new FileInfo(System.Windows.Forms.Application.StartupPath + "\\Warehouse.exe");
            //MessageBox.Show("新版本功能：");
            new Thread(new ParameterizedThreadStart(showBox)).Start("版本信息: " + iu.localversion + "\r\n\r\n更新时间: " + finfo.LastAccessTime.ToString("yyyy-MM-dd"));
            //MessageBox.Show("版本信息: " + iu.localversion + "\r\n\r\n更新时间: " + finfo.LastAccessTime.ToString("yyyy-MM-dd"));
            

        }


        private void 获取中控版本ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(checkedListBox1.Items.Count == 0) new Thread(new ParameterizedThreadStart(showBox)).Start("请选择在线料仓");
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    //版本号查询
                    string d = Data.Data(comboBox4.Text, "3", "42", "0000");//指令为0x2A。。。。转换成十进制是42
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "2B", "3", false, 3, "查询软件版本", d, 3, s_Produce));

                }
            }

        }


        private void 系统使用帮助ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //System.Diagnostics.Process.Start("C:\\Program Files (x86)\\Internet Explorer\\iexplore.exe",System.Windows.Forms.Application.StartupPath+ "\\UserDoc\\UserDoc.html"); 
            //System.Diagnostics.Process.Start("explorer.exe", System.Windows.Forms.Application.StartupPath + "\\UserDoc\\UserDoc.html");
            show(checkedListBox2.Items.Count + "daxiao ");
            for (int i = 0; i < checkedListBox2.Items.Count; i++)
            {
                //checkedListBox1.SetItemChecked(i, true);
                checkedListBox2.SetSelected(i,true);
            }

        }
        private void 服务器设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //开启打印机
            System.Data.DataTable dt = new System.Data.DataTable();
            DataRow dr;
            //设置列表头 
            foreach (DataGridViewColumn headerCell in dataGridView1.Columns)
            {
                if (headerCell.HeaderCell.Value.Equals("盘库类型"))//不加入盘库类型
                {
                    continue;
                }
                //dataGridView1.Columns.
                dt.Columns.Add(headerCell.HeaderText);
            }
            foreach (DataGridViewRow item in dataGridView1.Rows)
            {

               
                dr = dt.NewRow();
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                   
                    dr[i] = item.Cells[i].Value.ToString();
                }
                dt.Rows.Add(dr);
            }
            DataSet dy = new DataSet();
            dy.Tables.Add(dt);
            MyDLL.TakeOver(dy);

        }
        
        private void checkedListBox2_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (checkedListBox2.IndexFromPoint(
                checkedListBox2.PointToClient(System.Windows.Forms.Cursor.Position).X,
                checkedListBox2.PointToClient(System.Windows.Forms.Cursor.Position).Y) == -1)
            {
                e.NewValue = e.CurrentValue;
            }
        }

        private void checkedListBox2_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {

                if (checkedListBox2.Text.Equals(""))
                //if(checkedListBox2.CheckedItems.Count == 0)
                {
                    重试连接ToolStripMenuItem.Visible = false;
                    删除料仓ToolStripMenuItem.Visible = false;
                    显示盘库时间ToolStripMenuItem.Visible = false;
                }
                else
                {
                    重试连接ToolStripMenuItem.Visible = true;
                    删除料仓ToolStripMenuItem.Visible = true;
                    显示盘库时间ToolStripMenuItem.Visible = true;

                }
            }
        }

        private void 重试连接ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox2.CheckedItems.Count != 0)
            {
                Queue<FacMessage> ins_queue = new Queue<FacMessage>();
                for (int i = 0; i < checkedListBox2.Items.Count; i++)
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        string id = selectID(checkedListBox2.Items[i].ToString());
                        string data_search = Data.Data(comboBox4.Text, id, "00", "0000");
                        //ins_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, 3, "查询连接", data_search));
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, 3, "查询连接", data_search, s_Produce));
                    }
                } //end for
            }
            else
            {
                MessageBox.Show("请选择料仓进行重试连接", "提示");
            }
        }

        /// <summary>
        /// 关闭串口界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            groupBox_serial.Visible = false;
        }

        private void 开启回传数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            if (comboBox3.Items.Count == 0 && comboBox5.Items.Count == 0 && comboBox6.Items.Count == 0)
            {
                if (checkedListBox1.CheckedItems.Count != 1)
                {
                    MessageBox.Show("请选择一个料仓进行回传数据  " + checkedListBox1.CheckedItems.Count.ToString(), "提示");
                    return;
                }
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    if (checkedListBox1.GetItemChecked(i))
                    {
                        string data = Data.Data(comboBox4.Text, selectID(checkedListBox1.Items[i].ToString()), "38", "0001");
                        while (true)
                        {
                            Thread.Sleep(20);
                            if (list_status.Count == 0)
                            {
                                try
                                {
                                    //serialPort1.WriteLine(data);
                                    JsonMsg js = new JsonMsg("2", data);
                                    string sendMqttInfo = JsonConvert.SerializeObject(js);
                                    WriteMQ(sendMqttInfo);
                                    break;

                                }
                                catch (Exception exc) { break; }
                            }
                        }
                    }
                }
            }
            else
            {
                new Thread(new ParameterizedThreadStart(showBox)).Start("有料仓正在操作，请稍后再试");
                //MessageBox.Show(, "提示");
            }
        }

        /// <summary>
        /// 获取设备的基础信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 获取基础信息ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());

                    //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    //string d = Data.Data(comboBox4.Text, id, "10", "0000");
                    ////serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    //sendIns_queue.Enqueue(new FacMessage(ins_num++, "0B", id, false, 3, "获取料仓信息", d, s_Produce));

                    //d = Data.Data(comboBox4.Text, id, "12", "0000");
                    ////serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    //sendIns_queue.Enqueue(new FacMessage(ins_num++, "0C", id, false, 3, "获取料仓信息", d, s_Produce));

                    string d = Data.DataGetCAnangle(comboBox4.Text, id, "14", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "0F", id, false, 3, "获取k,b,c", d, s_Produce));//步进角


                    //获取查询设备类型
                    string type = Data.Data(comboBox4.Text, id, "50", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    ///MessageBox.Show("发送50指令++++++++++++++++++++=");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "33", id, false, 3, "获取设备类型", type, s_Produce));//步进角


                    //获取水平角度
                    string hengAngle = Data.Data(comboBox4.Text, id, "52", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "35", id, false, 3, "获取水平角度", hengAngle, s_Produce));//步进角


                    //获取水平初始定位角度。
                    string shuiping = Data.Data(comboBox4.Text, id, "56", "0000");//0x38
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "39", id, false, 3, "获取水平初始定位角度", shuiping, s_Produce));//步进角

                    //获取方仓边长。
                    string fangbian = Data.Data(comboBox4.Text, id, "64", "0000");//0x40
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "41", id, false, 3, "获取方仓边长", fangbian, s_Produce));//方仓边长

                    //获取方仓边宽。
                    string fangkuan = Data.Data(comboBox4.Text, id, "66", "0000");//0x40
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "41", id, false, 3, "获取方仓边宽", fangkuan, s_Produce));//方仓边宽

                    //获取方仓左边距。
                    string fangzuo = Data.Data(comboBox4.Text, id, "68", "0000");//0x40
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "41", id, false, 3, "获取方仓左边距", fangzuo, s_Produce));//方仓左边距

                    //获取上锥。
                    string shangzhui = Data.Data(comboBox4.Text, id, "70", "0000");//0x40
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "41", id, false, 3, "获取上锥", shangzhui, s_Produce));//上锥


                    richTextBox1.AppendText("发送基础信息查询指令\r\n\r\n");

                }

            }
        }

        private void 直接查询ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data = Data.Data(comboBox4.Text, id, "34", "0000");
                    Thread.Sleep(100);
                    while (true)
                    {
                        Thread.Sleep(20);
                        if (list_status.Count == 0)
                        {
                            try
                            {
                                //serialPort1.WriteLine(data);
                                //将要发送的指令,通过消息队列发送给mqtt发
                                JsonMsg js = new JsonMsg("2", data);
                                string sendMqttInfo = JsonConvert.SerializeObject(js);
                                WriteMQ(sendMqttInfo);
                                break;

                            }
                            catch (Exception exc) { break; }
                        }
                    }
                }
            }

        }

        private void toolStripTextBox2_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox2.AutoToolTip = false;
            toolStripTextBox2.ToolTipText = "范围是0到50米";
        }

        private void toolStripTextBox3_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox3.AutoToolTip = false;
            toolStripTextBox3.ToolTipText = "范围是0到100米";
        }

        private void toolStripTextBox5_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox5.AutoToolTip = false;
            toolStripTextBox5.ToolTipText = "范围是0到40米";
        }
        private void toolStripTextBox4_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "范围是0到15吨/m³";
        }
        private void toolStripTextBox10_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "边长范围是未知";
        }
        private void toolStripTextBox11_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "边宽范围是未知";
        }
        private void toolStripTextBox12_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "范围是未知";
        }
        private void toolStripTextBox13_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "上锥范围是未知";
        }
        private void toolStripTextBoxBuJv_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "范围是0到5度之间";
        }
         private void toolStripTextShuipingValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "范围是-90到90度之间";
        }



        


        private void toolStripTextSetShuipingValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox4.AutoToolTip = false;
            toolStripTextBox4.ToolTipText = "范围是-90到90度之间";
        }

        private void toolStripTextBoxKValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxKValue.AutoToolTip = false;
            toolStripTextBoxKValue.ToolTipText = "范围是0到255度之间";
        }
        //输入b值的实现
        private void toolStripTextBoxBValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxBValue.AutoToolTip = false;
            toolStripTextBoxBValue.ToolTipText = "范围是-127到127度之间";
        }

        //输入矫正值的实现
        private void toolStripTextBoxCangleValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxBValue.AutoToolTip = false;
            toolStripTextBoxBValue.ToolTipText = "范围是-90到90度之间";
        }
        //输入水平角度鼠标上移后显示的数据
        private void toolStripTextBoxHangleValue_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxBValue.AutoToolTip = false;
            toolStripTextBoxBValue.ToolTipText = "范围是0到90度之间";
        }
        //输入wendu值的实现
        private void toolStripTextBoxWenDu_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxWenDu.AutoToolTip = false;
            toolStripTextBoxWenDu.ToolTipText = "范围是0到50度之间";
        }
        //输入加热时间值的实现
        private void toolStripTextBoxHitTime_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBoxHitTime.AutoToolTip = false;
            toolStripTextBoxHitTime.ToolTipText = "范围是1到20分钟之间";
        }
        
        /// <summary>
        /// 镜头除尘模式按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 镜头除尘ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //oper_ins.Clear();
            if (checkedListBox1.CheckedItems.Count == 0)
            {
                MessageBox.Show("请选择料仓进行清洁镜头", "提示");
                return;
            }


            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                    //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    string data = Data.Data(comboBox4.Text, id, "22", "0000");
                    aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除尘", data, s_Produce));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "镜头除尘前查询状态", data_search, 3, s_Produce));
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));

                    if (ins_num > 2000)
                    {
                        ins_num = 1;
                    }
                }
            }
        }

        /// <summary>
        /// 镜头除湿模式按钮点击事件功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 镜头除湿ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //oper_ins.Clear();
            if (checkedListBox1.CheckedItems.Count == 0)
            {
                MessageBox.Show("请选择料仓进行清洁镜头", "提示");
                return;
            }
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string data_search = Data.Data(comboBox4.Text, id, "32", "0000");
                    //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                    string data = Data.Data(comboBox4.Text, id, "22", "0001");
                    aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除湿", data, s_Produce));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "镜头除湿前查询状态", data_search, 3, s_Produce));
                    //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));

                }
            }
        }

        private void 删除料仓ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (checkedListBox2.CheckedItems.Count != 0)
            {
                int del = 0;
                MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                DialogResult dr = MessageBox.Show("确认要删除" + checkedListBox2.CheckedItems.Count + "个料仓吗？", "提示", messButton);
                if (dr == DialogResult.OK)
                {
                    int d = 0;
                    for (int i = checkedListBox2.Items.Count - 1; i >= 0; i--)
                    {
                        if (checkedListBox2.GetItemChecked(i))
                        {
                            d = delete(checkedListBox2.GetItemText(checkedListBox2.Items[i].ToString()));
                            del++;
                            Invoke(new MethodInvoker(delegate()
                            {
                                //让文本框获取焦点，不过注释这行也能达到效果
                                richTextBox1.Focus();
                                //设置光标的位置到文本尾   
                                richTextBox1.Select(richTextBox1.TextLength, 0);
                                //滚动到控件光标处   
                                richTextBox1.ScrollToCaret();
                                richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n删除了料仓" + checkedListBox2.GetItemText(checkedListBox2.Items[i].ToString()) + "\r\n\r\n");
                                if (d != 0)
                                {
                                    checkedListBox2.Items.RemoveAt(i);
                                }
                            }));
                        }
                    }
                    if (del != 0)
                    {
                        //Thread a = new Thread(display);//启动时显示粮仓线程
                        //a.Start();
                        //让文本框获取焦点，不过注释这行也能达到效果
                        richTextBox1.Focus();
                        //设置光标的位置到文本尾   
                        richTextBox1.Select(richTextBox1.TextLength, 0);
                        //滚动到控件光标处   
                        richTextBox1.ScrollToCaret();
                        richTextBox1.AppendText(DateTime.Now.ToString() + "\r\n成功删除" + del + "个料仓\r\n\r\n");
                    }
                    else
                    {
                        new Thread(new ParameterizedThreadStart(showBox)).Start("存在数据表删除失败，请检查数据库");
                        //MessageBox.Show(, "提示");
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择料仓进行删除", "提示");
            }

        }

        private void 当前数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            button3.PerformClick();
        }

        private void 历史数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button4.PerformClick();
        }

        /// <summary>
        /// 定时器，盘库时定时查询状态，来确定盘库是否完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer2_Tick(object sender, EventArgs e)
        {
            new Thread(timer2_on).Start();
        }

        private void timer2_on(object obj)
        {
            if (send_ins.Count == 0)
                return;

            for (int i = send_ins.Count - 1; i >= 0; i--)
            {
                send_ins[i].life_time--;
                if (send_ins[i].life_time <= 0)
                {//如果到达20分钟，则盘库超时
                    string name = getName(send_ins[i].fac_num);
                    comboBox3.Items.Remove(name);
                    for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                    {
                        if (send_ins[i].fac_num.PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                        {
                            CalcVol_list.RemoveAt(it);
                            break;
                        }
                    }
                    string id = selectID(name);
                    // 如果盘库失败 取消料仓操作
                    string data = Data.Data(comboBox4.Text, id, "30", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "1F", id, false, TIME, "取消当前操作", data, 2, s_Produce));

                    string searchTemp = Data.Data(comboBox4.Text, id, "16", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "11", id, false, TIME+5, "查询温湿度", data));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", searchTemp, s_Produce));

                    send_ins.RemoveAt(i);
                    if (comboBox3.Items.Count == 0)
                    {
                        comboBox3.Visible = false;
                        label3.Visible = false;
                        //groupBox2.Visible = false;
                        progressBar2.Value = 0;
                        label19.Text = "0";
                    }
                    else
                        comboBox3.Text = comboBox3.Items[0].ToString();
                    //让文本框获取焦点，不过注释这行也能达到效果
                    richTextBox1.Focus();
                    //设置光标的位置到文本尾   
                    richTextBox1.Select(richTextBox1.TextLength, 0);
                    //滚动到控件光标处   
                    richTextBox1.ScrollToCaret();
                    richTextBox1.AppendText(DateTime.Now.ToString() + "\r\n" + name + "  盘库超时\r\n\r\n");

                    DateTime now = DateTime.Now;
                    string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

                    //超时将体积 重量设置为-1， -1存数据表

                    //string sql = "insert into [bindata] (BinID, Volume, Weight, DateTime) values(" + id + ", -1, -1, '" + time + "')";
                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //if (db.command.ExecuteNonQuery() > 0)
                    //{
                    //    new Thread(new ParameterizedThreadStart(showBox)).Start(name + "  盘库超时");
                    //    //让文本框获取焦点，不过注释这行也能达到效果
                    //    richTextBox1.Focus();
                    //    //设置光标的位置到文本尾   
                    //    richTextBox1.Select(richTextBox1.TextLength, 0);
                    //    //滚动到控件光标处   
                    //    richTextBox1.ScrollToCaret();
                    //    richTextBox1.AppendText(DateTime.Now.ToString() + "\r\n" + name + "  盘库超时\r\n");
            
                    //    //MessageBox.Show(, "提示");
                    //}
                    //db.Close();

                    //超时信息存日志表
                    //DataBase dbLog = new DataBase();
                    string sql = "insert into binlog values('" + id + "', '盘库超时', '" + "TimeOut" + "', '盘库超时', '" + time + "')";
                    MySqlConn ms = new MySqlConn();
                    int rd = ms.nonSelect(sql);
                    ms.Close();
                    //dbLog.command.CommandText = sql;
                    //dbLog.command.Connection = dbLog.connection;
                    //dbLog.command.ExecuteNonQuery();
                    //dbLog.Close();
                }
                else
                {//如果没有到达盘库超时时间，则发送查询指令
                    //string data = Data.Data(comboBox4.Text, send_ins[i].fac_num, "34", "0000");
                    ////MessageBox.Show(d);
                    //aim_ins.Enqueue(new FacMessage(ins_num++, "23", send_ins[i].fac_num, false, TIME_WAIT, "查询结果", data, s_Produce));

                    //string data_search = Data.Data(comboBox4.Text, send_ins[i].fac_num, "32", "0000");
                    ////向发送链表中添加此指令，但是不发送这条指令，发送的是查询状态指令
                    //sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", send_ins[i].fac_num, false, TIME_WAIT, "获取盘库结果前查询状态", data_search, s_Produce));
                    
                    if (ins_num > 2000)
                    {
                        ins_num = 0;
                    }
                }

             

            }
        }

        private void toolStripTextBox1_MouseMove(object sender, MouseEventArgs e)
        {
            toolStripTextBox1.AutoToolTip = false;
            toolStripTextBox1.ToolTipText = "输入料仓编号";
        }

        /// <summary>
        /// 清洁镜头定时器功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void clean_timer_Tick(object sender, EventArgs e)
        {
            if (comboBox5.Items.Count == 0)
            {
                try
                {
                    label33.Visible = false;
                    comboBox5.Visible = false;

                }
                catch (Exception exc) { }
            }
            else
            {
                label33.Visible = true;
                comboBox5.Visible = true;


                if (clean_list.Count != 0)
                {
                    for (int i = clean_list.Count - 1; i >= 0; i--)
                    {
                        clean_list[i].life_time--;

                        //richTextBox1.AppendText(clean_list[i].fac_name + "  " + clean_list[i].life_time + "\r\n");
                        if (clean_list[i].life_time <= 0)
                        {
                            try
                            {
                                comboBox5.Items.Remove(clean_list[i].fac_name);
                                for (int j = CalcVol_list.Count - 1; j >= 0; j--)
                                {
                                    if (CalcVol_list[j].fac_num.PadLeft(2, '0').Equals(clean_list[i].fac_name.PadLeft(2, '0')))
                                    {
                                        CalcVol_list.RemoveAt(j);
                                        break;
                                    }
                                }
                                clean_list.RemoveAt(i);
                            }
                            catch (Exception exc) { }
                        }
                    }
                }
            }

        }

        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Selected)
                {
                    try
                    {
                        string sql = "delete from binauto where BinID = " + dataGridView1.Rows[i].Cells["BinID"].Value
                           + " AND Time = '" + dataGridView1.Rows[i].Cells["Time"].Value + "'";
                        MySqlConn ms = new MySqlConn();
                        int isR = ms.nonSelect(sql);
                        ms.Close();
                        //DataBase db = new DataBase();
                        //db.command.Connection = db.connection;
                       
                        //db.command.CommandText = sql;
                        //db.command.ExecuteNonQuery();

                        //db.Close();
                    }
                    catch (Exception exc)
                    {
                        new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置,delete操作");

                        //MessageBox.Show("", "提示");
                    }
                }
            }
            Thread loadAuto = new Thread(LoadAuto);
            loadAuto.Start();
            显示盘库时间ToolStripMenuItem.PerformClick();
        }
        //清空
        private void button7_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = "";
            //for (int i = 0; i < checkedListBox1.Items.Count; i++)
            //{
            //    if (checkedListBox1.GetItemChecked(i))
            //    {
            //        string id = selectID(checkedListBox1.Items[i].ToString());
            //        string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0201");//2为实时查询温度。
            //        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n查询测量设备温度\r\n\r\n "+ data);
            //        sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内温度", data,3, s_Produce));//指令为返回的代码


            //    }//判断是否被选中
            //}//循环


        }

        private void 料位图形显示ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread thread_form1 = new Thread(InvokeShowForm1);
            thread_form1.Start();
        }

        /// <summary>
        /// 料位图形显示
        /// </summary>
        /// <param name="obj"></param>
        private void InvokeShowForm1(object obj)
        {
            MethodInvoker MethInvo = new MethodInvoker(show_form1);
            BeginInvoke(MethInvo);
        }

        private void show_form1()
        {
            Form1 form1 = new Form1();
            form1.Show();
        }

        /// <summary>
        /// 显示盘库进度的定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void show_timer_Tick(object sender, EventArgs e)
        {
            if (CalcVol_list.Count != 0)
            {
                if (it_oper >= CalcVol_list.Count)
                {//CalcVol_list的下标it_oper如果大于CalcVol_list的节点个数，就置为0
                    it_oper = 0;
                }
                groupBox2.Visible = true;
                //if (CalcVol_list[it_oper].ins_num == 2)
                //{
                //    CalcVol_list[it_oper].life_time += 3;
                //}
                for (int i = 0; i < CalcVol_list.Count; i++)
                {//遍历找出清洁镜头的节点
                    if (CalcVol_list[i].ins_num == 2)
                        CalcVol_list[i].life_time +=  2;//进度
                }
                string fac_num = CalcVol_list[it_oper].fac_num;
                int schedule = CalcVol_list[it_oper].life_time;//操作进度
                int OperType = CalcVol_list[it_oper].ins_num;//操作类型，1表示盘库，2表示清洁镜头
                label6.Text = getName(fac_num);
                if (schedule >= 100)
                {
                    schedule = 100;
                    CalcVol_list.RemoveAt(it_oper);
                }
                progressBar2.Value = schedule;
                label19.Text = schedule.ToString();
                if (1 == OperType)
                {
                    label22.Text = "料仓盘库";
                }
                else if (2 == OperType)
                {
                    label22.Text = "清洁镜头";
                }
                it_oper++;
            }
            else
            {
                it_oper = 0;
                groupBox2.Visible = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Show();
                //this.ShowInTaskbar = true;  //显示在系统任务栏
                this.WindowState = FormWindowState.Maximized;  //还原窗体
                notifyIcon1.Visible = false;  //托盘图标隐藏
            }
        }

        private void pC管理软件升级ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new Thread(ConnectServer).Start();
        }
        /// <summary>
        /// 检查更新
        /// </summary>
        private void ConnectServer()
        {
            //检查更新
            InitializationUpdate iu = new InitializationUpdate();
            iu.NowVersion();
            iu.DownloadCheckUpdateXml();
            iu.LatestVersion();

            //MessageBox.Show("新版本功能：");
            if (iu.latesversion != iu.localversion)
            {
                Process.Start(System.Windows.Forms.Application.StartupPath + "\\Update.exe");
            }
            else
            {
                MessageBox.Show("已经是最新版本，不需要更新");
            }
        }

        /// <summary>
        /// 检测料仓在线的定时器功能实现
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnlineTimer_Tick(object sender, EventArgs e)
        {
            
            OnlineCheak();
        }

        private void OnlineCheak()
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                string id = selectID(checkedListBox1.Items[i].ToString());
                string data = Data.Data(comboBox4.Text, id, "00", "0000");
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data, s_Produce));
            }
            for (int i = 0; i < checkedListBox2.Items.Count; i++)
            {
                string id = selectID(checkedListBox2.Items[i].ToString());
                string data = Data.Data(comboBox4.Text, id, "00", "0000");
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data, s_Produce));
            }
        }

        private void 重启全套设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = checkedListBox1.Items.Count - 1; i >= 0; i--)
            {
                //bool isoperating = false;
                if (checkedListBox1.GetItemChecked(i))
                {
                    //MessageBox.Show(checkedListBox1.Items[i].ToString());
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string d = Data.Data(comboBox4.Text, id, "20", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "15", id, false, 3, "重启全套设备", d, 3, s_Produce));

                }
            } //end for
        }
        private void 重启中控设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {

            for (int i = checkedListBox1.Items.Count - 1; i >= 0; i--)
            {
                //bool isoperating = false;
                if (checkedListBox1.GetItemChecked(i))
                {
                    //MessageBox.Show(checkedListBox1.Items[i].ToString());
                    string id = selectID(checkedListBox1.Items[i].ToString());
                    string d = Data.Data(comboBox4.Text, id, "24", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "19", id, false, 3, "重启中控设备", d, 3, s_Produce));

                }
            } //end for
        }

        private string isRe(int equip,string weight)
        {
            //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据

            try
            {
                string sql = "select Weight,DateTime from bindata where BinID = " + equip + " order by DateTime desc";
                float LastWeight = 0;
                string LastTime = "";
                //dbLastData.command.CommandText = sql;
                //dbLastData.command.Connection = dbLastData.connection;
                //dbLastData.Dr = dbLastData.command.ExecuteReader();
                MySqlConn msc2 = new MySqlConn();
                MySqlDataReader rd = msc2.getDataFromTable(sql);
                int havedata = 0;//检查是否是新表
                while (rd.Read())
                {
                    if (rd["Weight"].ToString().Equals("") == false)
                    {//获取到第一个重量不为NULL的值，记录为最新数据,并且跳出循环
                        LastWeight = float.Parse(rd["Weight"].ToString());
                        LastTime = rd["DateTime"].ToString();
                        havedata = 1;//有数据说明不是新表
                        break;
                    }
                }

                if (havedata == 0)
                {
                    return "ok";//是新表直接跳出
                }
                rd.Close();
                msc2.Close();

                DateTime a = Convert.ToDateTime(LastTime);
                string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                DateTime b = Convert.ToDateTime(time);

                TimeSpan ts = b - a;//datetime做差
                int cha = (int)ts.TotalSeconds;
                float wei_f = float.Parse(weight);
                if (LastWeight == wei_f && cha < 120)//如果新输入的和保存在数据库中的数据相等且时间差小于两分钟，这说明是重复输入，不能输入
                {
                    return "error";
                }
                else
                {
                    return "ok";
                }
            }catch(Exception ee)
            {
                MessageBox.Show("isRe == " + ee.ToString());
                return "ok";
            }
           
        }
        private string isReWendu(int equip, string temp)
        {
            //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据
            int havedata = 0;//检查是否是新表
            string sql = "select Temp,DateTime from bindata where BinID = " + equip + " order by DateTime desc";
            float LastWeight = 0;
            string LastTime = "";
            //dbLastData.command.CommandText = sql;
            //dbLastData.command.Connection = dbLastData.connection;
            //dbLastData.Dr = dbLastData.command.ExecuteReader();
            try
            {
                MySqlConn msc2 = new MySqlConn();
                MySqlDataReader rd = msc2.getDataFromTable(sql);
                while (rd.Read())
                {
                    if (rd["Temp"].ToString().Equals("") == false)
                    {//获取到第一个重量不为NULL的值，记录为最新数据,并且跳出循环
                        LastWeight = float.Parse(rd["Temp"].ToString());
                        LastTime = rd["DateTime"].ToString();
                        havedata = 1;//有数据说明不是新表
                        break;
                    }
                }
                if (havedata == 0)
                {
                    return "ok";//是新表直接跳出
                }
                rd.Close();
                msc2.Close();
            }catch(Exception ee)
            {
                richTextBox1.AppendText("查询是否有重复温度出错：" + ee.ToString());
            }
           

            DateTime a = Convert.ToDateTime(LastTime);
            string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            DateTime b = Convert.ToDateTime(time);

            TimeSpan ts = b - a;//datetime做差
            int cha = (int)ts.TotalSeconds;
            float wei_f = float.Parse(temp);
            if (LastWeight == wei_f && cha < 120)//如果新输入的和保存在数据库中的数据相等且时间差小于两分钟，这说明是重复输入，不能输入
            {
                return "error";
            }
            else
            {
                return "ok";
            }
        }

     
        public void getWenDo(object obj)
        {
            string id = (string)obj;
            while (isRequireWenDu)
            {
                Thread.Sleep(10000);
                string data = Data.DataWenDuAndShiJian(comboBox4.Text, id, "44", "0202");//2为实时查询温度。 02 为查询实时温度
                //show("发送的指令：" + data + "\r\n\r\n");
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "2D", id, false, TIME, "查询仓内实时温度", data, s_Produce));//指令为返回的代码

            }
        }

        public bool IsGroupNameRe(string newName)
        {
            try
            {
                string sqlGroup = "select * from groupinfo where gstate = '1' and gname = '" + newName + "'";
                int i = 0;
                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sqlGroup);
                while (rdGroup.Read())
                {
                    i++;
                }
                rdGroup.Close();
                msc1.Close();
                if (i > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }




                //2、根据获取的combobox第一个Gid对料仓进行查询,显示列表
            }
            catch (Exception ee)
            {
                show("验证组名是否重复报错。 错误：" + ee.ToString());
                return true;
            }
        }
        public void getGroupListByAdd()
        {
            try
            {
                comboBoxGroup.Items.Clear();

                //添加料仓设备
                //1、查询分组，将分组信息添加到comboboxGroup中
                string sqlGroup = "select * from groupinfo where gstate = '1' order by gid asc";

                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sqlGroup);

                while (rdGroup.Read())
                {
                    comboBoxGroup.Items.Add(rdGroup["Gname"].ToString());
                }
                rdGroup.Close();
                msc1.Close();


                if (comboBoxGroup.Items.Count > 0)
                {
                    comboBoxGroup.Text = comboBoxGroup.Items[0].ToString();
                }



            }
            catch (Exception ee)
            {
                show("查询料仓分组报错。 错误：" + ee.ToString());
            }
        }

        public void getGroupList()
        {
            try
            {
                comboBoxGroup.Items.Clear();

                //添加料仓设备
                //1、查询分组，将分组信息添加到comboboxGroup中
                string sqlGroup = "select * from groupinfo where gstate = '1'";

                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sqlGroup);

                while (rdGroup.Read())
                {
                    comboBoxGroup.Items.Add(rdGroup["Gname"].ToString());
                }
                rdGroup.Close();
                msc1.Close();


                if (comboBoxGroup.Items.Count > 0)
                {
                    comboBoxGroup.Text = comboBoxGroup.Items[0].ToString();
                }


                //2、根据获取的combobox第一个Gid对料仓进行查询,显示列表

            }
            catch (Exception ee)
            {
                show("查询料仓分组报错。 错误：" + ee.ToString());
            }
        }



        public void show(string text)
        {
            richTextBox1.AppendText(DateTime.Now.ToString("G") +"\r\n"+text +"\r\n");
        }

        private string getGname(string id)//根据要上线的设备id获取他属于哪一个组
        {
            string gname = "";
            string sql = "select Gname from groupinfo where gid = (select Gid from bininfo where BinID = " + id + ")";
            MySqlConn ms = new MySqlConn();
            MySqlDataReader rdr = ms.getDataFromTable(sql);//直接对象引用

            while (rdr.Read())
            {
                gname = rdr["Gname"].ToString();
            }
            rdr.Close();
            ms.Close();
            return gname;
        }
        /// <summary>
        /// 选择分组之后的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void selectGroup(object sender, EventArgs e)
        {
            checkedListBox1.Items.Clear();
            checkedListBox2.Items.Clear();

            if (port_isopen == 1)//串口正常时进行查询后，查看在线状态
            {
                try
                {
                    string groupname = comboBoxGroup.Text;
                    string sql = "select * from bininfo where Gid = (select Gid from groupinfo where Gname = '" + groupname + "' and Gstate = 1)";
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rdr = ms.getDataFromTable(sql);//直接对象引用

                    while (rdr.Read())
                    {
                        Thread.Sleep(20);
                        checkedListBox2.Items.Remove(rdr["BinName"].ToString());
                        checkedListBox2.Items.Add(rdr["BinName"].ToString());
                    }
                    SortCheckedList(checkedListBox2);

                    rdr.Close();
                    ms.Close();
                }
                catch (Exception exc)
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置!!!!");

                }
                OnlineCheak();
            }
            else//串口不正常时不进行操作
            {
                try
                {
                    string groupname = comboBoxGroup.Text;
                    string sql = "select * from bininfo where Gid = (select Gid from groupinfo where Gname = '" + groupname + "')";
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rdr = ms.getDataFromTable(sql);//直接对象引用

                    while (rdr.Read())
                    {
                        Thread.Sleep(20);
                        checkedListBox2.Items.Remove(rdr["BinName"].ToString());
                        checkedListBox2.Items.Add(rdr["BinName"].ToString());
                    }
                    SortCheckedList(checkedListBox2);

                    rdr.Close();
                    ms.Close();
                }
                catch (Exception exc)
                {
                    new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");

                }
            }

        }



        void Log(string str)    // 记录服务启动  
        {
            file_mutex.WaitOne();
            {
                string info = string.Format("{0}", str);
                string path = "C://Mqtt//MyshowW.txt";//"C://Mqtt//Myshow.txt"
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(info);
                    //关闭
                    sw.Close();
                }
            }
            file_mutex.ReleaseMutex();

        }

    }
}
