using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Snags : XmlSerializable
    {
		public float StandingWoodyDebrisCarbon { get; private set; }
		public float StandingWoodyDebrisCarbonNitrogenRatio { get; private set; }
		public float SnagsPerResourceUnit { get; private set; }
		public float OtherCarbon { get; private set; }
		public float OtherCarbonNitrogenRatio { get; private set; }
		public float StandingWoodyDebrisDecompositionRate { get; private set; }
		public double WoodDecompositionRate { get; private set; }
		public float StandingWoodyDebrisHalfLife { get; private set; }

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

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("snags"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("swdC"))
            {
                this.StandingWoodyDebrisCarbon = reader.ReadElementContentAsFloat();
                if (this.StandingWoodyDebrisCarbon < 0.0F)
                {
                    throw new XmlException("Standing woody debris carbon is negative.");
                }
            }
            else if (reader.IsStartElement("swdCN"))
            {
                this.StandingWoodyDebrisCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.StandingWoodyDebrisCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Standing woody debris carbon:nitrogen ratio is negative.");
                }
            }
            else if (reader.IsStartElement("swdCount"))
            {
                this.SnagsPerResourceUnit = reader.ReadElementContentAsFloat();
                if (this.SnagsPerResourceUnit < 0.0F)
                {
                    throw new XmlException("Negative numner of snags per resource unit.");
                }
            }
            else if (reader.IsStartElement("otherC"))
            {
                this.OtherCarbon = reader.ReadElementContentAsFloat();
                if (this.OtherCarbon < 0.0F)
                {
                    throw new XmlException("Other carbon is negative.");
                }
            }
            else if (reader.IsStartElement("otherCN"))
            {
                this.OtherCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.OtherCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Other biomass carbon:nitrogen ratio is negative.");
                }
            }
            else if (reader.IsStartElement("swdDecompRate"))
            {
                this.StandingWoodyDebrisDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.StandingWoodyDebrisDecompositionRate < 0.0F)
                {
                    throw new XmlException("Standing woody debris decomposition rate is negative.");
                }
            }
            else if (reader.IsStartElement("woodDecompRate"))
            {
                this.WoodDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.WoodDecompositionRate < 0.0F)
                {
                    throw new XmlException("Wood decomposition rate is negative.");
                }
            }
            else if (reader.IsStartElement("swdHalfLife"))
            {
                this.StandingWoodyDebrisHalfLife = reader.ReadElementContentAsFloat();
                if (this.StandingWoodyDebrisHalfLife < 0.0F)
                {
                    throw new XmlException("Half life of standing woody debris is negative.");
                }
            }
            else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
