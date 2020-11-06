using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Fire : Enablable
	{
		public bool OnlySimulation { get; private set; }
		public double KbdIref { get; private set; }
		public double FireSuppression { get; private set; }
		public double Land { get; private set; }
		// TODO: obtain from climate?
		public double MeanAnnualPrecipitation { get; private set; }
		public double AverageFireSize { get; private set; }
		public double FireSizeSigma { get; private set; }
		public int FireReturnInterval { get; private set; }
		public double FireExtinctionProbability { get; private set; }
		public double FuelKfc1 { get; private set; }
		public double FuelKfc2 { get; private set; }
		public double FuelKfc3 { get; private set; }
		public double CrownKill1 { get; private set; }
		public double CrownKill2 { get; private set; }
		public double CrownKillDbh { get; private set; }
		public double BurnSomFraction { get; private set; }
		public double BurnFoliageFraction { get; private set; }
		public double BurnBranchFraction { get; private set; }
		public double BurnStemFraction { get; private set; }
		public FireWind Wind { get; private set; }

		public Fire()
        {
			this.Wind = new FireWind();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("fire"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("enabled"))
			{
				this.Enabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("onlySimulation"))
			{
				this.OnlySimulation = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("KBDIref"))
			{
				this.KbdIref = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("rFireSuppression"))
			{
				this.FireSuppression = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("rLand"))
			{
				this.Land = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("meanAnnualPrecipitation"))
			{
				this.MeanAnnualPrecipitation = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("averageFireSize"))
			{
				this.AverageFireSize = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("fireSizeSigma"))
			{
				this.FireSizeSigma = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("fireReturnInterval"))
			{
				this.FireReturnInterval = reader.ReadElementContentAsInt();
			}
			else if (reader.IsStartElement("fireExtinctionProbability"))
			{
				this.FireExtinctionProbability = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("fuelKFC1"))
			{
				this.FuelKfc1 = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("fuelKFC2"))
			{
				this.FuelKfc2 = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("fuelKFC3"))
			{
				this.FuelKfc3 = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("crownKill1"))
			{
				this.CrownKill1 = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("crownKill2"))
			{
				this.CrownKill2 = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("crownKillDbh"))
			{
				this.CrownKillDbh = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("burnSOMFraction"))
			{
				this.BurnSomFraction = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("burnFoliageFraction"))
			{
				this.BurnFoliageFraction = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("burnBranchFraction"))
			{
				this.BurnBranchFraction = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("burnStemFraction"))
			{
				this.BurnStemFraction = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("wind"))
			{
				this.Wind.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
