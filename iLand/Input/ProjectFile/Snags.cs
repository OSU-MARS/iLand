using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Snags : XmlSerializable
    {
		public float StandingCarbon { get; private set; }
		public float StandingCarbonNitrogenRatio { get; private set; }
		public float SnagsPerResourceUnit { get; private set; }
		public float OtherCarbon { get; private set; }
		public float OtherCarbonNitrogenRatio { get; private set; }
		public float StandingDecompositionRate { get; private set; }
		public float WoodDecompositionRate { get; private set; }
		public float SnagHalfLife { get; private set; }

		public Snags()
        {
			this.OtherCarbon = 0.0F;
			this.OtherCarbonNitrogenRatio = 50.0F;
            this.SnagHalfLife = 0.0F;
            this.SnagsPerResourceUnit = 0;
			this.StandingCarbon = 0.0F;
			this.StandingCarbonNitrogenRatio = 50.0F;
			this.StandingDecompositionRate = 0.0F;
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
                this.StandingCarbon = reader.ReadElementContentAsFloat();
                if (this.StandingCarbon < 0.0F)
                {
                    throw new XmlException("Standing woody debris carbon is negative.");
                }
            }
            else if (reader.IsStartElement("swdCN"))
            {
                this.StandingCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.StandingCarbonNitrogenRatio < 0.0F)
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
                this.StandingDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.StandingDecompositionRate < 0.0F)
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
                this.SnagHalfLife = reader.ReadElementContentAsFloat();
                if (this.SnagHalfLife < 0.0F)
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
