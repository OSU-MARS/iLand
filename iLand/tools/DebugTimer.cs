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
        private static Dictionary<string, double> mTimingList;

        private bool isDisposed;
        private TickTack t;
        private bool m_hideShort; // if true, hide messages for short operations (except an explicit call to showElapsed())
        private bool m_shown;
        private bool m_silent;
        private string m_caption;

        public void setSilent() { m_silent = true; }
        public void setHideShort(bool hide_short_messages) { m_hideShort = hide_short_messages; }

        static DebugTimer()
        {
            mTimingList = new Dictionary<string, double>();
        }

        public DebugTimer()
        {
            isDisposed = false;
            m_hideShort = false;
            m_silent = false;
            t = new TickTack();
            start();
        }

        public DebugTimer(string caption, bool silent = false)
        {
            isDisposed = false;
            m_caption = caption;
            m_silent = silent;
            m_hideShort = true;
            t = new TickTack();

            if (!mTimingList.ContainsKey(caption))
            {
                if (!mTimingList.ContainsKey(caption))
                {
                    mTimingList[caption] = 0.0;
                }
            }
            start();
        }

        public static void clearAllTimers()
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
                    double t = elapsed();
                    mTimingList[m_caption] += t;
                    // show message if timer is not set to silent, and if time > 100ms (if timer is set to hideShort (which is the default))
                    if (!m_silent && (!m_hideShort || t > 100.0))
                    {
                        showElapsed();
                    }
                }

                this.isDisposed = true;
            }
        }

        public static void printAllTimers()
        {
            Debug.WriteLine("Total timers" + Environment.NewLine + "================");
            double total = 0.0;
            foreach (KeyValuePair<string, double> timer in mTimingList)
            {
                if (timer.Value > 0.0)
                {
                    Debug.WriteLine(timer.Key + ": " + timeStr(timer.Value));
                }
                total += timer.Value;
            }
            Debug.WriteLine("Sum: " + total + "ms");
        }

        // pretty formatting of timing information
        public static string timeStr(double value_ms)
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

        public void interval(string text)
        {
            double elapsed_time = elapsed();
            Debug.WriteLine("Timer " + text + timeStr(elapsed_time));
            start();
        }

        public void showElapsed()
        {
            if (!m_shown)
            {
                Debug.WriteLine("Timer " + m_caption + ": " + timeStr(elapsed()));
            }
            m_shown = true;
        }

        public double elapsed()
        {
            return t.elapsed() * 1000;
        }

        public void start()
        {
            t.start();
            m_shown = false;
        }
    }
}
