using iLand.Tree;
using iLand.World;
using System.Collections.Generic;

namespace iLand.Test
{
    internal class ObservedResourceUnitTrees
    {
        public Dictionary<int, float> DiameterInCmByTreeID { get; private init; }
        public Dictionary<int, float> HeightInMByTreeID { get; private init; }

        public ObservedResourceUnitTrees()
        {
            this.DiameterInCmByTreeID = new();
            this.HeightInMByTreeID = new();
        }

        public void ObserveResourceUnit(ResourceUnit resourceUnit)
        {
            this.DiameterInCmByTreeID.Clear();
            this.HeightInMByTreeID.Clear();
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.DiameterInCmByTreeID.Add(treesOfSpecies.TreeID[treeIndex], treesOfSpecies.DbhInCm[treeIndex]);
                    this.HeightInMByTreeID.Add(treesOfSpecies.TreeID[treeIndex], treesOfSpecies.HeightInM[treeIndex]);
                }
            }
        }
    }
}
