using iLand.Tree;
using iLand.World;
using System.Collections.Generic;

namespace iLand.Test
{
    internal class ObservedResourceUnitTrees
    {
        public Dictionary<int, float> DiameterInCmByTag { get; private init; }
        public Dictionary<int, float> HeightInMByTag { get; private init; }

        public ObservedResourceUnitTrees()
        {
            this.DiameterInCmByTag = new();
            this.HeightInMByTag = new();
        }

        public void ObserveResourceUnit(ResourceUnit resourceUnit)
        {
            this.DiameterInCmByTag.Clear();
            this.HeightInMByTag.Clear();
            foreach (Trees treesOfSpecies in resourceUnit.Trees.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    this.DiameterInCmByTag.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Dbh[treeIndex]);
                    this.HeightInMByTag.Add(treesOfSpecies.Tag[treeIndex], treesOfSpecies.Height[treeIndex]);
                }
            }
        }
    }
}
