﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using DLLFullPrint;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Office.Interop.Excel;
using MySql.Data.MySqlClient;


namespace DLLFullPrint
{
    public partial class Frm_PrintSet : Form
    {
        public Frm_PrintSet()
        {
            InitializeComponent();
        }

        public static DataSet MyDS_Grid;
        public int header = 30; //标题栏的默认高度
        public int row = 23;    //单元格的默认高度
        public Color linecolor = Color.Black;   //边线的颜色
        public int linewidth = 1;   //边线的宽度
        public bool PLintUP = true;
        public bool PLintLeft = true;
        public bool PLintUnit = true;
        public bool Aspect = true;//打印方向
        public bool boundary = false;//是否打印分割线


        #region  设置打印数据的相关信息
        /// <summary>
        /// 设置打印数据的相关信息
        /// </summary>
        /// <param dgp="PrintClass">公共类PrintClass</param>
        public void MSetUp(PrintClass dgp)
        {
            string n="false";
            string[] margin = new string[4];
            if (checkBox_margin.Checked==true)
                n="true";
            else
                n="alse";
            margin[0]=n;
            margin[1] = textBox_topmargin.Text;
            margin[2] = textBox_leftmargin.Text;
            margin[3] = textBox_buttommargin.Text;
            dgp.PrintSetUp(margin);
            dgp.headerheight = this.header;//列标题的默认高度
            if (checkBox_Header.Checked == true)//列标题
            {
                dgp.brushHeaderFont = new SolidBrush(dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor);//前景色
                dgp.headerfont = dataGridView1.ColumnHeadersDefaultCellStyle.Font;//字体样式
                dgp.brushHeaderBack = new SolidBrush(dataGridView1.ColumnHeadersDefaultCellStyle.BackColor);//背景色
                dgp.headerheight = dataGridView1.ColumnHeadersHeight;//列标题的高度
            }
            if (checkBox_Cell.Checked == true)//单元格
            {
                dgp.Cellfont = dataGridView1.RowsDefaultCellStyle.Font;//字体样式
                dgp.brushCellBack = new SolidBrush(dataGridView1.RowsDefaultCellStyle.BackColor);//背景色
                dgp.brushCellFont = new SolidBrush(dataGridView1.RowsDefaultCellStyle.ForeColor);//前景色
                dgp.rowgap = row;
            }
            if (checkBox_Table.Checked == true)//表格
            {
                dgp.AlignmentSgin = this.comboBox_Alignment.SelectedIndex;//对齐方式
            }
            dgp.iseverypageprinttitle = checkBox_Title.Checked;//是否每一页都打印标题
            dgp.needprintpageindex = checkBox_Page.Checked;//是否每一页都打印页脚
            dgp.PageAspect = Aspect;//设置横向打印
            //设置表格的边线
            dgp.LineUP = PLintUP;//是否打印上边线
            dgp.LineLeft = PLintLeft;//是否打印左边线
            dgp.LineUnit = PLintUnit;//是否打印单元格边线
            dgp.LineColor = linecolor;//设置线的颜色
            dgp.Boundary = checkBox_Boundary.Checked;//是否打印分割线
            dgp.HAlignment = comboBox_HAlignment.SelectedIndex;//列标题的对齐方式
            dgp.UAlignment = comboBox_UAlignment.SelectedIndex;//单元格的对齐方式
        }
        #endregion

        public void Barbarism_DataGrid(int n)
        {
            if (n == 0)
            {
                System.Drawing.Font Hfont = new System.Drawing.Font("宋体", 10);
                dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.White;//列标题的背景颜色
                dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;//列标题的字体颜色
                dataGridView1.ColumnHeadersDefaultCellStyle.Font = Hfont;//列标题的字体样式
                dataGridView1.ColumnHeadersHeight = 30;//列标题的高度
                textBox_Size.Text = dataGridView1.ColumnHeadersHeight.ToString();//列标题的高度
            }
            if (n == 1)
            {
                System.Drawing.Font Hfont = new System.Drawing.Font("宋体", 9);
                dataGridView1.RowsDefaultCellStyle.BackColor = Color.White;//单元格的背景颜色
                dataGridView1.RowsDefaultCellStyle.ForeColor = Color.Black;//单元格的字体颜色
                dataGridView1.RowsDefaultCellStyle.Font = Hfont;//单元格的字体样式
                //textBox_CellSize.Text = dataGridView1.Rows[0].Height.ToString();//单元格的高度
            }
        }

