using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Snag : XmlSerializable
    {
        public float DbhBreakpointSmallMedium { get; private set; }
        public float DdhBreakpointMediumLarge { get; private set; }

        public Snag()
        {
            this.DbhBreakpointSmallMedium = 20.0F;
            this.DdhBreakpointMediumLarge = 100.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "snag":
                    reader.Read();
                    break;
                case "smallMediumDbhBreakpoint":
                    this.DbhBreakpointSmallMedium = reader.ReadElementContentAsFloat();
                    if (this.DbhBreakpointSmallMedium < 0.0F)
                    {
                        throw new XmlException("Breakpoint between DBH classes 1 and 2 is negative.");
                    }
                    break;
                case "mediumLargeDbhBreakpoint":
                    this.DdhBreakpointMediumLarge = reader.ReadElementContentAsFloat();
                    if (this.DdhBreakpointMediumLarge < 0.0F)
                    {
                        throw new XmlException("Breakpoint between DBH classes 2 and 3 is negative.");
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}