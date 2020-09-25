using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    /** Timer class that writes timings to the Debug-Output-Channel
        The class writes the elapsed time to qDebug() when either destructed, or when explicitely showElapsed() is called.
          elapsed() queries the elapsed time in milliseconds since construction or start() is called. Using interval() one can
          write a message with the time elapsed up the calling time, and the clock is reset afterwards. The name of the timer is
          set during construction. This message is printed when showElapsed() is called or durig destruction.
          Additionally, elapsed times of timers sharing the same caption are aggregated. Use clearAllTimers() to reset and printAllTimers()
          print the sums to the debug console. "Silent" DebugOutputs (setSilent() don't print timings for each iteration, but are still
            counted in the sums. If setAsWarning() is issued, the debug messages are print as warning, thus also visible
          when debug messages are disabled.
          @code void foo() {
             DebugTimer t("foo took [ms]:");
             <some lengthy operation>
         } // will e.g. print "foo took [ms]: 123" to debug console

         void bar() {
            clearAllTimers(); // set all timers to 0
            for (i=0;i<1000;i++)
               foo();
            printAllTimers(); // print the sum of the timings.
         }
         @endcode
         For Windows, the "TickTack"-backend is used.
    */
    internal class DebugTimer : IDisposable
    {
        private static readonly Dictionary<string, double> mTimingList;

        private bool isDisposed;
        private readonly TickTack t;
        private bool m_shown;
        private readonly string m_caption;

        // if true, hide messages for short operations (except an explicit call to showElapsed())
        public bool HideShort { get; set; }
        public bool Silent { get; set; }

        static DebugTimer()
        {
            mTimingList = new Dictionary<string, double>();
        }

        public DebugTimer()
        {
            isDisposed = false;
            HideShort = false;
            Silent = false;
            t = new TickTack();
            Start();
        }

        public DebugTimer(string caption, bool silent = false)
        {
            isDisposed = false;
            m_caption = caption;
            Silent = silent;
            HideShort = true;
            t = new TickTack();

            if (!mTimingList.ContainsKey(caption))
            {
                if (!mTimingList.ContainsKey(caption))
                {
                    mTimingList[caption] = 0.0;
                }
            }
            Start();
        }

        public static void ClearAllTimers()
        {
            foreach (string key in mTimingList.Keys)
            {
                mTimingList[key] = 0.0;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed == false)
            {
                if (disposing)
                {
                    double t = Elapsed();
                    mTimingList[m_caption] += t;
                    // show message if timer is not set to silent, and if time > 100ms (if timer is set to hideShort (which is the default))
                    if (!Silent && (!HideShort || t > 100.0))
                    {
                        ShowElapsed();
                    }
                }

                this.isDisposed = true;
            }
        }

        public static void PrintAllTimers()
        {
            Debug.WriteLine("Total timers" + Environment.NewLine + "================");
            double total = 0.0;
            foreach (KeyValuePair<string, double> timer in mTimingList)
            {
                if (timer.Value > 0.0)
                {
                    Debug.WriteLine(timer.Key + ": " + TimeStr(timer.Value));
                }
                total += timer.Value;
            }
            Debug.WriteLine("Sum: " + total + "ms");
        }

        // pretty formatting of timing information
        public static string TimeStr(double value_ms)
        {
            if (value_ms < 10000)
            {
                return value_ms.ToString("0ms");
            }
            if (value_ms < 60000)
            {
                return (value_ms / 1000).ToString("0.000s");
            }
            if (value_ms < 60000 * 60)
            {
                return String.Format("{0:0}m {1:0.000}s", Math.Floor(value_ms / 60000), (value_ms % 60000) / 1000);
            }

            return String.Format("{0:0}h {1:0}m {2:0.000}s", Math.Floor(value_ms / 3600000), //h
                                                             Math.Floor((value_ms % 3600000) / 60000), //m
                                                             Math.Round((value_ms % 60000) / 1000));    //s
        }

        public void Interval(string text)
        {
            double elapsed_time = Elapsed();
            Debug.WriteLine("Timer " + text + TimeStr(elapsed_time));
            Start();
        }

        public void ShowElapsed()
        {
            if (!m_shown)
            {
                Debug.WriteLine("Timer " + m_caption + ": " + TimeStr(Elapsed()));
            }
            m_shown = true;
        }

        public double Elapsed()
        {
            return t.Elapsed() * 1000;
        }

        public void Start()
        {
            t.Start();
            m_shown = false;
        }
    }
}
