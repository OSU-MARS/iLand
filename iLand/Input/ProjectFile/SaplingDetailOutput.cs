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

            if (reader.IsStartElement("saplingdetail"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("condition"))
            {
                this.Condition = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("minDbh"))
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
