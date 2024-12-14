using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DynamicStandAnnualOutput : ConditionAnnualOutput
    {
        public bool BySpecies { get; private set; }
        public bool ByResourceUnit { get; private set; }

        public string? Columns { get; protected set; }
        public string? ResourceUnitCondition { get; protected set; }
        public string? TreeFilter { get; protected set; }

        public DynamicStandAnnualOutput()
            : base("dynamicStand")
        {
            this.ByResourceUnit = true;
            this.BySpecies = true;
            this.Columns = null;
            this.ResourceUnitCondition = null;
            this.TreeFilter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else
            {
                switch (reader.Name)
                {
                    case "bySpecies":
                        this.BySpecies = reader.ReadElementContentAsBoolean();
                        break;
                    case "byResourceUnit":
                        this.ByResourceUnit = reader.ReadElementContentAsBoolean();
                        break;
                    case "columns":
                        this.Columns = reader.ReadElementContentAsString().Trim();
                        break;
                    case "condition":
                        this.Condition = reader.ReadElementContentAsString().Trim();
                        break;
                    case "ruCondition":
                        this.ResourceUnitCondition = reader.ReadElementContentAsString().Trim();
                        break;
                    case "ruFilter":
                        this.ResourceUnitFilter = reader.ReadElementContentAsString().Trim();
                        break;
                    case "treeFilter":
                        this.TreeFilter = reader.ReadElementContentAsString().Trim();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
