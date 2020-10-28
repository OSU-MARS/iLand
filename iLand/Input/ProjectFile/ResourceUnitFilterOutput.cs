using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitFilterOutput : ConditionOutput
    {
        [XmlElement(ElementName = "rufilter")]
        public string ResourceUnitFilter { get; set; }
    }
}
