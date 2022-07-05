using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SaplingDetailAnnualOutput : ConditionAnnualOutput
    {
        public float MinDbh { get; private set; }

        public SaplingDetailAnnualOutput()
            : base("saplingDetail")
        {
            this.MinDbh = 0.0F;
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
                    case "condition":
                        this.Condition = reader.ReadElementContentAsString().Trim();
                        break;
                    case "minDbh":
                        this.MinDbh = reader.ReadElementContentAsFloat();
                        if (this.MinDbh < 0.0F)
                        {
                            throw new XmlException("Minimum DBH for sapling detail is negative.");
                        }
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
