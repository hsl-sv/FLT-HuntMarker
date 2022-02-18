using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FLT_HuntMarker
{
    public enum DotType
    {
        Circle,
    }

    class CONFIG
    {
        public const string TIMESTAMP_FORMAT = "MM-dd HH:mm:ss";
        public const string OBJECT_PREFIX = "ID_";
        public const string LOGFILE = @"./Log.log";
        public const string TMPFILE = @"./Log.tmp";

        public static double COLOR_OPACITY = 0.8;
        public static SolidColorBrush COLOR_S = new(Colors.Orange) { Opacity = COLOR_OPACITY };
        public static SolidColorBrush COLOR_A = new(Colors.LightGreen) { Opacity = COLOR_OPACITY };
        public static SolidColorBrush COLOR_B = new(Colors.LightBlue) { Opacity = COLOR_OPACITY };
    }
}
