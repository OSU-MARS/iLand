using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Wind : Enablable
    {
        public string? SpeciesParameter { get; private set; }
        public string? SoilFreezeMode { get; private set; }
        public bool TriggeredByTimeEvent { get; private set; }
        public int DurationPerIteration { get; private set; }
        public float GustModifier { get; private set; }
        public float TopoModifier { get; private set; }
        public float DirectionVariation { get; private set; }
        public float Direction { get; private set; }
        public int DayOfYear { get; private set; }
        public float Speed { get; private set; }
        public float Duration { get; private set; }
        public string? TopoGridFile { get; private set; }
        public float FactorEdge { get; private set; }
        public float EdgeDetectionThreshold { get; private set; }
        public string? TopexModifierType { get; private set; }
        public string? LriTransferFunction { get; private set; }
        public string? EdgeProbability { get; private set; }
        public int EdgeAgeBaseValue { get; private set; }
        public float EdgeBackgroundProbability { get; private set; }
        public string? OnAfterWind { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "speciesParameter", StringComparison.Ordinal))
            {
                this.SpeciesParameter = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "soilFreezeMode", StringComparison.Ordinal))
            {
                this.SoilFreezeMode = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "triggeredByTimeEvent", StringComparison.Ordinal))
            {
                this.TriggeredByTimeEvent = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "durationPerIteration", StringComparison.Ordinal))
            {
                this.DurationPerIteration = reader.ReadElementContentAsInt();
                if (this.DurationPerIteration < 0)
                {
                    throw new XmlException("Wind event duration is negative.");
                }
            }
            else if (String.Equals(reader.Name, "gustModifier", StringComparison.Ordinal))
            {
                this.GustModifier = reader.ReadElementContentAsFloat();
                if (this.GustModifier < 0)
                {
                    throw new XmlException("Gust multiplier is negative.");
                }
            }
            else if (String.Equals(reader.Name, "topoModifier", StringComparison.Ordinal))
            {
                this.TopoModifier = reader.ReadElementContentAsFloat();
                if (this.TopoModifier < 0)
                {
                    throw new XmlException("Topographic windspeed multiplier is negative.");
                }
            }
            else if (String.Equals(reader.Name, "directionVariation", StringComparison.Ordinal))
            {
                this.DirectionVariation = reader.ReadElementContentAsFloat();
                // no clear range of values from iLand documentation
            }
            else if (String.Equals(reader.Name, "direction", StringComparison.Ordinal))
            {
                this.Direction = reader.ReadElementContentAsFloat();
                // no meaningful restriction from iLand documentation
            }
            else if (String.Equals(reader.Name, "dayOfYear", StringComparison.Ordinal))
            {
                this.DayOfYear = reader.ReadElementContentAsInt();
                if ((this.DayOfYear < 0) || (this.DayOfYear > Constant.DaysInLeapYear))
                {
                    throw new XmlException("Day of year on which windstorm occurs is negative or greater than the number of days in a leap year.");
                }
            }
            else if (String.Equals(reader.Name, "speed", StringComparison.Ordinal))
            {
                this.Speed = reader.ReadElementContentAsFloat();
                if (this.Speed < 0.0F)
                {
                    throw new XmlException("Storm windspeed is negative.");
                }
            }
            else if (String.Equals(reader.Name, "duration", StringComparison.Ordinal))
            {
                this.Duration = reader.ReadElementContentAsFloat();
                if (this.Duration < 0.0F)
                {
                    throw new XmlException("Storm duration is negative.");
                }
            }
            else if (String.Equals(reader.Name, "topoGridFile", StringComparison.Ordinal))
            {
                this.TopoGridFile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "factorEdge", StringComparison.Ordinal))
            {
                this.FactorEdge = reader.ReadElementContentAsFloat();
                if (this.FactorEdge < 0.0F)
                {
                    throw new XmlException("Edge widspeed factor is negative.");
                }
            }
            else if (String.Equals(reader.Name, "edgeDetectionThreshold", StringComparison.Ordinal))
            {
                this.EdgeDetectionThreshold = reader.ReadElementContentAsFloat();
                if (this.EdgeDetectionThreshold < 0.0F)
                {
                    throw new XmlException("Height difference threshold for wind edge effects is negative.");
                }
            }
            else if (String.Equals(reader.Name, "topexModifierType", StringComparison.Ordinal))
            {
                this.TopexModifierType = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "LRITransferFunction", StringComparison.Ordinal))
            {
                this.LriTransferFunction = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "edgeProbability", StringComparison.Ordinal))
            {
                this.EdgeProbability = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "edgeAgeBaseValue", StringComparison.Ordinal))
            {
                this.EdgeAgeBaseValue = reader.ReadElementContentAsInt();
                if (this.EdgeAgeBaseValue < 0)
                {
                    throw new XmlException("Edge age is negative.");
                }
            }
            else if (String.Equals(reader.Name, "edgeBackgroundProbability", StringComparison.Ordinal))
            {
                this.EdgeBackgroundProbability = reader.ReadElementContentAsFloat();
                if ((this.EdgeBackgroundProbability < 0.0F) || (this.EdgeBackgroundProbability > 1.0F))
                {
                    throw new XmlException("Edge background probability is negative or greater than 1.0.");
                }
            }
            else if (String.Equals(reader.Name, "onAfterWind", StringComparison.Ordinal))
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
