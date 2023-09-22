using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrigDataForERP
{
    class gv
    {
        public static bool CtrlLock = false;
        public static int EEGAmplCoef = 2;
        public static double ERPAmplCoef = 1;
        public static double EpochAmplCoef = 1;
        public static double RsltEpochAmplCoef = 1;

        public static int ORDER = 7;
        public static int N21 = 21;//
        public static int FIRST_EP = 400;
        public static int LAST_EP = 600;
        public static int NPOINTS;
        public static int NCAN = 1;
        public static int NMS = 1;

    }
}
