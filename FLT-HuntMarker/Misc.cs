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
        Flag,
    }

    class CONFIG
    {
        public const string TIMESTAMP_FORMAT = "MM-dd HH:mm:ss";
        public const string OBJECT_PREFIX = "ID_";
        public const string LOGFILE = @"./Log.log";
        public const string TMPFILE = @"./Log.tmp";

        public static double PARAM_DOT_SIZE = 20.0;
        public static double PARAM_FONT_SIZE = 11.0;
        public static double FF14_MAP_SIZE = 42.96; // Map size of X or Y

        public static double COLOR_OPACITY = 0.8;
        public static double PARAM_DUPLICATE_DISTANCE = 1.0;
        public static int PARAM_DUPLICATE_PERIOD_HOUR = 1;
        public static int PARAM_PLAYER_INTERVAL_MS = 200;
        public static int PARAM_HOOK_INTERVAL_MS = 3000; // Check FF14 process start / exit

        // Brushes are frozen : CONFIG may be first touched by a background thread (HookMonitor),
        // an unfrozen brush would belong to that thread and throw when used on UI thread
        public static SolidColorBrush COLOR_S = FrozenBrush(Colors.Orange, COLOR_OPACITY);
        public static SolidColorBrush COLOR_A = FrozenBrush(Colors.LightGreen, COLOR_OPACITY);
        public static SolidColorBrush COLOR_B = FrozenBrush(Colors.LightBlue, COLOR_OPACITY);
        public static SolidColorBrush COLOR_S_TEXT = FrozenBrush(Colors.Red);
        public static SolidColorBrush COLOR_A_TEXT = FrozenBrush(Colors.DarkGreen);
        public static SolidColorBrush COLOR_B_TEXT = FrozenBrush(Colors.Blue);
        public static SolidColorBrush COLOR_TEXT_BG = FrozenBrush(Colors.Yellow);
        public static SolidColorBrush COLOR_AETHERYTE = FrozenBrush(Colors.MediumPurple, COLOR_OPACITY);

        private static SolidColorBrush FrozenBrush(Color color, double opacity = 1.0)
        {
            SolidColorBrush brush = new(color) { Opacity = opacity };
            brush.Freeze();

            return brush;
        }
    }
}
