using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Site
	{
		[XmlElement(ElementName = "availableNitrogen")]
		public double AvailableNitrogen { get; set; }

		[XmlElement(ElementName = "soilDepth")]
		public double SoilDepth { get; set; }

		[XmlElement(ElementName = "pctSand")]
		public double PercentSand { get; set; }

		[XmlElement(ElementName = "pctSilt")]
		public double PercentSilt { get; set; }

		[XmlElement(ElementName = "pctClay")]
		public double PercentClay { get; set; }

		[XmlElement(ElementName = "youngLabileC")]
		public double YoungLabileCarbon { get; set; }

		[XmlElement(ElementName = "youngLabileN")]
		public double YoungLabileNitrogen { get; set; }

		[XmlElement(ElementName = "youngLabileDecompRate")]
		public double YoungLabileDecompRate { get; set; }

		[XmlElement(ElementName = "youngRefractoryC")]
		public double YoungRefractoryCarbon { get; set; }

		[XmlElement(ElementName = "youngRefractoryN")]
		public double YoungRefractoryNitrogen { get; set; }

		[XmlElement(ElementName = "youngRefractoryDecompRate")]
		public float YoungRefractoryDecompositionRate { get; set; }

		[XmlElement(ElementName = "somC")]
		public double SoilOrganicMatterCarbon { get; set; }

		[XmlElement(ElementName = "somN")]
		public double SoilOrganicMatterNitrogen { get; set; }

		[XmlElement(ElementName = "somDecompRate")]
		public double SoilOrganicMatterDecompositionRate { get; set; }

		[XmlElement(ElementName = "soilHumificationRate")]
		public double SoilHumificationRate { get; set; }

		public Site()
        {
			this.YoungRefractoryDecompositionRate = -1.0F;
        }
    }
}
