using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace Update
{
    public class InitializationUpdate
    {
        //使用WebClient下载
        public WebClient client = new WebClient();
        //解压
        //public ZipHelper zh = new ZipHelper();

        public Version latesversion;//最新版本
        public Version localversion;//当前版本
        //主窗体
        public Form1 form;
        //通知内容
        public string nnidtext = null;
        //主程序路径
        public static string route = System.Windows.Forms.Application.StartupPath;
        //更新程序路径
        public string updateDir = route + @"\Update";
        //服务器地址
        public string serverURL = "http://47.93.193.217:8080";
        //压缩包下载地址
        public string zipUrl = null;
        //压缩包名
        public string zipname = null;
        //更新信息
        public string upInformation = null;


        //获取本地版本号
        public void NowVersion()
        {

            System.Diagnostics.FileVersionInfo fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(route + @"\Warehouse.exe");
            localversion = new Version(fv.FileVersion);
        }

        /// <summary>
        /// 从服务器上获取最新的版本号
        /// </summary>
        public void DownloadCheckUpdateXml()
        {

            try
            {
                // 判断目标目录是否存在如果不存在则新建
                if (!System.IO.Directory.Exists(updateDir))
                {
                    System.IO.Directory.CreateDirectory(updateDir);
                }
                //第一个参数是文件的地址,第二个参数是文件保存的路径文件名
                client.DownloadFile(serverURL + @"/MyUpdate/DataInput/update.xml", updateDir + @"\update.xml");
            }
            catch (Exception exc)
            {
                nnidtext = "没有检测到更新";
                //MessageBox.Show("网络连接失败");
                //Environment.Exit(0);
            }
        }

        /// <summary>
        /// 读取从服务器获取的最新版本号
        /// </summary>
        public void LatestVersion()
        {
            if (File.Exists(updateDir + @"\update.xml"))
            {
                XmlDocument doc = new XmlDocument();
                //加载要读取的XML
                doc.Load(updateDir + @"\update.xml");

                //获得子节点 返回节点的集合
                XmlNode Update = doc.SelectSingleNode("update");
                XmlNode version = Update.SelectSingleNode("version");
                XmlNode info = version.FirstChild;
                XmlNode upinfo = version.SelectSingleNode("upinfo");
                upInformation = upinfo.InnerText;
                latesversion = new Version(info.InnerText);
            }
            else if (!File.Exists(updateDir + @"\update.xml"))
            {
                nnidtext = "检查更新失败，请检查网络设置";
                MessageBox.Show(nnidtext);
                //Environment.Exit(0);
            }
        }

        /// <summary>
        /// 下载安装包
        /// </summary>
        public void DownloadInstall()
        {
            if (localversion == null || latesversion == null)
                return;

            if (localversion == latesversion)
            {
                nnidtext = "恭喜你，已经更新到最新版本";
                MessageBox.Show(nnidtext);

            }
            else if (localversion < latesversion && File.Exists(updateDir + @"\update.xml"))
            {
                nnidtext = "发现新版本，即将下载更新补丁";
                XmlDocument doc = new XmlDocument();
                //加载要读取的XML
                doc.Load(updateDir + "\\update.xml");

                //获得子节点 返回节点的集合
                XmlNode Update = doc.SelectSingleNode("update");
                XmlNode version = Update.SelectSingleNode("version");
                XmlNode upPath = version.SelectSingleNode("upPath");
                XmlNode upname = version.SelectSingleNode("upname");
                XmlNode upfileSize = version.SelectSingleNode("upfileSize");
                zipname = upname.InnerText;
                zipUrl = serverURL + upPath.InnerText;
                if (File.Exists(updateDir + @"\" + upname.InnerText))
                {
                    //MessageBox.Show(updateDir + @"/" + upname.InnerText+" "+ route);
                    ZipHelper.UnZip(updateDir + @"/" + upname.InnerText, route);
                    File.Delete(updateDir + @"\update.xml");
                    File.Delete(updateDir + @"/" + upname.InnerText);
                    MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
                    DialogResult dr = MessageBox.Show("升级成功，确认重启软件？", "提示", messButton);
                    if (dr == DialogResult.OK)
                    {
                        Process.Start(route + @"/Warehouse.exe");
                        Application.Exit();
                    }
                    else
                    {
                        Application.Exit();
                    }
                }
                //else if (!File.Exists(updateDir + @"\" + upname.InnerText))
                //{
                //    //如果一次没有下载成功，则检查三次
                //    for (int i = 1; i < 3; i++)
                //    {
                //        client.DownloadFile(upPath.InnerText, updateDir + @"\" + upname);
                //    }
                //    nnidtext = "下载失败，请检查您的网络连接是否正常";
                //    Environment.Exit(0);
                //}
            }
        }

        /// <summary>
        /// 结束程序
        /// </summary>
        public void KillProgram()
        {
            Process[] processList = Process.GetProcesses();
            foreach (Process process in processList)
            {
                //如果程序启动了，则杀死
                if (process.ProcessName == "Warehouse")
                {
                    process.Kill();
                }
            }
        }

    }

}
