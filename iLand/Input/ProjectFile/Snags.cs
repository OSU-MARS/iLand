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
            }
            else if (reader.IsStartElement("swdCN"))
            {
                this.StandingWoodyDebrisCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("swdCount"))
            {
                this.SnagsPerResourceUnit = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("otherC"))
            {
                this.OtherCarbon = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("otherCN"))
            {
                this.OtherCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("swdDecompRate"))
            {
                this.StandingWoodyDebrisDecompositionRate = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("woodDecompRate"))
            {
                this.WoodDecompositionRate = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("swdHalfLife"))
            {
                this.StandingWoodyDebrisHalfLife = reader.ReadElementContentAsFloat();
            }
			else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
