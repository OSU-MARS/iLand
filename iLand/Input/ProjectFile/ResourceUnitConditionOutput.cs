using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitConditionOutput : ConditionOutput
    {
        [XmlElement(ElementName = "conditionRU")]
        public string ConditionRU { get; set; }

        public ResourceUnitConditionOutput()
        {
            this.ConditionRU = null;
        }
    }
}
