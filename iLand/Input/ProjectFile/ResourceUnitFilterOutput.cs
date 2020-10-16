using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    // BUGBUG: unused in C++
    public class ResourceUnitFilterOutput : ConditionOutput
    {
        [XmlElement(ElementName = "rufilter")]
        public string ResourceUnitFilter { get; set; }
    }
}
