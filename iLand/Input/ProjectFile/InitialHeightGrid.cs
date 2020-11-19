using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class InitialHeightGrid : XmlSerializable
    {
        public string FitFormula { get; private set; }
        public string? FileName { get; private set; }
		public int MaxTries { get; private set; }

		public InitialHeightGrid()
        {
			this.FileName = null;
			this.FitFormula = "polygon(x, 0,0, 0.8,1, 1.1, 1, 1.25,0)";
			this.MaxTries = 10;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "heightGrid", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "fileName", StringComparison.Ordinal))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "maxTries", StringComparison.Ordinal))
            {
                this.MaxTries = reader.ReadElementContentAsInt();
                if (this.MaxTries < 1)
                {
                    throw new XmlException("Maximum height tries is zero or negative.");
                }
            }
            else if (String.Equals(reader.Name, "fitFormula", StringComparison.Ordinal))
            {
                this.FitFormula = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
