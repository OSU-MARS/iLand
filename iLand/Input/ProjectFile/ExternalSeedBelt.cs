using System;
using System.Collections.Generic;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ExternalSeedBelt : Enablable
    {
        public int SectorsX { get; private set; }
        public int SectorsY { get; private set; }
        public int WidthInM { get; private set; }
		public List<ExternalSeedBeltSector> Sectors { get; private init; }

		public ExternalSeedBelt()
            : base("externalSeedBelt")
        {
			this.SectorsX = 0;
			this.SectorsY = 0;
            this.WidthInM = 10;

            this.Sectors = [];
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                if (String.Equals(reader.Name, "species", StringComparison.Ordinal))
                {
                    ExternalSeedBeltSector species = new();
                    species.ReadXml(reader);
                    this.Sectors.Add(species);
                }
                else
                {
                    this.ReadEnabled(reader);
                }
            }
            else
            {
                switch (reader.Name)
                {
                    case "width":
                        this.WidthInM = reader.ReadElementContentAsInt();
                        if (this.WidthInM < 0)
                        {
                            throw new XmlException("Seed belt width is negative.");
                        }
                        break;
                    case "sectorsX":
                        this.SectorsX = reader.ReadElementContentAsInt();
                        if (this.SectorsX < 0)
                        {
                            throw new XmlException("Seed belt size in x direction is negative.");
                        }
                        break;
                    case "sectorsY":
                        this.SectorsY = reader.ReadElementContentAsInt();
                        if (this.SectorsY < 0)
                        {
                            throw new XmlException("Seed belt size in y direction is negative.");
                        }
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
