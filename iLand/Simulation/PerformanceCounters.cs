using System;

namespace iLand.Simulation
{
    public class PerformanceCounters
    {
        public TimeSpan LightFill { get; set; }
        public TimeSpan LightPattern { get; set; }
        public TimeSpan Logging { get; set; }
        public TimeSpan ObjectInstantiation { get; set; }
        public TimeSpan ObjectSetup { get; set; }
        public TimeSpan OnStartYear { get; set; }
        public TimeSpan TreeGrowthAndMortality { get; set; }
        public TimeSpan TreeInstantiation { get; set; }

        public PerformanceCounters()
        {
            this.LightFill = TimeSpan.Zero;
            this.LightPattern = TimeSpan.Zero;
            this.Logging = TimeSpan.Zero;
            this.ObjectInstantiation = TimeSpan.Zero;
            this.ObjectSetup = TimeSpan.Zero;
            this.OnStartYear = TimeSpan.Zero;
            this.TreeGrowthAndMortality = TimeSpan.Zero;
            this.TreeInstantiation = TimeSpan.Zero;
        }
    }
}
