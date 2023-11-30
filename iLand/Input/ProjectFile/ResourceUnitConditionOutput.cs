using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitConditionOutput : ConditionAnnualOutput
    {
        public string? ConditionRU { get; private set; }

        public ResourceUnitConditionOutput(string elementName)
            : base(elementName)
        {
            this.ConditionRU = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else
            {
                switch (reader.Name)
                {
                    case "condition":
                        this.Condition = reader.ReadElementContentAsString().Trim();
                        break;
                    case "conditionRU":
                        this.ConditionRU = reader.ReadElementContentAsString().Trim();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
