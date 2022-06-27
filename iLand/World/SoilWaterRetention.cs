using iLand.Input;

namespace iLand.World
{
    internal abstract class SoilWaterRetention
    {
        public abstract float GetSoilWaterPotentialFromWaterContent(float soilDepthInMM, float soilWaterContentInMM);
        public abstract float GetSoilWaterContentFromPsi(float soilDepthInMM, float psiInKilopascals);
        public abstract float Setup(EnvironmentReader environmentReader, bool useSoilSaturation);
    }
}
