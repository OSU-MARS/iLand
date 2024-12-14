using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class Ecosystem : XmlSerializable
	{
		// density of air, kg/m³
		public float AirDensity { get; private set; }

		// GPP to NPP, Waring 1998
		public float AutotrophicRespirationMultiplier { get; private set; }

		// 3-PG evapotranspiration
		// evaporation transmittance, m/s
		public float BoundaryLayerConductance { get; private set; }

		// maximum monthly mean light use efficency used for the 3-PG model, gC/MJ
		public float LightUseEpsilon { get; private set; }

		public float InterceptionStorageBroadleafInMM { get; private set; } // C++: mDecidousFactor
        public float InterceptionStorageNeedleInMM { get; private set; } // C++: mNeedleFactor

		public float GroundVegetationLeafAreaIndex { get; private set; }
        public float GroundVegetationPsiMin { get; private set; }

        // for calculation of max-canopy-conductance
        public float LaiThresholdForConstantStandConductance { get; private set; }

		// "k" parameter (Beer-Lambert) used for calculation of absorbed light on resource unit level
		public float ResourceUnitLightExtinctionCoefficient { get; private set; } // C++: lightExtinctionCoefficient

        public float SnowDensity { get; private set; } ///< density (kg/m3) of the snow
		public float SnowInitialDepth { get; private set; }	// m
		// TODO: remove unused
        //public float SnowTemperature { get; private set; } ///< Threshold temperature for snowing / snow melt

        // "k" for beer lambert used for opacity of single trees
        public float TreeLightStampExtinctionCoefficient { get; private set; } // C++: lightExtinctionCoefficientOpacity

        public float SnowmeltTemperature { get; private set; }

		// tau-value for trailing average temperature calculation: τ = number of days included in average
		// Mäkelä A, Pulkkinen M, Kolari P, et al. 2008. Developing an empirical model of stand GPP with the LUE approach: analysis of eddy covariance 
		// data at five contrasting conifer sites in Europe. Global Change Biology 14(1):92-108. https://doi.org/10.1111/j.1365-2486.2007.01463.x
		public float TemperatureMA1tau { get; private set; }

		public Ecosystem()
        {
			this.AirDensity = 1.2041F; // dry air at 20 °C and 101.325 kPa
			this.AutotrophicRespirationMultiplier = 0.47F;
			this.BoundaryLayerConductance = 0.2F;
            this.GroundVegetationLeafAreaIndex = 1.0F;
            this.GroundVegetationPsiMin = -1.5F;
            this.InterceptionStorageBroadleafInMM = 2.0F;
			this.InterceptionStorageNeedleInMM = 4.0F;
			this.LaiThresholdForConstantStandConductance = 3.0F;
			this.ResourceUnitLightExtinctionCoefficient = 0.5F;
            this.SnowDensity = 300.0F;
            this.SnowInitialDepth = 0.0F;
            //this.SnowTemperature = 0.0F;
            this.TreeLightStampExtinctionCoefficient = 0.5F;
			this.LightUseEpsilon = 2.8F; // max light use efficiency (aka alpha_c), gC/MJ
			this.SnowmeltTemperature = 0.0F;
			this.TemperatureMA1tau = 5.0F;
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "ecosystem":
					reader.Read();
					break;
				case "autotrophicRespirationMultiplier":
					this.AutotrophicRespirationMultiplier = reader.ReadElementContentAsFloat();
					if ((this.AutotrophicRespirationMultiplier < 0.0F) || (this.AutotrophicRespirationMultiplier > 1.0F))
					{
						throw new XmlException("Autotrophic respiration multiplier " + this.AutotrophicRespirationMultiplier + " is negative or greater than 1.0.");
					}
					break;
				case "lightUseEpsilon":
					this.LightUseEpsilon = reader.ReadElementContentAsFloat();
					if (this.LightUseEpsilon < 0.0F)
					{
						throw new XmlException("Light use epsilon " + this.LightUseEpsilon + "is negative.");
					}
					break;
				case "lightExtinctionCoefficient":
					this.ResourceUnitLightExtinctionCoefficient = reader.ReadElementContentAsFloat();
					if (this.ResourceUnitLightExtinctionCoefficient < 0.0F)
					{
						throw new XmlException("Light extinction coefficient " + this.ResourceUnitLightExtinctionCoefficient + " is negative.");
					}
					break;
				case "lightExtinctionCoefficientOpacity":
					this.TreeLightStampExtinctionCoefficient = reader.ReadElementContentAsFloat();
					if (this.TreeLightStampExtinctionCoefficient < 0.0F)
					{
						throw new XmlException("Light extinction opacity " + this.TreeLightStampExtinctionCoefficient + "is negative.");
					}
					break;
				case "temperatureMA1tau":
					this.TemperatureMA1tau = reader.ReadElementContentAsFloat();
					if (this.TemperatureMA1tau < 0.0F)
					{
						throw new XmlException("Number of days' temperature to average (τ) is negative.");
					}
					break;
				case "airDensity":
					this.AirDensity = reader.ReadElementContentAsFloat();
					if (this.AirDensity < 0.0F)
					{
						throw new XmlException("Air density is negative.");
					}
					break;
				case "laiThresholdForClosedStands":
					this.LaiThresholdForConstantStandConductance = reader.ReadElementContentAsFloat();
					if (this.LaiThresholdForConstantStandConductance < 0.0F)
					{
						throw new XmlException("LAI threshold for closed stands is negative.");
					}
					break;
				case "boundaryLayerConductance":
					this.BoundaryLayerConductance = reader.ReadElementContentAsFloat();
					if (this.BoundaryLayerConductance < 0.0F)
					{
						throw new XmlException("Boundary layer conductance is negative.");
					}
					break;
                case "groundVegetationLAI":
                    this.GroundVegetationLeafAreaIndex = reader.ReadElementContentAsFloat();
                    if ((this.GroundVegetationLeafAreaIndex < 0.0F) || (this.GroundVegetationLeafAreaIndex > 25.0F)) // sanity upper bound
                    {
                        throw new XmlException("Ground vegetation leaf area index " + this.GroundVegetationLeafAreaIndex + " is negative or unexpectedly large.");
                    }
                    break;
                case "groundVegetationPsiMin":
                    this.GroundVegetationPsiMin = reader.ReadElementContentAsFloat();
                    if (this.GroundVegetationPsiMin > 0.0F)
                    {
                        throw new XmlException("Ground vegetation leaf area index " + this.GroundVegetationLeafAreaIndex + " is positive.");
                    }
                    break;
                case "interceptionStorageNeedle":
					this.InterceptionStorageNeedleInMM = reader.ReadElementContentAsFloat();
					if (this.InterceptionStorageNeedleInMM < 0.0F)
					{
						throw new XmlException("Needle interception storage of " + this.InterceptionStorageNeedleInMM + " mm is negative.");
					}
					break;
				case "interceptionStorageBroadleaf":
					this.InterceptionStorageBroadleafInMM = reader.ReadElementContentAsFloat();
					if (this.InterceptionStorageBroadleafInMM < 0.0F)
					{
						throw new XmlException("Broadleaf inteception storage of " + this.InterceptionStorageBroadleafInMM + " mm is negative.");
					}
					break;
                case "snowDensity":
                    this.SnowDensity = reader.ReadElementContentAsFloat();
                    if ((this.SnowDensity < 0.0F) || (this.SnowDensity > 1000.3F)) // sanity upper bound of water at 4 °C
                    {
                        throw new XmlException("Snow density of " + this.SnowDensity + " kg/m³ is negative or unexpectedly high.");
                    }
                    break;
                case "snowInitialDepth":
                    this.SnowInitialDepth = reader.ReadElementContentAsFloat();
                    if ((this.SnowInitialDepth < 0.0F) || (this.SnowInitialDepth > 1000.0F)) // sanity upper bound assuming project areas don't include large ice sheets
                    {
                        throw new XmlException("Initial snow depth of " + this.SnowInitialDepth + " m is negative or unexpectedly high.");
                    }
                    break;
                //case "snowTemperature":
                //    this.SnowTemperature = reader.ReadElementContentAsFloat();
                //    if ((this.SnowTemperature < -60.0F) || (this.SnowTemperature > 20.0F)) // arbitrary sanity range
                //    {
                //        throw new XmlException("Initial snow temperature of " + this.SnowTemperature + " °C is unexpectedly low or high.");
                //    }
                //    break;
                case "snowMeltTemperature":
                    this.SnowmeltTemperature = reader.ReadElementContentAsFloat();
                    if ((this.SnowmeltTemperature < -20.0F) || (this.SnowmeltTemperature > 20.0F)) // arbitrary sanity range
                    {
                        throw new XmlException("Snowmelt temperature of " + this.SnowmeltTemperature + " °C is unexpectedly low or high.");
                    }
                    break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
