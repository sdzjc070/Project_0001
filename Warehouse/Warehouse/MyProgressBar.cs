using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warehouse
{
    public class MyProgressBar : VerticalProgressBar
    {
        ////重写OnPaint方法
        //protected override void OnForeColorChanged (EventArgs e)
        //{
        //    SolidBrush brush = null;
        //    Rectangle bounds = new Rectangle(0, 0, base.Width, base.Height);
        //    bounds.Width = ((int)(bounds.Width * (((double)base.Value) / ((double)base.Maximum)))) - 4;
        //    brush = new SolidBrush(Color.Coral);
        //    e.Graphics.FillRectangle(brush, 2, 2, bounds.Width, bounds.Height);


        //}
    }
}
