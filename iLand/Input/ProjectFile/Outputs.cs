using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Outputs : XmlSerializable
	{
		public string? DatabaseFile { get; private set; }

		public ResourceUnitConditionOutput Carbon { get; init; }
		public ResourceUnitConditionOutput CarbonFlow { get; init; }
		public DynamicStandOutput DynamicStand { get; init; }
		public ConditionOutput Landscape { get; init; }
		public LandscapeRemovedOutput LandscapeRemoved { get; init; }
		public Enablable Management { get; init; }
		public Enablable ProductionMonth { get; init; }
		public ConditionOutput Sapling { get; init; }
		public SaplingDetailOutput SaplingDetail { get; init; }
		public ConditionOutput Stand { get; init; }
		public Enablable StandDead { get; init; }
		public FilterOutput Tree { get; init; }
		public FilterOutput TreeRemoved { get; init; }
		public ResourceUnitConditionOutput Water { get; init; }

		public Enablable BarkBeetle { get; init; }
		public Enablable Wind { get; init; }
		public Enablable Fire { get; init; }
		public Logging Logging { get; init; }

		public Outputs()
        {
			this.DatabaseFile = null;

			this.Carbon = new ResourceUnitConditionOutput();
			this.CarbonFlow = new ResourceUnitConditionOutput();
			this.DynamicStand = new DynamicStandOutput();
			this.Landscape = new ConditionOutput();
			this.LandscapeRemoved = new LandscapeRemovedOutput();
			this.Management = new Enablable();
			this.ProductionMonth = new Enablable();
			this.Sapling = new ConditionOutput();
			this.SaplingDetail = new SaplingDetailOutput();
			this.Stand = new ConditionOutput();
			this.StandDead = new Enablable();
			this.Tree = new FilterOutput();
			this.TreeRemoved = new FilterOutput();
			this.Water = new ResourceUnitConditionOutput();

			this.BarkBeetle = new Enablable();
			this.Wind = new Enablable();
			this.Fire = new Enablable();
			this.Logging = new Logging();
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "output", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "databaseFile", StringComparison.Ordinal))
			{
				this.DatabaseFile = reader.ReadElementContentAsString().Trim();
			}
			//else if (String.Equals(reader.Name, "debugOutput", StringComparison.Ordinal))
			//{
			//	this.DebugOutput = reader.ReadElementContentAsBoolean();
			//}
			//else if (String.Equals(reader.Name, "debugOutputAutoSave", StringComparison.Ordinal))
			//{
			//	this.DebugOutputAutoSave = reader.ReadElementContentAsBoolean();
			//}
			else if (String.Equals(reader.Name, "tree", StringComparison.Ordinal))
			{
				this.Tree.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "treeRemoved", StringComparison.Ordinal))
			{
				this.TreeRemoved.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "stand", StringComparison.Ordinal))
			{
				this.Stand.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "standDead", StringComparison.Ordinal))
			{
				this.StandDead.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "productionMonth", StringComparison.Ordinal))
			{
				this.ProductionMonth.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "management", StringComparison.Ordinal))
			{
				this.Management.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "sapling", StringComparison.Ordinal))
			{
				this.Sapling.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "saplingDetail", StringComparison.Ordinal))
			{
				this.SaplingDetail.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "carbon", StringComparison.Ordinal))
			{
				this.Carbon.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "carbonFlow", StringComparison.Ordinal))
			{
				this.CarbonFlow.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "water", StringComparison.Ordinal))
			{
				this.Water.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "landscape", StringComparison.Ordinal))
			{
				this.Landscape.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "dynamicStand", StringComparison.Ordinal))
			{
				this.DynamicStand.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "barkbeetle", StringComparison.Ordinal))
			{
				this.BarkBeetle.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
			{
				this.Wind.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "fire", StringComparison.Ordinal))
			{
				this.Fire.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "landscapeRemoved", StringComparison.Ordinal))
			{
				this.LandscapeRemoved.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "logging", StringComparison.Ordinal))
			{
				this.Logging.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
