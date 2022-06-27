using System.Diagnostics;

namespace iLand.Tool
{
    // TODO: remove and convert to Stopwatch()
    internal class TickTack
    {
        private readonly Stopwatch t;

        public TickTack() 
        {
            t = new Stopwatch();
            Reset(); 
        }

        public double Elapsed()
        {
            return t.Elapsed.TotalSeconds;
        }

        public void Reset() 
        { 
            t.Restart(); 
        }

        public void Start()
        {
            t.Start();
        }
    }
}
