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
            else
            {
                switch (reader.Name)
                {
                    case "includeHarvest":
                        this.IncludeHarvest = reader.ReadElementContentAsBoolean();
                        break;
                    case "includeNatural":
                        this.IncludeNatural = reader.ReadElementContentAsBoolean();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
