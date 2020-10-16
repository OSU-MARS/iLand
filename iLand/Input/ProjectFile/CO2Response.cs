using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class CO2Response
    {
        [XmlElement(ElementName = "p0")]
        public double P0 { get; set; }

        [XmlElement(ElementName = "baseConcentration")]
        public double BaseConcentration { get; set; }

        [XmlElement(ElementName = "compensationPoint")]
        public double CompensationPoint { get; set; }

        [XmlElement(ElementName = "beta0")]
        public double Beta0 { get; set; }

        public CO2Response()
        {
            // must be set in project file
            this.BaseConcentration = 0.0;
            this.Beta0 = 0.0;
            this.CompensationPoint = 0.0;
            this.P0 = 0.0;
        }
    }
}
