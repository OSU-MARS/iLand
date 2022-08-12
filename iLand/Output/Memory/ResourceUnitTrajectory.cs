using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Output.Memory
{
    public class ResourceUnitTrajectory
    {
        public ResourceUnitAllSpeciesTrajectory? AllTreeSpeciesTrajectory { get; private init; }
        public ResourceUnit ResourceUnit { get; private init; }
        public List<ResourceUnitTreeSpeciesTrajectory>? TreeSpeciesTrajectories { get; private init; }

        public ResourceUnitTrajectory(ResourceUnit resourceUnit, ResourceUnitMemoryOutputs resourceUnitOutputs, int initialCapacityInYears)
        {
            this.AllTreeSpeciesTrajectory = resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.AllTreeSpeciesStatistics) ? new(initialCapacityInYears) : null;
            this.ResourceUnit = resourceUnit;

            if (resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.IndividualTreeSpeciesStatistics))
            {
                IList<Trees> treesOnResourceUnit = resourceUnit.Trees.TreesBySpeciesID.Values;
                this.TreeSpeciesTrajectories = new(treesOnResourceUnit.Count);

                // for now, instantiate trajectories only for trees species which are initially present on the resource unit
                for (int treeSpeciesIndex = 0; treeSpeciesIndex < treesOnResourceUnit.Count; ++treeSpeciesIndex)
                {
                    TreeSpecies treeSpecies = treesOnResourceUnit[treeSpeciesIndex].Species;
                    ResourceUnitTreeSpecies resourceUnitTreeSpecies = resourceUnit.Trees.GetResourceUnitSpecies(treeSpecies);
                    this.TreeSpeciesTrajectories.Add(new ResourceUnitTreeSpeciesTrajectory(resourceUnitTreeSpecies, initialCapacityInYears));
                }
            }
            else 
            {
                this.TreeSpeciesTrajectories = null;
            }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.AllTreeSpeciesTrajectory))]
        public bool HasAllTreeSpeciesStatistics 
        { 
            get { return this.AllTreeSpeciesTrajectory != null; }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.TreeSpeciesTrajectories))]
        public bool HasIndividualTreeSpeciesStatistics
        {
            get { return (this.TreeSpeciesTrajectories) != null && (this.TreeSpeciesTrajectories.Count > 0); }
        }

        public void AddYear()
        {
            if (this.HasAllTreeSpeciesStatistics)
            {
                ResourceUnitTreeStatistics endOfYearResourceUnitTreeStatistics = this.ResourceUnit.Trees.TreeAndSaplingStatisticsForAllSpecies;
                this.AllTreeSpeciesTrajectory.AddYear(endOfYearResourceUnitTreeStatistics);
            }

            if (this.HasIndividualTreeSpeciesStatistics)
            {
                IList<Trees> treesOnResourceUnit = this.ResourceUnit.Trees.TreesBySpeciesID.Values;
                if (treesOnResourceUnit.Count > this.TreeSpeciesTrajectories.Count)
                {
                    // TODO: support species ingrowth
                    throw new NotSupportedException("Expected " + this.TreeSpeciesTrajectories.Count + " tree species on resource unit " + this.ResourceUnit.ID + " but " + treesOnResourceUnit.Count + " species are present. Did a species grow into the resource unit?");
                }

                int treeSpeciesSourceIndex = 0;
                for (int treeSpeciesDestinationIndex = 0; treeSpeciesDestinationIndex < this.TreeSpeciesTrajectories.Count; ++treeSpeciesDestinationIndex)
                {
                    ResourceUnitTreeSpeciesTrajectory speciesTrajectory = this.TreeSpeciesTrajectories[treeSpeciesSourceIndex];
                    ResourceUnitTreeSpecies? resourceUnitTreeSpecies = null; // all trees on the resource unit may have died
                    if (treesOnResourceUnit.Count > treeSpeciesSourceIndex)
                    {
                        TreeSpecies treeSpecies = treesOnResourceUnit[treeSpeciesSourceIndex].Species;
                        resourceUnitTreeSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(treeSpecies);
                    }
                    if (Object.ReferenceEquals(speciesTrajectory.TreeSpecies, resourceUnitTreeSpecies) == false)
                    {
                        // if a tree species dies out of the resource unit it's removed from the resource unit's list of trees
                        // Thus, there are no statistics for the species and either 1) years could be no longer added to the trajectory,
                        // which is more compact in memory and on disk but more complex to manipulate and analyze due to the uneven
                        // timestep, or 2) zeros can be inserted in the trajectory to record species absence. For now, the latter
                        // option is used as sparse recording can be confusing (https://github.com/trotsiuk/r3PG/issues/75) and doesn't
                        // clearly save enough space to be worth its complexity.
                        // For now, assume species only die out of the resource unit and, thus, that statistics can be recorded for all
                        // species by walking through all source indices.
                        speciesTrajectory.AddYearWithoutSpecies();
                    }
                    else
                    {
                        speciesTrajectory.AddYear(resourceUnitTreeSpecies);
                        ++treeSpeciesSourceIndex;
                    }
                }
                if (treeSpeciesSourceIndex != treesOnResourceUnit.Count)
                {
                    throw new NotSupportedException("Expected to capture statistics for " + treesOnResourceUnit.Count + " tree species on resource unit " + this.ResourceUnit.ID + " but did so for only " + (treeSpeciesSourceIndex - 1) + " species. Did a species grow into the resource unit or did a combination of ingrowth and local extirpation occur?");
                }
            }
        }
    }
}
