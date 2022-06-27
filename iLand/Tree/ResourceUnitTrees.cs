using iLand.Input.ProjectFile;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tree
{
    public class ResourceUnitTrees
    {
        private int nextDefaultTreeID;
        private readonly bool retainStandStatisticsInMemory;
        private readonly ResourceUnit ru;

        public float AggregatedLightWeightedLeafArea { get; private set; } // sum of lightresponse*LA of the current unit
        public float AverageLeafAreaWeightedAgingFactor { get; private set; } // used by WaterCycle
        public float AverageLightRelativeIntensity { get; set; } // ratio of RU leaf area to light weighted leaf area = 0..1 light factor
        public bool HasDeadTrees { get; private set; } // if true, the resource unit has dead trees and needs maybe some cleanup
        public float PhotosyntheticallyActiveArea { get; set; } // TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public float PhotosyntheticallyActiveAreaPerLightWeightedLeafArea { get; private set; } ///<
        public List<ResourceUnitTreeSpecies> SpeciesAvailableOnResourceUnit { get; private init; }
        public ResourceUnitTreeStatistics StatisticsForAllSpeciesAndStands { get; private init; }
        public float TotalLeafArea { get; private set; } // total leaf area of resource unit (m2)
        public float TotalLightWeightedLeafArea { get; private set; } // sum of lightResponse * LeafArea for all trees
        public Dictionary<string, Trees> TreesBySpeciesID { get; private init; } // reference to the tree list.
        public Dictionary<int, ResourceUnitTreeStatistics> TreeStatisticsByStandID { get; private init; }
        public TreeSpeciesSet TreeSpeciesSet { get; private init; } // get SpeciesSet this RU links to.

        public ResourceUnitTrees(Project projectFile, ResourceUnit ru, TreeSpeciesSet treeSpeciesSet)
        {
            this.nextDefaultTreeID = 0;
            this.retainStandStatisticsInMemory = projectFile.Output.Memory.StandStatistics.Enabled;
            this.ru = ru;

            this.AggregatedLightWeightedLeafArea = 0.0F;
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;
            this.AverageLightRelativeIntensity = 0.0F;
            this.HasDeadTrees = false;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea = 0.0F;
            this.SpeciesAvailableOnResourceUnit = new List<ResourceUnitTreeSpecies>(treeSpeciesSet.Count);
            this.TreeStatisticsByStandID = new Dictionary<int, ResourceUnitTreeStatistics>();
            this.StatisticsForAllSpeciesAndStands = new ResourceUnitTreeStatistics(ru)
            {
                IsPerHectare = true
            };
            this.TotalLeafArea = 0.0F;
            this.TotalLightWeightedLeafArea = 0.0F;
            this.TreesBySpeciesID = new Dictionary<string, Trees>();
            this.TreeSpeciesSet = treeSpeciesSet;

            for (int index = 0; index < treeSpeciesSet.Count; ++index)
            {
                TreeSpecies species = treeSpeciesSet[index];
                Debug.Assert(species.Index == index);

                ResourceUnitTreeSpecies ruSpecies = new(species, ru);
                this.SpeciesAvailableOnResourceUnit.Add(ruSpecies);
            }
        }

        // aggregate the tree aging values (weighted by leaf area)
        public void AddAging(float leafArea, float agingFactor)
        {
            this.AverageLeafAreaWeightedAgingFactor += leafArea * agingFactor;
        }

        // called from ResourceUnit.ReadLight
        public void AddLightResponse(float leafArea, float lightResponse) 
        { 
            this.AggregatedLightWeightedLeafArea += leafArea * lightResponse; 
        }

        public int AddTree(Landscape landscape, string speciesID)
        {
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out Trees? treesOfSpecies) == false)
            {
                int speciesIndex = -1;
                foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
                {
                    if (String.Equals(speciesID, ruSpecies.Species.ID))
                    {
                        speciesIndex = ruSpecies.Species.Index;
                        break;
                    }
                }
                if (speciesIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(speciesID));
                }

                treesOfSpecies = new Trees(landscape, this.ru, this.SpeciesAvailableOnResourceUnit[speciesIndex].Species);
                this.TreesBySpeciesID.Add(speciesID, treesOfSpecies);
            }
            Debug.Assert(String.Equals(treesOfSpecies.Species.ID, speciesID, StringComparison.OrdinalIgnoreCase));

            int treeIndex = treesOfSpecies.Count;
            treesOfSpecies.Add();
            treesOfSpecies.Tag[treeIndex] = this.nextDefaultTreeID++; // doesn't guarantee unique tree ID when tree lists are combined with regeneration
            return treeIndex;
        }

        /// called from Trees.ReadLightInfluenceField(): each tree to added to the total weighted leaf area on a unit
        public void AddWeightedLeafArea(float leafArea, float lightResponse)
        {
            this.TotalLightWeightedLeafArea += leafArea * lightResponse; // TODO: how is this different from AddLightResponse()
            this.TotalLeafArea += leafArea;
        }

        private void AverageAging()
        {
            this.AverageLeafAreaWeightedAgingFactor = this.TotalLeafArea > 0.0F ? this.AverageLeafAreaWeightedAgingFactor / this.TotalLeafArea : 0.0F; // calculate aging value (calls to addAverageAging() by individual trees)
            // if (this.AverageLeafAreaWeightedAgingFactor < 0.00001F)
            // {
            //     Debug.WriteLine("RU-index " + this.ru.ResourceUnitGridIndex + " average aging < 0.00001. Suspiciously low.");
            // }
            if ((this.AverageLeafAreaWeightedAgingFactor < 0.0F) || (this.AverageLeafAreaWeightedAgingFactor > 1.0F))
            {
                throw new ArithmeticException("Average aging invalid: RU-index " + this.ru.ResourceUnitGridIndex + ", LAI " + this.StatisticsForAllSpeciesAndStands.LeafAreaIndex);
            }
        }

        // function is called immediately before the growth of individuals
        public void BeforeTreeGrowth()
        {
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;
        }

        // function is called after finishing the individual growth / mortality.
        public void AfterTreeGrowth()
        {
            ru.Trees.RemoveDeadTrees();
            this.AverageAging();
        }

        public void CalculatePhotosyntheticActivityRatio()
        {
            if (this.AggregatedLightWeightedLeafArea == 0.0F)
            {
                this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea = 0.0F;
                return;
            }

            Debug.Assert(this.AggregatedLightWeightedLeafArea > 0.0);
            this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea = this.PhotosyntheticallyActiveArea / this.AggregatedLightWeightedLeafArea;
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("RU: aggregated lightresponse: " + mAggregatedLR + " eff.area./wla: " + mEffectiveArea_perWLA);
            //}
        }

        public float GetPhotosyntheticallyActiveArea(float leafArea, float lightResponse) 
        { 
            return this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea * leafArea * lightResponse; 
        }

        public ResourceUnitTreeSpecies GetResourceUnitSpecies(TreeSpecies species)
        {
            return this.SpeciesAvailableOnResourceUnit[species.Index];
        }

        public void OnEndYear()
        {
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
            {
                ruSpecies.StatisticsDead.OnEndYear(); // calculate the dead trees
                ruSpecies.StatisticsManagement.OnEndYear(); // stats of removed trees
                ruSpecies.UpdateGwl(); // get sum of dead trees (died + removed)
                ruSpecies.Statistics.OnEndYear(); // calculate the living (and add removed volume to gwl)
                this.StatisticsForAllSpeciesAndStands.AddCurrentYears(ruSpecies.Statistics);
            }
            foreach (ResourceUnitTreeStatistics standStatistics in this.TreeStatisticsByStandID.Values)
            {
                standStatistics.OnEndYear();
            }
            this.StatisticsForAllSpeciesAndStands.OnEndYear(); // aggregate on RU level
        }

        public void OnStartYear()
        {
            this.AggregatedLightWeightedLeafArea = 0.0F;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.TotalLightWeightedLeafArea = 0.0F;
            this.TotalLeafArea = 0.0F;

            // reset resource unit-level statistics and species dead and management statistics
            // Species' live statistics are not zeroed at this point as current year resource unit GPP and NPP calculations require the species' leaf area in 
            // the previous year.
            this.StatisticsForAllSpeciesAndStands.Zero();
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
            {
                // ruSpecies.Statistics.Zero(); // deferred until ResourceUnit.CalculateWaterAndBiomassGrowthForYear()
                ruSpecies.StatisticsDead.Zero();
                ruSpecies.StatisticsManagement.Zero();
            }
            foreach (ResourceUnitTreeStatistics standStatistics in this.TreeStatisticsByStandID.Values)
            {
                standStatistics.Zero();
            }
        }

        // sets the flag that indicates that the resource unit contains dead trees
        public void OnTreeDied()
        { 
            this.HasDeadTrees = true; 
        }

        /** recreate statistics. This is necessary after events that changed the structure
            of the stand *after* the growth of trees (where stand statistics are updated).
            An example is after disturbances.  */
        // TODO: obviate this by decrementing removed trees?
        public void RecalculateStatistics(bool recalculateSpecies)
        {
            // when called after disturbances (recalculate_stats=false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int species = 0; species < this.SpeciesAvailableOnResourceUnit.Count; ++species)
            {
                if (recalculateSpecies)
                {
                    this.SpeciesAvailableOnResourceUnit[species].Statistics.Zero();
                }
                else
                {
                    this.SpeciesAvailableOnResourceUnit[species].Statistics.ZeroTreeStatistics();
                }
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Debug.Assert(treesOfSpecies.IsDead(treeIndex) == false);
                    speciesOnRU.Statistics.AddToCurrentYear(treesOfSpecies, treeIndex, null, skipDead: true);

                    int standID = treesOfSpecies.StandID[treeIndex];
                    if (standID >= 0)
                    {
                        this.TreeStatisticsByStandID[standID].AddToCurrentYear(treesOfSpecies, treeIndex, null, skipDead: true);
                    }
                }
            }

            if (recalculateSpecies)
            {
                for (int species = 0; species < this.SpeciesAvailableOnResourceUnit.Count; ++species)
                {
                    this.SpeciesAvailableOnResourceUnit[species].Statistics.OnEndYear();
                }
            }
        }

        /// remove dead trees from tree list
        /// reduce size of vector if lots of space is free
        /// tests showed that this way of cleanup is very fast,
        /// because no memory allocations are performed (simple memmove())
        /// when trees are moved.
        public void RemoveDeadTrees()
        {
            if (this.HasDeadTrees == false)
            {
                return;
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                int lastLiveTreeIndex;
                for (lastLiveTreeIndex = treesOfSpecies.Count - 1; lastLiveTreeIndex >= 0 && treesOfSpecies.IsDead(lastLiveTreeIndex); --lastLiveTreeIndex)
                {
                }

                int overwriteIndex = 0;
                while (overwriteIndex < lastLiveTreeIndex)
                {
                    if (treesOfSpecies.IsDead(overwriteIndex))
                    {
                        treesOfSpecies.Copy(lastLiveTreeIndex, overwriteIndex); // copy data!
                        --lastLiveTreeIndex; //
                        while (lastLiveTreeIndex >= overwriteIndex && treesOfSpecies.IsDead(lastLiveTreeIndex))
                        {
                            --lastLiveTreeIndex;
                        }
                    }
                    ++overwriteIndex;
                }
                ++lastLiveTreeIndex; // last points now to the first dead tree

                // free resources
                if (lastLiveTreeIndex != treesOfSpecies.Count)
                {
                    treesOfSpecies.RemoveRange(lastLiveTreeIndex, treesOfSpecies.Count - lastLiveTreeIndex); // BUGBUG: assumes dead trees are at end of list
                    if (treesOfSpecies.Count == 0)
                    {
                        this.TreesBySpeciesID.Remove(treesOfSpecies.Species.ID);
                    }
                    else if (treesOfSpecies.Capacity > 100)
                    {
                        if (((float)treesOfSpecies.Count / (float)treesOfSpecies.Capacity) < 0.2F)
                        {
                            // int target_size = 2*mTrees.Count;
                            // Debug.WriteLine("reduce size from " + mTrees.Capacity + " to " + target_size);
                            // mTrees.reserve(qMax(target_size, 100));
                            // if (GlobalSettings.Instance.LogDebug())
                            // {
                            //     Debug.WriteLine("reduce tree storage of RU " + Index + " from " + Trees.Capacity + " to " + Trees.Count);
                            // }
                            treesOfSpecies.Capacity = treesOfSpecies.Count;
                        }
                    }
                }
            }

            this.HasDeadTrees = false;
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "yearEnd()" above!!!
        public void SetupStatistics()
        {
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;

            // add all trees to the statistics objects of the species
            ResourceUnitTreeStatistics? currentStandStatistics = null;
            int previousStandID = Int32.MinValue;
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float agingFactor = treesOfSpecies.Species.GetAgingFactor(treesOfSpecies.Height[treeIndex], treesOfSpecies.Age[treeIndex]);
                    this.AddAging(treesOfSpecies.LeafArea[treeIndex], agingFactor);

                    speciesOnRU.Statistics.AddToCurrentYear(treesOfSpecies, treeIndex, null, skipDead: true);

                    int standID = treesOfSpecies.StandID[treeIndex];
                    if (standID >= 0)
                    {
                        if (standID != previousStandID)
                        {
                            if (this.TreeStatisticsByStandID.TryGetValue(standID, out currentStandStatistics) == false)
                            {
                                if (this.retainStandStatisticsInMemory)
                                {
                                    currentStandStatistics = new ResourceUnitTreeStatisticsWithPreviousYears(this.ru);
                                }
                                else
                                {
                                    currentStandStatistics = new ResourceUnitTreeStatistics(this.ru);
                                }
                                this.TreeStatisticsByStandID.Add(standID, currentStandStatistics);
                            }

                            previousStandID = standID;
                        }
                        currentStandStatistics!.AddToCurrentYear(treesOfSpecies, treeIndex, null, skipDead: true);
                    }
                }
            }

            // summarize statistics for the whole resource unit
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
            {
                ruSpecies.SaplingStats.AverageAgeAndHeights();
                ruSpecies.Statistics.AddToCurrentYear(ruSpecies.SaplingStats);
                ruSpecies.Statistics.OnEndYear();
                this.StatisticsForAllSpeciesAndStands.AddCurrentYears(ruSpecies.Statistics);
            }
            this.StatisticsForAllSpeciesAndStands.OnEndYear();
            this.AverageAging();
        }
    }
}
