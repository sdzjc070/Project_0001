using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyTest
{
   public  class TransCoding
    {
        public string readbuffer = "";//读缓冲区
        public string writebuffer = "";//写缓冲区
        private Mutex file_mutex = new Mutex();//文件互斥锁
        public string instruction = "";//数据报指令部分
        public string device = "";//设备码
        /// <summary>
        /// 检测收到的数据
        /// </summary>


        //根据一段字符串计算出CRC校验码
        unsafe static char Cal_crc8(char* ptr, int len)
        {//生成CRC8校验码函数
            char crc;
            crc = (char)0;
#pragma warning disable CS0219 // 变量“j”已被赋值，但从未使用过它的值
            int j = 0;
#pragma warning restore CS0219 // 变量“j”已被赋值，但从未使用过它的值
            while ((len--) > 0)
            {
                crc ^= *ptr++;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc = (char)((crc << 1) ^ 0x07);//检验序列x8+x2+x1+1，即100000111，舍高位，即0x07
                    }
                    else crc <<= 1;
                }
            }
            return crc;
        }


        //根据指令中的一些信息（厂区码，料仓编号，指令编号，数据部分），计算CRC。
        public string Crc(string str1, string str2, string str3, string str4)
        {//将textbox中的字符串转变成可进行crc的char[]并生成CRC8校验码
            //str1 = 12345, str2=0x05,str3=0x02, str4=0x0618
            //str1表示厂区码，str2表示设备地址，str3表示指令码，str4表示参数
            //MessageBox.Show(str1 + "\n" + str2 + "\n" + str3 + "\n" + str4);
            string str = str1;
            int text2 = Convert.ToInt32(str2, 16);//将str2中的字符串转为十六进制
            int text4 = Convert.ToInt32(str3, 16);//将str3中的字符串转为十六进制
            //long tt = Convert.ToInt64(str4, 10);//tt是str4中string的十进制int表示
            //string st = tt.ToString("x4");//st是t的十六进制
            string st = str4;
            string t1 = "";

            long t1i;//十六进制的前两位转成十进制
            char[] cc = new char[str1.Length + str2.Length / 2 + str3.Length / 2 + str4.Length];
            for (int i = 0; i < str.Length; i++)
            {
                cc[i] = (char)(str[i] - '0');
            }//厂区码添加到cc中
            cc[5] = (char)(text2);
            cc[6] = (char)(text4);

            if (str4.Length == 4)//不等于4时正常计算
            {
                str4 = str4.PadLeft(str4.Length * 2, '0');
            }
            for (int i = 0; i < (str4.Length) / 2; i++)
            {
                t1 = (str4[2 * i] + "" + str4[2 * i + 1]).ToUpper();
                t1i = Int32.Parse(t1, System.Globalization.NumberStyles.HexNumber);
                cc[7 + i] = (char)(t1i);
            }
            string crc = "";
            unsafe
            {
                char* ch = stackalloc char[cc.Length];
                for (int i = 0; i < cc.Length; i++)
                {
                    ch[i] = cc[i];
                    int t = ch[i];
                }
                int res = Cal_crc8(ch, cc.Length);
                crc = res.ToString("x2");
                crc = (crc[crc.Length - 2] + "" + crc[crc.Length - 1]);
            }
            return crc.ToUpper();
        }

        //用于生成步进角的Crc验证
        public string Crc1(string str1, string str2, string str3, string str4)//厂区码，设备id，指令码，数据
        {//将textbox中的字符串转变成可进行crc的char[]并生成CRC8校验码
            //str1 = 12345, str2=0x05,str3=0x02, str4=0x0618
            //str1表示厂区码，str2表示设备地址，str3表示指令码，str4表示参数
            //MessageBox.Show(str1 + "\n" + str2 + "\n" + str3 + "\n" + str4);
            string str = str1;
            int text2 = Convert.ToInt32(str2, 16);//将str2中的字符串转为十六进制
            int text4 = Convert.ToInt32(str3, 16);//将str3中的字符串转为十六进制
            //long tt = Convert.ToInt64(str4, 10);//tt是str4中string的十进制int表示
            //string st = tt.ToString("x4");//st是t的十六进制
            string st = str4;
            string t1 = "";

            long t1i;//十六进制的前两位转成十进制
            char[] cc = new char[str1.Length + str2.Length / 2 + str3.Length / 2 + str4.Length];//str4的长度是4
            for (int i = 0; i < str.Length; i++)
            {
                cc[i] = (char)(str[i] - '0');
            }//厂区码添加到cc中
            cc[5] = (char)(text2);
            cc[6] = (char)(text4);

            if (str4.Length == 4)
            {
                str4 = "0001" + str4;//左边拼上0100。
            }
            for (int i = 0; i < (str4.Length) / 2; i++)
            {
                t1 = (str4[2 * i] + "" + str4[2 * i + 1]).ToUpper();
                t1i = Int32.Parse(t1, System.Globalization.NumberStyles.HexNumber);
                cc[7 + i] = (char)(t1i);
            }
            string crc = "";
            unsafe
            {
                char* ch = stackalloc char[cc.Length];
                for (int i = 0; i < cc.Length; i++)
                {
                    ch[i] = cc[i];
                    int t = ch[i];
                }
                int res = Cal_crc8(ch, cc.Length);
                crc = res.ToString("x2");
                crc = (crc[crc.Length - 2] + "" + crc[crc.Length - 1]);
            }
            return crc.ToUpper();
        }



        /// <summary>
        /// 将12345变成0102030405
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string oneTotwo(string str)
        {
            string retstr = "";
            for (int i = 0; i < str.Length; i++)
            {
                retstr += "0" + str[i];
            }
            return retstr;
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
        /// <summary>
        /// 字节数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string byteToHexStr(byte[] bytes)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    returnStr += bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }

        //通过传入一条指令所需要的信息，打包成一条完整的连续的指令。
        public string Data(string str1, string str2, string str3, string str4)
        {
            
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");
           

            str4 = str4.PadLeft(4, '0');
            for (int i = 0; i < str4.Length / 4; i++)
            {
                long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                s4 += int4.ToString("x4");
            }
            //str4 = long.Parse(str4).ToString("x4");
            string crc = Crc(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = s4.PadLeft(8, '0');
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        //通过传入一条指令所需要的信息，打包成一条完整的连续的指令。
        public string Dataq(string str1, string str2, string str3, string str4)
        {
   
            str2 = str2.Substring(0, str2.Length - 1);
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");
         

            str4 = str4.PadLeft(4, '0');
            for (int i = 0; i < str4.Length / 4; i++)
            {
                long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                s4 += int4.ToString("x4");
            }
            //str4 = long.Parse(str4).ToString("x4");
            string crc = Crc(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = s4.PadLeft(8, '0');
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        public string DataF(string str1, string str2, string str3, string str4)
        {
         
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");
            string xiaoshu = "";
        

            double d = double.Parse(str4);
            int a = (int)Math.Floor(d);//整数部分

            string zhengshu=Convert.ToString(a, 16);//整数十六进制
            if (str4.Length == 4)
            {
                xiaoshu = "0" + str4[3];//小数十六进制
            }
            else if (str4.Length == 2)
            {
                xiaoshu = "00";//小数十六进制
            }

            if (zhengshu.Length == 1)
            {
                zhengshu = "0" + zhengshu;
            }

            s4+= "01"+zhengshu + xiaoshu;


            string crc = Crc(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示

            if (s4.Length == 6)
                s4 = s4.PadLeft(8, '0');
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }


        public string DataZ(string str1, string str2, string str3, string str4)
        {
            
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");
            string xiaoshu = "";

            double d = double.Parse(str4);
            int a = (int)Math.Floor(d);//整数部分

            string zhengshu = Convert.ToString(a, 16);//整数十六进制
            if (str4.Length == 4)
            {
                 xiaoshu = "0" + str4[3];//小数十六进制
            }else if(str4.Length == 2)
            {
                xiaoshu = "00" ;//小数十六进制
            }
  
            if (zhengshu.Length == 1)
            {
                zhengshu = "0" + zhengshu;
            }

            s4 =zhengshu + xiaoshu;



            string crc = Crc1(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示

            if (s4.Length == 4)
                s4 = "0100" + s4;
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        public string DataGetCAnangle(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {
            string str = "";
            string s4 = "";
            try
            {

                int int2 = int.Parse(str2);
                string str2_16 = int2.ToString("x2");
                int int3 = int.Parse(str3);
                string str3_16 = int3.ToString("x2");
      


                s4 = "00"+str4+"00" ;
                //for (int i = 0; i < str4.Length / 4; i++)
                //{
                //    long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                //    s4 += int4.ToString("x4");//变成16进制...X为 十六进制 2为 每次都是两位数 4为 每次都是4位数 
                //}
                //str4 = long.Parse(str4).ToString("x4");

                string crc = CrcCanagle(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示

                if (s4.Length == 4)
                    s4 = s4.PadLeft(8, '0');
                str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
                //MessageBox.Show(str);
                str = ":0C" + str + "\r";

            }
            catch(Exception e)
            {
                Log("组装指令失败" + e.ToString());
            }


            return str;//要发送的数据报

        }

        public string DataGetCAnangleF(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {
            string str = "";
            string s4 = "";
            try
            {

                int int2 = int.Parse(str2);
                string str2_16 = int2.ToString("x2");
                int int3 = int.Parse(str3);
                string str3_16 = int3.ToString("x2");



                s4 = "01" + str4 + "00";
                //for (int i = 0; i < str4.Length / 4; i++)
                //{
                //    long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                //    s4 += int4.ToString("x4");//变成16进制...X为 十六进制 2为 每次都是两位数 4为 每次都是4位数 
                //}
                //str4 = long.Parse(str4).ToString("x4");

                string crc = CrcCanagle(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示

                if (s4.Length == 4)
                    s4 = s4.PadLeft(8, '0');
                str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
                //MessageBox.Show(str);
                str = ":0C" + str + "\r";

            }
            catch (Exception e)
            {
                Log("组装指令失败" + e.ToString());
            }


            return str;//要发送的数据报

        }

        public string CrcCanagle(string str1, string str2, string str3, string str4)
        {//将textbox中的字符串转变成可进行crc的char[]并生成CRC8校验码
            //str1 = 12345, str2=0x05,str3=0x02, str4=0x0618
            //str1表示厂区码，str2表示设备地址，str3表示指令码，str4表示参数
            //MessageBox.Show(str1 + "\n" + str2 + "\n" + str3 + "\n" + str4);
            string str = str1;
            int text2 = Convert.ToInt32(str2, 16);//将str2中的字符串转为十六进制..将16进制的12转为10进制整数
            int text4 = Convert.ToInt32(str3, 16);//将str3中的字符串转为十六进制
            //long tt = Convert.ToInt64(str4, 10);//tt是str4中string的十进制int表示
            //string st = tt.ToString("x4");//st是t的十六进制
            string st = str4;
            string t1 = "";

            long t1i;//十六进制的前两位转成十进制
            char[] cc = new char[str1.Length + str2.Length / 2 + str3.Length / 2 + 4];
            for (int i = 0; i < str.Length; i++)
            {
                cc[i] = (char)(str[i] - '0');
            }//厂区码添加到cc中
            cc[5] = (char)(text2);
            cc[6] = (char)(text4);

            for (int i = 0; i < (str4.Length) / 2; i++)
            {
                t1 = (str4[2 * i] + "" + str4[2 * i + 1]).ToUpper();
                t1i = Int32.Parse(t1, System.Globalization.NumberStyles.HexNumber);
                cc[7 + i] = (char)(t1i);
            }
            string crc = "";
            unsafe
            {
                char* ch = stackalloc char[cc.Length];
                for (int i = 0; i < cc.Length; i++)
                {
                    ch[i] = cc[i];
                    int t = ch[i];
                }
                int res = Cal_crc8(ch, cc.Length);
                crc = res.ToString("x2");
                crc = (crc[crc.Length - 2] + "" + crc[crc.Length - 1]);
            }
            return crc.ToUpper();
        }
        /// <summary>
        /// 将字符串转成二进制
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string bianma(string s)
        {
            bool isFu = false;
            string res = "";
            int num = int.Parse(s);
            if (num < 0)//判断是否是负数
            {
                num = 0 - num;//是负数就先变成正数
                isFu = true;
            }


            while (num != 0)
            {
                int mod = num % 2;
                num = (int)num / 2;
                res = mod + res;
            }
            res = res.PadLeft(7, '0');

            //判断最高为上的数字
            if (isFu)
            {
                res = '1' + res;
            }
            else
            {
                res = '0' + res;
            }
            return res.ToString();
        }



        /// <summary>
        /// 解码函数
        /// 通过一条指令，将其解析成指令中含有的信息，
        /// 包括厂区码，料仓编号，指令编号，以及数据部分。
        /// 并使用空格将其分割开，返回这个使用空格分隔开的字符串。
        /// </summary>
        /// <param name="str"></param>
        public string decoding(string str)
        {

            string len = str[1] + "" + str[2];
            int l = Int32.Parse(len, System.Globalization.NumberStyles.HexNumber);
            string crc = str[str.Length - 4] + "" + str[str.Length - 3];
            if (l != (str.Length - 5) / 2)
            {
                return "0";
            }

            string fac = "";
            for (int i = 0; i < 5; i++)
            {
                string s = str[2 * i + 3] + "" + str[2 * i + 4];//截取指令中的十六进制数。在指令中每两位十六进制数对应一个十进制数
                int a = Int32.Parse(s);//将字符串转成int类型
                fac += a.ToString();
            }

            string equip = str[13] + "" + str[14];
            //int equip_int = Int32.Parse(equip, System.Globalization.NumberStyles.HexNumber);
            //equip = equip_int.ToString();
            string oper = str[15] + "" + str[16];
            string data = "";
            string crc_data = "";//用于测试crc的data数据
            if (oper == "23")
            {
                int id = Int32.Parse(equip, System.Globalization.NumberStyles.HexNumber);//料仓地址的十进制表示 
                for (int i = 0; i < 2; i++)//两次循环取出体积重量
                {
                    //d表示整数位，f表示小数位
                    string d = str[6 * i + 17] + "" + str[6 * i + 18] + "" + str[6 * i + 19] + "" + str[6 * i + 20];
                    string f = str[6 * i + 21] + "" + str[6 * i + 22];
                    float d_int = Int32.Parse(d, System.Globalization.NumberStyles.HexNumber);
                    float f_int = Int32.Parse(f, System.Globalization.NumberStyles.HexNumber);
                    data += ((d_int + f_int / 100).ToString() + "+");
                }
                //////////////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!取出。扫描点数，出错点数，纠正点数
                //29 30 = 字节6
                string error1 = str[29] + "" + str[30];//点数
                string error2 = str[31] + "" + str[32];


                //前7个字节一个样


                //
                string sql = "select * from bininfo where BinID=" + id.ToString().PadLeft(2, '0');
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

                if (type.Equals("1") || type.Equals("4"))
                {
                    Log("平面扫描算法");
                    //字节8。。两部分
                    string all1 = str[33] + "" + str[34];//0x10

                    //字节9
                    string all2 = str[35] + "" + str[36];//0x04

                    string zong = all1 + all2;


                    //int e3 = Int32.Parse("012c", System.Globalization.NumberStyles.HexNumber);

                    //MessageBox.Show("大小为：" + e3);


                    //字节10：盘库模式
                    string mode = str[37] + "" + str[38];

                    int e1 = Int32.Parse(error1, System.Globalization.NumberStyles.HexNumber);
                    int e2 = Int32.Parse(error2, System.Globalization.NumberStyles.HexNumber);
                    int e3 = Int32.Parse(zong, System.Globalization.NumberStyles.HexNumber);

                    int model = Int32.Parse(mode, System.Globalization.NumberStyles.HexNumber);
                    data += e1.ToString() + "+" + e2.ToString() + "+" + e3.ToString() + "+" + model.ToString() + "+";//查看一下all1和all2是什么
                }
                else
                {
                    Log("!!!!!!!直径扫描算法");
                    string error3 = str[33] + "" + str[34];//全部采样点个数 字节8

                    //字节9：盘库模式
                    string mode = str[35] + "" + str[36];

                    int e1 = Int32.Parse(error1, System.Globalization.NumberStyles.HexNumber);
                    int e2 = Int32.Parse(error2, System.Globalization.NumberStyles.HexNumber);
                    int e3 = Int32.Parse(error3, System.Globalization.NumberStyles.HexNumber);

                    int model = Int32.Parse(mode, System.Globalization.NumberStyles.HexNumber);
                    data += e1.ToString() + "+" + e2.ToString() + "+" + e3.ToString() + "+" + model.ToString() + "+";

                }

                //string error3 = str[33] + "" + str[34];
                //int e1 = Int32.Parse(error1, System.Globalization.NumberStyles.HexNumber);
                //int e2 = Int32.Parse(error2, System.Globalization.NumberStyles.HexNumber);
                //int e3 = Int32.Parse(error3, System.Globalization.NumberStyles.HexNumber);
                //data += e1.ToString() + "+" + e2.ToString() + "+" + e3.ToString() + "+";
                ////获取盘库模式：
                //string moshi = str[35] + "" + str[36];
                //int ms = Int32.Parse(moshi, System.Globalization.NumberStyles.HexNumber);
                //data += ms.ToString() + "+";

            }
            else if (oper == "11")
            {
                string temp1 = str[17] + "" + str[18];//温度的整数部分
                string temp2 = str[19] + "" + str[20];//温度的小数部分
                crc_data += (temp1 + temp2);
                int temp1_int = Int32.Parse(temp1, System.Globalization.NumberStyles.HexNumber);//温度整数部分转化为十进制
                float temp2_int = Int32.Parse(temp2, System.Globalization.NumberStyles.HexNumber);//温度小数部分转化为十进制
                temp1 = Convert.ToString(temp1_int, 2);//temp的二进制表示
                temp1 = temp1.PadLeft(8, '0');//温度二进制的格式化表示

                if (temp1[0] == '1')
                {
                    temp1_int = temp1_int - 128;
                    temp1_int = -temp1_int;
                    temp2_int = -temp2_int;
                    data = ((temp1_int + temp2_int / 100).ToString());
                }
                else
                {
                    data += ((temp1_int + temp2_int / 100).ToString());
                }
                data += "+";
                string hum1 = str[21] + "" + str[22];//湿度的整数部分
                string hum2 = str[23] + "" + str[24];//湿度的小数部分
                crc_data += (hum1 + hum2);
                float hum1_int = Int32.Parse(hum1, System.Globalization.NumberStyles.HexNumber);//湿度整数部分转化为十进制
                float hum2_int = Int32.Parse(hum2, System.Globalization.NumberStyles.HexNumber);//湿度小数部分转化为十进制
                data += ((hum1_int + hum2_int / 100).ToString());
            }
            else if (oper == "25")
            {

              
                //查询数据库
                int id = Int32.Parse(equip, System.Globalization.NumberStyles.HexNumber);//料仓地址的十进制表示 
                string sql = "select * from bininfo where BinID=" + id.ToString().PadLeft(2, '0');
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
                    Log("Data25查询数据库出错。" + se.ToString());
                }

                if (type.Equals("1") || type.Equals("4"))
                {
                    //平面扫描算法
                    string Chuiangle = str[17] + "" + str[18];//垂直扫描角

                    int angle_Chui = Int32.Parse(Chuiangle, System.Globalization.NumberStyles.HexNumber);

                    string Shuiangle = str[19] + "" + str[20];//水平扫描角..最高位1为负数
                    int temp1_int = Int32.Parse(Shuiangle, System.Globalization.NumberStyles.HexNumber);//将水平角度部分转化为十进制

                    string temp1 = Convert.ToString(temp1_int, 2);//将十进制转为二进制表示
                    temp1 = temp1.PadLeft(8, '0');//二进制的格式化表示


                    int angle_Shui = 0;
                    if (temp1[0] == '1')
                    {
                        temp1_int = temp1_int - 128;
                        temp1_int = -temp1_int;
                        angle_Shui = temp1_int;
                    }
                    else
                    {
                        angle_Shui = temp1_int;
                    }


                    string distance = str[21] + "" + str[22] + "" + str[23] + "" + str[24];//距离（厘米）



                    int distance_int = Int32.Parse(distance, System.Globalization.NumberStyles.HexNumber);
                    //Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!垂直角16进制="+ Chuiangle + "水平16进制  =" + Shuiangle + "水平10进制value ===" + angle_Shui);
                    //获取的回传数据：垂直角度+水平角度+长度
                    data = angle_Chui.ToString() + "+" + angle_Shui.ToString() + "+" + distance_int.ToString();
                    //Log("平扫数据:" + data);
                }
                else if (type.Equals("0") || type.Equals("2"))
                {

                    string angle = str[17] + "" + str[18];//角度值
                    string distance = str[19] + "" + str[20] + "" + str[21] + "" + str[22];//距离，单位厘米
                    string schedule = str[23] + "" + str[24];//进度，完成是0x64

                    int angle_int = Int32.Parse(angle, System.Globalization.NumberStyles.HexNumber);
                    int distance_int = Int32.Parse(distance, System.Globalization.NumberStyles.HexNumber);
                    int schedule_int = Int32.Parse(schedule, System.Globalization.NumberStyles.HexNumber);
                    //获取的回传数据：垂直角度+长度+进度
                    data = angle_int.ToString() + "+" + distance_int.ToString() + "+" + schedule_int.ToString();
                   //Log("直径数据:" + data);
                }




            }
            else if (oper == "21")
            {
                string complet = str[17] + "" + str[18];//是否盘库完成
                string schedule_hex = str[19] + "" + str[20];//盘库进度
                string status_hex = str[23] + "" + str[24];//料仓状态
                int schedule_int = Int32.Parse(schedule_hex, System.Globalization.NumberStyles.HexNumber);
                int status_int = Int32.Parse(status_hex, System.Globalization.NumberStyles.HexNumber);
                data = complet + "+" + status_int.ToString().PadLeft(2, '0') + "+" + schedule_int.ToString();
                //for (int i = 0; i < (l - 8) / 2; i++)
                //{
                //    string d =  str[4 * i + 19] + "" + str[4 * i + 20];

                //    crc_data += d;
                //    int a = Int32.Parse(d, System.Globalization.NumberStyles.HexNumber);
                //    data += (a.ToString().PadLeft(4, '0')) + "";
                //}
                //data = complet + "+" + data;
            }
            else if (oper == "0B")
            {
                String Margin = str[17] + "" + str[18] + "" + str[19] + "" + str[20];//边距
                String Top = str[21] + "" + str[22] + "" + str[23] + "" + str[24];//顶高度
                String Wheelbase = str[25] + "" + str[26] + "" + str[27] + "" + str[28];//轴距
                int Margin_int = Int32.Parse(Margin, System.Globalization.NumberStyles.HexNumber);
                int Top_int = Int32.Parse(Top, System.Globalization.NumberStyles.HexNumber);
                int Wheelbase_int = Int32.Parse(Wheelbase, System.Globalization.NumberStyles.HexNumber);
                data = Margin_int.ToString() + "+" + Top_int.ToString() + "+" + Wheelbase_int.ToString();
            }
            else if (oper == "0D")
            {
                String Speed = str[17] + "" + str[18];
                int Speed_int = Int32.Parse(Speed, System.Globalization.NumberStyles.HexNumber);
                switch (Speed_int)
                {
                    case 0:
                        Speed_int = 300;
                        break;
                    case 1:
                        Speed_int = 1200;
                        break;
                    case 2:
                        Speed_int = 2400;
                        break;
                    case 3:
                        Speed_int = 4800;
                        break;
                    case 4:
                        Speed_int = 9600;
                        break;
                    case 5:
                        Speed_int = 19200;
                        break;
                }

                String Channel = str[19] + "" + str[20];
                String ModelAddress = str[21] + "" + str[22] + "" + str[23] + "" + str[24];
                int Channel_int = Int32.Parse(Channel, System.Globalization.NumberStyles.HexNumber);
                int Address_int = Int32.Parse(ModelAddress, System.Globalization.NumberStyles.HexNumber);

                data = Speed_int.ToString() + "+" + Channel_int.ToString() + "+" + Address_int.ToString();

            }
            else if (oper == "2F")//回复垂直校准
            {
                string str1 = str[17] + "" + str[18];//正负标志
                string str2 = str[19] + "" + str[20];//整数
                string str3 = str[21] + "" + str[22];//小数
                string str4 = str[23] + "" + str[24];

                int ca2 = Int32.Parse(str2, System.Globalization.NumberStyles.HexNumber);//整数
                float ca3 = Int32.Parse(str3, System.Globalization.NumberStyles.HexNumber);//小数
                //Log("ca2" + ca2 + "ca3" + ca3);

                float cangle = (ca2 + ca3 / 10);//角度差数值

                //Log("正负" + str1 + "数值" + cangle);

                data = "" + str1 + "+" + cangle;


            }
            else if (oper == "0F")
            {
                String sign_Correction = str[17] + "" + str[18];//垂直校准符号
                String Corr_vertical1 = str[19] + "" + str[20];//校准角度的整数部分
                String Corr_vertical2 = str[21] + "" + str[22];//校准角度的小数部分
                int Corr2_int = Int32.Parse(Corr_vertical2, System.Globalization.NumberStyles.HexNumber);
                String Correction_per = str[23] + "" + str[24];//比例校正百分比
                String Correction = str[25] + "" + str[26];//加减校正值
                String temp = Corr_vertical1 + "." + Corr2_int.ToString();
                //MessageBox.Show(temp);
                //int vertical = Int32.Parse(temp, System.Globalization.NumberStyles.HexNumber);
                if (sign_Correction.Equals("01"))
                {
                    temp = "-" + temp;
                }

                int Corr_percent = Int32.Parse(Correction_per, System.Globalization.NumberStyles.HexNumber);
                int Corr_int = Int32.Parse(Correction, System.Globalization.NumberStyles.HexNumber);
                if (Corr_int > 128)
                {
                    Corr_int = -(Corr_int - 128);
                }

                data = temp.ToString() + "+" + Corr_percent + "+" + Corr_int;
            }
            else
            {
                for (int i = 0; i < (l - 8) / 2; i++)
                {
                    string d = str[4 * i + 17] + "" + str[4 * i + 18] + "" + str[4 * i + 19] + "" + str[4 * i + 20];

                    crc_data += d;
                    int a = Int32.Parse(d, System.Globalization.NumberStyles.HexNumber);
                    data += (a.ToString().PadLeft(4, '0')) + "";
                }
                if (oper != "01")
                {
                    int data_int = Int32.Parse(crc_data, System.Globalization.NumberStyles.HexNumber);
                    crc_data = data_int.ToString("x4");//.PadLeft(4, '0');
                    if (crc.Equals(Crc(fac, equip, oper, crc_data)) == false)
                    {
                        return "0";
                    }
                }

            }

            //MessageBox.Show(fac + " " + equip + " " + oper + " " + data);
            return fac + " " + equip + " " + oper + " " + data;
            //返回的是厂区码+设备地址的十六进制+操作码的十六进制+数据的十进制
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

    }
}
