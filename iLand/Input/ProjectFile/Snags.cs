using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Snags
    {
		[XmlElement(ElementName = "swdC")]
		public double StandingWoodyDebrisCarbon { get; set; }

		[XmlElement(ElementName = "swdCN")]
		public double StandingWoodyDebrisCarbonNitrogenRatio { get; set; }

		[XmlElement(ElementName = "swdCount")]
		public double SnagsPerResourceUnit { get; set; }

		[XmlElement(ElementName = "otherC")]
		public double OtherCarbon { get; set; }

		[XmlElement(ElementName = "otherCN")]
		public double OtherCarbonNitrogenRatio { get; set; }

		[XmlElement(ElementName = "swdDecompRate")]
		public double StandingWoodyDebrisDecompositionRate { get; set; }

		[XmlElement(ElementName = "woodDecompRate")]
		public double WoodDecompositionRate { get; set; }

		[XmlElement(ElementName = "swdHalfLife")]
		public double StandingWoodyDebrisHalfLife { get; set; }

		public Snags()
        {
			this.OtherCarbon = 0.0;
			this.OtherCarbonNitrogenRatio = 50.0;
			this.SnagsPerResourceUnit = 0;
			this.StandingWoodyDebrisCarbon = 0.0;
			this.StandingWoodyDebrisCarbonNitrogenRatio = 50.0;
			this.StandingWoodyDebrisDecompositionRate = 0.0;
			this.StandingWoodyDebrisHalfLife = 0.0;
        }
    }
}
