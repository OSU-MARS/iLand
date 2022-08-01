using iLand.Tree;
using System.Collections.Generic;

namespace iLand.Output
{
    public class StandOrResourceUnitTrajectory
    {
        public List<float> AverageDbhByYear { get; private init; } // average dbh (cm)
        public List<float> AverageHeightByYear { get; private init; } // average tree height (m)
        public List<float> BasalAreaByYear { get; private init; } // sum of basal area of all trees (m2/ha)
        public List<float> LeafAreaIndexByYear { get; private init; } // [m2/m2]/ha stocked area.
        public List<float> LiveStemVolumeByYear { get; private init; } // sum of tree volume (m3/ha)
        public List<float> TreeNppByYear { get; private init; } // sum. of NPP (kg Biomass increment, above+belowground, trees >4m)/ha
        public List<float> TreeNppAbovegroundByYear { get; private init; } // above ground NPP (kg Biomass increment)/ha
        public List<float> TreesPerHectareByYear { get; private init; }

        public List<float> CohortsPerHectareByYear { get; private init; } // number of cohorts of saplings / ha
        public List<float> MeanSaplingAgeByYear { get; private init; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public List<float> SaplingNppByYear { get; private init; } // carbon gain of saplings (kg Biomass increment)/ha
        // number of sapling (Reinekes Law)
        public List<float> SaplingsPerHectareByYear { get; private init; } // number individuals in regeneration layer (represented by "cohortCount" cohorts) N/ha

        // carbon/nitrogen cycle
        public List<float> BranchCarbonByYear { get; private init; }
        public List<float> BranchNitrogenByYear { get; private init; }
        public List<float> CoarseRootCarbonByYear { get; private init; }
        public List<float> CoarseRootNitrogenByYear { get; private init; }
        public List<float> FineRootCarbonByYear { get; private init; }
        public List<float> FineRootNitrogenByYear { get; private init; }
        public List<float> FoliageCarbonByYear { get; private init; }
        public List<float> FoliageNitrogenByYear { get; private init; }
        public List<float> RegenerationCarbonByYear { get; private init; }
        public List<float> RegenerationNitrogenByYear { get; private init; }
        public List<float> StemCarbonByYear { get; private init; }
        public List<float> StemNitrogenByYear { get; private init; }

        protected StandOrResourceUnitTrajectory()
        {
            int defaultCapacityInYears = Constant.Data.AnnualAllocationIncrement;
            this.AverageDbhByYear = new(defaultCapacityInYears);
            this.AverageHeightByYear = new(defaultCapacityInYears);
            this.BasalAreaByYear = new(defaultCapacityInYears);
            this.LeafAreaIndexByYear = new(defaultCapacityInYears);
            this.LiveStemVolumeByYear = new(defaultCapacityInYears);
            this.TreeNppByYear = new(defaultCapacityInYears);
            this.TreeNppAbovegroundByYear = new(defaultCapacityInYears);
            this.TreesPerHectareByYear = new(defaultCapacityInYears);

            this.CohortsPerHectareByYear = new(defaultCapacityInYears);
            this.MeanSaplingAgeByYear = new(defaultCapacityInYears);
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

        protected void AddYear(StandOrResourceUnitTreeStatistics endOfYearLiveTreeStatistics)
        {
            this.AverageDbhByYear.Add(endOfYearLiveTreeStatistics.AverageDbhInCm);
            this.AverageHeightByYear.Add(endOfYearLiveTreeStatistics.AverageHeightInM);
            this.BasalAreaByYear.Add(endOfYearLiveTreeStatistics.BasalAreaInM2PerHa);
            this.CohortsPerHectareByYear.Add(endOfYearLiveTreeStatistics.CohortsPerHa);
            this.LeafAreaIndexByYear.Add(endOfYearLiveTreeStatistics.LeafAreaIndex);
            this.LiveStemVolumeByYear.Add(endOfYearLiveTreeStatistics.StemVolumeInM3PerHa);
            this.MeanSaplingAgeByYear.Add(endOfYearLiveTreeStatistics.MeanSaplingAgeInYears);
            this.TreeNppByYear.Add(endOfYearLiveTreeStatistics.TreeNppPerHa);
            this.TreeNppAbovegroundByYear.Add(endOfYearLiveTreeStatistics.TreeNppPerHaAboveground);
            this.TreesPerHectareByYear.Add(endOfYearLiveTreeStatistics.TreesPerHa);

            this.RegenerationCarbonByYear.Add(endOfYearLiveTreeStatistics.RegenerationCarbonInKgPerHa);
            this.RegenerationNitrogenByYear.Add(endOfYearLiveTreeStatistics.RegenerationNitrogenInKgPerHa);
            this.SaplingsPerHectareByYear.Add(endOfYearLiveTreeStatistics.SaplingsPerHa);
            this.SaplingNppByYear.Add(endOfYearLiveTreeStatistics.SaplingNppPerHa);

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
    }
}
