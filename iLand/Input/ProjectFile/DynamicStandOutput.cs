using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DynamicStandOutput : DynamicOutput
    {
        public bool BySpecies { get; private set; }
        public bool ByResourceUnit { get; private set; }

        public DynamicStandOutput()
        {
            this.ByResourceUnit = true;
            this.BySpecies = true;
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

            if (reader.IsStartElement("dynamicstand"))
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
            else if (reader.IsStartElement("by_species"))
            {
                this.BySpecies = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("by_ru"))
            {
                this.ByResourceUnit = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
