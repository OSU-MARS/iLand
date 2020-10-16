using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class LongDistanceDispersal
    {
        [XmlElement(ElementName = "rings")]
        public int Rings { get; set; }

        [XmlElement(ElementName = "thresholdArea")]
        public double ThresholdArea { get; set; }

        [XmlElement(ElementName = "thresholdLDD")]
        public double ThresholdLdd { get; set; }

        [XmlElement(ElementName = "LDDSeedlings")]
        public float LddSeedlings { get; set; }

        public LongDistanceDispersal()
        {
            this.LddSeedlings = 0.0001F;
            this.ThresholdArea = 0.0001;
            this.ThresholdLdd = 0.0001;
            this.Rings = 4;
        }
    }
}
