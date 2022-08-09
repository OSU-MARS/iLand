using iLand.Tree;
using System.Collections.Generic;

namespace iLand.Output
{
    public abstract class StandOrResourceUnitTrajectory
    {
        public List<float> AverageDbhByYear { get; private init; } // average dbh (cm)
        public List<float> AverageHeightByYear { get; private init; } // average tree height (m)
        public List<float> BasalAreaByYear { get; private init; } // sum of basal area of all trees (m²/ha)
        public List<float> LeafAreaIndexByYear { get; private init; } // [m²/m²]/ha stocked area.
        public List<float> LiveStemVolumeByYear { get; private init; } // sum of trees' stemp volume (m³/ha)
        public List<float> TreeNppAbovegroundByYear { get; private init; } // above ground NPP (kg biomass increment)/ha
        public List<float> TreeNppByYear { get; private init; } // sum of NPP (kg biomass increment, above+belowground, trees >4m)/ha
        public List<float> TreesPerHectareByYear { get; private init; }

        public List<float> SaplingMeanAgeByYear { get; private init; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public List<float> SaplingCohortsPerHectareByYear { get; private init; } // number of cohorts of saplings/ha
        public List<float> SaplingNppByYear { get; private init; } // carbon gain of saplings (kg biomass increment)/ha
        // number of saplings (Reineke)
        public List<float> SaplingsPerHectareByYear { get; private init; } // number individuals in regeneration layer (represented by "cohortCount" cohorts)/ha

        // carbon/nitrogen cycle
        public List<float> BranchCarbonByYear { get; private init; } // kg/ha
        public List<float> BranchNitrogenByYear { get; private init; } // kg/ha
        public List<float> CoarseRootCarbonByYear { get; private init; } // kg/ha
        public List<float> CoarseRootNitrogenByYear { get; private init; } // kg/ha
        public List<float> FineRootCarbonByYear { get; private init; } // kg/ha
        public List<float> FineRootNitrogenByYear { get; private init; } // kg/ha
        public List<float> FoliageCarbonByYear { get; private init; } // kg/ha
        public List<float> FoliageNitrogenByYear { get; private init; } // kg/ha
        public List<float> RegenerationCarbonByYear { get; private init; } // kg/ha
        public List<float> RegenerationNitrogenByYear { get; private init; } // kg/ha
        public List<float> StemCarbonByYear { get; private init; } // kg/ha
        public List<float> StemNitrogenByYear { get; private init; } // kg/ha

        protected StandOrResourceUnitTrajectory()
        {
            int defaultCapacityInYears = Constant.Data.AnnualAllocationIncrement;
            this.AverageDbhByYear = new(defaultCapacityInYears);
            this.AverageHeightByYear = new(defaultCapacityInYears);
            this.BasalAreaByYear = new(defaultCapacityInYears);
            this.LeafAreaIndexByYear = new(defaultCapacityInYears);
            this.LiveStemVolumeByYear = new(defaultCapacityInYears);
            this.TreeNppAbovegroundByYear = new(defaultCapacityInYears);
            this.TreeNppByYear = new(defaultCapacityInYears);
            this.TreesPerHectareByYear = new(defaultCapacityInYears);

            this.SaplingCohortsPerHectareByYear = new(defaultCapacityInYears);
            this.SaplingMeanAgeByYear = new(defaultCapacityInYears);
            this.SaplingNppByYear = new(defaultCapacityInYears);
            this.SaplingsPerHectareByYear = new(defaultCapacityInYears);

            this.BranchCarbonByYear = new(defaultCapacityInYears);
            this.BranchNitrogenByYear = new(defaultCapacityInYears);
            this.CoarseRootCarbonByYear = new(defaultCapacityInYears);
            this.CoarseRootNitrogenByYear = new(defaultCapacityInYears);
            this.FineRootCarbonByYear = new(defaultCapacityInYears);
            this.FineRootNitrogenByYear = new(defaultCapacityInYears);
            this.FoliageCarbonByYear = new(defaultCapacityInYears);
            this.FoliageNitrogenByYear = new(defaultCapacityInYears);
            this.RegenerationCarbonByYear = new(defaultCapacityInYears);
            this.RegenerationNitrogenByYear = new(defaultCapacityInYears);
            this.StemCarbonByYear = new(defaultCapacityInYears);
            this.StemNitrogenByYear = new(defaultCapacityInYears);
        }

        public int Years
        {
            get { return this.AverageDbhByYear.Count; }
        }

        protected void AddYear(StandOrResourceUnitTreeStatistics endOfYearLiveTreeStatistics)
        {
            this.AverageDbhByYear.Add(endOfYearLiveTreeStatistics.AverageDbhInCm);
            this.AverageHeightByYear.Add(endOfYearLiveTreeStatistics.AverageHeightInM);
            this.BasalAreaByYear.Add(endOfYearLiveTreeStatistics.BasalAreaInM2PerHa);
            this.LeafAreaIndexByYear.Add(endOfYearLiveTreeStatistics.LeafAreaIndex);
            this.LiveStemVolumeByYear.Add(endOfYearLiveTreeStatistics.StemVolumeInM3PerHa);
            this.TreeNppAbovegroundByYear.Add(endOfYearLiveTreeStatistics.TreeNppPerHaAboveground);
            this.TreeNppByYear.Add(endOfYearLiveTreeStatistics.TreeNppPerHa);
            this.TreesPerHectareByYear.Add(endOfYearLiveTreeStatistics.TreesPerHa);

            this.RegenerationCarbonByYear.Add(endOfYearLiveTreeStatistics.RegenerationCarbonInKgPerHa);
            this.RegenerationNitrogenByYear.Add(endOfYearLiveTreeStatistics.RegenerationNitrogenInKgPerHa);
            this.SaplingCohortsPerHectareByYear.Add(endOfYearLiveTreeStatistics.SaplingCohortsPerHa);
            this.SaplingMeanAgeByYear.Add(endOfYearLiveTreeStatistics.SaplingMeanAgeInYears);
            this.SaplingNppByYear.Add(endOfYearLiveTreeStatistics.SaplingNppPerHa);
            this.SaplingsPerHectareByYear.Add(endOfYearLiveTreeStatistics.SaplingsPerHa);

            this.BranchCarbonByYear.Add(endOfYearLiveTreeStatistics.BranchCarbonInKgPerHa);
            this.BranchNitrogenByYear.Add(endOfYearLiveTreeStatistics.BranchNitrogenInKgPerHa);
            this.CoarseRootCarbonByYear.Add(endOfYearLiveTreeStatistics.CoarseRootCarbonInKgPerHa);
            this.CoarseRootNitrogenByYear.Add(endOfYearLiveTreeStatistics.CoarseRootNitrogenInKgPerHa);
            this.FineRootCarbonByYear.Add(endOfYearLiveTreeStatistics.FineRootCarbonInKgPerHa);
            this.FineRootNitrogenByYear.Add(endOfYearLiveTreeStatistics.FineRootNitrogenInKgPerHa);
            this.FoliageCarbonByYear.Add(endOfYearLiveTreeStatistics.FoliageCarbonInKgPerHa);
            this.FoliageNitrogenByYear.Add(endOfYearLiveTreeStatistics.FoliageNitrogenInKgPerHa);
            this.StemCarbonByYear.Add(endOfYearLiveTreeStatistics.StemCarbonInKgPerHa);
            this.StemNitrogenByYear.Add(endOfYearLiveTreeStatistics.StemNitrogenInKgPerHa);
        }

        public abstract int GetID();
    }
}
