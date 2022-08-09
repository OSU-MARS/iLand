using iLand.Tree;
using iLand.World;

namespace iLand.Output
{
    // not a formally necessary type but does guarantee trajectories always capture years from the same resource unit
    public class ResourceUnitTrajectory : StandOrResourceUnitTrajectory
    {
        public ResourceUnit ResourceUnit { get; private init; }

        public ResourceUnitTrajectory(ResourceUnit resourceUnit)
        {
            this.ResourceUnit = resourceUnit;
        }

        public void AddYear()
        {
            ResourceUnitTreeStatistics endOfYearResourceUnitTreeStatistics = this.ResourceUnit.Trees.TreeAndSaplingStatisticsForAllSpecies;
            this.AddYear(endOfYearResourceUnitTreeStatistics);
        }

        public override int GetID()
        {
            return this.ResourceUnit.ID;
        }
    }
}
