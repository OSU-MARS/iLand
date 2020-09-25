namespace iLand.core
{
    internal class WaterCycleData
    {
        /// height of snow cover [mm water column]
        public double[] SnowCover { get; private set; }
        /// daily amount of water that actually reaches the ground (i.e., after interception)
        public double[] WaterReachingGround { get; private set; }

        public WaterCycleData()
        {
            this.SnowCover = new double[366];
            this.WaterReachingGround = new double[366];
        }
    }
}
