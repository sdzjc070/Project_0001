using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Warehouse
{
    static class Program
    {
        public static Form_Login form;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            Process[] processes = System.Diagnostics.Process.GetProcessesByName(Application.CompanyName);
            if (processes.Length > 1)
            {
                MessageBox.Show("软件正在运行","提示");
            }
            else
            {
                //SystemSleepManagement.PreventSleep();
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                form = new Form_Login();

                Application.Run(form);


            }
           
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string path = System.Windows.Forms.Application.StartupPath;
            FileStream fs = new FileStream(path + "\\StopRun.txt", FileMode.Create | FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(DateTime.Now.ToString() + " 发生异常 " + e.ExceptionObject.ToString() + "\r\n\r\n");
            sw.Flush();
            sw.Close();
            fs.Close();
            Environment.Exit(-1);
        }
    }
}
