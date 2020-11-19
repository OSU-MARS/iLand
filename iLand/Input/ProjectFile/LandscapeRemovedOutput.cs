using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LandscapeRemovedOutput : Enablable
    {
		public bool IncludeHarvest { get; private set; }
		public bool IncludeNatural { get; private set; }

		public LandscapeRemovedOutput()
            : base("landscapeRemoved")
        {
            this.IncludeHarvest = true;
            this.IncludeNatural = false;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
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
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
