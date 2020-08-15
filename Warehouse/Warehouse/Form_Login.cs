using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Update;
using System.Diagnostics;
using System.IO;
using MySql.Data.MySqlClient;

namespace Warehouse
{
    
    public partial class Form_Login : Form
    {
        //输入框水印  Win32Utility.SetCueText(textBox2, "请输入密码。。。");
        Users user_login = new Users();
        private MainForm mainform = null;
        private int SaveUsrInfo = 1;//是否需要保存密码
        public Form_Login()
        {
            InitializeComponent();
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

             string path = System.Windows.Forms.Application.StartupPath;
             if (File.Exists(path + "\\userinfo.txt") == false)//创建保存串口信息的文件
                 File.Create(path + "\\userinfo.txt").Close();

             StreamReader sr = new StreamReader(path + "\\userinfo.txt");//去除 Encoding.Default解决了中文乱码
            String line = sr.ReadLine();
            if (line != null)
            {
                String[] UserInfo = line.Split('+');
                if (UserInfo.Length == 2){
                    textBox1.Text = UserInfo[0];
                    textBox2.Text = UserInfo[1];

                }
            }
            sr.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Equals(""))
                MessageBox.Show("请输入用户名", "提示");
            else if (textBox2.Text.Equals(""))
                MessageBox.Show("请输入密码", "提示");
            else
            {
                try
                {
                    //DataBase db = new DataBase();
                    //db.command.CommandText = "select * from [user]";
                    //db.command.Connection = db.connection;

                    //db.Dr = db.command.ExecuteReader();
                    MySqlConn msc = new MySqlConn();
                    string sql = "select * from user";
                    MySqlDataReader rdr = msc.getDataFromTable(sql);
                    user_login.logout();
                    while (rdr.Read())
                    {
                        if (rdr["UserName"].ToString().Equals(textBox1.Text.Trim()))
                        {//当存在用户时，首先将用户名提取出来
                            user_login.name = textBox1.Text;
                            if (rdr["PassWord"].ToString().Equals(textBox2.Text))
                            {//如果密码也相同，则提取权限
                                user_login.admin = rdr["Admin"].ToString();
                                label3.Visible = true;
                                button1.Enabled = false;
                                textBox2.Text = "";
                                if (SaveUsrInfo == 1)
                                {//点击了保存用户名密码按钮
                                    string path = System.Windows.Forms.Application.StartupPath;
                                    FileStream fs = new FileStream(path + "\\userinfo.txt", FileMode.Create, FileAccess.Write);
                                    StreamWriter sw = new StreamWriter(fs);
                                    sw.WriteLine(user_login.name + "+" + rdr["PassWord"].ToString());
                                    sw.Flush();
                                    sw.Close();
                                    fs.Close();

                                }
                                

                                Thread.Sleep(50);

                                new Thread(Invoke_show).Start();
                                break;
                                //Thread thread = new Thread(show_mainform);
                                //thread.IsBackground = false;
                                //thread.Start();
                            }
                            else
                            {
                                MessageBox.Show("密码错误","提示");
                            }
                        }
                    }
                    if (user_login.name.Equals(""))
                        MessageBox.Show("用户不存在","提示");
                    rdr.Close();

                }
                catch (Exception exc)
                {
                    //DataBase database = new DataBase();
                    //string sql_createT = "create table [user] (UserName nvarchar(MAX), PassWord nvarchar(MAX), Admin int);";
                    //database.command.CommandText = sql_createT;
                    //database.command.Connection = database.connection;
                    //database.command.ExecuteNonQuery();

                    //string sql_init = "insert into [user] values ('root', '123', 1);";
                    //database.command.CommandText = sql_init;
                    //database.command.ExecuteNonQuery();
                    MessageBox.Show("数据库连接出错，请检查数据库连接\r\n错误代码：" + exc.ToString(), "提示");
                    return;
                }


            }
        }

        private void Invoke_show(object obj)
        {

            MethodInvoker meth = new MethodInvoker(show_mainform);
            BeginInvoke(meth);
            //Application.Run(new MainForm(user_login.name, user_login.admin));
        }

        private void show_mainform()
        {
            
            MainForm mainform;//显示主页面
            mainform = new MainForm(user_login.name, user_login.admin,this);
            mainform.Show();

            this.Visible = false;//隐藏当前页面
            button1.Enabled = true;
            label3.Visible = false;
            //this.Close();

        }


        private void textBox2_KeyDowm(object sender, KeyEventArgs e)
        {//为了解决keyup的回调问题，将KeyUp改为KeyDown
            if (e.KeyCode == Keys.Return)
            {
                button1.PerformClick();
            }
        }


        private void Form_Login_FormClosed(object sender, FormClosedEventArgs e)
        {
            //Application.Exit();
            System.Environment.Exit(0);
        }


        private void ConnectSever()
        {
            try
            {
                //检查更新
                InitializationUpdate iu = new InitializationUpdate();
                iu.NowVersion();
                iu.DownloadCheckUpdateXml();
                iu.LatestVersion();

                //MessageBox.Show("新版本功能：");
                if (iu.latesversion > iu.localversion)
                {
                    MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                    DialogResult dr = MessageBox.Show("检测到新版本，是否更新?", "提示", messButton);
                    if (dr == DialogResult.OK)
                    {
                        Process.Start(System.Windows.Forms.Application.StartupPath + "\\Update.exe");
                    }
                    else
                    {
                        return;
                    }
                }
            }
            catch(Exception ee)
            {
                MessageBox.Show("网络连接出现问题。问题描述：" + ee.ToString());
            }
           
        }

        private void Form_Login_Load(object sender, EventArgs e)
        {
            //Thread sever = new Thread(ConnectSever);//2018-11-14 去掉自动获取更新操作
            //sever.Start();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.CheckState == CheckState.Checked)
            {
                SaveUsrInfo = 1;
            }
            else
                SaveUsrInfo = 0;
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
