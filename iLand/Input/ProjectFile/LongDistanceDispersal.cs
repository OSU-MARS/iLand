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
            }
            else if (reader.IsStartElement("thresholdArea"))
            {
                this.ThresholdArea = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("thresholdLDD"))
            {
                this.ThresholdLdd = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("LDDSeedlings"))
            {
                this.LddSeedlings = reader.ReadElementContentAsFloat();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
