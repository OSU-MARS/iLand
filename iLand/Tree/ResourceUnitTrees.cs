using iLand.Extensions;
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

        public void ApplyLightIntensityPattern(Landscape landscape)
        {
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp stamp = treesOfSpecies.LightStamp[treeIndex]!;
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int stampOriginX = treeLightCellIndexXY.X - stamp.CenterCellIndex;
                    int stampOriginY = treeLightCellIndexXY.Y - stamp.CenterCellIndex;
                    int stampSize = stamp.GetSizeInLightCells();
                    for (int lightY = stampOriginY, stampY = 0; stampY < stampSize; ++lightY, ++stampY)
                    {
                        int lightIndex = lightGrid.IndexXYToIndex(stampOriginX, lightY);
                        for (int lightX = stampOriginX, stampX = 0; stampX < stampSize; ++lightX, ++lightIndex, ++stampX)
                        {
                            // http://iland-model.org/competition+for+light
                            float iXYJ = stamp[stampX, stampY]; // tree's light stamp value
                            if (iXYJ != 0.0F) // zero = no tree shading => LIF intensity = 1 => no change in light grid
                            {
                                float dominantHeightInM = vegetationHeightGrid[lightX, lightY, Constant.LightCellsPerHeightCellWidth]; // height of z*u,v on the current position
                                float zStarXYJ = treesOfSpecies.HeightInM[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY); // distance to center = height (45 degree line)
                                if (zStarXYJ < 0.0F)
                                {
                                    zStarXYJ = 0.0F;
                                }
                                float zStarMin = (zStarXYJ >= dominantHeightInM) ? 1.0F : zStarXYJ / dominantHeightInM; // tree influence height
                                float iStarXYJ = 1.0F - treesOfSpecies.Opacity[treeIndex] * iXYJ * zStarMin; // this tree's Beer-Lambert contribution to shading of light grid cell
                                if (iStarXYJ < 0.02F)
                                {
                                    iStarXYJ = 0.02F; // limit minimum value
                                }

                                lightGrid[lightIndex] *= iStarXYJ; // compound LIF intensity, Eq. 4
                            }
                        }
                    }
                }
            }
        }

        // Apply LIPs. This "torus" function wraps the influence at the edges of a 1 ha simulation area (each resource unit forms
        // its own indvidual torus).
        public void ApplyLightIntensityPatternTorus(Landscape landscape, int lightBufferTranslationInCells)
        {
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeResourceUnitIndexX = (treeLightCellIndexXY.X - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells; // offset within the hectare
                    int treeResourceUnitIndexY = (treeLightCellIndexXY.Y - lightBufferTranslationInCells) % Constant.LightCellsPerRUWidth + lightBufferTranslationInCells;
                    int ruOffsetX = treeLightCellIndexXY.X - treeResourceUnitIndexX;
                    int ruOffsetY = treeLightCellIndexXY.Y - treeResourceUnitIndexY; // offset of the corner of the resource index

                    LightStamp stamp = treesOfSpecies.LightStamp[treeIndex];
                    int stampOriginX = treeResourceUnitIndexX - stamp.CenterCellIndex;
                    int stampOriginY = treeResourceUnitIndexY - stamp.CenterCellIndex;

                    int stampSize = stamp.GetSizeInLightCells();
                    for (int stampY = 0; stampY < stampSize; ++stampY)
                    {
                        int lightY = stampOriginY + stampY;
                        int torusY = ResourceUnitTrees.GetTorusIndex(lightY, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffsetY); // 50 cells per 100m
                        for (int stampX = 0; stampX < stampSize; ++stampX)
                        {
                            float iXYJ = stamp[stampX, stampY]; // tree's light stamp value
                            if (iXYJ != 0.0F) // zero = no tree shading => LIF intensity = 1 => no change in light grid
                            {
                                int lightX = stampOriginX + stampX;
                                int torusX = ResourceUnitTrees.GetTorusIndex(lightX, Constant.LightCellsPerRUWidth, lightBufferTranslationInCells, ruOffsetX);

                                float dominantHeightInM = vegetationHeightGrid[torusX, torusY, Constant.LightCellsPerHeightCellWidth]; // height of Z* on the current position
                                float zStarXYJ = treesOfSpecies.HeightInM[treeIndex] - stamp.GetDistanceToCenter(stampX, stampY); // distance to center = height (45 degree line)
                                if (zStarXYJ < 0.0F)
                                {
                                    zStarXYJ = 0.0F;
                                }
                                float zStarMin = (zStarXYJ >= dominantHeightInM) ? 1.0F : zStarXYJ / dominantHeightInM;
                                // old: value = 1. - value*mOpacity / local_dom;
                                float iStarXYJ = 1.0F - iXYJ * treesOfSpecies.Opacity[treeIndex] * zStarMin;
                                if (iStarXYJ < 0.02F)
                                {
                                    iStarXYJ = 0.02F; // limit minimum value
                                }

                                lightGrid[torusX, torusY] *= iStarXYJ; // use wraparound coordinates
                            }
                        }
                    }
                }
            }
        }

        // calculates the "dominant height field". This grid is coarser as the fine-scaled light-grid.
        public void CalculateDominantHeightField(Landscape landscape)
        {
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp? readerStamp = treesOfSpecies.LightStamp[treeIndex].ReaderStamp;
                    Debug.Assert(readerStamp != null);

                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeHeightCellIndexX = treeLightCellIndexXY.X / Constant.LightCellsPerHeightCellWidth;
                    int treeHeightCellIndexY = treeLightCellIndexXY.Y / Constant.LightCellsPerHeightCellWidth;

                    // count trees that are on height-grid cells (used for stockable area)
                    int heightCellIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY);
                    float currentVegetationHeightInM = vegetationHeightGrid[heightCellIndex];
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        vegetationHeightGrid[heightCellIndex] = treeHeightInM;
                    }

                    int readerCenter = readerStamp.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                    int indexEastWest = treeLightCellIndexXY.X % Constant.LightCellsPerHeightCellWidth; // 4: very east, 0 west edge
                    int indexNorthSouth = treeLightCellIndexXY.Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
                    if (indexEastWest - readerCenter < 0)
                    { // west
                        int westNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX - 1, treeHeightCellIndexY);
                        currentVegetationHeightInM = vegetationHeightGrid[westNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            vegetationHeightGrid[westNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                    {  // east
                        int eastNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX + 1, treeHeightCellIndexY);
                        currentVegetationHeightInM = vegetationHeightGrid[eastNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            vegetationHeightGrid[eastNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexNorthSouth - readerCenter < 0)
                    {  // south
                        int southNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY - 1);
                        currentVegetationHeightInM = vegetationHeightGrid[southNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            vegetationHeightGrid[southNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                    {  // north
                        int northNeighborIndex = vegetationHeightGrid.IndexXYToIndex(treeHeightCellIndexX, treeHeightCellIndexY + 1);
                        currentVegetationHeightInM = vegetationHeightGrid[northNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            vegetationHeightGrid[northNeighborIndex] = treeHeightInM;
                        }
                    }
                }
            }
        }

        public void CalculateDominantHeightFieldTorus(Landscape landscape, int heightBufferTranslationInCells)
        {
            Grid<float> heightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    // height of Z*
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int heightCellIndexX = treeLightCellIndexXY.X / Constant.LightCellsPerHeightCellWidth; // position of tree on height grid
                    int heightCellIndexY = treeLightCellIndexXY.Y / Constant.LightCellsPerHeightCellWidth;
                    heightCellIndexX = (heightCellIndexX - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells; // 10: 10 x 10m pixeln in 100m
                    heightCellIndexY = (heightCellIndexY - heightBufferTranslationInCells) % Constant.HeightCellsPerRUWidth + heightBufferTranslationInCells;

                    // torus coordinates: ruOffset = coords of lower left corner of 1 ha patch
                    Point ruOffset = new(treesOfSpecies.LightCellIndexXY[treeIndex].X / Constant.LightCellsPerHeightCellWidth - heightCellIndexX, treesOfSpecies.LightCellIndexXY[treeIndex].Y / Constant.LightCellsPerHeightCellWidth - heightCellIndexY);

                    // count trees that are on height-grid cells (used for stockable area)
                    int torusX = ResourceUnitTrees.GetTorusIndex(heightCellIndexX, 10, heightBufferTranslationInCells, ruOffset.X);
                    int torusY = ResourceUnitTrees.GetTorusIndex(heightCellIndexY, 10, heightBufferTranslationInCells, ruOffset.Y);
                    int heightCellIndex = heightGrid.IndexXYToIndex(torusX, torusY);
                    float currentVegetationHeightInM = heightGrid[heightCellIndex];
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        heightGrid[heightCellIndex] = treeHeightInM;
                    }

                    LightStamp reader = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                    int readerCenter = reader.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                    int indexEastWest = treeLightCellIndexXY.X % Constant.LightCellsPerHeightCellWidth; // 4: very east, 0 west edge
                    int indexNorthSouth = treeLightCellIndexXY.Y % Constant.LightCellsPerHeightCellWidth; // 4: northern edge, 0: southern edge
                    if (indexEastWest - readerCenter < 0)
                    { // west
                        int westNeighborIndex = heightGrid.IndexXYToIndex(ResourceUnitTrees.GetTorusIndex(heightCellIndexX - 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                          ResourceUnitTrees.GetTorusIndex(heightCellIndexY, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                        currentVegetationHeightInM = heightGrid[westNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            heightGrid[westNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexEastWest + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                    {  // east
                        int eastNeighborIndex = heightGrid.IndexXYToIndex(ResourceUnitTrees.GetTorusIndex(heightCellIndexX + 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                          ResourceUnitTrees.GetTorusIndex(heightCellIndexY, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                        currentVegetationHeightInM = heightGrid[eastNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            heightGrid[eastNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexNorthSouth - readerCenter < 0)
                    {  // south
                        int southNeighborIndex = heightGrid.IndexXYToIndex(ResourceUnitTrees.GetTorusIndex(heightCellIndexX, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                           ResourceUnitTrees.GetTorusIndex(heightCellIndexY - 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                        currentVegetationHeightInM = heightGrid[southNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            heightGrid[southNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (indexNorthSouth + readerCenter >= Constant.LightCellsPerHeightCellWidth)
                    {  // north
                        int northNeighborIndex = heightGrid.IndexXYToIndex(ResourceUnitTrees.GetTorusIndex(heightCellIndexX, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.X),
                                                                           ResourceUnitTrees.GetTorusIndex(heightCellIndexY + 1, Constant.HeightCellsPerRUWidth, heightBufferTranslationInCells, ruOffset.Y));
                        currentVegetationHeightInM = heightGrid[northNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            heightGrid[northNeighborIndex] = treeHeightInM;
                        }
                    }
                }
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
            return this.SpeciesAvailableOnResourceUnit[species.Index];
        }

        /// helper function for gluing model area edges together to form a torus
        /// index: index at light grid
        /// count: number of pixels that are the model area (e.g. 100 m area with 2 m light pixel = 50)
        /// buffer: size of buffer around simulation area (in light pixels)
        private static int GetTorusIndex(int index, int count, int gridCellsPerResourceUnitWidth, int ruOffset)
        {
            return gridCellsPerResourceUnitWidth + ruOffset + (index - gridCellsPerResourceUnitWidth + count) % count;
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

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland-model.org/competition+for+light for details.
          */
        public void ReadLightInfluenceField(Landscape landscape)
        {
            const float outOfLandscapeInfluenceReduction = 0.1F;

            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            Grid<HeightCellFlags> heightFlags = landscape.VegetationHeightFlags;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp reader = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];

                    int readerOffset = reader.CenterCellIndex;
                    int writerOffset = treesOfSpecies.LightStamp[treeIndex]!.CenterCellIndex;
                    int writerReaderOffset = writerOffset - readerOffset; // offset on the *stamp* to the crown-cells

                    int readerOriginX = treeLightCellIndexXY.X - readerOffset;
                    int readerOriginY = treeLightCellIndexXY.Y - readerOffset;

                    int lightIndexX = readerOriginX;
                    int lightIndexY = readerOriginY;

                    int readerSize = reader.GetSizeInLightCells();
                    float lightResourceIndex = 0.0F;
                    for (int readerY = 0; readerY < readerSize; ++readerY, ++lightIndexY)
                    {
                        float lightValue = lightGrid[lightIndexX, lightIndexY];
                        for (int readerX = 0; readerX < readerSize; ++readerX)
                        {
                            float vegetationHeightInM = vegetationHeightGrid[lightIndexX + readerX, lightIndexY, Constant.LightCellsPerHeightCellWidth];
                            float z = MathF.Max(treesOfSpecies.HeightInM[treeIndex] - reader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                            float z_zstar = (z >= vegetationHeightInM) ? 1.0F : z / vegetationHeightInM;

                            float treeValue = 1.0F - treesOfSpecies.LightStamp[treeIndex]![readerX, readerY, writerReaderOffset] * treesOfSpecies.Opacity[treeIndex] * z_zstar;
                            treeValue = MathF.Max(treeValue, 0.02F);
                            float value = lightValue / treeValue; // remove impact of focal tree

                            // additional punishment if pixel is outside
                            if (heightFlags[lightIndexX + readerX, lightIndexY, Constant.LightCellsPerHeightCellWidth].IsInResourceUnit() == false)
                            {
                                value *= outOfLandscapeInfluenceReduction;
                            }
                            // Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                            // if (value>0.)
                            lightResourceIndex += value * reader[readerX, readerY];
                        }
                    }

                    // LRI correction...
                    float relativeHeight = treesOfSpecies.HeightInM[treeIndex] / vegetationHeightGrid[treeLightCellIndexXY.X, treeLightCellIndexXY.Y, Constant.LightCellsPerHeightCellWidth];
                    if (relativeHeight < 1.0F)
                    {
                        lightResourceIndex = treesOfSpecies.Species.SpeciesSet.GetLriCorrection(lightResourceIndex, relativeHeight);
                    }
                    if (lightResourceIndex > 1.0F)
                    {
                        lightResourceIndex = 1.0F;
                    }
                    treesOfSpecies.LightResourceIndex[treeIndex] = lightResourceIndex;

                    // Finally, add LRI of this Tree to the ResourceUnit!
                    this.resourceUnit.Trees.AddWeightedLeafArea(treesOfSpecies.LeafAreaInM2[treeIndex], lightResourceIndex);
                }
            }
        }

        /// Torus version of read stamp (glued edges)
        public void ReadLightInfluenceFieldTorus(Landscape landscape, int lightBufferWidthInCells)
        {
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp stampReader = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeResourceUnitIndexX = (treeLightCellIndexXY.X - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells; // offset within the hectare
                    int treeResourceUnitIndexY = (treeLightCellIndexXY.Y - lightBufferWidthInCells) % Constant.LightCellsPerRUWidth + lightBufferWidthInCells;
                    int ruOffsetX = treeLightCellIndexXY.X - treeResourceUnitIndexX; // offset from the corner of the resource unit
                    int ruOffsetY = treeLightCellIndexXY.Y - treeResourceUnitIndexY;

                    float lightResourceIndex = 0.0F;
                    int readerSize = stampReader.GetSizeInLightCells();
                    int readerOriginX = treeResourceUnitIndexX - stampReader.CenterCellIndex;
                    int readerOriginY = treeResourceUnitIndexY - stampReader.CenterCellIndex;
                    int writerReaderOffset = treesOfSpecies.LightStamp[treeIndex]!.CenterCellIndex - stampReader.CenterCellIndex; // offset on the *stamp* to the crown (light?) cells
                    for (int readerY = 0; readerY < readerSize; ++readerY)
                    {
                        int yTorus = ResourceUnitTrees.GetTorusIndex(readerOriginY + readerY, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffsetY);
                        for (int readerX = 0; readerX < readerSize; ++readerX)
                        {
                            // see http://iland-model.org/competition+for+light 
                            int xTorus = ResourceUnitTrees.GetTorusIndex(readerOriginX + readerX, Constant.LightCellsPerRUWidth, lightBufferWidthInCells, ruOffsetX);
                            float vegetationHeightInM = vegetationHeightGrid[xTorus, yTorus, Constant.LightCellsPerHeightCellWidth];
                            float influenceZ = MathF.Max(treesOfSpecies.HeightInM[treeIndex] - stampReader.GetDistanceToCenter(readerX, readerY), 0.0F); // distance to center = height (45 degree line)
                            float influenceZstar = (influenceZ >= vegetationHeightInM) ? 1.0F : influenceZ / vegetationHeightInM;

                            // TODO: why a nonzero floor as opposed to skipping division?
                            float focalIntensity = MathF.Max(1.0F - treesOfSpecies.LightStamp[treeIndex]![readerX, readerY, writerReaderOffset] * treesOfSpecies.Opacity[treeIndex] * influenceZstar, 0.02F);
                            // C++ code is actually Tree.LightGrid[Tree.LightGrid.IndexOf(xTorus, yTorus) + 1], which appears to be an off by
                            // one error corrected by Qt build implementing precdence in *ptr++ incorrectly.
                            float cellIntensity = lightGrid[xTorus, yTorus];
                            float cellIndex = cellIntensity / focalIntensity; // remove impact of focal tree

                            // debug for one tree in HJA
                            // if (id()==178020)
                            //     Debug.WriteLine(x + y + xt + yt + *grid_value + local_dom + own_value + value + (*reader)(x,y);
                            // if (_isnan(value))
                            //     Debug.WriteLine("isnan" + id();
                            // if (cellIndex * reader[readerX, readerY] > 1.0)
                            // {
                            //     Debug.WriteLine("LIFTorus: value > 1.0.");
                            // }
                            lightResourceIndex += cellIndex * stampReader[readerX, readerY];
                            //} // isIndexValid
                        }
                    }

                    // LRI correction...
                    float relativeHeight = treesOfSpecies.HeightInM[treeIndex] / vegetationHeightGrid[treeLightCellIndexXY.X, treeLightCellIndexXY.Y, Constant.LightCellsPerHeightCellWidth];
                    if (relativeHeight < 1.0F)
                    {
                        lightResourceIndex = treesOfSpecies.Species.SpeciesSet.GetLriCorrection(lightResourceIndex, relativeHeight);
                    }

                    if (Single.IsNaN(lightResourceIndex))
                    {
                        throw new InvalidOperationException("Light resource index unexpectedly NaN.");
                        // Debug.WriteLine("LRI invalid (nan) " + ID);
                        // this.LightResourceIndex[treeIndex] = 0.0F;
                        // Debug.WriteLine(reader.dump();
                    }

                    Debug.Assert((lightResourceIndex >= 0.0F) && (lightResourceIndex < 50.0F)); // sanity upper bound
                    if (lightResourceIndex > 1.0F)
                    {
                        lightResourceIndex = 1.0F; // TODO: why clamp?
                    }
                    // Debug.WriteLine("Tree #"<< id() + "value" + sum + "Impact" + mImpact;
                    treesOfSpecies.LightResourceIndex[treeIndex] = lightResourceIndex;

                    // Finally, add LRI of this Tree to the ResourceUnit!
                    this.resourceUnit.Trees.AddWeightedLeafArea(treesOfSpecies.LeafAreaInM2[treeIndex], lightResourceIndex);
                }
            }
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
