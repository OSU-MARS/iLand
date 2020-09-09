namespace iLand.core
{
    internal class ResourceUnitVariables
    {
        public double nitrogenAvailable; ///< nitrogen content  = kg/m2/year)
        public double cumCarbonUptake; ///< NPP   = kg C/ha)
        public double cumCarbonToAtm; ///< total flux of carbon to atmosphere  = kg C/ha)
        public double cumNEP; ///< cumulative ecosystem productivity  = kg C/ha), i.e. cumulative = NPP-losses = atm,harvest)
        public double carbonUptake, carbonToAtm, NEP; ///< values of the current year  = NPP, flux to atmosphere, net ecosystem prod., all values in kgC/ha)

        public ResourceUnitVariables()
        {
            nitrogenAvailable = 0.0; 
            cumCarbonUptake = 0.0; 
            cumCarbonToAtm = 0.0; 
            cumNEP = 0.0;
            carbonUptake = 0.0; 
            carbonToAtm = 0.0;
            NEP = 0.0;
        }
    }
}
