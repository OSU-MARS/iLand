﻿using iLand.Output;
using iLand.Tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.Core
{
    /** @class StandLoader
        @ingroup tools
        loads (initializes) trees for a "stand" from various sources.
        StandLoader initializes trees on the landscape. It reads (usually) from text files, creates the
        trees and distributes the trees on the landscape (on the ResoureceUnit or on a stand defined by a grid).

        See http://iland.boku.ac.at/initialize+trees
        */
    internal class StandLoader
    {
        // provide a mapping between "Picus"-style and "iLand"-style species Ids
        private static readonly List<int> PicusSpeciesIDs = new List<int>() { 0, 1, 17 };
        private static readonly List<string> iLandSpeciesIDs = new List<string>() { "piab", "piab", "fasy" };

        // evenlist: tentative order of pixel-indices (within a 5x5 grid) used as tree positions.
        // e.g. 12 = centerpixel, 0: upper left corner, ...
        private static readonly int[] Evenlist = new int[] { 12, 6, 18, 16, 8, 22, 2, 10, 14, 0, 24, 20, 4, 1, 13, 15, 19, 21, 3, 7, 11, 17, 23, 5, 9 };
        private static readonly int[] Unevenlist = new int[] { 11, 13, 7, 17, 1, 19, 5, 21, 9, 23, 3, 15, 6, 18, 2, 10, 4, 24, 12, 0, 8, 14, 20, 22 };

        private readonly Model mModel;
        private RandomCustomPdf mRandom;
        private List<InitFileItem> mInitItems;
        private readonly Dictionary<int, List<InitFileItem>> mStandInitItems;
        private Expression mHeightGridResponse; ///< response function to calculate fitting of pixels with pre-determined height
        private int mHeightGridTries; ///< maximum number of tries to land at pixel with fitting height

        /// define a stand grid externally
        public MapGrid CurrentMap { get; set; }
        /// set a constraining height grid (10m resolution)
        public MapGrid InitHeightGrid { get; set; } ///< grid with tree heights

        public StandLoader(Model model)
        {
            InitHeightGrid = new MapGrid();
            mStandInitItems = new Dictionary<int, List<InitFileItem>>();
            mModel = model;
            mRandom = null;
            CurrentMap = null;
            InitHeightGrid = null;
            mHeightGridResponse = null;
        }

        public void CopyTrees()
        {
            // we assume that all stands are equal, so wie simply COPY the trees and modify them afterwards
            Grid<ResourceUnit> ruGrid = mModel.ResourceUnitGrid;
            if (ruGrid[0] == null)
            {
                throw new NotSupportedException("Standloader: invalid resource unit pointer!");
            }

            // skip the first RU...
            List<Tree> tocopy = mModel.FirstResourceUnit().Trees;
            for (int p = 1; p < ruGrid.Count; ++p)
            {
                RectangleF rect = ruGrid[p].BoundingBox;
                foreach (Tree tree in tocopy)
                {
                    Tree newtree = ruGrid[p].NewTree();
                    newtree = tree; // copy tree data...
                    newtree.SetLightCellIndex(tree.GetCellCenterPoint().Add(rect.TopLeft()));
                    newtree.RU = ruGrid[p];
                    newtree.SetNewID();
                }
            }
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine(Tree.TreesCreated + " trees loaded / copied.");
            }
        }

        /** main routine of the stand setup.
          */
        public void ProcessInit()
        {
            GlobalSettings g = GlobalSettings.Instance;
            XmlHelper xml = new XmlHelper(g.Settings.Node("model.initialization"));

            string initializationMode = xml.GetString("mode", "copy");
            string type = xml.GetString("type", "");
            string fileName = xml.GetString("file", "");

            bool heightGridEnabled = xml.GetBool("heightGrid.enabled", false);
            mHeightGridTries = xml.ValueInt("heightGrid.maxTries", 10);
            if (heightGridEnabled)
            {
                string initHeightGridFile = GlobalSettings.Instance.Path(xml.GetString("heightGrid.fileName"), "init");
                Debug.WriteLine("initialization: using predefined tree heights map " + initHeightGridFile);

                MapGrid p = new MapGrid(initHeightGridFile, false);
                if (!p.IsValid())
                {
                    throw new NotSupportedException(String.Format("Error when loading grid with tree heights for stand initalization: file {0} not found or not valid.", initHeightGridFile));
                }
                InitHeightGrid = p;

                string expr = xml.GetString("heightGrid.fitFormula", "polygon(x, 0,0, 0.8,1, 1.1, 1, 1.25,0)");
                mHeightGridResponse = new Expression(expr);
                mHeightGridResponse.Linearize(0.0, 2.0);
            }

            Tree.ResetStatistics();

            // one global init-file for the whole area:
            if (initializationMode == "single")
            {
                // useful for 1ha simulations only...
                if (GlobalSettings.Instance.Model.ResourceUnits.Count > 1)
                {
                    throw new NotSupportedException("Error initialization: 'mode' is 'single' but more than one resource unit is simulated (consider using another 'mode').");
                }

                LoadInitFile(fileName, type, 0, GlobalSettings.Instance.Model.FirstResourceUnit()); // this is the first resource unit
                EvaluateDebugTrees();
                return;
            }

            // call a single tree init for each resource unit
            if (initializationMode == "unit")
            {
                foreach (ResourceUnit ru in g.Model.ResourceUnits)
                {
                    // set environment
                    g.Model.Environment.SetPosition(ru.BoundingBox.Center());
                    type = xml.GetString("type", "");
                    fileName = xml.GetString("file", "");
                    if (String.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }
                    LoadInitFile(fileName, type, 0, ru);
                    if (GlobalSettings.Instance.LogInfo())
                    {
                        Debug.WriteLine("loaded " + fileName + " on " + ru.BoundingBox + ", " + ru.Trees.Count + "trees.");
                    }
                }
                EvaluateDebugTrees();
                return;
            }

            // map-modus: load a init file for each "polygon" in the standgrid
            if (initializationMode == "map")
            {
                if (g.Model.StandGrid != null || !g.Model.StandGrid.IsValid())
                {
                    throw new NotSupportedException("Stand-Initialization: model.initialization.mode is 'map' but there is no valid stand grid defined (model.world.standGrid)");
                }
                string map_file_name = GlobalSettings.Instance.Path(xml.GetString("mapFileName"), "init");

                CsvFile map_file = new CsvFile(map_file_name);
                if (map_file.RowCount == 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} is empty or missing!", map_file_name));
                }
                int ikey = map_file.GetColumnIndex("id");
                int ivalue = map_file.GetColumnIndex("filename");
                if (ikey < 0 || ivalue < 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} does not contain the mandatory columns 'id' and 'filename'!", map_file_name));
                }
                string file_name;
                for (int i = 0; i < map_file.RowCount; i++)
                {
                    int key = Int32.Parse(map_file.Value(i, ikey));
                    if (key > 0)
                    {
                        file_name = map_file.Value(i, ivalue);
                        if (GlobalSettings.Instance.LogInfo())
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
                EvaluateDebugTrees();
                return;
            }

            // standgrid mode: load one large init file
            if (initializationMode == "standgrid")
            {
                fileName = GlobalSettings.Instance.Path(fileName, "init");
                if (!File.Exists(fileName))
                {
                    throw new NotSupportedException(String.Format("load-ini-file: file '{0}' does not exist.", fileName));
                }
                string content = Helper.LoadTextFile(fileName);
                // this processes the init file (also does the checking) and
                // stores in a Dictionary datastrucutre
                ParseInitFile(content, fileName);

                // setup the random distribution
                string density_func = xml.GetString("model.initialization.randomFunction", "1-x^2");
                if (GlobalSettings.Instance.LogInfo())
                {
                    Debug.WriteLine("density function: " + density_func);
                }
                if (mRandom == null || (mRandom.DensityFunction != density_func))
                {
                    mRandom = new RandomCustomPdf(density_func);
                    if (GlobalSettings.Instance.LogInfo())
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
                    ExecuteiLandInitStand(it.Key);
                }
                Debug.WriteLine("finished setup of trees.");
                EvaluateDebugTrees();
                return;
            }

            if (initializationMode == "snapshot")
            {
                // load a snapshot from a file
                Snapshot shot = new Snapshot();
                string input_db = GlobalSettings.Instance.Path(fileName);
                shot.Load(input_db);
                return;
            }
            throw new NotSupportedException("processInit: invalid initalization.mode!");
        }

        public void ProcessAfterInit()
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.Instance.Settings.Node("model.initialization"));

            string mode = xml.GetString("mode", "copy");
            if (mode == "standgrid")
            {
                // load a file with saplings per polygon
                string filename = xml.GetString("saplingFile", "");
                if (String.IsNullOrEmpty(filename))
                {
                    return;
                }
                filename = GlobalSettings.Instance.Path(filename, "init");
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
                    int row_stand = Int32.Parse(init_file.Value(i, istandid));
                    if (row_stand != stand_id)
                    {
                        if (stand_id >= 0)
                        {
                            // process stand
                            int ihigh = i - 1; // up to the last
                            total += LoadSaplingsLif(stand_id, init_file, ilow, ihigh);
                        }
                        ilow = i; // mark beginning of new stand
                        stand_id = row_stand;
                    }
                }
                if (stand_id >= 0)
                {
                    total += LoadSaplingsLif(stand_id, init_file, ilow, init_file.RowCount - 1); // the last stand
                }
                Debug.WriteLine("initialization of sapling: total created: " + total);
            }
        }

        public void EvaluateDebugTrees()
        {
            // evaluate debugging
            string dbg_str = GlobalSettings.Instance.Settings.GetStringParameter("debug_tree");
            int counter = 0;
            if (String.IsNullOrEmpty(dbg_str) == false)
            {
                if (dbg_str == "debugstamp")
                {
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees...");
                    // try to force an error if a stamp is invalid
                    AllTreeIterator treeIterator = new AllTreeIterator(GlobalSettings.Instance.Model);
                    double total_offset = 0.0;
                    for (Tree t = treeIterator.MoveNext(); t != null; t = treeIterator.MoveNext())
                    {
                        total_offset += t.Stamp.DistanceOffset;
                        if (!GlobalSettings.Instance.Model.LightGrid.Contains(t.LightCellIndex))
                        {
                            Debug.WriteLine("evaluateDebugTrees: debugstamp: invalid position found!");
                        }
                    }
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees finished...");
                    return;
                }
                TreeWrapper tw = new TreeWrapper();
                Expression dexp = new Expression(dbg_str, tw); // load expression dbg_str and enable external model variables
                AllTreeIterator at = new AllTreeIterator(GlobalSettings.Instance.Model);
                for (Tree t = at.MoveNext(); t != null; t = at.MoveNext())
                {
                    tw.Tree = t;
                    double result = dexp.Execute();
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
        public int LoadInitFile(string fileName, string type, int standID, ResourceUnit ru = null)
        {
            string pathFileName = GlobalSettings.Instance.Path(fileName, "init");
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
                return LoadPicusFile(pathFileName, ru);
            }
            if (type == "iland" || type == "distribution")
            {
                return LoadiLandFile(pathFileName, ru, standID);
            }
            throw new XmlException("Unknown initialization type '" + type + "'. Is a /project/model/initialization/type element present in the project file?");
        }

        public int LoadPicusFile(string fileName, ResourceUnit ru)
        {
            string content = Helper.LoadTextFile(fileName);
            if (String.IsNullOrEmpty(content))
            {
                Debug.WriteLine("file not found: " + fileName);
                return 0;
            }
            return LoadSingleTreeList(content, ru, fileName);
        }

        /** load a list of trees (given by content) to a resource unit. Param fileName is just for error reporting.
          returns the number of loaded trees.
          */
        public int LoadSingleTreeList(string treeList, ResourceUnit ru, string fileName)
        {
            if (ru == null)
            {
                ru = mModel.FirstResourceUnit();
            }
            Debug.Assert(ru != null);

            PointF offset = ru.BoundingBox.TopLeft();
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

            int iID = infile.GetColumnIndex("id");
            int iX = infile.GetColumnIndex("x");
            int iY = infile.GetColumnIndex("y");
            int iBhd = infile.GetColumnIndex("bhdfrom");
            if (iBhd < 0)
            {
                iBhd = infile.GetColumnIndex("dbh");
            }
            double height_conversion = 100.0;
            int iHeight = infile.GetColumnIndex("treeheight");
            if (iHeight < 0)
            {
                iHeight = infile.GetColumnIndex("height");
                height_conversion = 1.0; // in meter
            }
            int iSpecies = infile.GetColumnIndex("species");
            int iAge = infile.GetColumnIndex("age");
            if (iX == -1 || iY == -1 || iBhd == -1 || iSpecies == -1 || iHeight == -1)
            {
                throw new NotSupportedException(String.Format("Initfile {0} is not valid!\nRequired columns are: x,y, bhdfrom or dbh, species, treeheight or height.", fileName));
            }

            double dbh;
            bool ok;
            int cnt = 0;
            string speciesid;
            for (int i = 1; i < infile.RowCount; i++)
            {
                dbh = Double.Parse(infile.Value(i, iBhd));
                //if (dbh<5.)
                //    continue;
                PointF f = new PointF();
                if (iX >= 0 && iY >= 0)
                {
                    f.X = Single.Parse(infile.Value(i, iX)) + offset.X;
                    f.Y = Single.Parse(infile.Value(i, iY)) + offset.Y;
                }
                // position valid?
                if (!mModel.HeightGrid[f].IsValid())
                {
                    continue;
                }
                Tree tree = ru.NewTree();
                tree.SetLightCellIndex(f);
                if (iID >= 0)
                {
                    tree.ID = Int32.Parse(infile.Value(i, iID));
                }

                tree.Dbh = (float)dbh;
                tree.SetHeight(Single.Parse(infile.Value(i, iHeight)) / (float)height_conversion); // convert from Picus-cm to m if necessary

                speciesid = infile.Value(i, iSpecies);
                ok = Int32.TryParse(speciesid, out int picusid);
                if (ok)
                {
                    int idx = PicusSpeciesIDs.IndexOf(picusid);
                    if (idx == -1)
                    {
                        throw new NotSupportedException(String.Format("Loading init-file: invalid Picus-species-id. Species: {0}", picusid));
                    }
                    speciesid = iLandSpeciesIDs[idx];
                }
                Species s = speciesSet.GetSpecies(speciesid);
                if (ru == null || s == null)
                {
                    throw new NotSupportedException(String.Format("Loading init-file: either resource unit or species invalid. Species: {0}", speciesid));
                }
                tree.Species = s;

                ok = true;
                if (iAge >= 0)
                {
                    ok = Int32.TryParse(infile.Value(i, iAge), out int age);
                    tree.SetAge(age, tree.Height); // this is a *real* age
                }
                if (iAge < 0 || !ok || tree.Age == 0)
                {
                    tree.SetAge(0, tree.Height); // no real tree age available
                }

                tree.RU = ru;
                tree.Setup();
                cnt++;
            }
            return cnt;
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
        public int LoadDistributionList(string content, ResourceUnit ru, int stand_id, string fileName)
        {
            int total_count = ParseInitFile(content, fileName, ru);
            if (total_count == 0)
            {
                return 0;
            }

            // setup the random distribution
            string density_func = GlobalSettings.Instance.Settings.GetString("model.initialization.randomFunction", "1-x^2");
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine("density function: " + density_func);
            }
            if (mRandom == null || (mRandom.DensityFunction != density_func))
            {
                mRandom = new RandomCustomPdf(density_func);
                if (GlobalSettings.Instance.LogInfo())
                {
                    Debug.WriteLine("new probabilty density function: " + density_func);
                }
            }
            if (stand_id > 0)
            {
                // execute stand based initialization
                ExecuteiLandInitStand(stand_id);
            }
            else
            {
                // exeucte the initialization based on single resource units
                ExecuteiLandInit(ru);
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

            int icount = infile.GetColumnIndex("count");
            int ispecies = infile.GetColumnIndex("species");
            int idbh_from = infile.GetColumnIndex("dbh_from");
            int idbh_to = infile.GetColumnIndex("dbh_to");
            int ihd = infile.GetColumnIndex("hd");
            int iage = infile.GetColumnIndex("age");
            int idensity = infile.GetColumnIndex("density");
            if (icount < 0 || ispecies < 0 || idbh_from < 0 || idbh_to < 0 || ihd < 0 || iage < 0)
            {
                throw new NotSupportedException(String.Format("load-ini-file: file '{0}' containts not all required fields (count, species, dbh_from, dbh_to, hd, age).", fileName));
            }

            int istandid = infile.GetColumnIndex("stand_id");
            mInitItems.Clear();
            mStandInitItems.Clear();

            InitFileItem item = new InitFileItem();
            bool ok;
            int total_count = 0;
            for (int row = 0; row < infile.RowCount; row++)
            {
                item.count = Double.Parse(infile.Value(row, icount));
                total_count += (int)item.count;
                item.dbh_from = Double.Parse(infile.Value(row, idbh_from));
                item.dbh_to = Double.Parse(infile.Value(row, idbh_to));
                item.hd = Double.Parse(infile.Value(row, ihd));
                if (item.hd == 0.0 || item.dbh_from / 100.0 * item.hd < 4.0)
                {
                    Trace.TraceWarning(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from));
                }
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                ok = true;
                if (iage >= 0)
                {
                    ok = Int32.TryParse(infile.Value(row, iage), out item.age);
                }
                if (iage < 0 || !ok)
                {
                    item.age = 0;
                }

                item.species = speciesSet.GetSpecies(infile.Value(row, ispecies));
                if (idensity >= 0)
                {
                    item.density = Double.Parse(infile.Value(row, idensity));
                }
                else
                {
                    item.density = 0.0;
                }
                if (item.density < -1)
                {
                    throw new NotSupportedException(String.Format("load-ini-file: invalid value for density. Allowed range is -1..1: '{0}' in file '{1}', line {2}.",
                                                                  item.density, fileName, row));
                }
                if (item.species == null)
                {
                    throw new NotSupportedException(String.Format("load-ini-file: unknown speices '{0}' in file '{1}', line {2}.",
                                                                  infile.Value(row, ispecies), fileName, row));
                }
                if (istandid >= 0)
                {
                    int standid = Int32.Parse(infile.Value(row, istandid));
                    mStandInitItems[standid].Add(item);
                }
                else
                {
                    mInitItems.Add(item);
                }
            }
            return total_count;
        }

        public int LoadiLandFile(string fileName, ResourceUnit ru, int stand_id)
        {
            if (!File.Exists(fileName))
            {
                throw new NotSupportedException(String.Format("load-ini-file: file '{0}' does not exist.", fileName));
            }
            string content = Helper.LoadTextFile(fileName);
            return LoadDistributionList(content, ru, stand_id, fileName);
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
            public double basal_area; // accumulated basal area
            public Point pixelOffset; // location of the pixel
            public ResourceUnit resource_unit; // pointer to the resource unit the pixel belongs to
            public double h_max; // predefined maximum height at given pixel (if available from LIDAR or so)
            public bool locked; // pixel is dedicated to a single species

            public SInitPixel()
            {
                basal_area = 0.0;
                resource_unit = null;
                h_max = -1.0;
                locked = false;
            }
        };

        private int SortInitPixelLessThan(SInitPixel s1, SInitPixel s2)
        {
            if (s1.basal_area < s2.basal_area)
            {
                return -1;
            }
            if (s1.basal_area > s2.basal_area)
            {
                return 1;
            }
            return 0;
        }

        private int SortInitPixelUnlocked(SInitPixel s1, SInitPixel s2)
        {
            if (!s1.locked && s2.locked)
            {
                return -1;
            }
            return 0;
        }

        private void ExecuteiLandInit(ResourceUnit ru)
        {
            PointF offset = ru.BoundingBox.TopLeft();
            Point offsetIdx = GlobalSettings.Instance.Model.LightGrid.IndexAt(offset);

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
            foreach (InitFileItem item in mInitItems)
            {
                rand_fraction = Math.Abs(item.density);
                for (int i = 0; i < item.count; i++)
                {
                    // create trees
                    int tree_idx = ru.NewTreeIndex();
                    Tree tree = ru.Trees[tree_idx]; // get reference to modify tree
                    tree.Dbh = (float)RandomGenerator.Random(item.dbh_from, item.dbh_to);
                    tree.SetHeight(tree.Dbh / 100.0F * (float)item.hd); // dbh from cm->m, *hd-ratio -> meter height
                    tree.Species = item.species;
                    if (item.age <= 0)
                    {
                        tree.SetAge(0, tree.Height);
                    }
                    else
                    {
                        tree.SetAge(item.age, tree.Height);
                    }
                    tree.RU = ru;
                    tree.Setup();
                    total_count++;

                    // calculate random value. "density" is from 1..-1.
                    rand_val = mRandom.Get();
                    if (item.density < 0)
                    {
                        rand_val = 1.0 - rand_val;
                    }
                    rand_val = rand_val * rand_fraction + RandomGenerator.Random() * (1.0 - rand_fraction);

                    // key: rank of target pixel
                    // first: index of target pixel
                    // second: sum of target pixel
                    key = Global.Limit((int)(100.0 * rand_val), 0, 99); // get from random number generator
                    tree_map.Add(tcount[key].Item1, tree_idx); // store tree in map
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
                if (!mModel.HeightGrid[pixel_center].IsValid())
                {
                    // no trees on that pixel: let trees die
                    foreach (int tree_idx in trees)
                    {
                        ru.Trees[tree_idx].Die();
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
                            r = RandomGenerator.Random();
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
                    pos = ru.Index % 2 != 0 ? Evenlist[index] : Unevenlist[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    tree_pos = new Point(offsetIdx.X + 5 * (i / 10) + pos / 5,
                                         offsetIdx.Y + 5 * (i % 10) + pos % 5);
                    //Debug.WriteLine(tree_no++ + "to" + index);
                    ru.Trees[tree_idx].LightCellIndex = tree_pos;
                }
            }
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland.boku.ac.at/initialize+trees
        private void ExecuteiLandInitStand(int stand_id)
        {
            MapGrid grid = GlobalSettings.Instance.Model.StandGrid;
            if (CurrentMap != null)
            {
                grid = CurrentMap;
            }

            // get a list of positions of all pixels that belong to our stand
            List<int> indices = grid.GridIndices(stand_id);
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + stand_id + " not in project area. No init performed.");
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
                    pixelOffset = grid.Grid.IndexOf(i), // index in the 10m grid
                };
                p.resource_unit = GlobalSettings.Instance.Model.GetResourceUnit(grid.Grid.GetCellCenterPoint(p.pixelOffset));
                if (InitHeightGrid != null)
                {
                    p.h_max = InitHeightGrid.Grid[p.pixelOffset];
                }
                pixel_list.Add(p);
            }
            double area_factor = grid.Area(stand_id) / Constant.RUArea;

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
                if (item.density > 1.0)
                {
                    // special case with single-species-area
                    if (total_count == 0)
                    {
                        // randomize the pixels
                        for (int it = 0; it < pixel_list.Count; ++it)
                        {
                            pixel_list[it].basal_area = RandomGenerator.Random();
                        }
                        pixel_list.Sort(SortInitPixelLessThan);

                        for (int it = 0; it < pixel_list.Count; ++it)
                        {
                            pixel_list[it].basal_area = 0.0;
                        }
                    }

                    if (item.species != last_locked_species)
                    {
                        last_locked_species = item.species;
                        pixel_list.Sort(SortInitPixelUnlocked);
                    }
                }
                else
                {
                    pixel_list.Sort(SortInitPixelLessThan);
                    last_locked_species = null;
                }
                rand_fraction = item.density;
                int count = (int)(item.count * area_factor + 0.5); // round
                double init_max_height = item.dbh_to / 100.0 * item.hd;
                for (int i = 0; i < count; i++)
                {
                    bool found = false;
                    int tries = mHeightGridTries;
                    while (!found && --tries != 0)
                    {
                        // calculate random value. "density" is from 1..-1.
                        if (item.density <= 1.0)
                        {
                            rand_val = mRandom.Get();
                            if (item.density < 0)
                            {
                                rand_val = 1.0 - rand_val;
                            }
                            rand_val = rand_val * rand_fraction + RandomGenerator.Random() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            rand_val = RandomGenerator.Random() * Math.Min(item.density / 100.0, 1.0);
                        }
                        ++total_tries;

                        // key: rank of target pixel
                        key = Global.Limit((int)(pixel_list.Count * rand_val), 0, pixel_list.Count - 1); // get from random number generator

                        if (InitHeightGrid != null)
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            double p_value = pixel_list[key].h_max > 0.0 ? mHeightGridResponse.Calculate(init_max_height / pixel_list[key].h_max) : 0.0;
                            if (RandomGenerator.Random() < p_value)
                            {
                                found = true;
                            }
                        }
                        else
                        {
                            found = true;
                        }
                        if (last_locked_species != null && pixel_list[key].locked)
                        {
                            found = false;
                        }
                    }
                    if (tries < 0) ++total_misses;

                    // create a tree
                    ResourceUnit ru = pixel_list[key].resource_unit;
                    int tree_idx = ru.NewTreeIndex();
                    Tree tree = ru.Trees[tree_idx]; // get reference to modify tree
                    tree.Dbh = (float)RandomGenerator.Random(item.dbh_from, item.dbh_to);
                    tree.SetHeight((float)(tree.Dbh / 100.0 * item.hd)); // dbh from cm->m, *hd-ratio -> meter height
                    tree.Species = item.species;
                    if (item.age <= 0)
                    {
                        tree.SetAge(0, tree.Height);
                    }
                    else
                    {
                        tree.SetAge(item.age, tree.Height);
                    }
                    tree.RU = ru;
                    tree.Setup();
                    total_count++;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    tree_map.Add(pixel_list[key].pixelOffset, tree_idx);
                    pixel_list[key].basal_area += tree.BasalArea(); // aggregate the basal area for each 10m pixel
                    if (last_locked_species != null)
                    {
                        pixel_list[key].locked = true;
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
                if (GlobalSettings.Instance.LogInfo())
                {
                    Debug.WriteLine("init for stand " + stand_id + " treecount: " + total_count + ", tries: " + total_tries + ", misses: " + total_misses + ", %miss: " + Math.Round(total_misses * 100 / (double)total_count));
                }
            }

            foreach (SInitPixel p in pixel_list)
            {
                List<int> trees = tree_map[p.pixelOffset].ToList();
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
                            r = RandomGenerator.Random();
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
                    int pos = p.resource_unit.Index % 2 != 0 ? Evenlist[index] : Unevenlist[index];
                    Point tree_pos = new Point(p.pixelOffset.X * Constant.LightPerHeightSize + pos / Constant.LightPerHeightSize, // convert to LIF index
                                               p.pixelOffset.Y * Constant.LightPerHeightSize + pos % Constant.LightPerHeightSize);

                    p.resource_unit.Trees[tree_idx].LightCellIndex = tree_pos;
                    // test if tree position is valid..
                    if (!GlobalSettings.Instance.Model.LightGrid.Contains(tree_pos))
                    {
                        Debug.WriteLine("Standloader: invalid position!");
                    }
                }
            }
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine("init for stand " + stand_id + " with area" + grid.Area(stand_id) + " m2, count of 10m pixels: " + indices.Count + "initialized trees: " + total_count);
            }
        }

        /// a (hacky) way of adding saplings of a certain age to a stand defined by 'stand_id'.
        public int LoadSaplings(string content, int stand_id)
        {
            // Q_UNUSED(fileName);
            MapGrid stand_grid;
            if (CurrentMap != null)
            {
                stand_grid = CurrentMap; // if set
            }
            else
            {
                stand_grid = GlobalSettings.Instance.Model.StandGrid; // default
            }

            List<int> indices = stand_grid.GridIndices(stand_id); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + stand_id + " not in project area. No init performed.");
                return -1;
            }
            double area_factor = stand_grid.Area(stand_id) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

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

            SpeciesSet set = GlobalSettings.Instance.Model.FirstResourceUnit().SpeciesSet;
            double height, age;
            int total = 0;
            for (int row = 0; row < init.RowCount; ++row)
            {
                int pxcount = (int)Math.Round(Double.Parse(init.Value(row, icount)) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.GetSpecies(init.Value(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.Value(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.Value(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.Value(row, iage));

                int misses = 0;
                int hits = 0;
                while (hits < pxcount)
                {
                    // sapling location
                    int rnd_index = RandomGenerator.Random(0, indices.Count);
                    Point offset = stand_grid.Grid.IndexOf(indices[rnd_index]);
                    offset.X *= Constant.LightPerHeightSize; // index of 10m patch -> to lif pixel coordinates
                    offset.Y *= Constant.LightPerHeightSize;
                    int in_p = RandomGenerator.Random(0, Constant.LightPerHeightSize * Constant.LightPerHeightSize); // index of lif-pixel
                    offset.X += in_p / Constant.LightPerHeightSize;
                    offset.Y += in_p % Constant.LightPerHeightSize;

                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.Instance.Model.Saplings.Cell(offset, true, ref ru);
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
                        Debug.WriteLine("tried to add " + pxcount + " saplings at stand " + stand_id + " but failed in finding enough free positions. Added " + hits + " and stopped.");
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

        public int LoadSaplingsLif(int stand_id, CsvFile init, int low_index, int high_index)
        {
            MapGrid stand_grid;
            if (CurrentMap != null)
            {
                stand_grid = CurrentMap; // if set
            }
            else
            {
                stand_grid = GlobalSettings.Instance.Model.StandGrid; // default
            }

            if (!stand_grid.IsValid(stand_id))
            {
                return 0;
            }

            List<int> indices = stand_grid.GridIndices(stand_id); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + stand_id + " not in project area. No init performed.");
                return 0;
            }
            // prepare space for LIF-pointers (2m Pixel)
            List<KeyValuePair<int, float>> lif_ptrs = new List<KeyValuePair<int, float>>(indices.Count * Constant.LightPerHeightSize * Constant.LightPerHeightSize);
            Grid<float> modelGrid = GlobalSettings.Instance.Model.LightGrid;
            for (int l = 0; l < indices.Count; ++l)
            {
                Point offset = stand_grid.Grid.IndexOf(indices[l]);
                offset.X *= Constant.LightPerHeightSize; // index of 10m patch -> to lif pixel coordinates
                offset.Y *= Constant.LightPerHeightSize;
                for (int y = 0; y < Constant.LightPerHeightSize; ++y)
                {
                    for (int x = 0; x < Constant.LightPerHeightSize; ++x)
                    {
                        int modelIndex = modelGrid.IndexOf(offset.X + x, offset.Y + y);
                        KeyValuePair<int, float> indexAndValue = new KeyValuePair<int, float>(modelIndex, modelGrid[modelIndex]);
                        lif_ptrs.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lif_ptrs.Sort(CompareLifValue); // higher: highest values first

            double area_factor = stand_grid.Area(stand_id) / Constant.RUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

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

            SpeciesSet set = GlobalSettings.Instance.Model.FirstResourceUnit().SpeciesSet;
            double height, age;
            int total = 0;
            for (int row = low_index; row <= high_index; ++row)
            {
                int pxcount = (int)(Double.Parse(init.Value(row, icount)) * area_factor); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.GetSpecies(init.Value(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.Value(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.Value(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.Value(row, iage));
                double age4m = itopage == -1 ? 10 : Double.Parse(init.Value(row, itopage));
                double height_from = iheightfrom == -1 ? -1.0 : Double.Parse(init.Value(row, iheightfrom));
                double height_to = iheightto == -1 ? -1.0 : Double.Parse(init.Value(row, iheightto));
                double min_lif = iminlif == -1 ? 1.0 : Double.Parse(init.Value(row, iminlif));
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
                    int rnd_index = RandomGenerator.Random(0, min_lif_index);
                    if (iheightfrom != -1)
                    {
                        height = Global.Limit(RandomGenerator.Random(height_from, height_to), 0.05, 4.0);
                        if (age <= 1.0)
                        {
                            age = Math.Max(Math.Round(height / 4.0 * age4m), 1.0); // assume a linear relationship between height and age
                        }
                    }
                    Point offset = modelGrid.IndexOf(lif_ptrs[rnd_index].Key);
                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.Instance.Model.Saplings.Cell(offset, true, ref ru);
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
                    throw new NotSupportedException(String.Format("The grass cover percentage (column 'grass_cover') for stand '{0}' is '{1}', which is invalid (expected: 0-100)", stand_id, grass_cover_value));
                }
                GlobalSettings.Instance.Model.GrassCover.SetInitialValues(lif_ptrs, grass_cover_value);
            }

            return total;
        }

        private class InitFileItem
        {
            public Species species;
            public double count;
            public double dbh_from, dbh_to;
            public double hd;
            public int age;
            public double density;
        }
    }
}
