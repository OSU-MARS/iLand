using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class NitrogenResponseClasses
    {
        [XmlElement(ElementName = "class_1_a")]
        public float Class1A { get; set; }

        [XmlElement(ElementName = "class_1_b")]
        public float Class1B { get; set; }

        [XmlElement(ElementName = "class_2_a")]
        public float Class2A { get; set; }

        [XmlElement(ElementName = "class_2_b")]
        public float Class2B { get; set; }

        [XmlElement(ElementName = "class_3_a")]
        public float Class3A { get; set; }

        [XmlElement(ElementName = "class_3_b")]
        public float Class3B { get; set; }

        public NitrogenResponseClasses()
        {
            // no defaults in C++, must be set in project file
            this.Class1A = 0.0F;
            this.Class1B = 0.0F;
            this.Class2A = 0.0F;
            this.Class2B = 0.0F;
            this.Class3A = 0.0F;
            this.Class3B = 0.0F;
        }
    }
}
