using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Messaging;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Net.NetworkInformation;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Headers;
using MySql.Data.MySqlClient;
//Mysql版本！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
namespace MyTest
{
    public partial class Service1 : ServiceBase
    {
        private static string MQTT_BROKER_ADDRESS = "120.27.63.249";
        // create client instance 
        MqttClient client ;

        private CircularQueue<string> cirQueue = new CircularQueue<string>(3000);//定义接收机器数据的串口队列
        private int port_isopen = 0;//端口是否打开
        private int flag_threadout = 1;//退出登录时，将这个标识改为0，所有的线程将阻塞
        private int SqlConnect = 0;//是否连接数据库的标识符
        List<string> binlist = new List<string>();//测试

        List<Rebinlist> sendbinlist = new List<Rebinlist>();//用于存放获取到的数据库的料仓
        List<Rebinlist> sendPklist = new List<Rebinlist>();//用于存放获取到的数据库的料仓

        List<Rebinlist> clean_list = new List<Rebinlist>();//用于存放哪些料仓正在清洁镜头


        List<OffLineBinList> offlinebinlist = new List<OffLineBinList>();//用于当串口不可用的时候，来显示显示列表
        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        public TransCoding Data = new TransCoding();
        private string factory = "";
        private string mqCode = "";//厂区码，带有地区编码的

        //指令发送队列在SendIns函数中，会循环遍历发送队列sendIns_queue，
        //只要不为空就从队列中取指令发送，因此在需要发送指令的部分只需要
        //    将指令封装成FacMessage类型，再放到sendIns_queue中即可。
        //发送指令队列，数据结构是队列，作用：所有的需要发送的指令全部放到这个队列中统一处理
        private Queue<FacMessage> sendIns_queue = new Queue<FacMessage>();
        private int ins_num = 1;//用于记录指令状态的指令序号
        public static int TIME = 2;//向状态表中添加的时间
        private static int TIME_WAIT = 2;//半双工等待时间
        private static int s_Produce = 3600;//时间戳时间
                                            //目的指令缓冲区
        private Queue<FacMessage> aim_ins = new Queue<FacMessage>();//将查询指令的目的指令放入这个缓冲区中，作用：暂存盘库、清洁镜头、监控指令。发送完查询指令后将目的指令放入oper_ins中
        private int port_mask = 0;//屏蔽串口
        private Queue<FacMessage> oper_ins = new Queue<FacMessage>();//目的指令队列


        private System.Timers.Timer t_status;//用于遍历状态链表的定时器
        private List<FacMessage> list_status = new List<FacMessage>();//将回应信息加入到这个链表中
        //锁！
        private Mutex list_mutex = new Mutex();//用于更改链表时进行异步操作的互斥锁
        private List<FacMessage> NoAckList = new List<FacMessage>();
        //记录未接收到的指令次数,其中的节点存两个信息，料仓编号和未接收到指令的次数

        private int it_oper = 0;//盘库列表的索引
        private List<FacMessage> CalcVol_list = new List<FacMessage>();//正在盘库链表
        private List<FacMessage> send_ins = new List<FacMessage>();//盘库时，每30秒发送一次查询状态函数，这个链表标记哪个料仓需要查询

        private List<UserDO> ud = new List<UserDO>();
        private string adddress = "";//记录查询指令是否接收到
        private string JID = "0";//用于回传垂直校准ID
        private string TOPIC = "";//订阅主题

        //料仓查询计数器
        int allBinNum = 0;
        //盘库个数计数器
        int PanKuNum = 0;
        private int chuankou = 0;//判断串口是否连接正常

        System.Timers.Timer timerPk;
        System.Timers.Timer timerSendCheckState;
#pragma warning disable CS0169 // 从不使用字段“Service1.connectMqtt”
        System.Timers.Timer connectMqtt;
#pragma warning restore CS0169 // 从不使用字段“Service1.connectMqtt”
        //消息队列
        MessageQueue mq;//客户端发给服务端
        MessageQueue mq1;//服务端发送给客户端

        //判断网络环境
        Ping pingSender = new Ping();
        PingReply reply = null;

        Ping pingSender1 = new Ping();
        PingReply reply1 = null;

        //是否要重新连接mqtt
        Boolean toConnectMqtt = false;
        int isReConnect = 0;

        int calcVolChange = 0;
#pragma warning disable CS0414 // 字段“Service1.MqTestConnectNum”已被赋值，但从未使用过它的值
        int MqTestConnectNum = 4;
#pragma warning restore CS0414 // 字段“Service1.MqTestConnectNum”已被赋值，但从未使用过它的值

        //回传数据
        //private BackData[] backdata = new BackData[200];//回传数据
        private int recv_num = 0;//接收到回传数据的个数

        private string backLengthData = "";//回传的数据是
        private string backAnData = "";//回传的角度

        private string backAllInfo = "";

        private int back_complet = -1;//是否回传完成
        private string binPking = "";//要回传的数据i
#pragma warning disable CS0414 // 字段“Service1.isPk”已被赋值，但从未使用过它的值
        private int isPk = 0;//是否 有在线料仓在进行盘库操作
#pragma warning restore CS0414 // 字段“Service1.isPk”已被赋值，但从未使用过它的值
        private List<eqIdDo> binPkingList = new List<eqIdDo>();

        //用于同步代码块。一块代码。只能有一个线程去执行
        static Object locker = new Object();
#pragma warning disable CS0414 // 字段“Service1.reBackNum”已被赋值，但从未使用过它的值
        private int reBackNum = 2;//当回传的点不全的时候，需要多回传2次
#pragma warning restore CS0414 // 字段“Service1.reBackNum”已被赋值，但从未使用过它的值
        private int pktime = 0;
#pragma warning disable CS0414 // 字段“Service1.wdAndBack”已被赋值，但从未使用过它的值
        private int wdAndBack = 0;//判断回传温度是需要回传温度和回传数据，还是只是回传温度
#pragma warning restore CS0414 // 字段“Service1.wdAndBack”已被赋值，但从未使用过它的值
#pragma warning disable CS0414 // 字段“Service1.wdAndBackNum”已被赋值，但从未使用过它的值
        private int wdAndBackNum = 3;//用于控制变量在最少5s之后，才改变参数，让回传数据可以有时间回传
#pragma warning restore CS0414 // 字段“Service1.wdAndBackNum”已被赋值，但从未使用过它的值


        //用于同步代码块。一块代码。只能有一个线程去执行
        static Object loglocker = new Object();

        private Mutex file_mutex = new Mutex();//文件互斥锁
        private Mutex recnum_mutex = new Mutex();//rec_num互斥锁
        private Mutex connectNet_mutex = new Mutex();//网络连接互斥锁


        //保存温度
        private string saveBinID = "";//当前正在盘库的id，通过id判断接收到的温湿度是否可以保存
        private string saveTemp = "";
        private string saveHum = "";
        private int canBackTimes = 1;//允许点传不会来之后，可以重传的次数


        //mysql添加
        MySqlConn msc1 = new MySqlConn();





        //是否被取消盘库
        //private Dictionary<int, int> isStop = new Dictionary<int, int>();

        public  bool testIsHaveInternet()
        {
            try
            {
                Log("进行网络检查！！！");

               
                    reply1 = null;
                    reply1 = pingSender1.Send(MQTT_BROKER_ADDRESS);//ping服务器网址
                    if (reply1 == null || (reply1 != null && reply1.Status != IPStatus.Success))
                    {
                        Log("新---网络连接不正常！！！！！，设置isReConnect = 1");
                        isReConnect = 1;//网络连接不正常是为1、、、、连接正常时是0
                        return false;
                    }
                    else if (reply1.Status == IPStatus.Success)
                    {
                        Log("新---网络连接正常");
                        return true;
                    }
                    else
                    {
                        Log("新222---网络连接不正常！！！！！，设置isReConnect = 1");
                         isReConnect = 1;
                    return false;
                    }

              
               




                //HttpClient client = new HttpClient();
                //client.BaseAddress = new Uri("http://www.baidu.com");// + MQTT_BROKER_ADDRESS
                //                                                     // Add an Accept header for JSON format.
                //                                                     // 为JSON格式添加一个Accept报头
                //client.DefaultRequestHeaders.Accept.Add(
                //    new MediaTypeWithQualityHeaderValue("application/json"));
                //HttpResponseMessage response = client.GetAsync("api/products").Result;  // Blocking call（阻塞调用）! 
                //if (response.IsSuccessStatusCode)
                //{
                //    Log("网络连接正常");
                //    return true;
                //}
                //else
                //{
                //    Log("网络连接不正常！！！！！");
                //    return false;
                //}

            }catch(Exception e)
            {
                Log("网络连接不正常！！！！，设置isReConnect = 1" + e.ToString());
                isReConnect = 1;
                return false;
            }
            finally
            {
            }
           
        }
        //ip地址的读取！！！！！！！！！！
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


        public Service1()
        {
            InitializeComponent();
            
        }
        /// 服务启动  
        protected override void OnStart(string[] args)
        {
            try
            {
                Log("程序启动了。");    //将字符串 string 写入文件  
                                  // 其他功能  

                //读取ip地址
                string ippath = "C://Mqtt" + @"\IpConfig.txt";
                if (File.Exists(ippath) == false)//创建保存串口信息的文件
                    File.Create(ippath).Close();
                StreamReader sr = new StreamReader(ippath, Encoding.Default);
                String line = sr.ReadLine();

                if (line == null)
                {
                    Log("IPIPIPIPIPIP无信息,读取默认的ip地址");
                    //生成mqttClient
#pragma warning disable CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                    client = new MqttClient(System.Net.IPAddress.Parse(MQTT_BROKER_ADDRESS));
#pragma warning restore CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”

                }
                else
                {
                    Log("IPIPIPIPIPIP地址：" + line);
                    //生成mqttClient
#pragma warning disable CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                    client = new MqttClient(System.Net.IPAddress.Parse(line));
#pragma warning restore CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”

                }
                //关闭文件输入流
                sr.Close();




                string path1 = ".\\Private$\\MSMQDemo1";
                if (MessageQueue.Exists(path1))
                {
                    mq1 = new MessageQueue(path1);
                    mq1.Purge();
                    Log("已经有了列表1。");
                }
                else
                {
                    Log("新建列表1。");
                    mq1 = MessageQueue.Create(path1);
                }

                //新建消息循环队列或连接到已有的消息队列，用于接收
                string path = ".\\Private$\\MSMQDemo";
                if (MessageQueue.Exists(path))
                {
                    mq = new MessageQueue(path);
                    mq.Purge();
                    Log("已经有了列表。");

                }
                else
                {
                    Log("新建列表。");
                    mq = MessageQueue.Create(path);
                }

                Thread recMsg = new Thread(RecMsg);//接收win客户端指令线程

                recMsg.Start(mq);


                Log("查询数据库连接");
                int num = 0;
                while (num < 60)//进行数据库服务是否开启的判断
                {
                    if (JudgeDBServerStatus())
                    {

                        break;
                    }
                    Thread.Sleep(20000);
                    num++;
                    Log("二十秒钟过了:" + num);
                }
                if (num >= 60)
                {
                    Log("连接数据库超时，请检查数据库");
                    OnStop();
                    return;//结束
                }


                //检查表是否是全的
                Log("查询数据库表是否是全的");
                string contosql = conTosql();
                if (contosql.Equals("1"))
                {//检测到数据库中的表已经创建齐全
                    Log("数据库中表已经存在成功");
                    SqlConnect = 1;
                }
                else if (contosql.Equals("0"))
                {
                    Log("数据库检验表是否 是全的时出错");
                }
                else
                {//创建未创建的数据表
                    Log("新创建的数据表" + contosql.ToString());
                    SqlConnect = 1;
                }
                

                if (isCompleteTable() == 8)
                {
                    Log("字段补全/检查正常");
                }
                else
                {
                    Log("字段补全出错");
                }

               

                
                //如果数据库都正常，就
                if (SqlConnect == 1)
                {
                    //读取串口
                    loadPort();


                    Thread checkInt = new Thread(connectMqt);//开启网络连接线程
                    checkInt.Start();

                    Thread sendins_thread = new Thread(SendIns);//发送指令线程
                    sendins_thread.Start();

                    t_status = new System.Timers.Timer(1000);//状态链表使用的定时器，判断是否超时
                    t_status.Elapsed += new System.Timers.ElapsedEventHandler(getStatus);//每隔1s执行函数
                    t_status.AutoReset = true;
                    t_status.Enabled = true;
                    //、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、

                    timerSendCheckState = new System.Timers.Timer();
                    timerSendCheckState.Interval = 30000;//三十秒
                    timerSendCheckState.Elapsed += new System.Timers.ElapsedEventHandler(checkBinStateTheah);//查询盘库状态的指令
                    timerSendCheckState.Enabled = true;
                    timerSendCheckState.Start();

                    //connectMqtt = new System.Timers.Timer();
                    //connectMqtt.Interval = 30000;//三天循环一次60000*60*24*3
                    //connectMqtt.Elapsed += new System.Timers.ElapsedEventHandler(connectMqt);//检查网络情况
                    //connectMqtt.Enabled = true;
                    //connectMqtt.Start();

                   

                    ////用于发送盘库、清洁镜头进度给mqtt用户的方式
                    timerPk = new System.Timers.Timer();
                    timerPk.Interval = 8000;//3s
                    timerPk.Elapsed += new System.Timers.ElapsedEventHandler((s, e) => timer1_Elapsed(s, e));
                    timerPk.Enabled = true;
                    timerPk.Start();

                    //开启mqtt连接
                    MQTTtest();
                }
            }
            catch(Exception ee)
            {
                Log("程序启动出错：" + ee.ToString());
            }
            

        }
        private void MQTTtest()
        {
                Log("连接mqtt");
                if (testIsHaveInternet())
                {
                    Log("网络连接成功，可以进行mqtt连接");
                    try
                    {
                        //获取地区码
                        string sql = "select * from mqttcode where MqttCode = '1 '";
                        Log("数据库查询地区码");
                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            //Invoke(new MethodInvoker(delegate
                            //{f
                            //    //comboBox4.Items.Add(db.Dr["FactoryID"].ToString());//提取出厂区id
                            //    richTextBox1.AppendText(db.Dr["Code"].ToString() + "\r\n");
                            mqCode = rd["Code"].ToString();
                            //}));

                        }
                        rd.Close();
                    ms.Close();
                    //https://blog.csdn.net/ufocode/article/details/78985940    开发文档
                    //当接收到关注的消息后，该事件会被调用
                    client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

                    //当推送消息成功后，该事件会被调用
                    client.MqttMsgPublished += MqttMsgPublished;

                    //当连接断开后，该事件会被调用
                    client.ConnectionClosed += ConnectionClosed;

                    //关注话题成功后，该事件会被调用
                    client.MqttMsgSubscribed += MqttMsgSubscribed;

                    //取消关注成功后，该事件会被调用
                    client.MqttMsgUnsubscribed += MqttMsgUnsubscribed;




                    //todo 厂区编码
                    ////生成客户端ID并连接服务器  
                    string clientId = Guid.NewGuid().ToString();//mqCode.ToString()
                        //Log("连接connect1");
                        client.Connect(mqCode);
                        //Log("连接connect2");

                        Log("获取MySql的地区码是：" + mqCode);
                        string[] topic = { "win/" + mqCode };
                        byte[] qosLevels = { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE };
                        //订阅自己的号
                        int message_id = client.Subscribe(topic, qosLevels);
                        Log("订阅的topic:win/" + mqCode);
                        Log("message_id:" + message_id.ToString());




                        //string[] topic1 = { "win/123132" };
                        //int message_id1 = client.Subscribe(topic1, qosLevels);
                        //Log("message_id1:" + message_id1.ToString());
                    }
                    catch (Exception e)
                    {
                        Log("数据库读取出错或网络出现问题:" + e.ToString());
                        Log("网连接失败，等待connectMqt线程 连接网络");
                }

                }
                else
                {
                   Log("网连接失败，等待connectMqt线程 连接网络");
                   
                }
        }

        //当接收到关注的消息后，该事件会被调用
        private void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            // access data bytes throug e.Message
            //处理接收到的消息  
            string msg = System.Text.Encoding.Default.GetString(e.Message);

