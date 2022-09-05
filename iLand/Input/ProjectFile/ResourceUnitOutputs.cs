using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitOutputs : XmlSerializable
    {
        public Enablable AllTreeSpeciesStatistics { get; private init; }
        public Enablable IndividualTrees { get; private init; }
        public Enablable IndividualTreeSpeciesStatistics { get; private init; }
        public Enablable ThreePG { get; private init; }

        public ResourceUnitOutputs()
        {
            this.AllTreeSpeciesStatistics = new("allTreeSpeciesStatistics");
            this.IndividualTrees = new("individualTrees");
            this.IndividualTreeSpeciesStatistics = new("individualTreeSpeciesStatistics");
            this.ThreePG = new("threePG");
        }

        public bool IsAnyOutputEnabled()
        {
            return this.AllTreeSpeciesStatistics.Enabled || this.IndividualTrees.Enabled || this.IndividualTreeSpeciesStatistics.Enabled || this.ThreePG.Enabled;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                switch (reader.Name)
                {
                    case "allTreeSpeciesStatistics":
                        this.AllTreeSpeciesStatistics.ReadXml(reader);
                        break;
                    case "individualTrees":
                        this.IndividualTrees.ReadXml(reader);
                        break;
                    case "individualTreeSpeciesStatistics":
                        this.IndividualTreeSpeciesStatistics.ReadXml(reader);
                        break;
                    case "threePG":
                        this.ThreePG.ReadXml(reader);
                        break;
                    default:
                        throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
                }
            }
            else
            {
                switch (reader.Name)
                {
                    case "resourceUnits":
                        reader.Read();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
