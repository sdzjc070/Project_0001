using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Warehouse
{
    public class TransCoding : Form
    {
        public string readbuffer = "";//读缓冲区
        public string writebuffer = "";//写缓冲区

        public string instruction = "";//数据报指令部分
        public string device = "";//设备码
        /// <summary>
        /// 检测收到的数据
        /// </summary>



        unsafe static char Cal_crc8(char* ptr, int len)
        {//生成CRC8校验码函数
            char crc;
            crc = (char)0;
            int j = 0;
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
        //普通生成Crc验证
        public string Crc(string str1, string str2, string str3, string str4)
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

            long t1i ;//十六进制的前两位转成十进制
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

        //控制加热板的生成Crc验证。。。特殊之处在于只是用了前两个字节，所以在str4需要在右侧补充0；之前都是在前面补充0
        public string CrcWenDu(string str1, string str2, string str3, string str4)
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
            char[] cc = new char[str1.Length + str2.Length / 2 + str3.Length / 2 + str4.Length];
            for (int i = 0; i < str.Length; i++)
            {
                cc[i] = (char)(str[i] - '0');
            }//厂区码添加到cc中
            cc[5] = (char)(text2);
            cc[6] = (char)(text4);

            if (str4.Length == 4)
            {
                str4 = str4.PadRight(str4.Length * 2, '0');
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
                str4 = "0100" + str4;//左边拼上0100。
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

        public string Crc2(string str1, string str2, string str3, string str4)//厂区码，设备id，指令码，数据
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
            char[] cc = new char[str1.Length + str2.Length / 2 + str3.Length / 2 + 4];//str4的长度是4
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
        //设置k、b的crc验证
        public string CrcBVal(string str1, string str2, string str3, string str4)//厂区码，设备id，指令码，数据
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

            if (str4.Length == 4)
            {
                str4 = "0100" + str4;//左边拼上0100
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
        public string Data(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {

            //write(str1 + "1*" + str2 + "2**" + str3 + "3***" + str4 + "4****");
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");
            //write(int2 + "int2" + str2_16 + "str2_16" + int3 + "int3" + str3_16 + "str3_16");

            str4 = str4.PadLeft(4, '0');//数据前面加4个0
            for (int i = 0; i < str4.Length / 4; i++)
            {
                long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                s4 += int4.ToString("x4");//变成16进制...X为 十六进制 2为 每次都是两位数 4为 每次都是4位数 
            }
            //str4 = long.Parse(str4).ToString("x4");

            string crc = Crc(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if(s4.Length == 4)
                s4 = s4.PadLeft(8, '0');
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }
        //通过传入一条指令所需要的信息，打包成一条完整的连续的指令。
        public string DataF(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {

            //，write(str1 + "1*" + str2 + "2**" + str3 + "3***" + str4 + "4****");
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");

            str4 = str4.PadLeft(4, '0');//数据前面加4个0
            str4 = "0001" + str4;
            for (int i = 0; i < str4.Length / 4; i++)
            {
                long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                s4 += int4.ToString("x4");//变成16进制...X为 十六进制 2为 每次都是两位数 4为 每次都是4位数 
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
        public string DataGetCAnangle(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");

            str4 = "0001" + str4;
            for (int i = 0; i < str4.Length / 4; i++)
            {
                long int4 = long.Parse(str4[4 * i] + "" + str4[4 * i + 1] + "" + str4[4 * i + 2] + "" + str4[4 * i + 3]);
                s4 += int4.ToString("x4");//变成16进制...X为 十六进制 2为 每次都是两位数 4为 每次都是4位数 
            }
            //MessageBox.Show("s4 = " + s4 + "。str4 = " + str4);
            //str4 = long.Parse(str4).ToString("x4");

            string crc = CrcCanagle(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = s4.PadLeft(8, '0');
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            //MessageBox.Show(str);
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }
        //设置垂直角度、转动的水平角度
        public string DataToCangle(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");

            //判断是否是负数
           
            int num = int.Parse(str4);//将数据转化成数字
            bool isFu = false;
            if (num < 0)
            {
                num = 0 - num;
                isFu = true;
            }

           

            //正数部分
            string shu = "" + num;
            shu = shu.PadLeft(4, '0');
            //MessageBox.Show(shu);
            long int4 = long.Parse(shu[0] + "" + shu[1]);//表示整数位。。。1.5度。。str4为0150   int4为01
            s4 += int4.ToString("x2");//变成16进制.两位表示16机制

            long intXiao = long.Parse("" + shu[2]);//小数部分只有一位，乘上100后，前面要补齐4位需要加一个0.。若输入1.5.则str4位0150 intXiao只有一位，所以是5
            s4 += intXiao.ToString("x2");//变成16进制.两位表示16机制

            //str4 = long.Parse(str4).ToString("x4");

            if (isFu)//是负数
            {
                s4 = "01" + s4 + "00";//数据前面加4个0
            }
            else//是正数
            {
                s4 = "00" + s4 + "00";//数据前面加4个0
            }

            //MessageBox.Show(s4);
            string crc = Crc2(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        //设置初始水平角度
        public string DataToStartShuipingAngle(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");

            //判断是否是负数

            int num = int.Parse(str4);//将数据转化成数字
            bool isFu = false;
            if (num < 0)
            {
                num = 0 - num;
                isFu = true;
            }



            //正数部分
            string shu = "" + num;
            shu = shu.PadLeft(4, '0');
            //MessageBox.Show(shu);
            long int4 = long.Parse(shu[0] + "" + shu[1]);//表示整数位。。。1.5度。。str4为0150   int4为01
            s4 += int4.ToString("x2");//变成16进制.两位表示16机制

            long intXiao = long.Parse("" + shu[2]);//小数部分只有一位，乘上100后，前面要补齐4位需要加一个0.。若输入1.5.则str4位0150 intXiao只有一位，所以是5
            s4 += intXiao.ToString("x2");//变成16进制.两位表示16机制

            //str4 = long.Parse(str4).ToString("x4");

            if (isFu)//是负数
            {
                s4 = "01" + s4 + "01";//数据前面加4个0....设置角度，数值，逆时针（负数）
            }
            else//是正数
            {
                s4 = "01" + s4 + "00";//数据前面加4个0....设置角度，数值，顺时针（正数）
            }

            //MessageBox.Show(s4);
            string crc = Crc2(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
                                                                    //long data = Convert.ToInt64(s4.ToUpper());
                                                                    //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);

            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }


        //设置仓内镜头温度和时间..只是用了前两个字节，所以要在s4后面补0
        public string DataWenDuAndShiJian(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据（十进制的）
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");

            for (int i = 0; i < str4.Length / 2; i++)
            {
                long int4 = long.Parse(str4[2 * i] + "" + str4[2 * i + 1] );
                s4 += int4.ToString("x2");//变成16进制...变成两位的十六进制
            }
            //str4 = long.Parse(str4).ToString("x4");

            string crc = CrcWenDu(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = s4.PadRight(8, '0');//需要在后面补0 
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }





        public string DataSetHangle(string str1, string str2, string str3, string str4)//厂区码，设备吗，发送的请求指令，数据（十进制的）
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2");


            if (str4.Length == 1)//如果是一位数要在左边补0
                str4 = str4.PadLeft(2, '0');//需要在后面补0 


            for (int i = 0; i < str4.Length / 2; i++)
            {
                long int4 = long.Parse(str4[2 * i] + "" + str4[2 * i + 1]);
                s4 += int4.ToString("x2");//变成16进制...变成两位的十六进制
            }

            s4 = "01" + s4;
            //str4 = long.Parse(str4).ToString("x4");

            string crc = CrcWenDu(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
            //long data = Convert.ToInt64(s4.ToUpper());
            //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = s4.PadRight(8, '0');//需要在后面补0 
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }


        //设置步进角的数据信息
        public string DataBuJvJiao(string str1, string str2, string str3, string str4)//str1:厂区码。str2:设备id。str3："40"(0x28)。str4：设置的数据
        {
            
            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");//两位表示16机制
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2"); //两位表示16机制


            //MessageBox.Show(str4 + "*******************");
            str4 = str4.PadLeft(4, '0');
            long int4 = long.Parse(str4[0] + "" + str4[1]);//表示整数位。。。1.5度。。str4为0150   int4为01
            s4 += int4.ToString("x2");//变成16进制.两位表示16机制
            //MessageBox.Show(s4+"第一次");

            long intXiao = long.Parse(""+str4[2]);//小数部分只有一位，乘上100后，前面要补齐4位需要加一个0.。若输入1.5.则str4位0150 intXiao只有一位，所以是5
            s4 += intXiao.ToString("x2");//变成16进制.两位表示16机制
            //MessageBox.Show(s4);
            //str4 = long.Parse(str4).ToString("x4");

            string crc = Crc1(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
                                                                   //long data = Convert.ToInt64(s4.ToUpper());
                                                                   //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = "0100" + s4;
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        //设置边长的数据信息
        public string DataLong(string str1, string str2, string str3, string str4)//str1:厂区码。str2:设备id。str3："40"(0x28)。str4：设置的数据
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");//两位表示16机制
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2"); //两位表示16机制



            str4 = bianma(str4);//将长度转换为二进制
            //MessageBox.Show("str原始=====" + str4);
            str4 = str4.PadLeft(16, '0');//将二进制数填充为两个字节。一共16位。
            //MessageBox.Show("str4=====" + str4);

            string jie1 ="";
            string jie2 = "";
            for (int i = 0; i < 8; i++)
            {
                jie1 += str4[i];
                jie2 += str4[i + 8];
            }
            //MessageBox.Show("jie1=====" + jie1);
            //MessageBox.Show("jie2=====" + jie2);
            string gao = string.Format("{0:X}", System.Convert.ToInt32(jie1, 2)).PadLeft(2, '0');//二进制转换为十六进制
            string di = string.Format("{0:X}", System.Convert.ToInt32(jie2, 2)).PadLeft(2, '0');//二进制转换为十六进制


            s4 +=gao + di;


            //MessageBox.Show("s4======" + s4);

            string crc = Crc1(str1, str2_16, str3_16, s4.ToUpper());//CRC的十六进制表示
                                                                    //long data = Convert.ToInt64(s4.ToUpper());
                                                                    //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
            if (s4.Length == 4)
                s4 = "0100" + s4;
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }


        //设置b值
        public string DataBValue(string str1, string str2, string str3, string str4,string k)//str1:厂区码。str2:设备id。str3："14"。str4：设置的数据(b的值)。k：数据库中k的值
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");//两位表示16机制
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2"); //两位表示16机制
            //将k的值转为16进制
            long int4 = long.Parse(k);
            k = int4.ToString("x2");


            string erjinzhi = bianma(str4);//获取b的二进制数
            string shiliu = string.Format("{0:X}", System.Convert.ToInt32(erjinzhi, 2)).PadLeft(2, '0');//二进制转换为十六进制

            //MessageBox.Show("厂区码：" + str1 + "设备id：" + str2_16+ "指令码：" + str3_16 + "获取的k的16进制是" + k +"   获取的b的16进制是" + shiliu);

            s4 = k + shiliu;//s4为最后数据的那4个字节。前两个字节是0100.后两个字节中 前两个是k的值，后两个是b的值
            string crc = Crc1(str1, str2_16, str3_16, s4);//CRC的十六进制表示
                                                                    //long data = Convert.ToInt64(s4.ToUpper());
                                                                    //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);
                                                             

            if (s4.Length == 4)
            {
                s4 = "0100" + s4;
                //MessageBox.Show("crc:" + crc+"最后四个字节" + s4);
            }
            
            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
        }

        //设置k值
        public string DataKValue(string str1, string str2, string str3, string str4, string b)//str1:厂区码。str2:设备id。str3："14"。str4：设置的数据(k的值)。b：数据库中b的值
        {

            string str = "";
            string s4 = "";
            int int2 = int.Parse(str2);
            string str2_16 = int2.ToString("x2");//两位表示16机制
            int int3 = int.Parse(str3);
            string str3_16 = int3.ToString("x2"); //两位表示16机制
            //将k的值转为16进制
            int intB = int.Parse(b);
            string info = "";
            if (intB < 0)
            {
                //MessageBox.Show("是负数 intB" + intB);
                info = bianma(b);
                //MessageBox.Show("二进制 intB" + info);
            }
            else
            {
                //MessageBox.Show("是正数");
                info = bianma(b);
                //MessageBox.Show("二进制 intB" + info);
            }
            int intK = int.Parse(str4);
            string k  = intK.ToString("x2");


            //MessageBox.Show(k);
            //string erjinzhi = bianma(str4);//获取b的二进制数
            string shiliu = string.Format("{0:X}", System.Convert.ToInt32(info, 2)).PadLeft(2, '0');//b二进制转换为十六进制

            //MessageBox.Show("厂区码：" + str1 + "设备id：" + str2_16 + "指令码：" + str3_16 + "获取的k的16进制是" + k + "   获取的b的16进制是" + shiliu);

            s4 = k + shiliu;//s4为最后数据的那4个字节。前两个字节是0100.后两个字节中 前两个是k的值，后两个是b的值
            //MessageBox.Show(s4);
            string crc = Crc1(str1, str2_16, str3_16, s4);//CRC的十六进制表示
                                                          //long data = Convert.ToInt64(s4.ToUpper());
                                                          //int data = Int32.Parse(s4, System.Globalization.NumberStyles.AllowHexSpecifier);


            if (s4.Length == 4)
            {
                s4 = "0100" + s4;//设置时，一共四个字节，前两个字节固定是0100
                //MessageBox.Show("crc:" + crc+"最后四个字节" + s4);
            }

            str = oneTotwo(str1) + str2_16.ToUpper() + str3_16.ToUpper() + s4.ToUpper() + crc;
            str = ":0C" + str + "\r";
            return str;//要发送的数据报
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
            if(num < 0)//判断是否是负数
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
            
            string len = str[1] +""+ str[2];
            int l = Int32.Parse(len, System.Globalization.NumberStyles.HexNumber);
            string crc = str[str.Length - 4] + "" + str[str.Length - 3];
            if (l != (str.Length - 5) / 2)
            {
                return "0";
            }

            string fac = "";
            for (int i = 0; i < 5; i++)
            {
                string s = str[2*i + 3] +""+ str[2*i + 4];
                int a = Int32.Parse(s);
                fac += a.ToString();
            }

            string equip = str[13] +""+ str[14];
            //int equip_int = Int32.Parse(equip, System.Globalization.NumberStyles.HexNumber);
            //equip = equip_int.ToString();
            string oper = str[15] +""+ str[16];
            string data = "";
            string crc_data = "";//用于测试crc的data数据


            if (oper == "23")
            {//盘库结束后的转
             

                int id = Int32.Parse(equip, System.Globalization.NumberStyles.HexNumber);//料仓地址的十进制表示 

                for (int i = 0; i < 2; i++)
                {
                    //d表示整数位，f表示小数位
                    string d = str[6 * i + 17] + "" + str[6 * i + 18] + "" + str[6 * i + 19] + "" + str[6 * i + 20];
                    string f = str[6 * i + 21] + "" + str[6 * i + 22];
                    float d_int = Int32.Parse(d, System.Globalization.NumberStyles.HexNumber);
                    float f_int = Int32.Parse(f, System.Globalization.NumberStyles.HexNumber);
                    data += ((d_int + f_int / 100).ToString() + "+");
                }
                //////////////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //29 30
                string error1 = str[29] + "" + str[30];//点数字节6
                string error2 = str[31] + "" + str[32];//字节7

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
                    MessageBox.Show("查询数据库出错。" + se.ToString());
                }
                //MessageBox.Show("23+++++++++++++++++type"+type);

                //if (type.Equals("侧置平扫") || type.Equals("顶置平扫"))
                if (type.Equals("4")|| type.Equals("1"))
                {
                    //字节8。。两部分
                    string all1 = str[33] + "" + str[34];//0x10

                    //字节9
                    string all2 = str[35] + "" + str[36];//0x04

                    string zong = all1 + all2;


                    //int e3 = Int32.Parse("012c", System.Globalization.NumberStyles.HexNumber);

                    //MessageBox.Show("大小为：" + e3);


                    //字节10：盘库模式
                    string mode = str[37] + "" + str[38];


                    //字节11，字节12
                    //string Zjiao = str[39] + "" + str[40];//步进角整数部分
                    //string Xjiao = str[41] + "" + str[42];//步进角小数部分

                    //MessageBox.Show("整数部分" + Zjiao);
                    //MessageBox.Show("小数部分" + Xjiao);

                    //float zs= Int32.Parse(Zjiao, System.Globalization.NumberStyles.HexNumber);
                    //float xs= Int32.Parse(Xjiao, System.Globalization.NumberStyles.HexNumber);

                    //MessageBox.Show("整数部分ffffffffff-->" + zs);
                    //MessageBox.Show("小数部分ffffffffff-->" + xs);


                    int e1 = Int32.Parse(error1, System.Globalization.NumberStyles.HexNumber);
                    int e2 = Int32.Parse(error2, System.Globalization.NumberStyles.HexNumber);
                    int e3 = Int32.Parse(zong, System.Globalization.NumberStyles.HexNumber);

                    int model = Int32.Parse(mode, System.Globalization.NumberStyles.HexNumber);
                    data += e1.ToString() + "+" + e2.ToString() + "+" + e3.ToString() + "+" + model.ToString() + "+";//查看一下all1和all2是什么

                }
                else if(type.Equals("0")|| type.Equals("2"))
                {
                    //MessageBox.Show("type0+++++++++++++++++");
                    string error3 = str[33] + "" + str[34];//全部采样点个数 字节8

                    //字节9：盘库模式
                    string mode = str[35] + "" + str[36];
                    //MessageBox.Show("************" + str[35] + "********8" + str[36]);

                    //字节10，字节11
                    //string Zjiao = str[37] + "" + str[38];//步进角整数部分
                    //string Xjiao = str[39] + "" + str[40];//步进角小数部分

                    //float zs = Int32.Parse(Zjiao, System.Globalization.NumberStyles.HexNumber);
                    //float xs = Int32.Parse(Xjiao, System.Globalization.NumberStyles.HexNumber);

                    int e1 = Int32.Parse(error1, System.Globalization.NumberStyles.HexNumber);
                    int e2 = Int32.Parse(error2, System.Globalization.NumberStyles.HexNumber);
                    int e3 = Int32.Parse(error3, System.Globalization.NumberStyles.HexNumber);

                    int model = Int32.Parse(mode, System.Globalization.NumberStyles.HexNumber);
                    data += e1.ToString() + "+" + e2.ToString() + "+" + e3.ToString() + "+" + model.ToString() + "+";

                }



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
                    data = ((temp1_int+temp2_int/100).ToString());
                }
                else
                {
                    data += ((temp1_int+temp2_int/100).ToString());
                }
                data += "+";
                string hum1 = str[21] + "" + str[22];//湿度的整数部分
                string hum2 = str[23] + "" + str[24];//湿度的小数部分
                crc_data += (hum1 + hum2);
                float hum1_int = Int32.Parse(hum1, System.Globalization.NumberStyles.HexNumber);//湿度整数部分转化为十进制
                float hum2_int = Int32.Parse(hum2, System.Globalization.NumberStyles.HexNumber);//湿度小数部分转化为十进制
                data+=((hum1_int+hum2_int/100).ToString());
            }
            else if (oper == "25")
            {
                string angle = str[17] + "" + str[18];//角度值
                string distance = str[19] + "" + str[20] + "" + str[21] + "" + str[22];//距离，单位厘米
                string schedule = str[23] + "" + str[24];//进度，完成是0x64

                int angle_int = Int32.Parse(angle, System.Globalization.NumberStyles.HexNumber);
                int distance_int = Int32.Parse(distance, System.Globalization.NumberStyles.HexNumber);
                int schedule_int = Int32.Parse(schedule, System.Globalization.NumberStyles.HexNumber);

                data = angle_int.ToString()+"+"+distance_int.ToString()+"+"+schedule_int.ToString();
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
            else if(oper == "2B")//回应版本号
            {
                string str1 = str[17] + "" + str[18];//第一个字符
                string str2 = str[19] + "" + str[20];//第二个字符
                string str3 = str[21] + "" + str[22];//第三个字符
                string str4 = str[23] + "" + str[24];//第四个字符
                data = str1 + "+" + str2 + "+" + str3 + "+" + str4;
            }
            else if (oper == "2D" )//加热设置.只用两个字节
            {
                string str1 = str[17] + "" + str[18];//第一个字符
                string str2 = str[19] + "" + str[20];//第二个字符
                //string str3 = str[21] + "" + str[22];//第三个字符
                //string str4 = str[23] + "" + str[24];//第四个字符
                if (str1.Equals("02"))//回应温度是3个字节
                {
                    string temp1 = str2;                  //温度的整数部分
                    string temp2 = str[21] + "" + str[22];//温度的小数部分
                    int temp1_int = Int32.Parse(temp1, System.Globalization.NumberStyles.HexNumber);//温度整数部分转化为十进制
                    float temp2_int = Int32.Parse(temp2, System.Globalization.NumberStyles.HexNumber);//温度小数部分转化为十进制
                    temp1 = Convert.ToString(temp1_int, 2);//temp的二进制表示
                    temp1 = temp1.PadLeft(8, '0');//温度二进制的格式化表示

                    if (temp1[0] == '1')//判断是否是整数或者负数
                    {//负数
                        temp1_int = temp1_int - 128;//将第一个位置的标志位去除。
                        temp1_int = -temp1_int;//整数部分变负数
                        temp2_int = 0 - temp2_int;//小数部分变负数
                        str2 = ((temp1_int + temp2_int / 100).ToString());
                    }
                    else
                    {//整数
                        str2 = ((temp1_int + temp2_int / 100).ToString());
                    }
                }

                data = str1 + "+" + str2 ;
            }
            else if (oper == "2F")//垂直角度设置.只用两个字节
            {
                string str1 = str[17] + "" + str[18];//第一个字符
                string str2 = str[19] + "" + str[20];//第二个字符
                string str3 = str[21] + "" + str[22];//第三个字符
                //string str4 = str[23] + "" + str[24];//第四个字符

                string temp1 = str2;           //整数部分
                string temp2 = str3;           //小数部分
                int temp1_int = Int32.Parse(temp1, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制
                float temp2_int = Int32.Parse(temp2, System.Globalization.NumberStyles.HexNumber);//小数部分转化为十进制

                str2 = ((temp1_int + temp2_int / 10).ToString());

                data = str1 + "+" + str2;
            }
            else if (oper == "29")//回应步进角
            {
                string a1 = str[17] + "" + str[18];//字节1
                if (a1.Equals("00"))
                {
                    string a3 = str[21] + "" + str[22];//字节3,整数部分
                    string a4 = str[23] + "" + str[24];//字节4,小数部分
                                                       //String Margin = str[17] + "" + str[18] + "" + str[19] + "" + str[20];//边距
                                                       //String Top = str[21] + "" + str[22] + "" + str[23] + "" + str[24];//顶高度
                                                       //String Wheelbase = str[25] + "" + str[26] + "" + str[27] + "" + str[28];//轴距
                    int zhenshu = Int32.Parse(a3, System.Globalization.NumberStyles.HexNumber);
                    int xiaoshu = Int32.Parse(a4, System.Globalization.NumberStyles.HexNumber);
                    //int Wheelbase_int = Int32.Parse(Wheelbase, System.Globalization.NumberStyles.HexNumber);
                    //data = Margin_int.ToString() + "+" + Top_int.ToString() + "+" + Wheelbase_int.ToString();//最后要生成一个data用于返回
                    data = zhenshu.ToString() + "." + xiaoshu.ToString();
                }
                if(a1.Equals("01"))
                {
                    data = "设置成功";
                }
                
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
                String ModelAddress = str[21] + "" + str[22]+""+str[23]+""+str[24];
                int Channel_int = Int32.Parse(Channel, System.Globalization.NumberStyles.HexNumber);
                int Address_int = Int32.Parse(ModelAddress, System.Globalization.NumberStyles.HexNumber);

                data = Speed_int.ToString() + "+" + Channel_int.ToString() + "+" + Address_int.ToString();

            }
            else if (oper == "0B")//回应边距和顶高度。轴距
            {
                String Margin = str[17] + "" + str[18] + "" + str[19] + "" + str[20];//边距
                String Top = str[21] + "" + str[22] + "" + str[23] + "" + str[24];//顶高度
                String Wheelbase = str[25] + "" + str[26] + "" + str[27] + "" + str[28];//轴距
                int Margin_int = Int32.Parse(Margin, System.Globalization.NumberStyles.HexNumber);
                int Top_int = Int32.Parse(Top, System.Globalization.NumberStyles.HexNumber);
                int Wheelbase_int = Int32.Parse(Wheelbase, System.Globalization.NumberStyles.HexNumber);
                data = Margin_int.ToString() + "+" + Top_int.ToString() + "+" + Wheelbase_int.ToString();
            }

            else if (oper == "33")//回应设备类型
            {
                string str1 = str[17] + "" + str[18];//第一个字符
                string temp1 = str1;           //整数部分
                int temp1_int = Int32.Parse(temp1, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制


                data = ""+temp1_int;
            }
            else if (oper == "35")//回应回复水平角度读取/设置
            {
                string str1 = str[17] + "" + str[18];//第一个字符:0x00 读取。0x01 回应
                string str2 = str[19] + "" + str[20];//第二个字符 角度值
                int temp1_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制
                int temp2_int = Int32.Parse(str2, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制

                data = ""+ temp1_int+"+" + temp2_int;
            }
            else if (oper == "37")//回应调整水平角度
            {
                string str1 = str[17] + "" + str[18];//第一个字符:
                string str2 = str[19] + "" + str[20];//第二个字符 
                int temp1_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制
                int temp2_int = Int32.Parse(str2, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制

                data = "" + temp1_int + "+" + temp2_int;
            }
            else if (oper == "39")//回应调整水平角度
            {
                string str1 = str[17] + "" + str[18];//第一个字符:
                string str2 = str[19] + "" + str[20];//第二个字符 
                string str3 = str[21] + "" + str[22];//第三个字符 
                string str4 = str[23] + "" + str[24];
                int temp1_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制
                int temp2_int = Int32.Parse(str2, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制
                float ca3 = Int32.Parse(str3, System.Globalization.NumberStyles.HexNumber);//小数

                int temp4_int = Int32.Parse(str4, System.Globalization.NumberStyles.HexNumber);//整数部分转化为十进制




                float cangle = (temp2_int + ca3 / 10);





                data = "" + temp1_int + "+" + cangle + "+" + temp4_int;
            }
            else if(oper == "41")//回应方仓边长
            {
                string str1 = str[17] + "" + str[18];//第0个字符:0表示读取，1表示设置
                string str2 = str[19] + "" + str[20];//第1个字符 
                string str3 = str[21] + "" + str[22];//第2个字符 高字节部分
                string str4 = str[23] + "" + str[24];//第3个字符 低字节部分
                string str5 = str[21] + "" + str[22] + str[23] + str[24];
     
                int temp1_int = Int32.Parse(str5, System.Globalization.NumberStyles.HexNumber);
                int temp2_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);
             

                 data = "" + temp2_int+"+"+temp1_int;

                

            }
            else if (oper == "43")//回应方仓边宽
            {
                string str1 = str[17] + "" + str[18];//第一个字符:0表示读取，1表示设置
                string str2 = str[19] + "" + str[20];//第二个字符 ：高字节部分
                string str3 = str[21] + "" + str[22];//第三个字符 低字节部分
                string str5 = str[21] + "" + str[22] + str[23] + str[24];

                int temp1_int = Int32.Parse(str5, System.Globalization.NumberStyles.HexNumber);
                int temp2_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);


                data = "" + temp2_int + "+" + temp1_int;

            }
            else if (oper == "45")//回应方仓左边距参数
            {
                string str1 = str[17] + "" + str[18];//第一个字符:0表示读取，1表示设置
                string str2 = str[19] + "" + str[20];//第二个字符 ：高字节部分
                string str3 = str[21] + "" + str[22];//第三个字符 低字节部分
                string str5 = str[21] + "" + str[22] + str[23] + str[24];

                int temp1_int = Int32.Parse(str5, System.Globalization.NumberStyles.HexNumber);
                int temp2_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);


                data = "" + temp2_int + "+" + temp1_int;

            }else if(oper == "47")//回应上锥高度
            {
                string str1 = str[17] + "" + str[18];//第一个字符:0表示读取，1表示设置
                string str2 = str[19] + "" + str[20];//第二个字符 ：高字节部分
                string str3 = str[21] + "" + str[22];//第三个字符 低字节部分
                string str5 = str[21] + "" + str[22] + str[23] + str[24];

                int temp1_int = Int32.Parse(str5, System.Globalization.NumberStyles.HexNumber);
                int temp2_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);


                data = "" + temp2_int + "+" + temp1_int;

            }
            else if(oper == "49")
            {
              
                string str1 = str[17] + "" + str[18];//第几次定位 100表示完成
                string str2 = str[19] + "" + str[20];//正负标志
                string str3 = str[21] + "" + str[22];//整数
                string str4 = str[23] + "" + str[24];//小数

                int temp1_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);//第几次定位
                int ca2 = Int32.Parse(str3, System.Globalization.NumberStyles.HexNumber);//整数
                float ca3 = Int32.Parse(str4, System.Globalization.NumberStyles.HexNumber);//小数

                float cangle = (ca2 + ca3 / 10);//角度差数值

                //MessageBox.Show("定位几次" + temp1_int + "正负" + str2 + "数值" + cangle);
                data = "" + temp1_int + "+"+ str2 + "+" + cangle;


            }
            else if(oper== "51")
            {
                string str1 = str[17] + "" + str[18];//字节0
                int temp1_int = Int32.Parse(str1, System.Globalization.NumberStyles.HexNumber);

                data = "" + temp1_int;


            }
            else if (oper == "0F")
            {
           
                //int Corr1_int = Int32.Parse(Corr_vertical1, System.Globalization.NumberStyles.HexNumber);
                //int Corr2_int = Int32.Parse(Corr_vertical2, System.Globalization.NumberStyles.HexNumber);
                String Correction_per = str[23] + "" + str[24];//比例校正百分比
                String Correction = str[25]+""+str[26];//加减校正值


                String sign_Correction = str[17] + "" + str[18];//垂直校准正负
                String Corr_vertical1 = str[19] + "" + str[20];//校准角度的整数部分
                String Corr_vertical2 = str[21] + "" + str[22];//校准角度的小数部分
                
                int ca1 = Int32.Parse(sign_Correction, System.Globalization.NumberStyles.HexNumber);//垂直校准正负
                int ca2 = Int32.Parse(Corr_vertical1, System.Globalization.NumberStyles.HexNumber);//整数
                float ca3 = Int32.Parse(Corr_vertical2, System.Globalization.NumberStyles.HexNumber);//小数

                //MessageBox.Show("正负值" + ca1);
                //MessageBox.Show("整数" + ca2);
                //MessageBox.Show("小数" + ca3);
                float cangle = (ca2 + ca3 / 10);
                if (ca1 == 1)
                {
                    cangle = 0 - cangle;
                }

                int Corr1_int = Int32.Parse(Correction_per, System.Globalization.NumberStyles.HexNumber);
                int Corr2_int = Int32.Parse(Correction, System.Globalization.NumberStyles.HexNumber);//获取到16进制数的两位
                                                                                                     //1、16进制变为2进制
                                                                                                     //2、2进制解析第一位，分为正数，负数
                                                                                                     //3、2进制变为10进制。。保存数据库
                string er = System.Convert.ToString(Corr2_int, 2);//获取到的b的二进制
                if (er.Length == 8)//是八位说明是负数
                {
                    er = "-" + Towto10(er.Substring(1));
                }
                else
                {
                    er = Towto10(er);
                }
                data = Corr1_int + "+" + er +"+"+ cangle;
            }
            else
            {
                for (int i = 0; i < (l - 8) / 2; i++)
                {
                    string d = str[4 * i + 17] + "" + str[4 * i + 18] + "" + str[4 * i + 19] + "" + str[4 * i + 20];
                    
                    crc_data += d;
                    int a = Int32.Parse(d, System.Globalization.NumberStyles.HexNumber);
                    data += (a.ToString().PadLeft(4, '0'))+"";
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


        public string Towto10(string info)
        {
            int data = int.Parse(info);
            int res = Convert.ToInt32(info, 2);//二进制转10进制
            return "" + res;
        }


    }
}
