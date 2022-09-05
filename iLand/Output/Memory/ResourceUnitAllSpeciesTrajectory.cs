using iLand.Tree;

namespace iLand.Output.Memory
{
    // for now, a shim class for object model consistency
    public class ResourceUnitAllSpeciesTrajectory : StandOrResourceUnitTrajectory
    {
        public ResourceUnitAllSpeciesTrajectory(int initialCapacityInYears)
            : base(initialCapacityInYears)
        {
        }
    }
}