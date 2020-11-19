using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AnnualOutputs : XmlSerializable
    {
		public string? DatabaseFile { get; private set; }

		public ResourceUnitConditionOutput Carbon { get; private init; }
		public ResourceUnitConditionOutput CarbonFlow { get; private init; }
		public DynamicStandAnnualOutput DynamicStand { get; private init; }
		public ConditionAnnualOutput Landscape { get; private init; }
		public LandscapeRemovedOutput LandscapeRemoved { get; private init; }
		public Enablable Management { get; private init; }
		public Enablable ProductionMonth { get; private init; }
		public ConditionAnnualOutput Sapling { get; private init; }
		public SaplingDetailAnnualOutput SaplingDetail { get; private init; }
		public ConditionAnnualOutput Stand { get; private init; }
		public Enablable StandDead { get; private init; }
		public FilterAnnualOutput Tree { get; private init; }
		public FilterAnnualOutput TreeRemoved { get; private init; }
		public ResourceUnitConditionOutput Water { get; private init; }

		public Enablable BarkBeetle { get; private init; }
		public Enablable Wind { get; private init; }
		public Enablable Fire { get; private init; }

		public AnnualOutputs()
		{
			this.DatabaseFile = null;

			this.Carbon = new ResourceUnitConditionOutput("carbon");
			this.CarbonFlow = new ResourceUnitConditionOutput("carbonFlow");
			this.DynamicStand = new DynamicStandAnnualOutput();
			this.Landscape = new ConditionAnnualOutput("landscape");
			this.LandscapeRemoved = new LandscapeRemovedOutput();
			this.Management = new Enablable("management");
			this.ProductionMonth = new Enablable("productionMonth");
			this.Sapling = new ConditionAnnualOutput("sapling");
			this.SaplingDetail = new SaplingDetailAnnualOutput();
			this.Stand = new ConditionAnnualOutput("stand");
			this.StandDead = new Enablable("standDead");
			this.Tree = new FilterAnnualOutput("tree");
			this.TreeRemoved = new FilterAnnualOutput("treeRemoved");
			this.Water = new ResourceUnitConditionOutput("water");

			this.BarkBeetle = new Enablable("barkBeetle");
			this.Wind = new Enablable("wind");
			this.Fire = new Enablable("fire");
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				if (String.Equals(reader.Name, "barkBeetle", StringComparison.Ordinal))
				{
					this.BarkBeetle.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "carbon", StringComparison.Ordinal))
				{
					this.Carbon.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "carbonFlow", StringComparison.Ordinal))
				{
					this.CarbonFlow.ReadXml(reader);
				}
				//if (String.Equals(reader.Name, "debugOutput", StringComparison.Ordinal))
				//{
				//	this.DebugOutput = reader.ReadElementContentAsBoolean();
				//}
				//else if (String.Equals(reader.Name, "debugOutputAutoSave", StringComparison.Ordinal))
				//{
				//	this.DebugOutputAutoSave = reader.ReadElementContentAsBoolean();
				//}
				else if (String.Equals(reader.Name, "dynamicStand", StringComparison.Ordinal))
				{
					this.DynamicStand.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "fire", StringComparison.Ordinal))
				{
					this.Fire.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "landscape", StringComparison.Ordinal))
				{
					this.Landscape.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "landscapeRemoved", StringComparison.Ordinal))
				{
					this.LandscapeRemoved.ReadXml(reader);
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
				else if (String.Equals(reader.Name, "stand", StringComparison.Ordinal))
				{
					this.Stand.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "standDead", StringComparison.Ordinal))
				{
					this.StandDead.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "tree", StringComparison.Ordinal))
				{
					this.Tree.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "treeRemoved", StringComparison.Ordinal))
				{
					this.TreeRemoved.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "water", StringComparison.Ordinal))
				{
					this.Water.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
				{
					this.Wind.ReadXml(reader);
				}
				else
				{
					throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else if (String.Equals(reader.Name, "annual", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "databaseFile", StringComparison.Ordinal))
			{
				this.DatabaseFile = reader.ReadElementContentAsString().Trim();
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
