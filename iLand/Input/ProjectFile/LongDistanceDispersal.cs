using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LongDistanceDispersal : XmlSerializable
    {
        public int Rings { get; private set; }
        public float MinimumSeedlingDensity { get; private set; }
        public float MaximumSeedlingDensity { get; private set; }
        public float SeedlingsPerCell { get; private set; }

        public LongDistanceDispersal()
        {
            this.MinimumSeedlingDensity = 0.0000001F;
            this.MaximumSeedlingDensity = 0.0001F;
            this.Rings = 4;
            this.SeedlingsPerCell = 0.0001F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "longDistanceDispersal", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "rings", StringComparison.Ordinal))
            {
                this.Rings = reader.ReadElementContentAsInt();
                if (this.Rings < 0)
                {
                    throw new XmlException("Number of long distance dispersal rings is negative."); // possibly more desirable to check for zero or negative
                }
            }
            else if (String.Equals(reader.Name, "minSeedlingDensity", StringComparison.Ordinal))
            {
                this.MinimumSeedlingDensity = reader.ReadElementContentAsFloat();
                if (this.MinimumSeedlingDensity < 0.0F)
                {
                    throw new XmlException("Long distance dispersal minimum seedling density is negative.");
                }
            }
            else if (String.Equals(reader.Name, "maxSeedlingDensity", StringComparison.Ordinal))
            {
                this.MaximumSeedlingDensity = reader.ReadElementContentAsFloat();
                if (this.MaximumSeedlingDensity < 0.0F)
                {
                    throw new XmlException("Long distance dispersal maximum seedling density is negative.");
                }
            }
            else if (String.Equals(reader.Name, "lddSeedlingsPerCell", StringComparison.Ordinal))
            {
                this.SeedlingsPerCell = reader.ReadElementContentAsFloat();
                // range of values is unclear from iLand documentation
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
