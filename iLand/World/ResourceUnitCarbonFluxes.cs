namespace iLand.World
{
    public class ResourceUnitCarbonFluxes
    {
        // values of the current year = NPP, flux to atmosphere, net ecosystem productivity, all in kg C/ha
        public float CarbonToAtmosphereInKgPerHa { get; set; }
        public float NepInKgCPerHa { get; set; }
        public float NppInKgCPerHa { get; set; }

        public float CumulativeCarbonToAtmosphereInKgPerHa { get; set; } // total flux of carbon to atmosphere, kg C/ha
        // cumulative ecosystem productivity, kg C, i.e. cumulative = NPP - losses to atmosphere and harvest
        public float CumulativeNepInKgCPerHa { get; set; }
        // NPP, kg C/ha
        public float CumulativeNppInKgCPerHa { get; set; }

        public ResourceUnitCarbonFluxes()
        {
            this.CarbonToAtmosphereInKgPerHa = 0.0F;
            this.CumulativeCarbonToAtmosphereInKgPerHa = 0.0F;
            this.CumulativeNppInKgCPerHa = 0.0F;
            this.CumulativeNepInKgCPerHa = 0.0F;
            this.NepInKgCPerHa = 0.0F;
            this.NppInKgCPerHa = 0.0F;
        }
    }
}
