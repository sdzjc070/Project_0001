using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Odbc;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;

namespace MyTest
{
    class DataBase 
    {
        //public static string strConn = "Server=(local); Database=Factory;user=sa;password=123456";//(local)我的电脑本地是local。PC-20150915VNKU\\SQLSERVER2005
        //public SqlConnection connection = new SqlConnection(strConn);
        //public SqlCommand command = new SqlCommand();
        //public SqlDataReader Dr;
        //public SqlDataAdapter sda = new SqlDataAdapter();
        //public static string strConn = "DSN=warehouse1";
        ////数据库连接信息？？？？？？
        public string strConn;
        public SqlConnection connection;
        public SqlCommand command;
#pragma warning disable CS0649 // 从未对字段“DataBase.Dr”赋值，字段将一直保持其默认值 null
        public SqlDataReader Dr;
#pragma warning restore CS0649 // 从未对字段“DataBase.Dr”赋值，字段将一直保持其默认值 null
        public SqlDataAdapter sda;
        //public OdbcConnection connection = new OdbcConnection(strConn);
        //public OdbcCommand command = new OdbcCommand();
        //public OdbcDataReader Dr;
        //public OdbcDataAdapter sda = new OdbcDataAdapter();
        public DataBase() 
        {//在构造函数时将connection打开，不用在调用时再写Open函数
            String line = "";
            string name = "";
            string pwd = "";
            try
            {
                //读取sql地址"C://Mqtt//MyTestLog.txt
                string ippath = "C://Mqtt//SqlConfig.txt";
                if (File.Exists(ippath) == false)//创建保存串口信息的文件
                    File.Create(ippath).Close();
                StreamReader sr = new StreamReader(ippath, Encoding.Default);
                line = sr.ReadLine();
                string[] arr = line.Split('+');
                name = arr[0];
                pwd = arr[1];
                //关闭文件输入流
                sr.Close();
          

            strConn = "Server=" + name + "; Database=Factory;user=sa;password="+ pwd ;
            connection = new SqlConnection(strConn);
            command = new SqlCommand();
            sda = new SqlDataAdapter();
            connection.Open();
            }
#pragma warning disable CS0168 // 声明了变量“ee”，但从未使用过
            catch (Exception ee)
#pragma warning restore CS0168 // 声明了变量“ee”，但从未使用过
            {
                Log("DataBase.cs数据读取出错");
            }


        }
        public void Close()
        {
            connection.Close();
            connection.Dispose();
        }

        void Log(string str)    // 记录服务启动  
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
    }
}
