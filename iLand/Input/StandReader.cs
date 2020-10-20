using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using iLand.World;
using Microsoft.Collections.Extensions;
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
        private List<InitFileItem> mInitItems;
        private readonly Dictionary<int, List<InitFileItem>> mStandInitItems;
        private Expression mHeightGridResponse; // response function to calculate fitting of pixels with pre-determined height
        private int mHeightGridTries; // maximum number of tries to land at pixel with fitting height

        /// define a stand grid externally
        public MapGrid CurrentMap { get; set; }
        /// set a constraining height grid (10m resolution)
        public MapGrid InitHeightGrid { get; set; } // grid with tree heights

        public StandReader(Simulation.Model model)
        {
            InitHeightGrid = new MapGrid();
            mStandInitItems = new Dictionary<int, List<InitFileItem>>();
            mModel = model;
            mRandom = null;
            CurrentMap = null;
            InitHeightGrid = null;
            mHeightGridResponse = null;
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
        public void ProcessInit(Simulation.Model model)
        {
            string initializationMode = model.Project.Model.Initialization.Mode;
            string type = model.Project.Model.Initialization.Type;
            string fileName = model.Project.Model.Initialization.File;

            bool heightGridEnabled = model.Project.Model.Initialization.HeightGrid.Enabled;
            mHeightGridTries = model.Project.Model.Initialization.HeightGrid.MaxTries;
            if (heightGridEnabled)
            {
                string initHeightGridFile = model.Files.GetPath(model.Project.Model.Initialization.HeightGrid.FileName);
                Debug.WriteLine("initialization: using predefined tree heights map " + initHeightGridFile);

                MapGrid p = new MapGrid(model, initHeightGridFile, false);
                if (!p.IsValid())
                {
                    throw new NotSupportedException(String.Format("Error when loading grid with tree heights for stand initalization: file {0} not found or not valid.", initHeightGridFile));
                }
                InitHeightGrid = p;

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
                    throw new NotSupportedException("Error initialization: 'mode' is 'single' but more than one resource unit is simulated (consider using another 'mode').");
                }

                LoadInitFile(fileName, type, 0, model, model.FirstResourceUnit()); // this is the first resource unit
                EvaluateDebugTrees(model);
                return;
            }

            // call a single tree init for each resource unit
            if (initializationMode == "unit")
            {
                foreach (ResourceUnit ru in model.ResourceUnits)
                {
                    // set environment
                    model.Environment.SetPosition(ru.BoundingBox.Center(), model);
                    // BUGBUG: SetPosition() doesn't update type and file keys, inherited from C++
                    //type = xml.GetStringFromXml("type", "");
                    //fileName = xml.GetStringFromXml("file", "");
                    if (String.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }
                    LoadInitFile(fileName, type, 0, model, ru);
                    if (model.Files.LogDebug())
                    {
                        Debug.WriteLine("loaded " + fileName + " on " + ru.BoundingBox + ", " + ru.Trees.Count + "trees.");
                    }
                }
                EvaluateDebugTrees(model);
                return;
            }

            // map-modus: load a init file for each "polygon" in the standgrid
            if (initializationMode == "map")
            {
                if (model.StandGrid == null || model.StandGrid.IsValid() == false)
                {
                    throw new NotSupportedException("Stand-Initialization: model.initialization.mode is 'map' but there is no valid stand grid defined (model.world.standGrid)");
                }
                string mapFileName = model.Files.GetPath(model.Project.Model.Initialization.MapFileName);

                CsvFile map_file = new CsvFile(mapFileName);
                if (map_file.RowCount == 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} is empty or missing!", mapFileName));
                }
                int ikey = map_file.GetColumnIndex("id");
                int ivalue = map_file.GetColumnIndex("filename");
                if (ikey < 0 || ivalue < 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} does not contain the mandatory columns 'id' and 'filename'!", mapFileName));
                }
                string file_name;
                for (int i = 0; i < map_file.RowCount; i++)
                {
                    int key = Int32.Parse(map_file.GetValue(i, ikey));
                    if (key > 0)
                    {
                        file_name = map_file.GetValue(i, ivalue);
                        if (model.Files.LogDebug())
                        {
                            Debug.WriteLine("loading " + file_name + " for grid id " + key);
                        }
                        if (String.IsNullOrEmpty(file_name) == false)
                        {
                            LoadInitFile(file_name, type, key, null);
                        }
                    }
                }
                InitHeightGrid = null;
                EvaluateDebugTrees(model);
                return;
            }

            // standgrid mode: load one large init file
            if (initializationMode == "standgrid")
            {
                fileName = model.Files.GetPath(fileName, "init");
                if (!File.Exists(fileName))
                {
                    throw new NotSupportedException(String.Format("load-ini-file: file '{0}' does not exist.", fileName));
                }
                string content = File.ReadAllText(fileName);
                // this processes the init file (also does the checking) and
                // stores in a Dictionary datastrucutre
                ParseInitFile(content, fileName);

                // setup the random distribution
                string density_func = model.Project.Model.Initialization.RandomFunction;
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("density function: " + density_func);
                }
                if (mRandom == null || (mRandom.DensityFunction != density_func))
                {
                    mRandom = new RandomCustomPdf(model, density_func);
                    if (model.Files.LogDebug())
                    {
                        Debug.WriteLine("new probabilty density function: " + density_func);
                    }
                }

                if (mStandInitItems.Count == 0)
                {
                    Debug.WriteLine("Initialize trees ('standgrid'-mode): no items to process (empty landscape).");
                    return;
                    //throw new NotSupportedException("processInit: 'mode' is 'standgrid' but the init file is either empty or contains no 'stand_id'-column.");
                }
                foreach (KeyValuePair<int, List<InitFileItem>> it in mStandInitItems)
                {
                    mInitItems = it.Value; // copy the items...
                    InitializeStand(model, it.Key);
                }
                Debug.WriteLine("finished setup of trees.");
                EvaluateDebugTrees(model);
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
            throw new NotSupportedException("processInit: invalid initalization.mode!");
        }

        public void ProcessAfterInit(Simulation.Model model)
        {
            string mode = model.Project.Model.Initialization.Mode;
            if (mode == "standgrid")
            {
                // load a file with saplings per polygon
                string filename = model.Project.Model.Initialization.SaplingFile;
                if (String.IsNullOrEmpty(filename))
                {
                    return;
                }
                filename = model.Files.GetPath(filename, "init");
                if (File.Exists(filename) == false)
                {
                    throw new NotSupportedException(String.Format("load-sapling-ini-file: file '{0}' does not exist.", filename));
                }
                CsvFile init_file = new CsvFile(filename);
                int istandid = init_file.GetColumnIndex("stand_id");
                if (istandid == -1)
                {
                    throw new NotSupportedException("Sapling-Init: the init file contains no 'stand_id' column (required in 'standgrid' mode).");
                }

                int stand_id = -99999;
                int ilow = -1;
                int total = 0;
                for (int i = 0; i < init_file.RowCount; ++i)
                {
                    int row_stand = Int32.Parse(init_file.GetValue(i, istandid));
                    if (row_stand != stand_id)
                    {
                        if (stand_id >= 0)
                        {
                            // process stand
                            int ihigh = i - 1; // up to the last
                            total += LoadSaplingsLif(model, stand_id, init_file, ilow, ihigh);
                        }
                        ilow = i; // mark beginning of new stand
                        stand_id = row_stand;
                    }
                }
                if (stand_id >= 0)
                {
                    total += LoadSaplingsLif(model, stand_id, init_file, ilow, init_file.RowCount - 1); // the last stand
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
                if (dbg_str == "debugstamp")
                {
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees...");
                    // try to force an error if a stamp is invalid
                    AllTreeEnumerator treeIterator = new AllTreeEnumerator(model);
                    double total_offset = 0.0;
                    for (Tree tree = treeIterator.MoveNext(); tree != null; tree = treeIterator.MoveNext())
                    {
                        total_offset += tree.Stamp.CenterCellPosition;
                        if (model.LightGrid.Contains(tree.LightCellPosition) == false)
                        {
                            Debug.WriteLine("evaluateDebugTrees: debugstamp: invalid position found!");
                        }
                    }
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees finished...");
                    return;
                }
                TreeWrapper tw = new TreeWrapper();
                Expression dexp = new Expression(dbg_str, tw); // load expression dbg_str and enable external model variables
                AllTreeEnumerator at = new AllTreeEnumerator(model);
                for (Tree t = at.MoveNext(); t != null; t = at.MoveNext())
                {
                    tw.Tree = t;
                    double result = dexp.Execute(model);
                    if (result != 0.0)
                    {
                        t.EnableDebugging();
                        counter++;
                    }
                }
                Debug.WriteLine("evaluateDebugTrees: enabled debugging for " + counter + " trees.");
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
            if (ru == null)
            {
                ru = mModel.FirstResourceUnit();
            }
            Debug.Assert(ru != null);

            PointF ruOffset = ru.BoundingBox.TopLeft();
            SpeciesSet speciesSet = ru.SpeciesSet; // of default RU

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
            int ageColumn = infile.GetColumnIndex("age");
            if (xColumn == -1 || yColumn == -1 || dbhColumn == -1 || speciesColumn == -1 || heightColumn == -1)
            {
                throw new NotSupportedException(String.Format("Initfile {0} is not valid! Required columns are: x, y, bhdfrom or dbh, species, and treeheight or height.", fileName));
            }

            int treCount = 0;
            for (int rowIndex = 1; rowIndex < infile.RowCount; rowIndex++)
            {
                double dbh = Double.Parse(infile.GetValue(rowIndex, dbhColumn));
                //if (dbh<5.)
                //    continue;
                PointF physicalPosition = new PointF();
                if (xColumn >= 0 && yColumn >= 0)
                {
                    physicalPosition.X = Single.Parse(infile.GetValue(rowIndex, xColumn)) + ruOffset.X;
                    physicalPosition.Y = Single.Parse(infile.GetValue(rowIndex, yColumn)) + ruOffset.Y;
                }
                // position valid?
                if (!mModel.HeightGrid[physicalPosition].IsInWorld())
                {
                    continue;
                }
                Tree tree = ru.AddNewTree(model);
                tree.SetLightCellIndex(physicalPosition);
                if (idColumn >= 0)
                {
                    tree.ID = Int32.Parse(infile.GetValue(rowIndex, idColumn));
                }

                tree.Dbh = (float)dbh;
                tree.SetHeight(Single.Parse(infile.GetValue(rowIndex, heightColumn)) / (float)heightConversionFactor); // convert from Picus-cm to m if necessary

                string speciesID = infile.GetValue(rowIndex, speciesColumn);
                if (Int32.TryParse(speciesID, out int picusID))
                {
                    int idx = PicusSpeciesIDs.IndexOf(picusID);
                    if (idx == -1)
                    {
                        throw new NotSupportedException(String.Format("Loading init-file: invalid Picus-species-id. Species: {0}", picusID));
                    }
                    speciesID = iLandSpeciesIDs[idx];
                }
                Species species = speciesSet.GetSpecies(speciesID);
                if (ru == null || species == null)
                {
                    throw new NotSupportedException(String.Format("Loading init-file: either resource unit or species invalid. Species: {0}", speciesID));
                }
                tree.Species = species;

                bool ageParsed = true;
                if (ageColumn >= 0)
                {
                    // BUGBUG should probably throw if age parsing fails
                    ageParsed = Int32.TryParse(infile.GetValue(rowIndex, ageColumn), out int age);
                    tree.SetAge(age, tree.Height); // this is a *real* age
                }
                if (ageColumn < 0 || !ageParsed || tree.Age == 0)
                {
                    tree.SetAge(0, tree.Height); // no real tree age available
                }

                tree.RU = ru;
                tree.Setup(model);
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
                InitializeResourceUnit(ru, model);
                ru.CleanTreeList();
            }
            return total_count;
        }

        public int ParseInitFile(string content, string fileName, ResourceUnit ru = null)
        {
            if (ru != null)
            {
                ru = mModel.FirstResourceUnit();
            }
            Debug.Assert(ru != null);
            SpeciesSet speciesSet = ru.SpeciesSet; // of default RU
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
                InitFileItem initItem = new InitFileItem()
                {
                    Count = Double.Parse(infile.GetValue(row, countIndex)),
                    DbhFrom = Double.Parse(infile.GetValue(row, dbhFromIndex)),
                    DbhTo = Double.Parse(infile.GetValue(row, dbhToIndex)),
                    HD = Double.Parse(infile.GetValue(row, hdIndex))
                };
                if (initItem.HD == 0.0 || initItem.DbhFrom / 100.0 * initItem.HD < 4.0)
                {
                    Trace.TraceWarning(String.Format("File '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, initItem.HD, initItem.DbhFrom));
                }
                // TODO: DbhFrom < DbhTo?
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                totalCount += (int)initItem.Count;

                bool setAgeToZero = true;
                if (ageIndex >= 0)
                {
                    setAgeToZero = Int32.TryParse(infile.GetValue(row, ageIndex), out int age);
                    initItem.Age = age;
                }
                if (ageIndex < 0 || setAgeToZero == false)
                {
                    initItem.Age = 0;
                }

                initItem.Species = speciesSet.GetSpecies(infile.GetValue(row, speciesIndex));
                if (densityIndex >= 0)
                {
                    initItem.Density = Double.Parse(infile.GetValue(row, densityIndex));
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
                                                                  infile.GetValue(row, speciesIndex), fileName, row));
                }
                if (standIDindex >= 0)
                {
                    int standid = Int32.Parse(infile.GetValue(row, standIDindex));
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

        public class SInitPixel
        {
            public double BasalArea { get; set; } // accumulated basal area
            public Point CellPosition { get; set; } // location of the pixel
            public double MaxHeight { get; set; } // predefined maximum height at given pixel (if available from LIDAR or so)
            public ResourceUnit ResourceUnit { get; set; } // pointer to the resource unit the pixel belongs to
            public bool IsSingleSpecies { get; set; } // pixel is dedicated to a single species

            public SInitPixel()
            {
                this.BasalArea = 0.0;
                this.IsSingleSpecies = false;
                this.MaxHeight = -1.0;
                this.ResourceUnit = null;
            }
        };

        private int SortInitPixelLessThan(SInitPixel s1, SInitPixel s2)
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

        private int SortInitPixelUnlocked(SInitPixel s1, SInitPixel s2)
        {
            if (!s1.IsSingleSpecies && s2.IsSingleSpecies)
            {
                return -1;
            }
            return 0;
        }

        private void InitializeResourceUnit(ResourceUnit ru, Simulation.Model model)
        {
            PointF offset = ru.BoundingBox.TopLeft();
            Point offsetIdx = model.LightGrid.IndexAt(offset);

            // a multimap holds a list for all trees.
            // key is the index of a 10x10m pixel within the resource unit
            MultiValueDictionary<int, int> tree_map = new MultiValueDictionary<int, int>();
            //Dictionary<int,SInitPixel> tcount;

            List<MutableTuple<int, double>> tcount = new List<MutableTuple<int, double>>(); // counts
            for (int i = 0; i < 100; i++)
            {
                tcount.Add(new MutableTuple<int, double>(i, 0.0));
            }

            int key;
            double rand_val, rand_fraction;
            int total_count = 0;
            foreach (InitFileItem initItem in mInitItems)
            {
                rand_fraction = Math.Abs(initItem.Density);
                for (int i = 0; i < initItem.Count; i++)
                {
                    // create trees
                    Tree tree = ru.AddNewTree(model);
                    tree.Dbh = (float)model.RandomGenerator.Random(initItem.DbhFrom, initItem.DbhTo);
                    tree.SetHeight(tree.Dbh / 100.0F * (float)initItem.HD); // dbh from cm->m, *hd-ratio -> meter height
                    tree.Species = initItem.Species;
                    if (initItem.Age <= 0)
                    {
                        tree.SetAge(0, tree.Height);
                    }
                    else
                    {
                        tree.SetAge(initItem.Age, tree.Height);
                    }
                    tree.RU = ru;
                    tree.Setup(model);
                    total_count++;

                    // calculate random value. "density" is from 1..-1.
                    rand_val = mRandom.Get(model);
                    if (initItem.Density < 0)
                    {
                        rand_val = 1.0 - rand_val;
                    }
                    rand_val = rand_val * rand_fraction + model.RandomGenerator.Random() * (1.0 - rand_fraction);

                    // key: rank of target pixel
                    // first: index of target pixel
                    // second: sum of target pixel
                    key = Global.Limit((int)(100.0 * rand_val), 0, 99); // get from random number generator
                    tree_map.Add(tcount[key].Item1, tree.ID); // store tree in map
                    MutableTuple<int, double> ruBA = tcount[key];
                    ruBA.Item2 += tree.BasalArea(); // aggregate the basal area for each 10m pixel
                    if ((total_count < 20 && i % 2 == 0)
                        || (total_count < 100 && i % 10 == 0)
                        || (i % 30 == 0))
                    {
                        tcount.Sort(SortPairLessThan);
                    }
                }
                tcount.Sort(SortPairLessThan);
            }

            int bits, index, pos;
            Point tree_pos;
            for (int i = 0; i < 100; i++)
            {
                List<int> trees = tree_map[i].ToList();
                int c = trees.Count;
                PointF pixel_center = ru.BoundingBox.TopLeft().Add(new PointF((i / 10) * 10.0F + 5.0F, (i % 10) * 10.0F + 5.0F));
                if (!mModel.HeightGrid[pixel_center].IsInWorld())
                {
                    // no trees on that pixel: let trees die
                    foreach (int tree_idx in trees)
                    {
                        ru.Trees[tree_idx].Die(model);
                    }
                    continue;
                }

                bits = 0;
                index = -1;
                double r;
                foreach (int tree_idx in trees)
                {
                    if (c > 18)
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
                            r = model.RandomGenerator.Random();
                            index = Global.Limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Global.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("executeiLandInit: found no free bit.");
                        }
                        Global.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    pos = ru.Index % 2 != 0 ? EvenList[index] : UnevenList[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    tree_pos = new Point(offsetIdx.X + 5 * (i / 10) + pos / 5,
                                         offsetIdx.Y + 5 * (i % 10) + pos % 5);
                    //Debug.WriteLine(tree_no++ + "to" + index);
                    ru.Trees[tree_idx].LightCellPosition = tree_pos;
                }
            }
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland.boku.ac.at/initialize+trees
        private void InitializeStand(Simulation.Model model, int standID)
        {
            MapGrid grid = model.StandGrid;
            if (CurrentMap != null)
            {
                grid = CurrentMap;
            }

            // get a list of positions of all pixels that belong to our stand
            List<int> indices = grid.GridIndices(standID);
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + standID + " not in project area. No init performed.");
                return;
            }
            // a multiHash holds a list for all trees.
            // key is the location of the 10x10m pixel
            MultiValueDictionary<Point, int> tree_map = new MultiValueDictionary<Point, int>();
            List<SInitPixel> pixel_list = new List<SInitPixel>(indices.Count); // working list of all 10m pixels

            foreach (int i in indices)
            {
                SInitPixel p = new SInitPixel()
                {
                    CellPosition = grid.Grid.IndexOf(i), // index in the 10m grid
                };
                p.ResourceUnit = model.GetResourceUnit(grid.Grid.GetCellCenterPoint(p.CellPosition));
                if (InitHeightGrid != null)
                {
                    p.MaxHeight = InitHeightGrid.Grid[p.CellPosition];
                }
                pixel_list.Add(p);
            }
            double area_factor = grid.Area(standID) / Constant.RUArea;

            int key = 0;
            double rand_val, rand_fraction;
            int total_count = 0;
            int total_tries = 0;
            int total_misses = 0;
            if (InitHeightGrid != null && mHeightGridResponse == null)
            {
                throw new NotSupportedException("executeiLandInitStand: trying to initialize with height grid but without response function.");
            }

            Species last_locked_species = null;
            foreach (InitFileItem item in mInitItems)
            {
                if (item.Density > 1.0)
                {
                    // special case with single-species-area
                    if (total_count == 0)
                    {
                        // randomize the pixels
                        for (int it = 0; it < pixel_list.Count; ++it)
                        {
                            pixel_list[it].BasalArea = model.RandomGenerator.Random();
                        }
                        pixel_list.Sort(SortInitPixelLessThan);

                        for (int it = 0; it < pixel_list.Count; ++it)
                        {
                            pixel_list[it].BasalArea = 0.0;
                        }
                    }

                    if (item.Species != last_locked_species)
                    {
                        last_locked_species = item.Species;
                        pixel_list.Sort(SortInitPixelUnlocked);
                    }
                }
                else
                {
                    pixel_list.Sort(SortInitPixelLessThan);
                    last_locked_species = null;
                }
                rand_fraction = item.Density;
                int count = (int)(item.Count * area_factor + 0.5); // round
                double init_max_height = item.DbhTo / 100.0 * item.HD;
                for (int i = 0; i < count; i++)
                {
                    bool found = false;
                    int tries = mHeightGridTries;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        if (item.Density <= 1.0)
                        {
                            rand_val = mRandom.Get(model);
                            if (item.Density < 0)
                            {
                                rand_val = 1.0 - rand_val;
                            }
                            rand_val = rand_val * rand_fraction + model.RandomGenerator.Random() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            rand_val = model.RandomGenerator.Random() * Math.Min(item.Density / 100.0, 1.0);
                        }
                        ++total_tries;

                        // key: rank of target pixel
                        key = Global.Limit((int)(pixel_list.Count * rand_val), 0, pixel_list.Count - 1); // get from random number generator

                        if (InitHeightGrid != null)
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            double p_value = pixel_list[key].MaxHeight > 0.0 ? mHeightGridResponse.Evaluate(model, init_max_height / pixel_list[key].MaxHeight) : 0.0;
                            if (model.RandomGenerator.Random() < p_value)
                            {
                                found = true;
                            }
                        }
                        else
                        {
                            found = true;
                        }
                        if (last_locked_species != null && pixel_list[key].IsSingleSpecies)
                        {
                            found = false;
                        }
                    }
                    if (tries < 0)
                    {
                        ++total_misses;
                    }

                    // create a tree
                    ResourceUnit ru = pixel_list[key].ResourceUnit;
                    Tree tree = ru.AddNewTree(model);
                    tree.Dbh = (float)model.RandomGenerator.Random(item.DbhFrom, item.DbhTo);
                    tree.SetHeight((float)(tree.Dbh / 100.0 * item.HD)); // dbh from cm->m, *hd-ratio -> meter height
                    tree.Species = item.Species;
                    if (item.Age <= 0)
                    {
                        tree.SetAge(0, tree.Height);
                    }
                    else
                    {
                        tree.SetAge(item.Age, tree.Height);
                    }
                    tree.RU = ru;
                    tree.Setup(model);
                    total_count++;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    tree_map.Add(pixel_list[key].CellPosition, tree.ID);
                    pixel_list[key].BasalArea += tree.BasalArea(); // aggregate the basal area for each 10m pixel
                    if (last_locked_species != null)
                    {
                        pixel_list[key].IsSingleSpecies = true;
                    }

                    // resort list
                    if (last_locked_species == null && ((total_count < 20 && i % 2 == 0) || (total_count < 100 && i % 10 == 0) || (i % 30 == 0)))
                    {
                        pixel_list.Sort(SortInitPixelLessThan);
                    }
                }
            }
            if (total_misses > 0 || total_tries > total_count)
            {
                if (model.Files.LogDebug())
                {
                    Debug.WriteLine("init for stand " + standID + " treecount: " + total_count + ", tries: " + total_tries + ", misses: " + total_misses + ", %miss: " + Math.Round(total_misses * 100 / (double)total_count));
                }
            }

            foreach (SInitPixel p in pixel_list)
            {
                List<int> trees = tree_map[p.CellPosition].ToList();
                int c = trees.Count;
                int bits = 0;
                int index = -1;
                double r;
                foreach (int tree_idx in trees)
                {
                    if (c > 18)
                    {
                        index = (index + 1) % 25;
                    }
                    else
                    {
                        int stop = 1000;
                        index = 0;
                        do
                        {
                            // search a random position
                            r = model.RandomGenerator.Random();
                            index = Global.Limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Global.IsBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("executeiLandInit: found no free bit.");
                        }
                        Global.SetBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = p.ResourceUnit.Index % 2 != 0 ? EvenList[index] : UnevenList[index];
                    Point tree_pos = new Point(p.CellPosition.X * Constant.LightPerHeightSize + pos / Constant.LightPerHeightSize, // convert to LIF index
                                               p.CellPosition.Y * Constant.LightPerHeightSize + pos % Constant.LightPerHeightSize);

                    p.ResourceUnit.Trees[tree_idx].LightCellPosition = tree_pos;
                    // test if tree position is valid..
                    if (model.LightGrid.Contains(tree_pos) == false)
                    {
                        Debug.WriteLine("Standloader: invalid position!");
                    }
                }
            }
            if (model.Files.LogDebug())
            {
                Debug.WriteLine("init for stand " + standID + " with area" + grid.Area(standID) + " m2, count of 10m pixels: " + indices.Count + "initialized trees: " + total_count);
            }
        }

        /// a (hacky) way of adding saplings of a certain age to a stand defined by 'stand_id'.
        public int LoadSaplings(Simulation.Model model, string content, int standId)
        {
            // Q_UNUSED(fileName);
            MapGrid stand_grid;
            if (CurrentMap != null)
            {
                stand_grid = CurrentMap; // if set
            }
            else
            {
                stand_grid = model.StandGrid; // default
            }

            List<int> indices = stand_grid.GridIndices(standId); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + standId + " not in project area. No init performed.");
                return -1;
            }
            double area_factor = stand_grid.Area(standId) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            CsvFile init = new CsvFile();
            init.LoadFromString(content);
            int ispecies = init.GetColumnIndex("species");
            int icount = init.GetColumnIndex("count");
            int iheight = init.GetColumnIndex("height");
            int iage = init.GetColumnIndex("age");
            if (ispecies == -1 || icount == -1)
            {
                throw new NotSupportedException("Error while loading saplings: columns 'species' or 'count' are missing!!");
            }

            SpeciesSet set = model.FirstResourceUnit().SpeciesSet;
            double height, age;
            int total = 0;
            for (int row = 0; row < init.RowCount; ++row)
            {
                int pxcount = (int)Math.Round(Double.Parse(init.GetValue(row, icount)) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.GetSpecies(init.GetValue(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.GetValue(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.GetValue(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.GetValue(row, iage));

                int misses = 0;
                int hits = 0;
                while (hits < pxcount)
                {
                    // sapling location
                    int rnd_index = model.RandomGenerator.Random(0, indices.Count);
                    Point offset = stand_grid.Grid.IndexOf(indices[rnd_index]);
                    offset.X *= Constant.LightPerHeightSize; // index of 10m patch -> to lif pixel coordinates
                    offset.Y *= Constant.LightPerHeightSize;
                    int in_p = model.RandomGenerator.Random(0, Constant.LightPerHeightSize * Constant.LightPerHeightSize); // index of lif-pixel
                    offset.X += in_p / Constant.LightPerHeightSize;
                    offset.Y += in_p % Constant.LightPerHeightSize;

                    ResourceUnit ru = null;
                    SaplingCell sc = model.Saplings.Cell(offset, model, true, ref ru);
                    if (sc != null && sc.MaxHeight() > height)
                    {
                        //if (!ru || ru.saplingHeightForInit(offset) > height) {
                        misses++;
                    }
                    else
                    {
                        // ok
                        hits++;
                        if (sc != null)
                        {
                            sc.AddSapling((float)height, (int)age, species.Index);
                        }
                        //ru.resourceUnitSpecies(species).changeSapling().addSapling(offset, height, age);
                    }
                    if (misses > 3 * pxcount)
                    {
                        Debug.WriteLine("tried to add " + pxcount + " saplings at stand " + standId + " but failed in finding enough free positions. Added " + hits + " and stopped.");
                        break;
                    }
                }
                total += hits;

            }
            return total;
        }

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

        public int LoadSaplingsLif(Simulation.Model model, int standID, CsvFile init, int low_index, int high_index)
        {
            MapGrid standGrid;
            if (CurrentMap != null)
            {
                standGrid = CurrentMap; // if set
            }
            else
            {
                standGrid = model.StandGrid; // default
            }

            if (!standGrid.IsValid(standID))
            {
                return 0;
            }

            List<int> indices = standGrid.GridIndices(standID); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + standID + " not in project area. No init performed.");
                return 0;
            }
            // prepare space for LIF-pointers (2m Pixel)
            List<KeyValuePair<int, float>> lif_ptrs = new List<KeyValuePair<int, float>>(indices.Count * Constant.LightPerHeightSize * Constant.LightPerHeightSize);
            Grid<float> lightGrid = model.LightGrid;
            for (int l = 0; l < indices.Count; ++l)
            {
                Point offset = standGrid.Grid.IndexOf(indices[l]);
                offset.X *= Constant.LightPerHeightSize; // index of 10m patch -> to lif pixel coordinates
                offset.Y *= Constant.LightPerHeightSize;
                for (int y = 0; y < Constant.LightPerHeightSize; ++y)
                {
                    for (int x = 0; x < Constant.LightPerHeightSize; ++x)
                    {
                        int modelIndex = lightGrid.IndexOf(offset.X + x, offset.Y + y);
                        KeyValuePair<int, float> indexAndValue = new KeyValuePair<int, float>(modelIndex, lightGrid[modelIndex]);
                        lif_ptrs.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lif_ptrs.Sort(CompareLifValue); // higher: highest values first

            double area_factor = standGrid.Area(standID) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            int ispecies = init.GetColumnIndex("species");
            int icount = init.GetColumnIndex("count");
            int iheight = init.GetColumnIndex("height");
            int iheightfrom = init.GetColumnIndex("height_from");
            int iheightto = init.GetColumnIndex("height_to");
            int iage = init.GetColumnIndex("age");
            int itopage = init.GetColumnIndex("age4m");
            int iminlif = init.GetColumnIndex("min_lif");
            if ((iheightfrom == -1) ^ (iheightto == -1))
            {
                throw new NotSupportedException("Error while loading saplings: height not correctly provided. Use either 'height' or 'height_from' and 'height_to'.");
            }
            if (ispecies == -1 || icount == -1)
            {
                throw new NotSupportedException("Error while loading saplings: columns 'species' or 'count' are missing!!");
            }

            SpeciesSet set = model.FirstResourceUnit().SpeciesSet;
            double height, age;
            int total = 0;
            for (int row = low_index; row <= high_index; ++row)
            {
                int pxcount = (int)(Double.Parse(init.GetValue(row, icount)) * area_factor); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.GetSpecies(init.GetValue(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.GetValue(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.GetValue(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.GetValue(row, iage));
                double age4m = itopage == -1 ? 10 : Double.Parse(init.GetValue(row, itopage));
                double height_from = iheightfrom == -1 ? -1.0 : Double.Parse(init.GetValue(row, iheightfrom));
                double height_to = iheightto == -1 ? -1.0 : Double.Parse(init.GetValue(row, iheightto));
                double min_lif = iminlif == -1 ? 1.0 : Double.Parse(init.GetValue(row, iminlif));
                // find LIF-level in the pixels
                int min_lif_index = 0;
                if (min_lif < 1.0)
                {
                    for (int it = 0; it < lif_ptrs.Count; ++it, ++min_lif_index)
                    {
                        if (lif_ptrs[it].Value <= min_lif)
                        {
                            break;
                        }
                    }
                    if (pxcount < min_lif_index)
                    {
                        // not enough LIF pixels available
                        min_lif_index = pxcount; // try the brightest pixels (ie with the largest value for the LIF)
                    }
                }
                else
                {
                    // No LIF threshold: the full range of pixels is valid
                    min_lif_index = lif_ptrs.Count;
                }

                double hits = 0.0;
                while (hits < pxcount)
                {
                    int rnd_index = model.RandomGenerator.Random(0, min_lif_index);
                    if (iheightfrom != -1)
                    {
                        height = Global.Limit(model.RandomGenerator.Random(height_from, height_to), 0.05, 4.0);
                        if (age <= 1.0)
                        {
                            age = Math.Max(Math.Round(height / 4.0 * age4m), 1.0); // assume a linear relationship between height and age
                        }
                    }
                    Point offset = lightGrid.IndexOf(lif_ptrs[rnd_index].Key);
                    ResourceUnit ru = null;
                    SaplingCell sc = model.Saplings.Cell(offset, model, true, ref ru);
                    if (sc != null)
                    {
                        SaplingTree st = sc.AddSapling((float)height, (int)age, species.Index);
                        if (st != null)
                        {
                            hits += Math.Max(1.0, ru.ResourceUnitSpecies(st.SpeciesIndex).Species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(st.Height));
                        }
                        else
                        {
                            hits++;
                        }
                    }
                    else
                    {
                        hits++; // avoid an infinite loop
                    }
                }

                total += pxcount;
            }

            // initialize grass cover
            if (init.GetColumnIndex("grass_cover") > -1)
            {
                int grass_cover_value = Int32.Parse(init.Value(low_index, "grass_cover"));
                if (grass_cover_value < 0 || grass_cover_value > 100)
                {
                    throw new NotSupportedException(String.Format("The grass cover percentage (column 'grass_cover') for stand '{0}' is '{1}', which is invalid (expected: 0-100)", standID, grass_cover_value));
                }
                model.GrassCover.SetInitialValues(model, lif_ptrs, grass_cover_value);
            }

            return total;
        }

        private class InitFileItem
        {
            public int Age { get; set; }
            public double Count { get; set; }
            public double Density { get; set; }
            public double DbhFrom { get; set; }
            public double DbhTo { get; set; }
            public double HD { get; set; }
            public Species Species { get; set; }
        }
    }
}
