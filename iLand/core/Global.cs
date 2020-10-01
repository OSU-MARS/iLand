using System;
using System.Diagnostics;

namespace iLand.Core
{
    internal class Global
    {
        //typedef int TreeSpecies;

        //#define MSGRETURN(x) { qDebug() << x; return; }
        //#define WARNINGRETURN(x) { qWarning() << x; return; }
        //#define ERRORRETURN(x) { qError() << x; return; }

        // relocated from loose to GlobalSettings
        //void dbg_helper(const char* where, const char* what,const char* file,int line);
        //void dbg_helper_ext(const char* where, const char* what,const char* file,int line, const QString &s);
        //bool logLevelDebug(); // true, if detailed debug information is logged
        //bool logLevelInfo(); // true, if only important aggreate info is logged
        //bool logLevelWarning(); // true if only severe warnings/errors are logged.
        //void setLogLevel(int loglevel); // setter function

        public static int Modulo(int a, int b)
        {
            // because C modulo operation gives negative numbers for negative values, here a fix
            // that always returns positive numbers: http://www.lemoda.net/c/modulo-operator/
            return (((a % b) + b) % b);
        }

        // conversions rad/degree
        //#define RAD(x) (x*M_PI/180.)
        public static double ToRadians(double degrees)
        {
            return Math.PI / 180.0 * degrees;
        }

        //#define GRAD(x) (x/M_PI*180.)
        public static double ToDegrees(double radians)
        {
            return 180.0 / Math.PI * radians;
        }

        public static double Limit(double value, double min, double max)
        {
            Debug.Assert(max > min);
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        public static int Limit(int value, int min, int max)
        {
            Debug.Assert(max > min);
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        public static void SetBit(ref int rTarget, int bit, bool value)
        {
            if (value)
            {
                rTarget |= (1 << bit);  // set bit
            }
            else
            {
                rTarget &= ((1 << bit) ^ 0xffffff); // clear bit
            }
        }

        public static bool IsBitSet(int value, int bit)
        {
            return (value & (1 << bit)) != 0;
        }
    }
}
