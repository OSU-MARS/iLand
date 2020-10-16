using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Fire : Enablable
	{
		[XmlElement(ElementName = "onlySimulation")]
		public bool OnlySimulation { get; set; }

		[XmlElement(ElementName = "KBDIref")]
		public double KbdIref { get; set; }

		[XmlElement(ElementName = "rFireSuppression")]
		public double FireSuppression { get; set; }

		[XmlElement(ElementName = "rLand")]
		public double Land { get; set; }

		// TODO: obtian from climate?
		[XmlElement(ElementName = "meanAnnualPrecipitation")]
		public double MeanAnnualPrecipitation { get; set; }

		[XmlElement(ElementName = "averageFireSize")]
		public double AverageFireSize { get; set; }

		[XmlElement(ElementName = "fireSizeSigma")]
		public double FireSizeSigma { get; set; }

		[XmlElement(ElementName = "fireReturnInterval")]
		public int FireReturnInterval { get; set; }

		[XmlElement(ElementName = "fireExtinctionProbability")]
		public double FireExtinctionProbability { get; set; }

		[XmlElement(ElementName = "fuelKFC1")]
		public double FuelKfc1 { get; set; }
		[XmlElement(ElementName = "fuelKFC2")]
		public double FuelKfc2 { get; set; }
		[XmlElement(ElementName = "fuelKFC3")]
		public double FuelKfc3 { get; set; }

		[XmlElement(ElementName = "crownKill1")]
		public double CrownKill1 { get; set; }
		[XmlElement(ElementName = "crownKill2")]
		public double CrownKill2 { get; set; }
		[XmlElement(ElementName = "crownKillDbh")]
		public double CrownKillDbh { get; set; }

		[XmlElement(ElementName = "burnSOMFraction")]
		public double BurnSomFraction { get; set; }

		[XmlElement(ElementName = "burnFoliageFraction")]
		public double BurnFoliageFraction { get; set; }

		[XmlElement(ElementName = "burnBranchFraction")]
		public double BurnBranchFraction { get; set; }

		[XmlElement(ElementName = "burnStemFraction")]
		public double BurnStemFraction { get; set; }

		[XmlElement(ElementName = "wind")]
		public FireWind Wind { get; set; }
    }
}
