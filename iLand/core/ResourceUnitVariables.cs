namespace iLand.core
{
    internal class ResourceUnitVariables
    {
        public double CarbonToAtm { get; set; }
        public double CarbonUptake { get; set; }
        public double CumCarbonToAtm { get; set; } ///< total flux of carbon to atmosphere  = kg C/ha)
        public double CumCarbonUptake { get; set; } ///< NPP   = kg C/ha)
        public double CumNep { get; set; } ///< cumulative ecosystem productivity  = kg C/ha), i.e. cumulative = NPP-losses = atm,harvest)
        public double Nep { get; set; } ///< values of the current year  = NPP, flux to atmosphere, net ecosystem prod., all values in kgC/ha)
        public double NitrogenAvailable { get; set; } ///< nitrogen content  = kg/m2/year)

        public ResourceUnitVariables()
        {
            this.CarbonToAtm = 0.0;
            this.CarbonUptake = 0.0;
            this.CumCarbonToAtm = 0.0;
            this.CumCarbonUptake = 0.0;
            this.CumNep = 0.0;
            this.Nep = 0.0;
            this.NitrogenAvailable = 0.0;
        }
    }
}
