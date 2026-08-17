using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Painto.Modules
{
    public class PenData
    {
        public Windows.UI.Color PenColor { get; set; }
        public int Thickness { get; set; }
        public string penType { get; set; }
        public string Icon { get; set; }
        public string PenColorString;
        public double Opacity { get; set; } = 1.0;
        public string Name { get; set; }
    }
}
