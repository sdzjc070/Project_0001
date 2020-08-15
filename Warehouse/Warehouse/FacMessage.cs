using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Warehouse
{
    public class FacMessage
    {
        public int ins_num;//指令序号
        public string ins_answer;//应答指令码
        public string fac_num;////料仓编号
        public bool sign_answer;//应答标志, true表示应答， false表示未应答
        public int life_time;//超时时间
        public string message;//数据信息
        public string instruction;//指令码
        public int resend;//重发次数
        public int ProduceTime;//产生时间，记录产生了多少秒

        /// <summary>
        /// 不含重发次数
        /// </summary>
        /// <param name="ins_num"></param>
        /// <param name="ins_answer"></param>
        /// <param name="fac_num"></param>
        /// <param name="sign_answer"></param>
        /// <param name="life_time"></param>
        /// <param name="message"></param>
        /// <param name="instruction"></param>
        public FacMessage(int ins_num, string ins_answer, string fac_num, 
            bool sign_answer, int life_time, string message, string instruction, int producetime)
        {
            this.ins_num = ins_num;
            this.ins_answer = ins_answer;
            this.fac_num = fac_num;
            this.sign_answer = sign_answer;
            this.life_time = life_time;
            this.message = message;
            this.instruction = instruction;
            this.ProduceTime = producetime;
        }

        /// <summary>
        /// 含有重发次数
        /// </summary>
        /// <param name="ins_num"></param> 
        /// <param name="ins_answer"></param>
        /// <param name="fac_num"></param>
        /// <param name="sign_answer"></param>
        /// <param name="life_time"></param>
        /// <param name="message"></param>
        /// <param name="instruction"></param>
        /// <param name="resend"></param>
        public FacMessage(int ins_num, string ins_answer, string fac_num, 
            bool sign_answer, int life_time, string message, string instruction, int resend, int producetime)
        {
            this.ins_num = ins_num;
            this.ins_answer = ins_answer;
            this.fac_num = fac_num;
            this.sign_answer = sign_answer;
            this.life_time = life_time;
            this.message = message;
            this.instruction = instruction;
            this.resend = resend;
            this.ProduceTime = producetime;
        }

        public FacMessage(string fac_num, int life_time, int ins_num)
        {//应用于盘库队列，标明料仓编号，操作进度, 操作类型，1表示盘库，2表示清洁镜头, 0表示处理无回应的情况
            this.fac_num = fac_num;
            this.life_time = life_time;
            this.ins_num = ins_num;
        }

        public string Out()
        {
            return ins_num + " " + ins_answer + " " + fac_num + " " + sign_answer + " " + life_time +instruction+ "\r\n";
        }
    }
}
