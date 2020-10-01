namespace iLand.Core
{
    public class WaterCycleData
    {
        /// height of snow cover [mm water column]
        public double[] SnowCover { get; private set; }
        /// daily amount of water that actually reaches the ground (i.e., after interception)
        public double[] WaterReachingGround { get; private set; }

        public WaterCycleData()
        {
            this.SnowCover = new double[Constant.DaysInLeapYear];
            this.WaterReachingGround = new double[Constant.DaysInLeapYear];
        }
    }
}
