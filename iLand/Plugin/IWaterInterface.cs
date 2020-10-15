using iLand.World;

namespace iLand.Plugin
{
    /** WaterInterface allows accessing intermediate water variables (e.g. interception)
     */
    internal interface IWaterInterface
    {
        void CalculateWater(ResourceUnit resourceUnit, WaterCycleData waterCycle);
    }
}
