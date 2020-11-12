using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ModelSettings : XmlSerializable
    {
		// if true, seed dispersal, establishment, ... is modelled
		public bool RegenerationEnabled { get; private set; }

		// if false, no natural (intrinsic+stress) mortality occurs
		public bool MortalityEnabled { get; private set; }

		// if false, trees will apply/read light patterns, but do not grow
		public bool GrowthEnabled { get; private set; }

		// if true, snag dynamics and soil CN cycle is modelled
		public bool CarbonCycleEnabled { get; private set; }

		// maximum light use efficency used for the 3PG model
		public float Epsilon { get; private set; }

		// "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
		public float LightExtinctionCoefficient { get; private set; }

		// "k" for beer lambert used for opacity of single trees
		public float LightExtinctionCoefficientOpacity { get; private set; }

		// "tau"-value for delayed temperature calculation acc. to Maekela 2008
		public float TemperatureTau { get; private set; }

		// density of air [kg / m3]
		public float AirDensity { get; private set; }

		// for calculation of max-canopy-conductance
		public float LaiThresholdForClosedStands { get; private set; }

		// 3pg-evapotranspiration
		public float BoundaryLayerConductance { get; private set; }

		public float InterceptionStorageNeedle { get; private set; }
		public float InterceptionStorageBroadleaf { get; private set; }
		public float SnowMeltTemperature { get; private set; }
		public bool WaterUseSoilSaturation { get; private set; }

		// if true, the 'correct' version of the calculation of belowground allocation is used (default=true)
		public bool UseParFractionBelowGroundAllocation { get; private set; }

		public SeedDispersal SeedDispersal { get; private set; }
		public DefaultSoil DefaultSoil { get; private set; }
		public Grass Grass { get; private set; }
		public Browsing Browsing { get; private set; }

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
			this.DefaultSoil = new DefaultSoil();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("settings"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("regenerationEnabled"))
			{
				this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("mortalityEnabled"))
			{
				this.MortalityEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("growthEnabled"))
			{
				this.GrowthEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("carbonCycleEnabled"))
			{
				this.CarbonCycleEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("regenerationEnabled"))
			{
				this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("epsilon"))
			{
				this.Epsilon = reader.ReadElementContentAsFloat();
				if (this.Epsilon < 0.0F)
                {
					throw new XmlException("Epsilon is negative.");
                }
			}
			else if (reader.IsStartElement("lightExtinctionCoefficient"))
			{
				this.LightExtinctionCoefficient = reader.ReadElementContentAsFloat();
				if (this.LightExtinctionCoefficient < 0.0F)
				{
					throw new XmlException("Light extinction coefficient is negative.");
				}
			}
			else if (reader.IsStartElement("lightExtinctionCoefficientOpacity"))
			{
				this.LightExtinctionCoefficientOpacity = reader.ReadElementContentAsFloat();
				if (this.LightExtinctionCoefficientOpacity < 0.0F)
				{
					throw new XmlException("Light extinction opacity is negative.");
				}
			}
			else if (reader.IsStartElement("temperatureTau"))
			{
				this.TemperatureTau = reader.ReadElementContentAsFloat();
				if (this.TemperatureTau < 0.0F)
				{
					throw new XmlException("Temperature tau is negative.");
				}
			}
			else if (reader.IsStartElement("airDensity"))
			{
				this.AirDensity = reader.ReadElementContentAsFloat();
				if (this.AirDensity < 0.0F)
				{
					throw new XmlException("Air density is negative.");
				}
			}
			else if (reader.IsStartElement("laiThresholdForClosedStands"))
			{
				this.LaiThresholdForClosedStands = reader.ReadElementContentAsFloat();
				if (this.AirDensity < 0.0F)
				{
					throw new XmlException("Air density is negative.");
				}
			}
			else if (reader.IsStartElement("boundaryLayerConductance"))
			{
				this.BoundaryLayerConductance = reader.ReadElementContentAsFloat();
				if (this.BoundaryLayerConductance < 0.0F)
				{
					throw new XmlException("Boundary layer conductance is negative.");
				}
			}
			else if (reader.IsStartElement("interceptionStorageNeedle"))
			{
				this.InterceptionStorageNeedle = reader.ReadElementContentAsFloat();
				if (this.InterceptionStorageNeedle < 0.0F)
				{
					throw new XmlException("Needle interception storage is negative.");
				}
			}
			else if (reader.IsStartElement("interceptionStorageBroadleaf"))
			{
				this.InterceptionStorageBroadleaf = reader.ReadElementContentAsFloat();
				if (this.InterceptionStorageBroadleaf < 0.0F)
				{
					throw new XmlException("Broadleaf inteception storage is negative.");
				}
			}
			else if (reader.IsStartElement("snowMeltTemperature"))
			{
				this.SnowMeltTemperature = reader.ReadElementContentAsFloat();
				if (this.SnowMeltTemperature < -273.15F)
				{
					throw new XmlException("Snowmelt temperature is below absolute zero.");
				}
			}
			else if (reader.IsStartElement("waterUseSoilSaturation"))
			{
				this.WaterUseSoilSaturation = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("usePARFractionBelowGroundAllocation"))
			{
				this.UseParFractionBelowGroundAllocation = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("seedDispersal"))
			{
				this.SeedDispersal.ReadXml(reader);
			}
			else if (reader.IsStartElement("defaultSoil"))
			{
				this.DefaultSoil.ReadXml(reader);
			}
			else if (reader.IsStartElement("grass"))
			{
				this.Grass.ReadXml(reader);
			}
			else if (reader.IsStartElement("browsing"))
			{
				this.Browsing.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
