using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Model : XmlSerializable
    {
        public Ecosystem Ecosystem { get; private init; }
        public Management Management { get; private init; }
        public Microclimate Microclimate { get; private init; }
        public Permafrost Permafrost { get; private init; }
        public SeedDispersal SeedDispersal { get; private init; }
        public ModelSettings Settings { get; private init; }

        public Model()
        {
            this.Ecosystem = new();
            this.Management = new();
            this.Microclimate = new();
            this.Permafrost = new();
            this.SeedDispersal = new();
            this.Settings = new();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "model":
                    reader.Read();
                    break;
                case "ecosystem":
                    this.Ecosystem.ReadXml(reader);
                    break;
                case "management":
                    this.Management.ReadXml(reader);
                    break;
                case "microclimate":
                    this.Microclimate.ReadXml(reader);
                    break;
                case "permafrost":
                    this.Permafrost.ReadXml(reader);
                    break;
                case "seedDispersal":
                    this.SeedDispersal.ReadXml(reader);
                    break;
                case "settings":
                    this.Settings.ReadXml(reader);
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }

        public void Validate()
        {
            this.Permafrost.Validate();
        }
    }
}
