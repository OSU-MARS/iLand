using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Output.Memory
{
    public class ResourceUnitTrajectory
    {
        public ResourceUnitAllSpeciesTrajectory? AllTreeSpeciesTrajectory { get; private init; }
        public ResourceUnit ResourceUnit { get; private init; }
        public ResourceUnitIndividualTreeTrajectories[]? IndividualTreeTrajectories { get; private init; }
        public ResourceUnitTreeSpecies[]? ResourceUnitTreeSpecies { get; private init; }
        public ResourceUnitThreePGTimeSeries[]? ThreePGTimeSeries { get; private init; }
        public ResourceUnitTreeSpeciesTrajectory[]? TreeSpeciesTrajectories { get; private init; }

        public ResourceUnitTrajectory(ResourceUnit resourceUnit, ResourceUnitMemoryOutputs resourceUnitOutputs, int initialCapacityInYears)
        {
            this.AllTreeSpeciesTrajectory = null;
            this.IndividualTreeTrajectories = null;
            this.ResourceUnit = resourceUnit;
            this.ThreePGTimeSeries = null;
            this.TreeSpeciesTrajectories = null;

            if (resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.AllTreeSpeciesStatistics))
            {
                this.AllTreeSpeciesTrajectory = new(initialCapacityInYears);
            }

            bool logIndividualTrees = resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.IndividualTrees);
            bool logSpeciesStatistics = resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.IndividualTreeSpeciesStatistics);
            bool logThreePG = resourceUnitOutputs.HasFlag(ResourceUnitMemoryOutputs.ThreePG);
            if (logIndividualTrees || logSpeciesStatistics || logThreePG)
            {
                IList<TreeListSpatial> treesOnResourceUnitBySpecies = resourceUnit.Trees.TreesBySpeciesID.Values;
                this.ResourceUnitTreeSpecies = new ResourceUnitTreeSpecies[treesOnResourceUnitBySpecies.Count];
                if (logIndividualTrees)
                {
                    this.IndividualTreeTrajectories = new ResourceUnitIndividualTreeTrajectories[treesOnResourceUnitBySpecies.Count];
                }
                if (logSpeciesStatistics)
                {
                    this.TreeSpeciesTrajectories = new ResourceUnitTreeSpeciesTrajectory[treesOnResourceUnitBySpecies.Count];
                }
                if (logThreePG)
                {
                    this.ThreePGTimeSeries = new ResourceUnitThreePGTimeSeries[treesOnResourceUnitBySpecies.Count];
                }

                // for now, instantiate trajectories only for trees species which are initially present on the resource unit
                for (int treeSpeciesIndex = 0; treeSpeciesIndex < treesOnResourceUnitBySpecies.Count; ++treeSpeciesIndex)
                {
                    TreeSpecies treeSpecies = treesOnResourceUnitBySpecies[treeSpeciesIndex].Species;
                    ResourceUnitTreeSpecies resourceUnitTreeSpecies = resourceUnit.Trees.GetResourceUnitSpecies(treeSpecies);
                    this.ResourceUnitTreeSpecies[treeSpeciesIndex] = resourceUnitTreeSpecies;

                    if (logIndividualTrees)
                    {
                        this.IndividualTreeTrajectories![treeSpeciesIndex] = new ResourceUnitIndividualTreeTrajectories(initialCapacityInYears);
                    }
                    if (logSpeciesStatistics)
                    {
                        this.TreeSpeciesTrajectories![treeSpeciesIndex] = new ResourceUnitTreeSpeciesTrajectory(initialCapacityInYears);
                    }
                    if (logThreePG)
                    {
                        this.ThreePGTimeSeries![treeSpeciesIndex] = new ResourceUnitThreePGTimeSeries(initialCapacityInYears);
                    }
                }
            }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.AllTreeSpeciesTrajectory))]
        public bool HasAllTreeSpeciesStatistics 
        { 
            get 
            {
                return this.AllTreeSpeciesTrajectory != null; 
            }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.IndividualTreeTrajectories), nameof(ResourceUnitTrajectory.ResourceUnitTreeSpecies))]
        public bool HasIndividualTreeTrajectories
        {
            get 
            {
                bool hasIndividualTreeTrajectories = (this.IndividualTreeTrajectories != null) && (this.IndividualTreeTrajectories.Length > 0);
                Debug.Assert((hasIndividualTreeTrajectories == false) || (this.ResourceUnitTreeSpecies != null));
                return hasIndividualTreeTrajectories; 
            }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.ResourceUnitTreeSpecies), nameof(ResourceUnitTrajectory.TreeSpeciesTrajectories))]
        public bool HasTreeSpeciesStatistics
        {
            get
            {
                bool hasTreeSpeciesStatistics = (this.TreeSpeciesTrajectories != null) && (this.TreeSpeciesTrajectories.Length > 0);
                Debug.Assert((hasTreeSpeciesStatistics == false) || (this.ResourceUnitTreeSpecies != null));
                return hasTreeSpeciesStatistics;
            }
        }

        [MemberNotNullWhen(true, nameof(ResourceUnitTrajectory.ResourceUnitTreeSpecies), nameof(ResourceUnitTrajectory.ThreePGTimeSeries))]
        public bool HasThreePGTimeSeries
        {
            get
            {
                bool hasThreePGTimeSeries = (this.ThreePGTimeSeries != null) && (this.ThreePGTimeSeries.Length > 0);
                Debug.Assert((hasThreePGTimeSeries == false) || (this.ResourceUnitTreeSpecies != null));
                return hasThreePGTimeSeries;
            }
        }

        public void AddYear()
        {
            if (this.HasAllTreeSpeciesStatistics)
            {
                ResourceUnitTreeStatistics endOfYearResourceUnitTreeStatistics = this.ResourceUnit.Trees.TreeAndSaplingStatisticsForAllSpecies;
                this.AllTreeSpeciesTrajectory.AddYear(endOfYearResourceUnitTreeStatistics);
            }

            bool hasIndividualTreeSpeciesStatistics = this.HasTreeSpeciesStatistics;
            bool hasIndividualTreeTrajectories = this.HasIndividualTreeTrajectories;
            bool hasThreePGTimeSeries = this.HasThreePGTimeSeries;
            if (hasIndividualTreeSpeciesStatistics || hasIndividualTreeTrajectories || hasThreePGTimeSeries)
            {
                Debug.Assert(this.ResourceUnitTreeSpecies != null);

                IList<TreeListSpatial> treesOnResourceUnit = this.ResourceUnit.Trees.TreesBySpeciesID.Values;
                if (treesOnResourceUnit.Count > this.ResourceUnitTreeSpecies.Length)
                {
                    // TODO: support species ingrowth
                    throw new NotSupportedException("Expected " + this.ResourceUnitTreeSpecies.Length + " tree species on resource unit " + this.ResourceUnit.ID + " but " + treesOnResourceUnit.Count + " species are present. Did a species grow into the resource unit?");
                }

                int treeSpeciesSourceIndex = 0;
                for (int treeSpeciesDestinationIndex = 0; treeSpeciesDestinationIndex < this.ResourceUnitTreeSpecies.Length; ++treeSpeciesDestinationIndex)
                {
                    ResourceUnitTreeSpecies? sourceTreeSpeciesCurrentlyOnResourceUnit = null; // all trees on the resource unit may have died
                    if (treesOnResourceUnit.Count > treeSpeciesSourceIndex)
                    {
                        TreeSpecies treeSpecies = treesOnResourceUnit[treeSpeciesSourceIndex].Species;
                        sourceTreeSpeciesCurrentlyOnResourceUnit = this.ResourceUnit.Trees.GetResourceUnitSpecies(treeSpecies);
                    }

                    // 3-PG outputs are all zero for the initialization year (simulation year 0) and can be suppressed if needed
                    // For now, the initialization year is included for consistency with other output files.
                    ResourceUnitTreeSpecies destinationTreeSpeciesForLogging = this.ResourceUnitTreeSpecies[treeSpeciesDestinationIndex];
                    if (Object.ReferenceEquals(destinationTreeSpeciesForLogging, sourceTreeSpeciesCurrentlyOnResourceUnit) == false)
                    {
                        // if a tree species dies out of the resource unit it's removed from the resource unit's list of trees
                        // Thus, there are no statistics for the species and either 1) years could be no longer added to the trajectory,
                        // which is more compact in memory and on disk but more complex to manipulate and analyze due to the uneven
                        // timestep, or 2) zeros can be inserted in the trajectory to record species absence. For now, the latter
                        // option is used as sparse recording can be confusing (https://github.com/trotsiuk/r3PG/issues/75) and doesn't
                        // clearly save enough space to be worth its complexity.
                        // For now, assume species only die out of the resource unit and, thus, that statistics can be recorded for all
                        // species by walking through all source indices.
                        if (hasIndividualTreeSpeciesStatistics)
                        {
                            ResourceUnitTreeSpeciesTrajectory speciesTrajectory = this.TreeSpeciesTrajectories![treeSpeciesDestinationIndex];
                            speciesTrajectory.AddYearWithoutSpecies();
                        }
                        if (hasIndividualTreeTrajectories)
                        {
                            ResourceUnitIndividualTreeTrajectories treeTrajectories = this.IndividualTreeTrajectories![treeSpeciesDestinationIndex];
                            treeTrajectories.AddYearWithoutSpecies(destinationTreeSpeciesForLogging);
                        }
                        if (hasThreePGTimeSeries)
                        {
                            ResourceUnitThreePGTimeSeries threePGseries = this.ThreePGTimeSeries![treeSpeciesDestinationIndex];
                            threePGseries.AddYearWithoutSpecies();
                        }
                    }
                    else
                    {
                        if (hasIndividualTreeSpeciesStatistics)
                        {
                            ResourceUnitTreeSpeciesTrajectory speciesTrajectory = this.TreeSpeciesTrajectories![treeSpeciesDestinationIndex];
                            speciesTrajectory.AddYear(sourceTreeSpeciesCurrentlyOnResourceUnit);
                        }
                        if (hasIndividualTreeTrajectories)
                        {
                            ResourceUnitIndividualTreeTrajectories treeTrajectories = this.IndividualTreeTrajectories![treeSpeciesDestinationIndex];
                            treeTrajectories.AddYear(treesOnResourceUnit[treeSpeciesSourceIndex]);
                        }
                        if (hasThreePGTimeSeries)
                        {
                            ResourceUnitThreePGTimeSeries threePGseries = this.ThreePGTimeSeries![treeSpeciesDestinationIndex];
                            threePGseries.AddYear(sourceTreeSpeciesCurrentlyOnResourceUnit);
                        }

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
