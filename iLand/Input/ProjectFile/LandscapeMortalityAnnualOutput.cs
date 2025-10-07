using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LandscapeMortalityAnnualOutput : Enablable
    {
        public string DbhClasses { get; private set; }
        public bool IncludeFelled { get; private set; } // live trees harvested or cut and dropped, dead trees salvage harvested
		public bool IncludeNatural { get; private set; } // trees dead of stress or killed by an abiotic or biotic agent

		public LandscapeMortalityAnnualOutput()
            : base("landscapeMortality")
        {
            this.DbhClasses = String.Empty;
            this.IncludeFelled = true;
            this.IncludeNatural = false;
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
                    case "dbhClasses":
                        this.DbhClasses = reader.ReadElementContentAsString();
                        break;
                    case "includeFelled":
                        this.IncludeFelled = reader.ReadElementContentAsBoolean();
                        break;
                    case "includeNatural":
                        this.IncludeNatural = reader.ReadElementContentAsBoolean();
                        break;
                    default:
                        throw new XmlException($"Element '{reader.Name}' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
