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
		public List<ExternalSeedBeltSector> Sectors { get; init; }

		public ExternalSeedBelt()
        {
			this.SectorsX = 0;
			this.SectorsY = 0;
            this.WidthInM = 10;

            this.Sectors = new List<ExternalSeedBeltSector>();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                if (String.Equals(reader.Name, "species", StringComparison.Ordinal))
                {
                    ExternalSeedBeltSector species = new ExternalSeedBeltSector();
                    species.ReadXml(reader);
                    this.Sectors.Add(species);
                }
                else
                {
                    throw new XmlException("Encountered unexpected attributes.");
                }
            }
            else if (String.Equals(reader.Name, "externalSeedBelt", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "width", StringComparison.Ordinal))
            {
                this.WidthInM = reader.ReadElementContentAsInt();
                if (this.WidthInM < 0)
                {
                    throw new XmlException("Seed belt width is negative.");
                }
            }
            else if (String.Equals(reader.Name, "sectorsX", StringComparison.Ordinal))
            {
                this.SectorsX = reader.ReadElementContentAsInt();
                if (this.SectorsX < 0)
                {
                    throw new XmlException("Seed belt size in x direction is negative.");
                }
            }
            else if (String.Equals(reader.Name, "sectorsY", StringComparison.Ordinal))
            {
                this.SectorsY = reader.ReadElementContentAsInt();
                if (this.SectorsY < 0)
                {
                    throw new XmlException("Seed belt size in y direction is negative.");
                }
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
