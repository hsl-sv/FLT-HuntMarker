using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
