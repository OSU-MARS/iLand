using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Logging : XmlSerializable
    {
        public string LogTarget { get; private set; }
        public string LogFile { get; private set; }
        public bool Flush { get; private set; }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("logging"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("logTarget"))
			{
				this.LogTarget = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("logFile"))
			{
				this.LogFile = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("flush"))
			{
				this.Flush = reader.ReadElementContentAsBoolean();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
