using iLand.Tree;

namespace iLand.Output.Memory
{
    public class ResourceUnitTreeSpeciesTrajectory : StandOrResourceUnitTrajectory
    {
        public ResourceUnitTreeSpecies TreeSpecies { get; private init; }

        public ResourceUnitTreeSpeciesTrajectory(ResourceUnitTreeSpecies treeSpecies, int initialCapacityInYears)
            : base(initialCapacityInYears)
        {
            this.TreeSpecies = treeSpecies;
        }

        public void AddYear(ResourceUnitTreeSpecies treeSpecies)
        {
            base.AddYear(treeSpecies.StatisticsLive);
        }

        public void AddYearWithoutSpecies()
        {
            if (this.LengthInYears == this.CapacityInYears)
            {
                this.Extend();
            }

            int addIndex = this.LengthInYears;
            this.AverageDbhByYear[addIndex] = 0.0F;
            this.AverageHeightByYear[addIndex] = 0.0F;
            this.BasalAreaByYear[addIndex] = 0.0F;
            this.LeafAreaIndexByYear[addIndex] = 0.0F;
            this.LiveStemVolumeByYear[addIndex] = 0.0F;
            this.TreeNppAbovegroundByYear[addIndex] = 0.0F;
            this.TreeNppByYear[addIndex] = 0.0F;
            this.TreesPerHectareByYear[addIndex] = 0.0F;

            this.RegenerationCarbonByYear[addIndex] = 0.0F;
            this.RegenerationNitrogenByYear[addIndex] = 0.0F;
            this.SaplingCohortsPerHectareByYear[addIndex] = 0.0F;
            this.SaplingMeanAgeByYear[addIndex] = Constant.NoDataFloat;
            this.SaplingNppByYear[addIndex] = 0.0F;
            this.SaplingsPerHectareByYear[addIndex] = 0.0F;

            this.BranchCarbonByYear[addIndex] = 0.0F;
            this.BranchNitrogenByYear[addIndex] = 0.0F;
            this.CoarseRootCarbonByYear[addIndex] = 0.0F;
            this.CoarseRootNitrogenByYear[addIndex] = 0.0F;
            this.FineRootCarbonByYear[addIndex] = 0.0F;
            this.FineRootNitrogenByYear[addIndex] = 0.0F;
            this.FoliageCarbonByYear[addIndex] = 0.0F;
            this.FoliageNitrogenByYear[addIndex] = 0.0F;
            this.StemCarbonByYear[addIndex] = 0.0F;
            this.StemNitrogenByYear[addIndex] = 0.0F;

            ++this.LengthInYears;
        }
    }
}
