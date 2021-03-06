﻿using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.Input
{
    /** @class StandLoader
        loads (initializes) trees for a "stand" from various sources.
        StandLoader initializes trees on the landscape. It reads (usually) from text files, creates the
        trees and distributes the trees on the landscape (on the ResourceUnit or on a stand defined by a grid).

        See http://iland-model.org/initialize+trees
        */
    internal class StandReader
    {
        // provide a mapping between "Picus"-style and "iLand"-style species Ids
        // TODO: if needed, expand species support
        private static readonly ReadOnlyCollection<int> PicusSpeciesIDs = new List<int>() { 0, 1, 17 }.AsReadOnly();
        private static readonly ReadOnlyCollection<string> iLandSpeciesIDs = new List<string>() { "piab", "piab", "fasy" }.AsReadOnly();

        // evenlist: tentative order of pixel-indices (within a 5x5 grid) used as tree positions.
        // e.g. 12 = centerpixel, 0: upper left corner, ...
        private static readonly int[] EvenList = new int[] { 12, 6, 18, 16, 8, 22, 2, 10, 14, 0, 24, 20, 4, 1, 13, 15, 19, 21, 3, 7, 11, 17, 23, 5, 9 };
        private static readonly int[] UnevenList = new int[] { 11, 13, 7, 17, 1, 19, 5, 21, 9, 23, 3, 15, 6, 18, 2, 10, 4, 24, 12, 0, 8, 14, 20, 22 };

        private RandomCustomPdf? mProbabilityDistribution;
        private readonly Dictionary<int, List<StandInitializationFileRow>> mTreeRowsByStandID;
        private readonly List<StandInitializationFileRow> mTreeRowsForDefaultStand;
        private Expression? mHeightGridResponse; // response function to calculate fitting of pixels with pre-determined height
        private int mHeightGridTries; // maximum number of tries to land at pixel with fitting height

        /// set a constraining height grid (10m resolution)
        private MapGrid initialHeightGrid; // grid with tree heights

        public StandReader()
        {
            this.initialHeightGrid = new MapGrid();
            this.mHeightGridResponse = null;
            this.mProbabilityDistribution = null;
            this.mTreeRowsByStandID = new Dictionary<int, List<StandInitializationFileRow>>();
            this.mTreeRowsForDefaultStand = new List<StandInitializationFileRow>();
        }

        //public void CopyTrees()
        //{
        //    // we assume that all stands are equal, so wie simply COPY the trees and modify them afterwards
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
        //    //if (GlobalSettings.Instance.LogInfo())
        //    //{
        //    //    Debug.WriteLine(Tree.TreesCreated + " trees loaded / copied.");
        //    //}
        //}

        /** main routine of the stand setup.
          */
        public void Setup(Project projectFile, Landscape landscape, RandomGenerator randomGenerator)
        {
            this.mHeightGridTries = projectFile.World.Initialization.HeightGrid.MaxTries;
            string? initialHeightGridFile = projectFile.World.Initialization.HeightGrid.FileName;
            if (String.IsNullOrEmpty(initialHeightGridFile) == false)
            {
                string initialHeightGridPath = projectFile.GetFilePath(ProjectDirectory.Home, initialHeightGridFile);
                this.initialHeightGrid = new MapGrid(landscape, initialHeightGridPath);
                if (this.initialHeightGrid.IsValid() == false)
                {
                    throw new NotSupportedException(String.Format("Error when loading grid with tree heights for stand initalization: file {0} not found or not valid.", initialHeightGridPath));
                }

                string fitFormula = projectFile.World.Initialization.HeightGrid.FitFormula;
                this.mHeightGridResponse = new Expression(fitFormula);
                this.mHeightGridResponse.Linearize(0.0, 2.0);
            }

            //Tree.ResetStatistics();

            // one global init-file for the whole area:
            ResourceUnitTreeInitializationMethod ruInitializationMethod = projectFile.World.Initialization.TreeInitializationMethod;
            string? treeFileType = projectFile.World.Initialization.TreeFileFormat;
            string? treeFileName = projectFile.World.Initialization.TreeFile;
            if (ruInitializationMethod == ResourceUnitTreeInitializationMethod.Single)
            {
                // useful for 1ha simulations only...
                if (landscape.ResourceUnits.Count > 1)
                {
                    throw new NotSupportedException("'mode' is 'single' but more than one resource unit is simulated (consider using another mode).");
                }

                this.LoadTreeFile(projectFile, landscape, landscape.ResourceUnits[0], randomGenerator, treeFileName, treeFileType, 0); // this is the first resource unit
                StandReader.EvaluateDebugTrees(projectFile, landscape);
            }
            // cloned resource units: initialize each resource unit from a single, common tree file
            else if (ruInitializationMethod == ResourceUnitTreeInitializationMethod.Unit)
            {
                foreach (ResourceUnit ru in landscape.ResourceUnits)
                {
                    // set environment
                    landscape.Environment.SetPosition(projectFile, ru.BoundingBox.Center());
                    if (String.IsNullOrEmpty(treeFileName))
                    {
                        continue;
                    }
                    // TODO: why reload trees each time when all RUs share the same init file?
                    this.LoadTreeFile(projectFile, landscape, ru, randomGenerator, treeFileName, treeFileType, 0);
                }
                StandReader.EvaluateDebugTrees(projectFile, landscape);
                return;
            }
            // map-modus: load a different tree init file for each stand "polygon" in the standgrid
            else if (ruInitializationMethod == ResourceUnitTreeInitializationMethod.Unit)
            {
                if (landscape.StandGrid == null || landscape.StandGrid.IsValid() == false)
                {
                    throw new NotSupportedException("model.initialization.mode is 'map' but there is no valid stand grid defined (model.world.standGrid)");
                }
                string mapFileName = projectFile.GetFilePath(ProjectDirectory.Home, projectFile.World.Initialization.MapFileName);

                CsvFile mapFile = new(mapFileName);
                if (mapFile.RowCount == 0)
                {
                    throw new NotSupportedException(String.Format("Map file {0} is empty or missing.", mapFileName));
                }
                int idColumn = mapFile.GetColumnIndex("id");
                int fileNameColumn = mapFile.GetColumnIndex("filename");
                if (idColumn < 0 || fileNameColumn < 0)
                {
                    throw new NotSupportedException(String.Format("Map file {0} does not contain the mandatory columns 'id' and 'filename'.", mapFileName));
                }

                for (int row = 0; row < mapFile.RowCount; ++row)
                {
                    int key = Int32.Parse(mapFile.GetValue(idColumn, row));
                    if (key > 0)
                    {
                        treeFileName = mapFile.GetValue(fileNameColumn, row);
                        if (String.IsNullOrEmpty(treeFileName) == false)
                        {
                            if (landscape.ResourceUnits.Count != 1)
                            {
                                throw new NullReferenceException("Expected only a default resource unit on landscape.");
                            }
                            this.LoadTreeFile(projectFile, landscape, landscape.ResourceUnits[0], randomGenerator, treeFileName, treeFileType, key);
                        }
                    }
                }

                StandReader.EvaluateDebugTrees(projectFile, landscape);
            }
            // standgrid mode: load one large init file
            else if (ruInitializationMethod == ResourceUnitTreeInitializationMethod.StandGrid)
            {
                throw new NotImplementedException("Tree initialization mode " + ruInitializationMethod + " is not currently supported.");
            }
            else
            {
                treeFileName = projectFile.GetFilePath(ProjectDirectory.Init, treeFileName);
                if (!File.Exists(treeFileName))
                {
                    throw new FileNotFoundException(String.Format("File '{0}' does not exist.", treeFileName));
                }

                // this processes the init file (also does the checking) and
                // stores in a Dictionary datastrucutre
                if (landscape.ResourceUnits.Count != 1)
                {
                    throw new NullReferenceException("Expected only a default resource unit on landscape.");
                }
                this.ParseInitFile(treeFileName, landscape.ResourceUnits[0]);

                // setup the random distribution
                string? probabilityDistributionFunction = projectFile.World.Initialization.RandomFunction;
                if (probabilityDistributionFunction == null)
                {
                    throw new NotSupportedException("");
                }
                if (this.mProbabilityDistribution == null || (this.mProbabilityDistribution.DensityFunction != probabilityDistributionFunction))
                {
                    this.mProbabilityDistribution = new RandomCustomPdf(probabilityDistributionFunction);
                }

                if (this.mTreeRowsByStandID.Count == 0)
                {
                    throw new NotSupportedException("'mode' is 'standgrid' but the init file is empty.");
                }
                foreach (KeyValuePair<int, List<StandInitializationFileRow>> it in this.mTreeRowsByStandID)
                {
                    this.InitializeStand(projectFile, landscape, randomGenerator, it.Key);
                }

                StandReader.EvaluateDebugTrees(projectFile, landscape);
            }
        }

        public void SetupSaplings(Model model)
        {
            // nothing to do if no saplings are specified
            string? saplingFileName = model.Project.World.Initialization.SaplingFile;
            if (String.IsNullOrEmpty(saplingFileName))
            {
                return;
            }
            // load a file with saplings per polygon
            string saplingFilePath = model.Project.GetFilePath(ProjectDirectory.Init, saplingFileName);
            CsvFile saplingFile = new(saplingFilePath);
            int standIDindex = saplingFile.GetColumnIndex("stand_id");
            if (standIDindex == -1)
            {
                throw new NotSupportedException("The sapling init file contains no 'stand_id' column (required in 'standgrid' mode).");
            }

            ResourceUnitTreeInitializationMethod treeInitializationMethod = model.Project.World.Initialization.TreeInitializationMethod;
            if (treeInitializationMethod == ResourceUnitTreeInitializationMethod.StandGrid)
            {
                int previousStandID = -99999;
                int standStartRow = -1;
                for (int row = 0; row < saplingFile.RowCount; ++row)
                {
                    int standID = Int32.Parse(saplingFile.GetValue(standIDindex, row));
                    if (standID != previousStandID)
                    {
                        if (previousStandID >= 0)
                        {
                            // process stand
                            int standEndRow = row - 1; // up to the last
                            this.LoadSaplingsLif(model, previousStandID, saplingFile, standStartRow, standEndRow);
                        }
                        standStartRow = row; // mark beginning of new stand
                        previousStandID = standID;
                    }
                }
                if (previousStandID >= 0)
                {
                    this.LoadSaplingsLif(model, previousStandID, saplingFile, standStartRow, saplingFile.RowCount - 1); // the last stand
                }
            }
            else if (saplingFile.RowCount != 1)
            {
                throw new NotSupportedException("Sapling initialization not supported for tree initialization method " + treeInitializationMethod + ".");
            }
            // nothing to do: sapling file is empty
        }

        public static void EvaluateDebugTrees(Project projectFile, Landscape landscape)
        {
            // evaluate debugging
            string? isDebugTreeExpressionString = projectFile.World.Debug.DebugTree;
            if (String.IsNullOrEmpty(isDebugTreeExpressionString) == false)
            {
                if (String.Equals(isDebugTreeExpressionString, "debugstamp", StringComparison.OrdinalIgnoreCase))
                {
                    // check for trees which aren't correctly placed
                    AllTreesEnumerator treeIterator = new(landscape);
                    while (treeIterator.MoveNext())
                    {
                        if (landscape.LightGrid.Contains(treeIterator.CurrentTrees.LightCellPosition[treeIterator.CurrentTreeIndex]) == false)
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

        /// load a single init file. Calls loadPicusFile() or loadiLandFile()
        /// @param fileName file to load
        /// @param type init mode. allowed: "picus"/"single" or "iland"/"distribution"
        public int LoadTreeFile(Project projectFile, Landscape landscape, ResourceUnit ru, RandomGenerator randomGenerator, string? treeFileName, string? fileFormat, int standID)
        {
            string treeFilePath = projectFile.GetFilePath(ProjectDirectory.Init, treeFileName);
            if (!File.Exists(treeFilePath))
            {
                throw new FileNotFoundException(String.Format("File '{0}' does not exist!", treeFilePath));
            }

            if (String.Equals(fileFormat, "csvOrPicus", StringComparison.Ordinal))
            {
                if (standID > 0)
                {
                    throw new XmlException(String.Format("Initialization type '{0}' currently not supported for stand initilization mode!" + fileFormat));
                }
                return StandReader.LoadCsvOrPicusFile(projectFile, landscape, ru, treeFilePath);
            }
            if (String.Equals(fileFormat, "iLand", StringComparison.Ordinal) || String.Equals(fileFormat, "distribution", StringComparison.Ordinal))
            {
                return this.LoadDistributionList(projectFile, landscape, ru, randomGenerator, treeFilePath, standID);
            }
            throw new XmlException("Unknown initialization type '" + fileFormat + "'. Is a /project/model/initialization/type element present in the project file?");
        }

        /** load a list of trees (given by content) to a resource unit. Param fileName is just for error reporting.
            returns the number of loaded trees.
          */
        private static int LoadCsvOrPicusFile(Project projectFile, Landscape landscape, ResourceUnit ru, string treeFileName)
        {
            string treeFileContent = File.ReadAllText(treeFileName);
            if (String.IsNullOrEmpty(treeFileContent))
            {
                throw new FileLoadException("File '" + treeFileName + "' is empty.");
            }

            // cut out the <trees> </trees> part if present
            int treesElementStart = treeFileContent.IndexOf("<trees>", StringComparison.Ordinal);
            if (treesElementStart > -1)
            {
                int treesElementStop = treeFileContent.IndexOf("</trees>", StringComparison.Ordinal);
                treeFileContent = treeFileContent[(treesElementStart + "<trees>".Length)..(treesElementStop - 1)];
            }

            CsvFile picusTreeFile = new();
            picusTreeFile.LoadFromString(treeFileContent);

            int standIDcolumn = picusTreeFile.GetColumnIndex("standID");
            int tagColumn = picusTreeFile.GetColumnIndex("id");
            int xColumn = picusTreeFile.GetColumnIndex("x");
            int yColumn = picusTreeFile.GetColumnIndex("y");
            int dbhColumn = picusTreeFile.GetColumnIndex("bhdfrom");
            if (dbhColumn < 0)
            {
                dbhColumn = picusTreeFile.GetColumnIndex("dbh");
            }
            float heightConversionFactor = 0.01F; // cm to m
            int heightColumn = picusTreeFile.GetColumnIndex("treeheight");
            if (heightColumn < 0)
            {
                heightColumn = picusTreeFile.GetColumnIndex("height");
                heightConversionFactor = 1.0F; // input is in meters
            }
            int speciesColumn = picusTreeFile.GetColumnIndex("species");
            int ageColumnIndex = picusTreeFile.GetColumnIndex("age");
            if (xColumn == -1 || yColumn == -1 || dbhColumn == -1 || speciesColumn == -1 || heightColumn == -1)
            {
                throw new NotSupportedException(String.Format("Initfile {0} is not valid! Required columns are: x, y, bhdfrom or dbh, species, and treeheight or height.", treeFileName));
            }

            PointF ruOffset = ru.BoundingBox.TopLeft();
            TreeSpeciesSet speciesSet = ru.Trees.TreeSpeciesSet; // of default RU
            int treeCount = 0;
            for (int rowIndex = 1; rowIndex < picusTreeFile.RowCount; ++rowIndex)
            {
                float dbh = Single.Parse(picusTreeFile.GetValue(dbhColumn, rowIndex));
                //if (dbh<5.)
                //    continue;
                PointF physicalPosition = new();
                if (xColumn >= 0 && yColumn >= 0)
                {
                    physicalPosition.X = Single.Parse(picusTreeFile.GetValue(xColumn, rowIndex)) + ruOffset.X;
                    physicalPosition.Y = Single.Parse(picusTreeFile.GetValue(yColumn, rowIndex)) + ruOffset.Y;
                }
                // position valid?
                if (landscape.HeightGrid[physicalPosition].IsOnLandscape() == false)
                {
                    throw new NotSupportedException("Tree is not in world.");
                }

                string speciesID = picusTreeFile.GetValue(speciesColumn, rowIndex);
                if (Int32.TryParse(speciesID, out int picusID))
                {
                    int speciesIndex = StandReader.PicusSpeciesIDs.IndexOf(picusID);
                    if (speciesIndex == -1)
                    {
                        throw new NotSupportedException("Invalid Picus species id " + picusID);
                    }
                    speciesID = StandReader.iLandSpeciesIDs[speciesIndex];
                }
                TreeSpecies species = speciesSet[speciesID];
                if (ru == null || species == null)
                {
                    throw new NotSupportedException(String.Format("Loading init-file: either resource unit or species invalid. Species: {0}", speciesID));
                }

                int treeIndex = ru.Trees.AddTree(landscape, speciesID);
                Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[species.ID];
                treesOfSpecies.SetLightCellIndex(treeIndex, physicalPosition);
                if (tagColumn >= 0)
                {
                    // override default of ID = count of trees currently on resource unit
                    // So long as all trees are specified from a tree list and AddTree() isn't called later then IDs will remain unique.
                    treesOfSpecies.Tag[treeIndex] = Int32.Parse(picusTreeFile.GetValue(tagColumn, rowIndex));
                }

                treesOfSpecies.Dbh[treeIndex] = dbh;
                treesOfSpecies.SetHeight(treeIndex, heightConversionFactor * Single.Parse(picusTreeFile.GetValue(heightColumn, rowIndex))); // convert from Picus-cm to m if necessary

                int age = 0;
                if (ageColumnIndex >= 0)
                {
                    age = Int32.Parse(picusTreeFile.GetValue(ageColumnIndex, rowIndex));
                }
                treesOfSpecies.SetAge(treeIndex, age, treesOfSpecies.Height[treeIndex]);
                if (standIDcolumn >= 0)
                {
                    treesOfSpecies.StandID[treeIndex] = Int32.Parse(picusTreeFile.GetValue(standIDcolumn, rowIndex));
                }

                treesOfSpecies.Setup(projectFile, treeIndex);
                ++treeCount;
            }

            return treeCount;
        }

        /** initialize trees on a resource unit based on dbh distributions.
          use a fairly clever algorithm to determine tree positions.
          see http://iland-model.org/initialize+trees
          @param content tree init file (including headers) in a string
          @param ru resource unit
          @param fileName source file name (for error reporting)
          @return number of trees added
          */
        private int LoadDistributionList(Project projectFile, Landscape landscape, ResourceUnit ru, RandomGenerator randomGenerator, string treeFileName, int standID)
        {
            int treeCount = this.ParseInitFile(treeFileName, ru);
            if (treeCount == 0)
            {
                return 0;
            }

            if (standID > 0)
            {
                // execute stand based initialization
                this.InitializeStand(projectFile, landscape, randomGenerator, standID);
            }
            else
            {
                // exeucte the initialization based on single resource units
                this.InitializeResourceUnit(projectFile, landscape, ru, randomGenerator);
                ru.Trees.RemoveDeadTrees(); // TODO: is this necessary?
            }
            return treeCount;
        }

        private int ParseInitFile(string treeFileName, ResourceUnit defaultRU)
        {
            TreeSpeciesSet speciesSet = defaultRU.Trees.TreeSpeciesSet; // of default RU
            Debug.Assert(speciesSet != null);

            //DebugTimer t("loadiLandFile");
            CsvFile standFile = new(treeFileName);

            int countIndex = standFile.GetColumnIndex("count");
            int speciesIndex = standFile.GetColumnIndex("species");
            int dbhFromIndex = standFile.GetColumnIndex("dbh_from");
            int dbhToIndex = standFile.GetColumnIndex("dbh_to");
            int hdIndex = standFile.GetColumnIndex("hd");
            int ageIndex = standFile.GetColumnIndex("age");
            int densityIndex = standFile.GetColumnIndex("density");
            if (countIndex < 0 || speciesIndex < 0 || dbhFromIndex < 0 || dbhToIndex < 0 || hdIndex < 0 || ageIndex < 0)
            {
                throw new NotSupportedException("File '" + treeFileName + "' is missing at least one column from { count, species, dbh_from, dbh_to, hd, age }.");
            }
            int standIDindex = standFile.GetColumnIndex("stand_id");

            this.mTreeRowsByStandID.Clear();

            int totalCount = 0;
            for (int rowIndex = 0; rowIndex < standFile.RowCount; ++rowIndex)
            {
                StandInitializationFileRow standRow = new(speciesSet[standFile.GetValue(speciesIndex, rowIndex)])
                {
                    Count = Single.Parse(standFile.GetValue(countIndex, rowIndex)),
                    DbhFrom = Single.Parse(standFile.GetValue(dbhFromIndex, rowIndex)),
                    DbhTo = Single.Parse(standFile.GetValue(dbhToIndex, rowIndex)),
                    HeightDiameterRatio = Single.Parse(standFile.GetValue(hdIndex, rowIndex))
                };
                if (standRow.HeightDiameterRatio == 0.0 || standRow.DbhFrom / 100.0 * standRow.HeightDiameterRatio < Constant.Sapling.MaximumHeight)
                {
                    throw new NotSupportedException(String.Format("File '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", treeFileName, standRow.HeightDiameterRatio, standRow.DbhFrom));
                }
                // TODO: DbhFrom < DbhTo?
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                totalCount += (int)standRow.Count;

                bool setAgeToZero = true;
                if (ageIndex >= 0)
                {
                    setAgeToZero = Int32.TryParse(standFile.GetValue(ageIndex, rowIndex), out int age);
                    standRow.Age = age;
                }
                if (ageIndex < 0 || setAgeToZero == false)
                {
                    standRow.Age = 0;
                }

                if (densityIndex >= 0)
                {
                    standRow.Density = Single.Parse(standFile.GetValue(densityIndex, rowIndex));
                }
                else
                {
                    standRow.Density = 0.0F;
                }
                if (standRow.Density < -1)
                {
                    throw new NotSupportedException(String.Format("Invalid density. Allowed range is -1..1: '{0}' in file '{1}', line {2}.",
                                                                  standRow.Density, treeFileName, rowIndex));
                }
                if (standRow.TreeSpecies == null)
                {
                    throw new NotSupportedException(String.Format("Unknown species '{0}' in file '{1}', line {2}.",
                                                                  standFile.GetValue(speciesIndex, rowIndex), treeFileName, rowIndex));
                }
                if (standIDindex >= 0)
                {
                    int standID = Int32.Parse(standFile.GetValue(standIDindex, rowIndex));
                    if (this.mTreeRowsByStandID.TryGetValue(standID, out List<StandInitializationFileRow>? standRows) == false)
                    {
                        standRows = new List<StandInitializationFileRow>();
                        this.mTreeRowsByStandID.Add(standID, standRows);
                    }
                    standRows.Add(standRow);
                }
                else
                {
                    this.mTreeRowsForDefaultStand.Add(standRow);
                }
            }
            return totalCount;
        }

        // sort function
        private static int SortPairLessThan(MutableTuple<int, float> s1, MutableTuple<int, float> s2)
        {
            if (s1.Item2 < s2.Item2)
            {
                return -1;
            }
            if (s1.Item2 > s2.Item2)
            {
                return 1;
            }
            return 0;
        }

        public class HeightInitCell
        {
            public float BasalArea { get; set; } // accumulated basal area
            public Point GridCellIndex { get; set; } // location of the pixel
            public float MaxHeight { get; set; } // predefined maximum height at given pixel (if available from LIDAR or so)
            public ResourceUnit ResourceUnit { get; set; } // pointer to the resource unit the pixel belongs to
            public bool IsSingleSpecies { get; set; } // pixel is dedicated to a single species

            public HeightInitCell(ResourceUnit ru)
            {
                this.BasalArea = 0.0F;
                this.IsSingleSpecies = false;
                this.MaxHeight = -1.0F;
                this.ResourceUnit = ru;
            }
        };

        private int CompareHeightInitCells(HeightInitCell s1, HeightInitCell s2)
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

        private int SortInitPixelUnlocked(HeightInitCell s1, HeightInitCell s2)
        {
            if (!s1.IsSingleSpecies && s2.IsSingleSpecies)
            {
                return -1;
            }
            return 0;
        }

        private void InitializeResourceUnit(Project projectFile, Landscape landscape, ResourceUnit ru, RandomGenerator randomGenerator)
        {
            Debug.Assert(this.mProbabilityDistribution != null);

            //List<MutableTuple<int, float>> resourceUnitBasalAreaByHeightCellIndex = new List<MutableTuple<int, float>>();
            //for (int heightCellIndex = 0; heightCellIndex < Constant.HeightSizePerRU * Constant.HeightSizePerRU; ++heightCellIndex)
            //{
            //    resourceUnitBasalAreaByHeightCellIndex.Add(new MutableTuple<int, float>(heightCellIndex, 0.0F));
            //}

            // a multimap holds a list for all trees.
            // key is the index of a 10x10m pixel within the resource unit
            Dictionary<int, MutableTuple<Trees, List<int>>> treeIndexByHeightCellIndex = new();
            int totalTreeCount = 0;
            foreach (StandInitializationFileRow standRow in this.mTreeRowsForDefaultStand)
            {
                float randFraction = standRow.Density;
                for (int index = 0; index < standRow.Count; ++index)
                {
                    // create trees
                    int treeIndex = ru.Trees.AddTree(landscape, standRow.TreeSpecies.ID);
                    Trees treesOfSpecies = ru.Trees.TreesBySpeciesID[standRow.TreeSpecies.ID];
                    treesOfSpecies.Dbh[treeIndex] = randomGenerator.GetRandomFloat(standRow.DbhFrom, standRow.DbhTo);
                    treesOfSpecies.SetHeight(treeIndex, 0.001F * treesOfSpecies.Dbh[treeIndex] * (float)standRow.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    treesOfSpecies.Species = standRow.TreeSpecies;
                    if (standRow.Age < 1)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        treesOfSpecies.SetAge(treeIndex, standRow.Age, treesOfSpecies.Height[treeIndex]);
                    }
                    treesOfSpecies.Setup(projectFile, treeIndex);
                    ++totalTreeCount;

                    // calculate random value. "density" is from 1..-1.
                    float randomValue = (float)this.mProbabilityDistribution.GetRandomValue(randomGenerator);
                    if (standRow.Density < 0.0F)
                    {
                        randomValue = 1.0F - randomValue;
                    }
                    randomValue = randomValue * randFraction + randomGenerator.GetRandomFloat() * (1.0F - randFraction);

                    // key: rank of target pixel
                    // item1: index of target pixel
                    // item2: sum of target pixel
                    int heightCellIndex = Maths.Limit((int)(Constant.HeightSizePerRU * Constant.HeightSizePerRU * randomValue), 0, Constant.HeightSizePerRU * Constant.HeightSizePerRU - 1); // get from random number generator
                    // int heightCellIndex = landscape.HeightGrid.IndexOf(landscape.HeightGrid.GetCellIndex(treesOfSpecies.GetCellCenterPoint(treeIndex)));
                    if (treeIndexByHeightCellIndex.TryGetValue(heightCellIndex, out MutableTuple<Trees, List<int>>? treesInCell) == false)
                    {
                        treesInCell = new MutableTuple<Trees, List<int>>(treesOfSpecies, new List<int>());
                        treeIndexByHeightCellIndex.Add(heightCellIndex, treesInCell);
                    }
                    treesInCell.Item2.Add(treesOfSpecies.Tag[treeIndex]); // store tree in map

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

            for (int heightCellIndex = 0; heightCellIndex < Constant.HeightSizePerRU * Constant.HeightSizePerRU; ++heightCellIndex)
            {
                MutableTuple<Trees, List<int>> treesInCell = treeIndexByHeightCellIndex[heightCellIndex];
                PointF heightPixelCenter = ru.BoundingBox.TopLeft().Add(new PointF(Constant.HeightSize * (heightCellIndex / Constant.HeightSize + 0.5F), Constant.HeightSize * (heightCellIndex % Constant.HeightSize + 0.5F)));
                if (landscape.HeightGrid[heightPixelCenter].IsOnLandscape() == false)
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
                PointF offset = ru.BoundingBox.TopLeft();
                Point offsetIdx = landscape.LightGrid.GetCellIndex(offset);
                foreach (int treeIndex in treesInCell.Item2)
                {
                    if (treesInCell.Item2.Count > 18)
                    {
                        index = (index + 1) % 25;
                    }
                    else
                    {
                        int stop = 1000;
                        index = 0;
                        do
                        {
                            //r = drandom();
                            //if (r<0.5)  // skip position with a prob. of 50% -> adds a little "noise"
                            //    index++;
                            //index = (index + 1)%25; // increase and roll over

                            // search a random position
                            float r = randomGenerator.GetRandomFloat();
                            index = Maths.Limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Maths.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("InitializeResourceUnit(): found no free bit.");
                        }
                        Maths.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = ru.ResourceUnitGridIndex % Constant.LightSize != 0 ? EvenList[index] : UnevenList[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    Point lightCellIndex = new(offsetIdx.X + Constant.LightCellsPerHeightSize * (heightCellIndex / Constant.HeightSize) + pos / Constant.LightCellsPerHeightSize,
                                               offsetIdx.Y + Constant.LightCellsPerHeightSize * (heightCellIndex % Constant.HeightSize) + pos % Constant.LightCellsPerHeightSize);
                    //Debug.WriteLine(tree_no++ + "to" + index);
                    treesInCell.Item1.LightCellPosition[treeIndex] = lightCellIndex;
                }
            }
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland-model.org/initialize+trees
        private void InitializeStand(Project projectFile, Landscape landscape, RandomGenerator randomGenerator, int standID)
        {
            MapGrid? standGrid = landscape.StandGrid;
            if (standGrid == null)
            {
                throw new NotSupportedException("Landscape does not have a stand grid.");
            }

            // get a list of positions of all pixels that belong to our stand
            List<int> heightGridCellIndicesInStand = standGrid.GetGridIndices(standID);
            if (heightGridCellIndicesInStand.Count == 0)
            {
                Debug.WriteLine("Stand " + standID + " not in project area. No initialization performed.");
                return;
            }
            // a multiHash holds a list for all trees.
            // key is the location of the 10x10m pixel
            Dictionary<Point, MutableTuple<Trees, List<int>>> treeIndicesByHeightCellIndex = new();
            List<HeightInitCell> heightCells = new(heightGridCellIndicesInStand.Count); // working list of all 10m pixels

            foreach (int cellIndex in heightGridCellIndicesInStand)
            {
                Point heightCellIndex = standGrid.Grid.GetCellPosition(cellIndex);
                ResourceUnit ru = landscape.GetResourceUnit(standGrid.Grid.GetCellCenterPosition(heightCellIndex));
                HeightInitCell heightCell = new(ru)
                {
                    GridCellIndex = heightCellIndex // index in the 10m grid
                };
                if (this.initialHeightGrid.IsValid())
                {
                    heightCell.MaxHeight = this.initialHeightGrid.Grid[heightCell.GridCellIndex];
                }
                heightCells.Add(heightCell);
            }
            float standAreaInResourceUnits = standGrid.GetArea(standID) / Constant.RUArea;

            if (this.initialHeightGrid.IsValid() && (this.mHeightGridResponse == null))
            {
                throw new NotSupportedException("Attempt to initialize from height grid but without response function.");
            }

            Debug.Assert(this.mProbabilityDistribution != null);
            Debug.Assert(this.mHeightGridResponse != null);
            int key = 0;
            TreeSpecies? lastLockedSpecies = null;
            int treeCount = 0;
            int totalTries = 0;
            int totalMisses = 0;
            foreach (StandInitializationFileRow standRow in this.mTreeRowsForDefaultStand)
            {
                if (standRow.Density > 1.0)
                {
                    // special case with single-species-area
                    if (treeCount == 0)
                    {
                        // randomize the pixels
                        for (int heightIndex = 0; heightIndex < heightCells.Count; ++heightIndex)
                        {
                            heightCells[heightIndex].BasalArea = randomGenerator.GetRandomFloat();
                        }
                        heightCells.Sort(this.CompareHeightInitCells);

                        for (int heightIndex = 0; heightIndex < heightCells.Count; ++heightIndex)
                        {
                            heightCells[heightIndex].BasalArea = 0.0F;
                        }
                    }

                    if (standRow.TreeSpecies != lastLockedSpecies)
                    {
                        lastLockedSpecies = standRow.TreeSpecies;
                        heightCells.Sort(SortInitPixelUnlocked);
                    }
                }
                else
                {
                    heightCells.Sort(CompareHeightInitCells);
                    lastLockedSpecies = null;
                }

                double rand_fraction = standRow.Density;
                int count = (int)(standRow.Count * standAreaInResourceUnits + 0.5); // round
                double init_max_height = standRow.DbhTo / 100.0 * standRow.HeightDiameterRatio;
                for (int i = 0; i < count; ++i)
                {
                    bool found = false;
                    int tries = this.mHeightGridTries;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        double randomValue;
                        if (standRow.Density <= 1.0)
                        {
                            randomValue = this.mProbabilityDistribution.GetRandomValue(randomGenerator);
                            if (standRow.Density < 0)
                            {
                                randomValue = 1.0 - randomValue;
                            }
                            randomValue = randomValue * rand_fraction + randomGenerator.GetRandomFloat() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            randomValue = randomGenerator.GetRandomFloat() * Math.Min(standRow.Density / 100.0, 1.0);
                        }
                        ++totalTries;

                        // key: rank of target pixel
                        key = Maths.Limit((int)(heightCells.Count * randomValue), 0, heightCells.Count - 1); // get from random number generator

                        if (this.initialHeightGrid.IsValid())
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            float p_value = heightCells[key].MaxHeight > 0.0F ? (float)this.mHeightGridResponse.Evaluate(init_max_height / heightCells[key].MaxHeight) : 0.0F;
                            if (randomGenerator.GetRandomFloat() < p_value)
                            {
                                found = true;
                            }
                        }
                        else
                        {
                            found = true;
                        }
                        if (lastLockedSpecies != null && heightCells[key].IsSingleSpecies)
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
                    Trees trees = ru.Trees.TreesBySpeciesID[standRow.TreeSpecies.ID];
                    int treeIndex = ru.Trees.AddTree(landscape, standRow.TreeSpecies.ID);
                    trees.Dbh[treeIndex] = (float)randomGenerator.GetRandomFloat(standRow.DbhFrom, standRow.DbhTo);
                    trees.SetHeight(treeIndex, trees.Dbh[treeIndex] / 100.0F * standRow.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    trees.Species = standRow.TreeSpecies;
                    if (standRow.Age <= 0)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        trees.SetAge(treeIndex, standRow.Age, trees.Height[treeIndex]);
                    }
                    trees.Setup(projectFile, treeIndex);
                    ++treeCount;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    if (treeIndicesByHeightCellIndex.TryGetValue(heightCells[key].GridCellIndex, out MutableTuple<Trees, List<int>>? treesInCell) == false)
                    {
                        treesInCell = new MutableTuple<Trees, List<int>>(trees, new List<int>());
                        treeIndicesByHeightCellIndex.Add(heightCells[key].GridCellIndex, treesInCell);
                    }
                    treesInCell.Item2.Add(treeIndex);

                    heightCells[key].BasalArea += trees.GetBasalArea(treeIndex); // aggregate the basal area for each 10m pixel
                    if (lastLockedSpecies != null)
                    {
                        heightCells[key].IsSingleSpecies = true;
                    }

                    // resort list
                    if (lastLockedSpecies == null && ((treeCount < 20 && i % 2 == 0) || (treeCount < 100 && i % 10 == 0) || (i % 30 == 0)))
                    {
                        heightCells.Sort(CompareHeightInitCells);
                    }
                }
            }

            foreach (HeightInitCell heightCell in heightCells)
            {
                MutableTuple<Trees, List<int>> treesInCell = treeIndicesByHeightCellIndex[heightCell.GridCellIndex];
                int index = -1;
                foreach (int treeIndex in treesInCell.Item2)
                {
                    if (treesInCell.Item2.Count > 18)
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
                            float random = randomGenerator.GetRandomFloat();
                            index = Maths.Limit((int)(25 * random * random), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Maths.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("InitializeStand(): found no free bit.");
                        }
                        Maths.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = heightCell.ResourceUnit.ResourceUnitGridIndex % Constant.LightSize != 0 ? StandReader.EvenList[index] : StandReader.UnevenList[index];
                    Point lightCellIndex = new(heightCell.GridCellIndex.X * Constant.LightCellsPerHeightSize + pos / Constant.LightCellsPerHeightSize, // convert to LIF index
                                               heightCell.GridCellIndex.Y * Constant.LightCellsPerHeightSize + pos % Constant.LightCellsPerHeightSize);
                    if (landscape.LightGrid.Contains(lightCellIndex) == false)
                    {
                        throw new NotSupportedException("Tree is positioned outside of the light grid.");
                    }

                    Trees trees = treesInCell.Item1;
                    trees.LightCellPosition[treeIndex] = lightCellIndex;
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
        //        Debug.WriteLine("stand " + standID + " not in project area. No init performed.");
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
        //        int pxcount = (int)Math.Round(Double.Parse(init.GetValue(countIndex, row)) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
        //        TreeSpecies species = set.GetSpecies(init.GetValue(speciesIndex, row));
        //        if (species == null)
        //        {
        //            throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.GetValue(speciesIndex, row)));
        //        }
        //        float height = heightIndex == -1 ? Constant.Sapling.MinimumHeight : Single.Parse(init.GetValue(heightIndex, row));
        //        int age = ageIndex == -1 ? 1 : Int32.Parse(init.GetValue(ageIndex, row));

        //        int misses = 0;
        //        int hits = 0;
        //        while (hits < pxcount)
        //        {
        //            // sapling location
        //            int rnd_index = model.RandomGenerator.GetRandomInteger(0, indices.Count);
        //            Point offset = standGrid.Grid.GetCellPosition(indices[rnd_index]);
        //            offset.X *= Constant.LightCellsPerHeightSize; // index of 10m patch -> to lif pixel coordinates
        //            offset.Y *= Constant.LightCellsPerHeightSize;
        //            int in_p = model.RandomGenerator.GetRandomInteger(0, Constant.LightCellsPerHeightSize * Constant.LightCellsPerHeightSize); // index of lif-pixel
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
        //                Debug.WriteLine("tried to add " + pxcount + " saplings at stand " + standID + " but failed in finding enough free positions. Added " + hits + " and stopped.");
        //                break;
        //            }
        //        }
        //        total += hits;

        //    }
        //    return total;
        //}

        private int CompareLifValue(KeyValuePair<int, float> a, KeyValuePair<int, float> b)
        {
            // reverse order
            if (a.Value > b.Value)
            {
                return -1;
            }
            if (a.Value < b.Value)
            {
                return 1;
            }
            return 0;
        }

        private int LoadSaplingsLif(Model model, int standID, CsvFile saplingFile, int startRowIndex, int endRowIndex)
        {
            MapGrid? standGrid = model.Landscape.StandGrid; // default
            if (standGrid == null)
            {
                throw new NotSupportedException();
            }
            if (standGrid.IsValid(standID) == false)
            {
                throw new NotSupportedException();
            }

            List<int> standGridIndices = standGrid.GetGridIndices(standID); // list of 10x10m pixels
            if (standGridIndices.Count == 0)
            {
                // Debug.WriteLine("Stand " + standID + " not in project area. No initialization performed.");
                return 0;
            }

            // prepare space for LIF-pointers (2m Pixel)
            List<KeyValuePair<int, float>> lightCellIndexAndValues = new(standGridIndices.Count * Constant.LightCellsPerHeightSize * Constant.LightCellsPerHeightSize);
            Grid<float> lightGrid = model.Landscape.LightGrid;
            for (int standGridIndex = 0; standGridIndex < standGridIndices.Count; ++standGridIndex)
            {
                Point cellOrigin = standGrid.Grid.GetCellPosition(standGridIndices[standGridIndex]);
                cellOrigin.X *= Constant.LightCellsPerHeightSize; // index of 10m patch -> to lif pixel coordinates
                cellOrigin.Y *= Constant.LightCellsPerHeightSize;
                for (int lightY = 0; lightY < Constant.LightCellsPerHeightSize; ++lightY)
                {
                    for (int lightX = 0; lightX < Constant.LightCellsPerHeightSize; ++lightX)
                    {
                        int modelIndex = lightGrid.IndexOf(cellOrigin.X + lightX, cellOrigin.Y + lightY);
                        KeyValuePair<int, float> indexAndValue = new(modelIndex, lightGrid[modelIndex]);
                        lightCellIndexAndValues.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lightCellIndexAndValues.Sort(CompareLifValue); // higher: highest values first

            float standAreaInHa = standGrid.GetArea(standID) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            int speciesIndex = saplingFile.GetColumnIndex("species");
            int countIndex = saplingFile.GetColumnIndex("count");
            int heightIndex = saplingFile.GetColumnIndex("height");
            int heightFromIndex = saplingFile.GetColumnIndex("height_from");
            int heightToIndex = saplingFile.GetColumnIndex("height_to");
            int ageIndex = saplingFile.GetColumnIndex("age");
            int topAgeIndex = saplingFile.GetColumnIndex("age4m");
            int minLifIndex = saplingFile.GetColumnIndex("min_lif");
            if ((heightFromIndex == -1) ^ (heightToIndex == -1))
            {
                throw new FileLoadException("Height not correctly provided. Use either 'height' or 'height_from' and 'height_to'.");
            }
            if (speciesIndex == -1 || countIndex == -1)
            {
                throw new FileLoadException("Column 'species' or 'count' is missing.");
            }

            int total = 0;
            for (int row = startRowIndex; row <= endRowIndex; ++row)
            {
                int cellsWithSaplings = (int)(Double.Parse(saplingFile.GetValue(countIndex, row)) * standAreaInHa); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                // TODO: constants for defaults, 5 cm is also hard coded elsewhere
                float height = heightIndex == -1 ? Constant.Sapling.MinimumHeight : Single.Parse(saplingFile.GetValue(heightIndex, row));
                int age = ageIndex == -1 ? 1 : Int32.Parse(saplingFile.GetValue(ageIndex, row));
                float age4m = topAgeIndex == -1 ? 10.0F : Single.Parse(saplingFile.GetValue(topAgeIndex, row));
                float minHeight = heightFromIndex == -1 ? -1.0F : Single.Parse(saplingFile.GetValue(heightFromIndex, row));
                float maxHeight = heightToIndex == -1 ? -1.0F : Single.Parse(saplingFile.GetValue(heightToIndex, row));
                float minLightIntensity = minLifIndex == -1 ? 1.0F : Single.Parse(saplingFile.GetValue(minLifIndex, row));
                // find LIF-level in the pixels
                int minLightIndex = 0;
                if (minLightIntensity < 1.0)
                {
                    for (int lightIndex = 0; lightIndex < lightCellIndexAndValues.Count; ++lightIndex, ++minLightIndex)
                    {
                        if (lightCellIndexAndValues[lightIndex].Value <= minLightIntensity)
                        {
                            break;
                        }
                    }
                    if (cellsWithSaplings < minLightIndex)
                    {
                        // not enough LIF pixels available
                        minLightIndex = cellsWithSaplings; // try the brightest pixels (ie with the largest value for the LIF)
                    }
                }
                else
                {
                    // No LIF threshold: the full range of pixels is valid
                    minLightIndex = lightCellIndexAndValues.Count;
                }

                float fractionallyOccupiedCellCount = 0.0F;
                while (fractionallyOccupiedCellCount < cellsWithSaplings)
                {
                    int randomIndex = model.RandomGenerator.GetRandomInteger(0, minLightIndex);
                    if (heightFromIndex != -1)
                    {
                        height = (float)model.RandomGenerator.GetRandomFloat(minHeight, maxHeight);
                        if (age <= 1)
                        {
                            age = Math.Max((int)MathF.Round(height / Constant.Sapling.MaximumHeight * age4m), 1); // assume a linear relationship between height and age
                        }
                    }
                    Point lightCellIndex = lightGrid.GetCellPosition(lightCellIndexAndValues[randomIndex].Key);
                    SaplingCell? saplingCell = model.Landscape.GetSaplingCell(lightCellIndex, true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        TreeSpecies species = ru.Trees.TreeSpeciesSet[saplingFile.GetValue(speciesIndex, row)];
                        Sapling? sapling = saplingCell.AddSaplingIfSlotFree(height, age, species.Index);
                        if (sapling != null)
                        {
                            fractionallyOccupiedCellCount += Math.Max(1.0F, species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(sapling.Height));
                        }
                        else
                        {
                            ++fractionallyOccupiedCellCount;
                        }
                    }
                    else
                    {
                        ++fractionallyOccupiedCellCount; // avoid an infinite loop
                    }
                }

                total += cellsWithSaplings;
            }

            // initialize grass cover
            if (saplingFile.GetColumnIndex("grass_cover") > -1)
            {
                int grass_cover_value = Int32.Parse(saplingFile.GetValue("grass_cover", startRowIndex));
                if (grass_cover_value < 0 || grass_cover_value > 100)
                {
                    throw new NotSupportedException(String.Format("The grass cover percentage (column 'grass_cover') for stand '{0}' is '{1}', which is invalid (expected: 0-100)", standID, grass_cover_value));
                }
                model.Landscape.GrassCover.SetInitialValues(model.RandomGenerator, lightCellIndexAndValues, grass_cover_value);
            }

            return total;
        }

        private class StandInitializationFileRow
        {
            public int Age { get; set; }
            public float Count { get; set; }
            public float Density { get; set; }
            public float DbhFrom { get; set; }
            public float DbhTo { get; set; }
            public float HeightDiameterRatio { get; set; }
            public TreeSpecies TreeSpecies { get; set; }

            public StandInitializationFileRow(TreeSpecies treeSpecies)
            {
                this.TreeSpecies = treeSpecies;
            }
        }
    }
}
