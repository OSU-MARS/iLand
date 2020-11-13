namespace iLand.World
{
    public class ResourceUnitCarbonFluxes
    {
        // values of the current year = NPP, flux to atmosphere, net ecosystem productivity, all in kgC/ha
        public float CarbonToAtmosphere { get; set; }
        public float Nep { get; set; }
        public float Npp { get; set; }

        public float TotalCarbonToAtmosphere { get; set; } // total flux of carbon to atmosphere, kg C/ha
        // cumulative ecosystem productivity, kg C/ha, i.e. cumulative = NPP-losses = atm,harvest
        public float TotalNep { get; set; }
        // NPP, kg C/ha
        public float TotalNpp { get; set; }

        public ResourceUnitCarbonFluxes()
        {
            this.CarbonToAtmosphere = 0.0F;
            this.Npp = 0.0F;
            this.TotalCarbonToAtmosphere = 0.0F;
            this.TotalNpp = 0.0F;
            this.TotalNep = 0.0F;
            this.Nep = 0.0F;
        }
    }
}
