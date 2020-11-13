﻿using System.Xml;

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
                if (this.DurationPerIteration < 0)
                {
                    throw new XmlException("Wind event duration is negative.");
                }
            }
            else if (reader.IsStartElement("gustModifier"))
            {
                this.GustModifier = reader.ReadElementContentAsFloat();
                if (this.GustModifier < 0)
                {
                    throw new XmlException("Gust multiplier is negative.");
                }
            }
            else if (reader.IsStartElement("topoModifier"))
            {
                this.TopoModifier = reader.ReadElementContentAsFloat();
                if (this.TopoModifier < 0)
                {
                    throw new XmlException("Topographic windspeed multiplier is negative.");
                }
            }
            else if (reader.IsStartElement("directionVariation"))
            {
                this.DirectionVariation = reader.ReadElementContentAsFloat();
                // no clear range of values from iLand documentation
            }
            else if (reader.IsStartElement("direction"))
            {
                this.Direction = reader.ReadElementContentAsFloat();
                // no meaningful restriction from iLand documentation
            }
            else if (reader.IsStartElement("dayOfYear"))
            {
                this.DayOfYear = reader.ReadElementContentAsInt();
                if ((this.DayOfYear < 0) || (this.DayOfYear > Constant.DaysInLeapYear))
                {
                    throw new XmlException("Day of year on which windstorm occurs is negative or greater than the number of days in a leap year.");
                }
            }
            else if (reader.IsStartElement("speed"))
            {
                this.Speed = reader.ReadElementContentAsFloat();
                if (this.Speed < 0.0F)
                {
                    throw new XmlException("Storm windspeed is negative.");
                }
            }
            else if (reader.IsStartElement("duration"))
            {
                this.Duration = reader.ReadElementContentAsFloat();
                if (this.Duration < 0.0F)
                {
                    throw new XmlException("Storm duration is negative.");
                }
            }
            else if (reader.IsStartElement("topoGridFile"))
            {
                this.TopoGridFile = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("factorEdge"))
            {
                this.FactorEdge = reader.ReadElementContentAsFloat();
                if (this.FactorEdge < 0.0F)
                {
                    throw new XmlException("Edge widspeed factor is negative.");
                }
            }
            else if (reader.IsStartElement("edgeDetectionThreshold"))
            {
                this.EdgeDetectionThreshold = reader.ReadElementContentAsFloat();
                if (this.EdgeDetectionThreshold < 0.0F)
                {
                    throw new XmlException("Height difference threshold for wind edge effects is negative.");
                }
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
                if (this.EdgeAgeBaseValue < 0)
                {
                    throw new XmlException("Edge age is negative.");
                }
            }
            else if (reader.IsStartElement("edgeBackgroundProbability"))
            {
                this.EdgeBackgroundProbability = reader.ReadElementContentAsFloat();
                if ((this.EdgeBackgroundProbability < 0.0F) || (this.EdgeBackgroundProbability > 1.0F))
                {
                    throw new XmlException("Edge background probability is negative or greater than 1.0.");
                }
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
