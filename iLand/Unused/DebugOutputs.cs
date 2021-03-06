﻿using System;

namespace iLand.Tools
{
    // fine grained debug outputs
    // defines available debug output types.
    [Flags]
    public enum DebugOutputs : uint
    {
        TreeNpp = 1, 
        TreePartition = 2, 
        TreeGrowth = 4,
        StandGpp = 8, 
        WaterCycle = 16,
        DailyResponses = 32,
        Establishment = 64, 
        SaplingGrowth = 128, 
        CarbonCycle = 256,
        Performance = 512
    };
}
