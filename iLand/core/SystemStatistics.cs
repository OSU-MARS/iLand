using iLand.Tools;
using System;
using System.Collections.Generic;

namespace iLand.Core
{
    /** holds system statistics primarily aimed for performance and memory analyis.
      */
    internal class SystemStatistics
    {
        public int NewSaplings { get; set; }
        public int SaplingCount { get; set; }
        public int TreeCount { get; set; }

        public TimeSpan ApplyPatternTime { get; set; }
        public TimeSpan CarbonCycleTime { get; set; }
        public TimeSpan EstablishmentTime { get; set; }
        public TimeSpan ManagementTime { get; set; }
        public TimeSpan ReadPatternTime { get; set; }
        public TimeSpan SeedDistributionTime { get; set; }
        public TimeSpan SaplingTime { get; set; }
        public TimeSpan TotalYearTime { get; set; }
        public TimeSpan TreeGrowthTime { get; set; }
        public TimeSpan WriteOutputTime { get; set; }

        public SystemStatistics()
        {
            Reset();
        }

        public void Reset()
        {
            TreeCount = 0; 
            SaplingCount = 0; 
            NewSaplings = 0;
            ManagementTime = TimeSpan.Zero; 
            ApplyPatternTime = ReadPatternTime = TreeGrowthTime = TimeSpan.Zero;
            SeedDistributionTime = SaplingTime = EstablishmentTime = CarbonCycleTime = WriteOutputTime = TotalYearTime = TimeSpan.Zero;
        }

        public void WriteOutput()
        {
            if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.Performance))
            {
                List<object> output = GlobalSettings.Instance.DebugList(0, DebugOutputs.Performance);
                output.AddRange(new object[] { TreeCount, SaplingCount, NewSaplings, ManagementTime, ApplyPatternTime, ReadPatternTime, TreeGrowthTime,
                                               SeedDistributionTime, EstablishmentTime, SaplingTime, CarbonCycleTime, WriteOutputTime, TotalYearTime } );
            }
        }
    }
}
