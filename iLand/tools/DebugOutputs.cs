using System;

namespace iLand.tools
{
    // fine grained debug outputs
    // defines available debug output types.
    [Flags]
    internal enum DebugOutputs : uint
    {
        dTreeNPP = 1, 
        dTreePartition = 2, 
        dTreeGrowth = 4,
        dStandGPP = 8, 
        dWaterCycle = 16,
        dDailyResponses = 32,
        dEstablishment = 64, 
        dSaplingGrowth = 128, 
        dCarbonCycle = 256,
        dPerformance = 512
    };
}
