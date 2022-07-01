using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing;

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
            this.initialHeightGrid = new GridRaster10m();
            this.treeSizeDistribution = null;
        }

        /// load a single init file. Calls loadPicusFile() or loadiLandFile()
        /// @param fileName file to load
        /// @param type init mode. allowed: "picus"/"single" or "iland"/"distribution"
        private void ApplyTreeFileToResourceUnit(Project projectFile, Landscape landscape, ResourceUnit ru, RandomGenerator randomGenerator, TreeReader treeFile, int standID)
        {
            if (treeFile.IndividualDbhInCM.Count > 0)
            {
                PopulateResourceUnitWithIndividualTrees(projectFile, landscape, ru, treeFile);
            }
            else if (treeFile.TreeSizeDistribution.Count > 0)
            {
                if (standID > Constant.DefaultStandID)
                {
                    // execute stand based initialization
                    PopulateStandTreesFromSizeDistribution(projectFile, landscape, treeFile.TreeSizeDistribution, randomGenerator, standID);
                }
                else
                {
                    // exeucte the initialization based on single resource units
                    PopulateResourceUnitTreesFromSizeDistribution(projectFile, landscape, ru, treeFile.TreeSizeDistribution, randomGenerator);
                    ru.Trees.RemoveDeadTrees(); // TODO: is this necessary?
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled tree file format in '" + treeFile.Path + "'. Expected either a list of individual trees or a tree size distribution. Does a tree file list used with a stand raster unexpectedly point to another tree file list?");
            }
        }

        private static int CompareHeightInitCells(HeightInitCell s1, HeightInitCell s2)
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

        private static int CompareInitPixelUnlocked(HeightInitCell s1, HeightInitCell s2)
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

        public static void EvaluateDebugTrees(Project projectFile, Landscape landscape)
        {
            // evaluate debugging
            string? isDebugTreeExpressionString = projectFile.World.Debug.DebugTree;
            if (string.IsNullOrEmpty(isDebugTreeExpressionString) == false)
            {
                if (string.Equals(isDebugTreeExpressionString, "debugstamp", StringComparison.OrdinalIgnoreCase))
                {
                    // check for trees which aren't correctly placed
                    AllTreesEnumerator treeIterator = new(landscape);
                    while (treeIterator.MoveNext())
                    {
                        if (landscape.LightGrid.Contains(treeIterator.CurrentTrees.LightCellIndexXY[treeIterator.CurrentTreeIndex]) == false)
                        {
                            throw new NotSupportedException("debugstamp: invalid tree position found.");
                        }
                    }
                    return;
                }

                TreeWrapper treeWrapper = new(null);
                Expression isDebugTreeExpression = new(isDebugTreeExpressionString, treeWrapper); // load expression dbg_str and enable external model variables
                AllTreesEnumerator allTreeEnumerator = new(landscape);
                while (allTreeEnumerator.MoveNext())
                {
                    // TODO: why is debug expression evaluated for all trees rather than just trees marked for debugging?
                    treeWrapper.Trees = allTreeEnumerator.CurrentTrees;
                    treeWrapper.TreeIndex = allTreeEnumerator.CurrentTreeIndex;
                    double result = isDebugTreeExpression.Execute();
                    if (result != 0.0)
                    {
                        allTreeEnumerator.CurrentTrees.SetDebugging(allTreeEnumerator.CurrentTreeIndex);
                    }
                }
            }
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
        //    double area_factor = standGrid.GetArea(standID) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

        //    // parse the content of the init-file
        //    // species
        //    CsvFile init = new CsvFile();
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
        //        int pxcount = (int)Math.Round(Double.Parse(init[countIndex, row)) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
        //        TreeSpecies species = set.GetSpecies(init[speciesIndex, row));
        //        if (species == null)
        //        {
        //            throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init[speciesIndex, row)));
        //        }
        //        float height = heightIndex == -1 ? Constant.Sapling.MinimumHeight : Single.Parse(init[heightIndex, row));
        //        int age = ageIndex == -1 ? 1 : Int32.Parse(init[ageIndex, row));

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

        private void PopulateResourceUnitTreesFromSizeDistribution(Project projectFile, Landscape landscape, ResourceUnit ru, List<TreeSizeRange> treeSizeDistribution, RandomGenerator randomGenerator)
        {
            Debug.Assert(this.treeSizeDistribution != null);

            //List<MutableTuple<int, float>> resourceUnitBasalAreaByHeightCellIndex = new List<MutableTuple<int, float>>();
            //for (int heightCellIndex = 0; heightCellIndex < Constant.HeightSizePerRU * Constant.HeightSizePerRU; ++heightCellIndex)
            //{
            //    resourceUnitBasalAreaByHeightCellIndex.Add(new MutableTuple<int, float>(heightCellIndex, 0.0F));
            //}

            // a multimap holds a list for all trees.
            // key is the index of a 10x10m pixel within the resource unit

            Dictionary<int, (Trees Trees, List<int> TreeIndices)> treeIndexByHeightCellIndex = new();
            int totalTreeCount = 0;
            foreach (TreeSizeRange sizeRange in treeSizeDistribution)
            {
                float randFraction = sizeRange.Density;
                for (int index = 0; index < sizeRange.Count; ++index)
                {
                    // create trees
                    TreeSpecies treeSpecies = ru.Trees.TreeSpeciesSet[sizeRange.TreeSpecies];
                    int treeIndex = ru.Trees.AddTree(landscape, treeSpecies.ID);
                    Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[treeSpecies.ID];
                    treesOfSpecies.Dbh[treeIndex] = randomGenerator.GetRandomFloat(sizeRange.DbhFrom, sizeRange.DbhTo);
                    treesOfSpecies.SetHeight(treeIndex, 0.001F * treesOfSpecies.Dbh[treeIndex] * (float)sizeRange.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    if (sizeRange.Age < 1)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        treesOfSpecies.SetAge(treeIndex, sizeRange.Age, treesOfSpecies.Height[treeIndex]);
                    }
                    treesOfSpecies.Setup(projectFile, treeIndex);
                    ++totalTreeCount;

                    // calculate random value. "density" is from 1..-1.
                    float randomValue = this.treeSizeDistribution.GetRandomValue(randomGenerator);
                    if (sizeRange.Density < 0.0F)
                    {
                        randomValue = 1.0F - randomValue;
                    }
                    randomValue = randomValue * randFraction + randomGenerator.GetRandomProbability() * (1.0F - randFraction);

                    // key: rank of target pixel
                    // item1: index of target pixel
                    // item2: sum of target pixel
                    int heightCellIndex = Maths.Limit((int)(Constant.HeightCellsPerRUWidth * Constant.HeightCellsPerRUWidth * randomValue), 0, Constant.HeightCellsPerRUWidth * Constant.HeightCellsPerRUWidth - 1); // get from random number generator
                    // int heightCellIndex = landscape.HeightGrid.IndexOf(landscape.HeightGrid.GetCellIndex(treesOfSpecies.GetCellCenterPoint(treeIndex)));
                    if (treeIndexByHeightCellIndex.TryGetValue(heightCellIndex, out (Trees Trees, List<int> TreeIndices) treesInCell) == false)
                    {
                        treesInCell = new(treesOfSpecies, new List<int>());
                        treeIndexByHeightCellIndex.Add(heightCellIndex, treesInCell);
                    }
                    treesInCell.TreeIndices.Add(treesOfSpecies.Tag[treeIndex]); // store tree in map

                    //MutableTuple<int, float> resourceUnitBasalArea = resourceUnitBasalAreaByHeightCellIndex[randomHeightCellIndex];
                    //resourceUnitBasalArea.Item2 += treesOfSpecies.GetBasalArea(treeIndex); // aggregate the basal area for each 10m pixel
                    //if ((totalTreeCount < 20 && index % 2 == 0) ||
                    //    (totalTreeCount < 100 && index % 10 == 0) ||
                    //    (index % 30 == 0))
                    //{
                    //    resourceUnitBasalAreaByHeightCellIndex.Sort(StandReader.SortPairLessThan);
                    //}
                }
                //resourceUnitBasalAreaByHeightCellIndex.Sort(StandReader.SortPairLessThan);
            }

            for (int heightCellIndex = 0; heightCellIndex < Constant.HeightCellsPerRUWidth * Constant.HeightCellsPerRUWidth; ++heightCellIndex)
            {
                (Trees Trees, List<int> TreeIndices) treesInCell = treeIndexByHeightCellIndex[heightCellIndex];
                PointF heightCellProjectCentroid = ru.ProjectExtent.Location.Add(new PointF(Constant.HeightCellSizeInM * (heightCellIndex / Constant.HeightCellSizeInM + 0.5F), Constant.HeightCellSizeInM * (heightCellIndex % Constant.HeightCellSizeInM + 0.5F)));
                if (landscape.HeightGrid[heightCellProjectCentroid].IsOnLandscape() == false)
                {
                    throw new NotSupportedException("Resource unit contains trees which are outside of landscpe.");
                    // no trees on that pixel: let trees die
                    //Trees trees = treesInCell.Item1;
                    //foreach (int treeIndex in treesInCell.Item2)
                    //{
                    //    trees.Die(model, treeIndex);
                    //}
                    //continue;
                }

                int bits = 0;
                int index = -1;
                PointF ruGridOriginInProjectCoordinates = ru.ProjectExtent.Location;
                Point ruLightIndexXY = landscape.LightGrid.GetCellXYIndex(ruGridOriginInProjectCoordinates);
                foreach (int treeIndex in treesInCell.TreeIndices)
                {
                    if (treesInCell.TreeIndices.Count > 18)
                    {
                        index = (index + 1) % 25;
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
                            index = Maths.Limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Maths.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0 && projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                        {
                            Trace.TraceInformation("InitializeResourceUnit(): found no free bit.");
                        }
                        Maths.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = ru.ResourceUnitGridIndex % Constant.LightCellSizeInM != 0 ? TreePopulator.EvenHeightCellPositions[index] : TreePopulator.UnevenHeightCellPositions[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    Point lightCellIndex = new(ruLightIndexXY.X + Constant.LightCellsPerHeightCellWidth * (heightCellIndex / Constant.HeightCellSizeInM) + pos / Constant.LightCellsPerHeightCellWidth,
                                               ruLightIndexXY.Y + Constant.LightCellsPerHeightCellWidth * (heightCellIndex % Constant.HeightCellSizeInM) + pos % Constant.LightCellsPerHeightCellWidth);
                    // if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                    // {
                    //     Trace.TraceInformation(tree_no++ + "to" + index);
                    // }
                    treesInCell.Trees.LightCellIndexXY[treeIndex] = lightCellIndex;
                }
            }
        }

        /** load a list of trees (given by content) to a resource unit. Param fileName is just for error reporting.
            returns the number of loaded trees.
          */
        private static void PopulateResourceUnitWithIndividualTrees(Project projectFile, Landscape landscape, ResourceUnit ru, TreeReader treeFile)
        {
            Point ruPositionInResourceUnitGrid = landscape.ResourceUnitGrid.GetCellXYIndex(ru.ResourceUnitGridIndex);
            PointF translationToPlaceTreeOnResourceUnit = new(Constant.ResourceUnitSizeInM * ruPositionInResourceUnitGrid.X, Constant.ResourceUnitSizeInM * ruPositionInResourceUnitGrid.Y);

            TreeSpeciesSet speciesSet = ru.Trees.TreeSpeciesSet; // of default RU
            for (int treeIndexInFile = 0; treeIndexInFile < treeFile.IndividualDbhInCM.Count; ++treeIndexInFile)
            {
                //if (dbh<5.)
                //    continue;
                float treeProjectX = treeFile.IndividualGisX[treeIndexInFile] - landscape.ProjectOriginInGisCoordinates.X + translationToPlaceTreeOnResourceUnit.X;
                float treeProjectY = treeFile.IndividualGisY[treeIndexInFile] - landscape.ProjectOriginInGisCoordinates.Y + translationToPlaceTreeOnResourceUnit.Y;
                if (landscape.HeightGrid[treeProjectX, treeProjectY].IsOnLandscape() == false)
                {
                    throw new NotSupportedException("Individual tree " + treeFile.IndividualTag[treeIndexInFile] + " (line " + (treeIndexInFile + 1) + ") is not located in project simulation area after being displaced to resource unit " + ru.ID + ". Tree coordinates are (" + treeProjectX + ", " + treeProjectY + ")");
                }

                string speciesID = treeFile.IndividualSpeciesID[treeIndexInFile];
                TreeSpecies species = speciesSet[speciesID];

                int treeIndexInResourceUnitTreeList = ru.Trees.AddTree(landscape, speciesID);
                Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[species.ID];
                treesOfSpecies.SetLightCellIndex(treeIndexInResourceUnitTreeList, new PointF(treeProjectX, treeProjectY));

                float heightInM = treeFile.IndividualHeightInM[treeIndexInFile];
                treesOfSpecies.SetAge(treeIndexInResourceUnitTreeList, treeFile.IndividualAge[treeIndexInFile], heightInM);
                treesOfSpecies.Dbh[treeIndexInResourceUnitTreeList] = treeFile.IndividualDbhInCM[treeIndexInFile];
                treesOfSpecies.SetHeight(treeIndexInResourceUnitTreeList, heightInM);
                treesOfSpecies.StandID[treeIndexInResourceUnitTreeList] = treeFile.IndividualStandID[treeIndexInFile];
                treesOfSpecies.Tag[treeIndexInResourceUnitTreeList] = treeFile.IndividualTag[treeIndexInFile];

                treesOfSpecies.Setup(projectFile, treeIndexInResourceUnitTreeList);
            }
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
            List<int> heightGridCellIndicesInStand = standRaster.GetGridIndices(standID);
            if (heightGridCellIndicesInStand.Count == 0)
            {
                if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                {
                    Trace.TraceInformation("Stand " + standID + " not in project area. No initialization performed.");
                }
                return;
            }
            // a multiHash holds a list for all trees.
            // key is the location of the 10x10m pixel
            Dictionary<int, (Trees Trees, List<int> TreeIndices)> treeIndicesByHeightCellIndex = new();
            List<HeightInitCell> heightCells = new(heightGridCellIndicesInStand.Count); // working list of all 10m pixels

            foreach (int heightCellIndex in heightGridCellIndicesInStand)
            {
                ResourceUnit ru = landscape.GetResourceUnit(standRaster.Grid.GetCellProjectCentroid(heightCellIndex));
                HeightInitCell heightCell = new(ru)
                {
                    GridCellIndex = heightCellIndex // index in the 10m grid
                };
                if (initialHeightGrid.IsSetup())
                {
                    heightCell.MaxHeight = initialHeightGrid.Grid[heightCell.GridCellIndex];
                }
                heightCells.Add(heightCell);
            }
            float standAreaInResourceUnits = standRaster.GetAreaInSquareMeters(standID) / Constant.ResourceUnitAreaInM2;

            if (initialHeightGrid.IsSetup() && heightGridResponse == null)
            {
                throw new NotSupportedException("Attempt to initialize from height grid but without response function.");
            }

            Debug.Assert(this.treeSizeDistribution != null);
            Debug.Assert(heightGridResponse != null);
            int key = 0;
            string? previousSpecies = null;
            int treeCount = 0;
            int totalTries = 0;
            int totalMisses = 0;
            foreach (TreeSizeRange treeSizeRange in treeSizeDistribution)
            {
                if (treeSizeRange.Density > 1.0)
                {
                    // special case with single-species-area
                    if (treeCount == 0)
                    {
                        // randomize the pixels
                        for (int heightIndex = 0; heightIndex < heightCells.Count; ++heightIndex)
                        {
                            heightCells[heightIndex].BasalArea = randomGenerator.GetRandomProbability();
                        }
                        heightCells.Sort(CompareHeightInitCells);

                        for (int heightIndex = 0; heightIndex < heightCells.Count; ++heightIndex)
                        {
                            heightCells[heightIndex].BasalArea = 0.0F;
                        }
                    }

                    if (string.Equals(treeSizeRange.TreeSpecies, previousSpecies, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        previousSpecies = treeSizeRange.TreeSpecies;
                        heightCells.Sort(CompareInitPixelUnlocked);
                    }
                }
                else
                {
                    heightCells.Sort(CompareHeightInitCells);
                    previousSpecies = null;
                }

                double rand_fraction = treeSizeRange.Density;
                int count = (int)(treeSizeRange.Count * standAreaInResourceUnits + 0.5); // round
                double init_max_height = treeSizeRange.DbhTo / 100.0 * treeSizeRange.HeightDiameterRatio;
                int maxHeightFittingAttempts = projectFile.World.Initialization.HeightGrid.MaxTries;
                for (int i = 0; i < count; ++i)
                {
                    bool found = false;
                    int tries = maxHeightFittingAttempts;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        double randomValue;
                        if (treeSizeRange.Density <= 1.0)
                        {
                            randomValue = this.treeSizeDistribution.GetRandomValue(randomGenerator);
                            if (treeSizeRange.Density < 0)
                            {
                                randomValue = 1.0 - randomValue;
                            }
                            randomValue = randomValue * rand_fraction + randomGenerator.GetRandomProbability() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            randomValue = randomGenerator.GetRandomProbability() * Math.Min(treeSizeRange.Density / 100.0, 1.0);
                        }
                        ++totalTries;

                        // key: rank of target pixel
                        key = Maths.Limit((int)(heightCells.Count * randomValue), 0, heightCells.Count - 1); // get from random number generator

                        if (initialHeightGrid.IsSetup())
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            float p_value = heightCells[key].MaxHeight > 0.0F ? (float)heightGridResponse.Evaluate(init_max_height / heightCells[key].MaxHeight) : 0.0F;
                            if (randomGenerator.GetRandomProbability() < p_value)
                            {
                                found = true;
                            }
                        }
                        else
                        {
                            found = true;
                        }
                        if (previousSpecies != null && heightCells[key].IsSingleSpecies)
                        {
                            found = false;
                        }
                    }
                    if (tries < 0)
                    {
                        ++totalMisses;
                    }

                    // create a tree
                    ResourceUnit ru = heightCells[key].ResourceUnit;
                    Trees trees = ru.Trees.TreesBySpeciesID[treeSizeRange.TreeSpecies];
                    TreeSpecies treeSpecies = ru.Trees.TreeSpeciesSet[treeSizeRange.TreeSpecies];
                    int treeIndex = ru.Trees.AddTree(landscape, treeSpecies.ID);
                    trees.Dbh[treeIndex] = (float)randomGenerator.GetRandomFloat(treeSizeRange.DbhFrom, treeSizeRange.DbhTo);
                    trees.SetHeight(treeIndex, trees.Dbh[treeIndex] / 100.0F * treeSizeRange.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    if (treeSizeRange.Age <= 0)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        trees.SetAge(treeIndex, treeSizeRange.Age, trees.Height[treeIndex]);
                    }
                    trees.Setup(projectFile, treeIndex);
                    ++treeCount;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    if (treeIndicesByHeightCellIndex.TryGetValue(heightCells[key].GridCellIndex, out (Trees Trees, List<int> TreeIndices) treesInCell) == false)
                    {
                        treesInCell = new(trees, new List<int>());
                        treeIndicesByHeightCellIndex.Add(heightCells[key].GridCellIndex, treesInCell);
                    }
                    treesInCell.TreeIndices.Add(treeIndex);

                    heightCells[key].BasalArea += trees.GetBasalArea(treeIndex); // aggregate the basal area for each 10m pixel
                    if (previousSpecies != null)
                    {
                        heightCells[key].IsSingleSpecies = true;
                    }

                    // resort list
                    if (previousSpecies == null && (treeCount < 20 && i % 2 == 0 || treeCount < 100 && i % 10 == 0 || i % 30 == 0))
                    {
                        heightCells.Sort(CompareHeightInitCells);
                    }
                }
            }

            foreach (HeightInitCell heightCell in heightCells)
            {
                (Trees Trees, List<int> TreeIndices) treesInCell = treeIndicesByHeightCellIndex[heightCell.GridCellIndex];
                int index = -1;
                foreach (int treeIndex in treesInCell.TreeIndices)
                {
                    if (treesInCell.TreeIndices.Count > 18)
                    {
                        index = (index + 1) % 25;
                    }
                    else
                    {
                        int bits = 0;
                        int stop = 1000;
                        index = 0;
                        do
                        {
                            // search a random position
                            float random = randomGenerator.GetRandomProbability();
                            index = Maths.Limit((int)(25 * random * random), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Maths.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0 && projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                        {
                            Trace.TraceInformation("InitializeStand(): found no free bit.");
                        }
                        Maths.SetBit(ref bits, index, true); // mark position as used
                    }

                    // get position from fixed lists (one for even, one for uneven resource units)
                    int positionInResourceUnit = heightCell.ResourceUnit.ResourceUnitGridIndex % Constant.LightCellSizeInM != 0 ? EvenHeightCellPositions[index] : UnevenHeightCellPositions[index];
                    Point heightCellIndexXY = landscape.HeightGrid.GetCellXYIndex(heightCell.GridCellIndex);
                    Point lightCellIndex = new(heightCellIndexXY.X * Constant.LightCellsPerHeightCellWidth + positionInResourceUnit / Constant.LightCellsPerHeightCellWidth, // convert to LIF index
                                               heightCellIndexXY.Y * Constant.LightCellsPerHeightCellWidth + positionInResourceUnit % Constant.LightCellsPerHeightCellWidth);
                    if (landscape.LightGrid.Contains(lightCellIndex) == false)
                    {
                        throw new NotSupportedException("Tree is positioned outside of the light grid.");
                    }

                    Trees trees = treesInCell.Trees;
                    trees.LightCellIndexXY[treeIndex] = lightCellIndex;
                }
            }
        }

        public void SetupTrees(Project projectFile, Landscape landscape, RandomGenerator randomGenerator)
        {
            string? initialHeightGridFile = projectFile.World.Initialization.HeightGrid.FileName;
            if (string.IsNullOrEmpty(initialHeightGridFile) == false)
            {
                string initialHeightGridPath = projectFile.GetFilePath(ProjectDirectory.Home, initialHeightGridFile);
                initialHeightGrid = new GridRaster10m(initialHeightGridPath);

                string fitFormula = projectFile.World.Initialization.HeightGrid.FitFormula;
                heightGridResponse = new Expression(fitFormula);
                heightGridResponse.Linearize(0.0, 2.0);
            }

            TreeInitializationMethod resourceUnitInitialization = projectFile.World.Initialization.Trees;
            string treeFilePath = projectFile.GetFilePath(ProjectDirectory.Init, projectFile.World.Initialization.TreeFile);
            TreeReader treeFile = new(treeFilePath);

            if (treeFile.IndividualDbhInCM.Count > 0)
            {
                // cloned individual trees: initialize each resource unit from a single, common tree file
                foreach (ResourceUnit ru in landscape.ResourceUnits)
                {
                    ApplyTreeFileToResourceUnit(projectFile, landscape, ru, randomGenerator, treeFile, Constant.DefaultStandID);
                }
            }
            else if (treeFile.TreeSizeDistribution.Count > 0)
            {
                // appply a single tree size distribution to all resource units
                string? treeSizeDistribution = projectFile.World.Initialization.TreeSizeDistribution;
                if (treeSizeDistribution == null)
                {
                    throw new NotSupportedException("");
                }
                if (this.treeSizeDistribution == null || this.treeSizeDistribution.ProbabilityDensityFunction != treeSizeDistribution)
                {
                    this.treeSizeDistribution = new(treeSizeDistribution);
                }

                foreach (ResourceUnit ru in landscape.ResourceUnits)
                {
                    PopulateResourceUnitTreesFromSizeDistribution(projectFile, landscape, ru, treeFile.TreeSizeDistribution, randomGenerator);
                }
            }
            else if (treeFile.TreeFileNameByStandID.Count > 0)
            {
                // different kinds of trees in each stand: load and apply a different tree file per stand
                // The tree file can be either individual trees or a size distribution.
                if (landscape.StandRaster == null || landscape.StandRaster.IsSetup() == false)
                {
                    throw new NotSupportedException("model.world.initialization.trees is 'standRaster' but no stand raster (model.world.initialization.standRasterFile) is present.");
                }

                string treesByStandIDFilePath = projectFile.GetFilePath(ProjectDirectory.Init, projectFile.World.Initialization.TreeFile);
                TreeReader treesByStandID = new(treesByStandIDFilePath);
                if (treesByStandID.TreeFileNameByStandID.Count == 0)
                {
                    throw new NotSupportedException("The tree file '" + treesByStandIDFilePath + "' provides an empty list of stand IDs and additional tree files.");
                }
                foreach ((int standID, string standTreeFileName) in treesByStandID.TreeFileNameByStandID)
                {
                    // TODO: load stand's tree file only once
                    IList<(ResourceUnit RU, float OccupiedAreaInRU)> resourceUnitsInStand = landscape.StandRaster.GetResourceUnitAreaFractions(standID);
                    string standTreeFilePath = projectFile.GetFilePath(ProjectDirectory.Init, standTreeFileName);
                    TreeReader standTreeFile = new(standTreeFilePath);
                    for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitsInStand.Count; ++resourceUnitIndex)
                    {
                        ResourceUnit ru = resourceUnitsInStand[resourceUnitIndex].RU;
                        ApplyTreeFileToResourceUnit(projectFile, landscape, ru, randomGenerator, standTreeFile, standID);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("model.world.initialization.trees " + resourceUnitInitialization + " is not currently supported.");
            }

            EvaluateDebugTrees(projectFile, landscape);
        }

        private class HeightInitCell
        {
            public float BasalArea { get; set; } // accumulated basal area
            public int GridCellIndex { get; init; } // location of the pixel
            public float MaxHeight { get; set; } // predefined maximum height at given pixel (if available from LIDAR or so)
            public ResourceUnit ResourceUnit { get; private init; } // pointer to the resource unit the pixel belongs to
            public bool IsSingleSpecies { get; set; } // pixel is dedicated to a single species

            public HeightInitCell(ResourceUnit ru)
            {
                BasalArea = 0.0F;
                IsSingleSpecies = false;
                MaxHeight = -1.0F;
                ResourceUnit = ru;
            }
        };
    }
}
