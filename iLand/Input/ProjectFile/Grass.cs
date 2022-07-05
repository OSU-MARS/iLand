using iLand.World;
using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Grass : Enablable
    {
		public GrassAlgorithm Algorithm { get; private set; }
		public string? ContinuousRegenerationEffect { get; private set; }
		public int ContinuousYearsToFullCover { get; private set; }
		public string? ContinuousCover { get; private set; }
		public string? PixelDuration { get; private set; }
		public float PixelLifThreshold { get; private set; }

		public Grass()
			: base("grass")
        {
			this.Algorithm = GrassAlgorithm.CellOnOff;
			this.ContinuousRegenerationEffect = null;
			this.ContinuousCover = null;
			this.ContinuousYearsToFullCover = 1;
			this.PixelDuration = null;
			this.PixelLifThreshold = 0.2F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
			if (reader.AttributeCount != 0)
			{
				this.ReadEnabled(reader);
			}
			else
			{
				switch (reader.Name)
				{
					case "type":
						string grassAlgorithmAsString = reader.ReadElementContentAsString().Trim();
						this.Algorithm = grassAlgorithmAsString switch
						{
							"continuous" => GrassAlgorithm.ContinuousLight,
							"pixel" => GrassAlgorithm.CellOnOff,
							_ => throw new XmlException("Unknown grass algorithm type '" + grassAlgorithmAsString + "'.")
						};
						break;
					case "grassDuration":
						this.PixelDuration = reader.ReadElementContentAsString().Trim();
						break;
					case "lifThreshold":
						this.PixelLifThreshold = reader.ReadElementContentAsFloat();
						if ((this.PixelLifThreshold < 0.0F) || (this.PixelLifThreshold > 1.0F))
						{
							throw new XmlException("Grass LIF threshold is negative or greater than 1.0.");
						}
						break;
					case "grassPotential":
						this.ContinuousCover = reader.ReadElementContentAsString().Trim();
						break;
					case "maxTimeLag":
						this.ContinuousYearsToFullCover = reader.ReadElementContentAsInt();
						if (this.ContinuousYearsToFullCover < 0)
						{
							throw new XmlException("Maxium grass time lag is negative.");
						}
						break;
					case "grassEffect":
						this.ContinuousRegenerationEffect = reader.ReadElementContentAsString().Trim();
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
        }
    }
}
