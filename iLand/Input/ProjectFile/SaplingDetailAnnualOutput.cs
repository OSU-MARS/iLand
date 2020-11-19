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
            else if (String.Equals(reader.Name, "condition", StringComparison.Ordinal))
            {
                this.Condition = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "minDbh", StringComparison.Ordinal))
            {
                this.MinDbh = reader.ReadElementContentAsFloat();
                if (this.MinDbh < 0.0F)
                {
                    throw new XmlException("Minimum DBH for sapling detail is negative.");
                }
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
