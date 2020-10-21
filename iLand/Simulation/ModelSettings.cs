using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Simulation
{
    public class ModelSettings
    {
        public int CurrentYear { get; set; }

        // list of settings
        // general on/off switches
        public bool GrowthEnabled { get; private set; } // if false, trees will apply/read light patterns, but do not grow
        public bool MortalityEnabled { get; private set; } // if false, no natural (intrinsic+stress) mortality occurs
        public bool RegenerationEnabled { get; set; } // if true, seed dispersal, establishment, ... is modelled
        public bool CarbonCycleEnabled { get; set; } // if true, snag dynamics and soil CN cycle is modelled
        // light
        public float LightExtinctionCoefficient { get; private set; } // "k" parameter (beer lambert) used for calc. of absorbed light on resourceUnit level
        public float LightExtinctionCoefficientOpacity { get; private set; } // "k" for beer lambert used for opacity of single trees
        public bool IsTorus { get; private set; } // special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
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

        public ModelSettings()
        {
            this.CurrentYear = 0;
            // all other members set in LoadModelSettings()
        }

        public void LoadModelSettings(Model model)
        {
            BrowsingPressure = model.Project.Model.Settings.Browsing.BrowsingPressure;
            GrowthEnabled = model.Project.Model.Settings.GrowthEnabled;
            MortalityEnabled = model.Project.Model.Settings.MortalityEnabled;
            LightExtinctionCoefficient = model.Project.Model.Settings.LightExtinctionCoefficient;
            LightExtinctionCoefficientOpacity = model.Project.Model.Settings.LightExtinctionCoefficientOpacity;
            TemperatureTau = model.Project.Model.Settings.TemperatureTau;
            Epsilon = model.Project.Model.Settings.Epsilon;
            AirDensity = model.Project.Model.Settings.AirDensity;
            LaiThresholdForClosedStands = model.Project.Model.Settings.LaiThresholdForClosedStands;
            BoundaryLayerConductance = model.Project.Model.Settings.BoundaryLayerConductance;
            RecruitmentVariation = model.Project.Model.Settings.SeedDispersal.RecruitmentDimensionVariation;
            UseParFractionBelowGroundAllocation = model.Project.Model.Settings.UseParFractionBelowGroundAllocation;

            Latitude = Global.ToRadians(model.Project.Model.World.Latitude);
            
            IsTorus = model.Project.Model.Parameter.Torus;
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