            //来一个请求，开一个线程来处理
            Thread sendins_thread = new Thread(DealWith);//发送指令线程
            sendins_thread.Start(msg);


        }


        private void MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            Log("！！！！！！！！！！！！！当推送消息成功后，该事件会被调用MqttMsgPublished");
        }

        private void ConnectionClosed(object sender, EventArgs e)
        {
            Log("！！！！！！！！！！！！！连接被关闭-->ConnectionClosed。准备开始从新连接-----关闭以前老的可能是");
            Log(e.ToString());

            

        }

        //关注话题成功后，该事件会被调用
        private void MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            Log("！！！！！！！！！！！！！关注话题成功后，该事件会被调用MqttMsgSubscribed");
        }


        //取消关注成功后，该事件会被调用
        private void MqttMsgUnsubscribed(object sender, MqttMsgUnsubscribedEventArgs e)
        {
            Log("！！！！！！！！！！！！！MqttMsgUnsubscribed");
        }
        //用于接收用户发送的mqtt指令处理的线程
        private void DealWith(object obj)
        {
            string msg = obj.ToString();//将线程中传入的object对象转为string

            Log("接收到的mqtt指令是："+ msg + "\r\n");
            JObject jo = (JObject)JsonConvert.DeserializeObject(msg);//调用程序NetonJson，将传过来的string转为json对象
            string user = jo["sender"].ToString();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
            string act = jo["actionType"].ToString();//用户的请求是什么
            string info = jo["data"].ToString();
            string result = jo["result"].ToString();//用户发来的用于显示历史数据的页数
            //根据动作去进行不同操作
            JsonInfo jif = new JsonInfo(result, user, act, info);//!!!!!!!注意result
            ToDo td = new ToDo();
            int actT = td.actionType(jif);
            Log("执行动作代码=" + actT);

            switch (actT)
            {
                case 1:
                    Log("要执行查询在线料仓的动作" + "\r\n");
                    Thread t1 = new Thread(getBinlist);//发送指令线程
                    t1.Start(jif);
                    break;
                case 2:
                    Log("要执行盘库操作" + "\r\n");
                    isPk = 1;//you料仓进行盘库
                    Thread t2 = new Thread(panKu);//发送指令线程
                    t2.Start(jif);
                    //panKu(jif);
                    break;
                case 3:
                     Log("要执行查询历史记录" + "\r\n");                        
                     Thread t3 = new Thread(getHisList);//发送指令线程
                     t3.Start(jif);                 
                    //getHisList(jif);
                    break;
                case 4:
                    Log("要执行日期查询历史记录" + "\r\n");
                    Thread t4 = new Thread(getHisListByDate);//发送指令线程
                    t4.Start(jif);
                    //getHisListByDate(jif);
                    break;
                case 5:
                    Log("取消盘库" + "\r\n");
                    //取消盘库的时候，要在binPkingList中删除这个用户
                    Thread t5 = new Thread(cancel);//发送指令线程
                    t5.Start(jif);
                    //cancel(jif);
                    break;
                case 6:
                    Log("获取料仓详情" + "\r\n");
                    Thread t6 = new Thread(getBinInfo);//发送指令线程
                    t6.Start(jif);
                    //getBinInfo(jif);
                    break;
                case 7:
                    isPk = 1;//you料仓进行盘库
                    Log("要执行清洁镜头" + "\r\n");
                    //来一个请求，开一个线程来处理
                    Thread clearThread = new Thread(qingJie);//发送指令线程
                    clearThread.Start(jif);
                    break;
                case 8:
                    Log("要执行获取数据分析" + "\r\n");
                    Thread t8 = new Thread(getBinAnal);//发送指令线程
                    t8.Start(jif);
                    //getBinAnal(jif);
                    break;

                case 9:
                    Log("要获取段日期的数据" + "\r\n");
                    Thread t9 = new Thread(getLongTime);//发送指令线程
                    t9.Start(jif);
                    //getBinAnal(jif);
                    break;
                case 10:
                    Log("要获取分组列表" + "\r\n");
                    Thread t10 = new Thread(getGroup);//发送指令线程
                    t10.Start(jif);
                    break;
                case 11:
                    Log("根据分组获取料仓信息" + "\r\n");
                    Thread t11 = new Thread(getBinlistByGroup);//发送指令线程
                    t11.Start(jif);
                    break;
                case 12:
                    Log("远程垂直校准" + "\r\n");
                    Thread t12 = new Thread(setWarehouseJiaozhun);
                    t12.Start(jif);
                    break;
                case 13:
                    Log("获取垂直角度" + "\r\n");
                    Thread t13= new Thread(getWarehouseJiaozhun);
                    t13.Start(jif);
                    break;
            }

        }
        /// <summary>
        /// 发送指令函数
        /// </summary>
        /// <param name="obj"></param>
        /// 
        //函数SendIns实现发送指令功能，该函数在一个独立的线程中执行，
        //    这个线程不关闭。在SendIns函数中，会循环遍历发送队列sendIns_queue，
        //    只要不为空就从队列中取指令发送，因此在需要发送指令的部分只需要将指
        //    令封装成FacMessage类型，再放到sendIns_queue中即可。
        private void SendIns(object obj)//???发送指令
        {
            while (flag_threadout == 1)
            {
                Thread.Sleep(30);
                //发送队列不为空
                if (sendIns_queue.Count != 0)
                {
                    Thread.Sleep(20);
                    FacMessage ele = sendIns_queue.Dequeue();
                    if (ele.ProduceTime >= 0)//时间每一秒减减，如果过了这个时间，就只取指令不发指令
                    {
                        //Log("执行 serialPort_WriteLine");
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
        /// 串口发送指令函数
        /// </summary>
        /// <param name="facmsg"></param>
        private void serialPort_WriteLine(object facmsg)//一直运行的线程中，只要队列中有信息就调用方法发送指令
        {

            FacMessage ele = (FacMessage)facmsg;
           // Log("串口发送指令===========" +"料仓编号"+ ele.fac_num+"指令码"+ele.instruction+"应答指令码"+ele.ins_answer+"应答标志"+ele.sign_answer);
            //thread.Start(ins_answer);
            while (flag_threadout == 1)
            {//没有点击退出按钮
                Thread.Sleep(30);
                if (port_mask == 0)//0：必须执行完一个料仓的状态和盘库指令。。。0时才执行，1时不执行
                {//发出类似于盘库这样的操作指令时，需要先将发送函数阻塞，来发送盘库指令

                    if (oper_ins.Count == 0 && list_status.Count == 0)//没有等待盘库操作（清洁镜头，监控，盘库：操作指令、目的指令）  没有等待回应的
                    {//操作指令队列为空（表示操作指令已经取出并发出）并且状态链表为空（所有发出去的指令需要都回应了或者超时处理了）
                        try
                        {
                            if (aim_ins.Count != 0)
                            {
                                if (ele.ins_answer.Equals("21"))
                                {
                                    oper_ins.Enqueue(aim_ins.Dequeue());//aim_ins的出队的信息放入到oper_ins队列中
                                   Log("添加oper指令");
                                }
                            }
                            if (ele.ins_answer.Equals("27"))
                            {
                                Log("在串口处将要发送回传数据！！！！！！！");
                            }
                            serialPort1.WriteLine(ele.instruction);
                            ele.life_time = 2;//检查两次.每次发指令给他记录一下存活时间，如果超过两秒没回应，则为离线
                            list_status.Add(ele);
                        }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                        catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
                        {
                            if (ele.ins_answer.Equals("21"))
                            {
                                oper_ins.Clear();
                                //oper_ins.Enqueue(aim_ins.Dequeue());
                            }

                            //MessageBox.Show("mqtt请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试，不在线的长度=", "提示");
                            //！！！！！！！！！！！！！！！！！！！！！
                            //编写mqtt告诉用户接触不良，不能使用
                            for (int i = 0; i < offlinebinlist.Count(); i++)
                            {
                                Rebinlist rin = new Rebinlist(offlinebinlist[i].bid, offlinebinlist[i].bname, "串口不正常");
                                sendbinlist.Add(rin);
                            }

                            //将获取的数据发送回Mian、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、
                            JsonMsg js = new JsonMsg("3", "请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试");//指令往回传

                            string sendMqttInfo = JsonConvert.SerializeObject(js);
                            WriteMQ(sendMqttInfo);

                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试");
                        }
                        break;//发送指令后退出循环，指令一定会发出，因为有超时函数来清空状态链表和准备发送指令队列
                    }

                }

            }
        }
        /// <summary>
        /// 在程序开始运行时，创建一个定时器来执行getStatus函数，在这个函数中遍
        /// 历状态链表，如果发现状态链表的某个节点的应答标志已经为true（在trans
        /// 函数中，接收到一条可靠的指令，会修改状态链表的这个节点的状态值），表示
        /// 已经接收并处理了这一条指令，就删除这个节点；如果在预定的时间内还没有收
        /// 到回应，就应该进行超时处理，处理函数是statusTimeout，传递一个类型为FacMessage
        /// 的参数，然后根据指令编号来进行下一步的超时处理。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void getStatus(object sender, System.Timers.ElapsedEventArgs e)//1s调用一次
        {//循环获取状态函数，在定时器中实现
            //Log("getState的状态计时器");
            //用于判断回传的点是否回传完毕。
            if (back_complet > 0)
            {
                back_complet--;//当有回传数据的时候会有给他赋值为5。过5秒后进行统计
                //Log("判断开始回传的时间：back_complet = "+ back_complet);
                if (back_complet == 0)//回传最后一个点的时候back_complet = 3.当过完2s，就会执操作，1、判断是否全部回传：是，则什么都不管，否，则再回传一次。若三次回传还是不全，则将已经回传的点放入到数据库中。
                {
                    Log("料仓回传过后5秒，back_complet = 0.recv_num=" + recv_num );

                    if (recv_num != 0)
                    {
                        Log("料仓回传过后五秒，recv_num ！= 0。说明点没有回传完全");
                        Log("保存数据库");
                        port_mask = 0;
                        try
                        {

                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n回传数据" + backLengthData);
                            //保存在数据库中
                            //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据

                            MySqlConn mysql1 = new MySqlConn();
                            string sql = "SELECT DateTime  FROM bindata where BinID = " + binPking + " Order by DateTime desc limit 1";//最新的 那个料仓的条记录
                            MySqlDataReader rd = mysql1.getDataFromTable(sql);

                            string LastTime = "";
                            //dbLastData.command.CommandText = sql;
                            //dbLastData.command.Connection = dbLastData.connection;
                            //dbLastData.Dr = dbLastData.command.ExecuteReader();
                            while (rd.Read())
                            {
                                if (rd["DateTime"].ToString().Equals("") == false)
                                {//获取到第一个重量不为NULL的值，记录为最新数据,并且跳出循环
                                    LastTime = rd["DateTime"].ToString();
                                    Log("获取到的最新的盘库数据时间：" + LastTime);
                                    break;
                                }
                            }
                            rd.Close();

                            string sq = "update bindata set BackData = '" + backLengthData + "' , BackAn = '" + backAnData + "',BackAll = '" + backAllInfo + "'  where DateTime = '" + LastTime + "'  ";//and Weight > 0
                            //dbLastData.command.CommandText = sq;
                            //dbLastData.command.Connection = dbLastData.connection;
                            int i = mysql1.nonSelect(sq);
                          
                            if (i > 0)
                            {
                                Log("\r\n点不全的情况下，回传数据保存成功\r\n");
                                Log("\r\n回传数据值：----------" + backLengthData);
                                Log("\r\n回传数据值：----------" + backAnData);
                                //Log("回传的数据backLengthData：" + backLengthData);
                                backLengthData = "";
                                backAnData = "";
                                backAllInfo = "";
                                recv_num = 0;
                            }
                            else
                            {
                                Log(DateTime.Now.ToString("G") + "\r\n回传数据保存失败\r\n");
                            }
                            mysql1.Close();
                        }
                        catch (Exception xx)
                        {
                            recv_num = 0;
                            backLengthData = "";
                            backAnData = "";
                            backAllInfo = "";
                            Log(DateTime.Now.ToString("G") + "\r\n回传数据保存失败" + xx.ToString());
                        }


                        Log("重置回传的信息");
                        recv_num = 0;
                        backLengthData = "";
                        backAnData = "";
                        backAllInfo = "";
                    }
                    else//recv_num = 0 说明回传数据成功了，不需要再次回传
                    {
                        Log("recv_num=" + recv_num + ",验证回传数量是否返回回传，数量正确，回传完毕");
                        port_mask = 0;
                        back_complet = -1;//跳出计时器
                        //重置回传的信息
                        Log("重置回传的信息，解除串口屏蔽");
                        recv_num = 0;
                        backLengthData = "";
                        backAnData = "";
                        backAllInfo = "";
                    }

                }
            }


            foreach (FacMessage FacInfo in sendIns_queue)
            {//遍历发送指令队列，并将指令的产生时间加一
                FacInfo.ProduceTime--;
            }
            for (int i = 0; i < list_status.Count; i++)
            {
                if (list_status[i].sign_answer == false)
                {//表示未接收到回应
                    list_status[i].life_time--;//life_time起始时间为2。这个线程为没1秒执行一次
                    //Console.WriteLine(list_status[i].ins_num + ": lifetime " + list_status[i].life_time);
                    if (list_status[i].life_time <= 0)
                    {//超时未响应，创建线程处理


                        Log("发送指令超时。未响应联系不上的设备的id=" + list_status[i].fac_num);

                        string msg = list_status[i].message;
                        FacMessage ele = new FacMessage(list_status[i].ins_num, list_status[i].ins_answer,
                            list_status[i].fac_num, list_status[i].sign_answer,
                            list_status[i].life_time, list_status[i].message,
                            list_status[i].instruction, list_status[i].resend - 1, list_status[i].ProduceTime);
                        //如果在预定的时间内还没有收到回应，就应该进行超时处理，处理函数是statusTimeout，传递一个类型为FacMessage的参数，然后根据指令编号来进行下一步的超时处理。
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
        /// <summary>fSendIns
        /// 超时处理函数
        /// 根据指令编号来进行下一步的超时处理。
        /// </summary>
        /// <param name="index"></param>
        private void statusTimeout(object index)
        {
            try
            {
                FacMessage fac = (FacMessage)index;//指令对象
                list_mutex.WaitOne();//再删除节点之前加锁
                //Console.WriteLine("i = "+ i+" sum = "+list_status.Count+" Time: "+ DateTime.Now);
                //list_status.RemoveAt(i);
                for (int i = 0; i < list_status.Count; i++)
                {
                    if (fac.fac_num.Equals(list_status[i].fac_num) && fac.ins_answer.Equals(list_status[i].ins_answer))
                    {
                        list_mutex.WaitOne();
                        list_status.RemoveAt(i);//删除那个节点
                        list_mutex.ReleaseMutex();
                    }
                }
                list_mutex.ReleaseMutex();//删除节点之后解锁

                int FacExist = 0;//是否是第一次未收到指令,根据指令进行操作
                for (int i = NoAckList.Count - 1; i >= 0; i--)
                {//遍历未收到指令的链表
                    if (NoAckList[i].fac_num.Equals(fac.fac_num))
                    {//如果找到说明不是第一次未接收到指令，
                        FacExist = 1;
                        NoAckList[i].life_time++;

                        if (NoAckList[i].life_time >= 10)
                        {

                            //for(int j = 0; j < binlist.Count; j++)
                            //{
                            //    if (binlist[j].bname.Equals(getName(NoAckList[i].fac_num)))
                            //    {
                            //        binlist.Remove(binlist[j]);
                            //    }
                            //}
                            //binlist.Remove(getName(NoAckList[i].fac_num));//在线料仓显示区域
                            //Rebinlist rb = new Rebinlist(NoAckList[i].fac_num, getName(NoAckList[i].fac_num), "离线");
                            //sendbinlist.Add(rb);

                            NoAckList.RemoveAt(i);
                        }
                    }
                }
                if (0 == FacExist)
                {
                    NoAckList.Add(new FacMessage(fac.fac_num, 1, 0));
                }


                if (fac.ins_answer.Equals("01"))//查询是否在线
                {//如果01号指令没有回应，说明料仓不存在或不在线，将其添加到不在线列表中

                    string fac_name = getName(fac.fac_num);
                    if (fac_name.Equals("") == false)
                    {
                        //先remove的目的时防止料仓重复
                        //Invoke(new MethodInvoker(delegate
                        //{
                        //    //binlist.Remove(fac_name);
                        //    richTextBox1.AppendText("不在线料仓 : " + fac_name + "\r\n");
                        //    //添加离线的料仓
                        //    Rebinlist rin = new Rebinlist(fac.fac_num, fac_name, "离线");
                        //    sendbinlist.Add(rin);
                        //}));
                        Log("不在线料仓 : " + fac_name);
                        //添加离线的料仓
                        Rebinlist rin = new Rebinlist(fac.fac_num, fac_name, "离线");
                        sendbinlist.Add(rin);
                    }
                    else
                    {
                        Log("请输入有效的料仓编号");
                        //new Thread(new ParameterizedThreadStart(showBox)).Start("请输入有效的料仓编号");

                    }

                }
                //else if (fac.ins_answer.Equals("11"))
                //{//如果查询温湿度指令没有恢复，就把查询温湿度按钮变为可用
                //    查询温度ToolStripMenuItem.Enabled = true;
                //}
                else if (fac.ins_answer.Equals("21"))
                {//表示查询状态指令没有回应，需要重发

                    if (fac.resend > 0)
                    {
                        aim_ins.Enqueue(oper_ins.Dequeue());
                        sendIns_queue.Enqueue(fac);
                    }
                    else
                        oper_ins.Clear();
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
                int isRe = ms.nonSelect(sql);
                ms.Close();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.command.ExecuteNonQuery();
                //db.Close();

            }
#pragma warning disable CS0168 // 声明了变量“se”，但从未使用过
            catch (SqlException se)
#pragma warning restore CS0168 // 声明了变量“se”，但从未使用过
            {
                //Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));//数据库异常存入文件
                //thread_file.Start(se.ToString());
                Log("请检查数据库是否创建好");
                //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");

            }


        }
        private void loadPort()//加载串口数据
        {
            Log("读取串口");
            try
            {
                //string path = "E:\\Warehouse\\Warehouse\\bin\\Release";
                string path = "C:\\Program Files\\WHSetup1";
                if (File.Exists(path + "\\serialPort.txt") == false)//创建保存串口信息的文件

                    File.Create(path + "\\serialPort.txt").Close();

                StreamReader sr = new StreamReader(path + "\\serialPort.txt", Encoding.Default);
                String line = sr.ReadLine();
                if (line == null)
                {
                    chuankou = -1;
                    Log("请先设置串口");
                    //new Thread(new ParameterizedThreadStart(showBox)).Start(d["Main_CKInfo1"]);//请先设置串口

                    //new Thread(display_noport).Start();
                }
                else
                {
                    Log("串口信息存在");
                    string[] serial = line.Split('+');//文件中串口信息按照"+"分隔
                    try
                    {
                        //this.serialPort1 = new System.IO.Ports.SerialPort(this.components);
                        serialPort1.PortName = serial[0];
                        //this.serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
                        serialPort1.BaudRate = int.Parse(serial[1]);
                        serialPort1.Open();
                        port_isopen = 1;
                        chuankou = port_isopen;
                        //刷新ToolStripMenuItem.PerformClick();
                    }
                    catch (Exception exc)
                    {
                        chuankou = 0;
                        Log("串口设置已失效，请重新设置.错误：" + exc.ToString());
                        //  new Thread(new ParameterizedThreadStart(showBox)).Start(d["Main_CKInfo"]);//串口设置已失效，请重新设置
                    }

                    Thread thread_takeData = new Thread(takeData);//解析数据
                    thread_takeData.Start();

                    if (port_isopen == 1)
                    {//串口正常
                        Thread load_fac = new Thread(OpenMainForm);//启动时显示料仓线程
                        load_fac.Start();
                    }
                    else
                    {
                        chuankou = -1;
                        Log("串口不正常，请检查串口是否插牢");
                        //new Thread(display_noport).Start();
                        Thread load_fac = new Thread(OpenMainForm);//启动
                        load_fac.Start();
                    }
                }
                sr.Close();
#pragma warning disable CS0168 // 声明了变量“e”，但从未使用过
            }catch(Exception e)
#pragma warning restore CS0168 // 声明了变量“e”，但从未使用过
            {
                Log("读取串口文件出错，请检查文件目录下是否有serialPort.txt文件");
            }
            
        }
        private void OpenMainForm(object obj)
        {//主要功能是向中控发送每一个料仓的查询指令
            Thread.Sleep(30);

            Log("查询厂区码");
            try
            {
                if (factory.Equals(""))//如果厂区码是空的
                {
                    string sql = "select * from config";

                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rd = ms.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        factory = rd["FactoryID"].ToString();
                    }
                    rd.Close();
                    ms.Close();
                    Log("获取的厂区码是" + factory);
                }
            }
            catch(Exception e)
            {
                Log("查询c出错："+e.ToString() );
            }
           
        }

        //接收win客户端通过消息队列发送的消息
        private void RecMsg(object obj)
        {
            try
            {
                // Receive message, 同步的Receive方法阻塞当前执行线程，直到一个message可以得到
                Log("开启了msg接收线程");
                while (true)
                {
                    System.Messaging.Message message = mq.Receive();

                    message.Formatter = new System.Messaging.XmlMessageFormatter(new System.Type[] { typeof(string) });
                    //消息队列收到消息了才会进行处理
                    //Log("main的请求："+message.Body.ToString());
                    //接收数据，解析数据，对应发送
                    string info = message.Body.ToString();
                    JieXi(info);
                }
            }
            catch(Exception e)
            {
                Log(e.ToString());
            }
          


        }
        //解析
        private void JieXi(string info)
        {
            JObject jo = (JObject)JsonConvert.DeserializeObject(info);//调用程序NetonJson，将传过来的string转为json对象
            string typeMsg = jo["type"].ToString();//获取返回信息的类型
            string dataMsg = jo["data"].ToString();//用户的请求是什么
            //Log("！！！！！！！！！！！！接收到MSMQ消息" + typeMsg);
            if (typeMsg.Equals("1"))
            {//验证串口信息
                if (dataMsg.Equals("isChuankouOk"))
                {
                    JsonMsg js = new JsonMsg("1", "" + chuankou);
                    Log("串口状态：" + chuankou);
                    string sendMqttInfo = JsonConvert.SerializeObject(js);
                    WriteMQ(sendMqttInfo);
                }
            }
            else if (typeMsg.Equals("2"))
            {//传递来的是指令字符
                try
                {
                    //直接通过串口发送
                    serialPort1.WriteLine(dataMsg);
                }
                catch (Exception exc)
                { // 如果串口连接失败，就要向客户端发送 请检查无线模块的信息。
                    Log("请检查无线设备是否接触不良\r\n请重插无线模块并重新设置通信后重试");
                    //在程序工作的过程中，串口出现异常，需要通过mqtt发送给客户端
                    Log("错误：" + exc.ToString());

                    //每当发送指令的时候拦截出错信息。并将报错信息传给客户端

                    JsonMsg js = new JsonMsg("1", "" + chuankou);
                    string sendMqttInfo = JsonConvert.SerializeObject(js);
                    WriteMQ(sendMqttInfo);
                }


            }
        }
        private void WriteMQ(string info)
        {

            System.Messaging.Message message = new System.Messaging.Message();
            message.Body = info.Trim();
            message.Formatter = new System.Messaging.XmlMessageFormatter(new System.Type[] { typeof(string) });
            //Log("发送mq信息");
            mq1.Send(message);
        }
        /// 服务停止  
        protected override void OnStop()
        {
            Log("程序停止了。");

        }

        /// 系统关闭   
        protected override void OnShutdown()
        {
            Log("电脑关闭了。");
        }
        void Log(string str)    // 记录服务启动  
        {
            file_mutex.WaitOne();
            {
                string info = string.Format("{0}-{1}", DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss"), str);
                string path = "C://Mqtt//MyTestLog.txt";//"C://Mqtt//MyTestLog.txt"
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(info);
                    //关闭
                    sw.Close();
                }
            }
            file_mutex.ReleaseMutex();

        }

        //获取机器的数据
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytecount = serialPort1.BytesToRead;
                byte[] readBuffer = new byte[bytecount];
                serialPort1.Read(readBuffer, 0, readBuffer.Length);
                string readstr = Encoding.UTF8.GetString(readBuffer);

                cirQueue.In(readstr);
                data_buffer += cirQueue.Out();//获取数据
                //将获取的数据发送回Mian、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、、
                JsonMsg js = new JsonMsg("2", readstr);//指令往回传

                string sendMqttInfo = JsonConvert.SerializeObject(js);
                WriteMQ(sendMqttInfo);
                cirQueue.Clear();


            }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
            catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
            {
                //richTextBox1.AppendText(exc.ToString() + "\r\n");
            }
        }
        private string data_buffer = "";//从循环队列中取出的数据先放到缓冲去区中。
        private string data_take = "";//从数据缓冲区取出数据报进行处理
        private void takeData(object obj)//截取数据
        {
            try
            {
                while (flag_threadout == 1)
                {
                    //加sleep
                    Thread.Sleep(50);
                    while (data_buffer.Equals("") != true)
                    {
                        int i = 0;//i记录出现":"的位置
                        for (i = 0; i < data_buffer.Length; i++)
                        {
                            if (data_buffer[i] == ':')//指令的开头
                                break;
                        }
                        int j = 0;//j记录出现"\n"的位置
                        for (j = i; j < data_buffer.Length; j++)
                        {
                            if (data_buffer[j] == '\n')
                            {
                                data_take = data_buffer.Substring(i, j - i + 1);
                                //进行代指令的解析！！！
                                new Thread(new ParameterizedThreadStart(trans)).Start(data_take);
                                data_buffer = data_buffer.Remove(i, data_take.Length);
                                data_take = "";
                                break;
                            }
                        }
                    }

                }

            }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
            catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
            {
            }
        }
        //接受设备的指令后，解析指令，并将相应的发送指令队列中的状态队列的状态改为true
        private void setSign(object obj)
        {
            //在接收到数据后，经过处理数据函数，已经将料仓编号和指令提取出来，并用‘+’分隔开
            //在这只需要按照‘+’切割就可以得到料仓编号和指令
            string[] receive = ((string)obj).Split('+');
            //接收到的设备地址转为十进制
            int equip_receive = Int32.Parse(receive[0], System.Globalization.NumberStyles.HexNumber);
            //接收到的指令码转为十进制
            int instruction_receive = Int32.Parse(receive[1], System.Globalization.NumberStyles.HexNumber);
            //richTextBox1.AppendText(equip_receive.ToString()+"  "+instruction_receive.ToString()+"-----\r\n");
            //枷锁

            list_mutex.WaitOne();
            for (int i = 0; i < list_status.Count; i++)
            {
                //状态链表中的设备地址转化为十进制,将料仓编号转为十进制
                int equip_list = Int32.Parse(list_status[i].fac_num, System.Globalization.NumberStyles.HexNumber);
                //状态链表中的指令码转化为十进制
                int instruction_list = Int32.Parse(list_status[i].ins_answer.ToString(), System.Globalization.NumberStyles.HexNumber);

                if (equip_list == equip_receive && instruction_list == instruction_receive)
                {//只是将状态标志改为true，并不执行删除操作，删除操作统一在遍历链表的定时器中删除

                    list_status[i].sign_answer = true;//对接收到了的指令进行改变状态的操作，标识出已经接收到指令。
                }

            }
            list_mutex.ReleaseMutex();

        }
        /// <summary>
        /// ID获取料仓名
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
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rd = ms.getDataFromTable(sql);
                    while (rd.Read())
                    {
                        name = rd["BinName"].ToString();
                    }
                    rd.Close();
                    ms.Close();
                    return name;
                }
#pragma warning disable CS0168 // 声明了变量“se”，但从未使用过
                catch (SqlException se)
#pragma warning restore CS0168 // 声明了变量“se”，但从未使用过
                {
                    //Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    //thread_file.Start(se.ToString());
                    ////MessageBox.Show("请检查数据库是否创建好\r\n", d["MB_Title"]);
                    //new Thread(new ParameterizedThreadStart(showBox)).Start(d["Main_Alter7"]);
                    Log("请检查数据库是否创建好--getName");
                    return "";
                }

            }
            else
            {
                //MessageBox.Show("请检查数据库是否创建好", d["MB_Title"]);
                Log("请检查数据库是否创建好--getName");
                return "";
            }

        }
        //添加错误信息到文件中
        private void method_file(object obj)
        {//数据库连接不上时，将这个错误信息添加到日志文件中
            string message_error = obj.ToString();
            string path = "C:\\Program Files\\WHSetup1"; // E:\\Warehouse\\Warehouse\\bin\\Release
            FileStream fs = new FileStream(path + "\\logByMqtt.txt", FileMode.Create | FileMode.Append);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine("错误信息是：" + message_error + " 时间是：" + DateTime.Now.ToString());
            sw.Flush();
            sw.Close();
            fs.Close();
        }



        private void trans(object obj)
        {
            try
            {
                string ins = obj.ToString();
               //Log("收到的指令" + ins);
                string str = Data.decoding(ins);
                if (str.Length <= 1)
                {
                    return;
                }

                string[] s = str.Split(' '); //返回的是厂区码+设备地址的十六进制+操作码的十六进制+数据的十进制
                int equip = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);//料仓地址的十进制表示
                string data = s[3];
                if (s[0].Equals(factory) != true)//查询厂区码是否一致!!!factory为厂区码
                {
                    Log("收到非本厂区数据，请更换通信频道");
                    //new Thread(new ParameterizedThreadStart(showBox)).Start(d["Main_trans1"]);//"收到非本厂区数据，请更换通信频道"
                    return;
                }

                //接收到指令后， 新建一个线程来处理状态链表，将应答标志改成true
                //刷新控件时调用的方法
                new Thread(new ParameterizedThreadStart(setSign)).Start(equip.ToString() + "+" + s[2]);//根据设备id和操作指令，将状态链表中相应的指令改为true等待剔除

                //用于验证设备是否掉线，通过10条指令来检验。将收到指令的设备从无回应list中删除
                for (int i = NoAckList.Count - 1; i >= 0; i--)
                {
                    if (NoAckList[i].fac_num.PadLeft(2, '0').Equals(equip.ToString().PadLeft(2, '0')))//通过料仓编号进行匹配，如果匹配成功，则说明该指令收到了回应，应该从NoAckList中将其删除。
                    {
                        NoAckList.RemoveAt(i);
                        break;
                    }
                }

                //richTextBox1.AppendText(equip.ToString() + "+" + s[2]+"\r\n");
                //s[2] 是操作码
                if (s[2].Equals("01"))//在线料仓，回应在线料仓，并回复直径， 仓筒高度， 下锥高度， 物料密度
                {//回应仓库参数,添加料仓后回应
                 //Log("回复是否在线");

                    Rebinlist rin = new Rebinlist(equip.ToString(), getName(equip.ToString().PadLeft(2, '0')), "在线");
                    sendbinlist.Add(rin);
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
                        string sql = "select * from bininfo where BinID =" + equip.ToString().PadLeft(2, '0');
                        try
                        {//检测数据库是否可以连接
                         //DataBase db = new DataBase();
                         //db.command.CommandText = sql;
                         //db.command.Connection = db.connection;
                         //db.Dr = db.command.ExecuteReader();
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
                                //因为手机端不会添加料仓，所以新料仓查询添加工作交给pc客户端
                            }
                            else
                            {//如果有这个料仓，就设置参数,并在在线列表中显示
                                string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                                if (!(diameter / 100).ToString().Equals(diameterInSql.ToString()))
                                {//如果发现有不相等的情况，记录下时间
                                    string saveMsg = diameterInSql.ToString() + "-->" + (diameter / 100).ToString();
                                    //DataBase dbSaveLog = new DataBase();
                                    string sqlSaveLog = "insert into binlog values('" + equip.ToString() + "', '仓筒直径被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                    //dbSaveLog.command.CommandText = sqlSaveLog;
                                    //dbSaveLog.command.Connection = dbSaveLog.connection;
                                    //dbSaveLog.command.ExecuteNonQuery();
                                    //db.Close();
                                    MySqlConn ms1 = new MySqlConn();
                                    int res = ms1.nonSelect(sqlSaveLog);
                                    ms1.Close();
                                    if (res > 0)
                                    {
                                        Log("参数修改成功");
                                    }
                                 }
                                if (!(((cylinderH / 100).ToString()).Equals(cylinderHInSql.ToString())))
                                {
                                    string saveMsg = cylinderHInSql.ToString() + "-->" + (cylinderH / 100).ToString() + ".";
                                    //DataBase dbSaveLog = new DataBase();
                                    string sqlSaveLog = "insert into binlog values('" + equip.ToString() + "', '仓筒高度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                    //dbSaveLog.command.CommandText = sqlSaveLog;
                                    //dbSaveLog.command.Connection = dbSaveLog.connection;
                                    //dbSaveLog.command.ExecuteNonQuery();
                                    //db.Close();
                                    MySqlConn ms2 = new MySqlConn();
                                    int res = ms2.nonSelect(sqlSaveLog);
                                    ms2.Close();
                                    if (res > 0)
                                    {
                                        Log("参数修改成功");
                                    }
                                }
                                if (!(((pyramidH / 100).ToString()).Equals(pyramidHInsSql.ToString())))
                                {
                                    string saveMsg = pyramidHInsSql.ToString() + "-->" + (pyramidH / 100).ToString();
                                    //DataBase dbSaveLog = new DataBase();
                                    string sqlSaveLog = "insert into binlog values('" + equip.ToString() + "', '下锥高度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                    //dbSaveLog.command.CommandText = sqlSaveLog;
                                    //dbSaveLog.command.Connection = dbSaveLog.connection;
                                    //dbSaveLog.command.ExecuteNonQuery();
                                    //db.Close();
                                    MySqlConn ms3 = new MySqlConn();
                                    int res = ms3.nonSelect(sqlSaveLog);
                                    ms3.Close();
                                    if (res > 0)
                                    {
                                        Log("参数修改成功");
                                    }
                                }
                                if (!((density / 1000).ToString()).Equals(densityInSql.ToString()))
                                {
                                    string saveMsg = densityInSql.ToString() + "-->" + (density / 1000).ToString();
                                    //DataBase dbSaveLog = new DataBase();
                                    string sqlSaveLog = "insert into binlog values('" + equip.ToString() + "', '物料密度被修改', '" + ins + "', '" + saveMsg + "', '" + time + "')";
                                    //dbSaveLog.command.CommandText = sqlSaveLog;
                                    //dbSaveLog.command.Connection = dbSaveLog.connection;
                                    //dbSaveLog.command.ExecuteNonQuery();
                                    //db.Close();
                                    MySqlConn ms4 = new MySqlConn();
                                    int res = ms4.nonSelect(sqlSaveLog);
                                    ms4.Close();
                                    if (res > 0)
                                    {
                                        Log("参数修改成功");
                                    }
                                }
                               


                            }
                            //发送边距，顶高，轴距查询指令！！！！！！！！！！！！

                            
                        }
                        catch (Exception se)
                        {//如果数据库连接失败，则抛出异常.,并将异常写入文件中
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好--if (s[2].Equals(01))//在线料仓");
                        }
                    }
                    else
                    {//如果是一开始数据库没有连接好，抛出异常
                        Log("请检查数据库是否创建好--if (s[2].Equals(01))//在线料仓");
                    }
                }
                if (s[2].Equals("0B"))//回复边距，顶高，轴距参数
                {
                    //Log("收到回复边距，顶高，轴距");
                    // 设备数据
                    String[] datas = data.Split('+');
                    if (datas.Length != 3)
                    {
                        return;
                    }
                    String Margin = datas[0];//边距
                    String Top = datas[1];//顶高度
                    String Wheelbase = datas[2];//轴距
                    //Log(Margin + " " + Top + " " + Wheelbase + "\r\n");

                }
                if (s[2].Equals("11"))
                {//回应温度湿度
                    //Log("接收到设备的返回信息查询温度的信息");
                    //查询温度ToolStripMenuItem.Enabled = true;
                    //接收完温度后进行查询回传数据


                    //Log("接收到设备的返回信息=" + data);
                    string[] d = data.Split('+');
                    string id = Convert.ToInt32(s[1], 16).ToString();
                    string temp = d[0];//温度
                    string hum = d[1];//湿度
                    Log("获取到的温度id = " + id + "，temp=" + temp + ",hum = " + hum + "，saveBinID = " + saveBinID);

                    if (id.Equals(saveBinID))//当获取到的温湿度id是之前盘库查询状态时的id，证明这个温湿度是盘库过程中的温湿度。
                    {
                        Log("将温湿度保存到变量中");
                        saveTemp = temp;
                        saveHum = hum;
                    }
                    else
                    {
                        Log("接收到的温湿度不是真正盘库的料仓的，不保存在变量中");
                    }


                    //string temp1 = ins[17] + "" + ins[18] + "" + str[19] + "" + str[20];//温度的整数部分

                    //Log("接收到的温度的指令==" + temp1);
                    //Log("解析的温度是==" + temp);

                    //Log("有相同的温度1");
                    //if (temp.Equals("0") && hum.Equals("0"))
                    //    return;
                    //Log("有相同的温度2");
                    //if (isReWendu(equip, temp).Equals("error"))//保证温度只记录一次
                    //{
                    //    Log("执行isReWendu.新输入的和保存在数据库中的数据相等且时间差小于1分钟，这说明是重复输入，不能输入!!!!!!!!!!!!!!!!!!!!");
                    //}
                    //else
                    //{
                    //    //1 查询数据库，看看数据库中这个id的料仓在最近一个是否有温度为空的记录，如果有就update这个记录，如果没有就insert新记录
                    //    //DataBase dbLastData = new DataBase();//寻找是否有记录
                    //    Log("正常保存温度 id=" + equip);
                    //    string sql1 = "select  * from bindata where BinID = '"+equip+ "' and Temp is null order by DateTime desc LIMIT 1";//mysql取值，取前1条记录
                    //                                                                                                                      //dbLastData.command.CommandText = sql1;
                    //                                                                                                                      //dbLastData.command.Connection = dbLastData.connection;
                    //                                                                                                                      //dbLastData.Dr = dbLastData.command.ExecuteReader();
                    //    MySqlConn ms = new MySqlConn();
                    //    MySqlDataReader rd = ms.getDataFromTable(sql1);

                    //    string shijian = "";
                    //    string isUpdate = "0";
                    //    Log("正常保存温度1");
                    //    while (rd.Read())
                    //    {
                    //        Log("正常保存温度2");
                    //        shijian = rd["DateTime"].ToString();
                    //        DateTime a = Convert.ToDateTime(shijian);
                    //        string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    //        DateTime b = Convert.ToDateTime(time);
                    //        TimeSpan ts = b - a;//datetime做差
                    //        int cha = (int)ts.TotalSeconds;
                    //        Log("正常保存温度3");
                    //        if (rd["Weight"].ToString().Equals("") == false && cha < 90)//保存体积重量的时间和要保存温度的时间查在90s以内，温度就可以保存
                    //        {//获取到第一个重量不为NULL的值.且是最新的数据
                    //            Log("------------------------------这一次重量已经保存，要update数据库保存温度");
                    //            isUpdate = "1";//如果有，就执行update
                    //            break;
                    //        }
                    //    }
                    //    //dbLastData.Dr.Close();
                    //    //dbLastData.Close();
                    //    rd.Close();
                    //    ms.Close();


                    //    if (isUpdate.Equals("0"))//说明没有值。要insert
                    //    {
                    //        //在没有体积的数据的时候不输入
                    //        Log("在插入温度的时候，isUpdate，因为之前没有重量体积数据，想要新建一条。执行温度的insert。但是不能这样");
                    //        //DateTime now = DateTime.Now;
                    //        //string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    //        //string sql = "insert into [bindata] ( BinID,Temp, Hum,DateTime) values(" + id + "," + temp + "," + hum + ",'" + time + "') ";
                    //        //try
                    //        //{//检测数据库能否连接成功
                    //        //    DataBase db = new DataBase();
                    //        //    db.command.CommandText = sql;
                    //        //    db.command.Connection = db.connection;
                    //        //    if (db.command.ExecuteNonQuery() > 0)
                    //        //    {
                    //        //        Log("接收到温湿度信息并保存");
                    //        //        db.Close();
                    //        //    }
                    //        //}
                    //        //catch (Exception se)
                    //        //{//数据库连接失败时，抛出异常
                    //        //    //Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    //        //    //thread_file.Start(se.ToString());
                    //        //    Log("请检查数据库是否创建好" + se.ToString());
                    //        //    //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //        //    //richTextBox1.AppendText(se.ToString() + "\r\n");
                    //        //}
                    //    }
                    //    else//说明数据库中有值了要update
                    //    {
                    //        Log("说明数据库中有体积和重量了。要update这个语句,执行温度的update");
                    //        DateTime now = DateTime.Now;
                    //        string time = shijian;
                    //        string sql = "update bindata set Temp = '"+ temp +"', Hum = '"+ hum +"' where BinID = '"+ equip +"' and DateTime = '"+ shijian + "' ";
                    //        //label24.Text = temp + "  ℃";
                    //        //label27.Text = hum + "  %";
                    //        try
                    //        {//检测数据库能否连接成功
                    //         //DataBase db = new DataBase();
                    //         //db.command.CommandText = sql;
                    //         //db.command.Connection = db.connection;
                    //            MySqlConn ms1 = new MySqlConn();
                    //            int r = ms1.nonSelect(sql);
                    //            ms1.Close();
                    //            if (r > 0)
                    //            {
                    //                Log("update记录的温度了");
                    //            }
                    //        }
                    //        catch (Exception se)
                    //        {//数据库连接失败时，抛出异常
                    //            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                    //            thread_file.Start(se.ToString());
                    //            Log("请检查数据库是否创建好");
                    //            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                    //            //richTextBox1.AppendText(se.ToString() + "\r\n");
                    //        }
                    //    }
                    //}


                    //Log("wdAndBack的值："+ wdAndBack + ",当是1时需要回传数据，当是0时不需要回传数据");

                    //if (wdAndBack == 1)
                    //{
                    //    recv_num = 0;
                    //    backLengthData = "";
                    //    backAnData = "";

                    //    Log("发送开启回传指令");
                    //    string databack = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "38", "0001");
                    //    sendIns_queue.Enqueue(new FacMessage(1, "27", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "开启回传数据", databack, s_Produce - 3595));
                    //    wdAndBack = 0;//当回传完后设为初始态，这样单独请求温度就不会请求回传数据了。
                    //}


                }
                //回应盘库
                else if (s[2].Equals("13"))
                {//回应确认 盘点库容

                    Log(getName(equip.ToString().PadLeft(2, '0')) + "开始盘库\r\n\r\n");
                    string eq_name = getName(equip.ToString());
                    if (eq_name.Equals("") != true)
                    {
                        //comboBox5.Items.Remove(eq_name);
                        //comboBox6.Items.Remove(eq_name);
                        //comboBox3.Items.Remove(eq_name);
                        //comboBox3.Items.Add(eq_name);
                        //if (comboBox3.Items.Count == 1)
                        //{
                        //    comboBox3.Text = comboBox3.Items[0].ToString();
                        //}
                        //if (comboBox6.Items.Count == 0)
                        //{
                        //    player.Stop();
                        //}
                        //接收到盘库信息，将料仓信息加入链表中
                        FacMessage calc = new FacMessage(equip.ToString().PadLeft(2, '0'), 0, 1);
                        CalcVol_list.Add(calc);
                        //存入开始盘库的别彪

                        //binlist.Remove(fac_name);
                        Log("加入盘库列表的料仓 : " + equip.ToString() + "\r\n");
                        //添加离线的料仓
                        //Rebinlist rin = new Rebinlist(equip.ToString(), getName(equip.ToString().PadLeft(2, '0')), "0");
                        //sendPklist.Add(rin);


                    }

                    string data_find = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "32", "0000");//厂区码
                    send_ins.Add(new FacMessage(ins_num++, "21", equip.ToString().PadLeft(2, '0'), false, 120, "回应状态", data_find, 3, s_Produce - 3595));

                }
                else if (s[2].Equals("17"))
                {//回应确认清洁镜头
                    string eq_name = getName(equip.ToString());

                    Log("开始清洁镜头：" + eq_name);

                    //Rebinlist rin = new Rebinlist(equip.ToString(), getName(equip.ToString().PadLeft(2, '0')), "0");
                    //sendPklist.Add(rin);


                    FacMessage calc = new FacMessage(equip.ToString().PadLeft(2, '0'), 0, 2);
                    CalcVol_list.Add(calc);

                }
                else if (s[2].Equals("23"))//针对手机的
                {//盘库结果

    
                    string volume = (data.Split('+'))[0];
                    string weight = (data.Split('+'))[1];


                    string e1 = (data.Split('+'))[2];
                    string e2 = (data.Split('+'))[3];
                    string e3 = (data.Split('+'))[4];
                    string model = (data.Split('+'))[5];
                    string moshi = "没获取到盘库算法";


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
                        volume = "-" + volume;
                        weight = "-" + weight;

                    }

                    //Log("数据：" + e1 + "," + e2 + "," + e3 + "," + moshi + ",");
                    string print = e1 + "," + e2 + "," + e3;


                    //MessageBox.Show(data+" "+ volume+"  "+weight);
                    float vol_f = float.Parse(volume);
                    float wei_f = float.Parse(weight);
                    float diameter = 0, cylinderh = 0, pyramidh = 0;
                    if (isRe(equip, weight).Equals("error"))//确保盘一次库就只有一个显示盘库结果。
                    {
                        //表示数据库中已经有了数据了，所以就不做任何操作
                        Log("已经保存了体积数据");
                        //删除这个节点，因为已经保存好了
                        //从正在盘库显示框中删除节点，从正在盘库列表中删除此节点
                        //盘库时，每30秒发送一次查询状态函数，这个链表标记哪个料仓需要查询
                        for (int i = send_ins.Count - 1; i >= 0; i--)
                        {//从后往前遍历，避免删除节点后数组越界,删除节点
                            if (send_ins.Count == 0) break;
                            if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                            {
                                //将查询完的那个指令从队列中删除，这样就不会在状态中再次查询了
                                Log("删除了send_ins，设备id:" + send_ins[i].fac_num);
                                send_ins.RemoveAt(i);
                                //break;

                            }
                        }
                        //删除一个盘库信息,//正在盘库链表
                        for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                        {
                            if (CalcVol_list.Count == 0) break;
                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                            {
                                //再把正在盘库的信息列表中的完成盘库的信息删掉
                                Log("删除了CalcVol中的，设备id:" + CalcVol_list[i].fac_num);
                                CalcVol_list.RemoveAt(i);//盘库完成删除状态
                                //break;
                            }
                        }

                    }
                    else//表示数据库中没有这次盘库的数据，需要保存，然后删除节点
                    {
                        Log("接收到盘库结果，要保存新数据");
                        try
                        {
                            string sql = "select * from bininfo where BinID = " + equip;
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
                            //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据
                            sql = "select Weight from bindata where BinID = " + equip + " order by DateTime desc";
                            float LastWeight = 0;
                            //dbLastData.command.CommandText = sql;
                            //dbLastData.command.Connection = dbLastData.connection;
                            //dbLastData.Dr = dbLastData.command.ExecuteReader();
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
                            //if ((LastWeight != 0 && Math.Abs(wei_f - LastWeight) > 10))
                            //{//如果接收到的数据比仓库体积还大, 或者重量与最新数据相比相差10吨，就开启回传数据
                            //    Log("数据比仓库体积还大, 或者重量与最新数据相比相差10吨，就开启回传数据");
                            //    string databack = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "38", "0001");
                            //    sendIns_queue.Enqueue(new FacMessage(1, "27", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "开启回传数据", databack, s_Produce - 3595));
                            //}
                        }
#pragma warning disable CS0168 // 声明了变量“e”，但从未使用过
                        catch (Exception e) { }
#pragma warning restore CS0168 // 声明了变量“e”，但从未使用过

                        if (vol_f == 0)
                            return;
                        string id = Convert.ToInt32(s[1], 16).ToString();
                        //将查询的数据传入数据库
                        try
                        {

                            //获取到料仓的当前数据库中的密度
                            string ss = "select * from bininfo where BinID=" + id;
                            float densityInSql = 0;
                            float Jd = 0;
                            //DataBase db = new DataBase();
                            //db.command.CommandText = ss;
                            //db.command.Connection = db.connection;
                            //db.Dr = db.command.ExecuteReader();
                            MySqlConn ms = new MySqlConn();
                            MySqlDataReader rd = ms.getDataFromTable(ss);
                            while (rd.Read())
                            {

                                densityInSql = float.Parse(rd["Density"].ToString());
                                Jd = float.Parse(rd["Angle"].ToString());
                            }
                            rd.Close();
                            ms.Close();
                            string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            //保存查询到的各种信息，加上保存在变量中的温湿度
                            string sql = "insert into bindata (BinID, Volume, Weight,Temp,Hum, DateTime,Algorithm ,PrintNum,Quality,MiDu,Jd) values(" + id + ", " + volume + ", " + weight + "," + saveTemp + "," + saveHum + ", '" + time + "','" + model + "','" + print + "','0'," + (densityInSql).ToString() + "," + (Jd).ToString() + ")";

                            //DataBase db_save = new DataBase();
                            //db_save.command.CommandText = sql;
                            //db_save.command.Connection = db_save.connection;
                            MySqlConn ms2 = new MySqlConn();
                            int n = ms2.nonSelect(sql);
                            ms2.Close();
                            if (n > 0)
                            {

                                Log("查询到  " + getName(equip.ToString().PadLeft(2, '0')) + "  料仓数据并保存");


                                //DataBase db_log = new DataBase();
                                //string sql_log = "insert into [binlog] values('" + equip.ToString() + "', '盘库成功', '" + ins + "', '盘库完成', '" + time + "')";

                                //查询到体积后查询温湿度信息存数据库
                                string searchTemp = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "16", "0000");
                                //Log("发送查询温湿度指令");                                //
                                //sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", searchTemp, s_Produce));

                                Log("发送开启回传指令");
                                recv_num = 0;
                                backLengthData = "";
                                backAnData = "";
                                backAllInfo = "";
                                canBackTimes = 1;//允许多回传一次
                                string databack = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "38", "0001");
                                sendIns_queue.Enqueue(new FacMessage(1, "27", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "开启回传数据", databack, s_Produce - 3595));

                                //wdAndBack = 1;//使在回传温湿度的时候也会传数据。
                                //wdAndBackNum = 3;
                                //Log("将温度wdAndBack改为："+ wdAndBack + "。wdAndBackNum = " + wdAndBackNum);

                                //从正在盘库显示框中删除节点，从正在盘库列表中删除此节点
                                //盘库时，每30秒发送一次查询状态函数，这个链表标记哪个料仓需要查询
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {//从后往前遍历，避免删除节点后数组越界,删除节点
                                    if (send_ins.Count == 0) break;
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {
                                        //将查询完的那个指令从队列中删除，这样就不会在状态中再次查询了
                                        Log("删除了send_ins，设备id:" + send_ins[i].fac_num);
                                        send_ins.RemoveAt(i);
                                        //break;

                                    }
                                }
                                //删除一个盘库信息,//正在盘库链表
                                for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                                {
                                    if (CalcVol_list.Count == 0) break;
                                    if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                                    {
                                        //再把正在盘库的信息列表中的完成盘库的信息删掉
                                        Log("删除了CalcVol中的，设备id:" + CalcVol_list[i].fac_num);
                                        CalcVol_list.RemoveAt(i);//盘库完成删除状态
                                        //break;
                                    }
                                }

                            }
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库设置，保存数据错误");
                            //出错时删节点
                            for (int i = send_ins.Count - 1; i >= 0; i--)
                            {//从后往前遍历，避免删除节点后数组越界,删除节点
                                if (send_ins.Count == 0) break;
                                if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                {
                                    //将查询完的那个指令从队列中删除，这样就不会在状态中再次查询了
                                    send_ins.RemoveAt(i);
                                    //break;

                                }
                            }
                            //删除一个盘库信息,//正在盘库链表
                            for (int i = CalcVol_list.Count - 1; i >= 0; i--)
                            {
                                if (CalcVol_list.Count == 0) break;
                                if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                                {
                                    //再把正在盘库的信息列表中的完成盘库的信息删掉
                                    CalcVol_list.RemoveAt(i);
                                    //break;
                                }
                            }

                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库设置");
                        }
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
                    for (int j = CalcVol_list.Count - 1; j >= 0; j--)
                    {
                        if (CalcVol_list[j].fac_num.PadLeft(2, '0').Equals(equip.ToString().PadLeft(2, '0')))
                        {
                            CalcVol_list.RemoveAt(j);
                            Log("已停止当前操作");
                            //break;
                        }
                    }

                    ////让文本框获取焦点，不过注释这行也能达到效果
                    //richTextBox1.Focus();
                    ////设置光标的位置到文本尾   
                    //richTextBox1.Select(richTextBox1.TextLength, 0);
                    ////滚动到控件光标处   
                    //richTextBox1.ScrollToCaret();
                    //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + "  已停止当前操作\r\n\r\n");
                    Log(getName(equip.ToString().PadLeft(2, '0')) + "  已停止当前操作");

                }

                //接收到工作状态
                else if (s[2].Equals("21"))
                {//接收到工作状态
                    Log("接收到工作状态!!!!!!!!!!!!!!!!!!!!!!!!!!! 料仓id是==" + equip.ToString().PadLeft(2, '0'));
                    //Log(data);
                    int issendins = 0;//判断是否发出了指令
                    string[] data_array = data.Split('+');
                    if (data_array.Length != 3)
                        return;
                    string complet = data_array[0];//盘库是否完成
                    data = data_array[1];//料仓状态
                    string schedule = data_array[2];//盘库进度
                    int schedule_int = Int32.Parse(schedule);//进度信息！！
                    //richTextBox1.AppendText("接收到21\r\nsendIns_list长度  "+sendIns_list.Count.ToString() + "\r\n");
                    int data_int = Int32.Parse(data, System.Globalization.NumberStyles.HexNumber);
                    //Log("data_int*******************************" + data_int);

                    if (data_int == 2 && schedule_int == 100)
                    {
                        //progressBar2.Value = 0;//进度条
                    }
                    //接收到状态后，将进度信息更改到正在盘库列表中
                    for (int i = 0; i < CalcVol_list.Count; i++)
                    {
                        if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                        {
                            //Log("料仓id是==" + equip.ToString());
                            CalcVol_list[i].life_time = schedule_int;//???????
                            Log("获取到的进度是==" + schedule_int);

                            //20181225添加数据

                            string id = Convert.ToInt32(s[1], 16).ToString();
                            saveBinID = id;//将正在盘库的料仓id存到全局变量中
                            Log("接收到工作状态后，发送获取温湿度指令！！！！！！");
                            string searchTemp = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "16", "0000");
                            sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", searchTemp, s_Produce));


                        }
                    }
                    FacMessage ele;
                    //Log("oper_ins.Count ：  " + oper_ins.Count + "\r\n");

                    if (oper_ins.Count != 0)
                    {   //oper_ins目的指令队列

                        //Log("接收到21\r\n complet：  " + complet + "\r\n");
                        //Log("\r\n data_int：  " + data_int + "\r\n");
                        //Log("\r\n schedule_int：  " + schedule_int + "\r\n");
                        ele = oper_ins.Dequeue();
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

                                            Log("发送查询结果指令的id是==" + fac_num);
                                            //Log("ele.instruction" + ele.instruction);
                                            serialPort1.WriteLine(ele.instruction);
                                            //list_status.Add(new FacMessage(ins_num++, "23", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "查询结果", data));
                                            list_status.Add(ele);
                                            issendins = 1;
                                            break;
                                        }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                                        catch (Exception exc) { }
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过

                                    }
                                }
                                port_mask = 0;//解除屏蔽
                            }

                            //return;
                        }
                        else if (data_int != 2 && complet.Equals("00"))//没有正在盘库&&盘库没完成
                        {
                            Log("接收到被取消盘库data_int != 2 && complet.Equals(00)");
                            if (send_ins.Count != 0)
                            {
                                for (int i = send_ins.Count - 1; i >= 0; i--)
                                {
                                    if (send_ins[i].fac_num.Equals(equip.ToString().PadLeft(2, '0')))
                                    {

                                        //comboBox3.Items.Remove(getName(send_ins[i].fac_num));

                                        for (int it = CalcVol_list.Count - 1; it >= 0; it--)
                                        {
                                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[it].fac_num))
                                            {
                                                Log("要删除的正在盘库的id是==" + equip.ToString());
                                                //CalcVol_list.RemoveAt(it);
                                                Log("料仓id是==" + equip.ToString());
                                                CalcVol_list[i].life_time = -6;//被取消盘库
                                                Log("warning：：：：：：：：：：：获取到的进度是==" + -6 + "。设备被取消盘库");
                                                //break;
                                                //isStop.Add(equip,1);//因为取消盘库而删除节点时，记录



                                            }
                                        }
                                        String name = getName(send_ins[i].fac_num);

                                        send_ins.RemoveAt(i);

                                        issendins = 1;

                                        DateTime now = DateTime.Now;
                                        string sql = "insert into binlog values('" + equip.ToString() + "', '硬件故障', '" + ins + "', '盘库被取消', '" + now.ToString("yyyy/MM/dd HH:mm:ss") + "')";
                                        //DataBase db = new DataBase();
                                        //db.command.CommandText = sql;
                                        //db.command.Connection = db.connection;
                                        //db.command.ExecuteNonQuery();
                                        MySqlConn ms = new MySqlConn();
                                        int n = ms.nonSelect(sql);
                                        ms.Close();
                                        Log("料仓 " + name + " 被取消盘库");
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
                                        //删除料仓的操作。
                                        //string fac_name = getName(fac_num);
                                        //int d = delete(fac_name);
                                        //if (d > 0)
                                        //{
                                        //    Invoke(new MethodInvoker(delegate
                                        //    {
                                        //        //让文本框获取焦点，不过注释这行也能达到效果
                                        //        richTextBox1.Focus();
                                        //        //设置光标的位置到文本尾   
                                        //        richTextBox1.Select(richTextBox1.TextLength, 0);
                                        //        //滚动到控件光标处   
                                        //        richTextBox1.ScrollToCaret();
                                        //        richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n删除了料仓  " + fac_name + "\r\n\r\n");
                                        //        if (d != 0)
                                        //        {
                                        //            checkedListBox1.Items.Remove(fac_name);
                                        //        }
                                        //    }));
                                        //}

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
                                                    serialPort1.WriteLine(ele.instruction);
                                                    list_status.Add(ele);
                                                    break;
                                                }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                                                catch (Exception exc) { }
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过

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
                                                        serialPort1.WriteLine(ele.instruction);
                                                        list_status.Add(ele);
                                                        break;

                                                    }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                                                    catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
                                                    {
                                                        break;
                                                    }
                                                }
                                            }
                                            port_mask = 0;//解除屏蔽
                                        }
                                        else
                                        {
                                            //先remove的目的时防止料仓重复
                                            //binlist.Remove(fac_name);
                                            Log("加入盘库列表的料仓 : " + getName(fac_num));
                                            //添加离线的料仓
                                            //Rebinlist rin = new Rebinlist(equip.ToString(), getName(fac_num), "忙");
                                            //sendPklist.Add(rin);

                                            //MessageBox.Show(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                            //new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                        }
                                        //MessageBox.Show(, "提示");
                                    }
                                    else
                                    {
                                        //先remove的目的时防止料仓重复
                                        //binlist.Remove(fac_name);
                                        Log("有别的操作的料仓 : " + getName(fac_num));
                                        //添加离线的料仓
                                        //Rebinlist rin = new Rebinlist(equip.ToString(), getName(fac_num), "忙");
                                        //sendPklist.Add(rin);
                                        //MessageBox.Show(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                        //new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
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
                                    if (isoperating == false || ele.instruction.Equals("delete"))
                                    { //先remove的目的时防止料仓重复
                                      //binlist.Remove(fac_name);
                                        Log("有别的操作的料仓2 : " + getName(fac_num));
                                        //添加离线的料仓
                                        //Rebinlist rin = new Rebinlist(equip.ToString(), getName(fac_num), "忙");
                                        //sendPklist.Add(rin);
                                        //MessageBox.Show(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                        //new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在盘库， 请稍后操作示");
                                    }
                                    //MessageBox.Show();

                                    //料仓正在进行盘库，但是操作指令不是查询数据指令
                                    else if (isoperating == true && (ele.ins_answer.Equals("23") == false))
                                    {
                                        //先remove的目的时防止料仓重复
                                        //binlist.Remove(fac_name);
                                        Log("有别的操作的料仓23 : " + getName(fac_num));
                                        //添加离线的料仓
                                        //Rebinlist rin = new Rebinlist(equip.ToString(), getName(fac_num), "忙");
                                        //sendPklist.Add(rin);
                                        //MessageBox.Show(" 料仓  " + getName(fac_num) + "  正在进料监控， 请稍后操作");
                                        //new Thread(new ParameterizedThreadStart(showBox)).Start("料仓  " + getName(fac_num) + "  正在盘库， 请稍后操作");
                                    }
                                    //MessageBox.Show(" ", "提示");
                                }
                                else if (data_int == 3)
                                {
                                    //先remove的目的时防止料仓重复

                                    //binlist.Remove(fac_name);
                                    Log("有别的操作的料仓3 : " + getName(fac_num));
                                    //添加离线的料仓
                                    //Rebinlist rin = new Rebinlist(fac_num, getName(fac_num), "忙");
                                    //sendPklist.Add(rin);
                                    //MessageBox.Show(" 料仓  " + getName(fac_num) + "  正在清洁镜头， 请稍后操作");
                                    //new Thread(new ParameterizedThreadStart(showBox)).Start(" 料仓  " + getName(fac_num) + "  正在清洁镜头， 请稍后操作");
                                    //MessageBox.Show("", "提示");
                                }

                            }
                        }


                    }
                }
                else if (s[2].Equals("27"))
                {//开始回传数据
                    Log("清空前"+ backLengthData+"---"+ backAnData+"----"+ backAllInfo);
                    recv_num = 0;
                    backLengthData = "";
                    backAnData = "";
                    backAllInfo = "";
                    Log("清空后" + backLengthData + "---" + backAnData + "----" + backAllInfo);
                    Log("接收到回传指令，，，准备回传");
                    binPking = "" + equip;//哪个库正在盘库，哪个就给他附上值，用于回传的点个数错误时可以找到对应的料仓。

                    //查询数据库,判断是直径扫描还是面扫描
                    string sql = "select * from bininfo where BinID=" + equip.ToString().PadLeft(2, '0');
                    string type = "";
                    try
                    {//检测数据库是否可以连接
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {
                            type = rd["type"].ToString();
                        }
                        rd.Close();
                        ms.Close();
                    }
                    catch (Exception se)
                    {
                        Log("查询数据库出错。" + se.ToString());
                    }

                    //if (type.Equals("侧置平扫") || type.Equals("顶置平扫"))
                    if (type.Equals("1") || type.Equals("4"))
                    {

                        Log("获取到27指令!!!!!!!平面扫描算法");
                    }
                    else
                    {
                        Log("获取到27指令!!!!!!!直径扫描算法");
                    }


                    int data_int = Int32.Parse(data);
                    DateTime now = DateTime.Now;
                    recv_num = data_int;//传过来的点的个数
                    Log("开启回传数据，回传的大小为recv_num：" + recv_num + "，id是：" + binPking);
                    port_mask = 1;//屏蔽串口
                    Log("屏蔽串口!!");

                }
                else if (s[2].Equals("25"))
                {//回传数据的过程

                    recnum_mutex.WaitOne();
                    {
                        binPking = "" + equip;//哪个库正在盘库，哪个就给他附上值，用于回传的点个数错误时可以找到对应的料仓。

                        back_complet = 5;//判断是否回传完成的，因为定时器每1秒检查一次回传。。。当开始回传到收到的最后一个回传，3s后开始计算回传的点
                        port_mask = 1;//屏蔽串口
                                      //Log("屏蔽串口!!");
                        string[] d = data.Split('+');

                        recv_num--;//在确认回传的时候收到的测量点数


                        backAnData += d[0] + ",";//角度值：

                        backLengthData += d[1] + ",";//将要保存数据库的数据保存在字段中


                        backAllInfo += data + ";";//角度值：

                        Log("recv_num : " + data);

                        if (recv_num == 0)
                        {
                            Log("回传数据正常。。。点全部回传过来了");
                            try
                            {
                                if (backLengthData.Equals("") || backAnData.Equals(""))
                                {
                                    Log("有空的数据");
                                }
                                else
                                {
                                    //保存在数据库中
                                    //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据

                                    string sql = "SELECT DateTime  FROM bindata where BinID = " + equip + " Order by DateTime desc limit 1";//最新的 那个料仓的条记录
                                    string LastTime = "";
                                    //dbLastData.command.CommandText = sql;
                                    //dbLastData.command.Connection = dbLastData.connection;
                                    //dbLastData.Dr = dbLastData.command.ExecuteReader();
                                    MySqlConn ms = new MySqlConn();
                                    MySqlDataReader rd = ms.getDataFromTable(sql);
                                    while (rd.Read())
                                    {
                                        if (rd["DateTime"].ToString().Equals("") == false)
                                        {//获取到第一个重量不为NULL的值，记录为最新数据,并且跳出循环
                                            LastTime = rd["DateTime"].ToString();
                                            break;
                                        }
                                    }
                                    rd.Close();
                                    ms.Close();
                                    string sq = "update bindata set BackData = '" + backLengthData + "' , BackAn = '" + backAnData + "',BackAll = '"+ backAllInfo + "' where DateTime = '" + LastTime + "'  ";//之前是必须是正的，现在可以是负的了and Volume > 0
                                    //dbLastData.command.CommandText = sq;
                                    //dbLastData.command.Connection = dbLastData.connection;
                                    //int i = dbLastData.command.ExecuteNonQuery();
                                    MySqlConn ms1 = new MySqlConn();
                                    int n = ms1.nonSelect(sq);
                                    ms1.Close();
                                    //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n回传数据值：" + i + "----------" + backLengthData);
                                    if (n > 0)
                                    {
                                        Log(DateTime.Now.ToString("G") + "\r\n回传数据保存成功\r\n");
                                        reBackNum = 2;//保存了数据就要变化
                                        Log("回传的数据backLengthData：" + backLengthData);
                                        Log("回传的数据backAnData：" + backAnData);
                                        Log("回传的数据backAllInfo：" + backAllInfo);
                                        backLengthData = "";
                                        backAnData = "";
                                        backAllInfo = "";
                                    }
                                    else
                                    {
                                        Log(DateTime.Now.ToString("G") + "\r\n回传数据保存失败\r\n");
                                    }
                                }

                            }
#pragma warning disable CS0168 // 声明了变量“e”，但从未使用过
                            catch (Exception e)
#pragma warning restore CS0168 // 声明了变量“e”，但从未使用过
                            {
                                Log(DateTime.Now.ToString("G") + "\r\n回传数据保存失败");
                            }
                            finally
                            {
                                port_mask = 0;//解除屏蔽
                            }
                        }
                    }
                    recnum_mutex.ReleaseMutex();



                }
                else if (s[2].Equals("31"))
                {
                    Log("接收到回传结束的指令0x31,recv_num = " + recv_num);
                    if (recv_num != 0 && canBackTimes > 0)
                    {
                        Log("(31)数据没有回传完全-----缺失的点recv_num = " + recv_num+ "。。。。将recv_num设为0，让5s后的计时器不进行数据库操作");
                        canBackTimes--;
                        recv_num = 0;
                        back_complet = 5;
                        backLengthData = "";
                        backAnData = "";
                        backAllInfo = "";
                        string databack = Data.Data(factory, equip.ToString().PadLeft(2, '0'), "38", "0001");
                        sendIns_queue.Enqueue(new FacMessage(1, "27", equip.ToString().PadLeft(2, '0'), false, TIME_WAIT, "开启回传数据", databack, s_Produce - 3595));
                    }
                    else
                    {
                        Log("0x31数据全部回传成功，recv_num = 0 不做任何操作。。。。或者已经回传了2次了canBackTimes = " + canBackTimes + "....recv_num =" + recv_num + "。等待5s进行保存处理");
                    }



                }
                else if (s[2].Equals("2F"))//获取垂直校准
                {
                    string[] datas = data.Split('+');
                    string str1 = "";
                    if (datas[0].Equals("00"))
                    {
                        str1 = datas[1];
                        //Log("str1" + str1);
                    }
                    else if (datas[0].Equals("01"))
                    {
                        str1 = "-" + datas[1];
                        //Log("str1"+str1);
                    }
                    //SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseJiao", datas[0]);//   
                    //str1 = datas[0] + datas[1];
                    SendJsonInfo sendJif = new SendJsonInfo("pk", TOPIC, "setresponseJiao", str1);// 
                    string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                    //Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                    //将数据发送出去
                    client.Publish(TOPIC, Encoding.UTF8.GetBytes(sendMqttInfo));

                }
                else if (s[2].Equals("0F"))//回复垂直校准
                {
               
                    string[] datas = data.Split('+');
                    SendJsonInfo sendJif = new SendJsonInfo("pk", TOPIC, "responseJiao",  datas[0]);//                                                                                                                 
                    string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                    //Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                    //将数据发送出去
                    client.Publish(TOPIC, Encoding.UTF8.GetBytes(sendMqttInfo));

                }
                else if (s[2].Equals("FF"))
                {
                    DateTime now = DateTime.Now;
                    //string time = now.Year + "/" + now.Month + "/" + now.Day + " " +
                    //    now.Hour.ToString().PadLeft(2, '0') + ":" + now.Minute.ToString().PadLeft(2, '0') + ":" + now.Second.ToString().PadLeft(2, '0');
                    string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

                    int data_int = Int32.Parse(data);

                    if (data_int == 0)
                    {
                        try
                        {
                            string sql = "insert into binlog values('" + equip.ToString() + "', '硬件故障', '" + ins + "', '激光头无回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;

                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //MessageBox.Show(getName(equip.ToString().PadLeft(2, '0')) + "  激光头无回应,请清洁镜头后重试");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  激光头无回应,请清洁镜头后重试");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            //richTextBox1.Focus();
                            ////设置光标的位置到文本尾   
                            //richTextBox1.Select(richTextBox1.TextLength, 0);
                            ////滚动到控件光标处   
                            //richTextBox1.ScrollToCaret();

                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 激光头无回应\r\n\r\n");
                            Log(getName(equip.ToString().PadLeft(2, '0')) + " 激光头无回应");


                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }


                    }
                    else if (data_int == 1)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '激光头回应数据异常', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  激光头回应数据异常,请清洁镜头后重试");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  激光头回应数据异常,请清洁镜头后重试");
                            //MessageBox.Show(", "硬件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            //richTextBox1.Focus();
                            ////设置光标的位置到文本尾   
                            //richTextBox1.Select(richTextBox1.TextLength, 0);
                            ////滚动到控件光标处   
                            //richTextBox1.ScrollToCaret();
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 激光头回应数据异常\r\n\r\n");
                            Log(getName(equip.ToString().PadLeft(2, '0')) + " 激光头回应数据异常");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");

                        }


                    }
                    else if (data_int == 2)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,角度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '角度计无回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //MessageBox.Show(getName(equip.ToString().PadLeft(2, '0')) + "  角度计无回应,请相关技术人员检测");
                            ////new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  角度计无回应,请相关技术人员检测");
                            ////MessageBox.Show(", "硬件故障");
                            ////让文本框获取焦点，不过注释这行也能达到效果
                            //richTextBox1.Focus();
                            ////设置光标的位置到文本尾   
                            //richTextBox1.Select(richTextBox1.TextLength, 0);
                            ////滚动到控件光标处   
                            //richTextBox1.ScrollToCaret();
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 角度计无回应\r\n\r\n");
                            Log(getName(equip.ToString().PadLeft(2, '0')) + " 角度计无回应");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            Log("请检查数据库是否创建好\r\n");
                        }

                    }
                    else if (data_int == 3)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '温度计没回应', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //MessageBox.Show(getName(equip.ToString().PadLeft(2, '0')) + "  温度计无回应,请相关技术人员检测\r\n", "提示");
                            ////new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  温度计无回应,请相关技术人员检测");
                            ////MessageBox.Show(", "硬件故障");
                            ////让文本框获取焦点，不过注释这行也能达到效果
                            //richTextBox1.Focus();
                            ////设置光标的位置到文本尾   
                            //richTextBox1.Select(richTextBox1.TextLength, 0);
                            ////滚动到控件光标处   
                            //richTextBox1.ScrollToCaret();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + " 温度计无回应\r\n\r\n");

                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 4)
                    {
                        //接收到状态后，将进度信息更改到正在盘库列表中
                        for (int i = 0; i < CalcVol_list.Count; i++)
                        {
                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                            {
                                Log("料仓id是==" + equip.ToString());
                                CalcVol_list[i].life_time = -4;
                                Log("warning：：：：：：：：：：：获取到的进度是==" + -4 + "。与测量设备485通讯失败");
                            }
                        }
                        Log("与测量设备485通讯失败");
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '与测量设备485通讯失败（干扰导致CRC校验错误）', '" + ins + "', '与测量设备485通讯失败（干扰导致CRC校验错误）', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                        }
#pragma warning disable CS0168 // 声明了变量“se”，但从未使用过
                        catch (SqlException se)
