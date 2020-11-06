using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class FilterOutput : Enablable
    {
        public string Filter { get; private set; }

        public FilterOutput()
        {
            this.Filter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("tree") ||
                reader.IsStartElement("treeRemoved"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("filter"))
            {
                this.Filter = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
