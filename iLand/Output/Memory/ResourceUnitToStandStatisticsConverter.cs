using iLand.Extensions;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Output.Memory
{
    internal class ResourceUnitToStandStatisticsConverter
    {
        private int count;
        private UInt32[] resourceUnitStandIDs;
        private LiveTreeAndSaplingStatistics?[] resourceUnitStatisticsByStand;

        public ResourceUnitToStandStatisticsConverter()
        {
            this.count = 0;

            const int defaultStandCapacity = 5;
            this.resourceUnitStandIDs = new UInt32[defaultStandCapacity];
            this.resourceUnitStatisticsByStand = new LiveTreeAndSaplingStatistics?[defaultStandCapacity];
        }

        private int Capacity
        {
            get { return this.resourceUnitStandIDs.Length; }
        }

        public void AddResourceUnitToStandStatisticsThreadSafe(float resourceUnitAreaInM2, SortedList<UInt32, StandLiveTreeAndSaplingStatistics> statisticsByStandID)
        {
            for (int resourceUnitStandIndex = 0; resourceUnitStandIndex < this.count; ++resourceUnitStandIndex)
            {
                UInt32 standID = this.resourceUnitStandIDs[resourceUnitStandIndex];
                LiveTreeAndSaplingStatistics? resourceUnitStandStatistics = this.resourceUnitStatisticsByStand[resourceUnitStandIndex];
                Debug.Assert(resourceUnitStandStatistics != null);

                StandLiveTreeAndSaplingStatistics standStatistics = statisticsByStandID[standID];
                lock (statisticsByStandID)
                {
                    standStatistics.Add(resourceUnitAreaInM2, resourceUnitStandStatistics);
                }
            }
        }

        public void CalculateStandStatisticsFromResourceUnit(ResourceUnit resourceUnit)
        {
            // zero any previously used stats
            for (int standIndex = 0; standIndex < this.count; ++standIndex)
            {
                LiveTreeAndSaplingStatistics? statistics = this.resourceUnitStatisticsByStand[standIndex];
                Debug.Assert(statistics != null);
                statistics.Zero();
            }
            this.count = 0;

            // find stand statistics for this resource unit's trees
            IList<TreeListSpatial> treesBySpecies = resourceUnit.Trees.TreesBySpeciesID.Values;
            UInt32 currentStandID = Constant.NoDataUInt32;
            LiveTreeAndSaplingStatistics? currentResourceUnitStandStatistics = null;
            for (int treeSpeciesIndex = 0; treeSpeciesIndex < treesBySpecies.Count; ++treeSpeciesIndex)
            {
                TreeListSpatial treesOfSpecies = treesBySpecies[treeSpeciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    UInt32 standID = treesOfSpecies.StandID[treeIndex];
                    if (currentStandID != standID)
                    {
                        for (int standIndex = 0; standIndex < this.count; ++standIndex)
                        {
                            if (this.resourceUnitStandIDs[standIndex] == standID)
                            {
                                currentStandID = standID;
                                currentResourceUnitStandStatistics = this.resourceUnitStatisticsByStand[standIndex];
                                break;
                            }
                        }
                        if (currentStandID != standID)
                        {
                            if (this.count == this.Capacity)
                            {
                                int newCapacity = 2 * this.Capacity;
                                this.resourceUnitStatisticsByStand = this.resourceUnitStatisticsByStand.Resize(newCapacity);
                                this.resourceUnitStandIDs = this.resourceUnitStandIDs.Resize(newCapacity);
                            }

                            currentStandID = standID;
                            currentResourceUnitStandStatistics = this.resourceUnitStatisticsByStand[this.count];
                            if (currentResourceUnitStandStatistics == null)
                            {
                                currentResourceUnitStandStatistics = new();
                                this.resourceUnitStatisticsByStand[this.count] = currentResourceUnitStandStatistics;
                            }

                            this.resourceUnitStandIDs[this.count] = standID;
                            ++this.count;
                        }
                    }

                    currentResourceUnitStandStatistics!.Add(treesOfSpecies, treeIndex);
                }
            }

            float resourceUnitAreaInM2 = resourceUnit.AreaInLandscapeInM2;
            for (int standIndex = 0; standIndex < this.count; ++standIndex)
            {
                LiveTreeAndSaplingStatistics? resourceUnitStandStatistics = this.resourceUnitStatisticsByStand[standIndex];
                Debug.Assert(resourceUnitStandStatistics != null);
                resourceUnitStandStatistics.OnAdditionsComplete(resourceUnitAreaInM2);
            }
        }
    }
}
