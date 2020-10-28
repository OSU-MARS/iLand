using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;

namespace iLand.Input
{
    /** @class StandLoader
        loads (initializes) trees for a "stand" from various sources.
        StandLoader initializes trees on the landscape. It reads (usually) from text files, creates the
        trees and distributes the trees on the landscape (on the ResoureceUnit or on a stand defined by a grid).

        See http://iland.boku.ac.at/initialize+trees
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

        private readonly Simulation.Model mModel;
        private RandomCustomPdf mRandom;
        private List<StandInitializationFileRow> mInitItems;
        private readonly Dictionary<int, List<StandInitializationFileRow>> mStandInitItems;
        private Expression mHeightGridResponse; // response function to calculate fitting of pixels with pre-determined height
        private int mHeightGridTries; // maximum number of tries to land at pixel with fitting height

        /// set a constraining height grid (10m resolution)
        public MapGrid InitHeightGrid { get; set; } // grid with tree heights

        public StandReader(Simulation.Model model)
        {
            this.mHeightGridResponse = null;
            this.mModel = model;
            this.mRandom = null;
            this.mStandInitItems = new Dictionary<int, List<StandInitializationFileRow>>();

            this.InitHeightGrid = new MapGrid();
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
        public void Setup(Simulation.Model model)
        {
            string initializationMode = model.Project.Model.Initialization.Mode;
            string type = model.Project.Model.Initialization.Type;
            string fileName = model.Project.Model.Initialization.File;

            bool heightGridEnabled = model.Project.Model.Initialization.HeightGrid.Enabled;
            mHeightGridTries = model.Project.Model.Initialization.HeightGrid.MaxTries;
            if (heightGridEnabled)
            {
                string initHeightGridFile = model.Files.GetPath(model.Project.Model.Initialization.HeightGrid.FileName);
                Debug.WriteLine("StandReader.Setup(): using predefined tree heights map " + initHeightGridFile);

                this.InitHeightGrid = new MapGrid(model, initHeightGridFile);
                if (this.InitHeightGrid.IsValid() == false)
                {
                    throw new NotSupportedException(String.Format("Error when loading grid with tree heights for stand initalization: file {0} not found or not valid.", initHeightGridFile));
                }

                string expr = model.Project.Model.Initialization.HeightGrid.FitFormula;
                mHeightGridResponse = new Expression(expr);
                mHeightGridResponse.Linearize(model, 0.0, 2.0);
            }

            //Tree.ResetStatistics();

            // one global init-file for the whole area:
            if (String.Equals(initializationMode, "single", StringComparison.Ordinal))
            {
                // useful for 1ha simulations only...
                if (model.ResourceUnits.Count > 1)
                {
                    throw new NotSupportedException("'mode' is 'single' but more than one resource unit is simulated (consider using another mode).");
                }

                this.LoadInitFile(fileName, type, 0, model, model.ResourceUnits[0]); // this is the first resource unit
                this.EvaluateDebugTrees(model);
                return;
            }

            // call a single tree init for each resource unit
            if (initializationMode == "unit")
            {
                foreach (ResourceUnit ru in model.ResourceUnits)
                {
                    // set environment
                    model.Environment.SetPosition(ru.BoundingBox.Center(), model);
                    if (String.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }
                    this.LoadInitFile(fileName, type, 0, model, ru);
                    if (model.Files.LogDebug())
                    {
                        Debug.WriteLine("StandReader.Setup(): loaded " + fileName + " on " + ru.BoundingBox + ", " + ru.TreesBySpeciesID.Count + " tree species.");
                    }
                }
                this.EvaluateDebugTrees(model);
                return;
            }

            // map-modus: load a init file for each "polygon" in the standgrid
            if (initializationMode == "map")
            {
                if (model.StandGrid == null || model.StandGrid.IsValid() == false)
                {
                    throw new NotSupportedException("model.initialization.mode is 'map' but there is no valid stand grid defined (model.world.standGrid)");
                }
                string mapFileName = model.Files.GetPath(model.Project.Model.Initialization.MapFileName);

                CsvFile mapFile = new CsvFile(mapFileName);
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
                
                for (int row = 0; row < mapFile.RowCount; row++)
                {
                    int key = Int32.Parse(mapFile.GetValue(idColumn, row));
                    if (key > 0)
                    {
                        fileName = mapFile.GetValue(fileNameColumn, row);
                        if (model.Files.LogDebug())
                        {
                            Debug.WriteLine("StandReader.Setup(): loading " + fileName + " for grid id " + key);
                        }
                        if (String.IsNullOrEmpty(fileName) == false)
                        {
                            this.LoadInitFile(fileName, type, key, null);
                        }
                    }
                }

                this.InitHeightGrid = null;
                this.EvaluateDebugTrees(model);
                return;
            }

            // standgrid mode: load one large init file
            if (initializationMode == "standgrid")
            {
                fileName = model.Files.GetPath(fileName, "init");
                if (!File.Exists(fileName))
                {
                    throw new FileNotFoundException(String.Format("File '{0}' does not exist.", fileName));
                }
                string content = File.ReadAllText(fileName);
                // this processes the init file (also does the checking) and
                // stores in a Dictionary datastrucutre
                ParseInitFile(content, fileName);

                // setup the random distribution
                string density_func = model.Project.Model.Initialization.RandomFunction;
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("StandReader.Setup(): density function: " + density_func);
                }
                if (mRandom == null || (mRandom.DensityFunction != density_func))
                {
                    mRandom = new RandomCustomPdf(model, density_func);
                    if (model.Files.LogDebug())
                    {
                        Debug.WriteLine("StandReader.Setup(): new probabilty density function: " + density_func);
                    }
                }

                if (mStandInitItems.Count == 0)
                {
                    Debug.WriteLine("Initialize trees ('standgrid'-mode): no items to process (empty landscape).");
                    return;
                    //throw new NotSupportedException("processInit: 'mode' is 'standgrid' but the init file is either empty or contains no 'stand_id'-column.");
                }
                foreach (KeyValuePair<int, List<StandInitializationFileRow>> it in mStandInitItems)
                {
                    mInitItems = it.Value; // copy the items...
                    this.InitializeStand(model, it.Key);
                }

                Debug.WriteLine("StandReader.Setup(): finished setup of trees.");
                this.EvaluateDebugTrees(model);
                return;
            }

            //if (initializationMode == "snapshot")
            //{
            //    // load a snapshot from a file
            //    Snapshot shot = new Snapshot();
            //    string snapshotDatabasePath = model.GlobalSettings.Path(fileName);
            //    shot.Load(snapshotDatabasePath, model);
            //    return;
            //}
            throw new NotSupportedException("StandReader.Setup(): invalid initalization.mode!");
        }

        public void SetupSaplings(Simulation.Model model)
        {
            string mode = model.Project.Model.Initialization.Mode;
            if (mode == "standgrid")
            {
                // load a file with saplings per polygon
                string saplingFileName = model.Project.Model.Initialization.SaplingFile;
                if (String.IsNullOrEmpty(saplingFileName))
                {
                    return;
                }
                saplingFileName = model.Files.GetPath(saplingFileName, "init");
                if (File.Exists(saplingFileName) == false)
                {
                    throw new NotSupportedException(String.Format("load-sapling-ini-file: file '{0}' does not exist.", saplingFileName));
                }
                CsvFile saplingFile = new CsvFile(saplingFileName);
                int standIDindex = saplingFile.GetColumnIndex("stand_id");
                if (standIDindex == -1)
                {
                    throw new NotSupportedException("he init file contains no 'stand_id' column (required in 'standgrid' mode).");
                }

                int previousStandID = -99999;
                int standStartRow = -1;
                int total = 0;
                for (int row = 0; row < saplingFile.RowCount; ++row)
                {
                    int standID = Int32.Parse(saplingFile.GetValue(standIDindex, row));
                    if (standID != previousStandID)
                    {
                        if (previousStandID >= 0)
                        {
                            // process stand
                            int standEndRow = row - 1; // up to the last
                            total += this.LoadSaplingsLif(model, previousStandID, saplingFile, standStartRow, standEndRow);
                        }
                        standStartRow = row; // mark beginning of new stand
                        previousStandID = standID;
                    }
                }
                if (previousStandID >= 0)
                {
                    total += this.LoadSaplingsLif(model, previousStandID, saplingFile, standStartRow, saplingFile.RowCount - 1); // the last stand
                }
                Debug.WriteLine("initialization of sapling: total created: " + total);
            }
        }

        public void EvaluateDebugTrees(Simulation.Model model)
        {
            // evaluate debugging
            string dbg_str = model.Project.Model.Parameter.DebugTree;
            int counter = 0;
            if (String.IsNullOrEmpty(dbg_str) == false)
            {
                if (String.Equals(dbg_str, "debugstamp", StringComparison.OrdinalIgnoreCase))
                {
                    // check for trees which aren't correctly placed
                    AllTreesEnumerator treeIterator = new AllTreesEnumerator(model);
                    while (treeIterator.MoveNext())
                    {
                        if (model.LightGrid.Contains(treeIterator.CurrentTrees.LightCellPosition[treeIterator.CurrentTreeIndex]) == false)
                        {
                            throw new NotSupportedException("debugstamp: invalid tree position found.");
                        }
                    }
                    return;
                }

                TreeWrapper treeWrapper = new TreeWrapper();
                Expression debugExp = new Expression(dbg_str, treeWrapper); // load expression dbg_str and enable external model variables
                AllTreesEnumerator treeIndex = new AllTreesEnumerator(model);
                while (treeIndex.MoveNext())
                {
                    // TODO: why is debug expression evaluated for all trees rather than just trees marked for debugging?
                    treeWrapper.Trees = treeIndex.CurrentTrees;
                    double result = debugExp.Execute(model);
                    if (result != 0.0)
                    {
                        treeIndex.CurrentTrees.SetDebugging(treeIndex.CurrentTreeIndex);
                        counter++;
                    }
                }
            }
        }

        /// load a single init file. Calls loadPicusFile() or loadiLandFile()
        /// @param fileName file to load
        /// @param type init mode. allowed: "picus"/"single" or "iland"/"distribution"
        public int LoadInitFile(string fileName, string type, int standID, Simulation.Model model, ResourceUnit ru = null)
        {
            string pathFileName = model.Files.GetPath(fileName, "init");
            if (!File.Exists(pathFileName))
            {
                throw new FileNotFoundException(String.Format("File '{0}' does not exist!", pathFileName));
            }

            if (type == "picus" || type == "single")
            {
                if (standID > 0)
                {
                    throw new XmlException(String.Format("Initialization type '{0}' currently not supported for stand initilization mode!" + type));
                }
                return LoadPicusFile(pathFileName, ru, model);
            }
            if (type == "iland" || type == "distribution")
            {
                return LoadiLandFile(model, pathFileName, ru, standID);
            }
            throw new XmlException("Unknown initialization type '" + type + "'. Is a /project/model/initialization/type element present in the project file?");
        }

        public int LoadPicusFile(string fileName, ResourceUnit ru, Simulation.Model model)
        {
            string content = File.ReadAllText(fileName);
            if (String.IsNullOrEmpty(content))
            {
                Debug.WriteLine("file not found: " + fileName);
                return 0;
            }
            return LoadSingleTreeList(model, content, ru, fileName);
        }

        /** load a list of trees (given by content) to a resource unit. Param fileName is just for error reporting.
            returns the number of loaded trees.
          */
        private int LoadSingleTreeList(Simulation.Model model, string treeList, ResourceUnit ru, string fileName)
        {
            PointF ruOffset = ru.BoundingBox.TopLeft();
            TreeSpeciesSet speciesSet = ru.TreeSpeciesSet; // of default RU

            string trimmedList = treeList;
            // cut out the <trees> </trees> part if present
            int treesElementStart = treeList.IndexOf("<trees>", StringComparison.Ordinal);
            if (treesElementStart > -1)
            {
                int treesElementStop = treeList.IndexOf("</trees>", StringComparison.Ordinal);
                trimmedList = treeList[(treesElementStart + "<trees>".Length)..(treesElementStop - 1)];
            }

            CsvFile infile = new CsvFile();
            infile.LoadFromString(trimmedList);

            int idColumn = infile.GetColumnIndex("id");
            int xColumn = infile.GetColumnIndex("x");
            int yColumn = infile.GetColumnIndex("y");
            int dbhColumn = infile.GetColumnIndex("bhdfrom");
            if (dbhColumn < 0)
            {
                dbhColumn = infile.GetColumnIndex("dbh");
            }
            double heightConversionFactor = 100.0; // cm to m
            int heightColumn = infile.GetColumnIndex("treeheight");
            if (heightColumn < 0)
            {
                heightColumn = infile.GetColumnIndex("height");
                heightConversionFactor = 1.0; // input is in meters
            }
            int speciesColumn = infile.GetColumnIndex("species");
            int ageColumnIndex = infile.GetColumnIndex("age");
            if (xColumn == -1 || yColumn == -1 || dbhColumn == -1 || speciesColumn == -1 || heightColumn == -1)
            {
                throw new NotSupportedException(String.Format("Initfile {0} is not valid! Required columns are: x, y, bhdfrom or dbh, species, and treeheight or height.", fileName));
            }

            int treCount = 0;
            for (int rowIndex = 1; rowIndex < infile.RowCount; rowIndex++)
            {
                double dbh = Double.Parse(infile.GetValue(dbhColumn, rowIndex));
                //if (dbh<5.)
                //    continue;
                PointF physicalPosition = new PointF();
                if (xColumn >= 0 && yColumn >= 0)
                {
                    physicalPosition.X = Single.Parse(infile.GetValue(xColumn, rowIndex)) + ruOffset.X;
                    physicalPosition.Y = Single.Parse(infile.GetValue(yColumn, rowIndex)) + ruOffset.Y;
                }
                // position valid?
                if (mModel.HeightGrid[physicalPosition].IsInWorld() == false)
                {
                    throw new NotSupportedException("Tree is not in world.");
                }

                string speciesID = infile.GetValue(speciesColumn, rowIndex);
                if (Int32.TryParse(speciesID, out int picusID))
                {
                    int idx = PicusSpeciesIDs.IndexOf(picusID);
                    if (idx == -1)
                    {
                        throw new NotSupportedException("Invalid Picus species id " + picusID);
                    }
                    speciesID = iLandSpeciesIDs[idx];
                }
                TreeSpecies species = speciesSet.GetSpecies(speciesID);
                if (ru == null || species == null)
                {
                    throw new NotSupportedException(String.Format("Loading init-file: either resource unit or species invalid. Species: {0}", speciesID));
                }

                int treeIndex = ru.AddTree(model, speciesID);
                Trees treesOfSpecies = ru.TreesBySpeciesID[species.ID];
                treesOfSpecies.SetLightCellIndex(treeIndex, physicalPosition);
                if (idColumn >= 0)
                {
                    treesOfSpecies.ID[treeIndex] = Int32.Parse(infile.GetValue(idColumn, rowIndex));
                }

                treesOfSpecies.Dbh[treeIndex] = (float)dbh;
                treesOfSpecies.SetHeight(treeIndex, Single.Parse(infile.GetValue(heightColumn, rowIndex)) / (float)heightConversionFactor); // convert from Picus-cm to m if necessary

                int age = 0;
                if (ageColumnIndex >= 0)
                {
                    age = Int32.Parse(infile.GetValue(ageColumnIndex, rowIndex));
                }
                treesOfSpecies.SetAge(treeIndex, age, treesOfSpecies.Height[treeIndex]);

                treesOfSpecies.Setup(model, treeIndex);
                treCount++;
            }
            return treCount;
            //Debug.WriteLine("loaded init-file contained" + lines.count() + "lines.";
            //Debug.WriteLine("lines: " + lines;
        }

