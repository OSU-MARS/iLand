using System.Diagnostics;

namespace iLand.tools
{
    internal class TickTack
    {
        private Stopwatch t;

        public TickTack() 
        {
            t = new Stopwatch();
            reset(); 
        }

        public double elapsed()
        {
            return t.Elapsed.TotalSeconds;
        }

        public void reset() 
        { 
            t.Restart(); 
        }

        public void start()
        {
            t.Start();
        }
    }
}
