using iLand.Extensions;
using iLand.Input.ProjectFile;
using iLand.Input.Tree;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace iLand.World
{
    /** loads (initializes) trees for a "stand" from various sources.
        StandLoader initializes trees on the landscape. It reads (usually) from text files, creates the
        trees and distributes the trees on the landscape (on the ResourceUnit or on a stand defined by a grid).

        See http://iland-model.org/initialize+trees
        */
    internal class TreePopulator
    {
        // evenlist: tentative order of pixel-indices (within a 5x5 grid) used as tree positions.
        // e.g. 12 = centerpixel, 0: upper left corner, ...
        private static readonly int[] EvenHeightCellPositions = new int[] { 12, 6, 18, 16, 8, 22, 2, 10, 14, 0, 24, 20, 4, 1, 13, 15, 19, 21, 3, 7, 11, 17, 23, 5, 9 };
        private static readonly int[] UnevenHeightCellPositions = new int[] { 11, 13, 7, 17, 1, 19, 5, 21, 9, 23, 3, 15, 6, 18, 2, 10, 4, 24, 12, 0, 8, 14, 20, 22 };

        // set a constraining height grid (10m resolution)
        private Expression? heightGridResponse; // response function to calculate fitting of pixels with pre-determined height
        private GridRaster10m initialHeightGrid; // grid with tree heights

        private RandomCustomPdf? treeSizeDistribution;

        public TreePopulator()
        {
            this.heightGridResponse = null;
            this.initialHeightGrid = new();
            this.treeSizeDistribution = null;
        }

        private void ApplyTreeFileToResourceUnit(Project projectFile, Landscape landscape, ResourceUnit resourceUnit, RandomGenerator randomGenerator, TreeReader treeReader, int standID)
        {
            if (treeReader is IndividualTreeReader individualTreeReader)
            {
                float lightStampBeerLambertK = projectFile.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
                TreePopulator.PopulateResourceUnitWithIndividualTrees(landscape, resourceUnit, individualTreeReader, lightStampBeerLambertK);
            }
            else if (treeReader is TreeSizeDistributionReaderCsv sizeDistributionReader)
            {
                this.PopulateStandTreesFromSizeDistribution(projectFile, landscape, sizeDistributionReader.TreeSizeDistribution, randomGenerator, standID);
            }
            else
            {
                throw new NotSupportedException("Unhandled tree file format in '" + treeReader.FilePath + "'. Expected either a list of individual trees or a tree size distribution. Does a tree file list used with a stand raster unexpectedly point to another tree file list?");
            }
        }

        private static int CompareHeightInitCells(StandHeightInitCell s1, StandHeightInitCell s2)
        {
            if (s1.BasalArea < s2.BasalArea)
            {
                return -1;
            }
            if (s1.BasalArea > s2.BasalArea)
            {
                return 1;
            }
            return 0;
        }

        private static int CompareInitPixelUnlocked(StandHeightInitCell s1, StandHeightInitCell s2)
        {
            if (!s1.IsSingleSpecies && s2.IsSingleSpecies)
            {
                return -1;
            }
            return 0;
        }

        //public void CopyTrees()
        //{
        //    // we assume that all stands are equal, so we simply COPY the trees and modify them afterwards
        //    Grid<ResourceUnit> ruGrid = mModel.ResourceUnitGrid;
        //    if (ruGrid[0] == null)
        //    {
        //        throw new NotSupportedException("Standloader: invalid resource unit pointer!");
        //    }

        //    // skip the first RU...
        //    List<Tree> tocopy = mModel.FirstResourceUnit().Trees;
        //    for (int p = 1; p < ruGrid.Count; ++p)
        //    {
        //        RectangleF rect = ruGrid[p].BoundingBox;
        //        foreach (Tree tree in tocopy)
        //        {
        //            Tree newtree = ruGrid[p].NewTree();
        //            newtree = tree; // copy tree data...
        //            newtree.SetLightCellIndex(tree.GetCellCenterPoint().Add(rect.TopLeft()));
        //            newtree.RU = ruGrid[p];
        //            newtree.SetNewID();
        //        }
        //    }
        //    // if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
        //    // {
        //    //     Trace.TraceInformation(Tree.TreesCreated + " trees loaded / copied.");
        //    // }
        //}

        private static void EvaluateDebugTrees(Landscape landscape, string debugTreeExpressionString)
        {
            // evaluate debugging
            if (String.Equals(debugTreeExpressionString, "debugstamp", StringComparison.Ordinal))
            {
                // check for trees which aren't correctly placed
                AllTreesEnumerator treeEnumerator = new(landscape);
                while (treeEnumerator.MoveNext())
                {
                    if (landscape.LightGrid.Contains(treeEnumerator.CurrentTrees.LightCellIndexXY[treeEnumerator.CurrentTreeIndex]) == false)
                    {
                        throw new NotSupportedException("debugstamp: invalid tree position found.");
                    }
                }
                return;
            }

            TreeVariableAccessor treeWrapper = new(null);
            Expression debugTreeExpression = new(debugTreeExpressionString, treeWrapper); // load expression dbg_str and enable external model variables
            AllTreesEnumerator allTreeEnumerator = new(landscape);
            while (allTreeEnumerator.MoveNext())
            {
                // TODO: why is debug expression evaluated for all trees rather than just trees marked for debugging?
                treeWrapper.Trees = allTreeEnumerator.CurrentTrees;
                treeWrapper.TreeIndex = allTreeEnumerator.CurrentTreeIndex;
                float result = debugTreeExpression.Execute();
                if (result != 0.0F)
                {
                    allTreeEnumerator.CurrentTrees.SetDebugging(allTreeEnumerator.CurrentTreeIndex);
                }
            }
        }

        // high risk member: likely to require debugging and additional test coverage
        private Point FindLightCellIndexXYForNewTree(Landscape landscape, ResourceUnit resourceUnit, int[] treeCountByHeightCell, TreeSizeRange sizeRange, ref (int TreePlacementBits, int TreePlacementIndex) treePlacementState, RandomGenerator randomGenerator)
        {
            Debug.Assert(this.treeSizeDistribution != null);

            // calculate random value. "density" is from 1..-1.
            float randomValue = this.treeSizeDistribution.GetRandomValue(randomGenerator);
            if (sizeRange.Density < 0.0F)
            {
                randomValue = 1.0F - randomValue;
            }
            randomValue = randomValue * sizeRange.Density + randomGenerator.GetRandomProbability() * (1.0F - sizeRange.Density);

            // pick a randomized light cell to place this tree in
            int heightCellIndex = Maths.Limit((int)(Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth * randomValue), 0, Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth - 1); // get from random number generator

            int treesAlreadyPresentInCell = treeCountByHeightCell[heightCellIndex];
            if (treesAlreadyPresentInCell > 18)
            {
                treePlacementState.TreePlacementIndex = (treePlacementState.TreePlacementIndex + 1) % 25;
            }
            else
            {
                int stop = 1000;
                do
                {
                    //r = drandom();
                    //if (r<0.5)  // skip position with a prob. of 50% -> adds a little "noise"
                    //    index++;
                    //index = (index + 1)%25; // increase and roll over

                    // search a random position
                    float r = randomGenerator.GetRandomProbability();
                    treePlacementState.TreePlacementIndex = Maths.Limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                }
                while (Maths.IsBitSet(treePlacementState.TreePlacementBits, treePlacementState.TreePlacementIndex) == true && stop-- != 0);

                Maths.SetBit(ref treePlacementState.TreePlacementBits, treePlacementState.TreePlacementIndex, true); // mark position as used
            }

            // get position from fixed lists (one for even, one for uneven resource units)
            int lightCellIndexWithinHeightCell = resourceUnit.ResourceUnitGridIndex % Constant.Grid.LightCellSizeInM != 0 ? TreePopulator.EvenHeightCellPositions[treePlacementState.TreePlacementIndex] : TreePopulator.UnevenHeightCellPositions[treePlacementState.TreePlacementIndex];
            // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
            PointF ruGridOriginInProjectCoordinates = resourceUnit.ProjectExtent.Location;
            Point ruLightIndexXY = landscape.LightGrid.GetCellXYIndex(ruGridOriginInProjectCoordinates);
            Point lightCellIndexXY = new(ruLightIndexXY.X + Constant.Grid.LightCellsPerHeightCellWidth * (heightCellIndex / Constant.Grid.HeightCellSizeInM) + lightCellIndexWithinHeightCell / Constant.Grid.LightCellsPerHeightCellWidth,
                                         ruLightIndexXY.Y + Constant.Grid.LightCellsPerHeightCellWidth * (heightCellIndex % Constant.Grid.HeightCellSizeInM) + lightCellIndexWithinHeightCell % Constant.Grid.LightCellsPerHeightCellWidth);
            Debug.Assert(resourceUnit.ProjectExtent.Contains(landscape.LightGrid.GetCellProjectCentroid(lightCellIndexXY)));

            treeCountByHeightCell[heightCellIndex] = treesAlreadyPresentInCell + 1;
            return lightCellIndexXY;
        }

        /// a (hacky) way of adding saplings of a certain age to a stand defined by 'stand_id'.
        //public int LoadSaplings(Model model, string content, int standID)
        //{
        //    MapGrid standGrid = model.StandGrid;
        //    List<int> indices = standGrid.GetGridIndices(standID); // list of 10x10m pixels
        //    if (indices.Count == 0)
        //    {
        //        if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
        //        {
        //            Trace.TraceInformational("stand " + standID + " not in project area. No init performed.");
        //        }
        //        return -1;
        //    }
        //    float area_factor = standGrid.GetArea(standID) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

        //    // parse the content of the init-file
        //    // species
        //    CsvFile init = new();
        //    init.LoadFromString(content);
        //    int speciesIndex = init.GetColumnIndex("species");
        //    int countIndex = init.GetColumnIndex("count");
        //    int heightIndex = init.GetColumnIndex("height");
        //    int ageIndex = init.GetColumnIndex("age");
        //    if (speciesIndex == -1 || countIndex == -1)
        //    {
        //        throw new NotSupportedException("Error while loading saplings: columns 'species' or 'count' are missing!!");
        //    }

        //    TreeSpeciesSet set = model.GetFirstSpeciesSet();
        //    int total = 0;
        //    for (int row = 0; row < init.RowCount; ++row)
        //    {
        //        int pxcount = (int)MathF.Round(Single.Parse(init[countIndex, row], CultureInfo.InvariantCulture) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
        //        TreeSpecies species = set.GetSpecies(init[speciesIndex, row]);
        //        if (species == null)
        //        {
        //            throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init[speciesIndex, row)));
        //        }
        //        float height = heightIndex == -1 ? Constant.Sapling.MinimumHeight : Single.Parse(init[heightIndex, row], CultureInfo.InvariantCulture);
        //        int age = ageIndex == -1 ? 1 : Int32.Parse(init[ageIndex, row], CultureInfo.InvariantCulture);

        //        int misses = 0;
        //        int hits = 0;
        //        while (hits < pxcount)
        //        {
        //            // sapling location
        //            int rnd_index = randomGenerator.GetRandomInteger(0, indices.Count);
        //            Point offset = standGrid.Grid.GetCellPosition(indices[rnd_index]);
        //            offset.X *= Constant.LightCellsPerHeightSize; // index of 10m patch -> to lif pixel coordinates
        //            offset.Y *= Constant.LightCellsPerHeightSize;
        //            int in_p = randomGenerator.GetRandomInteger(0, Constant.LightCellsPerHeightSize * Constant.LightCellsPerHeightSize); // index of lif-pixel
        //            offset.X += in_p / Constant.LightCellsPerHeightSize;
        //            offset.Y += in_p % Constant.LightCellsPerHeightSize;

        //            SaplingCell sc = model.Saplings.GetCell(model, offset, true, out ResourceUnit _);
        //            if (sc != null && sc.MaxHeight() > height)
        //            {
        //                //if (!ru || ru.saplingHeightForInit(offset) > height) {
        //                ++misses;
        //            }
        //            else
        //            {
        //                // ok
        //                ++hits;
        //                if (sc != null)
        //                {
        //                    sc.AddSaplingIfSlotFree(height, (int)age, species.Index);
        //                }
        //                //ru.resourceUnitSpecies(species).changeSapling().addSapling(offset, height, age);
        //            }
        //            if (misses > 3 * pxcount)
        //            {
        //                if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
        //                {
        //                    Trace.TraceInformation("tried to add " + pxcount + " saplings at stand " + standID + " but failed in finding enough free positions. Added " + hits + " and stopped.");
        //                }
        //                break;
        //            }
        //        }
        //        total += hits;

        //    }
        //    return total;
        //}

        private static void PopulateResourceUnitsWithIndividualTrees(Project projectFile, Landscape landscape, IndividualTreeReader individualTreeReader, ParallelOptions parallelComputeOptions)
        {
            float lightStampBeerLambertK = projectFile.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            float projectOriginGisCoordinatesX = landscape.ProjectOriginInGisCoordinates.X;
            float projectOriginGisCoordinatesY = landscape.ProjectOriginInGisCoordinates.Y;
            float resourceUnitMinimumX = landscape.ResourceUnitGrid.ProjectExtent.X;
            float resourceUnitMaximumX = resourceUnitMinimumX + landscape.ResourceUnitGrid.ProjectExtent.Width;
            float resourceUnitMinimumY = landscape.ResourceUnitGrid.ProjectExtent.Y;
            float resourceUnitMaximumY = resourceUnitMinimumX + landscape.ResourceUnitGrid.ProjectExtent.Height;

            int treeCount = individualTreeReader.Count;
            (int partitions, int treesPerPartion) = parallelComputeOptions.GetUniformPartitioning(treeCount, Constant.Data.MinimumTreesPerThread);
            Parallel.For(0, partitions, parallelComputeOptions, (int partitionIndex) =>
            {
                (int startTreeIndexInFile, int endTreeIndexInFile) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, treesPerPartion, treeCount);
                ResourceUnit? previousResourceUnit = null;
                int treeAddStartIndex = startTreeIndexInFile;
                Point[] treePositions = new Point[Constant.Data.MaxResourceUnitTreeBatchSize];
                int treesToAdd = 0;
                for (int treeIndexInFile = startTreeIndexInFile; treeIndexInFile < endTreeIndexInFile; ++treeIndexInFile)
                {
                    // if needed, trees of sapling size could be converted to saplings rather than added as treees
                    //if (dbh<5.)
                    //    continue;

                    // translate tree into project coordinates
                    int treeID = individualTreeReader.TreeID[treeIndexInFile];
                    float treeGisX = individualTreeReader.GisX[treeIndexInFile];
                    float treeGisY = individualTreeReader.GisY[treeIndexInFile];
                    float treeProjectX = treeGisX - projectOriginGisCoordinatesX;
                    float treeProjectY = treeGisY - projectOriginGisCoordinatesY;
                    if ((treeProjectX < resourceUnitMinimumX) || (treeProjectY < resourceUnitMinimumY) ||
                        (treeProjectX > resourceUnitMaximumX) || (treeProjectY > resourceUnitMaximumY))
                    {
                        throw new ArgumentOutOfRangeException(nameof(individualTreeReader), "Tree " + treeID + " at GIS coordinates x = " + treeGisX + ", y = " + treeGisY + " m is positioned beyond the extent of the resource unit grid (xmin = " + (resourceUnitMinimumX + projectOriginGisCoordinatesX) + ", ymin = " + (resourceUnitMinimumY + projectOriginGisCoordinatesY) + ", xmax = " + (resourceUnitMaximumX + projectOriginGisCoordinatesX) + ", ymax = " + (resourceUnitMaximumY + projectOriginGisCoordinatesY) + " m) and, therefore, cannot be simulated. Verify trees and resource units are being specified in the same coordinate system and adjust the set of trees and resource units so all trees are within resource units.");
                    }

                    // find resource unit tree is on
                    Point resourceUnitIndexXY = landscape.ResourceUnitGrid.GetCellXYIndex(treeProjectX, treeProjectY);
                    int resourceUnitIndexX = resourceUnitIndexXY.X;
                    int resourceUnitIndexY = resourceUnitIndexXY.Y;
                    ResourceUnit? resourceUnit = landscape.ResourceUnitGrid[resourceUnitIndexX, resourceUnitIndexY];
                    if (resourceUnit == null)
                    {
                        // trees may sometimes be positioned exactly on a resource unit boundary
                        // In this case QGIS 3.22 will, and other tools may, consider the tree to be within a given resource unit grid
                        // cell while iLand will consider the tree to be in an adjacent cell of the resource unit grid. If both cells
                        // are populated with resource units then there is no difficulty within the numerical limits of resource unit
                        // bookkeeping. However, if the cell iLand resolves to is unpopulated then iLand has the options of either
                        //
                        //   1) rejecting well formed GIS input due to mathematical details, which is undesirable
                        //   2) handling the edge case and tipping the tree into the resource unit seen by GIS
                        //
                        // The latter approach is adopted here. There appears to be approximately a one in two million chance these
                        // cases will be hit.
                        if ((treeProjectX - landscape.ResourceUnitGrid.ProjectExtent.X) % Constant.Grid.ResourceUnitSizeInM == 0.0F)
                        {
                            resourceUnit = landscape.ResourceUnitGrid[resourceUnitIndexX - 1, resourceUnitIndexY];
                            if (resourceUnit == null)
                            {
                                if (resourceUnitIndexXY.Y % Constant.Grid.ResourceUnitSizeInM == 0.0F)
                                {
                                    resourceUnit = landscape.ResourceUnitGrid[resourceUnitIndexX - 1, resourceUnitIndexY - 1];
                                    if (resourceUnit != null)
                                    {
                                        treeProjectX -= Constant.Grid.TreeNudgeIntoResourceUnitInM;
                                        treeProjectY -= Constant.Grid.TreeNudgeIntoResourceUnitInM;
                                    }
                                }
                            }
                            else
                            {
                                treeProjectX -= Constant.Grid.TreeNudgeIntoResourceUnitInM;
                            }
                        }
                        else if ((treeProjectY - landscape.ResourceUnitGrid.ProjectExtent.Y) % Constant.Grid.ResourceUnitSizeInM == 0.0F)
                        {
                            resourceUnit = landscape.ResourceUnitGrid[resourceUnitIndexX, resourceUnitIndexY - 1];
                            if (resourceUnit != null)
                            {
                                treeProjectY -= Constant.Grid.TreeNudgeIntoResourceUnitInM;
                            }
                        }
                    }
                    if (resourceUnit == null)
                    {
                        throw new ArgumentOutOfRangeException(nameof(individualTreeReader), "Tree " + treeID + " at GIS coordinates x = " + treeGisX + ", y = " + treeGisY + " m falls within the extents of the resource unit grid but is not positioned on a resource unit and, therefore, cannot be simulated. Verify trees and resource units are being specified in the same coordinate system and adjust the set of trees and resource units so all trees are within resource units.");
                    }

                    Point lightCellIndexXY = landscape.LightGrid.GetCellXYIndex(treeProjectX, treeProjectY);
                    Debug.Assert(resourceUnit.ProjectExtent.Contains(landscape.LightGrid.GetCellProjectCentroid(lightCellIndexXY)));

                    // add batch to resource unit when resource unit changes or when batch capacity is reached
                    if ((Object.ReferenceEquals(previousResourceUnit, resourceUnit) == false) || (treesToAdd == treePositions.Length))
                    {
                        if (previousResourceUnit != null)
                        {
                            lock (previousResourceUnit)
                            {
                                TreeSpanForAddition resourceUnitSlice = new(individualTreeReader, treePositions, treeAddStartIndex, treesToAdd);
                                previousResourceUnit.Trees.AddTrees(resourceUnitSlice, lightStampBeerLambertK);
                            }
                            treeAddStartIndex = treeIndexInFile;
                            treesToAdd = 0;
                        }
                    }

                    // capture tree's position
                    treePositions[treesToAdd] = lightCellIndexXY;
                    ++treesToAdd;

                    previousResourceUnit = resourceUnit;
                }

                // commit final batch
                if ((previousResourceUnit != null) && (treesToAdd > 0))
                {
                    lock (previousResourceUnit)
                    {
                        TreeSpanForAddition resourceUnitSlice = new(individualTreeReader, treePositions, treeAddStartIndex, treesToAdd);
                        previousResourceUnit.Trees.AddTrees(resourceUnitSlice, lightStampBeerLambertK);
                    }
                    // treeBatch.Count = 0; // not necessary as this is the batch's last use
                }
            });
        }

        private void PopulateResourceUnitTreesFromSizeDistribution(Project projectFile, Landscape landscape, ResourceUnit resourceUnit, List<TreeSizeRange> treeSizeDistribution, RandomGenerator randomGenerator)
        {
            float lightStampBeerLambertK = projectFile.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            int[] treeCountByHeightCell = new int[Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth];
            (int TreePlacementBits, int TreePlacementIndex) treePlacementState = (0, -1);
            TreeListForAddition treesToAdd = new(0);
            int uniqueTreeIDonResourceUnit = -1;
            for (int distributionIndex = 0; distributionIndex < treeSizeDistribution.Count; ++distributionIndex)
            {
                TreeSizeRange sizeRange = treeSizeDistribution[distributionIndex];
                int requiredCapacity = Simd128.RoundUpToWidth32(sizeRange.TreesPerResourceUnit);
                if (treesToAdd.Capacity < requiredCapacity)
                {
                    treesToAdd.Resize(requiredCapacity);
                }

                TreeSpecies treeSpecies = resourceUnit.Trees.TreeSpeciesSet[sizeRange.SpeciesID];
                WorldFloraID treeSpeciesID = treeSpecies.WorldFloraID;
                for (int treeIndex = 0; treeIndex < sizeRange.TreesPerResourceUnit; ++treeIndex)
                {
                    treesToAdd.AgeInYears[treeIndex] = sizeRange.Age;

                    float dbhInCm = randomGenerator.GetRandomFloat(sizeRange.DbhFrom, sizeRange.DbhTo);
                    treesToAdd.DbhInCm[treeIndex] = dbhInCm;

                    float heightInM = 0.01F * dbhInCm * sizeRange.HeightDiameterRatio; // dbh from cm->m, *hd-ratio -> meter height
                    treesToAdd.HeightInM[treeIndex] = heightInM;

                    Point lightCellIndexXY = this.FindLightCellIndexXYForNewTree(landscape, resourceUnit, treeCountByHeightCell, sizeRange, ref treePlacementState, randomGenerator);
                    Debug.Assert(resourceUnit.ProjectExtent.Contains(landscape.LightGrid.GetCellProjectCentroid(lightCellIndexXY)));
                    treesToAdd.LightCellIndexXY[treeIndex] = lightCellIndexXY;

                    treesToAdd.SpeciesID[treeIndex] = treeSpeciesID;
                    treesToAdd.StandID[treeIndex] = Constant.DefaultStandID;
                    treesToAdd.TreeID[treeIndex] = ++uniqueTreeIDonResourceUnit;
                }
                treesToAdd.Count = sizeRange.TreesPerResourceUnit;

                resourceUnit.Trees.AddTrees(treesToAdd.AsSpan(), lightStampBeerLambertK);
            }
        }

        // clones a list of trees (given by content) to a resource unit
        private static void PopulateResourceUnitWithIndividualTrees(Landscape landscape, ResourceUnit resourceUnit, IndividualTreeReader individualTreeReader, float lightStampBeerLambertK)
        {
            Point ruPositionInResourceUnitGrid = landscape.ResourceUnitGrid.GetCellXYIndex(resourceUnit.ResourceUnitGridIndex);
            float translationToPlaceTreeOnResourceUnitX = Constant.Grid.ResourceUnitSizeInM * ruPositionInResourceUnitGrid.X;
            float translationToPlaceTreeOnResourceUnitY = Constant.Grid.ResourceUnitSizeInM * ruPositionInResourceUnitGrid.Y;
            Point[] treePositions = new Point[individualTreeReader.Count];

            for (int treeIndexInFile = 0; treeIndexInFile < individualTreeReader.Count; ++treeIndexInFile)
            {
                //if (dbh<5.)
                //    continue;

                // locate tree
                float treeProjectX = individualTreeReader.GisX[treeIndexInFile] - landscape.ProjectOriginInGisCoordinates.X + translationToPlaceTreeOnResourceUnitX;
                float treeProjectY = individualTreeReader.GisY[treeIndexInFile] - landscape.ProjectOriginInGisCoordinates.Y + translationToPlaceTreeOnResourceUnitY;
                //if (landscape.VegetationHeightFlags[treeProjectX, treeProjectY].IsInResourceUnit() == false)
                //{
                //    throw new NotSupportedException("Individual tree " + individualTreeReader.TreeID[treeIndexInFile] + " (line " + (treeIndexInFile + 1) + ") is not located in project simulation area after being displaced to resource unit " + resourceUnit.ID + ". Tree coordinates are (" + treeProjectX + ", " + treeProjectY + ")");
                //}
                Point lightCellIndexXY = landscape.LightGrid.GetCellXYIndex(treeProjectX, treeProjectY);
                Debug.Assert(resourceUnit.ProjectExtent.Contains(landscape.LightGrid.GetCellProjectCentroid(lightCellIndexXY)));

                treePositions[treeIndexInFile] = lightCellIndexXY;
            }

            TreeSpanForAddition treeSpan = new(individualTreeReader, treePositions, 0, individualTreeReader.Count);
            resourceUnit.Trees.AddTrees(treeSpan, lightStampBeerLambertK);
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland-model.org/initialize+trees
        private void PopulateStandTreesFromSizeDistribution(Project projectFile, Landscape landscape, List<TreeSizeRange> treeSizeDistribution, RandomGenerator randomGenerator, int standID)
        {
            GridRaster10m? standRaster = landscape.StandRaster;
            if (standRaster == null)
            {
                throw new NotSupportedException("Landscape does not have a stand raster.");
            }

            // get a list of positions of all pixels that belong to our stand
            List<int> heightCellIndicesInStand = standRaster.GetGridIndices(standID);
            if (heightCellIndicesInStand.Count == 0)
            {
                if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                {
                    Trace.TraceInformation("Stand " + standID + " not in project area. No initialization performed.");
                }
                return;
            }

            List<StandHeightInitCell> heightCellsInStand = new(heightCellIndicesInStand.Count); // working list of all 10m pixels
            for (int heightCellsInstantiated = 0; heightCellsInstantiated < heightCellIndicesInStand.Count; ++heightCellsInstantiated)
            {
                ResourceUnit resourceUnit = landscape.GetResourceUnit(standRaster.Grid.GetCellProjectCentroid(heightCellsInstantiated));
                StandHeightInitCell heightCell = new(resourceUnit)
                {
                    HeightGridIndex = heightCellsInstantiated // index in the 10m grid
                };
                if (initialHeightGrid.IsSetup())
                {
                    heightCell.MaxHeight = initialHeightGrid.Grid[heightCell.HeightGridIndex];
                }
                heightCellsInStand.Add(heightCell);
            }
            float standAreaInResourceUnits = standRaster.GetAreaInSquareMeters(standID) / Constant.Grid.ResourceUnitAreaInM2;

            if (initialHeightGrid.IsSetup() && (heightGridResponse == null))
            {
                throw new NotSupportedException("Attempt to initialize from height grid but without response function.");
            }

            Debug.Assert(this.treeSizeDistribution != null);
            Debug.Assert(heightGridResponse != null);
            int heightCellIndex = 0;
            float lightStampBeerLambertK = projectFile.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            int maxHeightFittingAttempts = projectFile.World.Initialization.HeightGrid.MaxTries;
            ResourceUnit? previousResourceUnit = null;
            WorldFloraID previousSpeciesID = WorldFloraID.Unknown;
            int treeCount = 0;
            (int TreePlacementBits, int TreePlacementIndex) treePlacementState = (0, -1);
            int totalTries = 0;
            int totalMisses = 0;
            int[] treeCountByHeightCell = new int[heightCellIndicesInStand.Count];
            TreeListForAddition treesToAddToMostRecentResourceUnit = new(100);
            for (int distributionIndex = 0; distributionIndex < treeSizeDistribution.Count; ++distributionIndex)
            {
                TreeSizeRange treeSizeRange = treeSizeDistribution[distributionIndex];
                if (treeSizeRange.Density > 1.0)
                {
                    // special case with single-species-area
                    if (treeCount == 0)
                    {
                        // randomize the pixels
                        for (int heightIndex = 0; heightIndex < heightCellsInStand.Count; ++heightIndex)
                        {
                            heightCellsInStand[heightIndex].BasalArea = randomGenerator.GetRandomProbability();
                        }
                        heightCellsInStand.Sort(TreePopulator.CompareHeightInitCells);

                        for (int heightIndex = 0; heightIndex < heightCellsInStand.Count; ++heightIndex)
                        {
                            heightCellsInStand[heightIndex].BasalArea = 0.0F;
                        }
                    }

                    if (treeSizeRange.SpeciesID != previousSpeciesID)
                    {
                        previousSpeciesID = treeSizeRange.SpeciesID;
                        heightCellsInStand.Sort(TreePopulator.CompareInitPixelUnlocked);
                    }
                }
                else
                {
                    heightCellsInStand.Sort(TreePopulator.CompareHeightInitCells);
                    previousSpeciesID = WorldFloraID.Unknown;
                }

                float randomFraction = treeSizeRange.Density;
                float maxHeightInM = 0.01F * treeSizeRange.DbhTo * treeSizeRange.HeightDiameterRatio;
                int treesToAdd = (int)(treeSizeRange.TreesPerResourceUnit * standAreaInResourceUnits + 0.5F); // round
                for (int treesAdded = 0; treesAdded < treesToAdd; ++treesAdded)
                {
                    bool found = false;
                    int tries = maxHeightFittingAttempts;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        float randomValue;
                        if (treeSizeRange.Density <= 1.0)
                        {
                            randomValue = this.treeSizeDistribution.GetRandomValue(randomGenerator);
                            if (treeSizeRange.Density < 0)
                            {
                                randomValue = 1.0F - randomValue;
                            }
                            randomValue = randomValue * randomFraction + randomGenerator.GetRandomProbability() * (1.0F - randomFraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            randomValue = randomGenerator.GetRandomProbability() * MathF.Min(treeSizeRange.Density / 100.0F, 1.0F);
                        }
                        ++totalTries;

                        // key: rank of target pixel
                        heightCellIndex = Maths.Limit((int)(heightCellsInStand.Count * randomValue), 0, heightCellsInStand.Count - 1); // get from random number generator

                        if (initialHeightGrid.IsSetup())
                        {
                            // calculate how well the selected pixel fits w.r.t. the predefined height
                            if (heightCellsInStand[heightCellIndex].MaxHeight > 0.0F)
                            {
                                float heightResponse = heightGridResponse.Evaluate(maxHeightInM / heightCellsInStand[heightCellIndex].MaxHeight);
                                if (randomGenerator.GetRandomProbability() < heightResponse)
                                {
                                    found = true;
                                }
                            }
                        }
                        else
                        {
                            found = true;
                        }
                        if ((previousSpeciesID != WorldFloraID.Unknown) && heightCellsInStand[heightCellIndex].IsSingleSpecies)
                        {
                            found = false;
                        }
                    }
                    if (tries < 0)
                    {
                        ++totalMisses;
                    }

                    // add trees when resource unit changes
                    ResourceUnit resourceUnit = heightCellsInStand[heightCellIndex].ResourceUnit;
                    if ((Object.ReferenceEquals(resourceUnit, previousResourceUnit) == false) && (previousResourceUnit != null))
                    {
                        lock (previousResourceUnit)
                        {
                            previousResourceUnit.Trees.AddTrees(treesToAddToMostRecentResourceUnit.AsSpan(), lightStampBeerLambertK);
                            treesToAddToMostRecentResourceUnit.Count = 0;
                        }
                    }

                    int treeIndex = treesToAddToMostRecentResourceUnit.Count;
                    treesToAddToMostRecentResourceUnit.SpeciesID[treeIndex] = treeSizeRange.SpeciesID;

                    float dbhInCm = randomGenerator.GetRandomFloat(treeSizeRange.DbhFrom, treeSizeRange.DbhTo);
                    treesToAddToMostRecentResourceUnit.DbhInCm[treeIndex] = dbhInCm;

                    float heightInM = 0.01F * dbhInCm * treeSizeRange.HeightDiameterRatio;
                    treesToAddToMostRecentResourceUnit.HeightInM[treeIndex] = heightInM;

                    Point lightCellIndexXY = this.FindLightCellIndexXYForNewTree(landscape, resourceUnit, treeCountByHeightCell, treeSizeRange, ref treePlacementState, randomGenerator);
                    treesToAddToMostRecentResourceUnit.LightCellIndexXY[treeIndex] = lightCellIndexXY;

                    UInt16 ageInYears = treeSizeRange.Age;
                    treesToAddToMostRecentResourceUnit.AgeInYears[treeIndex] = ageInYears;
                    
                    ++treesToAddToMostRecentResourceUnit.Count;
                    ++treeCount;

                    heightCellsInStand[heightCellIndex].BasalArea += 0.25F * MathF.PI * 0.0001F * dbhInCm * dbhInCm; // aggregate the basal area for each 10m pixel
                    if (previousSpeciesID != WorldFloraID.Unknown)
                    {
                        heightCellsInStand[heightCellIndex].IsSingleSpecies = true;
                    }

                    // resort list
                    if ((previousSpeciesID == WorldFloraID.Unknown) && (treeCount < 20 && (treesAdded % 2 == 0) || (treeCount < 100) && (treesAdded % 10 == 0) || (treesAdded % 30 == 0)))
                    {
                        heightCellsInStand.Sort(TreePopulator.CompareHeightInitCells);
                    }
                }

                if (treesToAddToMostRecentResourceUnit.Count > 0)
                {
                    Debug.Assert(previousResourceUnit != null);
                    lock (previousResourceUnit)
                    {
                        previousResourceUnit.Trees.AddTrees(treesToAddToMostRecentResourceUnit.AsSpan(), lightStampBeerLambertK);
                        treesToAddToMostRecentResourceUnit.Count = 0; // needed as this may not be the 
                    }
                }
            }
        }

        public void SetupTrees(Project projectFile, Landscape landscape, ParallelOptions parallelComputeOptions, ThreadLocal<RandomGenerator> randomGenerator)
        {
            string? initialHeightGridFile = projectFile.World.Initialization.HeightGrid.FileName;
            if (String.IsNullOrEmpty(initialHeightGridFile) == false)
            {
                string initialHeightGridPath = projectFile.GetFilePath(ProjectDirectory.Home, initialHeightGridFile);
                this.initialHeightGrid = new(initialHeightGridPath);

                string fitFormula = projectFile.World.Initialization.HeightGrid.FitFormula;
                this.heightGridResponse = new(fitFormula);
                this.heightGridResponse.Linearize(0.0F, 2.0F);
            }

            List<string> treeFiles = projectFile.World.Initialization.TreeFiles;
            for (int treeFileIndex = 0; treeFileIndex < treeFiles.Count; ++treeFileIndex)
            {
                string treeFilePath = projectFile.GetFilePath(ProjectDirectory.Init, treeFiles[treeFileIndex]);
                TreeReader treeFile = TreeReader.Create(treeFilePath);

                if (treeFile is IndividualTreeReader individualTreeReader)
                {
                    if (projectFile.World.Initialization.CloneIndividualTreesToEachResourceUnit)
                    {
                        // cloned individual trees: initialize each resource unit from a single, common tree file if resource units aren't specified
                        // in tree file
                        Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                        {
                            ResourceUnit resourceUnit = landscape.ResourceUnits[resourceUnitIndex];
                            this.ApplyTreeFileToResourceUnit(projectFile, landscape, resourceUnit, randomGenerator.Value!, treeFile, Constant.DefaultStandID);
                        });
                    }
                    else
                    {
                        // full listing of individual trees (LiDAR segmentation or similar): transfer trees as listed to resource unit
                        TreePopulator.PopulateResourceUnitsWithIndividualTrees(projectFile, landscape, individualTreeReader, parallelComputeOptions);
                    }
                }
                else if (treeFile is TreeSizeDistributionReaderCsv treeSizeReader)
                {
                    // appply a single tree size distribution to all resource units
                    string? treeSizeDistribution = projectFile.World.Initialization.TreeSizeDistribution;
                    if (treeSizeDistribution == null)
                    {
                        throw new NotSupportedException("");
                    }
                    if ((this.treeSizeDistribution == null) || (this.treeSizeDistribution.ProbabilityDensityFunction != treeSizeDistribution))
                    {
                        this.treeSizeDistribution = new(treeSizeDistribution);
                    }

                    Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                    {
                        ResourceUnit resourceUnit = landscape.ResourceUnits[resourceUnitIndex];
                        this.PopulateResourceUnitTreesFromSizeDistribution(projectFile, landscape, resourceUnit, treeSizeReader.TreeSizeDistribution, randomGenerator.Value!);
                    });
                }
                else if (treeFile is TreeFileByStandIDReaderCsv treeFileByStandIDReader)
                {
                    // different kinds of trees in each stand: load and apply a different tree file per stand
                    // The tree file can be either individual trees or a size distribution.
                    if ((landscape.StandRaster == null) || (landscape.StandRaster.IsSetup() == false))
                    {
                        throw new NotSupportedException("/project/model/world/initialization/treeFile '" + treeFilePath + "' lists other tree files by stand ID but no stand raster file (/project/model/world/initialization/standRasterFile) is indicated.");
                    }

                    Parallel.For(0, treeFileByStandIDReader.TreeFileNameByStandID.Count, parallelComputeOptions, (int standIndex) =>
                    {
                        (int standID, string standTreeFileName) = treeFileByStandIDReader.TreeFileNameByStandID[standIndex];
                        // for now, assume tree files are seldom repeated and there's little to no benefit in caching loaded files
                        // C++ code doesn't mask tree generation using the stand raster, so resource units which lie in multiple stands will get
                        // multiple tree fills, if specified, which don't follow the stand boundaries and result in overstocking.
                        IList<(ResourceUnit ResourceUnit, float OccupiedAreaInRU)> resourceUnitsInStand = landscape.StandRaster.GetResourceUnitAreaFractions(standID);
                        string standTreeFilePath = projectFile.GetFilePath(ProjectDirectory.Init, standTreeFileName);
                        TreeReader standTreeFile = TreeReader.Create(standTreeFilePath);
                        RandomGenerator random = randomGenerator.Value!;
                        for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitsInStand.Count; ++resourceUnitIndex)
                        {
                            ResourceUnit resourceUnit = resourceUnitsInStand[resourceUnitIndex].ResourceUnit;
                            this.ApplyTreeFileToResourceUnit(projectFile, landscape, resourceUnit, random, standTreeFile, standID);
                        }
                    });
                }
                else
                {
                    throw new NotImplementedException("/project/model/world/initialization/treeFile '" + treeFilePath + "' is not in a recognized format.");
                }
            }

            string? debugTreeExpressionString = projectFile.World.Debug.DebugTree;
            if (String.IsNullOrEmpty(debugTreeExpressionString) == false)
            {
                TreePopulator.EvaluateDebugTrees(landscape, debugTreeExpressionString);
            }
        }

        private class StandHeightInitCell
        {
            public float BasalArea { get; set; } // accumulated basal area
            public int HeightGridIndex { get; init; } // height grid location of the cell
            public bool IsSingleSpecies { get; set; } // pixel is dedicated to a single species
            public float MaxHeight { get; set; } // predefined maximum height at given pixel (if available from LIDAR or so)
            public ResourceUnit ResourceUnit { get; private init; } // pointer to the resource unit the pixel belongs to

            public StandHeightInitCell(ResourceUnit resourceUnit)
            {
                this.BasalArea = 0.0F;
                this.IsSingleSpecies = false;
                this.MaxHeight = -1.0F;
                this.ResourceUnit = resourceUnit;
            }
        }
    }
}
