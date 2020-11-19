using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class WorldInitialization : XmlSerializable
    {
        public string? MapFileName { get; private set; }
		public string? RandomFunction { get; private set; }
		public string? SaplingFile { get; private set; }
        public string? TreeFile { get; private set; }
        public string? TreeFileFormat { get; private set; }
        public ResourceUnitTreeInitializationMethod TreeInitializationMethod { get; private set; }

        public InitialHeightGrid HeightGrid { get; private init; }
        public IntitialSnags Snags { get; private init; }

        public WorldInitialization()
        {
			this.TreeFile = null;
			this.MapFileName = "init";
			this.TreeInitializationMethod = ResourceUnitTreeInitializationMethod.Unit;
			this.RandomFunction = "1-x^2";
			this.SaplingFile = null;
			this.TreeFileFormat = null;

			this.HeightGrid = new InitialHeightGrid();
			this.Snags = new IntitialSnags();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "initialization", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "mapFileName", StringComparison.Ordinal))
            {
                this.MapFileName = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "treeInitializationMethod", StringComparison.Ordinal))
            {
                this.TreeInitializationMethod = Enum.Parse< ResourceUnitTreeInitializationMethod>(reader.ReadElementContentAsString(), ignoreCase: true);
            }
            else if (String.Equals(reader.Name, "treeFileFormat", StringComparison.Ordinal))
            {
                this.TreeFileFormat = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "randomFunction", StringComparison.Ordinal))
            {
                this.RandomFunction = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "treeFile", StringComparison.Ordinal))
            {
                this.TreeFile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "saplingFile", StringComparison.Ordinal))
            {
                this.SaplingFile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "snags", StringComparison.Ordinal))
            {
                this.Snags.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "heightGrid", StringComparison.Ordinal))
            {
                this.HeightGrid.ReadXml(reader);
            }
			else
			{
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
