using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Soil
    {
        [XmlElement(ElementName = "el")]
        public float El { get; set; }

        [XmlElement(ElementName = "er")]
        public float Er { get; set; }

        [XmlElement(ElementName = "qb")]
        public float Qb { get; set; }

        [XmlElement(ElementName = "qh")]
        public float Qh { get; set; }

        [XmlElement(ElementName = "swdDBHClass12")]
        public float SwdDbhClass12 { get; set; }

        [XmlElement(ElementName = "swdDBHClass23")]
        public float SwdDdhClass23 { get; set; }

        [XmlElement(ElementName = "useDynamicAvailableNitrogen")]
        public bool UseDynamicAvailableNitrogen { get; set; }

        [XmlElement(ElementName = "leaching")]
        public float Leaching { get; set; }

        [XmlElement(ElementName = "nitrogenDeposition")]
        public float NitrogenDeposition { get; set; }

        public Soil()
        {
            this.Leaching = 0.15F;
            this.NitrogenDeposition = 0.0F;
            this.Qb = 5.0F;
            this.UseDynamicAvailableNitrogen = false;
        }
    }
}
