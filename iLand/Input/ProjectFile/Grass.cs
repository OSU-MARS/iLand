using iLand.World;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Grass : Enablable
    {
		public GrassAlgorithmType Type { get; private set; }
		public string GrassDuration { get; private set; }
		public float LifThreshold { get; private set; }
		public string GrassPotential { get; private set; }
		public int MaxTimeLag { get; private set; }
		public string GrassEffect { get; private set; }

		public Grass()
        {
			this.GrassDuration = null;
			this.GrassPotential = null;
			this.LifThreshold = 0.2F;
			this.MaxTimeLag = 0; // TODO: unhelpful default since minimum value is 1
			this.Type = GrassAlgorithmType.Invalid; // TODO: unhelpful default, C++ defaulted to null string
        }

        protected override void ReadStartElement(XmlReader reader)
        {
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("grass"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("enabled"))
			{
				this.Enabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("type"))
            {
				string grassAlgorithmAsString = reader.ReadElementContentAsString().Trim();
				this.Type = grassAlgorithmAsString switch
				{
					"continuous" => GrassAlgorithmType.Continuous,
					"pixel" => GrassAlgorithmType.Pixel,
					_ => throw new XmlException("Unknown grass algorithm type '" + grassAlgorithmAsString + "'.")
				};
            }
			else if (reader.IsStartElement("grassDuration"))
			{
				this.GrassDuration = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("LIFThreshold"))
			{
				this.LifThreshold = reader.ReadElementContentAsFloat();
				if ((this.LifThreshold < 0.0F) || (this.LifThreshold > 1.0F))
				{
					throw new XmlException("Grass LIF threshold is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("grassPotential"))
			{
				this.GrassPotential = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("maxTimeLag"))
			{
				this.MaxTimeLag = reader.ReadElementContentAsInt();
				if (this.MaxTimeLag < 0)
                {
					throw new XmlException("Maxium grass time lag is negative.");
                }
			}
			else if (reader.IsStartElement("grassEffect"))
			{
				this.GrassEffect = reader.ReadElementContentAsString().Trim();
			}
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
