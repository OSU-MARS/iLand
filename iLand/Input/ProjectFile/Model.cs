using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Model : XmlSerializable
    {
        public Ecosystem Ecosystem { get; private init; }
        public Management Management { get; private init; }
        public SeedDispersal SeedDispersal { get; private init; }
        public ModelSettings Settings { get; private init; }

        public Model()
        {
            this.Ecosystem = new Ecosystem();
            this.Management = new Management();
            this.SeedDispersal = new SeedDispersal();
            this.Settings = new ModelSettings();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "model", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "ecosystem", StringComparison.Ordinal))
            {
                this.Ecosystem.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "management", StringComparison.Ordinal))
            {
                this.Management.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "seedDispersal", StringComparison.Ordinal))
            {
                this.SeedDispersal.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "settings", StringComparison.Ordinal))
            {
                this.Settings.ReadXml(reader);
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
