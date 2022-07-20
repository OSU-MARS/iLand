using System;
using System.Globalization;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ExternalSeedBeltSector : XmlSerializable
    {
        // space separated list of four letter species codes
        public string? SpeciesIDs { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 2)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            string? xAsString = reader.GetAttribute("x");
            if (String.IsNullOrWhiteSpace(xAsString))
            {
                throw new XmlException("x attribute of seed belt species is empty.");
            }
            string? yAsString = reader.GetAttribute("y");
            if (String.IsNullOrWhiteSpace(yAsString))
            {
                throw new XmlException("y attribute of seed belt species is empty.");
            }

            this.X = Int32.Parse(xAsString, CultureInfo.InvariantCulture);
            this.Y = Int32.Parse(yAsString, CultureInfo.InvariantCulture);
            // no restriction on range of x or y values
            this.SpeciesIDs = reader.ReadElementContentAsString().Trim();
        }
    }
}
