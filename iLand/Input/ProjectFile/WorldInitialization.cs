using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class WorldInitialization : XmlSerializable
    {
        public string? ResourceUnitFile { get; private set; }
        public string? SaplingsByStandFile { get; private set; }
        public string? StandRasterFile { get; private set; }
        public string? TreeFile { get; private set; }
        public TreeInitializationMethod Trees { get; private set; }
        public string? TreeSizeDistribution { get; private set; }

        public InitialHeightGrid HeightGrid { get; private init; }
        public IntitialSnags Snags { get; private init; }

        public WorldInitialization()
        {
            this.HeightGrid = new InitialHeightGrid();
            this.ResourceUnitFile = null;
            this.Snags = new IntitialSnags();
            this.SaplingsByStandFile = null;
            this.StandRasterFile = null;
            this.TreeFile = "init";
            this.Trees = TreeInitializationMethod.SingleFile;
            this.TreeSizeDistribution = "1-x^2";
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "heightGrid":
                    this.HeightGrid.ReadXml(reader);
                    break;
                case "initialization":
                    reader.Read();
                    break;
                case "resourceUnitFile":
                    this.ResourceUnitFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "saplingsByStandFile":
                    this.SaplingsByStandFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "snags":
                    this.Snags.ReadXml(reader);
                    break;
                case "standRasterFile":
                    this.StandRasterFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "treeFile":
                    this.TreeFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "trees":
                    this.Trees = Enum.Parse<TreeInitializationMethod>(reader.ReadElementContentAsString(), ignoreCase: true);
                    break;
                case "treeSizeDistribution":
                    this.TreeSizeDistribution = reader.ReadElementContentAsString().Trim();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
