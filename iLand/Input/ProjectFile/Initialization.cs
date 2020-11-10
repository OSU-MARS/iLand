using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Initialization : XmlSerializable
    {
		public string? MapFileName { get; private set; }
		public string? Mode { get; private set; }
		public string? Type { get; private set; }
		public string? RandomFunction { get; private set; }
		public string? File { get; private set; }
		public string? SaplingFile { get; private set; }
		public Snags Snags { get; private set; }
		public HeightGrid HeightGrid { get; private set; }

		public Initialization()
        {
			this.File = null;
			this.MapFileName = "init";
			this.Mode = "copy";
			this.RandomFunction = "1-x^2";
			this.SaplingFile = null;
			this.Type = null;

			this.HeightGrid = new HeightGrid();
			this.Snags = new Snags();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("initialization"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("mapFileName"))
            {
                this.MapFileName = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("mode"))
            {
                this.Mode = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("type"))
            {
                this.Type = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("randomFunction"))
            {
                this.RandomFunction = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("file"))
            {
                this.File = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("saplingFile"))
            {
                this.SaplingFile = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("snags"))
            {
                this.Snags.ReadXml(reader);
            }
            else if (reader.IsStartElement("heightGrid"))
            {
                this.HeightGrid.ReadXml(reader);
            }
			else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
