namespace iLand.World
{
    public class WaterCycleData
    {
        /// height of snow cover [mm water column]
        public float[] SnowCover { get; private init; }
        /// daily amount of water that actually reaches the ground (i.e., after interception)
        public float[] WaterReachingSoilByWeatherTimestep { get; private init; }

        public WaterCycleData()
        {
            this.SnowCover = new float[Constant.DaysInLeapYear];
            this.WaterReachingSoilByWeatherTimestep = new float[Constant.DaysInLeapYear]; // TODO: be able to allocate by number of weather timesteps per year
        }
    }
}
