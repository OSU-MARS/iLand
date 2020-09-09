namespace iLand.core
{
    internal class WaterCycleData
    {
        /// daily amount of water that actually reaches the ground (i.e., after interception)
        public double[] water_to_ground;
        /// height of snow cover [mm water column]
        public double[] snow_cover;

        public WaterCycleData()
        {
            water_to_ground = new double[366];
            snow_cover = new double[366];
        }
    }
}
