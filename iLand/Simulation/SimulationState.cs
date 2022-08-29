using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace iLand.Simulation
{
    public class SimulationState
    {
        public int CurrentCalendarYear { get; set; }
        public ConcurrentQueue<DominantHeightBuffer> DominantHeightBuffers { get; private init; }
        public ConcurrentQueue<LightBuffer> LightBuffers { get; private init; }
        public ParallelOptions ParallelComputeOptions { get; private init; }
        public bool TraceAutoFlushValueToRestore { get; init; }
        public TraceListener? TraceListener { get; init; }

        public SimulationState(int initialCalendarYear, ParallelOptions parallelOptions)
        {
            // set to initial year so outputs with initial state start log it at the calendar year when the model was initialized
            this.CurrentCalendarYear = initialCalendarYear;
            this.DominantHeightBuffers = new();
            this.LightBuffers = new();
            this.ParallelComputeOptions = parallelOptions;
        }
    }
}
