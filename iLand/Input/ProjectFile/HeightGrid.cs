using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class HeightGrid : Enablable
    {
		public string FileName { get; private set; }
		public int MaxTries { get; private set; }
		public string FitFormula { get; private set; }

		public HeightGrid()
        {
			this.FileName = "init";
			this.FitFormula = "polygon(x, 0,0, 0.8,1, 1.1, 1, 1.25,0)";
			this.MaxTries = 10;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("heightGrid"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("fileName"))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("maxTries"))
            {
                this.MaxTries = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("fitFormula"))
            {
                this.FitFormula = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
