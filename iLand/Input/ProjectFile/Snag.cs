using iLand.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Snag : XmlSerializable
    {
        public ReadOnlyCollection<float> DecayClassBiomassThresholds { get; private set; } // fraction of initial biomass

        public float DbhBreakpointSmallMedium { get; private set; } // cm
        public float DdhBreakpointMediumLarge { get; private set; } // cm
        public float DbhMinimumForDecayTracking { get; private set; } // cm

        public Snag()
        {
            this.DbhBreakpointSmallMedium = 20.0F; // cm
            this.DdhBreakpointMediumLarge = 100.0F; // cm
            this.DbhMinimumForDecayTracking = 1000.0F; // disabled by default
            this.DecayClassBiomassThresholds = new float[] { 0.2F, 0.4F, 0.7F, 0.9F }.AsReadOnly();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException($"Encountered unexpected attributes on element {reader.Name}.");
            }

            switch (reader.Name)
            {
                case "snag":
                    reader.Read();
                    break;
                case "decayClassBiomassThresholds":
                    this.DecayClassBiomassThresholds = reader.ReadElementContentAsFloatArray().AsReadOnly();
                    float previousThreshold = -Single.Epsilon;
                    for (int index = 0; index < this.DecayClassBiomassThresholds.Count; ++index)
                    {
                        float threshold = this.DecayClassBiomassThresholds[index];
                        if (threshold < 0.0F)
                        {
                            throw new XmlException($"Biomass fraction threshold {threshold} between decay classes {index + 1} and {index + 2} is negative.");
                        }
                        else if (threshold <= previousThreshold)
                        {
                            throw new XmlException($"Biomass fraction threshold {threshold} between decay classes {index + 1} and {index + 2} is not an increase from the previous threshold of {previousThreshold}. Greater decay classes must have higher thresholds indicating a larger amount of decomposition.");
                        }
                        else if (threshold > 1.0F)
                        {
                            throw new XmlException($"Biomass fraction threshold {threshold} between decay classes {index + 1} and {index + 2} is greater than 1.0.");
                        }

                        previousThreshold = threshold;
                    }
                    if (this.DecayClassBiomassThresholds.Count != 4)
                    {
                        throw new XmlException($"Exactly four decay class thresholds must be specified (in increasing order with values between 0 and 1) to separate decay classes 5, 4, 3, 2, and 1 of individually tracked dead trees in terms of the fraction of initial biomass remaining. Instead, {this.DecayClassBiomassThresholds.Count} thresholds were specified.");
                    }
                    break;
                case "smallMediumDbhBreakpoint":
                    this.DbhBreakpointSmallMedium = reader.ReadElementContentAsFloat();
                    if (this.DbhBreakpointSmallMedium < 0.0F)
                    {
                        throw new XmlException("Breakpoint between DBH classes 1 and 2 is negative.");
                    }
                    break;
                case "mediumLargeDbhBreakpoint":
                    this.DdhBreakpointMediumLarge = reader.ReadElementContentAsFloat();
                    if (this.DdhBreakpointMediumLarge < 0.0F)
                    {
                        throw new XmlException("Breakpoint between DBH classes 2 and 3 is negative.");
                    }
                    break;
                case "minimumDbhForDecayTracking":
                    this.DbhMinimumForDecayTracking = reader.ReadElementContentAsFloat();
                    if (this.DbhMinimumForDecayTracking < 0.0F)
                    {
                        throw new XmlException("Minimum DBH for decay class tracking is negative.");
                    }
                    break;
                default:
                    throw new XmlException($"Element '{reader.Name}' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}