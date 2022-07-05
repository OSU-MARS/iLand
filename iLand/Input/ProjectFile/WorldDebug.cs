using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class WorldDebug : XmlSerializable
    {
        public bool DumpStamps { get; private set; }
        public string? DebugTree { get; private set; }

        public WorldDebug()
        {
            this.DumpStamps = false;
            this.DebugTree = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "debug":
                    reader.Read();
                    break;
                case "debugTree":
                    this.DebugTree = reader.ReadElementContentAsString().Trim();
                    break;
                case "dumpStamps":
                    this.DumpStamps = reader.ReadElementContentAsBoolean();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
