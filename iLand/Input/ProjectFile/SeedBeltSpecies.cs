using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SeedBeltSpecies : XmlSerializable
    {
        // space separated list of four letter species codes
        public string IDs { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 2)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            this.X = Int32.Parse(reader.GetAttribute("x"));
            this.Y = Int32.Parse(reader.GetAttribute("y"));
            // no restriction on range of x or y values
            this.IDs = reader.ReadElementContentAsString().Trim();
        }
    }
}
