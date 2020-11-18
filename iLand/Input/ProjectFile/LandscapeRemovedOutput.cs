using System;
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

            if (String.Equals(reader.Name, "landscapeRemoved", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "includeHarvest", StringComparison.Ordinal))
            {
                this.IncludeHarvest = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "includeNatural", StringComparison.Ordinal))
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
