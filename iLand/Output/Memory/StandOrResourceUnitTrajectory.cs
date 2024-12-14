using iLand.Extensions;
using iLand.Tree;

namespace iLand.Output.Memory
{
    public class StandOrResourceUnitTrajectory // C++: StandStatistics
    {
        public int LengthInYears { get; protected set; }

        public float[] AverageDbhByYear { get; private set; } // average dbh (cm)
        public float[] AverageHeightByYear { get; private set; } // average tree height (m)
        public float[] LiveStemVolumeByYear { get; private set; } // sum of trees' stemp volume (m³/ha)
        public float[] TreeBasalAreaByYear { get; private set; } // sum of basal area of all trees (m²/ha)
        public float[] TreeLeafAreaIndexByYear { get; private set; } // [m²/m²]/ha stocked area of leaf area on trees > 4m, C++ leafAreaIndex()
        public float[] TreeNppAbovegroundByYear { get; private set; } // above ground NPP (kg biomass increment)/ha
        public float[] TreeNppByYear { get; private set; } // sum of NPP (kg biomass increment, above+belowground, trees > 4m)/ha
        public float[] TreesPerHectareByYear { get; private set; }

        public float[] SaplingBasalAreaByYear { get; private set; } // sum of basal area of all saplings (m²/ha), C++ saplingBasalArea(), mBasalAreaSaplings
        public float[] SaplingLeafAreaIndexByYear { get; private set; } // [m²/m²]/ha stocked area of leaf area on saplings, C++ leafAreaIndexSaplings(), mLAISaplings
        public float[] SaplingMeanAgeByYear { get; private set; } // average age of sapling (currenty not weighted with represented sapling numbers...), C++ mSumSaplingAge
        public float[] SaplingCohortsPerHectareByYear { get; private set; } // number of cohorts of saplings/ha, C++: saplingCount(), mSaplingCount
        public float[] SaplingNppByYear { get; private set; } // carbon gain of saplings (kg biomass increment)/ha, C++: mNPPsaplings
        // number of saplings (Reineke)
        public float[] SaplingsPerHectareByYear { get; private set; } // number individuals in regeneration layer (represented by "cohortCount" cohorts)/ha, C++ cohortCount(), mSaplingCount

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
            this.LiveStemVolumeByYear = new float[initialCapacityInYears];
            this.TreeBasalAreaByYear = new float[initialCapacityInYears];
            this.TreeLeafAreaIndexByYear = new float[initialCapacityInYears];
            this.TreeNppAbovegroundByYear = new float[initialCapacityInYears];
            this.TreeNppByYear = new float[initialCapacityInYears];
            this.TreesPerHectareByYear = new float[initialCapacityInYears];

            this.SaplingBasalAreaByYear = new float[initialCapacityInYears];
            this.SaplingCohortsPerHectareByYear = new float[initialCapacityInYears];
            this.SaplingLeafAreaIndexByYear = new float[initialCapacityInYears];
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

        public void AddBeforeDeathNppOfTree(TreeGrowthData growthData) ///< add only the NPP
        {
            int mostRecentYearIndex = this.LengthInYears - 1;

            // add NPP of trees that died due to mortality
            this.TreeNppByYear[mostRecentYearIndex] += growthData.NppTotal;
            this.TreeNppAbovegroundByYear[mostRecentYearIndex] += growthData.NppAboveground;
        }

        public void AddYear(LiveTreeAndSaplingStatistics endOfYearLiveTreeStatistics)
        {
            if (this.LengthInYears == this.CapacityInYears)
            {
                this.Extend();
            }

            int addIndex = this.LengthInYears;
            this.AverageDbhByYear[addIndex] = endOfYearLiveTreeStatistics.AverageDbhInCm;
            this.AverageHeightByYear[addIndex] = endOfYearLiveTreeStatistics.AverageHeightInM;
            this.LiveStemVolumeByYear[addIndex] = endOfYearLiveTreeStatistics.StemVolumeInM3PerHa;
            this.TreeBasalAreaByYear[addIndex] = endOfYearLiveTreeStatistics.BasalAreaInM2PerHa;
            this.TreeLeafAreaIndexByYear[addIndex] = endOfYearLiveTreeStatistics.LeafAreaIndex;
            this.TreeNppAbovegroundByYear[addIndex] = endOfYearLiveTreeStatistics.TreeNppPerHaAboveground;
            this.TreeNppByYear[addIndex] = endOfYearLiveTreeStatistics.TreeNppPerHa;
            this.TreesPerHectareByYear[addIndex] = endOfYearLiveTreeStatistics.TreesPerHa;

            this.RegenerationCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.RegenerationCarbonInKgPerHa;
            this.RegenerationNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.RegenerationNitrogenInKgPerHa;
            this.SaplingBasalAreaByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingBasalArea;
            this.SaplingCohortsPerHectareByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingCohortsPerHa;
            this.SaplingLeafAreaIndexByYear[addIndex] = endOfYearLiveTreeStatistics.SaplingLeafAreaIndex;
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
            this.StemCarbonByYear[addIndex] = endOfYearLiveTreeStatistics.StemAndReserveCarbonInKgPerHa;
            this.StemNitrogenByYear[addIndex] = endOfYearLiveTreeStatistics.StemAndReserveNitrogenInKgPerHa;

            ++this.LengthInYears;
        }

        protected void Extend()
        {
            int newCapacity = this.CapacityInYears + Constant.Data.DefaultAnnualAllocationIncrement;

            this.AverageDbhByYear = this.AverageDbhByYear.Resize(newCapacity);
            this.AverageHeightByYear = this.AverageHeightByYear.Resize(newCapacity);
            this.LiveStemVolumeByYear = this.LiveStemVolumeByYear.Resize(newCapacity);
            this.TreeBasalAreaByYear = this.TreeBasalAreaByYear.Resize(newCapacity);
            this.TreeLeafAreaIndexByYear = this.TreeLeafAreaIndexByYear.Resize(newCapacity);
            this.TreeNppAbovegroundByYear = this.TreeNppAbovegroundByYear.Resize(newCapacity);
            this.TreeNppByYear = this.TreeNppByYear.Resize(newCapacity);
            this.TreesPerHectareByYear = this.TreesPerHectareByYear.Resize(newCapacity);

            this.SaplingBasalAreaByYear = this.SaplingBasalAreaByYear.Resize(newCapacity);
            this.SaplingCohortsPerHectareByYear = this.SaplingCohortsPerHectareByYear.Resize(newCapacity);
            this.SaplingLeafAreaIndexByYear = this.SaplingLeafAreaIndexByYear.Resize(newCapacity);
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
