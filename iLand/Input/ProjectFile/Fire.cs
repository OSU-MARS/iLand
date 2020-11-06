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
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("rFireSuppression"))
			{
				this.FireSuppression = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("rLand"))
			{
				this.Land = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("meanAnnualPrecipitation"))
			{
				this.MeanAnnualPrecipitation = reader.ReadElementContentAsDouble();
				if (this.MeanAnnualPrecipitation < 0.0)
                {
					throw new XmlException("Mean annual precipitation is negative.");
                }
			}
			else if (reader.IsStartElement("averageFireSize"))
			{
				this.AverageFireSize = reader.ReadElementContentAsDouble();
				if (this.AverageFireSize < 0.0)
				{
					throw new XmlException("Average fire size is negative.");
				}
			}
			else if (reader.IsStartElement("fireSizeSigma"))
			{
				this.FireSizeSigma = reader.ReadElementContentAsDouble();
				if (this.FireSizeSigma < 0.0)
				{
					throw new XmlException("Fire size standard deviation is negative.");
				}
			}
			else if (reader.IsStartElement("fireReturnInterval"))
			{
				this.FireReturnInterval = reader.ReadElementContentAsInt();
				if (this.FireReturnInterval < 0)
				{
					throw new XmlException("Fire return interval is negative.");
				}
			}
			else if (reader.IsStartElement("fireExtinctionProbability"))
			{
				this.FireExtinctionProbability = reader.ReadElementContentAsDouble();
				if ((this.FireExtinctionProbability < 0.0) || (this.FireExtinctionProbability > 1.0))
				{
					throw new XmlException("Fire extinction probability is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("fuelKFC1"))
			{
				this.FuelKfc1 = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("fuelKFC2"))
			{
				this.FuelKfc2 = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("fuelKFC3"))
			{
				this.FuelKfc3 = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("crownKill1"))
			{
				this.CrownKill1 = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("crownKill2"))
			{
				this.CrownKill2 = reader.ReadElementContentAsDouble();
				// valid range unclear from iLand documentation
			}
			else if (reader.IsStartElement("crownKillDbh"))
			{
				this.CrownKillDbh = reader.ReadElementContentAsDouble();
				if (this.CrownKillDbh < 0.0)
				{
					throw new XmlException("Crown kill DBH is negative.");
				}
			}
			else if (reader.IsStartElement("burnSOMFraction"))
			{
				this.BurnSomFraction = reader.ReadElementContentAsDouble();
				if ((this.BurnSomFraction < 0.0) || (this.BurnSomFraction > 1.0))
				{
					throw new XmlException("Burned soil organic matter fraction is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("burnFoliageFraction"))
			{
				this.BurnFoliageFraction = reader.ReadElementContentAsDouble();
				if ((this.BurnFoliageFraction < 0.0) || (this.BurnFoliageFraction > 1.0))
				{
					throw new XmlException("Burned foliage fraction is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("burnBranchFraction"))
			{
				this.BurnBranchFraction = reader.ReadElementContentAsDouble();
				if ((this.BurnBranchFraction < 0.0) || (this.BurnBranchFraction > 1.0))
				{
					throw new XmlException("Burned stem fraction is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("burnStemFraction"))
			{
				this.BurnStemFraction = reader.ReadElementContentAsDouble();
				if ((this.BurnStemFraction < 0.0) || (this.BurnStemFraction > 1.0))
				{
					throw new XmlException("Burned branch fraction is negative or greater than 1.0.");
				}
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
