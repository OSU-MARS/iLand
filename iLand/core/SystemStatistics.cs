using iLand.tools;
using System.Collections.Generic;

namespace iLand.core
{
    /** holds a couple of system statistics primarily aimed for performance and memory analyis.
      */
    internal class SystemStatistics
    {
        // the system counters
        public int treeCount;
        public int saplingCount;
        public int newSaplings;
        // timings
        public double tManagement;
        public double tApplyPattern;
        public double tReadPattern;
        public double tTreeGrowth;
        public double tSeedDistribution;
        public double tSapling;
        public double tEstablishment;
        public double tCarbonCycle;
        public double tWriteOutput;
        public double tTotalYear;

        public SystemStatistics()
        {
            reset();
        }

        public void reset()
        {
            treeCount = 0; saplingCount = 0; newSaplings = 0;
            tManagement = 0.0; tApplyPattern = tReadPattern = tTreeGrowth = 0.0;
            tSeedDistribution = tSapling = tEstablishment = tCarbonCycle = tWriteOutput = tTotalYear = 0.0;
        }

        public void writeOutput()
        {
            if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dPerformance))
            {
                List<object> output = GlobalSettings.instance().debugList(0, DebugOutputs.dPerformance);
                output.AddRange(new object[] { treeCount, saplingCount, newSaplings, tManagement, tApplyPattern, tReadPattern, tTreeGrowth,
                                               tSeedDistribution, tEstablishment, tSapling, tCarbonCycle, tWriteOutput, tTotalYear } );
            }
        }

    }
}
