using System.Xml.Serialization;

namespace iLand.World
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
