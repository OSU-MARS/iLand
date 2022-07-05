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
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

            switch (reader.Name)
            {
                case "nitrogenResponseClasses":
                    reader.Read();
                    break;
                case "class1k":
                    this.Class1K = reader.ReadElementContentAsFloat();
                    if (this.Class1K >= 0.0F)
                    {
                        throw new XmlException("Class 1 nitrogen response: a is zero or positive.");
                    }
                    break;
                case "class1minimum":
                    this.Class1Minimum = reader.ReadElementContentAsFloat();
                    if (this.Class1Minimum <= 0.0F)
                    {
                        throw new XmlException("Class 1 nitrogen response: b is zero or negative.");
                    }
                    break;
                case "class2k":
                    this.Class2K = reader.ReadElementContentAsFloat();
                    if (this.Class2K >= 0.0F)
                    {
                        throw new XmlException("Class 2 nitrogen response: a is zero or positive.");
                    }
                    break;
                case "class2minimum":
                    this.Class2Minimum = reader.ReadElementContentAsFloat();
                    if (this.Class2Minimum <= 0.0F)
                    {
                        throw new XmlException("Class 2 nitrogen response: b is zero or negative.");
                    }
                    break;
                case "class3k":
                    this.Class3K = reader.ReadElementContentAsFloat();
                    if (this.Class3K >= 0.0F)
                    {
                        throw new XmlException("Class 3 nitrogen response: a is zero or positive.");
                    }
                    break;
                case "class3minimum":
                    this.Class3Minimum = reader.ReadElementContentAsFloat();
                    if (this.Class3Minimum <= 0.0F)
                    {
                        throw new XmlException("Class 3 nitrogen response: b is zero or negative.");
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
		}
	}
}
