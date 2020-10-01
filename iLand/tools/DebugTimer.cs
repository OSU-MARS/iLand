using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace iLand.Tools
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
        private static readonly Dictionary<string, TimeSpan> TimersByName;

        private bool isDisposed;
        private readonly Stopwatch stopwatch;
        private readonly string name;

        // if true, hide messages for short operations (except an explicit call to showElapsed())
        public bool HideShort { get; set; }
        public bool IsSilent { get; set; }

        static DebugTimer()
        {
            DebugTimer.TimersByName = new Dictionary<string, TimeSpan>();
        }

        public DebugTimer(string name, bool isSilent = true)
        {
            this.isDisposed = false;
            this.name = name;
            this.stopwatch = new Stopwatch();

            this.IsSilent = isSilent;
            this.HideShort = true;

            if (DebugTimer.TimersByName.ContainsKey(name) == false)
            {
                lock (DebugTimer.TimersByName)
                {
                    if (DebugTimer.TimersByName.ContainsKey(name) == false)
                    {
                        DebugTimer.TimersByName.Add(name, TimeSpan.Zero);
                    }
                }
            }

            this.stopwatch.Start();
        }

        public static void ClearAllTimers()
        {
            foreach (string key in DebugTimer.TimersByName.Keys)
            {
                DebugTimer.TimersByName[key] = TimeSpan.Zero;
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
                    TimeSpan elapsed = this.Elapsed();
                    DebugTimer.TimersByName[name] += elapsed;
                    // show message if timer is not set to silent, and if time > 100ms (if timer is set to hideShort (which is the default))
                    if (this.IsSilent == false && (this.HideShort == false || elapsed.TotalSeconds > 1.0))
                    {
                        Debug.WriteLine("Timer " + name + ": " + DebugTimer.TimeStr(this.Elapsed()));
                    }
                }

                this.isDisposed = true;
            }
        }

        public static void WriteTimers()
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (KeyValuePair<string, TimeSpan> timer in DebugTimer.TimersByName)
            {
                if (timer.Value > TimeSpan.Zero)
                {
                    Debug.WriteLine("Profile: " + timer.Key + " " + DebugTimer.TimeStr(timer.Value));
                }
                total += timer.Value;
            }
            Debug.WriteLine("Profile: total time elapsed under debug timers " + DebugTimer.TimeStr(total) + ".");
        }

        // pretty formatting of timing information
        public static string TimeStr(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60)
            {
                return duration.ToString("s\\.fff", CultureInfo.InvariantCulture) + " s";
            }
            if (duration.TotalMinutes < 60)
            {
                return duration.ToString("m\\:ss\\.fff", CultureInfo.InvariantCulture);
            }
            return duration.ToString("h\\:mm\\:ss");
        }

        public TimeSpan Elapsed()
        {
            return stopwatch.Elapsed;
        }
    }
}
