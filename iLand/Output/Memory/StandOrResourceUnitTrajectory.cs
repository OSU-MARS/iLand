using iLand.Extensions;
using iLand.Tree;

namespace iLand.Output.Memory
{
    public class StandOrResourceUnitTrajectory
    {
        public int CapacityInYears { get; private set; }
        public int LengthInYears { get; protected set; }

        public float[] AverageDbhByYear { get; private set; } // average dbh (cm)
        public float[] AverageHeightByYear { get; private set; } // average tree height (m)
        public float[] BasalAreaByYear { get; private set; } // sum of basal area of all trees (m²/ha)
        public float[] LeafAreaIndexByYear { get; private set; } // [m²/m²]/ha stocked area.
        public float[] LiveStemVolumeByYear { get; private set; } // sum of trees' stemp volume (m³/ha)
        public float[] TreeNppAbovegroundByYear { get; private set; } // above ground NPP (kg biomass increment)/ha
        public float[] TreeNppByYear { get; private set; } // sum of NPP (kg biomass increment, above+belowground, trees >4m)/ha
        public float[] TreesPerHectareByYear { get; private set; }

        public float[] SaplingMeanAgeByYear { get; private set; } // average age of sapling (currenty not weighted with represented sapling numbers...)
        public float[] SaplingCohortsPerHectareByYear { get; private set; } // number of cohorts of saplings/ha
        public float[] SaplingNppByYear { get; private set; } // carbon gain of saplings (kg biomass increment)/ha
        // number of saplings (Reineke)
        public float[] SaplingsPerHectareByYear { get; private set; } // number individuals in regeneration layer (represented by "cohortCount" cohorts)/ha
        
        // carbon/nitrogen cycle
        public float[] BranchCarbonByYear { get; private set; } // kg/ha
        public float[] BranchNitrogenByYear { get; private set; } // kg/ha
        public float[] CoarseRootCarbonByYear { get; private set; } // kg/ha
        public float[] CoarseRootNitrogenByYear { get; private set; } // kg/ha
        public float[] FineRootCarbonByYear { get; private set; } // kg/ha
        public float[] FineRootNitrogenByYear { get; private set; } // kg/ha
        public float[] FoliageCarbonByYear { get; private set; } // kg/ha
        public float[] FoliageNitrogenByYear { get; private set; } // kg/ha
        public float[] RegenerationCarbonByYear { get; private set; } // kg/ha
        public float[] RegenerationNitrogenByYear { get; private set; } // kg/ha
        public float[] StemCarbonByYear { get; private set; } // kg/ha
        public float[] StemNitrogenByYear { get; private set; } // kg/ha

        protected StandOrResourceUnitTrajectory(int initialCapacityInYears)
        {
            this.CapacityInYears = initialCapacityInYears;
            this.LengthInYears = 0;

            this.AverageDbhByYear = new float[initialCapacityInYears];
            this.AverageHeightByYear = new float[initialCapacityInYears];
            this.BasalAreaByYear = new float[initialCapacityInYears];
            this.LeafAreaIndexByYear = new float[initialCapacityInYears];
            this.LiveStemVolumeByYear = new float[initialCapacityInYears];
            this.TreeNppAbovegroundByYear = new float[initialCapacityInYears];
            this.TreeNppByYear = new float[initialCapacityInYears];
            this.TreesPerHectareByYear = new float[initialCapacityInYears];

            this.SaplingCohortsPerHectareByYear = new float[initialCapacityInYears];
            this.SaplingMeanAgeByYear = new float[initialCapacityInYears];
            this.SaplingNppByYear = new float[initialCapacityInYears];
            this.SaplingsPerHectareByYear = new float[initialCapacityInYears];

            this.BranchCarbonByYear = new float[initialCapacityInYears];
            this.BranchNitrogenByYear = new float[initialCapacityInYears];
            this.CoarseRootCarbonByYear = new float[initialCapacityInYears];
            this.CoarseRootNitrogenByYear = new float[initialCapacityInYears];
            this.FineRootCarbonByYear = new float[initialCapacityInYears];
            this.FineRootNitrogenByYear = new float[initialCapacityInYears];
            this.FoliageCarbonByYear = new float[initialCapacityInYears];
            this.FoliageNitrogenByYear = new float[initialCapacityInYears];
            this.RegenerationCarbonByYear = new float[initialCapacityInYears];
            this.RegenerationNitrogenByYear = new float[initialCapacityInYears];
            this.StemCarbonByYear = new float[initialCapacityInYears];
            this.StemNitrogenByYear = new float[initialCapacityInYears];
        }

