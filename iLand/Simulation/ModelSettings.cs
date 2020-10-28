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
        public float TemperatureTau { get; private set; } // "tau"-value for delayed temperature calculation acc. to Maekela 2008
        // water
        public float AirDensity { get; private set; } // density of air [kg / m3]
        public float LaiThresholdForClosedStands { get; private set; } // for calculation of max-canopy-conductance
        public float BoundaryLayerConductance { get; private set; } // 3pg-evapotranspiration

        // site variables (for now!)
        public float Latitude { get; private set; } // latitude of project site in radians
        // production
        public float Epsilon { get; private set; } // maximum light use efficency used for the 3PG model
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
            this.BrowsingPressure = model.Project.Model.Settings.Browsing.BrowsingPressure;
            this.GrowthEnabled = model.Project.Model.Settings.GrowthEnabled;
            this.MortalityEnabled = model.Project.Model.Settings.MortalityEnabled;
            this.LightExtinctionCoefficient = model.Project.Model.Settings.LightExtinctionCoefficient;
            this.LightExtinctionCoefficientOpacity = model.Project.Model.Settings.LightExtinctionCoefficientOpacity;
            this.TemperatureTau = model.Project.Model.Settings.TemperatureTau;
            this.Epsilon = model.Project.Model.Settings.Epsilon;
            this.AirDensity = model.Project.Model.Settings.AirDensity;
            this.LaiThresholdForClosedStands = model.Project.Model.Settings.LaiThresholdForClosedStands;
            this.BoundaryLayerConductance = model.Project.Model.Settings.BoundaryLayerConductance;
            this.RecruitmentVariation = model.Project.Model.Settings.SeedDispersal.RecruitmentDimensionVariation;
            this.UseParFractionBelowGroundAllocation = model.Project.Model.Settings.UseParFractionBelowGroundAllocation;

            this.Latitude = Maths.ToRadians(model.Project.Model.World.Latitude);
            
            this.IsTorus = model.Project.Model.Parameter.Torus;

            // snag dynamics / soil model enabled? (info used during setup of world)
            this.CarbonCycleEnabled = model.Project.Model.Settings.CarbonCycleEnabled;
            this.RegenerationEnabled = model.Project.Model.Settings.RegenerationEnabled;
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
                                                    String.Format("latitude={0}", Maths.ToDegrees(Latitude)) };
            Debug.WriteLine(String.Join(System.Environment.NewLine, set));
        }
    }
}
