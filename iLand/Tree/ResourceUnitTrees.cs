using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tree
{
    public class ResourceUnitTrees
    {
        private int mNextTreeID;
        private readonly ResourceUnit ru;

        public float AggregatedLightWeightedLeafArea { get; private set; } // sum of lightresponse*LA of the current unit
        public float AverageLeafAreaWeightedAgingFactor { get; private set; } // used by WaterCycle
        public float AverageLightRelativeIntensity { get; set; } // ratio of RU leaf area to light weighted leaf area = 0..1 light factor
        public bool HasDeadTrees { get; private set; } // if true, the resource unit has dead trees and needs maybe some cleanup
        public float PhotosyntheticallyActiveArea { get; set; } // TotalArea - Unstocked Area - loss due to BeerLambert (m2)
        public float PhotosyntheticallyActiveAreaPerLightWeightedLeafArea { get; private set; } ///<
        public List<ResourceUnitTreeSpecies> SpeciesPresentOnResourceUnit { get; private set; }
        public ResourceUnitTreeStatistics Statistics { get; private set; }
        public float TotalLeafArea { get; private set; } // total leaf area of resource unit (m2)
        public float TotalLightWeightedLeafArea { get; private set; } // sum of lightResponse * LeafArea for all trees
        public Dictionary<string, Trees> TreesBySpeciesID { get; private set; } // reference to the tree list.
        public TreeSpeciesSet TreeSpeciesSet { get; private set; } // get SpeciesSet this RU links to.

        public ResourceUnitTrees(ResourceUnit ru, TreeSpeciesSet treeSpeciesSet)
        {
            this.mNextTreeID = 0;
            this.ru = ru;

            this.AggregatedLightWeightedLeafArea = 0.0F;
            this.AverageLeafAreaWeightedAgingFactor = 0.0F;
            this.AverageLightRelativeIntensity = 0.0F;
            this.HasDeadTrees = false;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea = 0.0F;
            this.SpeciesPresentOnResourceUnit = new List<ResourceUnitTreeSpecies>();
            this.Statistics = new ResourceUnitTreeStatistics();
            this.TotalLeafArea = 0.0F;
            this.TotalLightWeightedLeafArea = 0.0F;
            this.TreesBySpeciesID = new Dictionary<string, Trees>();
            this.TreeSpeciesSet = treeSpeciesSet;

            //mRUSpecies.Capacity = set.count(); // ensure that the vector space is not relocated
            for (int index = 0; index < treeSpeciesSet.SpeciesCount(); ++index)
            {
                // TODO: this is an unnecessarily complex way of enumerating over all species in the species set
                TreeSpecies species = treeSpeciesSet.GetSpecies(index);
                if (species == null)
                {
                    throw new NotSupportedException("Species index " + index + " not found.");
                }

                ResourceUnitTreeSpecies ruSpecies = new ResourceUnitTreeSpecies(species, ru);
                this.SpeciesPresentOnResourceUnit.Add(ruSpecies);
                /* be careful: setup() is called with a pointer somewhere to the content of the mRUSpecies container.
                   If the container memory is relocated (List), the pointer gets invalid!!!
                   Therefore, a resize() is called before the loop (no resize()-operations during the loop)! */
                //mRUSpecies[i].setup(s,this); // setup this element
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
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out Trees treesOfSpecies) == false)
            {
                int speciesIndex = -1;
                foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesPresentOnResourceUnit)
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

                treesOfSpecies = new Trees(landscape, this.ru)
                {
                    Species = this.SpeciesPresentOnResourceUnit[speciesIndex].Species
                };
                this.TreesBySpeciesID.Add(speciesID, treesOfSpecies);
            }

            int treeIndex = treesOfSpecies.Count;
            treesOfSpecies.Add();
            treesOfSpecies.Tag[treeIndex] = this.mNextTreeID++; // doesn't guarantee unique tree ID when tree lists are combined with regeneration
            return treeIndex;
        }

        /// called from Trees.ReadLightInfluenceField(): each tree to added to the total weighted leaf area on a unit
        public void AddWeightedLeafArea(float leafArea, float lightResponse)
        {
            this.TotalLightWeightedLeafArea += leafArea * lightResponse; // TODO: how is this different from AddLightResponse()
            this.TotalLeafArea += leafArea;
        }

        public void AverageAging(float ruLeafAreaIndex, float stockableArea)
        {
            this.AverageLeafAreaWeightedAgingFactor = ruLeafAreaIndex > 0.0 ? this.AverageLeafAreaWeightedAgingFactor / (ruLeafAreaIndex * stockableArea) : 0.0F;
            if (this.AverageLeafAreaWeightedAgingFactor < 0.0F || this.AverageLeafAreaWeightedAgingFactor > 1.0F)
            {
                throw new NotSupportedException("Average aging out of range onRU " + this.ru.ResourceUnitGridIndex + " with , LAI " + ruLeafAreaIndex + ".");
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

            this.AverageLeafAreaWeightedAgingFactor = this.TotalLeafArea > 0.0F ? this.AverageLeafAreaWeightedAgingFactor / this.TotalLeafArea : 0.0F; // calculate aging value (calls to addAverageAging() by individual trees)
            if ((this.AverageLeafAreaWeightedAgingFactor > 0.0F) && (this.AverageLeafAreaWeightedAgingFactor < 0.00001F))
            {
                Debug.WriteLine("RU-index " + this.ru.ResourceUnitGridIndex + " average aging < 0.00001.");
            }
            if ((this.AverageLeafAreaWeightedAgingFactor < 0.0F) || (this.AverageLeafAreaWeightedAgingFactor > 1.0F))
            {
                throw new ArithmeticException("Average aging invalid: RU-index " + this.ru.ResourceUnitGridIndex + ", LAI " + this.Statistics.LeafAreaIndex);
            }
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
            return this.SpeciesPresentOnResourceUnit[species.Index];
        }

        public void OnEndYear()
        {
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesPresentOnResourceUnit)
            {
                ruSpecies.StatisticsDead.OnEndYear(); // calculate the dead trees
                ruSpecies.StatisticsManagement.OnEndYear(); // stats of removed trees
                ruSpecies.UpdateGwl(); // get sum of dead trees (died + removed)
                ruSpecies.Statistics.OnEndYear(); // calculate the living (and add removed volume to gwl)
                this.Statistics.Add(ruSpecies.Statistics);
            }
            this.Statistics.OnEndYear(); // aggregate on RU level
        }

        public void OnStartYear()
        {
            this.AggregatedLightWeightedLeafArea = 0.0F;
            this.PhotosyntheticallyActiveArea = 0.0F;
            this.TotalLightWeightedLeafArea = 0.0F;
            this.TotalLeafArea = 0.0F;

            // clear statistics global and per species...
            this.Statistics.Zero();
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesPresentOnResourceUnit)
            {
                ruSpecies.StatisticsDead.Zero();
                ruSpecies.StatisticsManagement.Zero();
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
        public void RecalculateStatistics(bool recalculateSpecies)
        {
            // when called after disturbances (recalculate_stats=false), we
            // clear only the tree-specific variables in the stats (i.e. we keep NPP, and regen carbon),
            // and then re-add all trees (since TreeGrowthData is NULL no NPP is available).
            // The statistics are not summarised here, because this happens for all resource units
            // in the yearEnd function of RU.
            for (int species = 0; species < this.SpeciesPresentOnResourceUnit.Count; ++species)
            {
                if (recalculateSpecies)
                {
                    this.SpeciesPresentOnResourceUnit[species].Statistics.Zero();
                }
                else
                {
                    this.SpeciesPresentOnResourceUnit[species].Statistics.ZeroTreeStatistics();
                }
            }

            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    speciesOnRU.Statistics.Add(treesOfSpecies, treeIndex, null);
                }
            }

            if (recalculateSpecies)
            {
                for (int species = 0; species < this.SpeciesPresentOnResourceUnit.Count; species++)
                {
                    this.SpeciesPresentOnResourceUnit[species].Statistics.OnEndYear();
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
                        if (((double)treesOfSpecies.Count / (double)treesOfSpecies.Capacity) < 0.2)
                        {
                            //int target_size = mTrees.Count*2;
                            //Debug.WriteLine("reduce size from "+mTrees.Capacity + "to" + target_size;
                            //mTrees.reserve(qMax(target_size, 100));
                            //if (GlobalSettings.Instance.LogDebug())
                            //{
                            //    Debug.WriteLine("reduce tree storage of RU " + Index + " from " + Trees.Capacity + " to " + Trees.Count);
                            //}
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
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float agingFactor = treesOfSpecies.Species.GetAgingFactor(treesOfSpecies.Height[treeIndex], treesOfSpecies.Age[treeIndex]);
                    this.AddAging(treesOfSpecies.LeafArea[treeIndex], agingFactor);
                }
            }

            // clear statistics (ru-level and ru-species level)
            this.Statistics.Zero();
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesPresentOnResourceUnit)
            {
                ruSpecies.Statistics.Zero();
                ruSpecies.StatisticsDead.Zero();
                ruSpecies.StatisticsManagement.Zero();
                ruSpecies.SaplingStats.ClearStatistics();
            }

            // add all trees to the statistics objects of the species
            foreach (Trees treesOfSpecies in this.TreesBySpeciesID.Values)
            {
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    speciesOnRU.Statistics.Add(treesOfSpecies, treeIndex, null, skipDead: true);
                }
            }

            // summarize statistics for the whole resource unit
            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesPresentOnResourceUnit)
            {
                ruSpecies.SaplingStats.AverageAgeAndHeights();
                ruSpecies.Statistics.Add(ruSpecies.SaplingStats);
                ruSpecies.Statistics.OnEndYear();
                this.Statistics.Add(ruSpecies.Statistics);
            }
            this.Statistics.OnEndYear();
            this.AverageAging(this.Statistics.LeafAreaIndex, this.ru.AreaInLandscape);
        }
    }
}