        public void InputCount(KeyPressEventArgs e, object sender, int n)
        {
            int tsele = 0;
            if (!(e.KeyChar <= '9' && e.KeyChar >= '0') && e.KeyChar != '\r' && e.KeyChar != '\b')
            {
                e.Handled = true;   //处理KeyPress事件
            }
            else
            {
                if (((System.Windows.Forms.TextBox)sender).SelectedText != "")
                {
                    tsele = ((System.Windows.Forms.TextBox)sender).SelectionLength;
                }
                if (n >0) //只能输入2位数
                {
                    if (e.KeyChar <= '9' && e.KeyChar >= '0')
                        if (((((System.Windows.Forms.TextBox)sender).Text).Length-tsele + 1) > n)
                            e.Handled = true;   //处理KeyPress事件
                }
            }

        }

        public void SetCheckBox(System.Windows.Forms.CheckBox CheckB, System.Windows.Forms.GroupBox GroupB)
        {
            if (CheckB.Checked == true)//如果CheckBox控件为可用状态
                GroupB.Enabled = false;//不可用
            else
                GroupB.Enabled = true;//可用
        }

        public void TextBoxValue(object sender,int d,int n,int m)
        {
            if (((System.Windows.Forms.TextBox)sender).Text == "")
            {
                ((System.Windows.Forms.TextBox)sender).Text = d.ToString();
            }
            if (int.Parse(((System.Windows.Forms.TextBox)sender).Text) < n || int.Parse(((System.Windows.Forms.TextBox)sender).Text) > m)
            {
                if (int.Parse(((System.Windows.Forms.TextBox)sender).Text) < n)
                    ((System.Windows.Forms.TextBox)sender).Text = n.ToString();
                if (int.Parse(((System.Windows.Forms.TextBox)sender).Text) > m)
                    ((System.Windows.Forms.TextBox)sender).Text = m.ToString();
            }
        }

        private void button_Preview_Click(object sender, EventArgs e)
        {
            //对打印信息进行设置
            PrintClass dgp = new PrintClass(this.dataGridView1, this.textBox_Title.Text, 16, comboBox_PageSize.SelectedIndex, checkBox_Aspect.Checked);
            MSetUp(dgp);//记录窗体中打印信息的相关设置
            string[] header = new string[dataGridView1.ColumnCount];//创建一个与数据列相等的字符串数组
            for (int p = 0; p < dataGridView1.ColumnCount; p++)//记录所有列标题的名列
            {
                header[p] = dataGridView1.Columns[p].HeaderCell.Value.ToString();
            }
            dgp.print();//显示打印预览窗体
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            MyDS_Grid = MyDLL.SendOut();
            dataGridView1.DataSource = MyDS_Grid.Tables[0];
            dataGridView1.RowHeadersVisible = false;
            comboBox_PageSize.SelectedIndex = 0;
            comboBox_HAlignment.SelectedIndex = 0;
            comboBox_UAlignment.SelectedIndex = 0;
            Barbarism_DataGrid(0);
            Barbarism_DataGrid(1);
        }

        private void checkBox_margin_MouseDown(object sender, MouseEventArgs e)
        {
            SetCheckBox(((System.Windows.Forms.CheckBox)sender), groupBox3);
        }

        private void button_BackColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = colorDialog1.Color;//列标题的背景颜色
        }

