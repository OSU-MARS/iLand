using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Wind : Enablable
    {
        [XmlElement(ElementName = "speciesParameter")]
        public string SpeciesParameter { get; set; }

        [XmlElement(ElementName = "soilFreezeMode")]
        public string SoilFreezeMode { get; set; }

        [XmlElement(ElementName = "triggeredByTimeEvent")]
        public bool TriggeredByTimeEvent { get; set; }

        [XmlElement(ElementName = "durationPerIteration")]
        public int DurationPerIteration { get; set; }

        [XmlElement(ElementName = "gustModifier")]
        public double GustModifier { get; set; }

        [XmlElement(ElementName = "topoModifier")]
        public double TopoModifier { get; set; }

        [XmlElement(ElementName = "directionVariation")]
        public double DirectionVariation { get; set; }

        [XmlElement(ElementName = "direction")]
        public double Direction { get; set; }

        [XmlElement(ElementName = "dayOfYear")]
        public int DayOfYear { get; set; }

        [XmlElement(ElementName = "speed")]
        public double Speed { get; set; }

        [XmlElement(ElementName = "duration")]
        public double Duration { get; set; }

        [XmlElement(ElementName = "topoGridFile")]
        public string TopoGridFile { get; set; }

        [XmlElement(ElementName = "factorEdge")]
        public double FactorEdge { get; set; }

        [XmlElement(ElementName = "edgeDetectionThreshold")]
        public double EdgeDetectionThreshold { get; set; }

        [XmlElement(ElementName = "topexModifierType")]
        public string TopexModifierType { get; set; }

        [XmlElement(ElementName = "LRITransferFunction")]
        public string LriTransferFunction { get; set; }

        [XmlElement(ElementName = "edgeProbability")]
        public string EdgeProbability { get; set; }

        [XmlElement(ElementName = "edgeAgeBaseValue")]
        public int EdgeAgeBaseValue { get; set; }

        [XmlElement(ElementName = "edgeBackgroundProbability")]
        public double EdgeBackgroundProbability { get; set; }

        [XmlElement(ElementName = "onAfterWind")]
        public string OnAfterWind { get; set; }
    }
}
