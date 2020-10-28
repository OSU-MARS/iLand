using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class ConditionOutput : Enablable
    {
        [XmlElement(ElementName = "condition")]
        public string Condition { get; set; }

        public ConditionOutput()
        {
            this.Condition = null;
        }
    }
}
