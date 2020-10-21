using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Snags
    {
		[XmlElement(ElementName = "swdC")]
		public float StandingWoodyDebrisCarbon { get; set; }

		[XmlElement(ElementName = "swdCN")]
		public float StandingWoodyDebrisCarbonNitrogenRatio { get; set; }

		[XmlElement(ElementName = "swdCount")]
		public float SnagsPerResourceUnit { get; set; }

		[XmlElement(ElementName = "otherC")]
		public float OtherCarbon { get; set; }

		[XmlElement(ElementName = "otherCN")]
		public float OtherCarbonNitrogenRatio { get; set; }

		[XmlElement(ElementName = "swdDecompRate")]
		public float StandingWoodyDebrisDecompositionRate { get; set; }

		[XmlElement(ElementName = "woodDecompRate")]
		public double WoodDecompositionRate { get; set; }

		[XmlElement(ElementName = "swdHalfLife")]
		public float StandingWoodyDebrisHalfLife { get; set; }

		public Snags()
        {
			this.OtherCarbon = 0.0F;
			this.OtherCarbonNitrogenRatio = 50.0F;
			this.SnagsPerResourceUnit = 0;
			this.StandingWoodyDebrisCarbon = 0.0F;
			this.StandingWoodyDebrisCarbonNitrogenRatio = 50.0F;
			this.StandingWoodyDebrisDecompositionRate = 0.0F;
			this.StandingWoodyDebrisHalfLife = 0.0F;
        }
    }
}
