using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class StandGrid : Enablable
    {
        public string? FileName { get; private set; }

        public StandGrid()
        {
            this.FileName = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("standGrid"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("fileName"))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