        private void button_Fint_Click(object sender, EventArgs e)
        {
            if (fontDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.ColumnHeadersDefaultCellStyle.Font = fontDialog1.Font;//列名标题
            fontDialog1.Dispose();
        }

        private void checkBox_Title_MouseDown(object sender, MouseEventArgs e)
        {
            SetCheckBox(((System.Windows.Forms.CheckBox)sender), groupBox4);
        }

        private void button_FontColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = colorDialog1.Color;//列标题的字体颜色
            colorDialog1.Dispose();
        }

        private void button_Default_Click(object sender, EventArgs e)
        {
            Barbarism_DataGrid(0);
        }

        private void button_CellFont_Click(object sender, EventArgs e)
        {
            if (fontDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.RowsDefaultCellStyle.Font = fontDialog1.Font;//单元格字体
            fontDialog1.Dispose();
        }

        private void button1_CellBackColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.RowsDefaultCellStyle.BackColor = colorDialog1.Color;//单元格的字体背景颜色
            colorDialog1.Dispose();
        }

        private void button_CellFontColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
                dataGridView1.RowsDefaultCellStyle.ForeColor = colorDialog1.Color;//单元格的字体颜色
            colorDialog1.Dispose();
        }

        private void checkBox_Cell_MouseDown(object sender, MouseEventArgs e)
        {
            SetCheckBox(((System.Windows.Forms.CheckBox)sender), groupBox5);
        }

        private void button_CellDefault_Click(object sender, EventArgs e)
        {
            Barbarism_DataGrid(1);
        }

        private void textBox_Size_Leave(object sender, EventArgs e)
        {
            TextBoxValue(sender, 30, 20, 99);
            dataGridView1.ColumnHeadersHeight = int.Parse(textBox_Size.Text);
            header = int.Parse(textBox_Size.Text);

        }

        private void textBox_Size_KeyPress(object sender, KeyPressEventArgs e)
        {
            InputCount(e, sender, 2);
            if (e.KeyChar == '\r')
                textBox_Size_Leave(sender, e);
        }

        public void DateGrid_CellHeight(DataGridView DG, int n)
        {
            for (int i = 0; i < DG.Rows.Count; i++)
            {
                DG.Rows[i].Height = n;
            }
        }
        private void textBox_CellSize_Leave(object sender, EventArgs e)
        {
            TextBoxValue(sender, 23, 20, 99);
            DateGrid_CellHeight(dataGridView1, int.Parse(textBox_CellSize.Text));
            row = int.Parse(textBox_CellSize.Text);

        }

        private void textBox_CellSize_KeyPress(object sender, KeyPressEventArgs e)
        {
            InputCount(e, sender, 2);
            if (e.KeyChar == '\r')
                textBox_CellSize_Leave(sender, e);
        }

        private void checkBox_Table_MouseDown(object sender, MouseEventArgs e)
        {
            SetCheckBox(((System.Windows.Forms.CheckBox)sender), groupBox6);
        }

        private void textBox_topmargin_KeyPress(object sender, KeyPressEventArgs e)
        {
            InputCount(e, sender, 3);
            if (e.KeyChar == '\r')
                textBox_topmargin_Leave(sender, e);
        }

        private void textBox_topmargin_Leave(object sender, EventArgs e)
        {
            TextBoxValue(sender, 60, 10, 500);
        }

        private void textBox_leftmargin_Leave(object sender, EventArgs e)
        {
            TextBoxValue(sender, 50, 10, 700);
        }

        private void textBox_leftmargin_KeyPress(object sender, KeyPressEventArgs e)
        {
            InputCount(e, sender, 3);
            if (e.KeyChar == '\r')
                textBox_leftmargin_Leave(sender, e);
        }

        private void textBox_buttommargin_KeyPress(object sender, KeyPressEventArgs e)
        {
            InputCount(e, sender, 3);
            if (e.KeyChar == '\r')
                textBox_buttommargin_Leave(sender, e);
        }

        private void textBox_buttommargin_Leave(object sender, EventArgs e)
        {
            TextBoxValue(sender, 50, 10, 500);
        }

        private void Frm_PrintSet_Activated(object sender, EventArgs e)
        {//在窗体中绘制一个预览表格
            Color Lcolor;
            Graphics g = panel_Line.CreateGraphics();
            int paneW = panel_Line.Width;//设置表格的宽度
            int paneH = panel_Line.Height;//设置表格的高度
            g.DrawRectangle(new Pen(Color.WhiteSmoke, paneW), 0, 0, paneW, paneH);//绘制一个矩形
            if (PLintUnit == true)//如果绘制单元格线
                Lcolor = linecolor;//设置单元格的颜色
            else
                Lcolor = Color.WhiteSmoke;//设置单元格线的颜为背景颜色           
            int unitW = (int)(((paneW - 20 * 2) / 3));//设置单元格的宽度
            int unitH = (int)((paneH - 20 * 2) / 3);//设置单元格的高度
            int bW = 0, bH = 0;
            //绘制一个3行3列的表格
            for (int i = 0; i < 2; i++)
            {
                g.DrawLine(new Pen(Lcolor, 1), 20 + unitW, 20, 20 + unitW, paneH - 20);//绘制纵线
                if (boundary==true && i==0)//如果是分割线并且是第一条线
                {
                    //设置分割线的坐标点
                    bW = paneW - 20;
                    bH = 20 + unitH;
                }
                else
                    g.DrawLine(new Pen(Lcolor, 1), 20, 20 + unitH, paneW - 20, 20 + unitH);//绘制横线
                unitH += unitH;//下一条横线的位置
                unitW += unitW;//下一条纵线的位置
            }
            if (boundary == true)//绘制分割线
                g.DrawLine(new Pen(linecolor, 1), 20, bH, bW, bH);
            if (PLintUP == true)//绘制最上面的线
                Lcolor = linecolor;//线的颜色
            else
                Lcolor = Color.WhiteSmoke;//与背景色相同的颜色
            g.DrawLine(new Pen(Lcolor, linewidth), 20, 20, paneW - 20, 20);//绘制上线
            g.DrawLine(new Pen(Lcolor, linewidth), 20, paneH - 20, paneW - 20, paneH - 20);//绘制下线
            if (PLintLeft == true)//绘制最左面的线
                Lcolor = linecolor;//线的颜色
            else
                Lcolor = Color.WhiteSmoke;//与背景色相同的颜色
            g.DrawLine(new Pen(Lcolor, linewidth), 20, 20, 20, paneH - 20);//绘制左线
            g.DrawLine(new Pen(Lcolor, linewidth), paneW - 20, 20, paneW - 20, paneH - 20);//绘制右线

        }

        private void Frm_PrintSet_Shown(object sender, EventArgs e)
        {
            Frm_PrintSet_Activated(sender, e);
        }

        private void checkBox_Aspect_MouseDown(object sender, MouseEventArgs e)
        {//改变窗体中预览表格的方向
            int aspX = 0;//宽度
            int aspY = 0;//高度

            if (((System.Windows.Forms.CheckBox)sender).Checked == false)//如果不是纵向打印
            {
                aspX = 136;//设置大小
                aspY = 98;
                PrintClass.PageScape = true;//横向打印
            }
            else
            {
                aspX = 100;//设置大小
                aspY = 116;
                PrintClass.PageScape = false;//纵向打印
            }
            panel_Line.Width = aspX;//设置控件的宽度
            panel_Line.Height = aspY;//设置控件的高度
            aspX = (int)((groupBox7.Width - aspX) / 2);//设置控件的Top
            panel_Line.Location = new System.Drawing.Point(aspX, 26);//设置控件的位置
            Frm_PrintSet_Activated(sender, e);//设用Activated事件
        }

        private void checkBox_line_MouseDown(object sender, MouseEventArgs e)
        {
            SetCheckBox(((System.Windows.Forms.CheckBox)sender), groupBox7);
            Frm_PrintSet_Activated(sender, e);
        }

        private void button_upline_Click(object sender, EventArgs e)
        {
            PLintUP = !PLintUP;
            Frm_PrintSet_Activated(sender, e);
        }

        private void button_leftline_Click(object sender, EventArgs e)
        {
            PLintLeft = !PLintLeft;
            Frm_PrintSet_Activated(sender, e);
        }

        private void button_cellline_Click(object sender, EventArgs e)
        {
            PLintUnit = !PLintUnit;
            Frm_PrintSet_Activated(sender, e);
        }

        private void button_LineColor_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
                linecolor = colorDialog1.Color;//边线颜色
            colorDialog1.Dispose();
            Frm_PrintSet_Activated(sender, e);
        }

        private void checkBox_Boundary_MouseDown(object sender, MouseEventArgs e)
        {
            boundary = !(((System.Windows.Forms.CheckBox)sender).Checked);
            Frm_PrintSet_Activated(sender, e);
        }
        //private string MySqlCon = "Database=factory;Data Source=127.0.0.1;User Id=root;" + "Password=123456;pooling=false;CharSet=utf8;port=3306";

        private void button1_Click(object sender, EventArgs e)
        {
            bool i = ExportToExcel(dataGridView1);
            //MessageBox.Show(i + "");

            //string a = "D:" + "\\KKHMD.xls";
            //ExportExcels(a, dataGridView1);

            //SaveFileDialog sfd = new SaveFileDialog();
            //sfd.Filter = "Excel文件|*.xlsx|Word文件|*.docx";
            //sfd.FilterIndex = 0;
            //if (sfd.ShowDialog() == DialogResult.OK)
            //{
                //连接数据库，查询数据
                //using (MySqlConnection conn = new MySqlConnection(MySqlCon))
                //{
                //    MySqlCommand mySqlCommand = new MySqlCommand("select * from bindata", conn);
                //    using (MySqlDataAdapter rd = new MySqlDataAdapter(mySqlCommand))
                //    {
                //        using (System.Data.DataTable dt = new System.Data.DataTable())
                //        {
                //            将数据库中查到的数据填充到DataTable数据表
                //            rd.Fill(dt);
                //            调用导出Excel的方法，传入DataTable数据表和路径
                //            ExportExcel(dt, sfd.FileName);
                //        }
                //    }
                //}

            //    MySqlCommand mySqlCommand;
            //    MySqlConnection mySqlConnection;
            //    MySqlDataAdapter mySqlDataAdapter;
            //    try
            //    {
            //        String sqlstr = "select * from bindata";
            //        mySqlConnection = new MySqlConnection(MySqlCon); //创建连接
            //        mySqlConnection.Open(); //打开连接
            //        mySqlCommand = new MySqlCommand(sqlstr, mySqlConnection); //创建命令对象
            //        System.Data.DataTable dataTable = new System.Data.DataTable();
            //        MessageBox.Show(dataTable + "");
            //        mySqlDataAdapter = new MySqlDataAdapter(mySqlCommand); //创建mysql数据库转换器
            //        mySqlDataAdapter.Fill(dataTable);  //通过数据库转换器填充datatable
            //        ExportExcel(dataTable, sfd.FileName);
            //        mySqlConnection.Close();//关闭连接
            //    }catch(MySqlException ex)
            //    {
            //        MessageBox.Show("数据库读取出错： " + ex);
            //    }




            //}

        }

        public static bool ExportToExcel(DataGridView dgvData)
        {

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Execl files (*.xls)|*.xls";
            saveFileDialog.FilterIndex = 0;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.CreatePrompt = true;
            saveFileDialog.Title = "Export Excel File To";
            DialogResult dr = saveFileDialog.ShowDialog();
            if (dr != DialogResult.OK)
            {
                return false;
            }

            Stream myStream;
            myStream = saveFileDialog.OpenFile();
            //StreamWriter sw = new StreamWriter(myStream, System.Text.Encoding.GetEncoding("gb2312"));
            StreamWriter sw = new StreamWriter(myStream, System.Text.Encoding.GetEncoding(-0));
            string str = "";

            try
            {

                //写标题
                for (int i = 0; i < dgvData.ColumnCount; i++)
                {
                    if (i > 0)
                    {
                        str += "\t";
                    }
                    str += dgvData.Columns[i].HeaderText;
                }
                sw.WriteLine(str);


                //写内容
                for (int j = 0; j < dgvData.Rows.Count; j++)
                {
                    string tempStr = "";
                    for (int k = 0; k < dgvData.Columns.Count; k++)
                    {
                        if (k > 0)
                        {
                            tempStr += "\t";
                        }
                        string cellValue = dgvData.Rows[j].Cells[k].Value.ToString();
                        cellValue = cellValue.Replace(" ", "");
                        cellValue = cellValue.Replace("\r", "");
                        cellValue = cellValue.Replace("\n", "");
                        cellValue = cellValue.Replace("\r\n", "");
                        tempStr += cellValue;
                        // tempStr += dgvData.Rows[j].Cells[k].Value.ToString();
                    }

                    sw.WriteLine(tempStr);
                }
                sw.Close();
                myStream.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                return false;
            }
            finally
            {
                sw.Close();
                myStream.Close();
            }

            return true;
        }

        void ExportExcel(System.Data.DataTable dt, string filepath)
        {
            //创建Excel应用程序类的一个实例，相当于从电脑开始菜单打开Excel
            ApplicationClass xlsxapp = new ApplicationClass();
            //新建一张Excel工作簿
            Workbook wbook = xlsxapp.Workbooks.Add(true);
            //第一个sheet页
            Worksheet wsheet = (Worksheet)wbook.Worksheets.get_Item(1);
            //将DataTable的列名显示在Excel表第一行
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                //注意Excel表的行和列的索引都是从1开始的
                wsheet.Cells[1, i + 1] = dt.Columns[i].ColumnName;
            }
            //遍历DataTable，给Excel赋值
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    //从第二行第一列开始写入数据
                    wsheet.Cells[i + 2, j + 1] = dt.Rows[i][j];
                }
            }
            //保存文件
            wbook.SaveAs(filepath);
            //释放资源
            xlsxapp.Quit();
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <param name="myDGV">控件DataGridView</param>
        private void ExportExcels(string fileName, DataGridView myDGV)
        {
            string saveFileName = "";
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.DefaultExt = "xls";
            saveDialog.Filter = "Excel文件|*.xls";
            saveDialog.FileName = fileName;
            saveDialog.ShowDialog();
            saveFileName = saveDialog.FileName;
            if (saveFileName.IndexOf(":") < 0) return; //被点了取消
            Microsoft.Office.Interop.Excel.Application xlApp = new Microsoft.Office.Interop.Excel.Application();
            if (xlApp == null)
            {
                MessageBox.Show("无法创建Excel对象，可能您的机子未安装Excel");
                return;
            }
            Microsoft.Office.Interop.Excel.Workbooks workbooks = xlApp.Workbooks;
            Microsoft.Office.Interop.Excel.Workbook workbook = workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
            Microsoft.Office.Interop.Excel.Worksheet worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Worksheets[1];//取得sheet1
                                                                                                                                  //写入标题
            for (int i = 0; i < myDGV.ColumnCount; i++)
            {
                worksheet.Cells[1, i + 1] = myDGV.Columns[i].HeaderText;
            }
            //写入数值
            for (int r = 0; r < myDGV.Rows.Count; r++)
            {
                for (int i = 0; i < myDGV.ColumnCount; i++)
                {
                    worksheet.Cells[r + 2, i + 1] = myDGV.Rows[r].Cells[i].Value;
                }
                System.Windows.Forms.Application.DoEvents();
            }
            worksheet.Columns.EntireColumn.AutoFit();//列宽自适应
            if (saveFileName != "")
            {
                try
                {
                    workbook.Saved = true;
                    workbook.SaveCopyAs(saveFileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出文件时出错,文件可能正被打开！\n" + ex.Message);
                }
            }
            xlApp.Quit();
            GC.Collect();//强行销毁
            MessageBox.Show("文件： " + fileName + ".xls 保存成功", "信息提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

}