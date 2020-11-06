using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class NitrogenResponseClasses : XmlSerializable
    {
        public float Class1A { get; private set; }
        public float Class1B { get; private set; }
        public float Class2A { get; private set; }
        public float Class2B { get; private set; }
        public float Class3A { get; private set; }
        public float Class3B { get; private set; }

        public NitrogenResponseClasses()
        {
            // no defaults in C++, must be set in project file
            this.Class1A = 0.0F;
            this.Class1B = 0.0F;
            this.Class2A = 0.0F;
            this.Class2B = 0.0F;
            this.Class3A = 0.0F;
            this.Class3B = 0.0F;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("nitrogenResponseClasses"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("class_1_a"))
			{
				this.Class1A = reader.ReadElementContentAsFloat();
                if (this.Class1A >= 0.0F)
                {
                    throw new XmlException("Class 1 nitrogen response: a is zero or positive.");
                }
			}
            else if (reader.IsStartElement("class_1_b"))
            {
                this.Class1B = reader.ReadElementContentAsFloat();
                if (this.Class1B <= 0.0F)
                {
                    throw new XmlException("Class 1 nitrogen response: b is zero or negative.");
                }
            }
            else if (reader.IsStartElement("class_2_a"))
            {
                this.Class2A = reader.ReadElementContentAsFloat();
                if (this.Class2A >= 0.0F)
                {
                    throw new XmlException("Class 2 nitrogen response: a is zero or positive.");
                }
            }
            else if (reader.IsStartElement("class_2_b"))
            {
                this.Class2B = reader.ReadElementContentAsFloat();
                if (this.Class2B <= 0.0F)
                {
                    throw new XmlException("Class 2 nitrogen response: b is zero or negative.");
                }
            }
            else if (reader.IsStartElement("class_3_a"))
            {
                this.Class3A = reader.ReadElementContentAsFloat();
                if (this.Class3A >= 0.0F)
                {
                    throw new XmlException("Class 3 nitrogen response: a is zero or positive.");
                }
            }
            else if (reader.IsStartElement("class_3_b"))
            {
                this.Class3B = reader.ReadElementContentAsFloat();
                if (this.Class3B <= 0.0F)
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
