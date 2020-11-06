using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Parameter : XmlSerializable
    {
        // TODO: move to world
        // special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
        public bool Torus { get; private set; }
        public string DebugTree { get; private set; }
        public bool DebugClear { get; private set; }
        public float GppPerYear { get; private set; }
        public bool DebugDumpStamps { get; private set; }

        public Parameter()
        {
            this.DebugDumpStamps = false;
            this.DebugTree = null;
            this.GppPerYear = 0.0F;
            this.Torus = false;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("parameter"))
            {
                reader.Read();
                return;
            }
            else if (reader.IsStartElement("torus"))
            {
                this.Torus = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("debug_tree"))
            {
                this.DebugTree = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("debug_clear"))
            {
                this.DebugClear = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("gpp_per_year"))
            {
                this.GppPerYear = reader.ReadElementContentAsFloat();
                if (this.GppPerYear < 0.0F)
                {
                    throw new XmlException("Fixed annual GPP override is negative.");
                }
            }
            else if (reader.IsStartElement("debugDumpStamps"))
            {
                this.DebugDumpStamps = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
