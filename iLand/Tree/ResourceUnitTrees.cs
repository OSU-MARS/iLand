using iLand.Extensions;
using iLand.Simulation;
using iLand.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
        public SortedList<WorldFloraID, TreeListSpatial> TreesBySpeciesID { get; private init; } // reference to the tree list.

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

        public TreeListSpatial AddTrees(TreeSpanForAddition treesToAdd, float lightStampBeerLambertK)
        {
            int speciesAddSourceIndex = 0;
            WorldFloraID previousTreeSpeciesID = treesToAdd.SpeciesID[0];
            int speciesAddCount;
            TreeListSpatial treesOfSpecies;
            for (int treeIndex = 0 + 1; treeIndex < treesToAdd.Length; ++treeIndex)
            {
                WorldFloraID treeSpeciesID = treesToAdd.SpeciesID[treeIndex];
                if (treeSpeciesID != previousTreeSpeciesID)
                {
                    // add this tree species to resource unit
                    // Trees can be grouped by species for more efficient addition. Currently, it's suggested this be done when writing tree data files (see README.md).
                    speciesAddCount = treeIndex - speciesAddSourceIndex;
                    treesOfSpecies = this.GetOrAddTreeSpecies(previousTreeSpeciesID, speciesAddCount);
                    treesOfSpecies.Add(treesToAdd, speciesAddSourceIndex, speciesAddCount, lightStampBeerLambertK);

                    speciesAddSourceIndex = treeIndex;
                }

                previousTreeSpeciesID = treeSpeciesID;
            }

            // add last (or only) species to resource unit
            speciesAddCount = treesToAdd.Length - speciesAddSourceIndex;
            treesOfSpecies = this.GetOrAddTreeSpecies(previousTreeSpeciesID, speciesAddCount);
            treesOfSpecies.Add(treesToAdd, speciesAddSourceIndex, speciesAddCount, lightStampBeerLambertK);
            return treesOfSpecies;
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
            this.resourceUnit.Trees.RemoveDeadTrees();
            this.AverageAging();
        }

        public void ApplyLightIntensityPattern(Landscape landscape, ConcurrentQueue<LightBuffer> lightBuffers)
        {
            if (lightBuffers.TryDequeue(out LightBuffer? lightBuffer) == false)
            {
                lightBuffer = new(isTorus: false);
            }
            lightBuffer.Fill(Constant.Grid.FullLightIntensity);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int bufferLightOriginX = resourceUnitLightGridOrigin.X - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            int bufferLightOriginY = resourceUnitLightGridOrigin.Y - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            float dominantHeightInM = Single.NaN; // height of z*u,v on the current position
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex]!;
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    float treeOpacity = treesOfSpecies.Opacity[treeIndex];

                    float[] stampData = treeLightStamp.Data;
                    int stampDataSize = treeLightStamp.DataSize;
                    int stampLightOriginX = treeLightCellIndexXY.X - treeLightStamp.CenterCellIndex;
                    int stampLightOriginY = treeLightCellIndexXY.Y - treeLightStamp.CenterCellIndex;
                    int stampBufferOriginX = stampLightOriginX - bufferLightOriginX;
                    int stampBufferOriginY = stampLightOriginY - bufferLightOriginY;
                    int stampHeightOriginX = stampLightOriginX / Constant.Grid.LightCellsPerHeightCellWidth;
                    int stampHeightOriginY = stampLightOriginY / Constant.Grid.LightCellsPerHeightCellWidth;
                    int stampSize = treeLightStamp.GetSizeInLightCells();
                    for (int stampIndexY = 0; stampIndexY < stampSize; ++stampIndexY)
                    {
                        int bufferIndex = lightBuffer.IndexXYToIndex(stampBufferOriginX, stampBufferOriginY + stampIndexY);
                        int heightIndex = vegetationHeightGrid.IndexXYToIndex(stampHeightOriginX, stampHeightOriginY + stampIndexY / Constant.Grid.LightCellsPerHeightCellWidth);
                        for (int stampIndex = stampDataSize * stampIndexY, stampIndexX = 0; stampIndexX < stampSize; ++bufferIndex, ++stampIndex, ++stampIndexX)
                        {
                            if (stampIndexX % Constant.Grid.LightCellsPerHeightCellWidth == 0)
                            {
                                dominantHeightInM = vegetationHeightGrid[heightIndex++];
                            }

                            // http://iland-model.org/competition+for+light
                            float iXYJ = stampData[stampIndex]; // tree's light stamp value
                            if (iXYJ != 0.0F) // zero = no tree shading => LIF intensity = 1 => no change in light grid
                            {
                                float zStarXYJ = treeHeightInM - treeLightStamp.GetDistanceToCenterInM(stampIndexX, stampIndexY); // distance to center = height (45 degree line)
                                if (zStarXYJ < 0.0F)
                                {
                                    zStarXYJ = 0.0F;
                                }
                                float zStarMin = (zStarXYJ >= dominantHeightInM) ? 1.0F : zStarXYJ / dominantHeightInM; // tree influence height
                                float iStarXYJ = 1.0F - treeOpacity * iXYJ * zStarMin; // this tree's Beer-Lambert contribution to shading of light grid cell
                                if (iStarXYJ < Constant.MinimumLightIntensity)
                                {
                                    iStarXYJ = Constant.MinimumLightIntensity; // limit minimum value
                                }

                                lightBuffer[bufferIndex] *= iStarXYJ; // compound LIF intensity, Eq. 4
                                // useful for checking correctness of calculations
                                // Debug.Assert(iStarXYJ > 0.0F);
                            }
                        }
                    }
                }
            }

            lock (landscape.LightGrid)
            {
                lightBuffer.ApplyToLightGrid(landscape.LightGrid, bufferLightOriginX, bufferLightOriginY);
            }
            lightBuffers.Enqueue(lightBuffer);
        }

        public unsafe void ApplyLightIntensityPattern128(Landscape landscape, ConcurrentQueue<LightBuffer> lightBuffers)
        {
            if (lightBuffers.TryDequeue(out LightBuffer? lightBuffer) == false)
            {
                lightBuffer = new(isTorus: false);
            }
            lightBuffer.Fill(Constant.Grid.FullLightIntensity);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int bufferLightOriginX = resourceUnitLightGridOrigin.X - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            int bufferLightOriginY = resourceUnitLightGridOrigin.Y - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            Vector128<float> dominantHeightInM = Vector128<float>.Zero; // height of z*u,v on the current position
            Vector128<float> one = Avx2Extensions.BroadcastScalarToVector128(1.0F);
            Vector128<int> four = Avx2Extensions.BroadcastScalarToVector128(Simd128.Width32);
            Vector128<int> zeroOneTwoThree = Vector128.Create(0, 1, 2, 3);
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            fixed (float* lightBufferCells = &lightBuffer.Data[0], vegetationHeightCells = &vegetationHeightGrid.Data[0])
            {
                for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                        LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex]!;
                        Vector128<float> treeHeightInM = Avx2Extensions.BroadcastScalarToVector128(treesOfSpecies.HeightInM[treeIndex]);
                        Vector128<float> treeOpacity = Avx2Extensions.BroadcastScalarToVector128(treesOfSpecies.Opacity[treeIndex]);

                        fixed (float* stampCells = &treeLightStamp.Data[0])
                        {
                            int stampDataSize = treeLightStamp.DataSize;
                            int stampLightOriginX = treeLightCellIndexXY.X - treeLightStamp.CenterCellIndex;
                            int stampLightOriginY = treeLightCellIndexXY.Y - treeLightStamp.CenterCellIndex;
                            int stampBufferOriginX = stampLightOriginX - bufferLightOriginX;
                            int stampBufferOriginY = stampLightOriginY - bufferLightOriginY;
                            int stampHeightOriginX = stampLightOriginX / Constant.Grid.LightCellsPerHeightCellWidth;
                            int stampHeightOriginY = stampLightOriginY / Constant.Grid.LightCellsPerHeightCellWidth;
                            int stampSize = treeLightStamp.GetSizeInLightCells(); // last SIMD step in each row may reach past stamp size and take zeros from [stampSize..stampDataSize]
                            for (int stampIndexY = 0; stampIndexY < stampSize; ++stampIndexY)
                            {
                                int bufferRowStartIndex = lightBuffer.IndexXYToIndex(stampBufferOriginX, stampBufferOriginY + stampIndexY);
                                int heightIndex = vegetationHeightGrid.IndexXYToIndex(stampHeightOriginX, stampHeightOriginY + stampIndexY / Constant.Grid.LightCellsPerHeightCellWidth);
                                int heightLoadModulus = 0;
                                int stampRowStartIndex = stampDataSize * stampIndexY;
                                float *stampRowEndAddress = stampCells + stampRowStartIndex + stampSize;
                                Vector128<int> stampIndexX128 = zeroOneTwoThree;
                                Vector128<int> stampIndexY128 = Avx2Extensions.BroadcastScalarToVector128(stampIndexY);
                                for (float* bufferAddress = lightBufferCells + bufferRowStartIndex, stampAddress = stampCells + stampRowStartIndex; stampAddress < stampRowEndAddress; bufferAddress += Simd128.Width32, stampAddress += Simd128.Width32)
                                {
                                    // light grid index:   0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 ... (cycle repeats)
                                    // height grid index:  0  0  0  0  0  1  1  1  1  1  2  2  2  2  2  3  3  3  3  3
                                    // height load step:   0           1           2           3           4
                                    // Vector128 index:    0  1  2  3  0  1  2  3  0  1  2  3  0  1  2  3  0  1  2  3 
                                    switch (heightLoadModulus)
                                    {
                                        case 0:
                                            dominantHeightInM = Avx.BroadcastScalarToVector128(vegetationHeightCells + heightIndex);
                                            ++heightLoadModulus;
                                            break;
                                        case 1:
                                            Vector128<float> dominantHeightInM1 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM1, Simd128.Blend32_123);
                                            ++heightLoadModulus;
                                            break;
                                        case 2:
                                            Vector128<float> dominantHeightInM2 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Shuffle(dominantHeightInM, dominantHeightInM2, Simd128.Shuffle32_1to0);
                                            ++heightLoadModulus;
                                            break;
                                        case 3:
                                            Vector128<float> dominantHeightInM3 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Shuffle(dominantHeightInM, dominantHeightInM3, Simd128.Shuffle32_2to01);
                                            ++heightLoadModulus;
                                            break;
                                        case 4:
                                            dominantHeightInM = Avx.Shuffle(dominantHeightInM, dominantHeightInM, Simd128.Shuffle32_3to012);
                                            heightLoadModulus = 0;
                                            break;
                                        default:
                                            throw new NotSupportedException(nameof(heightLoadModulus));
                                    };

                                    // http://iland-model.org/competition+for+light
                                    Vector128<float> iXYJ = Avx.LoadVector128(stampAddress); // tree's light stamp value
                                    Vector128<float> zStarXYJ = Avx.Subtract(treeHeightInM, treeLightStamp.GetDistanceToCenterInM(stampIndexX128, stampIndexY128)); // distance to center = height (45 degree line)
                                    zStarXYJ = Avx.Max(zStarXYJ, Vector128<float>.Zero);
                                    byte zStarBlend = (byte)Avx.MoveMask(Avx.CompareLessThan(zStarXYJ, dominantHeightInM));
                                    Vector128<float> zStarMin = Avx.Blend(one, Avx.Divide(zStarXYJ, dominantHeightInM), zStarBlend); // tree influence height
                                    Vector128<float> iStarXYJ = Avx.Subtract(one, Avx.Multiply(treeOpacity, Avx.Multiply(iXYJ, zStarMin))); // this tree's Beer-Lambert contribution to shading of light grid cell
                                    iStarXYJ = Avx.Max(iStarXYJ, Constant.MinimumLightIntensity128); // limit minimum value

                                    Vector128<float> lightIntensity = Avx.LoadVector128(bufferAddress);
                                    lightIntensity = Avx.Multiply(iStarXYJ, lightIntensity);  // compound LIF intensity, Eq. 4
                                    Avx.Store(bufferAddress, lightIntensity);

                                    stampIndexX128 = Avx.Add(stampIndexX128, four);
                                    // useful for checking correctness of calculations
                                    // DebugV.Assert(Avx.CompareGreaterThan(lightIntensity, Vector128<float>.Zero));
                                }
                            }
                        }
                    }
                }
            }

            lock (landscape.LightGrid)
            {
                lightBuffer.ApplyToLightGrid128(landscape.LightGrid, bufferLightOriginX, bufferLightOriginY);
            }
            lightBuffers.Enqueue(lightBuffer);
        }

        public unsafe void ApplyLightIntensityPattern256(Landscape landscape, ConcurrentQueue<LightBuffer> lightBuffers)
        {
            if (lightBuffers.TryDequeue(out LightBuffer? lightBuffer) == false)
            {
                lightBuffer = new(isTorus: false);
            }
            lightBuffer.Fill(Constant.Grid.FullLightIntensity);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int bufferLightOriginX = resourceUnitLightGridOrigin.X - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            int bufferLightOriginY = resourceUnitLightGridOrigin.Y - Constant.Grid.MaxLightStampSizeInLightCells / 2;
            Vector256<float> dominantHeightInM = Vector256<float>.Zero; // height of z*u,v on the current position
            Vector256<float> one = Avx2Extensions.BroadcastScalarToVector256(1.0F);
            Vector256<int> eight = Avx2Extensions.BroadcastScalarToVector256(Simd256.Width32);
            Vector256<int> zeroOneTwoThreeFourFiveSixSeven = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            fixed (float* lightBufferCells = &lightBuffer.Data[0], vegetationHeightCells = &vegetationHeightGrid.Data[0])
            {
                for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                        LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex]!;
                        Vector256<float> treeHeightInM = Avx2Extensions.BroadcastScalarToVector256(treesOfSpecies.HeightInM[treeIndex]);
                        Vector256<float> treeOpacity = Avx2Extensions.BroadcastScalarToVector256(treesOfSpecies.Opacity[treeIndex]);

                        fixed (float* stampCells = &treeLightStamp.Data[0])
                        {
                            int stampDataSize = treeLightStamp.DataSize;
                            // stamp data size must be either an integer multiple of 256 bits or an integer multiple plus 128 bits
                            int stampLightOriginX = treeLightCellIndexXY.X - treeLightStamp.CenterCellIndex;
                            int stampLightOriginY = treeLightCellIndexXY.Y - treeLightStamp.CenterCellIndex;
                            int stampBufferOriginX = stampLightOriginX - bufferLightOriginX;
                            int stampBufferOriginY = stampLightOriginY - bufferLightOriginY;
                            int stampHeightOriginX = stampLightOriginX / Constant.Grid.LightCellsPerHeightCellWidth;
                            int stampHeightOriginY = stampLightOriginY / Constant.Grid.LightCellsPerHeightCellWidth;
                            int stampSize = treeLightStamp.GetSizeInLightCells();
                            // stamp data sizes are 4, 8, 12, 16, 24, 32, 48, and 64, actual stamp sizes in light cells may range from
                            // next smallest data size to full width of stamp
                            // Default to SIMD across full width of stamp, either 256 bit only or 256 iterations plus a final 128 bit step.
                            // If the stamp size is small enough, remove unneeded 256 bit iterations.
                            //
                            // stampDataSize      4  4  4  4  8  8  8  8 12 12 12 12 16 16 16 16  24 24 24 24 24 24 24 24  32 32 32 32 32 32 32 32  48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48  64 ... 64
                            // stampSize          1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16  17 18 19 20 21 22 23 24  25 26 27 28 29 30 31 32  33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48  59 ... 64
                            // SIMD 256 size      0  0  0  0  8  8  8  8  8  8  8  8 16 16 16 16  16 16 16 16 24 24 24 24  24 24 24 24 32 32 32 32  32 32 32 32 40 40 40 40 40 40 40 40 48 48 48 48  48 ... 64
                            // SIMD 128 bit step  y  y  y  y  n  n  n  n  y  y  y  y  n  n  n  n   y  y  y  y  n  n  n  n   y  y  y  y  n  n  n  n   y  y  y  y  n  n  n  n  y  y  y  y  n  n  n  n   y ...  n
                            //
                            // See also stamps.R.
                            int stampSizeSimd256 = Simd256.Width32 * ((stampSize + 3) / Simd256.Width32);
                            Debug.Assert((stampDataSize % Simd128.Width32 == 0) && (stampSize <= stampDataSize) && (stampSize - stampSizeSimd256 < Simd128.Width32));
                            for (int stampIndexY = 0; stampIndexY < stampSize; ++stampIndexY)
                            {
                                // 256 bit part of stamp
                                // Stamps of size 8, 16, 24, 32, 48, and 64 require only 256 bit stamping.
                                int bufferRowStartIndex = lightBuffer.IndexXYToIndex(stampBufferOriginX, stampBufferOriginY + stampIndexY);
                                int heightIndex = vegetationHeightGrid.IndexXYToIndex(stampHeightOriginX, stampHeightOriginY + stampIndexY / Constant.Grid.LightCellsPerHeightCellWidth);
                                int heightLoadModulus = 0;
                                int stampRowStartIndex = stampDataSize * stampIndexY;
                                float* stampRowEndAddress256 = stampCells + stampRowStartIndex + stampSizeSimd256;
                                Vector256<int> stampIndexX256 = zeroOneTwoThreeFourFiveSixSeven;
                                Vector256<int> stampIndexY256 = Avx2Extensions.BroadcastScalarToVector256(stampIndexY);
                                for (float* bufferAddress = lightBufferCells + bufferRowStartIndex, stampAddress = stampCells + stampRowStartIndex; stampAddress < stampRowEndAddress256; bufferAddress += Simd256.Width32, stampAddress += Simd256.Width32)
                                {
                                    // light grid index:   0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37 38 39 ... (cycle repeats)
                                    // height grid index:  0  0  0  0  0  1  1  1  1  1  2  2  2  2  2  3  3  3  3  3  4  4  4  4  4  5  5  5  5  5  6  6  6  6  6  7  7  7  7  7
                                    // height load step:   0                       1                       2                       3                       4
                                    // Vector256 index:    0  1  2  3  4  5  6  7  0  1  2  3  4  5  6  7  0  1  2  3  4  5  6  7  0  1  2  3  4  5  6  7  0  1  2  3  4  5  6  7
                                    switch (heightLoadModulus)
                                    {
                                        case 0:
                                            dominantHeightInM = Avx.BroadcastScalarToVector256(vegetationHeightCells + heightIndex);
                                            Vector256<float> dominantHeightInM1 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM1, Simd256.Blend32_567);
                                            ++heightLoadModulus;
                                            break;
                                        case 1:
                                            Vector256<float> dominantHeightInM2 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            Vector256<float> dominantHeightInM3 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM2, Simd256.Blend32_23456);
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM3, Simd256.Blend32_7);
                                            ++heightLoadModulus;
                                            break;
                                        case 2:
                                            Vector256<float> dominantHeightInM4 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Shuffle(dominantHeightInM, dominantHeightInM, Simd128.Shuffle32_3to012); // expand height cell 3 from element 7 to all of upper lane (and element 3 to all of lower lane)
                                            dominantHeightInM = Avx.Permute2x128(dominantHeightInM, dominantHeightInM4, Simd256.Permute128_1upperTo1lower); // move height cell 3 to lower lane and set upper lane to height cell 4
                                            ++heightLoadModulus;
                                            break;
                                        case 3:
                                            Vector256<float> dominantHeightInM5 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            Vector256<float> dominantHeightInM6 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Permute2x128(dominantHeightInM, dominantHeightInM, Simd256.Permute128_1upperTo1lower); // copy height cell 4 into lower lane
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM5, Simd256.Blend32_12345);
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM6, Simd256.Blend32_67);
                                            ++heightLoadModulus;
                                            break;
                                        case 4:
                                            Vector256<float> dominantHeightInM7 = Avx.BroadcastScalarToVector256(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM = Avx.Permute2x128(dominantHeightInM, dominantHeightInM, Simd256.Permute128_1upperTo1lower); // copy height cell 6 into lower lane
                                            dominantHeightInM = Avx.Shuffle(dominantHeightInM, dominantHeightInM, Simd128.Shuffle32_2to01); // fill lower lane with height cell 6
                                            dominantHeightInM = Avx.Blend(dominantHeightInM, dominantHeightInM7, Simd256.Blend32_34567);
                                            heightLoadModulus = 0;
                                            break;
                                        default:
                                            throw new NotSupportedException(nameof(heightLoadModulus));
                                    };

                                    // http://iland-model.org/competition+for+light
                                    Vector256<float> iXYJ = Avx.LoadVector256(stampAddress); // tree's light stamp value
                                    Vector256<float> zStarXYJ = Avx.Subtract(treeHeightInM, treeLightStamp.GetDistanceToCenterInM(stampIndexX256, stampIndexY256)); // distance to center = height (45 degree line)
                                    zStarXYJ = Avx.Max(zStarXYJ, Vector256<float>.Zero);
                                    byte zStarBlend = (byte)Avx.MoveMask(Avx.CompareLessThan(zStarXYJ, dominantHeightInM));
                                    Vector256<float> zStarMin = Avx.Blend(one, Avx.Divide(zStarXYJ, dominantHeightInM), zStarBlend); // tree influence height
                                    Vector256<float> iStarXYJ = Avx.Subtract(one, Avx.Multiply(treeOpacity, Avx.Multiply(iXYJ, zStarMin))); // this tree's Beer-Lambert contribution to shading of light grid cell
                                    iStarXYJ = Avx.Max(iStarXYJ, Constant.MinimumLightIntensity256); // limit minimum value

                                    Vector256<float> lightIntensity = Avx.LoadVector256(bufferAddress);
                                    lightIntensity = Avx.Multiply(iStarXYJ, lightIntensity);  // compound LIF intensity, Eq. 4
                                    Avx.Store(bufferAddress, lightIntensity);

                                    stampIndexX256 = Avx2.Add(stampIndexX256, eight); // not needed for 256 bit on last 256 bit iteration but is needed if a 128 bit step follows
                                    // useful for checking correctness of calculations
                                    // DebugV.Assert(Avx.CompareGreaterThan(lightIntensity, Vector256<float>.Zero));
                                }

                                // 128 bit part of stamp row
                                // Stamps of size 4 require special casing, which is not currently implemented, to use 256 bit stamping.
                                // Stamps of size 12 also currently require special casing. As with 128 bit only stamping
                                // (ApplyLightIntensityPattern128()), this 128 bit step may take zeros from the stamp in [stampSize..stampDataSize].
                                if (stampSize > stampSizeSimd256)
                                {
                                    Vector128<float> dominantHeightInM128;
                                    switch (heightLoadModulus)
                                    {
                                        case 0:
                                            dominantHeightInM128 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            ++heightLoadModulus;
                                            break;
                                        case 1:
                                            heightIndex += 2;
                                            Vector128<float> dominantHeightInM2 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM128 = dominantHeightInM.GetUpper();
                                            dominantHeightInM128 = Avx.Shuffle(dominantHeightInM128, dominantHeightInM128, Simd128.Shuffle32_3to012); // move height cell 1 into elements 0 and 1
                                            dominantHeightInM128 = Avx.Blend(dominantHeightInM128, dominantHeightInM2, Simd128.Blend32_23);
                                            ++heightLoadModulus;
                                            break;
                                        case 2:
                                            dominantHeightInM128 = dominantHeightInM.GetUpper();
                                            dominantHeightInM128 = Avx.Shuffle(dominantHeightInM128, dominantHeightInM128, Simd128.Shuffle32_3to012); // broadcast height cell 3 to all elements
                                            ++heightLoadModulus;
                                            break;
                                        case 3:
                                            Vector128<float> dominantHeightInM5 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM128 = dominantHeightInM.GetUpper();
                                            dominantHeightInM128 = Avx.Blend(dominantHeightInM128, dominantHeightInM5, Simd128.Blend32_123);
                                            ++heightLoadModulus;
                                            break;
                                        case 4:
                                            Vector128<float> dominantHeightInM7 = Avx.BroadcastScalarToVector128(vegetationHeightCells + ++heightIndex);
                                            dominantHeightInM128 = dominantHeightInM.GetUpper();
                                            dominantHeightInM128 = Avx.Shuffle(dominantHeightInM128, dominantHeightInM128, Simd128.Shuffle32_2to01);
                                            dominantHeightInM128 = Avx.Blend(dominantHeightInM128, dominantHeightInM7, Simd128.Blend32_3);
                                            heightLoadModulus = 0;
                                            break;
                                        default:
                                            throw new NotSupportedException(nameof(heightLoadModulus));
                                    };

                                    // http://iland-model.org/competition+for+light
                                    Vector128<float> iXYJ = Avx.LoadVector128(stampRowEndAddress256); // tree's light stamp value
                                    Vector128<int> stampIndexX128 = stampIndexX256.GetLower();
                                    Vector128<int> stampIndexY128 = stampIndexY256.GetLower();
                                    Vector128<float> zStarXYJ = Avx.Subtract(treeHeightInM.GetLower(), treeLightStamp.GetDistanceToCenterInM(stampIndexX128, stampIndexY128)); // distance to center = height (45 degree line)
                                    zStarXYJ = Avx.Max(zStarXYJ, Vector128<float>.Zero);
                                    byte zStarBlend = (byte)Avx.MoveMask(Avx.CompareLessThan(zStarXYJ, dominantHeightInM128));
                                    Vector128<float> one128 = one.GetLower();
                                    Vector128<float> zStarMin = Avx.Blend(one128, Avx.Divide(zStarXYJ, dominantHeightInM128), zStarBlend); // tree influence height
                                    Vector128<float> iStarXYJ = Avx.Subtract(one128, Avx.Multiply(treeOpacity.GetLower(), Avx.Multiply(iXYJ, zStarMin))); // this tree's Beer-Lambert contribution to shading of light grid cell
                                    iStarXYJ = Avx.Max(iStarXYJ, Constant.MinimumLightIntensity128); // limit minimum value

                                    float* bufferAddress128 = lightBufferCells + bufferRowStartIndex + stampSizeSimd256;
                                    Vector128<float> lightIntensity = Avx.LoadVector128(bufferAddress128);
                                    lightIntensity = Avx.Multiply(iStarXYJ, lightIntensity);  // compound LIF intensity, Eq. 4
                                    Avx.Store(bufferAddress128, lightIntensity);

                                    // last step in this row so no index increment needed
                                    // useful for checking correctness of calculations
                                    // DebugV.Assert(Avx.CompareGreaterThan(lightIntensity, Vector128<float>.Zero));
                                }
                            }
                        }
                    }
                }
            }

            lock (landscape.LightGrid)
            {
                lightBuffer.ApplyToLightGrid256(landscape.LightGrid, bufferLightOriginX, bufferLightOriginY);
            }
            lightBuffers.Enqueue(lightBuffer);
        }

        // Apply LIPs. This "torus" function wraps the influence at the edges of a 1 ha simulation area (each resource unit forms
        // its own indvidual torus).
        public void ApplyLightIntensityPatternTorus(Landscape landscape, ConcurrentQueue<LightBuffer> lightBuffers)
        {
            if (lightBuffers.TryDequeue(out LightBuffer? lightBuffer) == false)
            {
                lightBuffer = new(isTorus: true);
            }
            lightBuffer.Fill(Constant.Grid.FullLightIntensity);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int resourceUnitLightGridOriginX = resourceUnitLightGridOrigin.X; // inclusive
            int resourceUnitLightGridOriginY = resourceUnitLightGridOrigin.Y; // inclusive
            int resourceUnitLightGridMaxX = resourceUnitLightGridOriginX + Constant.Grid.LightCellsPerRUWidth; // exclusive
            int resourceUnitLightGridMaxY = resourceUnitLightGridOriginY + Constant.Grid.LightCellsPerRUWidth; // exclusive
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex];
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    float treeOpacity = treesOfSpecies.Opacity[treeIndex];

                    float[] stampData = treeLightStamp.Data;
                    int stampDataSize = treeLightStamp.DataSize;
                    int stampLightOriginX = treeLightCellIndexXY.X - treeLightStamp.CenterCellIndex;
                    int stampLightOriginY = treeLightCellIndexXY.Y - treeLightStamp.CenterCellIndex;
                    int stampBufferOriginX = stampLightOriginX - resourceUnitLightGridOriginX;
                    int stampBufferOriginY = stampLightOriginY - resourceUnitLightGridOriginY;
                    int stampSize = treeLightStamp.GetSizeInLightCells();
                    for (int stampIndexY = 0; stampIndexY < stampSize; ++stampIndexY)
                    {
                        int lightTorusY = ResourceUnitTrees.ToTorusLightIndex(stampLightOriginY + stampIndexY, resourceUnitLightGridOriginY, resourceUnitLightGridMaxY);
                        int heightTorusY = lightTorusY / Constant.Grid.LightCellsPerHeightCellWidth;
                        int stampTorusY = ResourceUnitTrees.ToTorusLightIndex(stampBufferOriginY + stampIndexY, 0, Constant.Grid.LightCellsPerRUWidth);
                        for (int stampIndex = stampDataSize * stampIndexY, stampIndexX = 0; stampIndexX < stampSize; ++stampIndex, ++stampIndexX)
                        {
                            float iXYJ = stampData[stampIndex]; // tree's light stamp value
                            if (iXYJ != 0.0F) // zero = no tree shading => LIF intensity = 1 => no change in light grid
                            {
                                int lightTorusX = ResourceUnitTrees.ToTorusLightIndex(stampLightOriginX + stampIndexX, resourceUnitLightGridOriginX, resourceUnitLightGridMaxX);
                                int heightTorusX = lightTorusX / Constant.Grid.LightCellsPerHeightCellWidth;
                                float dominantHeightInM = vegetationHeightGrid[heightTorusX, heightTorusY]; // height of Z* on the current position
                                float zStarXYJ = treeHeightInM - treeLightStamp.GetDistanceToCenterInM(stampIndexX, stampIndexY); // distance to center = height (45 degree line)
                                if (zStarXYJ < 0.0F)
                                {
                                    zStarXYJ = 0.0F;
                                }
                                float zStarMin = (zStarXYJ >= dominantHeightInM) ? 1.0F : zStarXYJ / dominantHeightInM;
                                // old: value = 1. - value*mOpacity / local_dom;
                                float iStarXYJ = 1.0F - iXYJ * treeOpacity * zStarMin;
                                if (iStarXYJ < Constant.MinimumLightIntensity)
                                {
                                    iStarXYJ = Constant.MinimumLightIntensity; // limit minimum value
                                }

                                int stampTorusX = ResourceUnitTrees.ToTorusLightIndex(stampBufferOriginX + stampIndexX, 0, Constant.Grid.LightCellsPerRUWidth);
                                lightBuffer[stampTorusX, stampTorusY] *= iStarXYJ; // use wraparound coordinates
                            }
                        }
                    }
                }
            }

            lightBuffer.ApplyToLightGrid(landscape.LightGrid, resourceUnitLightGridOriginX, resourceUnitLightGridOriginY);
            lightBuffers.Enqueue(lightBuffer);
        }

        /// <summary>
        /// Calculates the resource unit's dominant height field on a local buffer and then uses the buffer to lift the vegetation 
        /// height grid.
        /// </summary>
        /// <remarks>
        /// Thread safe. Since the dominant height field may extend one height cell past the resource unit the dominant height
        /// buffer is a 12 x 12 grid (100 m resource unit / 10 m height cell + one height cell margin on each side). A write lock
        /// is therefore taken when evaluating max(vegetation height grid, dominant height buffer) to avoid race conditions between
        /// threads concurrently finding the dominant height fields of spatially adjacent resource units.
        /// </remarks>
        public void CalculateDominantHeightField(Landscape landscape, ConcurrentQueue<DominantHeightBuffer> dominantHeightBuffers)
        {
            if (dominantHeightBuffers.TryDequeue(out DominantHeightBuffer? dominantHeightBuffer) == false)
            {
                dominantHeightBuffer = new(isTorus: false);
            }
            dominantHeightBuffer.Fill(Constant.RegenerationLayerHeight);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int bufferLightOriginX = resourceUnitLightGridOrigin.X - Constant.Grid.LightCellsPerHeightCellWidth;
            int bufferLightOriginY = resourceUnitLightGridOrigin.Y - Constant.Grid.LightCellsPerHeightCellWidth;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp? readerStamp = treesOfSpecies.LightStamp[treeIndex].ReaderStamp;
                    Debug.Assert(readerStamp != null);

                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeBufferIndexX = (treeLightCellIndexXY.X - bufferLightOriginX) / Constant.Grid.LightCellsPerHeightCellWidth;
                    int treeBufferIndexY = (treeLightCellIndexXY.Y - bufferLightOriginY) / Constant.Grid.LightCellsPerHeightCellWidth;

                    // count trees that are on height-grid cells (used for stockable area)
                    int heightCellIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, treeBufferIndexY);
                    float currentVegetationHeightInM = dominantHeightBuffer[heightCellIndex];
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        dominantHeightBuffer[heightCellIndex] = treeHeightInM;
                    }

                    // if tree is both large enough and close enough to an edge of the height cell it's within, consider it as a
                    // dominant height in the neighboring height cell
                    // For now this applies only to neighbors in the cardinal directions and doesn't consider diagonal neighbors.
                    int readerRadiusInLightCells = readerStamp.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                    int lightSubcellIndexEastWest = treeLightCellIndexXY.X % Constant.Grid.LightCellsPerHeightCellWidth; // 0 = west edge of height cell, 4 = east edge of height cell
                    int lightSubcellIndexNorthSouth = treeLightCellIndexXY.Y % Constant.Grid.LightCellsPerHeightCellWidth; // 0 = southern edge of height cell, 4 = northern edge of height cell
                    if (lightSubcellIndexEastWest - readerRadiusInLightCells < 0)
                    {   
                        // tree's reader stamp extends into height cell to the west
                        int westNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX - 1, treeBufferIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[westNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[westNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexEastWest + readerRadiusInLightCells >= Constant.Grid.LightCellsPerHeightCellWidth)
                    {   
                        // tree's reader stamp extends into height cell to the east
                        int eastNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX + 1, treeBufferIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[eastNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[eastNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexNorthSouth - readerRadiusInLightCells < 0)
                    {   // tree's reader stamp extends into height cell to the south
                        int southNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, treeBufferIndexY - 1);
                        currentVegetationHeightInM = dominantHeightBuffer[southNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[southNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexNorthSouth + readerRadiusInLightCells >= Constant.Grid.LightCellsPerHeightCellWidth)
                    {  
                        // tree's reader stamp extends into height cell to the north
                        int northNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, treeBufferIndexY + 1);
                        currentVegetationHeightInM = dominantHeightBuffer[northNeighborIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[northNeighborIndex] = treeHeightInM;
                        }
                    }
                }
            }

            lock (landscape.VegetationHeightGrid)
            {
                dominantHeightBuffer.ApplyToHeightGrid(landscape.VegetationHeightGrid, bufferLightOriginX, bufferLightOriginY);
            }
            dominantHeightBuffers.Enqueue(dominantHeightBuffer);
        }

        /// <summary>
        /// Calculates the resource unit's dominant height field as wrapped onto a toroidal local buffer and then transfers the 
        /// buffer to the vegetation height grid.
        /// </summary>
        /// <remarks>
        /// Thread safe by default. Since the dominant height field is torus wrapped updates occur only within the spatial extent
        /// of the resource unit being processed and no race conditions exist between threads. Use of a dominant height buffer
        /// therefore isn't necessary (lifting could be performed directly on the vegetation height grid) but is retained as
        /// profiling of the non-torus case suggests there is some locality advantage to using a buffer.
        /// </remarks>
        public void CalculateDominantHeightFieldTorus(Landscape landscape, ConcurrentQueue<DominantHeightBuffer> dominantHeightBuffers)
        {
            if (dominantHeightBuffers.TryDequeue(out DominantHeightBuffer? dominantHeightBuffer) == false)
            {
                dominantHeightBuffer = new(isTorus: true);
            }
            dominantHeightBuffer.Fill(Constant.RegenerationLayerHeight);

            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int bufferLightOriginX = resourceUnitLightGridOrigin.X;
            int bufferLightOriginY = resourceUnitLightGridOrigin.Y;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    // height of Z*
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeBufferIndexX = (treeLightCellIndexXY.X - bufferLightOriginX) / Constant.Grid.LightCellsPerHeightCellWidth;
                    if (treeBufferIndexX >= Constant.Grid.HeightCellsPerRUWidth)
                    {
                        treeBufferIndexX = 0 + treeBufferIndexX - Constant.Grid.HeightCellsPerRUWidth;
                    }
                    int treeBufferIndexY = (treeLightCellIndexXY.Y - bufferLightOriginY) / Constant.Grid.LightCellsPerHeightCellWidth;
                    if (treeBufferIndexY >= Constant.Grid.HeightCellsPerRUWidth)
                    {
                        treeBufferIndexY = 0 + treeBufferIndexX - Constant.Grid.HeightCellsPerRUWidth;
                    }

                    // count trees that are on height-grid cells (used for stockable area)
                    int heightCellIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, treeBufferIndexY);
                    float currentVegetationHeightInM = dominantHeightBuffer[heightCellIndex];
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    if (treeHeightInM > currentVegetationHeightInM)
                    {
                        dominantHeightBuffer[heightCellIndex] = treeHeightInM;
                    }

                    LightStamp reader = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                    int readerRadiusInLightCells = reader.CenterCellIndex; // distance between edge and the center pixel. e.g.: if r = 2 . stamp=5x5
                    int lightSubcellIndexEastWest = treeLightCellIndexXY.X % Constant.Grid.LightCellsPerHeightCellWidth; // 0 = west edge of height cell, 4 = east edge of height cell
                    int lightSubcellIndexNorthSouth = treeLightCellIndexXY.Y % Constant.Grid.LightCellsPerHeightCellWidth; // 0 = southern edge of height cell, 4 = northern edge of height cell
                    if (lightSubcellIndexEastWest - readerRadiusInLightCells < 0)
                    {
                        // tree's reader stamp extends into height cell to the west
                        int westNeighborIndexX = treeBufferIndexX - 1;
                        if (westNeighborIndexX < 0)
                        {
                            westNeighborIndexX = Constant.Grid.HeightCellsPerRUWidth;
                        }
                        int westNeighborIndex = dominantHeightBuffer.IndexXYToIndex(westNeighborIndexX, treeBufferIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[westNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[westNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexEastWest + readerRadiusInLightCells >= Constant.Grid.LightCellsPerHeightCellWidth)
                    {
                        // tree's reader stamp extends into height cell to the east
                        int eastNeighborIndexX = treeBufferIndexX + 1;
                        if (eastNeighborIndexX >= Constant.Grid.HeightCellsPerRUWidth)
                        {
                            eastNeighborIndexX = 0;
                        }
                        int eastNeighborIndex = dominantHeightBuffer.IndexXYToIndex(eastNeighborIndexX, treeBufferIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[eastNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[eastNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexNorthSouth - readerRadiusInLightCells < 0)
                    {
                        // tree's reader stamp extends into height cell to the south
                        int southNeighborIndexY = treeBufferIndexY - 1;
                        if (southNeighborIndexY < 0)
                        {
                            southNeighborIndexY = Constant.Grid.HeightCellsPerRUWidth;
                        }
                        int southNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, southNeighborIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[southNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[southNeighborIndex] = treeHeightInM;
                        }
                    }
                    if (lightSubcellIndexNorthSouth + readerRadiusInLightCells >= Constant.Grid.LightCellsPerHeightCellWidth)
                    {
                        // tree's reader stamp extends into height cell to the north
                        int northNeighborIndexY = treeBufferIndexY + 1;
                        if (northNeighborIndexY >= Constant.Grid.HeightCellsPerRUWidth)
                        {
                            northNeighborIndexY = 0;
                        }
                        int northNeighborIndex = dominantHeightBuffer.IndexXYToIndex(treeBufferIndexX, northNeighborIndexY);
                        currentVegetationHeightInM = dominantHeightBuffer[northNeighborIndex];
                        treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                        if (treeHeightInM > currentVegetationHeightInM)
                        {
                            dominantHeightBuffer[northNeighborIndex] = treeHeightInM;
                        }
                    }
                }
            }

            dominantHeightBuffer.ApplyToHeightGrid(landscape.VegetationHeightGrid, bufferLightOriginX, bufferLightOriginY);
            dominantHeightBuffers.Enqueue(dominantHeightBuffer);
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

        private TreeListSpatial GetOrAddTreeSpecies(WorldFloraID speciesID, int treesToAdd)
        {
            if (this.TreesBySpeciesID.TryGetValue(speciesID, out TreeListSpatial? treesOfSpecies))
            {
                return treesOfSpecies;
            }

            foreach (ResourceUnitTreeSpecies ruSpecies in this.SpeciesAvailableOnResourceUnit)
            {
                TreeSpecies treeSpecies = ruSpecies.Species;
                if (speciesID == treeSpecies.WorldFloraID)
                {
                    int treeCapacity = Simd128.RoundUpToWidth32(treesToAdd);
                    treesOfSpecies = new(this.resourceUnit, treeSpecies, treeCapacity);
                    this.TreesBySpeciesID.Add(speciesID, treesOfSpecies);
                    Debug.Assert(treesOfSpecies.Species.WorldFloraID == speciesID);
                    return treesOfSpecies;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(speciesID));
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

        /** reads the light influence field value for a tree.
            The LIF field is scanned within the crown area of the focal tree, and the influence of
            the focal tree is "subtracted" from the LIF values.
            Finally, the "LRI correction" is applied.
            see http://iland-model.org/competition+for+light for details.
          */
        public void ReadLightInfluenceField(Landscape landscape)
        {
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex]!;
                    LightStamp treeReaderStamp = treesOfSpecies.LightStamp[treeIndex]!.ReaderStamp!;
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    float treeOpacity = treesOfSpecies.Opacity[treeIndex];
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeLightCellIndexX = treeLightCellIndexXY.X;
                    int treeLightCellIndexY = treeLightCellIndexXY.Y;

                    int lightStampSize = treeLightStamp.DataSize;
                    float[] lightStampData = treeLightStamp.Data;

                    int readerStampCenterIndex = treeReaderStamp.CenterCellIndex;
                    int readerToLightShift = treeLightStamp.CenterCellIndex - readerStampCenterIndex;
                    float[] readerStampData = treeReaderStamp.Data;
                    int readerStampDataSize = treeReaderStamp.DataSize;
                    int readerStampOriginX = treeLightCellIndexX - readerStampCenterIndex;
                    int readerStampOriginY = treeLightCellIndexY - readerStampCenterIndex;
                    int readerStampSize = treeReaderStamp.GetSizeInLightCells();

                    float lightResourceIndex = 0.0F;
                    for (int readerIndexY = 0; readerIndexY < readerStampSize; ++readerIndexY)
                    {
                        int lightIndexY = readerStampOriginY + readerIndexY;
                        int heightRowOrigin = vegetationHeightGrid.IndexXYToIndex(0, lightIndexY / Constant.Grid.LightCellsPerHeightCellWidth);
                        for (int lightStampIndex = (readerIndexY + readerToLightShift) * lightStampSize + readerToLightShift, readerIndex = readerStampDataSize * readerIndexY, readerIndexX = 0; readerIndexX < readerStampSize; ++lightStampIndex, ++readerIndex, ++readerIndexX)
                        {
                            int lightIndexX = readerStampOriginX + readerIndexX;
                            int heightIndex = heightRowOrigin + lightIndexX / Constant.Grid.LightCellsPerHeightCellWidth;
                            float vegetationHeightInM = vegetationHeightGrid[heightIndex];
                            float influenceZ = treeHeightInM - treeReaderStamp.GetDistanceToCenterInM(readerIndexX, readerIndexY); // distance to center = height (45 degree line)
                            if (influenceZ < 0.0F)
                            {
                                influenceZ = 0.0F;
                            }
                            float influenceZstar = (influenceZ >= vegetationHeightInM) ? 1.0F : influenceZ / vegetationHeightInM;

                            float cellLightIntensity = 1.0F - lightStampData[lightStampIndex] * treeOpacity * influenceZstar;
                            if (cellLightIntensity < Constant.MinimumLightIntensity)
                            {
                                cellLightIntensity = Constant.MinimumLightIntensity;
                            }
                            
                            float lightValue = lightGrid[lightIndexX, lightIndexY];
                            Debug.Assert((lightValue >= 0.0F) && (lightValue <= 1.0F));
                            float cellLightResourceIndex = lightValue / cellLightIntensity; // remove impact of focal tree

                            // Debug.WriteLine(x + y + local_dom + z + z_zstar + own_value + value + *(grid_value-1) + (*reader)(x,y) + mStamp.offsetValue(x,y,d_offset);
                            // if (value>0.)
                            lightResourceIndex += cellLightResourceIndex * readerStampData[readerIndex];
                        }
                    }

                    // LRI correction...
                    float relativeHeight = treeHeightInM / vegetationHeightGrid[treeLightCellIndexX, treeLightCellIndexY, Constant.Grid.LightCellsPerHeightCellWidth];
                    if (relativeHeight < 1.0F)
                    {
                        lightResourceIndex = treesOfSpecies.Species.SpeciesSet.GetLriCorrection(lightResourceIndex, relativeHeight);
                    }

                    Debug.Assert((Single.IsNaN(lightResourceIndex) == false) && (lightResourceIndex >= 0.0F) && (lightResourceIndex < 50.0F), "Light resource index for tree " + treesOfSpecies.TreeID[treeIndex] + " is " + lightResourceIndex + "."); // sanity upper bound
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
        public void ReadLightInfluenceFieldTorus(Landscape landscape)
        {
            Grid<float> lightGrid = landscape.LightGrid;
            Grid<float> vegetationHeightGrid = landscape.VegetationHeightGrid;
            Point resourceUnitLightGridOrigin = landscape.LightGrid.GetCellXYIndex(this.resourceUnit.ProjectExtent.X, this.resourceUnit.ProjectExtent.Y);
            int resourceUnitLightGridOriginX = resourceUnitLightGridOrigin.X; // inclusive
            int resourceUnitLightGridOriginY = resourceUnitLightGridOrigin.Y; // inclusive
            int resourceUnitLightGridMaxX = resourceUnitLightGridOriginX + Constant.Grid.LightCellsPerRUWidth; // exclusive
            int resourceUnitLightGridMaxY = resourceUnitLightGridOriginY + Constant.Grid.LightCellsPerRUWidth; // exclusive
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    LightStamp treeLightStamp = treesOfSpecies.LightStamp[treeIndex]!;
                    LightStamp treeReaderStamp = treeLightStamp.ReaderStamp!;
                    float treeHeightInM = treesOfSpecies.HeightInM[treeIndex];
                    float treeOpacity = treesOfSpecies.Opacity[treeIndex];
                    Point treeLightCellIndexXY = treesOfSpecies.LightCellIndexXY[treeIndex];
                    int treeLightCellIndexX = treeLightCellIndexXY.X;
                    int treeLightCellIndexY = treeLightCellIndexXY.Y;

                    int lightStampSize = treeLightStamp.DataSize;
                    float[] lightStampData = treeLightStamp.Data;

                    int readerStampCenterIndex = treeReaderStamp.CenterCellIndex;
                    int readerToLightShift = treeLightStamp.CenterCellIndex - readerStampCenterIndex;
                    float[] readerStampData = treeReaderStamp.Data;
                    int readerStampDataSize = treeReaderStamp.DataSize;
                    int readerStampOriginX = treeLightCellIndexX - readerStampCenterIndex;
                    int readerStampOriginY = treeLightCellIndexY - readerStampCenterIndex;
                    int readerStampSize = treeReaderStamp.GetSizeInLightCells();

                    float lightResourceIndex = 0.0F;
                    for (int readerIndexY = 0; readerIndexY < readerStampSize; ++readerIndexY)
                    {
                        int readerTorusY = ResourceUnitTrees.ToTorusLightIndex(readerStampOriginY + readerIndexY, resourceUnitLightGridOriginY, resourceUnitLightGridMaxY);
                        int heightRowOrigin = vegetationHeightGrid.IndexXYToIndex(0, readerTorusY / Constant.Grid.LightCellsPerHeightCellWidth);
                        for (int lightStampIndex = (readerIndexY + readerToLightShift) * lightStampSize + readerToLightShift, readerIndex = readerStampDataSize * readerIndexY, readerIndexX = 0; readerIndexX < readerStampSize; ++lightStampIndex, ++readerIndex, ++readerIndexX)
                        {
                            // see http://iland-model.org/competition+for+light 
                            int readerTorusX = ResourceUnitTrees.ToTorusLightIndex(readerStampOriginX + readerIndexX, resourceUnitLightGridOriginX, resourceUnitLightGridMaxX);
                            int heightIndex = heightRowOrigin + readerTorusX / Constant.Grid.LightCellsPerHeightCellWidth;
                            float vegetationHeightInM = vegetationHeightGrid[heightIndex];
                            float influenceZ = treeHeightInM - treeReaderStamp.GetDistanceToCenterInM(readerIndexX, readerIndexY); // distance to center = height (45 degree line)
                            if (influenceZ < 0.0F)
                            {
                                influenceZ = 0.0F;
                            }
                            float influenceZstar = (influenceZ >= vegetationHeightInM) ? 1.0F : influenceZ / vegetationHeightInM;

                            // TODO: why a nonzero floor as opposed to skipping division?
                            float cellLightIntensity = 1.0F - lightStampData[lightStampIndex] * treeOpacity * influenceZstar;
                            if (cellLightIntensity < Constant.MinimumLightIntensity)
                            {
                                cellLightIntensity = Constant.MinimumLightIntensity;
                            }
                            // C++ code is actually Tree.LightGrid[Tree.LightGrid.IndexOf(xTorus, yTorus) + 1], which appears to be an off by
                            // one error corrected by Qt build implementing precdence in *ptr++ incorrectly.
                            float lightValue = lightGrid[readerTorusX, readerTorusY];
                            Debug.Assert((lightValue >= 0.0F) && (lightValue <= 1.0F));
                            float cellLightResourceIndex = lightValue / cellLightIntensity; // remove impact of focal tree

                            lightResourceIndex += cellLightResourceIndex * readerStampData[readerIndex];
                        }
                    }

                    // LRI correction...
                    float relativeHeight = treeHeightInM / vegetationHeightGrid[treeLightCellIndexX, treeLightCellIndexY, Constant.Grid.LightCellsPerHeightCellWidth];
                    if (relativeHeight < 1.0F)
                    {
                        lightResourceIndex = treesOfSpecies.Species.SpeciesSet.GetLriCorrection(lightResourceIndex, relativeHeight);
                    }

                    Debug.Assert((Single.IsNaN(lightResourceIndex) == false) && (lightResourceIndex >= 0.0F) && (lightResourceIndex < 50.0F)); // sanity upper bound
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

            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
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

                            int simdCompatibleTreeCapacity = Simd128.RoundUpToWidth32(treesOfSpecies.Count);
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
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = this.resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                ResourceUnitTreeSpecies speciesOnRU = this.GetResourceUnitSpecies(treesOfSpecies.Species);
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    float agingFactor = treesOfSpecies.Species.GetAgingFactor(treesOfSpecies.HeightInM[treeIndex], treesOfSpecies.AgeInYears[treeIndex]);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToTorusLightIndex(int lightIndex, int torusOriginIndex, int torusMaximumIndex)
        {
            if (lightIndex < torusOriginIndex)
            {
                // if reader is past -y edge of resource unit reflect below +y edge
                // Origin index is inclusive.
                lightIndex = torusMaximumIndex + lightIndex - torusOriginIndex;
            }
            else if (lightIndex >= torusMaximumIndex)
            {
                // if reader is past +y edge of resource unit reflect to above -y edge
                // Max index is exclusive.
                lightIndex = torusOriginIndex + lightIndex - torusMaximumIndex;
            }
            Debug.Assert((lightIndex >= torusOriginIndex) && (lightIndex <= torusMaximumIndex));
            return lightIndex;
        }
    }
}
