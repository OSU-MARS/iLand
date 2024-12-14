using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ConditionAnnualOutput : Enablable
    {
        public string? Condition { get; protected set; }
        public string? ResourceUnitFilter { get; protected set; }

        public ConditionAnnualOutput(string elementName)
            : base(elementName)
        {
            this.Condition = null;
            this.ResourceUnitFilter = null;
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
                    case "rufilter":
                        this.ResourceUnitFilter = reader.ReadElementContentAsString().Trim();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
