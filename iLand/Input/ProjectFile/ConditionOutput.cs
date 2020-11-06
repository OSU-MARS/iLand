using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ConditionOutput : Enablable
    {
        public string Condition { get; protected set; }

        public ConditionOutput()
        {
            this.Condition = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("landscape") ||
                reader.IsStartElement("sapling") ||
                reader.IsStartElement("stand"))
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
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
