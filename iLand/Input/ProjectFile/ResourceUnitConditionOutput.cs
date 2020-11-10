using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitConditionOutput : ConditionOutput
    {
        public string? ConditionRU { get; private set; }

        public ResourceUnitConditionOutput()
        {
            this.ConditionRU = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("carbon") ||
                reader.IsStartElement("carbonFlow") ||
                reader.IsStartElement("water"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("condition"))
            {
                this.Condition = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("conditionRU"))
            {
                this.ConditionRU = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
