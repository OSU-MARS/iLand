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

        public Wind()
            : base("wind")
        {
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
                    case "speciesParameter":
                        this.SpeciesParameter = reader.ReadElementContentAsString().Trim();
                        break;
                    case "soilFreezeMode":
                        this.SoilFreezeMode = reader.ReadElementContentAsString().Trim();
                        break;
                    case "triggeredByTimeEvent":
                        this.TriggeredByTimeEvent = reader.ReadElementContentAsBoolean();
                        break;
                    case "durationPerIteration":
                        this.DurationPerIteration = reader.ReadElementContentAsInt();
                        if (this.DurationPerIteration < 0)
                        {
                            throw new XmlException("Wind event duration is negative.");
                        }
                        break;
                    case "gustModifier":
                        this.GustModifier = reader.ReadElementContentAsFloat();
                        if (this.GustModifier < 0)
                        {
                            throw new XmlException("Gust multiplier is negative.");
                        }
                        break;
                    case "topoModifier":
                        this.TopoModifier = reader.ReadElementContentAsFloat();
                        if (this.TopoModifier < 0)
                        {
                            throw new XmlException("Topographic windspeed multiplier is negative.");
                        }
                        break;
                    case "directionVariation":
                        this.DirectionVariation = reader.ReadElementContentAsFloat();
                        // no clear range of values from iLand documentation
                        break;
                    case "direction":
                        this.Direction = reader.ReadElementContentAsFloat();
                        // no meaningful restriction from iLand documentation
                        break;
                    case "dayOfYear":
                        this.DayOfYear = reader.ReadElementContentAsInt();
                        if ((this.DayOfYear < 0) || (this.DayOfYear > Constant.DaysInLeapYear))
                        {
                            throw new XmlException("Day of year on which windstorm occurs is negative or greater than the number of days in a leap year.");
                        }
                        break;
                    case "speed":
                        this.Speed = reader.ReadElementContentAsFloat();
                        if (this.Speed < 0.0F)
                        {
                            throw new XmlException("Storm windspeed is negative.");
                        }
                        break;
                    case "duration":
                        this.Duration = reader.ReadElementContentAsFloat();
                        if (this.Duration < 0.0F)
                        {
                            throw new XmlException("Storm duration is negative.");
                        }
                        break;
                    case "topoGridFile":
                        this.TopoGridFile = reader.ReadElementContentAsString().Trim();
                        break;
                    case "factorEdge":
                        this.FactorEdge = reader.ReadElementContentAsFloat();
                        if (this.FactorEdge < 0.0F)
                        {
                            throw new XmlException("Edge widspeed factor is negative.");
                        }
                        break;
                    case "edgeDetectionThreshold":
                        this.EdgeDetectionThreshold = reader.ReadElementContentAsFloat();
                        if (this.EdgeDetectionThreshold < 0.0F)
                        {
                            throw new XmlException("Height difference threshold for wind edge effects is negative.");
                        }
                        break;
                    case "topexModifierType":
                        this.TopexModifierType = reader.ReadElementContentAsString().Trim();
                        break;
                    case "LRITransferFunction":
                        this.LriTransferFunction = reader.ReadElementContentAsString().Trim();
                        break;
                    case "edgeProbability":
                        this.EdgeProbability = reader.ReadElementContentAsString().Trim();
                        break;
                    case "edgeAgeBaseValue":
                        this.EdgeAgeBaseValue = reader.ReadElementContentAsInt();
                        if (this.EdgeAgeBaseValue < 0)
                        {
                            throw new XmlException("Edge age is negative.");
                        }
                        break;
                    case "edgeBackgroundProbability":
                        this.EdgeBackgroundProbability = reader.ReadElementContentAsFloat();
                        if ((this.EdgeBackgroundProbability < 0.0F) || (this.EdgeBackgroundProbability > 1.0F))
                        {
                            throw new XmlException("Edge background probability is negative or greater than 1.0.");
                        }
                        break;
                    case "onAfterWind":
                        this.OnAfterWind = reader.ReadElementContentAsString().Trim();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
