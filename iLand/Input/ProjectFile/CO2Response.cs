using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class CO2Response : XmlSerializable
    {
        public float BaseConcentration { get; private set; }
        public float Beta0 { get; private set; }
        public float CompensationPoint { get; private set; }
        public float P0 { get; private set; }

        public CO2Response()
        {
            // must be set in project file
            this.BaseConcentration = 0.0F;
            this.Beta0 = 0.0F;
            this.CompensationPoint = 0.0F;
            this.P0 = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "co2response":
                    reader.Read();
                    break;
                case "p0":
                    this.P0 = reader.ReadElementContentAsFloat();
                    break;
                case "baseConcentration":
                    this.BaseConcentration = reader.ReadElementContentAsFloat();
                    if ((this.BaseConcentration < 0.0F) || (this.BaseConcentration > 1E6F))
                    {
                        throw new XmlException("Base CO₂ concentration is negative or greater than 100%.");
                    }
                    break;
                case "compensationPoint":
                    this.CompensationPoint = reader.ReadElementContentAsFloat();
                    if ((this.CompensationPoint < 0.0F) || (this.CompensationPoint > 1E6F))
                    {
                        throw new XmlException("CO₂ compensation point is negative or greater than 100%.");
                    }
                    break;
                case "beta0":
                    this.Beta0 = reader.ReadElementContentAsFloat();
                    if (this.Beta0 < 0.0F)
                    {
                        throw new XmlException("Productivity increase when CO₂ concentration doubles (beta0) is negative.");
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
