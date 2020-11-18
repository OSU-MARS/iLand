using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    // TODO: rationalize this DEM CRS to landscape grid transformation with DEM elevation and latitude settings
    public class ModelOrigin : XmlSerializable
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float Rotation { get; private set; }

        public ModelOrigin()
        {
            this.X = 0.0F;
            this.Y = 0.0F;
            this.Z = 0.0F;
            this.Rotation = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "modelOrigin", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "x", StringComparison.Ordinal))
            {
                this.X = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (String.Equals(reader.Name, "y", StringComparison.Ordinal))
            {
                this.Y = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (String.Equals(reader.Name, "z", StringComparison.Ordinal))
            {
                this.Z = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (String.Equals(reader.Name, "rotation", StringComparison.Ordinal))
            {
                this.Rotation = reader.ReadElementContentAsFloat();
                // no meaningful restriction on range of values, could constrain to 0-360 if needed
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
