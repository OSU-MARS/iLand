﻿namespace iLand.World
{
    public class WaterCycleData
    {
        /// height of snow cover [mm water column]
        public float[] SnowCover { get; private init; }
        /// daily amount of water that actually reaches the ground (i.e., after interception)
        public float[] WaterReachingGround { get; private init; }

        public WaterCycleData()
        {
            this.SnowCover = new float[Constant.DaysInLeapYear];
            this.WaterReachingGround = new float[Constant.DaysInLeapYear];
        }
    }
}
