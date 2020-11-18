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
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "debug", StringComparison.Ordinal))
            {
                reader.Read();
                return;
            }
            else if (String.Equals(reader.Name, "debugTree", StringComparison.Ordinal))
            {
                this.DebugTree = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "dumpStamps", StringComparison.Ordinal))
            {
                this.DumpStamps = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
