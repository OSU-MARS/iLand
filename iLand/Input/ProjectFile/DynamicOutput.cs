using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DynamicOutput : ConditionOutput
    {
        public string Columns { get; protected set; }
        public string ResourceUnitFilter { get; protected set; }
        public string TreeFilter { get; protected set; }

        public DynamicOutput()
        {
            this.Columns = null;
            this.ResourceUnitFilter = null;
            this.TreeFilter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("dynamic"))
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
            else if (reader.IsStartElement("rufilter"))
            {
                this.ResourceUnitFilter = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("columns"))
            {
                this.Columns = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("treefilter"))
            {
                this.TreeFilter = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
