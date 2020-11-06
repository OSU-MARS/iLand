using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class CO2Response : XmlSerializable
    {
        public float P0 { get; private set; }
        public float BaseConcentration { get; private set; }
        public float CompensationPoint { get; private set; }
        public float Beta0 { get; private set; }

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
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("CO2Response"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("p0"))
            {
                this.P0 = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("baseConcentration"))
            {
                this.BaseConcentration = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("compensationPoint"))
            {
                this.CompensationPoint = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("beta0"))
            {
                this.Beta0 = reader.ReadElementContentAsFloat();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
