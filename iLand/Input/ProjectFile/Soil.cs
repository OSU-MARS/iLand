using System.Drawing;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Soil
    {
        [XmlElement(ElementName = "el")]
        public double El { get; set; }

        [XmlElement(ElementName = "er")]
        public double Er { get; set; }

        [XmlElement(ElementName = "qb")]
        public double Qb { get; set; }

        [XmlElement(ElementName = "qh")]
        public double Qh { get; set; }

        [XmlElement(ElementName = "swdDBHClass12")]
        public double SwdDbhClass12 { get; set; }

        [XmlElement(ElementName = "swdDBHClass23")]
        public double SwdDdhClass23 { get; set; }

        [XmlElement(ElementName = "useDynamicAvailableNitrogen")]
        public bool UseDynamicAvailableNitrogen { get; set; }

        [XmlElement(ElementName = "leaching")]
        public double Leaching { get; set; }

        [XmlElement(ElementName = "nitrogenDeposition")]
        public double NitrogenDeposition { get; set; }

        public Soil()
        {
            this.Leaching = 0.15;
            this.NitrogenDeposition = 0.0;
            this.Qb = 5.0;
            this.UseDynamicAvailableNitrogen = false;
        }
    }
}