#pragma warning restore CS0168 // 声明了变量“se”，但从未使用过
                        {

                            Log("ff - 4 数据库来连接失败");
                        }
                    }
                    else if (data_int == 5)
                    {
                        for (int i = 0; i < CalcVol_list.Count; i++)
                        {
                            if (equip.ToString().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))
                            {
                                Log("料仓id是==" + equip.ToString());
                                CalcVol_list[i].life_time = -5;
                                Log("warning：：：：：：：：：：：获取到的进度是==" + -5 + "。与测量设备485通讯失败（线缆断开，发出指令无回复）");
                            }
                        }

                        Log("与测量设备485通讯失败（线缆断开，发出指令无回复）");
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','硬件故障,温度计无回应', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '与测量设备485通讯失败（线缆断开，发出指令无回复）', '" + ins + "', '与测量设备485通讯失败（线缆断开，发出指令无回复）', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                        }
#pragma warning disable CS0168 // 声明了变量“se”，但从未使用过
                        catch (SqlException se)
#pragma warning restore CS0168 // 声明了变量“se”，但从未使用过
                        {

                            Log("ff - 5 数据库来连接失败");
                        }
                    }

                    else if (data_int == 16)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,电机正忙或正在执行其他操作', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '硬件故障', '" + ins + "', '电机正忙或正在执行其他操作', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  电机正忙或正在执行其他操作");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  电机正忙或正在执行其他操作");
                            //MessageBox.Show(", "软件故障");
                            //让文本框获取焦点，不过注释这行也能达到效果
                            //richTextBox1.Focus();
                            ////设置光标的位置到文本尾   
                            //richTextBox1.Select(richTextBox1.TextLength, 0);
                            ////滚动到控件光标处   
                            //richTextBox1.ScrollToCaret();
                            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + getName(equip.ToString().PadLeft(2, '0')) + " 电机正忙\r\n\r\n");


                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 17)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  直径', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软甲故障', '" + ins + "', '没配置  直径', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  直径");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  直径");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 18)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '没配置  高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(equip.ToString().PadLeft(2, '0') + "  没配置  高度");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(equip.ToString().PadLeft(2, '0') + "  没配置  高度");
                            //MessageBox.Show(getName(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));

                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 19)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  下锥高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '没配置  下锥高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  下锥高度");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  下锥高度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 20)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  安装距离到顶高度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '没配置  安装距离到顶高度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  安装距离到顶高度");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  安装距离到顶高度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("\r\n", "提示");
                        }

                    }
                    else if (data_int == 21)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '软件故障', '" + ins + "', '没配置  密度', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            Log(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }

                    }
                    else if (data_int == 64)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '垂直测量失败，取消盘库', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 65)
                    {
                        try
                        {
                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '累计测量失败10个点，取消盘库', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 66)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '负值过大', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 67)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '超过满仓体积', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }
                    else if (data_int == 68)
                    {
                        try
                        {

                            int addr = Int32.Parse(s[1], System.Globalization.NumberStyles.HexNumber);
                            //string sql = "insert into [binlog] values('" + addr.ToString() + "','软件故障,没配置  密度', '" + DateTime.Now.ToString() + "');";
                            string sql = "insert into binlog values('" + addr.ToString() + "', '盘库过程出错', '" + ins + "', '错误数据过多', '" + time + "')";
                            //DataBase db = new DataBase();
                            //db.command.CommandText = sql;
                            //db.command.Connection = db.connection;
                            //db.command.ExecuteNonQuery();
                            //db.Close();
                            MySqlConn ms = new MySqlConn();
                            int n = ms.nonSelect(sql);
                            ms.Close();
                            //new Thread(new ParameterizedThreadStart(showBox)).Start(getName(equip.ToString().PadLeft(2, '0')) + "  没配置  密度");
                            //MessageBox.Show(", "软件故障");
                        }
                        catch (SqlException se)
                        {
                            Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));
                            thread_file.Start(se.ToString());
                            Log("请检查数据库是否创建好");
                            //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                            //MessageBox.Show("请检查数据库是否创建好\r\n", "提示");
                        }
                    }

                }
            }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
            catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
            {
                //richTextBox1.AppendText(exc.ToString() + "\r\n");
            }

        }



        //MQTT
        //用户获取在线的料仓列表
        private void getBinlist(object obj)
        {
            Log("asdadsasda");
            lock (locker)
            {
                Log("bbbbbbbbbbbbbbbbb");
                JsonInfo jif = (JsonInfo)obj;
                //1、查询数据库
                //2、发送是否在线指令
                //3、返回在线数组
                sendbinlist.Clear();
                offlinebinlist.Clear();
                if (factory.Equals("") != true)//如果有厂区码来了，就查料仓是否在线
                {
                    string sql = "select * from bininfo";//查询料仓信息表
                                                           //DataBase db;
                    Queue<FacMessage> send_queue = new Queue<FacMessage>();
                    try
                    {
                        //DataBase db = new DataBase();
                        //db.command.CommandText = sql;
                        //db.command.Connection = db.connection;
                        //db.Dr = db.command.ExecuteReader();
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        allBinNum = 0;
                        while (rd.Read())
                        {//显示列表，向存在于数据库中所有的料仓发送"00"号指令，检测料仓是否存在
                            //计数器加1
                            allBinNum++;
                            Thread.Sleep(10);
                            string id = rd["BinID"].ToString();//获取到了料仓的id
                            string name = rd["BinName"].ToString();
                            string data = "";
                            OffLineBinList ol = new OffLineBinList(id, name, "离线");
                            offlinebinlist.Add(ol);
                            data = Data.Data(factory, id, "00", "0000");
                            send_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data, s_Produce));
                            //并获取新添加的边距，顶高，轴距参数信息。。。判断是否被修改
                            Log("asdadsasda");
                        }
                        rd.Close();
                        ms.Close();
                    }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                    catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
                    {
                        //Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));//添加错误日志
                        //thread_file.Start(exc.ToString());
                        Log("查询料仓错误!!!!!");
                    }
                    FacMessage ele;
                    while (send_queue.Count != 0)
                    {
                        Log("执行了查询料仓的指令传递工作");
                        ele = send_queue.Dequeue();
                        sendIns_queue.Enqueue(ele);
                    }
                    while (flag_threadout == 1)
                    {
                        //当数据库中的料仓数量大小等于查询完状态的料仓列表的时候，说明所有料仓的是否在线状态都已经查询完毕，可以将数据发送了
                        if (sendbinlist.Count == allBinNum)
                        {
                            for (int i = 0; i < sendbinlist.Count; i++)
                            {
                                Log("在线的料仓的id是" + sendbinlist[i].bid + "名字是" + sendbinlist[i].bname + ",状态是" + sendbinlist[i].bstate);
                                //将查到的数据组合成指令一起发送给用户
                            }
                            //将sendbinlist进行排序
                            List<Rebinlist> infolist = new List<Rebinlist>();
                            //排好的列表
                            infolist = sortBinList(sendbinlist);
                            List<string> names = new List<string>();

                            //新建一个集合用于放不重复的id。1、循环可能包含重复的id字段，其中再循环新集合，若这个id没有在新集合中，就添加到新集合里。2、用新集合的id去中原始数据，找到一个输出一个
                            foreach (var infol in infolist)
                            {
                                string bname = infol.bname;
                                Boolean b = false;
                                if (names.Count == 0)

                                {
                                    names.Add(bname);
                                }
                                else
                                {
                                    foreach (var name in names)
                                    {
                                        if (name.Equals(bname))
                                        {
                                            b = true;
                                        }

                                    }
                                    if (!b)//数组中没有的时候，就添加
                                    {
                                        names.Add(bname);
                                    }
                                }

                            }
                            List<Rebinlist> finalList = new List<Rebinlist>();
                            foreach (var name in names)
                            {
                                Log("将要发送的id是" + name);
                                foreach (var send in infolist)
                                {
                                    if (name.Equals(send.bname))
                                    {
                                        finalList.Add(send);
                                        break;
                                    }
                                }
                            }
                            //将料仓对象列表，序列化成json数据
                            string binlist = JsonConvert.SerializeObject(finalList);
                            SendJsonInfo sendJif = new SendJsonInfo("success", "win/" + mqCode, "responseOnLineList", binlist);//
                            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                            string info = jif.getUserinfo() + "+" + sendMqttInfo;
                            Thread thread_mqtt = new Thread(new ParameterizedThreadStart(SendMqtt));
                            thread_mqtt.Start(info);
                            break;
                        }
                    }
                }
            }
        }
        //线程开启，直到数据库查询的所有的信息都查询完后，再跳出循环，向mqtt发送信息
        //根据分组来获取料仓在线信息
        private void getBinlistByGroup(object obj)
        {

            Log("根据分组id获取组内料仓信息");
            lock (locker)
            {
                JsonInfo jif = (JsonInfo)obj;

                string Gid = jif.getData();//获取用户传递过来的组号 ，为一个数字


                if (Gid == "" || Gid.Equals("null"))
                {
                    Log("网络请求的id是空的");
                    return;
                }

                Log("截取的Gid是" + Gid + "\r\n");


                //1、根据组号查询数据库，找出分组下的料仓信息
                //2、发送是否在线指令
                //3、返回在线数组
                sendbinlist.Clear();
                offlinebinlist.Clear();
                if (factory.Equals("") != true)//如果有厂区码来了，就查料仓是否在线
                {
                    string sql = "select * from bininfo where Gid = " + Gid;//查询料仓信息表
                                                                            //DataBase db;
                    Queue<FacMessage> send_queue = new Queue<FacMessage>();
                    try
                    {
                        //DataBase db = new DataBase();
                        //db.command.CommandText = sql;
                        //db.command.Connection = db.connection;
                        //db.Dr = db.command.ExecuteReader();
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        allBinNum = 0;
                        while (rd.Read())
                        {//显示列表，向存在于数据库中所有的料仓发送"00"号指令，检测料仓是否存在
                            //计数器加1
                            allBinNum++;
                            Thread.Sleep(10);
                            string id = rd["BinID"].ToString();//获取到了料仓的id
                            string name = rd["BinName"].ToString();
                            string data = "";
                            OffLineBinList ol = new OffLineBinList(id, name, "离线");
                            offlinebinlist.Add(ol);
                            data = Data.Data(factory, id, "00", "0000");
                            send_queue.Enqueue(new FacMessage(ins_num++, "01", id, false, TIME, "查询测试/添加料仓功能", data, s_Produce));
                            //并获取新添加的边距，顶高，轴距参数信息。。。判断是否被修改
                            Log("asdadsasda");
                        }
                        rd.Close();
                        ms.Close();
                    }
#pragma warning disable CS0168 // 声明了变量“exc”，但从未使用过
                    catch (Exception exc)
#pragma warning restore CS0168 // 声明了变量“exc”，但从未使用过
                    {
                        //Thread thread_file = new Thread(new ParameterizedThreadStart(method_file));//添加错误日志
                        //thread_file.Start(exc.ToString());
                        Log("查询料仓错误!!!!!");
                    }
                    FacMessage ele;
                    while (send_queue.Count != 0)
                    {
                        Log("执行了查询料仓的指令传递工作");
                        ele = send_queue.Dequeue();
                        sendIns_queue.Enqueue(ele);
                    }
                    while (flag_threadout == 1)
                    {
                        //Log("***************跑线程");
                        //当数据库中的料仓数量大小等于查询完状态的料仓列表的时候，说明所有料仓的是否在线状态都已经查询完毕，可以将数据发送了
                        if (sendbinlist.Count == allBinNum)
                        {
                            //Log("***************组装数据");
                            for (int i = 0; i < sendbinlist.Count; i++)
                            {
                                Log("在线的料仓的id是" + sendbinlist[i].bid + "名字是" + sendbinlist[i].bname + ",状态是" + sendbinlist[i].bstate);
                                //将查到的数据组合成指令一起发送给用户
                            }
                            //将sendbinlist进行排序
                            List<Rebinlist> infolist = new List<Rebinlist>();
                            //排好的列表
                            infolist = sortBinList(sendbinlist);
                            List<string> names = new List<string>();

                            //新建一个集合用于放不重复的id。1、循环可能包含重复的id字段，其中再循环新集合，若这个id没有在新集合中，就添加到新集合里。2、用新集合的id去中原始数据，找到一个输出一个
                            foreach (var infol in infolist)
                            {
                                string bname = infol.bname;
                                Boolean b = false;
                                if (names.Count == 0)
                                {
                                    names.Add(bname);
                                }
                                else
                                {
                                    foreach (var name in names)
                                    {
                                        if (name.Equals(bname))
                                        {
                                            b = true;
                                        }

                                    }
                                    if (!b)//数组中没有的时候，就添加
                                    {
                                        names.Add(bname);
                                    }
                                }

                            }
                            List<Rebinlist> finalList = new List<Rebinlist>();
                            foreach (var name in names)
                            {
                                Log("将要发送的id是" + name);
                                foreach (var send in infolist)
                                {
                                    if (name.Equals(send.bname))
                                    {
                                        finalList.Add(send);
                                        break;
                                    }
                                }
                            }
                            //将料仓对象列表，序列化成json数据
                            string binlist = JsonConvert.SerializeObject(finalList);
                            SendJsonInfo sendJif = new SendJsonInfo(Gid, "win/" + mqCode, "responseOnLineList", binlist);//
                            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                            string info = jif.getUserinfo() + "+" + sendMqttInfo;
                            Thread thread_mqtt = new Thread(new ParameterizedThreadStart(SendMqtt));
                            thread_mqtt.Start(info);
                            break;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 远程垂直校准
        /// </summary>
        /// <param name="obj"></param>
        private void setWarehouseJiaozhun(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            int flag = 0;
            try
            {
                string data = jif.getData();
                //取出用户发送的料仓id
                string[] ids = data.Split(',');
                string xiaoshu = "";
                //Log("IDIDids[0]" + ids[0]);
                JID = ids[0];

                string info = jif.getUserinfo();
                TOPIC = info;//保存topic都全局变量
 
                string fuhao = ids[1];//符号
                string shuju = ids[2];//数据

                //Log("符号ids[1]" + ids[1] + "数据ids[2]" + ids[2]);

                double d = double.Parse(ids[2]);
                int a = (int)Math.Floor(d);//整数部分
                string zhengshu = Convert.ToString(a, 16);//整数十六进制

                if (ids[2].Length == 4)
                {
                    xiaoshu = "0" + ids[2][3];//小数十六进制
                }
                else if (ids[2].Length == 2)
                {
                    xiaoshu = "00";//小数十六进制
                }
                else if (ids[2].Length == 3)
                {
                    xiaoshu = "0" + shuju[2];
                }

                    if (zhengshu.Length == 1)
                {
                    zhengshu = "0" + zhengshu;
                }

                //Log("zhengshu" + zhengshu + "xiaoshu"+xiaoshu);

                String test = zhengshu+xiaoshu;
                //String test = "0000";


                if (ids[1].Equals("0"))//正的角度
                {
                    if (flag == 0)
                    {

                        string data1 = Data.DataGetCAnangle(factory, JID, "46", test);
       
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "2F", JID, false, 3, "远程垂直校准", data1, s_Produce));//垂直角
                        flag = 1;
                    }
                }
                else if (ids[1].Equals("1"))//负的角度
                {
                    if (flag == 0)
                    {
                        //Log("执行负的角度");
                        string data1 = Data.DataGetCAnangleF(factory, JID, "46", test);
                        //Log("test" + test);
                        //Log("指令" + data1);
                        FacMessage facmes = new FacMessage(ins_num++, "2F", JID, false, TIME, "远程垂直校准", data1, 3, s_Produce);
                        sendIns_queue.Enqueue(facmes);
                        //serialPort_WriteLine(facmes);
                        flag = 1;
                    }
                }

  

            }
            catch (Exception e)
            {
                Log("垂直校准失败" + e.ToString());
            }

        }
        /// <summary>
        /// 获取垂直校准角度
        /// </summary>
        /// <param name="obj"></param>
        private void getWarehouseJiaozhun(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            try
            {
                string data = jif.getData();
                string[] ids = data.Split(',');
                Log("ids[0]" + ids[0]);
                JID = ids[0];
                string info = jif.getUserinfo();
                TOPIC = info;//保存topic都全局变量


                string d = Data.DataGetCAnangle(factory, JID, "14", "0000");
                //serialPort_WriteLine(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                sendIns_queue.Enqueue(new FacMessage(ins_num++, "0F", JID, false, 3, "获取k,b,c", d, s_Produce));//垂直角



            }
            catch(Exception e)
            {
                Log("获取垂直角" + e.ToString());
            }

        }

        private void SendMqtt(object obj)//???发送指令
        {
            string info = (string)obj;
            string[] arr = info.Split('+');

            Log("将要发送指令，数据是" + arr[1]);
                    //将数据发送出去
            Log("将要数据发送给：" + arr[0]);
           
                client.Publish(arr[0], Encoding.UTF8.GetBytes(arr[1]));
                
            
           
        }

        //对获取的料仓进行以id大小的排序，输入获得的乱序的料仓列表，输出排好的列表
        private List<Rebinlist> sortBinList(List<Rebinlist> sendbinlist)
        {
            List<Rebinlist> infolist = new List<Rebinlist>();
            //排序
            List<string> id = new List<string>();
            for (int i = 0; i < sendbinlist.Count; i++)
            {
                id.Add(sendbinlist[i].bid);
            }
            id.Sort();

            for (int j = 0; j < sendbinlist.Count; j++)
            {
                for (int i = 0; i < sendbinlist.Count; i++)
                {
                    if (id[j] == sendbinlist[i].bid)
                    {
                        infolist.Add(sendbinlist[i]);
                    }
                }
            }
            return infolist;
        }
        //清洁镜头操作
        private void qingJie(object obj)
        {


            //sendPklist = new List<Rebinlist>();//新建数组用于存放盘库的数据
            JsonInfo jif = (JsonInfo)obj;
            try
            {
                //1、解析数据
                string data = jif.getData();
                PanKuNum = 0;//要盘库的个数
                //取出用户发送的料仓id
                string[] ids = data.Split(',');
                string[] bins = new string[ids.Count() - 1];
                //生成List<eqIdDo>
                List<eqIdDo> eqList = new List<eqIdDo>();

                sendIns_queue.Clear();
                oper_ins.Clear();
                list_status.Clear();

                for (int i = 0; i < ids.Count() - 1; i++)
                {
                    Log("用户传递的id是" + ids[i]);
                    //将id给新的数组
                    bins[i] = ids[i];
                    //eqIdDo eq = new eqIdDo(ids[i], "0");
                    //eqList.Add(eq);

                }

                string eqid = bins[0];//一次只能选一个设备
                string uid = jif.getUserinfo();
                Log("清洁镜头用户的id是：" + uid);

                if (binPkingList.Count == 0)
                {//当前没有在盘库的设备
                    Log("厂区没有设备在盘库或者清洁");
                    eqIdDo user = new eqIdDo(uid, eqid, "0");//添加一个用户，和设备id，将设备的属性变为0
                    binPkingList.Add(user);
                    Queue<FacMessage> ins_queue = new Queue<FacMessage>();
                    for (int i = bins.Length - 1; i >= 0; i--)
                    {
                        string id = bins[i];
                        string data_search = Data.Data(factory, id, "32", "0000");
                        string data1 = Data.Data(factory, id, "22", "0001");
                        aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除湿", data1, s_Produce));
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "镜头除湿前查询状态", data_search, 3, s_Produce));
                        //向发送链表中添加此指令，但是不发送这条指令，发送的是查询指令
                    } //end for
                      /////
                    //Thread sendMqttThread = new Thread(SendMqttPanKu);//发送指令线程.用于接收状态
                    //sendMqttThread.Start(jif);

                }
                else
                {//当前存在盘库的设备
                    Log("厂区有设备在盘库或者清洁");
                    Boolean isE = false;
                    foreach (var itme in binPkingList)//遍历看看是否有用户已经在盘库列表中
                    {
                        if ((itme.getUid().Equals(uid)))//用户在盘库列表中
                        {
                            Log("用户在列表中！！！！！！！！！！！" + "itme.getUid()"+ itme.getUid() +",uid"+ uid);
                            isE = true;
                        }
                    }
                    //如果盘库列表中有用户，但是并没有料仓在盘库，可能的问题是并没有将盘库信息发出。
                    if (CalcVol_list.Count == 0)
                    {
                        //直接发送盘库指令
                        Log("直接发送清洁指令，因为之前的料仓并没操作");
                        string id = eqid;
                        string data_search = Data.Data(factory, id, "32", "0000");
                        string data1 = Data.Data(factory, id, "22", "0001");
                        aim_ins.Enqueue(new FacMessage(0, "17", id, false, 6, "清洁镜头--除湿", data1, s_Produce));
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "镜头除湿前查询状态", data_search, 3, s_Produce));
                    }
                    else
                    {
                        if (!isE)
                        {//用户id不在盘库列表中
                            Log("用户id不在盘库、清洁列表中");
                            if (binPkingList[0].getEqid().Equals(eqid))//且请求的都是同一个设备
                            {
                                eqIdDo user = new eqIdDo(uid, eqid, "1");//添加一个用户，和设备id，将设备的属性变为0
                                binPkingList.Add(user);//添加用户，这样定时器就会向这个用户发送消息
                                Log("向binPkingList添加id");
                            }
                            else
                            {
                                //新用户请求新的设备，则通过mqtt告知，有设备在盘库，请稍后。
                                Log("在厂区中已经存在盘库、清洁的料仓。即将通知用户设备占用");
                                List<Rebinlist> infolist = new List<Rebinlist>();
                                string name = getName(eqid);
                                //排好的列表
                                Rebinlist bin = new Rebinlist(eqid, name, "设备被占用请稍后");
                                infolist.Add(bin);
                                //将料仓对象列表，序列化成json数据
                                string binlist = JsonConvert.SerializeObject(infolist);

                                SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                              //生成发送的json数据
                                string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                                Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                                //将数据发送出去

                                client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                            }
                        }
                        else//用户在请求列表中
                        {
                            //如果在列表中。不做操作
                            Log("此用户在盘库列表中,清洁镜头");

                            if (CalcVol_list[0].fac_num.Equals(eqid.PadLeft(2, '0')))//且请求的都是同一个设备
                            {

                            }
                            else
                            {
                                //用户在盘库列表中，但是请求的是新设备
                                Log("已经存在盘库的料仓");
                                List<Rebinlist> infolist = new List<Rebinlist>();
                                string name = getName(eqid);
                                //排好的列表
                                Rebinlist bin = new Rebinlist(eqid, name, "设备被占用请稍后");
                                infolist.Add(bin);
                                //将料仓对象列表，序列化成json数据
                                string binlist = JsonConvert.SerializeObject(infolist);

                                SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                              //生成发送的json数据
                                string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                                Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                                //将数据发送出去
                                client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                            }
                        }
                    }
                }

            }
            catch(Exception e)
            {
                Log("清洁镜头出错，错误：" + e.ToString());
            }
           
        }
        //线程开启，直到数据库查询的所有的信息都查询完后，再跳出循环，向mqtt发送信息

        private void SendMqttClear(object obj)//???发送指令
        {
            //Invoke(new MethodInvoker(delegate
            //{
            Log("盘库料仓列表大小" + sendPklist.Count + "\r\n");

            //}));
            JsonInfo jif = (JsonInfo)obj;
            while (true)//第一次加载
            {
                //当数据库中的料仓数量大小等于查询完状态的料仓列表的时候，说明所有料仓的是否在线状态都已经查询完毕，可以将数据发送了
                if (sendPklist.Count == PanKuNum)
                {
                    for (int i = 0; i < sendPklist.Count; i++)
                    {
                        //Invoke(new MethodInvoker(delegate
                        //{
                        Log("在线的料仓的id是" + sendPklist[i].bid + "名字是" + sendPklist[i].bname + ",状态是" + sendPklist[i].bstate + "\r\n");

                        //}));
                        //将查到的数据组合成指令一起发送给用户

                    }
                    //将sendbinlist进行排序
                    List<Rebinlist> infolist = new List<Rebinlist>();
                    //排好的列表
                    infolist = sortBinList(sendPklist);
                    //将料仓对象列表，序列化成json数据
                    string binlist = JsonConvert.SerializeObject(infolist);

                    SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                    //生成发送的json数据
                    string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                    Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                    //将数据发送出去
                    client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
                    break;
                }
            }



        }
        // 你想在定时器中执行的功能，例如我在这里向让他写个日志打印时间，日志的存储位置是"E:\\LoginFile.txt"
        private void timeToSendSchedule(object sender, System.Timers.ElapsedEventArgs e, JsonInfo jif)
        {
            if (ud.Count() == 0) return;
            foreach(UserDO udList in ud){
                string uid = udList.getUid();
                List<eqIdDo> eqlist = udList.getEqList();
                foreach(eqIdDo eq in eqlist)
                {
                    string eqid = eq.getEqid();
                    //string doid = eq.getDoid();

                  
                }


            }



            if (CalcVol_list.Count != 0)
            {
                if (it_oper >= CalcVol_list.Count)
                {//CalcVol_list的下标it_oper如果大于CalcVol_list的节点个数，就置为0
                    it_oper = 0;
                }
                //groupBox2.Visible = true;
                //if (CalcVol_list[it_oper].ins_num == 2)
                //{
                //    CalcVol_list[it_oper].life_time += 3;
                //}
                //for (int i = 0; i < CalcVol_list.Count; i++)
                //{//遍历找出清洁镜头的节点
                //    if (CalcVol_list[i].ins_num == 2)
                //        CalcVol_list[i].life_time += 3;
                //}
                string fac_num = CalcVol_list[it_oper].fac_num;
                int schedule = CalcVol_list[it_oper].life_time;//操作进度
                int OperType = CalcVol_list[it_oper].ins_num;//操作类型，1表示盘库，2表示清洁镜头
                string binname = getName(fac_num);
                if (schedule >= 100)
                {
                    schedule = 100;
                    CalcVol_list.RemoveAt(it_oper);

                   
                }

                List<Rebinlist> infolist = new List<Rebinlist>();
                //排好的列表
                //Log("CalcVol_list，数据大小是" + CalcVol_list.Count + "\r\n");
                //Log("sendPklist，数据大小是" + sendPklist.Count + "\r\n");
                if (CalcVol_list.Count != sendPklist.Count)//大小不相等说明有删除的
                {

                    for (int i = 0; i < sendPklist.Count; i++)
                    {
                        int have = 0;
                        for (int j = CalcVol_list.Count - 1; j >= 0; j--)
                        {
                            if (CalcVol_list[j].fac_num.Equals(sendPklist[i].bid.ToString().PadLeft(2, '0')))
                            {
                                have = 1;
                                break;
                            }

                        }

                        if (have == 0)
                        {
                            sendPklist[i].isChecked = true;
                        }


                    }

                }

                List<Rebinlist> data = new List<Rebinlist>();
                for (int i = 0; i < sendPklist.Count; i++)
                {
                    if (sendPklist[i].isChecked == false)
                    {
                        data.Add(sendPklist[i]);
                    }
                }

                for (int i = 0; i < data.Count; i++)//循环遍历正在盘库的料仓
                {//刷新状态


                    if (fac_num == data[i].bid.ToString().PadLeft(2, '0'))
                    {
                        data[i].bstate = "" + schedule;//将获得的这个料仓的进度进行赋值
                    }


                }

                infolist = sortBinList(data);
                //将料仓对象列表，序列化成json数据
                string binlist = JsonConvert.SerializeObject(infolist);
                //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "data是" + binlist + "    \r\n");//返回进度数据
                SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                              //生成发送的json数据
                string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                //richTextBox1.AppendText("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                //将数据发送出去
                client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
                Log("料仓" + binname + "进度是=" + schedule);//返回进度数据

                it_oper++;
            }
            else//当没有盘库内容的时候。不进行操作
            {
                List<Rebinlist> infolist = new List<Rebinlist>();
                string binlist = JsonConvert.SerializeObject(infolist);
                //当都盘库完成时，清空列表
                SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                              //生成发送的json数据
                string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                //Log("将要发送指令，数据是" + sendMqttInfo);
                //将数据发送出去
                client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
                //Log("盘库完成，无数据 \r\n");//返回进度数据

                timerPk.Stop();
                it_oper = 0;

                //groupBox2.Visible = false;
            }
        }


        private void panKu(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            string data = jif.getData();
            //取出用户发送的料仓id
            string[] ids = data.Split(',');
            string[] bins = new string[ids.Count() - 1];
            for (int i = 0; i < ids.Count() - 1; i++)
            {
                Log("用户传递的id是" + ids[i]);
                //将id给新的数组
                bins[i] = ids[i];//现在默认是1个了
            }
            pktime = 0;//从现在开始5s内没有开始盘库的话就清空用户列表
            string eqid = bins[0];
            string uid = jif.getUserinfo();
            Log("盘库用户的id是：" +uid );

            if (binPkingList.Count == 0)//保证客户端的盘库数量也能看见，
            {//当前没有在盘库的设备
                if(CalcVol_list.Count == 0)//手机端没有盘库的，pc端也没有有盘库的
                {
                    Log("厂区没有设备在盘库");
                    eqIdDo user = new eqIdDo(uid, eqid, "0");//添加一个用户，和设备id，将设备的属性变为0
                    
                    binPkingList.Add(user);
                    Queue<FacMessage> ins_queue = new Queue<FacMessage>();
                    //给每一个料仓循环发送查询状态请求
                    for (int i = bins.Length - 1; i >= 0; i--)
                    {
                        string id = bins[i];
                        string data_search = Data.Data(factory, id, "32", "0000");
                        string d = Data.Data(factory, id, "18", "0000");
                        aim_ins.Enqueue(new FacMessage(ins_num++, "13", id, false, 3, "盘库", d, s_Produce));
                        Log("用户发送盘库指令！！！！！！");
                        sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "手动盘库前查询状态", data_search, 3, s_Produce));
                        //向发送链表中添加此指令，但是不发送这条指令，发送的是查询指令
                    } //end for
                }
                else//手机端没有盘库的，pc端有盘库的。一个时间段中只能有一个料仓在盘库
                {
                    Boolean isE = false;
                    if (!isE)
                    {//用户id不在盘库列表中
                        Log("手机用户发送盘库请求");
                        if (CalcVol_list[0].fac_num.Equals(eqid.PadLeft(2, '0')))//且请求的都是同一个设备
                        {
                            eqIdDo user = new eqIdDo(uid, eqid, "1");//添加一个用户，和设备id，将设备的属性变为0
                            binPkingList.Add(user);//添加用户，这样定时器就会向这个用户发送消息
                            Log("向binPkingList添加id");
                        }
                        else
                        {
                            //新用户请求新的设备，则通过mqtt告知，有设备在盘库，请稍后。
                            Log("已经存在盘库的料仓");
                            List<Rebinlist> infolist = new List<Rebinlist>();
                            string name = getName(eqid);
                            //排好的列表
                            Rebinlist bin = new Rebinlist(eqid, name, "设备被占用请稍后");
                            infolist.Add(bin);
                            //将料仓对象列表，序列化成json数据
                            string binlist = JsonConvert.SerializeObject(infolist);

                            SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                          //生成发送的json数据
                            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                            Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                            //将数据发送出去
                            client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                        }
                    }
                    else
                    {
                        //如果在列表中。不做操作
                    }
                }
            }
            else
            {//当前用户列表中有用户。
                Log("厂区有移动端用户正在盘库");
                Boolean isE = false;
                foreach (var itme in binPkingList)//遍历看看是否有用户已经在盘库列表中
                {
                    if ((itme.getUid().Equals(uid)))//用户在盘库列表中
                    {
                        //itme.getUid()正在盘库的用户ID,和想要盘库的用户ID uid
                        Log("用户在列表中！！！！！！！！！！！" + "itme.getUid()" + itme.getUid() + ",uid" + uid);
                        isE = true;
                    }
                }
                //如果盘库列表中有用户，但是并没有料仓在盘库，可能的问题是并没有将盘库信息发出。
                if(CalcVol_list.Count == 0)//!!!!!!!!!是不是要清空一下binPkingList？！！！！！！！！！！！
                {
                    //直接发送盘库指令
                    Log("直接发送盘库指令，因为之前的料仓并没盘");
                    string id = eqid;
                    string data_search = Data.Data(factory, id, "32", "0000");
                    string d = Data.Data(factory, id, "18", "0000");
                    aim_ins.Enqueue(new FacMessage(ins_num++, "13", id, false, 3, "盘库", d, s_Produce));
                    Log("用户发送盘库指令！！！！！！");
                    //Log("手动盘库前查询状态！！！！！！");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "手动盘库前查询状态", data_search, 3, s_Produce));
                }



                if (!isE)//进入这段逻辑就是不同的用户要操作
                {//用户id不在盘库列表中
                    Log("用户id不在盘库列表中");
                    if (binPkingList[0].getEqid().Equals(eqid))//且请求的都是同一个设备
                    {
                        eqIdDo user = new eqIdDo(uid, eqid, "1");//添加一个用户，和设备id，将设备的属性变为0
                        binPkingList.Add(user);//添加用户，这样定时器就会向这个用户发送消息
                        Log("向binPkingList添加新用户id");
                    }
                    else//不同的用户请求不同的设备
                    {
                        //新用户请求新的设备，则通过mqtt告知，有设备在盘库，请稍后。
                        Log("已经存在盘库的料仓");
                        List<Rebinlist> infolist = new List<Rebinlist>();
                        string name = getName(eqid);
                        //排好的列表
                        Rebinlist bin = new Rebinlist(eqid, name, "设备被占用请稍后");
                        infolist.Add(bin);
                        //将料仓对象列表，序列化成json数据
                        string binlist = JsonConvert.SerializeObject(infolist);

                        SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                      //生成发送的json数据
                        string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                        Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                        //将数据发送出去
                        client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                    }
                }
                else
                {
                    //如果在列表中。要判断用户请求的料仓是否和正在盘库的料仓一样。
                    //一样不操作，不一样，提示用户请求的料仓不能盘库
                    if (CalcVol_list[0].fac_num.Equals(eqid.PadLeft(2, '0')) && (CalcVol_list.Count !=0))//且请求的都是同一个设备
                    {

                    }
                    else
                    {
                        //用户在盘库列表中，但是请求的是新设备
                        Log("已经存在盘库的料仓");
                        List<Rebinlist> infolist = new List<Rebinlist>();
                        string name = getName(eqid);
                        //排好的列表
                        Rebinlist bin = new Rebinlist(eqid, name, "设备被占用请稍后");
                        infolist.Add(bin);
                        //将料仓对象列表，序列化成json数据
                        string binlist = JsonConvert.SerializeObject(infolist);

                        SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                      //生成发送的json数据
                        string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                        Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                        //将数据发送出去
                        client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                    }


                }

            }
            Log("完成盘库按钮操作！！！！！！！");
        }


        ////mqtt盘库操作
        //private void panKu(object obj)
        //{
        //    JsonInfo jif = (JsonInfo)obj;

        //    sendPklist = new List<Rebinlist>();//新建数组用于存放盘库的数据
        //    PanKuNum = 0;//要盘库的个数
        //    //1、解析数据
        //    string data = jif.getData();
        //    //取出用户发送的料仓id
        //    string[] ids = data.Split(',');
        //    string[] bins = new string[ids.Count() - 1];
        //    for (int i = 0; i < ids.Count() - 1; i++)
        //    {
        //        Log("用户传递的id是" + ids[i] );
        //        //将id给新的数组
        //        bins[i] = ids[i];
        //    }

        //    sendIns_queue.Clear();
        //    oper_ins.Clear();
        //    list_status.Clear();
        //    //加上先发送指令查询料仓设备状态，然后检索当前状态列表
        //    PanKuNum = bins.Count();
        //    Log("要盘库的个数是" + PanKuNum);
        //    //发送指令开始盘库
        //    Queue<FacMessage> ins_queue = new Queue<FacMessage>();
        //    for (int i = bins.Length - 1; i >= 0; i--)
        //    {

        //        string id = bins[i];
        //        string data_search = Data.Data(factory, id, "32", "0000");
        //        string d = Data.Data(factory, id, "18", "0000");
        //        aim_ins.Enqueue(new FacMessage(ins_num++, "13", id, false, 3, "盘库", d, s_Produce));
        //        sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "手动盘库前查询状态", data_search, 3, s_Produce));
        //        //向发送链表中添加此指令，但是不发送这条指令，发送的是查询指令

        //    } //end for

        //    Thread sendMqttThread = new Thread(SendMqttPanKu);//发送指令线程.用于介绍状态
        //    sendMqttThread.Start(jif);

        //}

        //线程开启，直到数据库查询的所有的信息都查询完后，再跳出循环，向mqtt发送信息

        private void SendMqttPanKu(object obj)//???发送指令
        {
            //Invoke(new MethodInvoker(delegate
            //{
                Log("盘库料仓列表大小" + sendPklist.Count + "\r\n");

            //}));
            JsonInfo jif = (JsonInfo)obj;
            while (true)//第一次加载
            {
                //当数据库中的料仓数量大小等于查询完状态的料仓列表的时候，说明所有料仓的是否在线状态都已经查询完毕，可以将数据发送了
                if (sendPklist.Count == PanKuNum)
                {
                    for (int i = 0; i < sendPklist.Count; i++)
                    {
                        //Invoke(new MethodInvoker(delegate
                        //{
                            Log("在线的料仓的id是" + sendPklist[i].bid + "名字是" + sendPklist[i].bname + ",状态是" + sendPklist[i].bstate + "\r\n");

                        //}));
                        //将查到的数据组合成指令一起发送给用户

                    }
                    //将sendbinlist进行排序
                    List<Rebinlist> infolist = new List<Rebinlist>();
                    //排好的列表
                    infolist = sortBinList(sendPklist);
                    //将料仓对象列表，序列化成json数据
                    string binlist = JsonConvert.SerializeObject(infolist);

                    SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                    //生成发送的json数据
                    string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                    Log("将要发送指令，数据是" + sendMqttInfo + "\r\n");
                    //将数据发送出去
                    client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
                    break;
                }
            }
        }
        //五秒钟运行一次的定时器,用于在用户进行盘库时向用户客户端发送盘库的进度
        private void timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //if (wdAndBack == 1)//如果在请求温湿度+回传数据的时候，并没有信息，他就会一直是1，这个时候需要定时器来帮助他变为0，保证在单独请求温湿度的时候，不需要回传数据
            //{
            //    wdAndBackNum--;
            //    Log("timer1_Elapsed=。wdAndBack= " + wdAndBack + "；wdAndBackNum：" + wdAndBackNum);
            //    if (wdAndBackNum <= 0)
            //    {
            //        wdAndBack = 0;
            //        wdAndBackNum = 3;
            //        Log("将温度的标志符改为。wdAndBack：" + wdAndBack);
            //    }
               
            //}
            //Log("执行定时器。binPkingList的长度===" + binPkingList.Count);
            //Log("执行定时器。CalcVol_list的长度===" + CalcVol_list.Count);
            if (binPkingList.Count != 0)//有盘库的料仓
            {
                foreach(var itme in binPkingList)//找出正在盘库的料仓用户
                {
                    string uid = itme.getUid();//远程控制用户的ID，订阅的主题 WebClient/U5MTU3MDE
                    string eqid = itme.getEqid().PadLeft(2, '0');//厂区码都是03
                    string binname = getName(eqid);
                    Log("binPkingList中的一个uid : " + uid+", 他查询的设备id：" + eqid);

                    Log("查询CalcVol_list的大小：" + CalcVol_list.Count);
                    for (int i = 0; i < CalcVol_list.Count; i++)
                    {//遍历找出清洁镜头的节点
                        if (CalcVol_list[i].ins_num == 2)//2表示清洁镜头
                        {
                            CalcVol_list[i].life_time += 3;
                        }
                           
                        if (CalcVol_list[i].life_time >= 100)//进度大于100
                        {
                            
                            CalcVol_list[i].life_time = 100;
                        }
                    }
                    Log("执行CalcVol_list的遍历");
                    for (int i = 0; i < CalcVol_list.Count; i++)
                    {//
                        calcVolChange = 1;//从无到有了
                        Log("CalcVol_list[i].fac_num:" + CalcVol_list[i].fac_num + "eqid：" + eqid);
                        if (CalcVol_list[i].fac_num.Equals(eqid))//如果进度中的id与用户请求的id相同
                        {

                            //发送mqtt给这个uid用户，说状态

                            //将sendbinlist进行排序
                            List<Rebinlist> infolist = new List<Rebinlist>();
                            Log("有CalcVol_list时。binPkingList中的一个uid：" + uid + ",设备id：" + eqid + "   进度"+ CalcVol_list[i].life_time);
                            string inforstr = "";//要传递的进度或者传递的错误。
                            //排好的列表
                            if(CalcVol_list[i].life_time == -4)
                            {
                                inforstr = "与测量设备485通讯失败";
                                CalcVol_list[i].life_time = 200;//为后续删除节点
                            }
                            else if(CalcVol_list[i].life_time == -5)
                            {
                                inforstr = "与测量设备485通讯失败";
                                CalcVol_list[i].life_time = 200;//为后续删除节点
                            }
                            else if(CalcVol_list[i].life_time == -6)
                            {
                                inforstr = "设备被取消盘库";
                                CalcVol_list[i].life_time = 200;//为后续删除节点
                            }
                            else
                            {
                                inforstr = "" + CalcVol_list[i].life_time;
                            }
                            Rebinlist r = new Rebinlist(eqid, binname, inforstr);
                            infolist.Add(r);
                            //将料仓对象列表，序列化成json数据
                            string binlist = JsonConvert.SerializeObject(infolist);

                            SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                          //生成发送的json数据
                            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                            Log("定时器将要发送指令，数据是" + sendMqttInfo + "\r\n");
                            //将数据发送出去
                            Log("发送进度的uid" + uid);
                            client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));
                        }
                    }

                    if (CalcVol_list.Count == 0 && calcVolChange == 1)//当盘库结束CalcVol_list会自动remove。当长度为0。。且是刚刚有盘库的内容，但因为盘库完成删除了的时候。说明这个料仓没有设备了，binPkingList也要remove
                    {
                        List<Rebinlist> infolist = new List<Rebinlist>();
#pragma warning disable CS0219 // 变量“isstopFlag”已被赋值，但从未使用过它的值
                        int isstopFlag = 0;
#pragma warning restore CS0219 // 变量“isstopFlag”已被赋值，但从未使用过它的值
                        int id = int.Parse(eqid);
                        try
                        {
                            //isstopFlag = isStop[id];
                        }
#pragma warning disable CS0168 // 声明了变量“ee”，但从未使用过
                        catch (Exception ee)
#pragma warning restore CS0168 // 声明了变量“ee”，但从未使用过
                        {
                            Log(id +" 不在取消盘库的状态下");
                        }
                        Rebinlist r;
                    

                        Log("CalcVol_list个数是0，且是从在线操作状态到完成状态。。。。binPkingList中的一个uid" + uid + ",设备id：" + eqid + "   进度应该是最后的100%");
                        //排好的列表
                        r = new Rebinlist(eqid, binname, "" + 100);

                        infolist.Add(r);
                        //将料仓对象列表，序列化成json数据
                        string binlist = JsonConvert.SerializeObject(infolist);

                        SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                                                                                                                      //生成发送的json数据
                        string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                        Log("定时器将要发送指令，数据是" + sendMqttInfo + "\r\n");
                        //将数据发送出去
                        client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));

                    }

                }
                if (CalcVol_list.Count == 0 && calcVolChange == 1)//等于100的都发完以后，清除
                {
                    binPkingList.Clear();//清除所有的
                    Log("binPkingList！！！！！！！！！！！！！！！！！清除所有的信息");
                    Log("calcVolChange设置成0");
                    calcVolChange = 0;//这个时候已经没有数据
                }
                else
                {
                    //删除节点操作
                    for (int i = CalcVol_list.Count-1 ; i >= 0; i--)
                    {
                        if (CalcVol_list[i].life_time >= 100)//如果进度中的id与用户请求的id相同
                        {
                            for (int j = binPkingList.Count - 1; j >= 0; j--)
                            {
                                if (binPkingList[j].getEqid().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))//删除已经是100 的binPking
                                {
                                    binPkingList.RemoveAt(j);
                                }
                            }
                            Log("删除了CalcVol_list节点");
                            CalcVol_list.RemoveAt(i);
                            Log("calcVolChange设置成0");
                            calcVolChange = 0;//这个时候已经没有数据
                        }
                    }
                }
                pktime++;//

                if (CalcVol_list.Count == 0 && pktime >5)
                {
                    Log("binPkingList！！！！！！！！！！！！！！！！！清除所有的信息");
                    binPkingList.Clear();
                }
                //如果binPkListing中有数据，但是盘库列表中没有数据超过5s，就需要清空binPkinglist

            }
            else
            {
                //Log("binPkingList是空的");
                //删除节点操作
                for (int i = 0; i < CalcVol_list.Count; i++)
                {
                    if (CalcVol_list[i].life_time >= 100)//如果进度中的id与用户请求的id相同
                    {
                        //发送mqtt给这个uid用户，说状态

                        for (int j = binPkingList.Count - 1; j >= 0; j--)//集合需要从后往前删除
                        {
                            if (binPkingList[j].getEqid().PadLeft(2, '0').Equals(CalcVol_list[i].fac_num))//删除已经是100 的binPking
                            {
                                binPkingList.RemoveAt(j);
                            }
                        }
                        Log("删除了CalcVol_list节点");

                        CalcVol_list.RemoveAt(i);
                        Log("calcVolChange设置成0");
                        calcVolChange = 0;//这个时候已经没有数据
                    }
                }
            }
        }
        //// 你想在定时器中执行的功能，例如我在这里向让他写个日志打印时间，日志的存储位置是"E:\\LoginFile.txt"
        //private void timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e, JsonInfo jif)
        //{
        //    if (isPk !=0 )
        //    {
        //        if (CalcVol_list.Count != 0)
        //        {
        //            if (it_oper >= CalcVol_list.Count)
        //            {//CalcVol_list的下标it_oper如果大于CalcVol_list的节点个数，就置为0
        //                it_oper = 0;
        //            }
        //            //groupBox2.Visible = true;
        //            //if (CalcVol_list[it_oper].ins_num == 2)
        //            //{
        //            //    CalcVol_list[it_oper].life_time += 3;
        //            //}
        //            for (int i = 0; i < CalcVol_list.Count; i++)
        //            {//遍历找出清洁镜头的节点
        //                if (CalcVol_list[i].ins_num == 2)
        //                    CalcVol_list[i].life_time += 10;
        //            }
        //            string fac_num = CalcVol_list[it_oper].fac_num;
        //            int schedule = CalcVol_list[it_oper].life_time;//操作进度
        //            int OperType = CalcVol_list[it_oper].ins_num;//操作类型，1表示盘库，2表示清洁镜头
        //            string binname = getName(fac_num);
        //            if (schedule >= 100)
        //            {
        //                schedule = 100;
        //                CalcVol_list.RemoveAt(it_oper);
        //                sendPklist.RemoveAt(it_oper);
        //            }

        //            List<Rebinlist> infolist = new List<Rebinlist>();
        //            //排好的列表
        //            //Log("CalcVol_list，数据大小是" + CalcVol_list.Count + "\r\n");
        //            //Log("sendPklist，数据大小是" + sendPklist.Count + "\r\n");
        //            if (CalcVol_list.Count != sendPklist.Count)//大小不相等说明有删除的
        //            {

        //                for (int i = 0; i < sendPklist.Count; i++)
        //                {
        //                    int have = 0;
        //                    for (int j = CalcVol_list.Count - 1; j >= 0; j--)
        //                    {
        //                        if (CalcVol_list[j].fac_num.Equals(sendPklist[i].bid.ToString().PadLeft(2, '0')))
        //                        {
        //                            have = 1;
        //                            break;
        //                        }

        //                    }

        //                    if (have == 0)
        //                    {
        //                        sendPklist[i].isChecked = true;
        //                    }


        //                }

        //            }

        //            List<Rebinlist> data = new List<Rebinlist>();

        //            for (int i = 0; i < sendPklist.Count; i++)
        //            {
        //                if (sendPklist[i].isChecked == false)
        //                {
        //                    data.Add(sendPklist[i]);
        //                }
        //            }

        //            for (int i = 0; i < data.Count; i++)
        //            {//刷新状态
        //                if (fac_num == data[i].bid.ToString().PadLeft(2, '0'))
        //                {
        //                    data[i].bstate = "" + schedule;
        //                }
        //            }

        //            infolist = sortBinList(data);
        //            //将料仓对象列表，序列化成json数据
        //            string binlist = JsonConvert.SerializeObject(infolist);
        //            //richTextBox1.AppendText(DateTime.Now.ToString("G") + "\r\n" + "data是" + binlist + "    \r\n");//返回进度数据
        //            SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
        //                                                                                                          //生成发送的json数据
        //            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
        //            //richTextBox1.AppendText("将要发送指令，数据是" + sendMqttInfo + "\r\n");
        //            //将数据发送出去
        //            client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
        //            Log("料仓" + binname + "进度是=" + schedule);//返回进度数据
        //            //发送进度
        //            it_oper++;
        //        }
        //        else//当没有盘库内容的时候。不进行操作
        //        {

        //            List<Rebinlist> infolist = new List<Rebinlist>();
        //            string binlist = JsonConvert.SerializeObject(infolist);
        //            //当都盘库完成时，清空列表
        //            SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
        //                                                                                                          //生成发送的json数据
        //            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
        //            //Log("将要发送指令，数据是" + sendMqttInfo);
        //            //将数据发送出去
        //            client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
        //            //Log("盘库完成，无数据 \r\n");//返回进度数据

        //            timerPk.Stop();
        //            isPk = 0;//停止定时器
        //            it_oper = 0;
        //            //groupBox2.Visible = false;
        //        }
        //    }

        //}

        private void timer1_Tick(object sender, EventArgs e)
        {
            //new Thread(checkBinState).Start();
        }
        private void checkBinStateTheah(object sender, System.Timers.ElapsedEventArgs e)//判断是否超时的定时器
        {
            new Thread(checkBinState).Start();
        }
        private void connectMqt(object e)//连接mqtt的定时器。运行中检查网络连接。每隔30s进行一次网络连接检查。
        {
            while (true)
            {
                Thread.Sleep(30000);
                //Log("30s进行网络连接测试");
                try
                {
                    //Log("ping进行测试");
                    reply = null;
                    reply = pingSender.Send(MQTT_BROKER_ADDRESS);//ping服务器网址
                   
                }
                catch (Exception ee)
                {
                    Log("connectMqt 错误=" + ee.ToString());
                }
                finally
                {
                    if (reply == null || (reply != null && reply.Status != IPStatus.Success))
                    {
                        Log("无法连接该网站,请检查网络!，设置isReConnect = 1");
                        isReConnect = 1;
                        toConnectMqtt = false;
                    }
                    else if (reply.Status == IPStatus.Success)
                    {
                        if (isReConnect == 1)
                        {
                            Log("网络连接回复");
                            toConnectMqtt = true;
                            isReConnect = 0;
                        }
                        else
                        {
                            //Log("网络是通畅的。。。。。，也没有断网");
                        }
                    }
                    else
                    {
                        Log("另一种-----无法连接该网站,请检查网络!，设置isReConnect = 1");
                        isReConnect = 1;
                        toConnectMqtt = false;
                    }

                }
                //从无网络的状态到有网络的状态时进行一次连接
                if (toConnectMqtt)//
                {
                    //开启mqtt连接
                    try
                    {
                        Log("定时器连接mqtt！！！！！！！！！！！！！！！！！！！,先关闭");
                        //client.Disconnect();
#pragma warning disable CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                        client = new MqttClient(System.Net.IPAddress.Parse(MQTT_BROKER_ADDRESS));
#pragma warning restore CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                        MQTTtest();
                        toConnectMqtt = false;
                    }
                    catch (Exception ea)
                    {
                        toConnectMqtt = false;
                        Log("断开mqtt出错+" +ea.ToString());
                        //如果已经关闭，则直接进行连接mqtt
#pragma warning disable CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                        client = new MqttClient(System.Net.IPAddress.Parse(MQTT_BROKER_ADDRESS));
#pragma warning restore CS0618 // “MqttClient.MqttClient(IPAddress)”已过时:“Use this ctor MqttClient(string brokerHostName) insted”
                        MQTTtest();

                    }
                    finally
                    {
                        toConnectMqtt = false;
                    }
                }
            }
            

        }


        //private void connectMqt(object sender, System.Timers.ElapsedEventArgs e)//连接mqtt的定时器。运行中检查网络连接
        //{


        //    try
        //    {
        //        reply = null;
        //        reply = pingSender.Send(MQTT_BROKER_ADDRESS);
        //    }
        //    catch (Exception)
        //    {
        //    }
        //    finally
        //    {
        //        if (reply == null || (reply != null && reply.Status != IPStatus.Success))
        //        {
        //            Log("无法连接该网站,请检查网络!");
        //            isReConnect = 1;
        //            toConnectMqtt = false;
        //        }
        //        else if (reply.Status == IPStatus.Success)
        //        {
        //            if (isReConnect == 1)
        //            {
        //                Log("网络连接回复");
        //                toConnectMqtt = true;
        //                isReConnect = 0;
        //            }

        //        }

        //    }
        //    //从无网络的状态到有网络的状态时进行一次连接
        //    if (toConnectMqtt)//
        //    {
        //        //开启mqtt连接
        //        try
        //        {
        //            Log("定时器连接mqtt！！！！！！！！！！！！！！！！！！！");
        //            //client.Disconnect();
        //            client = new MqttClient(System.Net.IPAddress.Parse(MQTT_BROKER_ADDRESS));
        //            MQTTtest();
        //            toConnectMqtt = false;
        //        }
        //        catch (Exception ea)
        //        {
        //            Log("断开mqtt出错");
        //        }
        //    }




        //}
        private void checkBinState(object e)
        {
           
            if (send_ins.Count == 0)
                return;
           // Log("send_ins的长度==" + send_ins.Count);
            for (int i = send_ins.Count - 1; i >= 0; i--)
            {
                send_ins[i].life_time--;
                Log("send_ins[i].life_time !!!!!!==" + send_ins[i].life_time);
                if (send_ins[i].life_time <= 0)
                {//如果到达20分钟，则盘库超时
                    string name = getName(send_ins[i].fac_num);
                    //comboBox3.Items.Remove(name);
                    Log("盘库超时");
                    
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
                    string data = Data.Data(factory, id, "30", "0000");
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "1F", id, false, TIME, "取消当前操作", data, 2, s_Produce));

                    string searchTemp = Data.Data(factory, id, "16", "0000");
                    //serialPort_WriteLine(new FacMessage(ins_num++, "11", id, false, TIME+5, "查询温湿度", data));
                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "11", id, false, TIME, "查询温湿度", searchTemp, s_Produce));

                    send_ins.RemoveAt(i);
            
                    DateTime now = DateTime.Now;
                    string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

         

                    //超时信息存日志表
                    //DataBase dbLog = new DataBase();
                    string sql = "insert into binlog values('" + id + "', '盘库超时', '" + "TimeOut" + "', '盘库超时', '" + time + "')";
                    MySqlConn ms = new MySqlConn();
                    int isR = ms.nonSelect(sql);
                    ms.Close();
                    //dbLog.command.CommandText = sql;
                    //dbLog.command.Connection = dbLog.connection;
                    //dbLog.command.ExecuteNonQuery();
                    //dbLog.Close();
                }
                else
                { 
                    //如果没有到达盘库超时时间，则发送查询指令
                    string data = Data.Data(factory, send_ins[i].fac_num, "34", "0000");
                    //MessageBox.Show(d);
                    aim_ins.Enqueue(new FacMessage(ins_num++, "23", send_ins[i].fac_num, false, TIME_WAIT, "查询结果", data, s_Produce));

                    string data_search = Data.Data(factory, send_ins[i].fac_num, "32", "0000");
                    //向发送链表中添加此指令，但是不发送这条指令，发送的是查询状态指令
                    //Log("getState中获取盘库结果前查询状态    send_ins[i].fac_num   !!!!!===" + send_ins[i].fac_num);
                    //Log("send_ins长度=========" + send_ins.Count);
                    //Log("sendIns_queue长度=========" + sendIns_queue.Count);
                    //foreach (FacMessage y in sendIns_queue)
                    //{
                    //    Log("队列中的指令"+y.instruction+"数据信息"+y.message);
                    //}

                    sendIns_queue.Enqueue(new FacMessage(ins_num++, "21", send_ins[i].fac_num, false, TIME_WAIT, "获取盘库结果前查询状态", data_search, s_Produce));
                    if (ins_num > 2000)
                    {
                        ins_num = 0;
                    }
                }

            }
        }
        //已知料仓名获取料仓编号的函数是selectID(string name)，其中，料仓名以string
        //    的形式传递给这个函数，得到的是料仓编号的十进制string类型，在整个项目中，
        //    变量的类型以string类型居多，原因是string转任何一种数据类型都好转，
        //    C#对类型的转化都封装了函数，调用即可。调用selectID函数多数在要发送指令时，
        //    用户在界面中选出需要进行操作的料仓，而料仓在界面上都以名称的形式出现，因此
        //    在把功能封装成指令时需要获得料仓的编号。

        private string selectID(string str)
        {
            string ret = "";
            if (SqlConnect == 1)
            {
                string sql = "select * from bininfo where BinName = '" + str + "'";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                while (rd.Read())
                {
                    ret = rd["BinID"].ToString();
                }
                rd.Close();
                ms.Close();
                return ret;
            }
            else
            {
                //new Thread(new ParameterizedThreadStart(showBox)).Start("请检查数据库是否创建好");
                Log("请检查数据库是否创建好--selectID");
                return "";
            }
        }

        private void getHisList(Object obj)

        {
            lock (locker)
            {
                JsonInfo jif = (JsonInfo)obj;
                try
                {
                    //定义用于存储从数据库中查询到的数据的llist，用于以后生成json数据
                    List<Ressh> rblist = new List<Ressh>();
                    //输入要查的料仓id，页码，一页显示的条数计算
                    //将所有数据
                    string data = jif.getData();//获取用户传递过来的id=1,2,3,
                    string str1 = data.Substring(0, data.Length - 1);//从右边去掉第一个
                    if (str1 == "" || str1.Equals("null"))
                    {
                        Log("请求的id是空的");

                    }
                    else
                    {
                        Log("截取的id是" + str1 + "\r\n");

                        int listlong = int.Parse("30");//一页显示30条
                        int pagenum = int.Parse(jif.getResult());//页码号,这个需要从客户那传递出来,通过result传过来

                        int last = (1 + pagenum) * listlong;//数据最后一个数据的位置
                        int first = pagenum * listlong;//第一个数据的位置

                        //sql语句 通过Top来查询第n到m条元素
                        //str1 是用户发来的料仓id
                        string sql = "SELECT  bindata.BinID,BinName,Volume,Weight,Temp,Hum, Code,Gname,(case Quality  when 0 then '未评测' when 1 then '数据可靠' when 2 then '数据不一定可靠' else '数据不可靠' end) as Quality, DateTime, MiDu FROM bindata, bininfo, mqttcode,groupinfo where bindata.BinID = bininfo.BinID AND bindata.BinID in(" + str1 + ")order by DateTime desc limit 0,30";
                        //DataBase db = new DataBase();
                        //db.command.CommandText = sql;
                        //db.command.Connection = db.connection;
                        //db.Dr = db.command.ExecuteReader();
                        MySqlConn ms = new MySqlConn();
                        MySqlDataReader rd = ms.getDataFromTable(sql);
                        while (rd.Read())
                        {//获取到每一个数据，写入到数组中
                            string bid = rd["BinID"].ToString();
                            string bname = rd["BinName"].ToString();
                            string vol = rd["Volume"].ToString();
                            string weight = rd["Weight"].ToString();
                            string temp = rd["Temp"].ToString();
                            string hum = rd["Hum"].ToString();
                            string DFcode = rd["Code"].ToString();
                            string DGnumber = rd["Gname"].ToString();
                            string zhiliang = rd["Quality"].ToString();
                            //Log("QualityQualityQuality == " + zhiliang);
                            string dateTime = rd["DateTime"].ToString();
                            string dens = rd["MiDu"].ToString();
                            Ressh rb = new Ressh(bid, bname, vol, weight, temp, hum, DFcode, DGnumber, zhiliang, dateTime, dens);
                            rblist.Add(rb);
                            //Log(db.Dr["DateTime"].ToString());
                        }
                        rd.Close();
                        ms.Close();
                        Log("获取的数据长度是=" + rblist.Count());

                    }
                    //对list数组进行json化
                    string datalist = JsonConvert.SerializeObject(rblist);
                    //Log("组成的json数据是=" + datalist);

                    string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
                    string result = jif.getResult();//用户发来的用于显示历史数据的页数
                    int setNumNext = int.Parse(result) + 1;//将用于发来的页码加1；
                    JsonInfo sendJif = new JsonInfo("" + setNumNext, user, "responseHistoryList", datalist);

                    //编辑数组发送指令
                    Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                    sendMqttThread.Start(sendJif);

                }
                catch (Exception e)
                {
                    Log("查询历史记录错误" + e.ToString());
                    //之后发送空的数据
                    List<ReBinHistory> rblist = new List<ReBinHistory>();

                    Log("获取的数据长度是=" + rblist.Count());
                    //对list数组进行json化
                    string datalist = JsonConvert.SerializeObject(rblist);
                    //Log("组成的json数据是=" + datalist);

                    string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
                    string result = jif.getResult();//用户发来的用于显示历史数据的页数
                    int setNumNext = int.Parse(result) + 1;//将用于发来的页码加1；
                    JsonInfo sendJif = new JsonInfo("" + setNumNext, user, "responseHistoryList", datalist);

                    //编辑数组发送指令
                    Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                    sendMqttThread.Start(sendJif);
                }
            }

        }

        private void getBinInfo(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            try
            {
                //输入要查的料仓id，页码，一页显示的条数计算
                //将所有数据
                string data = jif.getData();//获取用户传递过来的id=1,2,3,
                                            //string str1 = data.Substring(0, data.Length - 1);//从右边去掉第一个,即去掉最右边的逗号
                Log("获取的id是" + data + "\r\n");
                //sql语句 通过Top来查询第n到m条元素
                //str1 是用户发来的料仓id

                string[] ids = data.Split(',');
                Log("获取的id长度===" + ids.Length);
                string[] bins = new string[ids.Count() - 1];
                for (int i = 0; i < ids.Count() - 1; i++)
                {
                    Log("用户传递的id是" + ids[i]);
                    //将id给新的数组
                    bins[i] = ids[i];
                }
                //定义用于存储从数据库中查询到的数据的llist，用于以后生成json数据
                List<BinInfo> rblist = new List<BinInfo>();
                //DataBase db = new DataBase();
                for (int i = 0; i < bins.Length; i++)
                {
                    string sql = "select * from bininfo,bindata where bininfo.BinID = '" + bins[i] + "' and bindata.BinID = '" + bins[i] + "' order by bindata.DateTime desc  limit 1";

                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn ms = new MySqlConn();
                    MySqlDataReader rd = ms.getDataFromTable(sql);

                    while (rd.Read())
                    {//获取到每一个数据，写入到数组中
                        string bid = rd["BinID"].ToString();
                        string bname = rd["BinName"].ToString();
                        string Diameter = rd["Diameter"].ToString();
                        string CylinderH = rd["CylinderH"].ToString();
                        string PyramidH = rd["PyramidH"].ToString();
                        string Density = rd["MiDu"].ToString();
                        string Margin = rd["Margin"].ToString();
                        string BinTop = rd["BinTop"].ToString();
                        string Wheelbase = rd["Wheelbase"].ToString();
                        string Angle = rd["Angle"].ToString();
                        string Vol = rd["Volume"].ToString();
                        string Weight = rd["Weight"].ToString();
                        string Temp = rd["Temp"].ToString();
                        string Hum = rd["Hum"].ToString();
                        string DateTime = rd["DateTime"].ToString();
                        string Algorithm = rd["Algorithm"].ToString();
                        string PrintNum = rd["PrintNum"].ToString();
                        string Qual = rd["Quality"].ToString();
                        BinInfo rb = new BinInfo(bid, bname, Diameter, CylinderH, PyramidH, Density, Margin, BinTop, Wheelbase, Angle, Vol, Weight, Temp, Hum, DateTime, Algorithm, PrintNum, Qual);
                        rblist.Add(rb);
                        //Log(db.Dr["DateTime"].ToString());
                    }
                    rd.Close();
                    ms.Close();
                }


                //查询最新数据





                Log("获取的数据长度是=" + rblist.Count());
                //对list数组进行json化
                string datalist = JsonConvert.SerializeObject(rblist);
                //Log("组成的json数据是=" + datalist);

                string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息

                JsonInfo sendJif = new JsonInfo(data, user, "responseBinInfo", datalist);//str1，返回是哪个料仓的信息

                //编辑数组发送指令
                Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                sendMqttThread.Start(sendJif);
            }
            catch(Exception e)
            {
                Log(e.ToString());
            }
           

        }
        private void SendHistListMqtt(object obj)//将组成的数据发出
        {
            JsonInfo jif = (JsonInfo)obj;
            string res = jif.getResult();
            string act = jif.getActionType();
            string date = jif.getData();
            SendJsonInfo sendJif = new SendJsonInfo(res, "win/" + mqCode, act, date);//发送者是win端
                                                                                     //生成发送的json数据
            string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
            //richTextBox1.AppendText("将要发送指令，数据是" + sendMqttInfo + "\r\n");
            //将数据发送出去
            Log("准备发送");
            byte[] byteArray = System.Text.Encoding.Default.GetBytes(sendMqttInfo);
            Log("大小===" + byteArray.Length);
            //Log(byteArray);
            //client.Publish(jif.getUserinfo(), byteArray);///byte不能发汉字
            client.Publish(jif.getUserinfo(), Encoding.UTF8.GetBytes(sendMqttInfo));
            Log("1发送完成");
        }

        //用户选择日期查询
        private void getHisListByDate(object obj)
        {
            try
            {
                JsonInfo jif = (JsonInfo)obj;
                //输入要查的料仓id，页码，一页显示的条数计算
                //将所有数据
                string data = jif.getData();//获取用户传递过来的id=1,2,3,
                string str1 = data.Substring(0, data.Length - 1);//从右边去掉第一个
                Log("截取的id是==" + str1 + "\r\n");

                int listlong = int.Parse("1000");//一页显示1000条
                string resDate = jif.getResult() + "%";//这个需要从客户那传递出来,通过result传过来
                int pagenum = int.Parse("0");//页码
                Log("获得的时间是==" + resDate + "\r\n");
                int last = (1 + pagenum) * listlong;//数据最后一个数据的位置
                int first = pagenum * listlong;//第一个数据的位置
                //resDate = "2018/10/23%";
                //sql语句 输入的是id和要查询的时间。。。时间是nvarchar类型，将时间安照字符串处理
                //str1 是用户发来的料仓id，resDate用户要查询的时间
                string sql = "SELECT  bindata.BinID,BinName,Volume,Weight,Temp,Hum,(case Quality  when 0 then '未评测' when 1 then '数据可靠' when 2 then '数据不一定可靠' else '数据不可靠' end)as Quality, DateTime, MiDu FROM bindata, bininfo where bindata.BinID = bininfo.BinID AND bindata.BinID in("+ str1 + ") and DateTime like  '"+ resDate + "' order by DateTime desc";
                //DataBase db = new DataBase();
                //db.command.CommandText = sql;
                //db.command.Connection = db.connection;
                //db.Dr = db.command.ExecuteReader();
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                //定义用于存储从数据库中查询到的数据的llist，用于以后生成json数据
                List<ReBinHistory> rblist = new List<ReBinHistory>();
                while (rd.Read())
                {//获取到每一个数据，写入到数组中
                    string bid = rd["BinID"].ToString();
                    string bname = rd["BinName"].ToString();
                    string vol = rd["Volume"].ToString();
                    string weight = rd["Weight"].ToString();
                    string temp = rd["Temp"].ToString();
                    string hum = rd["Hum"].ToString();
                    string zhiliang = rd["Quality"].ToString();
                    string dateTime = rd["DateTime"].ToString();
                    string dens = rd["MiDu"].ToString();
                    ReBinHistory rb = new ReBinHistory(bid, bname, vol, weight, temp, hum, zhiliang, dateTime, dens);
                    rblist.Add(rb);

                    //Invoke(new MethodInvoker(delegate
                    //{
                    //    //comboBox4.Items.Add(db.Dr["FactoryID"].ToString());//提取出厂区id
                    //    richTextBox1.AppendText(db.Dr["DateTime"].ToString() + "\r\n");
                    //}));

                }
                rd.Close();
                ms.Close();
                Log("时间" + resDate + "，获取的数据长度是=" + rblist.Count() + "\r\n");

                if (rblist.Count() == 0)//如果查询结果是0，一种可能是没有数据，另一种可能是进入数据库查询时间的格式可能是2017-8-9。之前的查询时间是2017/8/9
                {//将2017/8/9格式转成2017-8-9
                    Log("检验时间格式");
                    string rtime = jif.getResult();
                    rtime = rtime.Replace("/", "-");
                    resDate = rtime + "%";
                    Log("修正后的时间格式是：" + rtime);
                    //sql语句 输入的是id和要查询的时间。。。时间是nvarchar类型，将时间安照字符串处理
                    //str1 是用户发来的料仓id，resDate用户要查询的时间。对于2005数据库时间要转换CONVERT(VARCHAR(10),[DateTime],60) like
                     sql = "SELECT  bindata.BinID,BinName,Volume,Weight,Temp,Hum,(case Quality  when 0 then '未评测' when 1 then '数据可靠' when 2 then '数据不一定可靠' else '数据不可靠' end)as Quality, DateTime, MiDu FROM bindata, bininfo where bindata.BinID = bininfo.BinID AND bindata.BinID in(" + str1 + ") and DateTime like  '" + resDate + "' order by DateTime desc";
                    //DataBase db = new DataBase();
                    //db.command.CommandText = sql;
                    //db.command.Connection = db.connection;
                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn ms1 = new MySqlConn();
                    MySqlDataReader rd1 = ms1.getDataFromTable(sql);
                    //定义用于存储从数据库中查询到的数据的llist，用于以后生成json数据
                    while (rd1.Read())
                    {//获取到每一个数据，写入到数组中
                        string bid = rd1["BinID"].ToString();
                        string bname = rd1["BinName"].ToString();
                        string vol = rd1["Volume"].ToString();
                        string weight = rd1["Weight"].ToString();
                        string temp = rd1["Temp"].ToString();
                        string hum = rd1["Hum"].ToString();
                        string zhiliang = rd1["Quality"].ToString();
                        string dateTime = rd1["DateTime"].ToString();
                        string dens = rd1["MiDu"].ToString();
                        ReBinHistory rb = new ReBinHistory(bid, bname, vol, weight, temp, hum, zhiliang, dateTime, dens);
                        rblist.Add(rb);

                        //Invoke(new MethodInvoker(delegate
                        //{
                        //    //comboBox4.Items.Add(db.Dr["FactoryID"].ToString());//提取出厂区id
                        //    richTextBox1.AppendText(db.Dr["DateTime"].ToString() + "\r\n");
                        //}));

                    }
                    rd1.Close();
                    ms1.Close();

                }

                Log("时间" + resDate + "，获取的数据长度是=" + rblist.Count() + "\r\n");
                //对list数组进行json化
                string datalist = JsonConvert.SerializeObject(rblist);
                //richTextBox1.AppendText("组成的json数据是=" + datalist + "\r\n");

                string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
                string result = jif.getResult();//用户发来的用于显示历史数据的页数
                                                //int setNumNext = int.Parse(result) + 1;//将用于发来的页码加1；
                JsonInfo sendJif = new JsonInfo("", user, "responseHistoryList", datalist);

                //编辑数组发送指令
                Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                sendMqttThread.Start(sendJif);
            }
            catch (Exception e)
            {
                Log("特定时间：" + e.ToString() + "，获取的数据长度是=\r\n");
            }
            

        }


        //获取段日期的数据
        private void getLongTime(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            string data = jif.getData();//获取用户传递过来的数据
            string time = jif.getResult();
            string str1 = data.Substring(0, data.Length - 1);//从右边去掉第一个
            Log("获得的data:" + data + "，获取的时间是time:" + time+ "截取的id是==" + str1);
            //定义用于存储从数据库中查询到的数据的llist，用于以后生成json数据
            List<ReBinHistory> rblist = new List<ReBinHistory>();

            string startTime = "";
            string endTime = "";
            try
            {
                string[] arr = time.ToString().Split(' ');
                startTime = arr[0];
                endTime = arr[1];
                Log("开始时间：" + startTime + "。结束时间：" + endTime);
            }
            catch (Exception ee)
            {
                Log("时间读取出错.错误：" + ee.ToString());
            }
            try
            {
                //                string sql =  "SELECT  bindata.BinID,BinName,Volume,Weight,Temp,Hum,"+
                // "(case Quality when 0 then '未评测' when 1 then '数据可靠' when 2 then '数据不一定可靠' else '数据不可靠' end) as Quality, DateTime, MiDu"+
                //"FROM bindata, bininfo where bindata.BinID = bininfo.BinID"+
                //"AND bindata.BinID in("+ str1 + ") and date(str_to_date(DateTime,'%Y/%m/%d')) between '2018/10/22' and  '2018/10/23' order by DateTime desc limit 0,1000";
                string sql = "SELECT  bindata.BinID,BinName,Volume,Weight,Temp,Hum,(case Quality  when 0 then '未评测' when 1 then '数据可靠' when 2 then '数据不一定可靠' else '数据不可靠' end) as Quality, DateTime, MiDu FROM bindata, bininfo where bindata.BinID = bininfo.BinID AND bindata.BinID in("+str1+") and date(str_to_date(DateTime,'%Y/%m/%d')) between '"+ startTime +"'and  '"+ endTime +"' order by DateTime desc limit 0,1000";
                ////获取数据库对象
                //DataBase db = new DataBase();
                ////设置sql语句
                //db.command.CommandText = sql;
                ////简历sql连接
                //db.command.Connection = db.connection;
                ////执行查询
                //db.Dr = db.command.ExecuteReader();
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                while (rd.Read())
                {//获取到每一个数据，写入到数组中
                    string bid = rd["BinID"].ToString();
                    string bname = rd["BinName"].ToString();
                    string vol = rd["Volume"].ToString();
                    string weight = rd["Weight"].ToString();
                    string temp = rd["Temp"].ToString();
                    string hum = rd["Hum"].ToString();
                    string zhiliang = rd["Quality"].ToString();
                    string dateTime = rd["DateTime"].ToString();
                 
                    string dens = rd["MiDu"].ToString();
                    ReBinHistory rb = new ReBinHistory(bid, bname, vol, weight, temp, hum, zhiliang, dateTime, dens);
                    rblist.Add(rb);
                    //Log(db.Dr["DateTime"].ToString());
                }
                rd.Close();
                ms.Close();
                Log("获取的数据长度是=" + rblist.Count());

                string datalist = JsonConvert.SerializeObject(rblist);
                //richTextBox1.AppendText("组成的json数据是=" + datalist + "\r\n");

                string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
                string result = jif.getResult();//用户发来的用于显示历史数据的页数
                                                //int setNumNext = int.Parse(result) + 1;//将用于发来的页码加1；
                JsonInfo sendJif = new JsonInfo("", user, "responseHistoryList", datalist);

                //编辑数组发送指令
                Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                sendMqttThread.Start(sendJif);


            }
            catch(Exception eee)
            {
                Log("数据库操作失败：" + eee.ToString());
            }

        }

        //获取分组列表
        private void getGroup(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            try
            {
                List<ReGroup> grouplist = new List<ReGroup>();
                string sqlGroup = "select * from groupinfo where gstate = '1'";

                MySqlConn msc1 = new MySqlConn();
                MySqlDataReader rdGroup = msc1.getDataFromTable(sqlGroup);
                while (rdGroup.Read())
                {
                    string gid = rdGroup["Gid"].ToString();
                    string gname = rdGroup["Gname"].ToString();
                    ReGroup g = new ReGroup(gid, gname);
                    grouplist.Add(g);
                }
                rdGroup.Close();
                msc1.Close();
                Log("获取的分组个数是=" + grouplist.Count());

                string datalist = JsonConvert.SerializeObject(grouplist);
                //richTextBox1.AppendText("组成的json数据是=" + datalist + "\r\n");

                string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
                JsonInfo sendJif = new JsonInfo("", user, "responseGroupList", datalist);

                //编辑数组发送指令
                Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
                sendMqttThread.Start(sendJif);


            }
            catch (Exception eee)
            {
                Log("数据库操作失败：" + eee.ToString());
            }

        }


        private void cancel(object obj)
        {
            try
            {
                //Log("进入取消盘库的线程1");
                JsonInfo jif = (JsonInfo)obj;
                //Log("进入取消盘库的线程2");
                string binid = jif.getData();//获取用户传递过来的id=1
                //Log("进入取消盘库的线程3");
                string id = binid;
                //Log("进入取消盘库的线程4、、id：" +id);
                //ins_queue.Enqueue(new FacMessage(ins_num++, "21", id, false, 3, "查询状态", data_search));
                //Log("取消盘库factory" +"*"+factory +"*"+ "id" +id);
                id = id.Substring(0, id.Length - 1);
                //string data = Data.Dataq(factory, id, "30", "0000");
                string data = Data.Data(factory, id, "30", "0000");
               // Log("取消盘库data" + data);
                //Log("发送取消盘库指令的指令");
                //return;

                FacMessage facmes = new FacMessage(ins_num++, "1F", id, false, TIME, "取消当前操作", data, 3, s_Produce);
                sendIns_queue.Enqueue(facmes);
                //Log("进入取消盘库的线程5");
                //一个用户取消了盘库，所有用户都收到取消的 盘库
                //Log("要删除这个用户节点");            
                for (int i = binPkingList.Count - 1; i >= 0; i--)
                {
                    //string binpkid = binPk.getEqid();//同一时间只有一个库在盘。当取消一个的时候，其他的也取消
                    string uid = binPkingList[i].getUid();
                    string eqid = binPkingList[i].getEqid().PadLeft(2, '0');//厂区码都是03
                    string binname = getName(eqid);
                    Log("用户" + uid + ",取消了盘库。设备binpkid=" + eqid + "。。id=" + id);
                    if (eqid.Equals(id))
                    {
                        //给这个用户发送mqtt
                        //将sendbinlist进行排序
                        List<Rebinlist> infolist = new List<Rebinlist>();
                        //排好的列表
                        Rebinlist r = new Rebinlist(eqid, binname, "" + "取消操作");
                        infolist.Add(r);
                        //将料仓对象列表，序列化成json数据
                        string binlist = JsonConvert.SerializeObject(infolist);
                        SendJsonInfo sendJif = new SendJsonInfo("pk", "win/" + mqCode, "responseStateAndDo", binlist);//
                        string sendMqttInfo = JsonConvert.SerializeObject(sendJif);
                        Log("取消盘库要发送的指令，数据是" + sendMqttInfo + "\r\n");
                        //将数据发送出去
                        client.Publish(uid, Encoding.UTF8.GetBytes(sendMqttInfo));

                        binPkingList.RemoveAt(i);
                        break;
                    }
                }
                if (ins_num > 2000)
                {
                    ins_num = 1;
                }
            }
            catch(Exception e)
            {
                Log("取消盘库线程报错：" + e.ToString());
            }
            

        }
      



            //获取画图点的信息
            private void getBinAnal(object obj)
        {
            JsonInfo jif = (JsonInfo)obj;
            string data = jif.getData();//获取用户传递过来的数据
            string str1 = data;//从右边去掉第一个。获取到是binname，体积和时间.."3,1402.11,2018/04/18 12:26:41".Substring(0, data.Length - 1)



            Log("请求XY坐标"+ str1);
            analysisData a = new analysisData();
            string datalist = a.analysis(str1);

            string user = jif.getUserinfo();//获取到的用户所订阅的订阅号，服务将向这个号发返回信息
            string result = jif.getResult();//用户发来的用于显示历史数据的页数
            //int setNumNext = int.Parse(result) + 1;//将用于发来的页码加1；
            JsonInfo sendJif = new JsonInfo("", user, "responseGetXY", datalist);
            //编辑数组发送指令
            Thread sendMqttThread = new Thread(SendHistListMqtt);//发送指令线程
            sendMqttThread.Start(sendJif);

        }

        //检查数据库是否开启Mysql
        /// <summary>
        /// 判断数据库服务是否已经启动,如果已经启动就返回True，否则返回False
        /// </summary>
        /// <returns></returns>
        private bool JudgeDBServerStatus()
        {
            bool ExistFlag = false;
            try
            {
                //检查Mysql数据库是否连接
                string sql = "select * from test where test = '1'";
                Log("准备连接MySql数据库");
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(sql);
                Log("准备连接数据库,MySqlDataReader正常");
                
                string a;
                Log("读取test数据");
                while (rd.Read())
                {
                    Log("有数据");
                    a = rd["test"].ToString();

                }
                rd.Close();
                ms.Close();
                Log("数据库连接成功");
                ExistFlag = true;
            }
            catch (Exception e)
            {
                
                Log("数据库读取出错，数据库服务没有开启" + e.ToString());
                return ExistFlag;
            }
            return ExistFlag;
        }

        /// <summary>???
        /// 测试数据库中的数据表是否全，如果不全则补全不存在的数据表
        /// </summary>
        /// <returns></returns>
        private string conTosql()
        {
            try
            {
                Log("检查数据库");
                int sum_table = 9;//一共需要9个表，存在一个就减减..再加上一个由于mqtt的code
                string[] tables = { "bininfo", "binauto", "bindata", "binlog", "config", "server", "user", "mqttcode", "groupinfo" };
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
                Log("没有表的数目：" + sum_table);
                rd.Close();
                ms.Close();
                if (sum_table == 0)
                {
                    return "1";
                }
                else
                {
                    Log("创建表");
                    string table = "";
                    for (int i = 0; i < 9; i++)
                    {
                        if (tables[i].Equals("") != true)
                        {
                            table += (tables[i] + "|");

                            string sql_createT = "";
                            string sql_initdata = "";
                            //if (tables[i].Equals("binauto"))
                            //{
                            //    sql_createT = "create table [binauto] (BinID int, Time nvarchar(MAX), " +
                            //        "Date int, BinName nvarchar(MAX), Operation nvarchar(MAX));";
                            //}
                            //else if (tables[i].Equals("bindata"))
                            //{
                            //    sql_createT = "create table [bindata] (BinID int, Volume float, Weight float, " +
                            //        "Temp float, Hum float, DateTime nvarchar(MAX),Algorithm nvarchar(10),PrintNum nvarchar(10),Quality nvarchar(10),BackData nvarchar(MAX) ,MiDu float,Jd float,BackAn nvarchar(MAX));";
                            //}
                            //else if (tables[i].Equals("bininfo"))
                            //{
                            //    sql_createT = "create table [bininfo] (BinID int, BinName nvarchar(MAX), Diameter float, " +
                            //        "CylinderH float, PyramidH float, Density float,Margin float,BinTop float,Wheelbase float,Angle float,KValue nchar(10),Bvalue nchar(10));";
                            //}
                            //else if (tables[i].Equals("binlog"))
                            //{
                            //    sql_createT = "create table [binlog] (Address int, Dataytpe nvarchar(MAX), Data nvarchar(MAX), " +
                            //        "Message nvarchar(MAX), Time nvarchar(MAX));";
                            //}
                            //else if (tables[i].Equals("config"))
                            //{
                            //    sql_createT = "create table [config] (DistrictID nvarchar(MAX), FactoryID nvarchar(MAX));";
                            //    sql_initdata = "insert into [config] values('0102', '00001')";

                            //}
                            //else if (tables[i].Equals("server"))
                            //{
                            //    sql_createT = "create table [server] (ServerIp nvarchar(MAX), ServerPort int, " +
                            //        "UpdateServ nvarchar(MAX), DataServ nvarchar(MAX));";
                            //}
                            //else if (tables[i].Equals("user"))
                            //{
                            //    sql_createT = "create table [user] (UserName nvarchar(MAX), PassWord nvarchar(MAX), Admin int);";
                            //    sql_initdata = "insert into [user] values('root', '123','1')";
                            //}
                            //else if (tables[i].Equals("mqttcode"))
                            //{
                            //    Log("创建表mqttcode");
                            //    sql_createT = "create table [mqttcode] (MqttCode nvarchar(MAX), Code nvarchar(MAX));";
                            //    sql_initdata = "insert into [mqttcode] values('1', '3706010001')";
                            //}

                            //mysql建表
                            if (tables[i].Equals("binauto"))
                            {
                                sql_createT = "create table `binauto` (`BinID` int null, `Time` varchar(255), " +
                                    "`Date` int, `BinName` varchar(255), `Operation` varchar(255));";
                            }
                            else if (tables[i].Equals("bindata"))
                            {
                                sql_createT = "create table `bindata` (`BinID` int, `Volume` float, `Weight` float, " +
                                    "`Temp` float, `Hum` float, `DateTime` varchar(255),`Algorithm` varchar(255),`PrintNum` varchar(255),`Quality` varchar(255),`BackData` varchar(2000) ,`MiDu` float,`Jd` float,`BackAn` varchar(2000),`BackAll` Text, `Hangle` float);";
                            }
                            else if (tables[i].Equals("bininfo"))
                            {
                                sql_createT = "create table `bininfo` (`BinID` int, `BinName` varchar(255), `Diameter` float, " +
                                    "`CylinderH` float, `PyramidH` float, `Density` float,`Margin` float,`BinTop` float,`Wheelbase` float,`Angle` float,`KValue` varchar(255),`Bvalue` varchar(255),`CAngle` varchar(255),`type` varchar(255),`HAngle` varchar(255),`Fbian` varchar(255),`Fkuan` varchar(255),`Fzuobian` varchar(255),`Gid` int,`UpperH` varchar(255));";
                            }
                            else if (tables[i].Equals("binlog"))
                            {
                                sql_createT = "create table `binlog` (`Address` int, `Dataytpe` varchar(255),`Data` varchar(255), " +
                                    "`Message` varchar(255), `Time` varchar(255));";
                            }
                            else if (tables[i].Equals("config"))
                            {
                                sql_createT = "create table config (DistrictID varchar(255), FactoryID varchar(255));";
                                sql_initdata = "insert into config values('0102', '00000')";

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
                                sql_initdata = "insert into mqttcode values('1', '37')";
                            }
                            else if (tables[i].Equals("groupinfo"))
                            {
                                Log("执行建表操作++++++++++++++++");
                                sql_createT = "create table groupinfo (Gid int, Gname varchar(255),Gstate varchar(255));";
                                sql_initdata = "insert into groupinfo values(1, '默认','1')";
                            }




                            try
                            {
                                MySqlConn ms1 = new MySqlConn();
                                int iRet = ms1.nonSelect(sql_createT);
                                ms1.Close();
                                Log("iRet大小=" + iRet);
                                if (iRet == 1) {
                                    Log("建表执行成功");
                                        }
                                
                                if (sql_initdata.Equals("") != true)
                                {
                                    MySqlConn ms2 = new MySqlConn();
                                    int iRet2 = ms2.nonSelect(sql_initdata);
                                    ms2.Close();
                                    if (iRet2 == 1) Log("插入数据成功");
                                    else Log("插入数据失败");
                                }
                                //DataBase database = new DataBase();
                                //Log("数据库建表，执行语句1");
                                //database.command.CommandText = sql_createT;
                                //Log("执行语句2");
                                //database.command.Connection = database.connection;
                                //Log("执行语句3");
                                //database.command.ExecuteNonQuery();
                                //Log("执行语句4");
                                //if (sql_initdata.Equals("") != true)
                                //{
                                //    database.command.CommandText = sql_initdata;
                                //    database.command.ExecuteNonQuery();
                                //}
                                //database.Close();

                            }
                            catch (Exception e)
                            {
                                Log("建表时出错" + e.ToString());
                                return "0";
                            }

                        }
                    }
                    //MessageBox.Show("数据库表格已完善", d["MB_Title"]);
                    Log("数据库表格已完善");//数据库表格已完善

                    return sum_table.ToString() + "|" + table;
                }
            }
#pragma warning disable CS0168 // 声明了变量“ee”，但从未使用过
            catch(Exception ee)
#pragma warning restore CS0168 // 声明了变量“ee”，但从未使用过
            {
                Log("数据库建表失败");
            }

            return "0";
            
        }
        private string isRe(int equip, string weight)
        {

            try
            {
                //DataBase dbLastData = new DataBase();//查询当前数据库中最新的数据
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
                if (LastWeight == wei_f && cha < 60)//如果新输入的和保存在数据库中的数据相等且时间差小于1分钟，这说明是重复输入，不能输入
                {
                    return "error";
                }
                else
                {
                    return "ok";
                }
            }
            catch (Exception ee)
            {
                Log("isR出错" + ee.ToString());
                return "ok";
            }

        }
        private string isReWendu(int equip, string temp)
        {
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
            }
            catch (Exception ee)
            {
               Log("查询是否有重复温度出错：" + ee.ToString());
            }


            DateTime a = Convert.ToDateTime(LastTime);
            string time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            DateTime b = Convert.ToDateTime(time);

            TimeSpan ts = b - a;//datetime做差
            int cha = (int)ts.TotalSeconds;
            float wei_f = float.Parse(temp);
            if (LastWeight == wei_f && cha < 60)//如果新输入的和保存在数据库中的数据相等且时间差小于1分钟，这说明是重复输入，不能输入
            {
                return "error";
            }
            else
            {
                return "ok";
            }
        }
        /// <summary>???Mysql
        /// 测试数据库中的数据表是否全，如果不全则补全不存在的数据表
        /// </summary>
        /// <returns></returns>
        private int isCompleteTable()
        {

            Log("检查数据库字段是否补齐");
            int isComplete = 0;
            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'HAngle';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);
                
                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bininfo中HAngle个数" + num);
                if (num == 0)
                {
                    string sql_initdata = "ALTER TABLE bininfo ADD `type` varchar(255)";//添加
                    string sql_initdata1 = "ALTER TABLE bininfo ADD `HAngle` varchar(255)";//添加
                    
                    Log("添加HAngle");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    int iRet1 = ms3.nonSelect(sql_initdata1);
                    ms3.Close();
                    Log("执行语句1  alter   type=" + iRet);
                    Log("执行语句1  alter  HAngle=" + iRet1);
                    if (iRet == 0 && iRet == iRet1)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bininfo ADD `CAngle` varchar(255)" + e.ToString() + "\r\n");

            }


            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bindata'and column_name = 'BackAll';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bindata中BackAll个数" + num);
                if (num == 0)
                {
                    string sql_initdata = "ALTER TABLE bindata ADD `BackAll` Text";//添加

                    Log("添加BackAll");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    ms3.Close();
                    Log("执行语句1  ALTER TABLE bindata ADD `BackAll` Text" + iRet);
                    if (iRet == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bindata ADD `BackAll` " + e.ToString() + "\r\n");

            }

            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'Fbian';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bininfo中Fbian个数" + num);
                if (num == 0)
                {
                    string sql_initdata1 = "ALTER TABLE bininfo ADD `Fbian` varchar(255)";//添加

                    Log("添加Fbian");

                    MySqlConn ms3 = new MySqlConn();
                    int iRet1 = ms3.nonSelect(sql_initdata1);
                    ms3.Close();
                    Log("执行语句1  alter  Fbian=" + iRet1);
                    if (iRet1 == 0 )
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bininfo ADD `Fbian` varchar(255)" + e.ToString() + "\r\n");

            }

            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'Fkuan';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();

                Log("bininfo中Fkuan个数" + num);
                if (num == 0)
                {

                    string sql_initdata = "ALTER TABLE bininfo ADD `Fkuan` varchar(255)";//添加
                    
                    Log("添加Fkuan");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    ms3.Close();
                    Log("执行语句1  alter  Fkuan=" + iRet);
                    if (iRet == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bininfo ADD `Fbian` varchar(255)" + e.ToString() + "\r\n");

            }

            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'Fzuobian';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();

                Log("bininfo中Fzuobian个数" + num);
                if (num == 0)
                {

                    string sql_initdata = "ALTER TABLE bininfo ADD `Fzuobian` varchar(255)";//添加

                    Log("添加Fzuobian");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    ms3.Close();
                    Log("执行语句1  alter  Fzuobian=" + iRet);
                    if (iRet == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bininfo ADD `Fbian` varchar(255)" + e.ToString() + "\r\n");

            }

            //给bininfo中添加关联字段gid
            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'Gid';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bininfo中Gid的个数" + num);
                if (num == 0)
                {
                    string sql_initdata = "ALTER TABLE bininfo ADD `Gid` int";//添加

                    Log("给bininfo添加了Gid关联字段");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    ms3.Close();
                    Log("执行语句1  ALTER TABLE bininfo ADD `Gid` int" + iRet);
                    if (iRet == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bindata ADD `BackAll` " + e.ToString() + "\r\n");

            }

            //给bindata中添加关联字段Hangle
            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bindata'and column_name = 'Hangle';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bininfo中Hangle的个数" + num);
                if (num == 0)
                {
                    string sql_initdata = "ALTER TABLE bindata ADD `Hangle` float";//添

                    Log("给bindata添加了Hangle关联字段");
                    MySqlConn ms3 = new MySqlConn();
                    int iRet = ms3.nonSelect(sql_initdata);
                    ms3.Close();
                    Log("执行语句1  ALTER TABLE bindata ADD `Hangle` float" + iRet);
                    if (iRet == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bindata ADD `BackAll` " + e.ToString() + "\r\n");

            }


            //给bininfo中添加关联字段UpperH
            try
            {
                int num = 0;
                //检查字段是否添加
                string add = " select column_name from information_schema.columns where table_schema='factory' and table_name='bininfo'and column_name = 'UpperH';";
                MySqlConn ms = new MySqlConn();
                MySqlDataReader rd = ms.getDataFromTable(add);

                while (rd.Read())
                {
                    num++;
                }
                rd.Close();
                ms.Close();
                Log("bininfo中UpperH个数" + num);
                if (num == 0)
                {
                    string sql_initdata1 = "ALTER TABLE bininfo ADD `UpperH` varchar(255)";//添加

                    Log("添加UpperH");

                    MySqlConn ms3 = new MySqlConn();
                    int iRet1 = ms3.nonSelect(sql_initdata1);
                    ms3.Close();
                    Log("执行语句1  alter  UpperH=" + iRet1);
                    if (iRet1 == 0)
                    {
                        Log("添加字段成功");
                        isComplete++;
                    }
                }
                else
                {
                    isComplete++;
                }


            }
            catch (Exception e)
            {
                Log("ALTER TABLE bininfo ADD `UpperH` varchar(255)" + e.ToString() + "\r\n");

            }




            //            但是，奇怪的事情出现了。
            //数据库明明存在该记录，但是每次返回的值却是0,而不是我所希望看到的1（数据库存在1条记录），看来在这里ExecuteNonQuery()方法并没有返回受影响的行数（记忆中应该是会返回受影响的行数的，难道我记错了）。

            //于是，百度了一下。

            //在msdn上看到了下面这样一段说明：

            //对于 UPDATE、INSERT 和DELETE 语句，返回值为该命令所影响的行数。

            //对于所有其他 DML语句，返回值都为 - 1。

            //对于 DDL语句，比如 CREATETABLE 或ALTER TABLE，返回值为0。

            //通过学习该说明，很容易了解到原因的所在。那就是因为ExecuteNonQuery()方法对于 UPDATE、INSERT 和DELETE 语句，返回值才是为该命令所影响的行数。所以在这里，我使用ExecuteNonQuery()来执行select语句，当然得不到我所希望看到的结果了。
            //---------------------
            //作者：三五月儿
            //来源：CSDN
            //原文：https://blog.csdn.net/yl2isoft/article/details/10042315 
            //            版权声明：本文为博主原创文章，转载请附上博文链接！
            //try
            //{
            //    int num = 0;
            //    //检查字段是否添加
            //    string add = "select name from Factory..syscolumns where id=object_id('Factory.dbo.bininfo') and (name='Bvalue')";

            //    DataBase dbadd = new DataBase();
            //    dbadd.command.CommandText = add;
            //    dbadd.command.Connection = dbadd.connection;
            //    dbadd.Dr = dbadd.command.ExecuteReader();
            //    while (dbadd.Dr.Read())
            //    {
            //        num++;
            //    }
            //    dbadd.Dr.Close();
            //    dbadd.Close();
            //    Log("bininfo中Margin个数" + num);
            //    if (num == 0)
            //    {
            //        string sql_initdata = "ALTER TABLE Factory.dbo.bininfo ADD Margin float ,BinTop float ,Wheelbase float , Angle float,KValue nchar(10),Bvalue nchar(10)";//添加
            //        //string sql_initdata = "update [bininfo] set ['Margin'], = " + Margin + " where [BinID] = " + equip.ToString();
            //        DataBase database = new DataBase();
            //        Log("数据库建表，执行语句1");
            //        Log("执行语句2");
            //        database.command.Connection = database.connection;
            //        Log("执行语句3");
            //        Log("执行语句4");
            //        if (sql_initdata.Equals("") != true)
            //        {
            //            database.command.CommandText = sql_initdata;
            //            database.command.ExecuteNonQuery();
            //        }
            //        database.Close();


            //        isComplete++;
            //    }
            //    else
            //    {
            //        isComplete++;
            //    }


            //}
            //catch (Exception e)
            //{
            //    Log("添加数据失败ALTER TABLE Factory.dbo.bininfo ADD Margin float ,BinTop float ,Wheelbase float , Angle float" + e.ToString() + "\r\n");

            //}
            /////////////////////test add 

            //try
            //{
            //    int num = 0;
            //    //检查字段是否添加
            //    string add = "select name from Factory..syscolumns where id=object_id('Factory.dbo.bindata') and (name='BackAn')";

            //    DataBase dbadd = new DataBase();
            //    dbadd.command.CommandText = add;
            //    dbadd.command.Connection = dbadd.connection;
            //    dbadd.Dr = dbadd.command.ExecuteReader();
            //    while (dbadd.Dr.Read())
            //    {
            //        num++;
            //    }
            //    dbadd.Dr.Close();
            //    dbadd.Close();
            //    Log("bindata中的个数" + num);
            //    if (num == 0)
            //    {
            //        Log("bindata添加字段");
            //        string toadd = "ALTER TABLE Factory.dbo.bindata ADD Algorithm nvarchar(10),PrintNum nvarchar(40) , Quality nvarchar(10), BackData nvarchar(MAX),MiDu float,Jd float,BackAn nvarchar(MAX)";//补全盘库信息表，4.11添加盘库当时的角度值
            //        //string sql_initdata = "update [bininfo] set ['Margin'], = " + Margin + " where [BinID] = " + equip.ToString();
            //        DataBase database = new DataBase();
            //        database.command.Connection = database.connection;
            //        if (toadd.Equals("") != true)
            //        {
            //            database.command.CommandText = toadd;
            //            database.command.ExecuteNonQuery();
            //        }
            //        database.Close();
            //        Log("bindata添加字段完成");
            //        isComplete++;
            //    }
            //    else
            //    {
            //        isComplete++;
            //    }


            //}
            //catch (Exception e)
            //{
            //    Log("添加数据失败ALTER TABLE Factory.dbo.bindata ADD Algorithm nvarchar(10),PrintNum nvarchar(40) , Quality nvarchar(10), BackData nvarchar(MAX)" + e.ToString() + "\r\n");
            //}
            return isComplete;
        }
    }
}
