using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace iLand.Simulation
{
    public class ModelSettings
    {
        // list of settings
        // general on/off switches
        public bool GrowthEnabled { get; private set; } // if false, trees will apply/read light patterns, but do not grow
        public bool MortalityEnabled { get; private set; } // if false, no natural (intrinsic+stress) mortality occurs
        public bool RegenerationEnabled { get; set; } // if true, seed dispersal, establishment, ... is modelled
        public bool CarbonCycleEnabled { get; set; } // if true, snag dynamics and soil CN cycle is modelled
        // light
        public double LightExtinctionCoefficient { get; private set; } // "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
        public double LightExtinctionCoefficientOpacity { get; private set; } // "k" for beer lambert used for opacity of single trees
        public bool TorusMode { get; private set; } // special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
        // climate
        public double TemperatureTau { get; private set; } // "tau"-value for delayed temperature calculation acc. to Maekela 2008
        // water
        public double AirDensity { get; private set; } // density of air [kg / m3]
        public double LaiThresholdForClosedStands { get; private set; } // for calculation of max-canopy-conductance
        public double BoundaryLayerConductance { get; private set; } // 3pg-evapotranspiration

        // site variables (for now!)
        public double Latitude { get; private set; } // latitude of project site in radians
        // production
        public double Epsilon { get; private set; } // maximum light use efficency used for the 3PG model
        public bool UseParFractionBelowGroundAllocation { get; private set; } // if true, the 'correct' version of the calculation of belowground allocation is used (default=true)

        public double BrowsingPressure { get; private set; }
        public double RecruitmentVariation { get; private set; }

        public void LoadModelSettings(GlobalSettings globalSettings)
        {
            XmlHelper xml = new XmlHelper(globalSettings.Settings.Node("model.settings"));
            if (xml.IsValid() == false)
            {
                throw new XmlException("/project/model/settings element not found in project file.");
            }
            BrowsingPressure = xml.GetDoubleFromXml("browsing.browsingPressure", 0.0);
            GrowthEnabled = xml.GetBooleanFromXml("growthEnabled", true);
            MortalityEnabled = xml.GetBooleanFromXml("mortalityEnabled", true);
            LightExtinctionCoefficient = xml.GetDoubleFromXml("lightExtinctionCoefficient", 0.5);
            LightExtinctionCoefficientOpacity = xml.GetDoubleFromXml("lightExtinctionCoefficientOpacity", 0.5);
            TemperatureTau = xml.GetDoubleFromXml("temperatureTau", 5);
            Epsilon = xml.GetDoubleFromXml("epsilon", 1.8); // max light use efficiency (aka alpha_c)
            AirDensity = xml.GetDoubleFromXml("airDensity", 1.2);
            LaiThresholdForClosedStands = xml.GetDoubleFromXml("laiThresholdForClosedStands", 3.0);
            BoundaryLayerConductance = xml.GetDoubleFromXml("boundaryLayerConductance", 0.2);
            RecruitmentVariation = xml.GetDoubleFromXml("seedDispersal.recruitmentDimensionVariation", 0.1); // +/- 10%
            UseParFractionBelowGroundAllocation = xml.GetBooleanFromXml("usePARFractionBelowGroundAllocation", true);

            Latitude = Global.ToRadians(globalSettings.Settings.GetDoubleFromXml("model.world.latitude", 48.0));
            
            TorusMode = globalSettings.Settings.GetBooleanParameter("torus", false);
        }

        public void Print()
        {
            List<string> set = new List<string>() { "Settings:",
                                                    String.Format("growthEnabled={0}", GrowthEnabled),
                                                    String.Format("mortalityEnabled={0}", MortalityEnabled),
                                                    String.Format("lightExtinctionCoefficient={0}", LightExtinctionCoefficient),
                                                    String.Format("lightExtinctionCoefficientOpacity={0}", LightExtinctionCoefficientOpacity),
                                                    String.Format("temperatureTau={0}", TemperatureTau),
                                                    String.Format("epsilon={0}", Epsilon),
                                                    String.Format("airDensity={0}", AirDensity),
                                                    String.Format("latitude={0}", Global.ToDegrees(Latitude)) };
            Debug.WriteLine(String.Join(System.Environment.NewLine, set));
        }
    }
}
