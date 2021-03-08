using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tools
{
    public class DebugTimerCollection
    {
        private readonly Dictionary<string, TimeSpan> totalElapsedTimeByName;

        public DebugTimerCollection()
        {
            this.totalElapsedTimeByName = new Dictionary<string, TimeSpan>();
        }

        public void AddTime(string name, TimeSpan elapsed)
        {
            this.totalElapsedTimeByName[name] += elapsed;
        }

        public void Clear()
        {
            this.totalElapsedTimeByName.Clear();
        }

        public DebugTimer Create(string name)
        {
            DebugTimer timer = new(name, this);

            if (this.totalElapsedTimeByName.ContainsKey(name) == false)
            {
                lock (this.totalElapsedTimeByName)
                {
                    if (this.totalElapsedTimeByName.ContainsKey(name) == false)
                    {
                        this.totalElapsedTimeByName.Add(name, TimeSpan.Zero);
                    }
                }
            }

            return timer;
        }

        public void WriteTimers()
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (KeyValuePair<string, TimeSpan> timer in this.totalElapsedTimeByName)
            {
                if (timer.Value > TimeSpan.Zero)
                {
                    Debug.WriteLine("Profile: " + timer.Key + " " + DebugTimer.TimeStr(timer.Value));
                }
                total += timer.Value;
            }
            Debug.WriteLine("Profile: total time elapsed under debug timers " + DebugTimer.TimeStr(total) + ".");
        }
    }
}
