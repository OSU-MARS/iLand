namespace iLand.World
{
    public class ResourceUnitVariables
    {
        // values of the current year = NPP, flux to atmosphere, net ecosystem productivity, all in kgC/ha
        public double CarbonToAtmosphere { get; set; }
        public double Nep { get; set; }
        public double Npp { get; set; }

        public double TotalCarbonToAtmosphere { get; set; } // total flux of carbon to atmosphere, kg C/ha
        // cumulative ecosystem productivity, kg C/ha, i.e. cumulative = NPP-losses = atm,harvest
        public double TotalNep { get; set; }
        // NPP, kg C/ha
        public double TotalNpp { get; set; }

        public ResourceUnitVariables()
        {
            this.CarbonToAtmosphere = 0.0;
            this.Npp = 0.0;
            this.TotalCarbonToAtmosphere = 0.0;
            this.TotalNpp = 0.0;
            this.TotalNep = 0.0;
            this.Nep = 0.0;
        }
    }
}
