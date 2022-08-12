using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SqlOutputs : XmlSerializable
    {
		public string? DatabaseFile { get; private set; }

		public ResourceUnitConditionOutput Carbon { get; private init; }
		public ResourceUnitConditionOutput CarbonFlow { get; private init; }
		public DynamicStandAnnualOutput DynamicStand { get; private init; }
		public FilterAnnualOutput IndividualTree { get; private init; }
		public ConditionAnnualOutput Landscape { get; private init; }
		public LandscapeRemovedOutput LandscapeRemoved { get; private init; }
		public Enablable Management { get; private init; }
		public ConditionAnnualOutput Sapling { get; private init; }
		public SaplingDetailAnnualOutput SaplingDetail { get; private init; }
		public ConditionAnnualOutput Stand { get; private init; }
		public Enablable StandDead { get; private init; }
		public Enablable ThreePG { get; private init; }
		public FilterAnnualOutput TreeRemoved { get; private init; }
		public ResourceUnitConditionOutput Water { get; private init; }

		public Enablable BarkBeetle { get; private init; }
		public Enablable Wind { get; private init; }
		public Enablable Fire { get; private init; }

		public SqlOutputs()
		{
			this.DatabaseFile = null;

			this.Carbon = new("carbon");
			this.CarbonFlow = new("carbonFlow");
			this.DynamicStand = new();
			this.IndividualTree = new("individualTree");
			this.Landscape = new("landscape");
			this.LandscapeRemoved = new();
			this.Management = new("management");
			this.Sapling = new("sapling");
			this.SaplingDetail = new();
			this.Stand = new("stand");
			this.StandDead = new("standDead");
			this.ThreePG = new("threePG");
			this.TreeRemoved = new("treeRemoved");
			this.Water = new("water");

			this.BarkBeetle = new("barkBeetle");
			this.Wind = new("wind");
			this.Fire = new("fire");
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "barkBeetle":
						this.BarkBeetle.ReadXml(reader);
						break;
					case "carbon":
						this.Carbon.ReadXml(reader);
						break;
					case "carbonFlow":
						this.CarbonFlow.ReadXml(reader);
						break;
					case "dynamicStand":
						this.DynamicStand.ReadXml(reader);
						break;
					case "fire":
						this.Fire.ReadXml(reader);
						break;
					case "individualTree":
						this.IndividualTree.ReadXml(reader);
						break;
					case "landscape":
						this.Landscape.ReadXml(reader);
						break;
					case "landscapeRemoved":
						this.LandscapeRemoved.ReadXml(reader);
						break;
					case "management":
						this.Management.ReadXml(reader);
						break;
					case "sapling":
						this.Sapling.ReadXml(reader);
						break;
					case "saplingDetail":
						this.SaplingDetail.ReadXml(reader);
						break;
					case "stand":
						this.Stand.ReadXml(reader);
						break;
					case "standDead":
						this.StandDead.ReadXml(reader);
						break;
					case "threePG":
						this.ThreePG.ReadXml(reader);
						break;
					case "treeRemoved":
						this.TreeRemoved.ReadXml(reader);
						break;
					case "water":
						this.Water.ReadXml(reader);
						break;
					case "wind":
						this.Wind.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "databaseFile":
						this.DatabaseFile = reader.ReadElementContentAsString().Trim();
						break;
					case "sql":
						reader.Read();
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
