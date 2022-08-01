using iLand.World;

namespace iLand.Tree
{
    public class ResourceUnitTreeSpeciesStatistics : ResourceUnitTreeStatistics
    {
        public ResourceUnitTreeSpecies ResourceUnitSpecies { get; private init; } // species if statistics are species-specific, otherwise null

        public ResourceUnitTreeSpeciesStatistics(ResourceUnit resourceUnit, ResourceUnitTreeSpecies ruSpecies)
            : base(resourceUnit)
        {
            this.ResourceUnitSpecies = ruSpecies;
        }

        public new void OnAdditionsComplete()
        {
            base.OnAdditionsComplete();
        }
    }
}
