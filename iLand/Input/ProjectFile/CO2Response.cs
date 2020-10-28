using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class CO2Response
    {
        [XmlElement(ElementName = "p0")]
        public float P0 { get; set; }

        [XmlElement(ElementName = "baseConcentration")]
        public float BaseConcentration { get; set; }

        [XmlElement(ElementName = "compensationPoint")]
        public float CompensationPoint { get; set; }

        [XmlElement(ElementName = "beta0")]
        public float Beta0 { get; set; }

        public CO2Response()
        {
            // must be set in project file
            this.BaseConcentration = 0.0F;
            this.Beta0 = 0.0F;
            this.CompensationPoint = 0.0F;
            this.P0 = 0.0F;
        }
    }
}
