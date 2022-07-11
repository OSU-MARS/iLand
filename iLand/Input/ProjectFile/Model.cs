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
    }
}
