using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    internal class ModelSettings
    {
        // list of settings
        // general on/off switches
        public bool growthEnabled; ///< if false, trees will apply/read light patterns, but do not grow
        public bool mortalityEnabled; ///< if false, no natural (intrinsic+stress) mortality occurs
        public bool regenerationEnabled; ///< if true, seed dispersal, establishment, ... is modelled
        public bool carbonCycleEnabled; ///< if true, snag dynamics and soil CN cycle is modelled
        // light
        public double lightExtinctionCoefficient; ///< "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
        public double lightExtinctionCoefficientOpacity; ///< "k" for beer lambert used for opacity of single trees
        public bool torusMode; ///< special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
        // climate
        public double temperatureTau; ///< "tau"-value for delayed temperature calculation acc. to Maekela 2008
        // water
        public double airDensity; // density of air [kg / m3]
        public double laiThresholdForClosedStands; // for calculation of max-canopy-conductance
        public double boundaryLayerConductance; // 3pg-evapotranspiration
                                                // nitrogen and soil model
        public bool useDynamicAvailableNitrogen; ///< if true, iLand utilizes the dynamically calculated NAvailable
        // site variables (for now!)
        public double latitude; ///< latitude of project site in radians
        // production
        public double epsilon; ///< maximum light use efficency used for the 3PG model
        public bool usePARFractionBelowGroundAllocation; ///< if true, the 'correct' version of the calculation of belowground allocation is used (default=true)

        public ModelSettings()
        {
        }

        public void loadModelSettings()
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.settings"));
            growthEnabled = xml.valueBool("growthEnabled", true);
            mortalityEnabled = xml.valueBool("mortalityEnabled", true);
            lightExtinctionCoefficient = xml.valueDouble("lightExtinctionCoefficient", 0.5);
            lightExtinctionCoefficientOpacity = xml.valueDouble("lightExtinctionCoefficientOpacity", 0.5);
            temperatureTau = xml.valueDouble("temperatureTau", 5);
            epsilon = xml.valueDouble("epsilon", 1.8); // max light use efficiency (aka alpha_c)
            airDensity = xml.valueDouble("airDensity", 1.2);
            laiThresholdForClosedStands = xml.valueDouble("laiThresholdForClosedStands", 3.0);
            boundaryLayerConductance = xml.valueDouble("boundaryLayerConductance", 0.2);
            XmlHelper world = new XmlHelper(GlobalSettings.instance().settings().node("model.world"));
            latitude = Global.RAD(world.valueDouble("latitude", 48.0));
            usePARFractionBelowGroundAllocation = xml.valueBool("usePARFractionBelowGroundAllocation", true);
            //useDynamicAvailableNitrogen = xml.valueBool("model.settings.soil.useDynamicAvailableNitrogen", false); // TODO: there is a bug in using a xml helper that whose top-node is set
            useDynamicAvailableNitrogen = GlobalSettings.instance().settings().valueBool("model.settings.soil.useDynamicAvailableNitrogen", false);
            torusMode = GlobalSettings.instance().settings().paramValueBool("torus", false);
        }

        public void print()
        {
            if (GlobalSettings.instance().logLevelInfo() == false)
            {
                return;
            }

            List<string> set = new List<string>() { "Settings:",
                                                    String.Format("growthEnabled={0}", growthEnabled),
                                                    String.Format("mortalityEnabled={0}", mortalityEnabled),
                                                    String.Format("lightExtinctionCoefficient={0}", lightExtinctionCoefficient),
                                                    String.Format("lightExtinctionCoefficientOpacity={0}", lightExtinctionCoefficientOpacity),
                                                    String.Format("temperatureTau={0}", temperatureTau),
                                                    String.Format("epsilon={0}", epsilon),
                                                    String.Format("airDensity={0}", airDensity),
                                                    String.Format("useDynamicAvailableNitrogen={0}", useDynamicAvailableNitrogen),
                                                    String.Format("latitude={0}", Global.GRAD(latitude)) };
            Debug.WriteLine(String.Join(System.Environment.NewLine, set));
        }
    }
}
