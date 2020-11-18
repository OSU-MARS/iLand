using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SaplingDetailOutput : ConditionOutput
    {
        public float MinDbh { get; private set; }

        public SaplingDetailOutput()
        {
            this.MinDbh = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "saplingDetail", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
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
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
