using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class ModelSettings
    {
		// if true, seed dispersal, establishment, ... is modelled
		[XmlElement(ElementName = "regenerationEnabled")]
		public bool RegenerationEnabled { get; set; }

		// if false, no natural (intrinsic+stress) mortality occurs
		[XmlElement(ElementName = "mortalityEnabled")]
		public bool MortalityEnabled { get; set; }

		// if false, trees will apply/read light patterns, but do not grow
		[XmlElement(ElementName = "growthEnabled")]
		public bool GrowthEnabled { get; set; }

		// if true, snag dynamics and soil CN cycle is modelled
		[XmlElement(ElementName = "carbonCycleEnabled")]
		public bool CarbonCycleEnabled { get; set; }

		// maximum light use efficency used for the 3PG model
		[XmlElement(ElementName = "epsilon")]
		public float Epsilon { get; set; }

		// "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
		[XmlElement(ElementName = "lightExtinctionCoefficient")]
		public float LightExtinctionCoefficient { get; set; }

		// "k" for beer lambert used for opacity of single trees
		[XmlElement(ElementName = "lightExtinctionCoefficientOpacity")]
		public float LightExtinctionCoefficientOpacity { get; set; }

		// "tau"-value for delayed temperature calculation acc. to Maekela 2008
		[XmlElement(ElementName = "temperatureTau")]
		public float TemperatureTau { get; set; }

		// density of air [kg / m3]
		[XmlElement(ElementName = "airDensity")]
		public float AirDensity { get; set; }

		// for calculation of max-canopy-conductance
		[XmlElement(ElementName = "laiThresholdForClosedStands")]
		public float LaiThresholdForClosedStands { get; set; }

		// 3pg-evapotranspiration
		[XmlElement(ElementName = "boundaryLayerConductance")]
		public float BoundaryLayerConductance { get; set; }

		[XmlElement(ElementName = "interceptionStorageNeedle")]
		public float InterceptionStorageNeedle { get; set; }

		[XmlElement(ElementName = "interceptionStorageBroadleaf")]
		public float InterceptionStorageBroadleaf { get; set; }

		[XmlElement(ElementName = "snowMeltTemperature")]
		public float SnowMeltTemperature { get; set; }

		[XmlElement(ElementName = "waterUseSoilSaturation")]
		public bool WaterUseSoilSaturation { get; set; }

		// if true, the 'correct' version of the calculation of belowground allocation is used (default=true)
		[XmlElement(ElementName = "usePARFractionBelowGroundAllocation")]
		public bool UseParFractionBelowGroundAllocation { get; set; }

		[XmlElement(ElementName = "seedDispersal")]
		public SeedDispersal SeedDispersal { get; set; }

		[XmlElement(ElementName = "soil")]
		public Soil Soil { get; set; }

		[XmlElement(ElementName = "grass")]
		public Grass Grass { get; set; }

		[XmlElement(ElementName = "browsing")]
		public Browsing Browsing { get; set; }

		public ModelSettings()
        {
			this.AirDensity = 1.2F;
			this.BoundaryLayerConductance = 0.2F;
			this.CarbonCycleEnabled = false;
			this.Epsilon = 1.8F; // max light use efficiency (aka alpha_c)
			this.GrowthEnabled = true;
			this.InterceptionStorageBroadleaf = 2.0F;
			this.InterceptionStorageNeedle = 4.0F;
			this.LaiThresholdForClosedStands = 3.0F;
			this.LightExtinctionCoefficient = 0.5F;
			this.LightExtinctionCoefficientOpacity = 0.5F;
			this.MortalityEnabled = true;
			this.RegenerationEnabled = false;
			this.SnowMeltTemperature = 0.0F;
			this.TemperatureTau = 5.0F;
			this.UseParFractionBelowGroundAllocation = true;
			this.WaterUseSoilSaturation = false; // TODO: unhelpful default since soil water capacity is ignored

			this.Browsing = new Browsing();
			this.Grass = new Grass();
			this.SeedDispersal = new SeedDispersal();
			this.Soil = new Soil();
        }
    }
}