        /** initialize trees on a resource unit based on dbh distributions.
          use a fairly clever algorithm to determine tree positions.
          see http://iland.boku.ac.at/initialize+trees
          @param content tree init file (including headers) in a string
          @param ru resource unit
          @param fileName source file name (for error reporting)
          @return number of trees added
          */
        public int LoadDistributionList(Simulation.Model model, string content, ResourceUnit ru, int standID, string fileName)
        {
            int total_count = ParseInitFile(content, fileName, ru);
            if (total_count == 0)
            {
                return 0;
            }

            // setup the random distribution
            string densityFunction = model.Project.Model.Initialization.RandomFunction;
            if (model.Files.LogDebug())
            {
                Debug.WriteLine("density function: " + densityFunction);
            }
            if (mRandom == null || (mRandom.DensityFunction != densityFunction))
            {
                mRandom = new RandomCustomPdf(model, densityFunction);
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("new probabilty density function: " + densityFunction);
                }
            }
            if (standID > 0)
            {
                // execute stand based initialization
                InitializeStand(model, standID);
            }
            else
            {
                // exeucte the initialization based on single resource units
                InitializeResourceUnit(model, ru);
                ru.RemoveDeadTrees(); // TODO: is this necessary?
            }
            return total_count;
        }

        public int ParseInitFile(string content, string fileName, ResourceUnit ru = null)
        {
            TreeSpeciesSet speciesSet = ru.TreeSpeciesSet; // of default RU
            Debug.Assert(speciesSet != null);

            //DebugTimer t("loadiLandFile");
            CsvFile infile = new CsvFile();
            infile.LoadFromString(content);

            int countIndex = infile.GetColumnIndex("count");
            int speciesIndex = infile.GetColumnIndex("species");
            int dbhFromIndex = infile.GetColumnIndex("dbh_from");
            int dbhToIndex = infile.GetColumnIndex("dbh_to");
            int hdIndex = infile.GetColumnIndex("hd");
            int ageIndex = infile.GetColumnIndex("age");
            int densityIndex = infile.GetColumnIndex("density");
            if (countIndex < 0 || speciesIndex < 0 || dbhFromIndex < 0 || dbhToIndex < 0 || hdIndex < 0 || ageIndex < 0)
            {
                throw new NotSupportedException("File '" + fileName + "' is missing at least one column from { count, species, dbh_from, dbh_to, hd, age }.");
            }
            int standIDindex = infile.GetColumnIndex("stand_id");
            
            mInitItems.Clear();
            mStandInitItems.Clear();

            int totalCount = 0;
            for (int row = 0; row < infile.RowCount; row++)
            {
                StandInitializationFileRow initItem = new StandInitializationFileRow()
                {
                    Count = Double.Parse(infile.GetValue(countIndex, row)),
                    DbhFrom = Double.Parse(infile.GetValue(dbhFromIndex, row)),
                    DbhTo = Double.Parse(infile.GetValue(dbhToIndex, row)),
                    HeightDiameterRatio = Double.Parse(infile.GetValue(hdIndex, row))
                };
                if (initItem.HeightDiameterRatio == 0.0 || initItem.DbhFrom / 100.0 * initItem.HeightDiameterRatio < Constant.Sapling.MaximumHeight)
                {
                    Trace.TraceWarning(String.Format("File '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, initItem.HeightDiameterRatio, initItem.DbhFrom));
                }
                // TODO: DbhFrom < DbhTo?
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                totalCount += (int)initItem.Count;

                bool setAgeToZero = true;
                if (ageIndex >= 0)
                {
                    setAgeToZero = Int32.TryParse(infile.GetValue(ageIndex, row), out int age);
                    initItem.Age = age;
                }
                if (ageIndex < 0 || setAgeToZero == false)
                {
                    initItem.Age = 0;
                }

                initItem.Species = speciesSet.GetSpecies(infile.GetValue(speciesIndex, row));
                if (densityIndex >= 0)
                {
                    initItem.Density = Double.Parse(infile.GetValue(densityIndex, row));
                }
                else
                {
                    initItem.Density = 0.0;
                }
                if (initItem.Density < -1)
                {
                    throw new NotSupportedException(String.Format("Invalid density. Allowed range is -1..1: '{0}' in file '{1}', line {2}.",
                                                                  initItem.Density, fileName, row));
                }
                if (initItem.Species == null)
                {
                    throw new NotSupportedException(String.Format("Unknown species '{0}' in file '{1}', line {2}.",
                                                                  infile.GetValue(speciesIndex, row), fileName, row));
                }
                if (standIDindex >= 0)
                {
                    int standid = Int32.Parse(infile.GetValue(standIDindex, row));
                    mStandInitItems[standid].Add(initItem);
                }
                else
                {
                    mInitItems.Add(initItem);
                }
            }
            return totalCount;
        }

        public int LoadiLandFile(Simulation.Model model, string fileName, ResourceUnit ru, int standID)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("File '" + fileName + "' does not exist.");
            }
            string content = File.ReadAllText(fileName);
            return LoadDistributionList(model, content, ru, standID, fileName);
        }

        // sort function
        public int SortPairLessThan(MutableTuple<int, double> s1, MutableTuple<int, double> s2)
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
            public double BasalArea { get; set; } // accumulated basal area
            public Point CellPosition { get; set; } // location of the pixel
            public double MaxHeight { get; set; } // predefined maximum height at given pixel (if available from LIDAR or so)
            public ResourceUnit ResourceUnit { get; set; } // pointer to the resource unit the pixel belongs to
            public bool IsSingleSpecies { get; set; } // pixel is dedicated to a single species

            public HeightInitCell()
            {
                this.BasalArea = 0.0;
                this.IsSingleSpecies = false;
                this.MaxHeight = -1.0;
                this.ResourceUnit = null;
            }
        };

        private int SortInitPixelLessThan(HeightInitCell s1, HeightInitCell s2)
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

        private void InitializeResourceUnit(Simulation.Model model, ResourceUnit ru)
        {
            List<MutableTuple<int, double>> resourceUnitBasalAreaByHeightCellIndex = new List<MutableTuple<int, double>>();
            for (int heightCellIndex = 0; heightCellIndex < Constant.HeightSizePerRU * Constant.HeightSizePerRU; ++heightCellIndex)
            {
                resourceUnitBasalAreaByHeightCellIndex.Add(new MutableTuple<int, double>(heightCellIndex, 0.0));
            }

            // a multimap holds a list for all trees.
            // key is the index of a 10x10m pixel within the resource unit
            Dictionary<int, MutableTuple<Trees, List<int>>> treeIndexByHeightCellIndex = new Dictionary<int, MutableTuple<Trees, List<int>>>();
            int totalTreeCount = 0;
            foreach (StandInitializationFileRow initItem in mInitItems)
            {
                double rand_fraction = initItem.Density;
                for (int item = 0; item < initItem.Count; ++item)
                {
                    // create trees
                    int treeIndex = ru.AddTree(model, initItem.Species.ID);
                    Trees treesOfSpecies = ru.TreesBySpeciesID[initItem.Species.ID];
                    treesOfSpecies.Dbh[treeIndex] = (float)model.RandomGenerator.GetRandomDouble(initItem.DbhFrom, initItem.DbhTo);
                    treesOfSpecies.SetHeight(treeIndex, 0.001F * treesOfSpecies.Dbh[treeIndex] * (float)initItem.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    treesOfSpecies.Species = initItem.Species;
                    if (initItem.Age < 1)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        treesOfSpecies.SetAge(treeIndex, initItem.Age, treesOfSpecies.Height[treeIndex]);
                    }
                    treesOfSpecies.Setup(model, treeIndex);
                    ++totalTreeCount;

                    // calculate random value. "density" is from 1..-1.
                    double randomValue = mRandom.GetRandomValue(model);
                    if (initItem.Density < 0)
                    {
                        randomValue = 1.0 - randomValue;
                    }
                    randomValue = randomValue * rand_fraction + model.RandomGenerator.GetRandomDouble() * (1.0 - rand_fraction);

                    // key: rank of target pixel
                    // item1: index of target pixel
                    // item2: sum of target pixel
                    int randomHeightCellIndex = Maths.Limit((int)(Constant.HeightSizePerRU * Constant.HeightSizePerRU * randomValue), 0, Constant.HeightSizePerRU * Constant.HeightSizePerRU - 1); // get from random number generator
                    if (treeIndexByHeightCellIndex.TryGetValue(randomHeightCellIndex, out MutableTuple<Trees, List<int>> treesInCell) == false)
                    {
                        treesInCell = new MutableTuple<Trees, List<int>>(treesOfSpecies, new List<int>());
                        treeIndexByHeightCellIndex.Add(randomHeightCellIndex, treesInCell);
                    }
                    treesInCell.Item2.Add(treesOfSpecies.ID[treeIndex]); // store tree in map

                    MutableTuple<int, double> resourceUnitBasalArea = resourceUnitBasalAreaByHeightCellIndex[randomHeightCellIndex];
                    resourceUnitBasalArea.Item2 += treesOfSpecies.GetBasalArea(treeIndex); // aggregate the basal area for each 10m pixel
                    if ((totalTreeCount < 20 && item % 2 == 0) || 
                        (totalTreeCount < 100 && item % 10 == 0) || 
                        (item % 30 == 0))
                    {
                        resourceUnitBasalAreaByHeightCellIndex.Sort(SortPairLessThan);
                    }
                }
                resourceUnitBasalAreaByHeightCellIndex.Sort(SortPairLessThan);
            }

            for (int heightCellIndex = 0; heightCellIndex < Constant.HeightSizePerRU * Constant.HeightSizePerRU; ++heightCellIndex)
            {
                MutableTuple<Trees, List<int>> treesInCell = treeIndexByHeightCellIndex[heightCellIndex];
                PointF heightPixelCenter = ru.BoundingBox.TopLeft().Add(new PointF(Constant.HeightSize * (heightCellIndex / Constant.HeightSize + 0.5F), Constant.HeightSize * (heightCellIndex % Constant.HeightSize + 0.5F)));
                if (mModel.HeightGrid[heightPixelCenter].IsInWorld() == false)
                {
                    // no trees on that pixel: let trees die
                    Trees trees = treesInCell.Item1;
                    foreach (int treeIndex in treesInCell.Item2)
                    {
                        trees.Die(model, treeIndex);
                    }
                    continue;
                }

                int bits = 0;
                int index = -1;
                double r;
                PointF offset = ru.BoundingBox.TopLeft();
                Point offsetIdx = model.LightGrid.GetCellIndex(offset);
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
                            r = model.RandomGenerator.GetRandomDouble();
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
                    int pos = ru.GridIndex % Constant.LightSize != 0 ? EvenList[index] : UnevenList[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    Point lightCellIndex = new Point(offsetIdx.X + Constant.LightCellsPerHeightSize * (heightCellIndex / Constant.HeightSize) + pos / Constant.LightCellsPerHeightSize,
                                                     offsetIdx.Y + Constant.LightCellsPerHeightSize * (heightCellIndex % Constant.HeightSize) + pos % Constant.LightCellsPerHeightSize);
                    //Debug.WriteLine(tree_no++ + "to" + index);
                    treesInCell.Item1.LightCellPosition[treeIndex] = lightCellIndex;
                }
            }
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland.boku.ac.at/initialize+trees
        private void InitializeStand(Simulation.Model model, int standID)
        {
            // get a list of positions of all pixels that belong to our stand
            MapGrid standGrid = model.StandGrid;
            List<int> heightGridCellIndicesInStand = standGrid.GetGridIndices(standID);
            if (heightGridCellIndicesInStand.Count == 0)
            {
                Debug.WriteLine("stand " + standID + " not in project area. No init performed.");
                return;
            }
            // a multiHash holds a list for all trees.
            // key is the location of the 10x10m pixel
            Dictionary<Point, MutableTuple<Trees, List<int>>> treeIndicesByHeightCellIndex = new Dictionary<Point, MutableTuple<Trees, List<int>>>();
            List<HeightInitCell> heightCells = new List<HeightInitCell>(heightGridCellIndicesInStand.Count); // working list of all 10m pixels

            foreach (int cellIndex in heightGridCellIndicesInStand)
            {
                HeightInitCell heightCell = new HeightInitCell()
                {
                    CellPosition = standGrid.Grid.GetCellPosition(cellIndex), // index in the 10m grid
                };
                heightCell.ResourceUnit = model.GetResourceUnit(standGrid.Grid.GetCellCenterPosition(heightCell.CellPosition));
                if (this.InitHeightGrid != null)
                {
                    heightCell.MaxHeight = this.InitHeightGrid.Grid[heightCell.CellPosition];
                }
                heightCells.Add(heightCell);
            }
            double standAreaInResourceUnits = standGrid.GetArea(standID) / Constant.RUArea;

            if ((this.InitHeightGrid != null) && (this.mHeightGridResponse == null))
            {
                throw new NotSupportedException("Attempt to initialize from height grid but without response function.");
            }

            int key = 0;
            TreeSpecies lastLockedSpecies = null;
            int treeCount = 0;
            int total_tries = 0;
            int total_misses = 0;
            foreach (StandInitializationFileRow item in mInitItems)
            {
                if (item.Density > 1.0)
                {
                    // special case with single-species-area
                    if (treeCount == 0)
                    {
                        // randomize the pixels
                        for (int it = 0; it < heightCells.Count; ++it)
                        {
                            heightCells[it].BasalArea = model.RandomGenerator.GetRandomDouble();
                        }
                        heightCells.Sort(SortInitPixelLessThan);

                        for (int it = 0; it < heightCells.Count; ++it)
                        {
                            heightCells[it].BasalArea = 0.0;
                        }
                    }

                    if (item.Species != lastLockedSpecies)
                    {
                        lastLockedSpecies = item.Species;
                        heightCells.Sort(SortInitPixelUnlocked);
                    }
                }
                else
                {
                    heightCells.Sort(SortInitPixelLessThan);
                    lastLockedSpecies = null;
                }

                double rand_fraction = item.Density;
                int count = (int)(item.Count * standAreaInResourceUnits + 0.5); // round
                double init_max_height = item.DbhTo / 100.0 * item.HeightDiameterRatio;
                for (int i = 0; i < count; i++)
                {
                    bool found = false;
                    int tries = mHeightGridTries;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        double randomValue;
                        if (item.Density <= 1.0)
                        {
                            randomValue = mRandom.GetRandomValue(model);
                            if (item.Density < 0)
                            {
                                randomValue = 1.0 - randomValue;
                            }
                            randomValue = randomValue * rand_fraction + model.RandomGenerator.GetRandomDouble() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            randomValue = model.RandomGenerator.GetRandomDouble() * Math.Min(item.Density / 100.0, 1.0);
                        }
                        ++total_tries;

                        // key: rank of target pixel
                        key = Maths.Limit((int)(heightCells.Count * randomValue), 0, heightCells.Count - 1); // get from random number generator

                        if (InitHeightGrid != null)
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            double p_value = heightCells[key].MaxHeight > 0.0 ? mHeightGridResponse.Evaluate(model, init_max_height / heightCells[key].MaxHeight) : 0.0;
                            if (model.RandomGenerator.GetRandomDouble() < p_value)
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
                        ++total_misses;
                    }

                    // create a tree
                    ResourceUnit ru = heightCells[key].ResourceUnit;
                    Trees trees = ru.TreesBySpeciesID[item.Species.ID];
                    int treeIndex = ru.AddTree(model, item.Species.ID);
                    trees.Dbh[treeIndex] = (float)model.RandomGenerator.GetRandomDouble(item.DbhFrom, item.DbhTo);
                    trees.SetHeight(treeIndex, trees.Dbh[treeIndex] / 100.0F * (float)item.HeightDiameterRatio); // dbh from cm->m, *hd-ratio -> meter height
                    trees.Species = item.Species;
                    if (item.Age <= 0)
                    {
                        throw new NotSupportedException("Tree age is zero or less.");
                    }
                    else
                    {
                        trees.SetAge(treeIndex, item.Age, trees.Height[treeIndex]);
                    }
                    trees.Setup(model, treeIndex);
                    ++treeCount;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    if (treeIndicesByHeightCellIndex.TryGetValue(heightCells[key].CellPosition, out MutableTuple<Trees, List<int>> treesInCell) == false)
                    {
                        treesInCell = new MutableTuple<Trees, List<int>>(trees, new List<int>());
                        treeIndicesByHeightCellIndex.Add(heightCells[key].CellPosition, treesInCell);
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
                        heightCells.Sort(SortInitPixelLessThan);
                    }
                }
            }
            if (total_misses > 0 || total_tries > treeCount)
            {
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("init for stand " + standID + " treecount: " + treeCount + ", tries: " + total_tries + ", misses: " + total_misses + ", %miss: " + Math.Round(total_misses * 100 / (double)treeCount));
                }
            }

            foreach (HeightInitCell heightCell in heightCells)
            {
                MutableTuple<Trees, List<int>> treesInCell = treeIndicesByHeightCellIndex[heightCell.CellPosition];
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
                            double random = model.RandomGenerator.GetRandomDouble();
                            index = Maths.Limit((int)(25 * random * random), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Maths.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("executeiLandInit: found no free bit.");
                        }
                        Maths.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = heightCell.ResourceUnit.GridIndex % Constant.LightSize != 0 ? StandReader.EvenList[index] : StandReader.UnevenList[index];
                    Point lightCellIndex = new Point(heightCell.CellPosition.X * Constant.LightCellsPerHeightSize + pos / Constant.LightCellsPerHeightSize, // convert to LIF index
                                                     heightCell.CellPosition.Y * Constant.LightCellsPerHeightSize + pos % Constant.LightCellsPerHeightSize);

                    Trees trees = treesInCell.Item1;
                    trees.LightCellPosition[treeIndex] = lightCellIndex;
                    // test if tree position is valid..
                    if (model.LightGrid.Contains(lightCellIndex) == false)
                    {
                        Debug.WriteLine("Standloader: invalid position!");
                    }
                }
            }
            if (model.Files.LogDebug())
            {
                Debug.WriteLine("init for stand " + standID + " with area" + standGrid.GetArea(standID) + " m2, count of 10m pixels: " + heightGridCellIndicesInStand.Count + "initialized trees: " + treeCount);
            }
        }

        /// a (hacky) way of adding saplings of a certain age to a stand defined by 'stand_id'.
        //public int LoadSaplings(Simulation.Model model, string content, int standID)
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
        //                    sc.AddSaplingIfSlotFree((float)height, (int)age, species.Index);
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

        private int LoadSaplingsLif(Simulation.Model model, int standID, CsvFile init, int startRowIndex, int endRowIndex)
        {
            MapGrid standGrid = model.StandGrid; // default
            if (standGrid.IsValid(standID) == false)
            {
                return 0;
            }

            List<int> standGridIndices = standGrid.GetGridIndices(standID); // list of 10x10m pixels
            if (standGridIndices.Count == 0)
            {
                Debug.WriteLine("stand " + standID + " not in project area. No init performed.");
                return 0;
            }

            // prepare space for LIF-pointers (2m Pixel)
            List<KeyValuePair<int, float>> lightCellIndexAndValues = new List<KeyValuePair<int, float>>(standGridIndices.Count * Constant.LightCellsPerHeightSize * Constant.LightCellsPerHeightSize);
            Grid<float> lightGrid = model.LightGrid;
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
                        KeyValuePair<int, float> indexAndValue = new KeyValuePair<int, float>(modelIndex, lightGrid[modelIndex]);
                        lightCellIndexAndValues.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lightCellIndexAndValues.Sort(CompareLifValue); // higher: highest values first

            double standAreaInHa = standGrid.GetArea(standID) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            int speciesIndex = init.GetColumnIndex("species");
            int countIndex = init.GetColumnIndex("count");
            int heightIndex = init.GetColumnIndex("height");
            int heightFromIndex = init.GetColumnIndex("height_from");
            int heightToIndex = init.GetColumnIndex("height_to");
            int ageIndex = init.GetColumnIndex("age");
            int topAgeIndex = init.GetColumnIndex("age4m");
            int minLifIndex = init.GetColumnIndex("min_lif");
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
                int cellsWithSaplings = (int)(Double.Parse(init.GetValue(countIndex, row)) * standAreaInHa); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                // TODO: constants for defaults, 5 cm is also hard coded elsewhere
                float height = heightIndex == -1 ? Constant.Sapling.MinimumHeight : Single.Parse(init.GetValue(heightIndex, row));
                int age = ageIndex == -1 ? 1 : Int32.Parse(init.GetValue(ageIndex, row));
                float age4m = topAgeIndex == -1 ? 10.0F : Single.Parse(init.GetValue(topAgeIndex, row));
                double minHeight = heightFromIndex == -1 ? -1.0 : Double.Parse(init.GetValue(heightFromIndex, row));
                double maxHeight = heightToIndex == -1 ? -1.0 : Double.Parse(init.GetValue(heightToIndex, row));
                double minLightIntensity = minLifIndex == -1 ? 1.0 : Double.Parse(init.GetValue(minLifIndex, row));
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

                double hits = 0.0;
                while (hits < cellsWithSaplings)
                {
                    int randomIndex = model.RandomGenerator.GetRandomInteger(0, minLightIndex);
                    if (heightFromIndex != -1)
                    {
                        height = (float)model.RandomGenerator.GetRandomDouble(minHeight, maxHeight);
                        if (age <= 1)
                        {
                            age = Math.Max((int)MathF.Round(height / Constant.Sapling.MaximumHeight * age4m), 1); // assume a linear relationship between height and age
                        }
                    }
                    Point lightCellIndex = lightGrid.GetCellPosition(lightCellIndexAndValues[randomIndex].Key);
                    SaplingCell saplingCell = model.Saplings.GetCell(model, lightCellIndex, true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        TreeSpecies species = ru.TreeSpeciesSet.GetSpecies(init.GetValue(speciesIndex, row));
                        Sapling sapling = saplingCell.AddSaplingIfSlotFree(height, age, species.Index);
                        if (sapling != null)
                        {
                            hits += Math.Max(1.0, species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(sapling.Height));
                        }
                        else
                        {
                            ++hits;
                        }
                    }
                    else
                    {
                        ++hits; // avoid an infinite loop
                    }
                }

                total += cellsWithSaplings;
            }

            // initialize grass cover
            if (init.GetColumnIndex("grass_cover") > -1)
            {
                int grass_cover_value = Int32.Parse(init.GetValue("grass_cover", startRowIndex));
                if (grass_cover_value < 0 || grass_cover_value > 100)
                {
                    throw new NotSupportedException(String.Format("The grass cover percentage (column 'grass_cover') for stand '{0}' is '{1}', which is invalid (expected: 0-100)", standID, grass_cover_value));
                }
                model.GrassCover.SetInitialValues(model, lightCellIndexAndValues, grass_cover_value);
            }

            return total;
        }

        private class StandInitializationFileRow
        {
            public int Age { get; set; }
            public double Count { get; set; }
            public double Density { get; set; }
            public double DbhFrom { get; set; }
            public double DbhTo { get; set; }
            public double HeightDiameterRatio { get; set; }
            public TreeSpecies Species { get; set; }
        }
    }
}
