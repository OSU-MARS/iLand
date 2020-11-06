using System.Xml;

namespace iLand.Input.ProjectFile
{
    // TODO: rationalize this DEM CRS to landscape grid transformation with DEM elevation and latitude settings
    public class Location : XmlSerializable
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float Rotation { get; private set; }

        public Location()
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

            if (reader.IsStartElement("location"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("x"))
            {
                this.X = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (reader.IsStartElement("y"))
            {
                this.Y = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (reader.IsStartElement("z"))
            {
                this.Z = reader.ReadElementContentAsFloat();
                // no restriction on range of values
            }
            else if (reader.IsStartElement("rotation"))
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
