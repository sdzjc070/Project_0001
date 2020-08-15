using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace Warehouse
{
    class DataBase
    {
        //public static string strConn = "Server=.;Database=Factory;Trusted_Connection=SSPI;Max Pool Size = 512";
        //public SqlConnection connection = new SqlConnection(strConn);
        //public SqlCommand command = new SqlCommand();
        //public SqlDataReader Dr;
        //public SqlDataAdapter sda = new SqlDataAdapter();


        //public static string strConn = "Server=(local); Database=Factory;user=sa;password=123456";//(local)。。。。PC-20150915VNKU\\SQLSERVER2005
        //public SqlConnection connection = new SqlConnection(strConn);
        //public SqlCommand command = new SqlCommand();
        //public SqlDataReader Dr;
        //public SqlDataAdapter sda = new SqlDataAdapter();

        public string strConn;
        public SqlConnection connection;
        public SqlCommand command;
        public SqlDataReader Dr;
        public SqlDataAdapter sda;


       
        //public static string strConn = "DSN=warehouse";
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
                //读取sql地址
                string ippath = "C://Mqtt" + @"\SqlConfig.txt";
                if (File.Exists(ippath) == false)//创建保存串口信息的文件
                    File.Create(ippath).Close();
                StreamReader sr = new StreamReader(ippath, Encoding.Default);
                line = sr.ReadLine();

                if (line == null)
                {
                   

                }
                else
                {
                    string[] arr = line.Split('+');
                    name = arr[0];
                    pwd = arr[1];
                }
                //关闭文件输入流
                sr.Close();
            }catch(Exception ee)
            {
            }
            strConn = "Server=" + name + "; Database=Factory;user=sa;password=" + pwd;
            connection = new SqlConnection(strConn);
            command = new SqlCommand();
            sda = new SqlDataAdapter();
            connection.Open();
            
        }
        public void Close()
        {
            connection.Close();
            connection.Dispose();
        }
    }
}
