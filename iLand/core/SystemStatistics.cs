using iLand.tools;
using System.Collections.Generic;

namespace iLand.core
{
    /** holds system statistics primarily aimed for performance and memory analyis.
      */
    internal class SystemStatistics
    {
        public int NewSaplings { get; set; }
        public int SaplingCount { get; set; }
        public int TreeCount { get; set; }

        public double ApplyPatternTime { get; set; }
        public double CarbonCycleTime { get; set; }
        public double EstablishmentTime { get; set; }
        public double ManagementTime { get; set; }
        public double ReadPatternTime { get; set; }
        public double SeedDistributionTime { get; set; }
        public double SaplingTime { get; set; }
        public double TotalYearTime { get; set; }
        public double TreeGrowthTime { get; set; }
        public double WriteOutputTime { get; set; }

        public SystemStatistics()
        {
            Reset();
        }

        public void Reset()
        {
            TreeCount = 0; 
            SaplingCount = 0; 
            NewSaplings = 0;
            ManagementTime = 0.0; 
            ApplyPatternTime = ReadPatternTime = TreeGrowthTime = 0.0;
            SeedDistributionTime = SaplingTime = EstablishmentTime = CarbonCycleTime = WriteOutputTime = TotalYearTime = 0.0;
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
