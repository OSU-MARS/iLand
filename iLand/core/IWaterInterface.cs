namespace iLand.core
{
    /** WaterInterface allows accessing intermediate water variables (e.g. interception)
     */
    internal interface IWaterInterface
    {
        void CalculateWater(ResourceUnit resource_unit, WaterCycleData water_data);
    }
}
