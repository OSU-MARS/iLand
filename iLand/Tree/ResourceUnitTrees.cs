using iLand.Input.ProjectFile;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    public class ResourceUnitTrees
    {
        private readonly ResourceUnit resourceUnit;

        public float AggregatedLightWeightedLeafArea { get; private set; } // sum of lightresponse*LA of the current unit
        public float AverageLeafAreaWeightedAgingFactor { get; private set; } // used by WaterCycle
        public float AverageLightRelativeIntensity { get; set; } // ratio of RU leaf area to light weighted leaf area = 0..1 light factor
        public bool HasDeadTrees { get; private set; } // if true, the resource unit has dead trees and needs maybe some cleanup
        public float PhotosyntheticallyActiveArea { get; set; } // TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public float PhotosyntheticallyActiveAreaPerLightWeightedLeafArea { get; private set; }
        public List<ResourceUnitTreeSpecies> SpeciesAvailableOnResourceUnit { get; private init; }
        public float TotalLeafArea { get; private set; } // total leaf area of resource unit (m2)
        public float TotalLightWeightedLeafArea { get; private set; } // sum of lightResponse * LeafArea for all trees
        public ResourceUnitTreeStatistics TreeAndSaplingStatisticsForAllSpecies { get; private init; }
        public TreeSpeciesSet TreeSpeciesSet { get; private init; } // get SpeciesSet this RU links to.
        public SortedList<int, ResourceUnitTreeStatistics> TreeStatisticsByStandID { get; private init; }
        public SortedList<string, TreeListSpatial> TreesBySpeciesID { get; private init; } // reference to the tree list.

        public ResourceUnitTrees(ResourceUnit resourceUnit, TreeSpeciesSet treeSpeciesSet)
        {
            this.resourceUnit = resourceUnit;

            this.AggregatedLightWeightedLeafArea = 0.0F;
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;
            this.AverageLightRelativeIntensity = 0.0F;
            this.HasDeadTrees = false;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea = 0.0F;
            this.SpeciesAvailableOnResourceUnit = new(treeSpeciesSet.Count);
            this.TotalLeafArea = 0.0F;
            this.TotalLightWeightedLeafArea = 0.0F;
            this.TreeAndSaplingStatisticsForAllSpecies = new(resourceUnit);
            this.TreeSpeciesSet = treeSpeciesSet;
            this.TreeStatisticsByStandID = new();
            this.TreesBySpeciesID = new();

            for (int index = 0; index < treeSpeciesSet.Count; ++index)
            {
                TreeSpecies species = treeSpeciesSet[index];
                Debug.Assert(species.Index == index);

                ResourceUnitTreeSpecies ruSpecies = new(species, resourceUnit);
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

        public int AddTree(Project projectFile, Landscape landscape, string speciesID, float dbhInCm, float heightInM, Point lightCellIndexXY, UInt16 ageInYears, out TreeListSpatial treesOfSpecies)
        {
            // get or create tree's species
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out TreeListSpatial? nullableTreesOfSpecies))
            {
                treesOfSpecies = nullableTreesOfSpecies;
            }
            else
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

                treesOfSpecies = new TreeListSpatial(landscape, this.resourceUnit, this.SpeciesAvailableOnResourceUnit[speciesIndex].Species);
                this.TreesBySpeciesID.Add(speciesID, treesOfSpecies);
            }
            Debug.Assert(String.Equals(treesOfSpecies.Species.ID, speciesID, StringComparison.OrdinalIgnoreCase));

            // create tree
            float lightStampBeerLambertK = projectFile.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            int treeIndex = treesOfSpecies.Count;
            treesOfSpecies.Add(dbhInCm, heightInM, ageInYears, lightCellIndexXY, lightStampBeerLambertK);
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
                throw new ArithmeticException("Average aging invalid: RU-index " + this.resourceUnit.ResourceUnitGridIndex + ", LAI " + this.TreeAndSaplingStatisticsForAllSpecies.LeafAreaIndex);
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
            resourceUnit.Trees.RemoveDeadTrees();
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
                ruSpecies.StatisticsLive.OnAdditionsComplete(); // calculate the living (and add removed volume to gwl)
                ruSpecies.StatisticsSnag.OnAdditionsComplete(); // calculate the dead trees
                ruSpecies.StatisticsManagement.OnAdditionsComplete(); // stats of removed trees
                this.TreeAndSaplingStatisticsForAllSpecies.Add(ruSpecies.StatisticsLive);
            }
            for (int standIndex = 0; standIndex < this.TreeStatisticsByStandID.Count; ++standIndex)
            {
                // TODO: how to transfer sapling statistics from tree species to stands?
                this.TreeStatisticsByStandID.Values[standIndex].OnAdditionsComplete();
            }
            this.TreeAndSaplingStatisticsForAllSpecies.OnAdditionsComplete(); // aggregate on RU level
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
            this.TreeAndSaplingStatisticsForAllSpecies.Zero();
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
            {
                // ruSpecies.Statistics.Zero(); // deferred until ResourceUnit.CalculateWaterAndBiomassGrowthForYear()
                ruSpecies.StatisticsSnag.Zero();
                ruSpecies.StatisticsManagement.Zero();
            }
            for (int standIndex = 0; standIndex < this.TreeStatisticsByStandID.Count; ++standIndex)
            {
                this.TreeStatisticsByStandID.Values[standIndex].Zero();
            }
        }

        // sets the flag that indicates that the resource unit contains dead trees
        public void OnTreeDied()
        { 
            this.HasDeadTrees = true; 
        }

        /** recreate statistics. This is necessary after events that changed the structure
            of the stand *after* the growth of trees (where stand statistics are updated).
            An example is after disturbances. */
        // TODO: obviate this by decrementing removed trees or deferring calculation until end of year?
        public void RecalculateStatistics(bool zeroSaplingStatistics)
        {
            // when called after disturbances (recalculateSpecies = false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int species = 0; species < this.SpeciesAvailableOnResourceUnit.Count; ++species)
            {
                if (zeroSaplingStatistics)
                {
                    this.SpeciesAvailableOnResourceUnit[species].StatisticsLive.Zero();
                }
                else
                {
                    this.SpeciesAvailableOnResourceUnit[species].StatisticsLive.ZeroStandingTreeStatisticsForRecalculation();
                }
            }

            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Debug.Assert(treesOfSpecies.IsDead(treeIndex) == false);
                    speciesOnRU.StatisticsLive.Add(treesOfSpecies, treeIndex);

                    int standID = treesOfSpecies.StandID[treeIndex];
                    this.TreeStatisticsByStandID[standID].Add(treesOfSpecies, treeIndex);
                }
            }

            if (zeroSaplingStatistics)
            {
                for (int species = 0; species < this.SpeciesAvailableOnResourceUnit.Count; ++species)
                {
                    this.SpeciesAvailableOnResourceUnit[species].StatisticsLive.OnAdditionsComplete();
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

            for (int treeSpeciesIndex = 0; treeSpeciesIndex < this.TreesBySpeciesID.Count; ++treeSpeciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.TreesBySpeciesID.Values[treeSpeciesIndex];
                int lastLiveTreeIndex;
                for (lastLiveTreeIndex = treesOfSpecies.Count - 1; lastLiveTreeIndex >= 0 && treesOfSpecies.IsDead(lastLiveTreeIndex); --lastLiveTreeIndex)
                {
                    // no loop body, just finding index of last live tree 
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
                ++lastLiveTreeIndex; // now index of the first dead tree or, if the last tree is alive, one past the end of the array

                if (lastLiveTreeIndex != treesOfSpecies.Count)
                {
                    // since live trees have been the end of the tree array now contains only dead trees which can be dropped
                    int treesToDrop = treesOfSpecies.Count - lastLiveTreeIndex;
                    if (treesToDrop == treesOfSpecies.Count)
                    {
                        // all trees of this species have died, so drop species
                        this.TreesBySpeciesID.RemoveAt(treeSpeciesIndex);
                        // decrement index so no tree species is skipped (it's incremented back on next iteration of loop)
                        --treeSpeciesIndex;
                    }
                    else
                    {
                        treesOfSpecies.DropLastNTrees(treesToDrop);
                        // release memory at ends of arrays if a meaningful amount can be freed
                        if ((treesOfSpecies.Capacity > 100) && (((float)treesOfSpecies.Count / (float)treesOfSpecies.Capacity) < 0.2F))
                        {
                            // int target_size = 2*mTrees.Count;
                            // Debug.WriteLine("reduce size from " + mTrees.Capacity + " to " + target_size);
                            // mTrees.reserve(qMax(target_size, 100));
                            // if (GlobalSettings.Instance.LogDebug())
                            // {
                            //     Debug.WriteLine("reduce tree storage of RU " + Index + " from " + Trees.Capacity + " to " + Trees.Count);
                            // }

                            int simdCompatibleTreeCapacity = Constant.Simd128.Width32 * (treesOfSpecies.Count / Constant.Simd128.Width32 + 1);
                            treesOfSpecies.Resize(simdCompatibleTreeCapacity);
                        }
                    }
                }
            }

            this.HasDeadTrees = false;
        }

        /// refresh of tree based statistics.
        /// WARNING: this function is only called once (during startup).
        /// see function "OnEndYear()" above!!!
        public void SetupStatistics()
        {
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;

            // add all trees to the statistics objects of the species
            ResourceUnitTreeStatistics? currentStandStatistics = null;
            int previousStandID = Int32.MinValue;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float agingFactor = treesOfSpecies.Species.GetAgingFactor(treesOfSpecies.HeightInM[treeIndex], treesOfSpecies.Age[treeIndex]);
                    this.AddAging(treesOfSpecies.LeafAreaInM2[treeIndex], agingFactor);

                    Debug.Assert(treesOfSpecies.IsDead(treeIndex) == false);
                    speciesOnRU.StatisticsLive.Add(treesOfSpecies, treeIndex);

                    int standID = treesOfSpecies.StandID[treeIndex];
                    if (standID != previousStandID)
                    {
                        if (this.TreeStatisticsByStandID.TryGetValue(standID, out currentStandStatistics) == false)
                        {
                            currentStandStatistics = new(this.resourceUnit);
                            this.TreeStatisticsByStandID.Add(standID, currentStandStatistics);
                        }

                        previousStandID = standID;
                    }
                    currentStandStatistics!.Add(treesOfSpecies, treeIndex);
                }
            }

            this.AverageAging();
        }
    }
}
