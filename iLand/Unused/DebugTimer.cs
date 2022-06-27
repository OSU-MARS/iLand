using System;
using System.Diagnostics;
using System.Globalization;

namespace iLand.Tool
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
    public class DebugTimer : IDisposable
    {
        private bool isDisposed;
        private readonly string name;
        private readonly DebugTimerCollection parent;
        private readonly Stopwatch stopwatch;

        // if true, hide messages for short operations (except an explicit call to showElapsed())
        public bool HideShort { get; set; }
        public bool IsSilent { get; set; }

        public DebugTimer(string name, DebugTimerCollection parent, bool isSilent = true)
        {
            this.isDisposed = false;
            this.name = name;
            this.parent = parent;
            this.stopwatch = new Stopwatch();

            this.IsSilent = isSilent;
            this.HideShort = true;

            this.stopwatch.Start();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed == false)
            {
                if (disposing)
                {
                    TimeSpan elapsed = this.Elapsed();
                    this.parent.AddTime(this.name, elapsed);
                    // show message if timer is not set to silent, and if time > 100ms (if timer is set to hideShort (which is the default))
                    if (this.IsSilent == false && (this.HideShort == false || elapsed.TotalSeconds > 1.0))
                    {
                        Debug.WriteLine("Timer " + name + ": " + DebugTimer.TimeStr(this.Elapsed()));
                    }
                }

                this.isDisposed = true;
            }
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
