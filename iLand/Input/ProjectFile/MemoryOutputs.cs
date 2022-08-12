using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class MemoryOutputs : XmlSerializable
    {
		public int InitialTrajectoryLengthInYears { get; private set; }
		public ResourceUnitMemoryOutputs ResourceUnitTrajectories { get; private set; }
		public Enablable StandTrajectories { get; private set; }

		public MemoryOutputs()
		{
			this.InitialTrajectoryLengthInYears = Constant.Data.AnnualAllocationIncrement;
			this.ResourceUnitTrajectories = ResourceUnitMemoryOutputs.None;
			this.StandTrajectories = new("standTrajectories");
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "standTrajectories":
						this.StandTrajectories.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "memory":
						reader.Read();
						break;
					case "initialTrajectoryLengthInYears":
						this.InitialTrajectoryLengthInYears = reader.ReadElementContentAsInt();
						break;
					case "resourceUnitTrajectories":
						this.ResourceUnitTrajectories = Enum.Parse<ResourceUnitMemoryOutputs>(reader.ReadElementContentAsString());
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
