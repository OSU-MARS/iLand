using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LandscapeRemovedOutput : Enablable
    {
		public bool IncludeHarvest { get; private set; }
		public bool IncludeNatural { get; private set; }

		public LandscapeRemovedOutput()
        {
            this.IncludeHarvest = true;
            this.IncludeNatural = false;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("landscape_removed"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("includeHarvest"))
            {
                this.IncludeHarvest = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("includeNatural"))
            {
                this.IncludeNatural = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
