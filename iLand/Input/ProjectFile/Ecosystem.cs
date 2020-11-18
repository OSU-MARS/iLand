using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class Ecosystem : XmlSerializable
	{
		// density of air, kg/m³
		public float AirDensity { get; private set; }

		// GPP to NPP, Waring 1998
		public float AutotrophicRespirationMultiplier { get; private set; }

		// 3pg-evapotranspiration
		public float BoundaryLayerConductance { get; private set; }

		// maximum light use efficency used for the 3PG model
		public float Epsilon { get; private set; }

		public float InterceptionStorageBroadleaf { get; private set; }
		public float InterceptionStorageNeedle { get; private set; }

		// for calculation of max-canopy-conductance
		public float LaiThresholdForClosedStands { get; private set; }

		// "k" parameter (Beer-Lambert) used for calculation of absorbed light on resource unit level
		public float LightExtinctionCoefficient { get; private set; }

		// "k" for beer lambert used for opacity of single trees
		public float LightExtinctionCoefficientOpacity { get; private set; }

		public float SnowmeltTemperature { get; private set; }

		// tau-value for delayed temperature calculation
		// Mäkelä A, Pulkkinen M, Kolari P, et al. 2008. Developing an empirical model of stand GPP with the LUE approach: analysis of eddy covariance 
		// data at five contrasting conifer sites in Europe. Global Change Biology 14(1):92-108. https://doi.org/10.1111/j.1365-2486.2007.01463.x
		public float TemperatureTau { get; private set; }

		public Ecosystem()
        {
			this.AirDensity = 1.2F;
			this.AutotrophicRespirationMultiplier = 0.47F;
			this.BoundaryLayerConductance = 0.2F;
			this.Epsilon = 1.8F; // max light use efficiency (aka alpha_c)
			this.InterceptionStorageBroadleaf = 2.0F;
			this.InterceptionStorageNeedle = 4.0F;
			this.LaiThresholdForClosedStands = 3.0F;
			this.LightExtinctionCoefficient = 0.5F;
			this.LightExtinctionCoefficientOpacity = 0.5F;
			this.SnowmeltTemperature = 0.0F;
			this.TemperatureTau = 5.0F;
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "ecosystem", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "autotrophicRespirationMultiplier", StringComparison.Ordinal))
			{
				this.AutotrophicRespirationMultiplier = reader.ReadElementContentAsFloat();
				if ((this.AutotrophicRespirationMultiplier < 0.0F) || (this.AutotrophicRespirationMultiplier > 1.0F))
				{
					throw new XmlException("Autotrophic respiration multiplier is negative or greater than 1.0.");
				}
			}
			else if (String.Equals(reader.Name, "epsilon", StringComparison.Ordinal))
			{
				this.Epsilon = reader.ReadElementContentAsFloat();
				if (this.Epsilon < 0.0F)
				{
					throw new XmlException("Epsilon is negative.");
				}
			}
			else if (String.Equals(reader.Name, "lightExtinctionCoefficient", StringComparison.Ordinal))
			{
				this.LightExtinctionCoefficient = reader.ReadElementContentAsFloat();
				if (this.LightExtinctionCoefficient < 0.0F)
				{
					throw new XmlException("Light extinction coefficient is negative.");
				}
			}
			else if (String.Equals(reader.Name, "lightExtinctionCoefficientOpacity", StringComparison.Ordinal))
			{
				this.LightExtinctionCoefficientOpacity = reader.ReadElementContentAsFloat();
				if (this.LightExtinctionCoefficientOpacity < 0.0F)
				{
					throw new XmlException("Light extinction opacity is negative.");
				}
			}
			else if (String.Equals(reader.Name, "temperatureTau", StringComparison.Ordinal))
			{
				this.TemperatureTau = reader.ReadElementContentAsFloat();
				if (this.TemperatureTau < 0.0F)
				{
					throw new XmlException("Temperature tau is negative.");
				}
			}
			else if (String.Equals(reader.Name, "airDensity", StringComparison.Ordinal))
			{
				this.AirDensity = reader.ReadElementContentAsFloat();
				if (this.AirDensity < 0.0F)
				{
					throw new XmlException("Air density is negative.");
				}
			}
			else if (String.Equals(reader.Name, "laiThresholdForClosedStands", StringComparison.Ordinal))
			{
				this.LaiThresholdForClosedStands = reader.ReadElementContentAsFloat();
				if (this.AirDensity < 0.0F)
				{
					throw new XmlException("Air density is negative.");
				}
			}
			else if (String.Equals(reader.Name, "boundaryLayerConductance", StringComparison.Ordinal))
			{
				this.BoundaryLayerConductance = reader.ReadElementContentAsFloat();
				if (this.BoundaryLayerConductance < 0.0F)
				{
					throw new XmlException("Boundary layer conductance is negative.");
				}
			}
			else if (String.Equals(reader.Name, "interceptionStorageNeedle", StringComparison.Ordinal))
			{
				this.InterceptionStorageNeedle = reader.ReadElementContentAsFloat();
				if (this.InterceptionStorageNeedle < 0.0F)
				{
					throw new XmlException("Needle interception storage is negative.");
				}
			}
			else if (String.Equals(reader.Name, "interceptionStorageBroadleaf", StringComparison.Ordinal))
			{
				this.InterceptionStorageBroadleaf = reader.ReadElementContentAsFloat();
				if (this.InterceptionStorageBroadleaf < 0.0F)
				{
					throw new XmlException("Broadleaf inteception storage is negative.");
				}
			}
			else if (String.Equals(reader.Name, "snowMeltTemperature", StringComparison.Ordinal))
			{
				this.SnowmeltTemperature = reader.ReadElementContentAsFloat();
				if (this.SnowmeltTemperature < -273.15F)
				{
					throw new XmlException("Snowmelt temperature is below absolute zero.");
				}
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
