using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Wind : Enablable
    {
        public string SpeciesParameter { get; private set; }
        public string SoilFreezeMode { get; private set; }
        public bool TriggeredByTimeEvent { get; private set; }
        public int DurationPerIteration { get; private set; }
        public double GustModifier { get; private set; }
        public double TopoModifier { get; private set; }
        public double DirectionVariation { get; private set; }
        public double Direction { get; private set; }
        public int DayOfYear { get; private set; }
        public double Speed { get; private set; }
        public double Duration { get; private set; }
        public string TopoGridFile { get; private set; }
        public double FactorEdge { get; private set; }
        public double EdgeDetectionThreshold { get; private set; }
        public string TopexModifierType { get; private set; }
        public string LriTransferFunction { get; private set; }
        public string EdgeProbability { get; private set; }
        public int EdgeAgeBaseValue { get; private set; }
        public double EdgeBackgroundProbability { get; private set; }
        public string OnAfterWind { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("wind"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("speciesParameter"))
            {
                this.SpeciesParameter = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("soilFreezeMode"))
            {
                this.SoilFreezeMode = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("triggeredByTimeEvent"))
            {
                this.TriggeredByTimeEvent = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("durationPerIteration"))
            {
                this.DurationPerIteration = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("gustModifier"))
            {
                this.GustModifier = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("topoModifier"))
            {
                this.TopoModifier = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("directionVariation"))
            {
                this.DirectionVariation = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("direction"))
            {
                this.Direction = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("dayOfYear"))
            {
                this.DayOfYear = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("speed"))
            {
                this.Speed = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("duration"))
            {
                this.Duration = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("topoGridFile"))
            {
                this.TopoGridFile = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("factorEdge"))
            {
                this.FactorEdge = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("edgeDetectionThreshold"))
            {
                this.EdgeDetectionThreshold = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("topexModifierType"))
            {
                this.TopexModifierType = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("LRITransferFunction"))
            {
                this.LriTransferFunction = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("edgeProbability"))
            {
                this.EdgeProbability = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("edgeAgeBaseValue"))
            {
                this.EdgeAgeBaseValue = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("edgeBackgroundProbability"))
            {
                this.EdgeBackgroundProbability = reader.ReadElementContentAsDouble();
            }
            else if (reader.IsStartElement("onAfterWind"))
            {
                this.OnAfterWind = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
