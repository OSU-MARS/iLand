namespace iLand.core
{
    /** WaterInterface allows accessing intermediate water variables (e.g. interception)
     */
    internal interface WaterInterface
    {
        void calculateWater(ResourceUnit resource_unit, WaterCycleData water_data);
    }
}
