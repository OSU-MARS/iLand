using iLand.Tree;
using System.Diagnostics;

namespace iLand.Output
{
    public class StandTreeStatistics : StandOrResourceUnitTreeStatistics
    {
        private float areaInLandscapeInM2;

        public int StandID { get; private init; }

        public StandTreeStatistics(int standID)
        {
            this.areaInLandscapeInM2 = 0.0F;
            this.StandID = standID;
        }

        public void Add(ResourceUnitTreeStatistics completedResourceUnitTreeStatistics)
        {
            // can't check stand ID for consistency as ResourceUnitTreeStatistics doesn't carry it, though this shim method does
            // provide a hook for checking method calls

            // since statistics at the resource unit level are per hectare they must be accumulated in an area weighted fashion
            float resourceUnitAreaInM2 = completedResourceUnitTreeStatistics.ResourceUnit.AreaInLandscapeInM2;
            this.areaInLandscapeInM2 += resourceUnitAreaInM2;

            // TODO: for area weighting to be completely correct the area stand occupies within the resource unit should be used
            // but this isn't currently available as an iLand input
            this.AverageDbhInCm += resourceUnitAreaInM2 * completedResourceUnitTreeStatistics.AverageDbhInCm;
            this.AverageHeightInM += resourceUnitAreaInM2 * completedResourceUnitTreeStatistics.AverageHeightInM;
            this.MeanSaplingAgeInYears += resourceUnitAreaInM2 * completedResourceUnitTreeStatistics.MeanSaplingAgeInYears;
            base.AddAreaWeighted(completedResourceUnitTreeStatistics, resourceUnitAreaInM2);
        }

        public void OnAdditionsComplete()
        {
            Debug.Assert(this.areaInLandscapeInM2 > 0.0F); // potentially nothing to do if all trees have died out of stand

            // accumulators: no need to change totals
            // this.TotalDbhInCm
            // this.TotalHeightInM
            // this.TotalLeafAreaInM2
            // this.TotalSaplingCohortAgeInYears

            // trees
            this.AverageDbhInCm /= this.areaInLandscapeInM2;
            this.AverageHeightInM /= this.areaInLandscapeInM2;
            this.BasalAreaInM2PerHa /= this.areaInLandscapeInM2;
            this.LeafAreaIndex = this.TotalLeafAreaInM2 / this.areaInLandscapeInM2; // this.ru.AreaWithTrees;
            this.StemVolumeInM3PerHa /= this.areaInLandscapeInM2;
            this.TreesPerHa /= this.areaInLandscapeInM2;
            this.TreeNppPerHa /= this.areaInLandscapeInM2;
            this.TreeNppPerHaAboveground /= this.areaInLandscapeInM2;

            // regeneration
            this.CohortsPerHa /= this.areaInLandscapeInM2;
            this.MeanSaplingAgeInYears /= this.areaInLandscapeInM2;
            this.SaplingsPerHa /= this.areaInLandscapeInM2;
            this.SaplingNppPerHa /= this.areaInLandscapeInM2;

            // carbon/nitrogen pools
            this.BranchCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.BranchNitrogenInKgPerHa /= this.areaInLandscapeInM2;
            this.CoarseRootCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.FineRootCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.FineRootNitrogenInKgPerHa /= this.areaInLandscapeInM2;
            this.CoarseRootNitrogenInKgPerHa /= this.areaInLandscapeInM2;
            this.FoliageCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.FoliageNitrogenInKgPerHa /= this.areaInLandscapeInM2;
            this.RegenerationCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.RegenerationNitrogenInKgPerHa /= this.areaInLandscapeInM2;
            this.StemCarbonInKgPerHa /= this.areaInLandscapeInM2;
            this.StemNitrogenInKgPerHa /= this.areaInLandscapeInM2;
        }

        public override void Zero()
        {
            base.Zero();

            this.areaInLandscapeInM2 = 0.0F;
        }
    }
}
