using System.Xml.Serialization;

namespace iLand.Simulation
{
    public enum GrassAlgorithmType
    {
        Invalid,
        [XmlEnum(Name = "continuous")]
        Continuous,
        [XmlEnum(Name = "pixel")]
        Pixel
    }
}
