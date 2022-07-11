using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Fire : Enablable
	{
		public bool OnlySimulation { get; private set; }
		public float KbdIref { get; private set; }
		public float FireSuppression { get; private set; }
		public float Land { get; private set; }
		// TODO: obtain from weather?
		public float MeanAnnualPrecipitation { get; private set; }
		public float AverageFireSize { get; private set; }
		public float FireSizeSigma { get; private set; }
		public int FireReturnInterval { get; private set; }
		public float FireExtinctionProbability { get; private set; }
		public float FuelKfc1 { get; private set; }
		public float FuelKfc2 { get; private set; }
		public float FuelKfc3 { get; private set; }
		public float CrownKill1 { get; private set; }
		public float CrownKill2 { get; private set; }
		public float CrownKillDbh { get; private set; }
		public float BurnSomFraction { get; private set; }
		public float BurnFoliageFraction { get; private set; }
		public float BurnBranchFraction { get; private set; }
		public float BurnStemFraction { get; private set; }
		public FireWind Wind { get; private init; }

		public Fire()
			: base("fire")
        {
			this.Wind = new FireWind();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				this.ReadEnabled(reader);
			}
			else
			{
				switch (reader.Name)
				{
					case "onlySimulation":
						this.OnlySimulation = reader.ReadElementContentAsBoolean();
						break;
					case "KBDIref":
						this.KbdIref = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "rFireSuppression":
						this.FireSuppression = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "rLand":
						this.Land = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "meanAnnualPrecipitation":
						this.MeanAnnualPrecipitation = reader.ReadElementContentAsFloat();
						if (this.MeanAnnualPrecipitation < 0.0F)
						{
							throw new XmlException("Mean annual precipitation is negative.");
						}
						break;
					case "averageFireSize":
						this.AverageFireSize = reader.ReadElementContentAsFloat();
						if (this.AverageFireSize < 0.0F)
						{
							throw new XmlException("Average fire size is negative.");
						}
						break;
					case "fireSizeSigma":
						this.FireSizeSigma = reader.ReadElementContentAsFloat();
						if (this.FireSizeSigma < 0.0F)
						{
							throw new XmlException("Fire size standard deviation is negative.");
						}
						break;
					case "fireReturnInterval":
						this.FireReturnInterval = reader.ReadElementContentAsInt();
						if (this.FireReturnInterval < 0)
						{
							throw new XmlException("Fire return interval is negative.");
						}
						break;
					case "fireExtinctionProbability":
						this.FireExtinctionProbability = reader.ReadElementContentAsFloat();
						if ((this.FireExtinctionProbability < 0.0F) || (this.FireExtinctionProbability > 1.0F))
						{
							throw new XmlException("Fire extinction probability is negative or greater than 1.0.");
						}
						break;
					case "fuelKFC1":
						this.FuelKfc1 = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "fuelKFC2":
						this.FuelKfc2 = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "fuelKFC3":
						this.FuelKfc3 = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "crownKill1":
						this.CrownKill1 = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "crownKill2":
						this.CrownKill2 = reader.ReadElementContentAsFloat();
						// valid range unclear from iLand documentation
						break;
					case "crownKillDbh":
						this.CrownKillDbh = reader.ReadElementContentAsFloat();
						if (this.CrownKillDbh < 0.0F)
						{
							throw new XmlException("Crown kill DBH is negative.");
						}
						break;
					case "burnSOMFraction":
						this.BurnSomFraction = reader.ReadElementContentAsFloat();
						if ((this.BurnSomFraction < 0.0F) || (this.BurnSomFraction > 1.0F))
						{
							throw new XmlException("Burned soil organic matter fraction is negative or greater than 1.0.");
						}
						break;
					case "burnFoliageFraction":
						this.BurnFoliageFraction = reader.ReadElementContentAsFloat();
						if ((this.BurnFoliageFraction < 0.0F) || (this.BurnFoliageFraction > 1.0F))
						{
							throw new XmlException("Burned foliage fraction is negative or greater than 1.0.");
						}
						break;
					case "burnBranchFraction":
						this.BurnBranchFraction = reader.ReadElementContentAsFloat();
						if ((this.BurnBranchFraction < 0.0F) || (this.BurnBranchFraction > 1.0F))
						{
							throw new XmlException("Burned stem fraction is negative or greater than 1.0.");
						}
						break;
					case "burnStemFraction":
						this.BurnStemFraction = reader.ReadElementContentAsFloat();
						if ((this.BurnStemFraction < 0.0F) || (this.BurnStemFraction > 1.0))
						{
							throw new XmlException("Burned branch fraction is negative or greater than 1.0.");
						}
						break;
					case "wind":
						this.Wind.ReadXml(reader);
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
