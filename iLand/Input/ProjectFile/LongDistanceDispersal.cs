using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LongDistanceDispersal : XmlSerializable
    {
        public int Rings { get; private set; }
        public float ThresholdArea { get; private set; }
        public float ThresholdLdd { get; private set; }
        public float LddSeedlings { get; private set; }

        public LongDistanceDispersal()
        {
            this.LddSeedlings = 0.0001F;
            this.ThresholdArea = 0.0001F;
            this.ThresholdLdd = 0.0001F;
            this.Rings = 4;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("longDistanceDispersal"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("rings"))
            {
                this.Rings = reader.ReadElementContentAsInt();
                if (this.Rings < 0)
                {
                    throw new XmlException("Number of long distance dispersal rings is negative."); // possibly more desirable to check for zero or negative
                }
            }
            else if (reader.IsStartElement("thresholdArea"))
            {
                this.ThresholdArea = reader.ReadElementContentAsFloat();
                if (this.ThresholdArea < 0.0F)
                {
                    throw new XmlException("Long distance dispersal threshold area is negative.");
                }
            }
            else if (reader.IsStartElement("thresholdLDD"))
            {
                this.ThresholdLdd = reader.ReadElementContentAsFloat();
                if (this.ThresholdLdd < 0.0F)
                {
                    throw new XmlException("Long distance dispersal threshold is negative.");
                }
            }
            else if (reader.IsStartElement("LDDSeedlings"))
            {
                this.LddSeedlings = reader.ReadElementContentAsFloat();
                // range of values is unclear from iLand documentation
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
