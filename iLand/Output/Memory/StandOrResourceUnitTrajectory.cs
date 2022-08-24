using iLand.Extensions;
using iLand.Tree;

namespace iLand.Output.Memory
{
    public class StandOrResourceUnitTrajectory
    {
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

        public int CapacityInYears
        {
            get { return this.AverageDbhByYear.Length; }
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
            int newCapacity = this.CapacityInYears + Constant.Data.DefaultAnnualAllocationIncrement;

            this.AverageDbhByYear = this.AverageDbhByYear.Resize(newCapacity);
            this.AverageHeightByYear = this.AverageHeightByYear.Resize(newCapacity);
            this.BasalAreaByYear = this.BasalAreaByYear.Resize(newCapacity);
            this.LeafAreaIndexByYear = this.LeafAreaIndexByYear.Resize(newCapacity);
            this.LiveStemVolumeByYear = this.LiveStemVolumeByYear.Resize(newCapacity);
            this.TreeNppAbovegroundByYear = this.TreeNppAbovegroundByYear.Resize(newCapacity);
            this.TreeNppByYear = this.TreeNppByYear.Resize(newCapacity);
            this.TreesPerHectareByYear = this.TreesPerHectareByYear.Resize(newCapacity);

            this.SaplingCohortsPerHectareByYear = this.SaplingCohortsPerHectareByYear.Resize(newCapacity);
            this.SaplingMeanAgeByYear = this.SaplingMeanAgeByYear.Resize(newCapacity);
            this.SaplingNppByYear = this.SaplingNppByYear.Resize(newCapacity);
            this.SaplingsPerHectareByYear = this.SaplingsPerHectareByYear.Resize(newCapacity);

            this.BranchCarbonByYear = this.BranchCarbonByYear.Resize(newCapacity);
            this.BranchNitrogenByYear = this.BranchNitrogenByYear.Resize(newCapacity);
            this.CoarseRootCarbonByYear = this.CoarseRootCarbonByYear.Resize(newCapacity);
            this.CoarseRootNitrogenByYear = this.CoarseRootNitrogenByYear.Resize(newCapacity);
            this.FineRootCarbonByYear = this.FineRootCarbonByYear.Resize(newCapacity);
            this.FineRootNitrogenByYear = this.FineRootNitrogenByYear.Resize(newCapacity);
            this.FoliageCarbonByYear = this.FoliageCarbonByYear.Resize(newCapacity);
            this.FoliageNitrogenByYear = this.FoliageNitrogenByYear.Resize(newCapacity);
            this.RegenerationCarbonByYear = this.RegenerationCarbonByYear.Resize(newCapacity);
            this.RegenerationNitrogenByYear = this.RegenerationNitrogenByYear.Resize(newCapacity);
            this.StemCarbonByYear = this.StemCarbonByYear.Resize(newCapacity);
            this.StemNitrogenByYear = this.StemNitrogenByYear.Resize(newCapacity);
        }
    }
}
