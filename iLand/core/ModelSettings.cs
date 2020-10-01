using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace iLand.Core
{
    public class ModelSettings
    {
        // list of settings
        // general on/off switches
        public bool GrowthEnabled { get; private set; } ///< if false, trees will apply/read light patterns, but do not grow
        public bool MortalityEnabled { get; private set; } ///< if false, no natural (intrinsic+stress) mortality occurs
        public bool RegenerationEnabled { get; set; } ///< if true, seed dispersal, establishment, ... is modelled
        public bool CarbonCycleEnabled { get; set; } ///< if true, snag dynamics and soil CN cycle is modelled
        // light
        public double LightExtinctionCoefficient { get; private set; } ///< "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
        public double LightExtinctionCoefficientOpacity { get; private set; } ///< "k" for beer lambert used for opacity of single trees
        public bool TorusMode { get; private set; } ///< special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
        // climate
        public double TemperatureTau { get; private set; } ///< "tau"-value for delayed temperature calculation acc. to Maekela 2008
        // water
        public double AirDensity { get; private set; } // density of air [kg / m3]
        public double LaiThresholdForClosedStands { get; private set; } // for calculation of max-canopy-conductance
        public double BoundaryLayerConductance { get; private set; } // 3pg-evapotranspiration
                                                // nitrogen and soil model
        public bool UseDynamicAvailableNitrogen { get; private set; } ///< if true, iLand utilizes the dynamically calculated NAvailable
        // site variables (for now!)
        public double Latitude { get; private set; } ///< latitude of project site in radians
        // production
        public double Epsilon { get; private set; } ///< maximum light use efficency used for the 3PG model
        public bool UseParFractionBelowGroundAllocation { get; private set; } ///< if true, the 'correct' version of the calculation of belowground allocation is used (default=true)

        public void LoadModelSettings()
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.Instance.Settings.Node("model.settings"));
            if (xml.IsValid() == false)
            {
                throw new XmlException("/project/model/settings element not found in project file.");
            }
            GrowthEnabled = xml.GetBool("growthEnabled", true);
            MortalityEnabled = xml.GetBool("mortalityEnabled", true);
            LightExtinctionCoefficient = xml.GetDouble("lightExtinctionCoefficient", 0.5);
            LightExtinctionCoefficientOpacity = xml.GetDouble("lightExtinctionCoefficientOpacity", 0.5);
            TemperatureTau = xml.GetDouble("temperatureTau", 5);
            Epsilon = xml.GetDouble("epsilon", 1.8); // max light use efficiency (aka alpha_c)
            AirDensity = xml.GetDouble("airDensity", 1.2);
            LaiThresholdForClosedStands = xml.GetDouble("laiThresholdForClosedStands", 3.0);
            BoundaryLayerConductance = xml.GetDouble("boundaryLayerConductance", 0.2);
            XmlHelper world = new XmlHelper(GlobalSettings.Instance.Settings.Node("model.world"));
            Latitude = Global.ToRadians(world.GetDouble("latitude", 48.0));
            UseParFractionBelowGroundAllocation = xml.GetBool("usePARFractionBelowGroundAllocation", true);
            //useDynamicAvailableNitrogen = xml.valueBool("model.settings.soil.useDynamicAvailableNitrogen", false); // TODO: there is a bug in using a xml helper that whose top-node is set
            UseDynamicAvailableNitrogen = GlobalSettings.Instance.Settings.GetBool("model.settings.soil.useDynamicAvailableNitrogen", false);
            TorusMode = GlobalSettings.Instance.Settings.GetBooleanParameter("torus", false);
        }

        public void Print()
        {
            if (GlobalSettings.Instance.LogDebug() == false)
            {
                return;
            }

            List<string> set = new List<string>() { "Settings:",
                                                    String.Format("growthEnabled={0}", GrowthEnabled),
                                                    String.Format("mortalityEnabled={0}", MortalityEnabled),
                                                    String.Format("lightExtinctionCoefficient={0}", LightExtinctionCoefficient),
                                                    String.Format("lightExtinctionCoefficientOpacity={0}", LightExtinctionCoefficientOpacity),
                                                    String.Format("temperatureTau={0}", TemperatureTau),
                                                    String.Format("epsilon={0}", Epsilon),
                                                    String.Format("airDensity={0}", AirDensity),
                                                    String.Format("useDynamicAvailableNitrogen={0}", UseDynamicAvailableNitrogen),
                                                    String.Format("latitude={0}", Global.ToDegrees(Latitude)) };
            Debug.WriteLine(String.Join(System.Environment.NewLine, set));
        }
    }
}
