using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageColorMatch
{
    public static class Extensions
    {
        public static int ToTrimmedByte(this float input)
        {
            if (input < 0)
            {
                return 0;
            }
            else if (input > 255)
            {
                return 255;
            }
            return (int)input;
        }
    }
}
