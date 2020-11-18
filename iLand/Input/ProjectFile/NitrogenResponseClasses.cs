using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class NitrogenResponseClasses : XmlSerializable
    {
        public float Class1K { get; private set; }
        public float Class1Minimum { get; private set; }
        public float Class2K { get; private set; }
        public float Class2Minimum { get; private set; }
        public float Class3K { get; private set; }
        public float Class3Minimum { get; private set; }

        public NitrogenResponseClasses()
        {
            // no defaults in C++, must be set in project file
            this.Class1K = 0.0F;
            this.Class1Minimum = 0.0F;
            this.Class2K = 0.0F;
            this.Class2Minimum = 0.0F;
            this.Class3K = 0.0F;
            this.Class3Minimum = 0.0F;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "nitrogenResponseClasses", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "class1k", StringComparison.Ordinal))
			{
				this.Class1K = reader.ReadElementContentAsFloat();
                if (this.Class1K >= 0.0F)
                {
                    throw new XmlException("Class 1 nitrogen response: a is zero or positive.");
                }
			}
            else if (String.Equals(reader.Name, "class1minimum", StringComparison.Ordinal))
            {
                this.Class1Minimum = reader.ReadElementContentAsFloat();
                if (this.Class1Minimum <= 0.0F)
                {
                    throw new XmlException("Class 1 nitrogen response: b is zero or negative.");
                }
            }
            else if (String.Equals(reader.Name, "class2k", StringComparison.Ordinal))
            {
                this.Class2K = reader.ReadElementContentAsFloat();
                if (this.Class2K >= 0.0F)
                {
                    throw new XmlException("Class 2 nitrogen response: a is zero or positive.");
                }
            }
            else if (String.Equals(reader.Name, "class2minimum", StringComparison.Ordinal))
            {
                this.Class2Minimum = reader.ReadElementContentAsFloat();
                if (this.Class2Minimum <= 0.0F)
                {
                    throw new XmlException("Class 2 nitrogen response: b is zero or negative.");
                }
            }
            else if (String.Equals(reader.Name, "class3k", StringComparison.Ordinal))
            {
                this.Class3K = reader.ReadElementContentAsFloat();
                if (this.Class3K >= 0.0F)
                {
                    throw new XmlException("Class 3 nitrogen response: a is zero or positive.");
                }
            }
            else if (String.Equals(reader.Name, "class3minimum", StringComparison.Ordinal))
            {
                this.Class3Minimum = reader.ReadElementContentAsFloat();
                if (this.Class3Minimum <= 0.0F)
                {
                    throw new XmlException("Class 3 nitrogen response: b is zero or negative.");
                }
            }
            else
            {
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
