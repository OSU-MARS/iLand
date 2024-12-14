using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Microclimate : Enablable
    {
        public bool BarkBeetleEffect { get; set; }
        public bool DecompositionEffect { get; set; }
        public bool EstablishmentEffect { get; set; }

        public Microclimate()
            : base("microclimate")
        {
            this.BarkBeetleEffect = true;
            this.DecompositionEffect = true;
            this.EstablishmentEffect = true;
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
                    case "barkbeetle":
                        this.BarkBeetleEffect = reader.ReadElementContentAsBoolean();
                        break;
                    case "decomposition":
                        this.DecompositionEffect = reader.ReadElementContentAsBoolean();
                        break;
                    case "establishment":
                        this.EstablishmentEffect = reader.ReadElementContentAsBoolean();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