        protected void AddYear(StandOrResourceUnitTreeStatistics endOfYearLiveTreeStatistics)
        {
            if (this.LengthInYears == this.CapacityInYears)
            {
                this.Extend();
            }

            int addIndex = this.LengthInYears;
            this.AverageDbhByYear[addIndex] = endOfYearLiveTreeStatistics.AverageDbhInCm;
            this.AverageHeightByYear[addIndex] = endOfYearLiveTreeStatistics.AverageHeightInM;
            this.BasalAreaByYear[addIndex] = endOfYearLiveTreeStatistics.BasalAreaInM2PerHa;
            this.LeafAreaIndexByYear[addIndex] = endOfYearLiveTreeStatistics.LeafAreaIndex;
            this.LiveStemVolumeByYear[addIndex] = endOfYearLiveTreeStatistics.StemVolumeInM3PerHa;
            this.TreeNppAbovegroundByYear[addIndex] = endOfYearLiveTreeStatistics.TreeNppPerHaAboveground;
            this.TreeNppByYear[addIndex] = endOfYearLiveTreeStatistics.TreeNppPerHa;
            this.TreesPerHectareByYear[addIndex] = endOfYearLiveTreeStatistics.TreesPerHa;

            this.RegenerationCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.RegenerationCarbonInKgPerHa;
            this.RegenerationNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.RegenerationNitrogenInKgPerHa;
            this.SaplingCohortsPerHectareByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingCohortsPerHa;
            this.SaplingMeanAgeByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingMeanAgeInYears;
            this.SaplingNppByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingNppPerHa;
            this.SaplingsPerHectareByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingsPerHa;

            this.BranchCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.BranchCarbonInKgPerHa;
            this.BranchNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.BranchNitrogenInKgPerHa;
            this.CoarseRootCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.CoarseRootCarbonInKgPerHa;
            this.CoarseRootNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.CoarseRootNitrogenInKgPerHa;
            this.FineRootCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.FineRootCarbonInKgPerHa;
            this.FineRootNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.FineRootNitrogenInKgPerHa;
            this.FoliageCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.FoliageCarbonInKgPerHa;
            this.FoliageNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.FoliageNitrogenInKgPerHa;
            this.StemCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.StemCarbonInKgPerHa;
            this.StemNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.StemNitrogenInKgPerHa;

            ++this.LengthInYears;
        }

        protected void Extend()
        {
            this.CapacityInYears += Constant.Data.AnnualAllocationIncrement;

            this.AverageDbhByYear = this.AverageDbhByYear.Resize(this.CapacityInYears);
            this.AverageHeightByYear = this.AverageHeightByYear.Resize(this.CapacityInYears);
            this.BasalAreaByYear = this.BasalAreaByYear.Resize(this.CapacityInYears);
            this.LeafAreaIndexByYear = this.LeafAreaIndexByYear.Resize(this.CapacityInYears);
            this.LiveStemVolumeByYear = this.LiveStemVolumeByYear.Resize(this.CapacityInYears);
            this.TreeNppAbovegroundByYear = this.TreeNppAbovegroundByYear.Resize(this.CapacityInYears);
            this.TreeNppByYear = this.TreeNppByYear.Resize(this.CapacityInYears);
            this.TreesPerHectareByYear = this.TreesPerHectareByYear.Resize(this.CapacityInYears);

            this.SaplingCohortsPerHectareByYear = this.SaplingCohortsPerHectareByYear.Resize(this.CapacityInYears);
            this.SaplingMeanAgeByYear = this.SaplingMeanAgeByYear.Resize(this.CapacityInYears);
            this.SaplingNppByYear = this.SaplingNppByYear.Resize(this.CapacityInYears);
            this.SaplingsPerHectareByYear = this.SaplingsPerHectareByYear.Resize(this.CapacityInYears);

            this.BranchCarbonByYear = this.BranchCarbonByYear.Resize(this.CapacityInYears);
            this.BranchNitrogenByYear = this.BranchNitrogenByYear.Resize(this.CapacityInYears);
            this.CoarseRootCarbonByYear = this.CoarseRootCarbonByYear.Resize(this.CapacityInYears);
            this.CoarseRootNitrogenByYear = this.CoarseRootNitrogenByYear.Resize(this.CapacityInYears);
            this.FineRootCarbonByYear = this.FineRootCarbonByYear.Resize(this.CapacityInYears);
            this.FineRootNitrogenByYear = this.FineRootNitrogenByYear.Resize(this.CapacityInYears);
            this.FoliageCarbonByYear = this.FoliageCarbonByYear.Resize(this.CapacityInYears);
            this.FoliageNitrogenByYear = this.FoliageNitrogenByYear.Resize(this.CapacityInYears);
            this.RegenerationCarbonByYear = this.RegenerationCarbonByYear.Resize(this.CapacityInYears);
            this.RegenerationNitrogenByYear = this.RegenerationNitrogenByYear.Resize(this.CapacityInYears);
            this.StemCarbonByYear = this.StemCarbonByYear.Resize(this.CapacityInYears);
            this.StemNitrogenByYear = this.StemNitrogenByYear.Resize(this.CapacityInYears);
        }
    }
}
