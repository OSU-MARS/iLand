using iLand.output;
using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace iLand.core
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
        private static List<int> picusSpeciesIds = new List<int>() { 0, 1, 17 };
        private static List<string> iLandSpeciesIds = new List<string>() { "piab", "piab", "fasy" };

        // evenlist: tentative order of pixel-indices (within a 5x5 grid) used as tree positions.
        // e.g. 12 = centerpixel, 0: upper left corner, ...
        private static readonly int[] evenlist = new int[] { 12, 6, 18, 16, 8, 22, 2, 10, 14, 0, 24, 20, 4, 1, 13, 15, 19, 21, 3, 7, 11, 17, 23, 5, 9 };
        private static readonly int[] unevenlist = new int[] { 11, 13, 7, 17, 1, 19, 5, 21, 9, 23, 3, 15, 6, 18, 2, 10, 4, 24, 12, 0, 8, 14, 20, 22 };

        private Model mModel;
        private RandomCustomPDF mRandom;
        private List<InitFileItem> mInitItems;
        private Dictionary<int, List<InitFileItem>> mStandInitItems;
        private MapGrid mCurrentMap;
        private MapGrid mInitHeightGrid; ///< grid with tree heights
        private Expression mHeightGridResponse; ///< response function to calculate fitting of pixels with pre-determined height
        private int mHeightGridTries; ///< maximum number of tries to land at pixel with fitting height

        /// define a stand grid externally
        public void setMap(MapGrid map) { mCurrentMap = map; }
        /// set a constraining height grid (10m resolution)
        public void setInitHeightGrid(MapGrid height_grid) { mInitHeightGrid = height_grid; }

        public StandLoader(Model model)
        {
            mInitHeightGrid = new MapGrid();
            mStandInitItems = new Dictionary<int, List<InitFileItem>>();
            mModel = model;
            mRandom = null;
            mCurrentMap = null;
            mInitHeightGrid = null;
            mHeightGridResponse = null;
        }

        public void copyTrees()
        {
            // we assume that all stands are equal, so wie simply COPY the trees and modify them afterwards
            Grid<ResourceUnit> ruGrid = mModel.RUgrid();
            if (ruGrid[0] == null)
            {
                throw new NotSupportedException("Standloader: invalid resource unit pointer!");
            }

            // skip the first RU...
            List<Tree> tocopy = mModel.ru().trees();
            for (int p = 1; p < ruGrid.count(); ++p)
            {
                RectangleF rect = ruGrid[p].boundingBox();
                foreach (Tree tree in tocopy)
                {
                    Tree newtree = ruGrid[p].newTree();
                    newtree = tree; // copy tree data...
                    newtree.setPosition(tree.position().Add(rect.TopLeft()));
                    newtree.setRU(ruGrid[p]);
                    newtree.setNewId();
                }
            }
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine(Tree.statCreated() + "trees loaded / copied.");
            }
        }

        /** main routine of the stand setup.
          */
        public void processInit()
        {
            GlobalSettings g = GlobalSettings.instance();
            XmlHelper xml = new XmlHelper(g.settings().node("model.initialization"));

            string copy_mode = xml.value("mode", "copy");
            string type = xml.value("type", "");
            string fileName = xml.value("file", "");

            bool height_grid_enabled = xml.valueBool("heightGrid.enabled", false);
            mHeightGridTries = xml.valueInt("heightGrid.maxTries", 10);
            if (height_grid_enabled)
            {
                string init_height_grid_file = GlobalSettings.instance().path(xml.value("heightGrid.fileName"), "init");
                Debug.WriteLine("initialization: using predefined tree heights map " + init_height_grid_file);

                MapGrid p = new MapGrid(init_height_grid_file, false);
                if (!p.isValid())
                {
                    throw new NotSupportedException(String.Format("Error when loading grid with tree heights for stand initalization: file {0} not found or not valid.", init_height_grid_file));
                }
                mInitHeightGrid = p;

                string expr = xml.value("heightGrid.fitFormula", "polygon(x, 0,0, 0.8,1, 1.1, 1, 1.25,0)");
                mHeightGridResponse = new Expression(expr);
                mHeightGridResponse.linearize(0.0, 2.0);
            }

            Tree.resetStatistics();

            // one global init-file for the whole area:
            if (copy_mode == "single")
            {
                // useful for 1ha simulations only...
                if (GlobalSettings.instance().model().ruList().Count > 1)
                {
                    throw new NotSupportedException("Error initialization: 'mode' is 'single' but more than one resource unit is simulated (consider using another 'mode').");
                }

                loadInitFile(fileName, type, 0, GlobalSettings.instance().model().ru()); // this is the first resource unit
                evaluateDebugTrees();
                return;
            }

            // call a single tree init for each resource unit
            if (copy_mode == "unit")
            {
                foreach (ResourceUnit ru in g.model().ruList())
                {
                    // set environment
                    g.model().environment().setPosition(ru.boundingBox().Center());
                    type = xml.value("type", "");
                    fileName = xml.value("file", "");
                    if (String.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }
                    loadInitFile(fileName, type, 0, ru);
                    if (GlobalSettings.instance().logLevelInfo())
                    {
                        Debug.WriteLine("loaded " + fileName + " on " + ru.boundingBox() + ", " + ru.trees().Count + "trees.");
                    }
                }
                evaluateDebugTrees();
                return;
            }

            // map-modus: load a init file for each "polygon" in the standgrid
            if (copy_mode == "map")
            {
                if (g.model().standGrid() != null || !g.model().standGrid().isValid())
                {
                    throw new NotSupportedException("Stand-Initialization: model.initialization.mode is 'map' but there is no valid stand grid defined (model.world.standGrid)");
                }
                string map_file_name = GlobalSettings.instance().path(xml.value("mapFileName"), "init");

                CSVFile map_file = new CSVFile(map_file_name);
                if (map_file.rowCount() == 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} is empty or missing!", map_file_name));
                }
                int ikey = map_file.columnIndex("id");
                int ivalue = map_file.columnIndex("filename");
                if (ikey < 0 || ivalue < 0)
                {
                    throw new NotSupportedException(String.Format("Stand-Initialization: the map file {0} does not contain the mandatory columns 'id' and 'filename'!", map_file_name));
                }
                string file_name;
                for (int i = 0; i < map_file.rowCount(); i++)
                {
                    int key = Int32.Parse(map_file.value(i, ikey));
                    if (key > 0)
                    {
                        file_name = map_file.value(i, ivalue);
                        if (GlobalSettings.instance().logLevelInfo())
                        {
                            Debug.WriteLine("loading " + file_name + " for grid id " + key);
                        }
                        if (String.IsNullOrEmpty(file_name) == false)
                        {
                            loadInitFile(file_name, type, key, null);
                        }
                    }
                }
                mInitHeightGrid = null;
                evaluateDebugTrees();
                return;
            }

            // standgrid mode: load one large init file
            if (copy_mode == "standgrid")
            {
                fileName = GlobalSettings.instance().path(fileName, "init");
                if (!File.Exists(fileName))
                {
                    throw new NotSupportedException(String.Format("load-ini-file: file '{0}' does not exist.", fileName));
                }
                string content = Helper.loadTextFile(fileName);
                // this processes the init file (also does the checking) and
                // stores in a Dictionary datastrucutre
                parseInitFile(content, fileName);

                // setup the random distribution
                string density_func = xml.value("model.initialization.randomFunction", "1-x^2");
                if (GlobalSettings.instance().logLevelInfo())
                {
                    Debug.WriteLine("density function: " + density_func);
                }
                if (mRandom == null || (mRandom.densityFunction() != density_func))
                {
                    mRandom = new RandomCustomPDF(density_func);
                    if (GlobalSettings.instance().logLevelInfo())
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
                    executeiLandInitStand(it.Key);
                }
                Debug.WriteLine("finished setup of trees.");
                evaluateDebugTrees();
                return;

            }
            if (copy_mode == "snapshot")
            {
                // load a snapshot from a file
                Snapshot shot = new Snapshot();
                string input_db = GlobalSettings.instance().path(fileName);
                shot.loadSnapshot(input_db);
                return;
            }
            throw new NotSupportedException("processInit: invalid initalization.mode!");
        }

        public void processAfterInit()
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.initialization"));

            string mode = xml.value("mode", "copy");
            if (mode == "standgrid")
            {
                // load a file with saplings per polygon
                string filename = xml.value("saplingFile", "");
                if (String.IsNullOrEmpty(filename))
                {
                    return;
                }
                filename = GlobalSettings.instance().path(filename, "init");
                if (File.Exists(filename) == false)
                {
                    throw new NotSupportedException(String.Format("load-sapling-ini-file: file '{0}' does not exist.", filename));
                }
                CSVFile init_file = new CSVFile(filename);
                int istandid = init_file.columnIndex("stand_id");
                if (istandid == -1)
                {
                    throw new NotSupportedException("Sapling-Init: the init file contains no 'stand_id' column (required in 'standgrid' mode).");
                }

                int stand_id = -99999;
                int ilow = -1, ihigh = 0;
                int total = 0;
                for (int i = 0; i < init_file.rowCount(); ++i)
                {
                    int row_stand = Int32.Parse(init_file.value(i, istandid));
                    if (row_stand != stand_id)
                    {
                        if (stand_id >= 0)
                        {
                            // process stand
                            ihigh = i - 1; // up to the last
                            total += loadSaplingsLIF(stand_id, init_file, ilow, ihigh);
                        }
                        ilow = i; // mark beginning of new stand
                        stand_id = row_stand;
                    }
                }
                if (stand_id >= 0)
                {
                    total += loadSaplingsLIF(stand_id, init_file, ilow, init_file.rowCount() - 1); // the last stand
                }
                Debug.WriteLine("initialization of sapling: total created: " + total);
            }
        }

        public void evaluateDebugTrees()
        {
            // evaluate debugging
            string dbg_str = GlobalSettings.instance().settings().paramValueString("debug_tree");
            int counter = 0;
            if (String.IsNullOrEmpty(dbg_str) == false)
            {
                if (dbg_str == "debugstamp")
                {
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees...");
                    // try to force an error if a stamp is invalid
                    AllTreeIterator treeIterator = new AllTreeIterator(GlobalSettings.instance().model());
                    double total_offset = 0.0;
                    for (Tree t = treeIterator.next(); t != null; t = treeIterator.next())
                    {
                        total_offset += t.stamp().offset();
                        if (!GlobalSettings.instance().model().grid().isIndexValid(t.positionIndex()))
                        {
                            Debug.WriteLine("evaluateDebugTrees: debugstamp: invalid position found!");
                        }
                    }
                    Debug.WriteLine("debug_tree = debugstamp: try touching all trees finished...");
                    return;
                }
                TreeWrapper tw = new TreeWrapper();
                Expression dexp = new Expression(dbg_str, tw); // load expression dbg_str and enable external model variables
                AllTreeIterator at = new AllTreeIterator(GlobalSettings.instance().model());
                for (Tree t = at.next(); t != null; t = at.next())
                {
                    tw.setTree(t);
                    double result = dexp.execute();
                    if (result != 0.0)
                    {
                        t.enableDebugging();
                        counter++;
                    }
                }
                Debug.WriteLine("evaluateDebugTrees: enabled debugging for " + counter + " trees.");
            }
        }

        /// load a single init file. Calls loadPicusFile() or loadiLandFile()
        /// @param fileName file to load
        /// @param type init mode. allowed: "picus"/"single" or "iland"/"distribution"
        public int loadInitFile(string fileName, string type, int stand_id, ResourceUnit ru = null)
        {
            string pathFileName = GlobalSettings.instance().path(fileName, "init");
            if (!File.Exists(pathFileName))
            {
                throw new NotSupportedException(String.Format("loadInitFile: File {0} does not exist!", pathFileName));
            }

            if (type == "picus" || type == "single")
            {
                if (stand_id > 0)
                {
                    throw new NotSupportedException(String.Format("loadInitFile: initialization type {0} currently not supported for stand initilization mode!" + type));
                }
                return loadPicusFile(pathFileName, ru);
            }
            if (type == "iland" || type == "distribution")
            {
                return loadiLandFile(pathFileName, ru, stand_id);
            }
            throw new NotSupportedException("loadInitFile: unknown initalization.type: " + type);
        }

        public int loadPicusFile(string fileName, ResourceUnit ru)
        {
            string content = Helper.loadTextFile(fileName);
            if (String.IsNullOrEmpty(content))
            {
                Debug.WriteLine("file not found: " + fileName);
                return 0;
            }
            return loadSingleTreeList(content, ru, fileName);
        }

        /** load a list of trees (given by content) to a resource unit. Param fileName is just for error reporting.
          returns the number of loaded trees.
          */
        public int loadSingleTreeList(string content, ResourceUnit ru, string fileName)
        {
            if (ru == null)
            {
                ru = mModel.ru();
            }
            Debug.Assert(ru != null);

            PointF offset = ru.boundingBox().TopLeft();
            SpeciesSet speciesSet = ru.speciesSet(); // of default RU

            string my_content = content;
            // cut out the <trees> </trees> part if present
            if (content.Contains("<trees>"))
            {
                Regex rx = new Regex(".*<trees>(.*)</trees>.*");
                MatchCollection matches = rx.Matches(content, 0);
                if (matches.Count < 1)
                {
                    return 0;
                }
                my_content = matches[1].Value.Trim();
            }

            CSVFile infile = new CSVFile();
            infile.loadFromString(my_content);

            int iID = infile.columnIndex("id");
            int iX = infile.columnIndex("x");
            int iY = infile.columnIndex("y");
            int iBhd = infile.columnIndex("bhdfrom");
            if (iBhd < 0)
            {
                iBhd = infile.columnIndex("dbh");
            }
            double height_conversion = 100.0;
            int iHeight = infile.columnIndex("treeheight");
            if (iHeight < 0)
            {
                iHeight = infile.columnIndex("height");
                height_conversion = 1.0; // in meter
            }
            int iSpecies = infile.columnIndex("species");
            int iAge = infile.columnIndex("age");
            if (iX == -1 || iY == -1 || iBhd == -1 || iSpecies == -1 || iHeight == -1)
            {
                throw new NotSupportedException(String.Format("Initfile {0} is not valid!\nRequired columns are: x,y, bhdfrom or dbh, species, treeheight or height.", fileName));
            }

            double dbh;
            bool ok;
            int cnt = 0;
            string speciesid;
            for (int i = 1; i < infile.rowCount(); i++)
            {
                dbh = Double.Parse(infile.value(i, iBhd));
                //if (dbh<5.)
                //    continue;
                PointF f = new PointF();
                if (iX >= 0 && iY >= 0)
                {
                    f.X = Single.Parse(infile.value(i, iX)) + offset.X;
                    f.Y = Single.Parse(infile.value(i, iY)) + offset.Y;
                }
                // position valid?
                if (!mModel.heightGrid().valueAt(f).isValid())
                {
                    continue;
                }
                Tree tree = ru.newTree();
                tree.setPosition(f);
                if (iID >= 0)
                {
                    tree.setId(Int32.Parse(infile.value(i, iID)));
                }

                tree.setDbh((float)dbh);
                tree.setHeight(Single.Parse(infile.value(i, iHeight)) / (float)height_conversion); // convert from Picus-cm to m if necessary

                speciesid = infile.value(i, iSpecies);
                ok = Int32.TryParse(speciesid, out int picusid);
                if (ok)
                {
                    int idx = picusSpeciesIds.IndexOf(picusid);
                    if (idx == -1)
                    {
                        throw new NotSupportedException(String.Format("Loading init-file: invalid Picus-species-id. Species: {0}", picusid));
                    }
                    speciesid = iLandSpeciesIds[idx];
                }
                Species s = speciesSet.species(speciesid);
                if (ru == null || s == null)
                {
                    throw new NotSupportedException(String.Format("Loading init-file: either resource unit or species invalid. Species: {0}", speciesid));
                }
                tree.setSpecies(s);

                ok = true;
                if (iAge >= 0)
                {
                    ok = Int32.TryParse(infile.value(i, iAge), out int age);
                    tree.setAge(age, tree.height()); // this is a *real* age
                }
                if (iAge < 0 || !ok || tree.age() == 0)
                {
                    tree.setAge(0, tree.height()); // no real tree age available
                }

                tree.setRU(ru);
                tree.setup();
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
        public int loadDistributionList(string content, ResourceUnit ru, int stand_id, string fileName)
        {
            int total_count = parseInitFile(content, fileName, ru);
            if (total_count == 0)
            {
                return 0;
            }

            // setup the random distribution
            string density_func = GlobalSettings.instance().settings().value("model.initialization.randomFunction", "1-x^2");
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("density function: " + density_func);
            }
            if (mRandom == null || (mRandom.densityFunction() != density_func))
            {
                mRandom = new RandomCustomPDF(density_func);
                if (GlobalSettings.instance().logLevelInfo())
                {
                    Debug.WriteLine("new probabilty density function: " + density_func);
                }
            }
            if (stand_id > 0)
            {
                // execute stand based initialization
                executeiLandInitStand(stand_id);
            }
            else
            {
                // exeucte the initialization based on single resource units
                executeiLandInit(ru);
                ru.cleanTreeList();
            }
            return total_count;
        }

        public int parseInitFile(string content, string fileName, ResourceUnit ru = null)
        {
            if (ru != null)
            {
                ru = mModel.ru();
            }
            Debug.Assert(ru != null);
            SpeciesSet speciesSet = ru.speciesSet(); // of default RU
            Debug.Assert(speciesSet != null);

            //DebugTimer t("loadiLandFile");
            CSVFile infile = new CSVFile();
            infile.loadFromString(content);

            int icount = infile.columnIndex("count");
            int ispecies = infile.columnIndex("species");
            int idbh_from = infile.columnIndex("dbh_from");
            int idbh_to = infile.columnIndex("dbh_to");
            int ihd = infile.columnIndex("hd");
            int iage = infile.columnIndex("age");
            int idensity = infile.columnIndex("density");
            if (icount < 0 || ispecies < 0 || idbh_from < 0 || idbh_to < 0 || ihd < 0 || iage < 0)
            {
                throw new NotSupportedException(String.Format("load-ini-file: file '{0}' containts not all required fields (count, species, dbh_from, dbh_to, hd, age).", fileName));
            }

            int istandid = infile.columnIndex("stand_id");
            mInitItems.Clear();
            mStandInitItems.Clear();

            InitFileItem item = new InitFileItem();
            bool ok;
            int total_count = 0;
            for (int row = 0; row < infile.rowCount(); row++)
            {
                item.count = Double.Parse(infile.value(row, icount));
                total_count += (int)item.count;
                item.dbh_from = Double.Parse(infile.value(row, idbh_from));
                item.dbh_to = Double.Parse(infile.value(row, idbh_to));
                item.hd = Double.Parse(infile.value(row, ihd));
                if (item.hd == 0.0 || item.dbh_from / 100.0 * item.hd < 4.0)
                {
                    Trace.TraceWarning(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from));
                }
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                ok = true;
                if (iage >= 0)
                {
                    ok = Int32.TryParse(infile.value(row, iage), out item.age);
                }
                if (iage < 0 || !ok)
                {
                    item.age = 0;
                }

                item.species = speciesSet.species(infile.value(row, ispecies));
                if (idensity >= 0)
                {
                    item.density = Double.Parse(infile.value(row, idensity));
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
                                                                  infile.value(row, ispecies), fileName, row));
                }
                if (istandid >= 0)
                {
                    int standid = Int32.Parse(infile.value(row, istandid));
                    mStandInitItems[standid].Add(item);
                }
                else
                {
                    mInitItems.Add(item);
                }
            }
            return total_count;
        }

        public int loadiLandFile(string fileName, ResourceUnit ru, int stand_id)
        {
            if (!File.Exists(fileName))
            {
                throw new NotSupportedException(String.Format("load-ini-file: file '{0}' does not exist.", fileName));
            }
            string content = Helper.loadTextFile(fileName);
            return loadDistributionList(content, ru, stand_id, fileName);
        }

        // sort function
        public int sortPairLessThan(MutableTuple<int, double> s1, MutableTuple<int, double> s2)
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

        private int sortInitPixelLessThan(SInitPixel s1, SInitPixel s2)
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

        private int sortInitPixelUnlocked(SInitPixel s1, SInitPixel s2)
        {
            if (!s1.locked && s2.locked)
            {
                return -1;
            }
            return 0;
        }

        private void executeiLandInit(ResourceUnit ru)
        {
            PointF offset = ru.boundingBox().TopLeft();
            Point offsetIdx = GlobalSettings.instance().model().grid().indexAt(offset);

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
                    int tree_idx = ru.newTreeIndex();
                    Tree tree = ru.trees()[tree_idx]; // get reference to modify tree
                    tree.setDbh((float)RandomGenerator.nrandom(item.dbh_from, item.dbh_to));
                    tree.setHeight(tree.dbh() / 100.0F * (float)item.hd); // dbh from cm->m, *hd-ratio -> meter height
                    tree.setSpecies(item.species);
                    if (item.age <= 0)
                    {
                        tree.setAge(0, tree.height());
                    }
                    else
                    {
                        tree.setAge(item.age, tree.height());
                    }
                    tree.setRU(ru);
                    tree.setup();
                    total_count++;

                    // calculate random value. "density" is from 1..-1.
                    rand_val = mRandom.get();
                    if (item.density < 0)
                    {
                        rand_val = 1.0 - rand_val;
                    }
                    rand_val = rand_val * rand_fraction + RandomGenerator.drandom() * (1.0 - rand_fraction);

                    // key: rank of target pixel
                    // first: index of target pixel
                    // second: sum of target pixel
                    key = Global.limit((int)(100.0 * rand_val), 0, 99); // get from random number generator
                    tree_map.Add(tcount[key].Item1, tree_idx); // store tree in map
                    MutableTuple<int, double> ruBA = tcount[key];
                    ruBA.Item2 += tree.basalArea(); // aggregate the basal area for each 10m pixel
                    if ((total_count < 20 && i % 2 == 0)
                        || (total_count < 100 && i % 10 == 0)
                        || (i % 30 == 0))
                    {
                        tcount.Sort(sortPairLessThan);
                    }
                }
                tcount.Sort(sortPairLessThan);
            }

            int bits, index, pos;
            Point tree_pos;
            for (int i = 0; i < 100; i++)
            {
                List<int> trees = tree_map[i].ToList();
                int c = trees.Count;
                PointF pixel_center = ru.boundingBox().TopLeft().Add(new PointF((i / 10) * 10.0F + 5.0F, (i % 10) * 10.0F + 5.0F));
                if (!mModel.heightGrid().valueAt(pixel_center).isValid())
                {
                    // no trees on that pixel: let trees die
                    foreach (int tree_idx in trees)
                    {
                        ru.trees()[tree_idx].die();
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
                            r = RandomGenerator.drandom();
                            index = Global.limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Global.isBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("executeiLandInit: found no free bit.");
                        }
                        Global.setBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    pos = ru.index() % 2 != 0 ? evenlist[index] : unevenlist[index];
                    // position of resource unit + position of 10x10m pixel + position within 10x10m pixel
                    tree_pos = new Point(offsetIdx.X + 5 * (i / 10) + pos / 5,
                                         offsetIdx.Y + 5 * (i % 10) + pos % 5);
                    //Debug.WriteLine(tree_no++ + "to" + index);
                    ru.trees()[tree_idx].setPosition(tree_pos);
                }
            }
        }

        // Initialization routine based on a stand map.
        // Basically a list of 10m pixels for a given stand is retrieved
        // and the filled with the same procedure as the resource unit based init
        // see http://iland.boku.ac.at/initialize+trees
        private void executeiLandInitStand(int stand_id)
        {
            MapGrid grid = GlobalSettings.instance().model().standGrid();
            if (mCurrentMap != null)
            {
                grid = mCurrentMap;
            }

            // get a list of positions of all pixels that belong to our stand
            List<int> indices = grid.gridIndices(stand_id);
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
                SInitPixel p = new SInitPixel();
                p.pixelOffset = grid.grid().indexOf(i); // index in the 10m grid
                p.resource_unit = GlobalSettings.instance().model().ru(grid.grid().cellCenterPoint(p.pixelOffset));
                if (mInitHeightGrid != null)
                {
                    p.h_max = mInitHeightGrid.grid().constValueAtIndex(p.pixelOffset);
                }
                pixel_list.Add(p);
            }
            double area_factor = grid.area(stand_id) / Constant.cRUArea;

            int key = 0;
            double rand_val, rand_fraction;
            int total_count = 0;
            int total_tries = 0;
            int total_misses = 0;
            if (mInitHeightGrid != null && mHeightGridResponse == null)
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
                            pixel_list[it].basal_area = RandomGenerator.drandom();
                        }
                        pixel_list.Sort(sortInitPixelLessThan);

                        for (int it = 0; it < pixel_list.Count; ++it)
                        {
                            pixel_list[it].basal_area = 0.0;
                        }
                    }

                    if (item.species != last_locked_species)
                    {
                        last_locked_species = item.species;
                        pixel_list.Sort(sortInitPixelUnlocked);
                    }
                }
                else
                {
                    pixel_list.Sort(sortInitPixelLessThan);
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
                            rand_val = mRandom.get();
                            if (item.density < 0)
                            {
                                rand_val = 1.0 - rand_val;
                            }
                            rand_val = rand_val * rand_fraction + RandomGenerator.drandom() * (1.0 - rand_fraction);
                        }
                        else
                        {
                            // limited area: limit potential area using the "density" input parameter
                            rand_val = RandomGenerator.drandom() * Math.Min(item.density / 100.0, 1.0);
                        }
                        ++total_tries;

                        // key: rank of target pixel
                        key = Global.limit((int)(pixel_list.Count * rand_val), 0, pixel_list.Count - 1); // get from random number generator

                        if (mInitHeightGrid != null)
                        {
                            // calculate how good the selected pixel fits w.r.t. the predefined height
                            double p_value = pixel_list[key].h_max > 0.0 ? mHeightGridResponse.calculate(init_max_height / pixel_list[key].h_max) : 0.0;
                            if (RandomGenerator.drandom() < p_value)
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
                    int tree_idx = ru.newTreeIndex();
                    Tree tree = ru.trees()[tree_idx]; // get reference to modify tree
                    tree.setDbh((float)RandomGenerator.nrandom(item.dbh_from, item.dbh_to));
                    tree.setHeight((float)(tree.dbh() / 100.0 * item.hd)); // dbh from cm->m, *hd-ratio -> meter height
                    tree.setSpecies(item.species);
                    if (item.age <= 0)
                    {
                        tree.setAge(0, tree.height());
                    }
                    else
                    {
                        tree.setAge(item.age, tree.height());
                    }
                    tree.setRU(ru);
                    tree.setup();
                    total_count++;

                    // store in the multiHash the position of the pixel and the tree_idx in the resepctive resource unit
                    tree_map.Add(pixel_list[key].pixelOffset, tree_idx);
                    pixel_list[key].basal_area += tree.basalArea(); // aggregate the basal area for each 10m pixel
                    if (last_locked_species != null)
                    {
                        pixel_list[key].locked = true;
                    }

                    // resort list
                    if (last_locked_species == null && ((total_count < 20 && i % 2 == 0) || (total_count < 100 && i % 10 == 0) || (i % 30 == 0)))
                    {
                        pixel_list.Sort(sortInitPixelLessThan);
                    }
                }
            }
            if (total_misses > 0 || total_tries > total_count)
            {
                if (GlobalSettings.instance().logLevelInfo())
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
                            r = RandomGenerator.drandom();
                            index = Global.limit((int)(25 * r * r), 0, 24); // use rnd()^2 to search for locations -> higher number of low indices (i.e. 50% of lookups in first 25% of locations)
                        }
                        while (Global.isBitSet(bits, index) == true && stop-- != 0);

                        if (stop == 0)
                        {
                            Debug.WriteLine("executeiLandInit: found no free bit.");
                        }
                        Global.setBit(ref bits, index, true); // mark position as used
                    }
                    // get position from fixed lists (one for even, one for uneven resource units)
                    int pos = p.resource_unit.index() % 2 != 0 ? evenlist[index] : unevenlist[index];
                    Point tree_pos = new Point(p.pixelOffset.X * Constant.cPxPerHeight + pos / Constant.cPxPerHeight, // convert to LIF index
                                               p.pixelOffset.Y * Constant.cPxPerHeight + pos % Constant.cPxPerHeight);

                    p.resource_unit.trees()[tree_idx].setPosition(tree_pos);
                    // test if tree position is valid..
                    if (!GlobalSettings.instance().model().grid().isIndexValid(tree_pos))
                    {
                        Debug.WriteLine("Standloader: invalid position!");
                    }
                }
            }
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("init for stand " + stand_id + " with area" + grid.area(stand_id) + " m2, count of 10m pixels: " + indices.Count + "initialized trees: " + total_count);
            }
        }

        /// a (hacky) way of adding saplings of a certain age to a stand defined by 'stand_id'.
        public int loadSaplings(string content, int stand_id, string fileName)
        {
            // Q_UNUSED(fileName);
            MapGrid stand_grid;
            if (mCurrentMap != null)
            {
                stand_grid = mCurrentMap; // if set
            }
            else
            {
                stand_grid = GlobalSettings.instance().model().standGrid(); // default
            }

            List<int> indices = stand_grid.gridIndices(stand_id); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + stand_id + " not in project area. No init performed.");
                return -1;
            }
            double area_factor = stand_grid.area(stand_id) / Constant.cRUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            CSVFile init = new CSVFile();
            init.loadFromString(content);
            int ispecies = init.columnIndex("species");
            int icount = init.columnIndex("count");
            int iheight = init.columnIndex("height");
            int iage = init.columnIndex("age");
            if (ispecies == -1 || icount == -1)
            {
                throw new NotSupportedException("Error while loading saplings: columns 'species' or 'count' are missing!!");
            }

            SpeciesSet set = GlobalSettings.instance().model().ru().speciesSet();
            double height, age;
            int total = 0;
            for (int row = 0; row < init.rowCount(); ++row)
            {
                int pxcount = (int)Math.Round(Double.Parse(init.value(row, icount)) * area_factor + 0.5); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.species(init.value(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.value(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.value(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.value(row, iage));

                int misses = 0;
                int hits = 0;
                while (hits < pxcount)
                {
                    // sapling location
                    int rnd_index = RandomGenerator.irandom(0, indices.Count);
                    Point offset = stand_grid.grid().indexOf(indices[rnd_index]);
                    offset.X *= Constant.cPxPerHeight; // index of 10m patch -> to lif pixel coordinates
                    offset.Y *= Constant.cPxPerHeight;
                    int in_p = RandomGenerator.irandom(0, Constant.cPxPerHeight * Constant.cPxPerHeight); // index of lif-pixel
                    offset.X += in_p / Constant.cPxPerHeight;
                    offset.Y += in_p % Constant.cPxPerHeight;

                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.instance().model().saplings().cell(offset, true, ref ru);
                    if (sc != null && sc.max_height() > height)
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
                            sc.addSapling((float)height, (int)age, species.index());
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

        private int LIFValueHigher(KeyValuePair<int, float> a, KeyValuePair<int, float> b)
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

        public int loadSaplingsLIF(int stand_id, CSVFile init, int low_index, int high_index)
        {
            MapGrid stand_grid;
            if (mCurrentMap != null)
            {
                stand_grid = mCurrentMap; // if set
            }
            else
            {
                stand_grid = GlobalSettings.instance().model().standGrid(); // default
            }

            if (!stand_grid.isValid(stand_id))
            {
                return 0;
            }

            List<int> indices = stand_grid.gridIndices(stand_id); // list of 10x10m pixels
            if (indices.Count == 0)
            {
                Debug.WriteLine("stand " + stand_id + " not in project area. No init performed.");
                return 0;
            }
            // prepare space for LIF-pointers (2m Pixel)
            List<KeyValuePair<int, float>> lif_ptrs = new List<KeyValuePair<int, float>>(indices.Count * Constant.cPxPerHeight * Constant.cPxPerHeight);
            Grid<float> modelGrid = GlobalSettings.instance().model().grid();
            for (int l = 0; l < indices.Count; ++l)
            {
                Point offset = stand_grid.grid().indexOf(indices[l]);
                offset.X *= Constant.cPxPerHeight; // index of 10m patch -> to lif pixel coordinates
                offset.Y *= Constant.cPxPerHeight;
                for (int y = 0; y < Constant.cPxPerHeight; ++y)
                {
                    for (int x = 0; x < Constant.cPxPerHeight; ++x)
                    {
                        int modelIndex = modelGrid.index(offset.X + x, offset.Y + y);
                        KeyValuePair<int, float> indexAndValue = new KeyValuePair<int, float>(modelIndex, modelGrid[modelIndex]);
                        lif_ptrs.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lif_ptrs.Sort(LIFValueHigher); // higher: highest values first

            double area_factor = stand_grid.area(stand_id) / Constant.cRUArea; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            // parse the content of the init-file
            // species
            int ispecies = init.columnIndex("species");
            int icount = init.columnIndex("count");
            int iheight = init.columnIndex("height");
            int iheightfrom = init.columnIndex("height_from");
            int iheightto = init.columnIndex("height_to");
            int iage = init.columnIndex("age");
            int itopage = init.columnIndex("age4m");
            int iminlif = init.columnIndex("min_lif");
            if ((iheightfrom == -1) ^ (iheightto == -1))
            {
                throw new NotSupportedException("Error while loading saplings: height not correctly provided. Use either 'height' or 'height_from' and 'height_to'.");
            }
            if (ispecies == -1 || icount == -1)
            {
                throw new NotSupportedException("Error while loading saplings: columns 'species' or 'count' are missing!!");
            }

            SpeciesSet set = GlobalSettings.instance().model().ru().speciesSet();
            double height, age;
            int total = 0;
            for (int row = low_index; row <= high_index; ++row)
            {
                int pxcount = (int)(Double.Parse(init.value(row, icount)) * area_factor); // no. of pixels that should be filled (sapling grid is the same resolution as the lif-grid)
                Species species = set.species(init.value(row, ispecies));
                if (species == null)
                {
                    throw new NotSupportedException(String.Format("Error while loading saplings: invalid species '{0}'.", init.value(row, ispecies)));
                }
                height = iheight == -1 ? 0.05 : Double.Parse(init.value(row, iheight));
                age = iage == -1 ? 1 : Double.Parse(init.value(row, iage));
                double age4m = itopage == -1 ? 10 : Double.Parse(init.value(row, itopage));
                double height_from = iheightfrom == -1 ? -1.0 : Double.Parse(init.value(row, iheightfrom));
                double height_to = iheightto == -1 ? -1.0 : Double.Parse(init.value(row, iheightto));
                double min_lif = iminlif == -1 ? 1.0 : Double.Parse(init.value(row, iminlif));
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
                    int rnd_index = RandomGenerator.irandom(0, min_lif_index);
                    if (iheightfrom != -1)
                    {
                        height = Global.limit(RandomGenerator.nrandom(height_from, height_to), 0.05, 4.0);
                        if (age <= 1.0)
                        {
                            age = Math.Max(Math.Round(height / 4.0 * age4m), 1.0); // assume a linear relationship between height and age
                        }
                    }
                    Point offset = modelGrid.indexOf(lif_ptrs[rnd_index].Key);
                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.instance().model().saplings().cell(offset, true, ref ru);
                    if (sc != null)
                    {
                        SaplingTree st = sc.addSapling((float)height, (int)age, species.index());
                        if (st != null)
                        {
                            hits += Math.Max(1.0, ru.resourceUnitSpecies(st.species_index).species().saplingGrowthParameters().representedStemNumberH(st.height));
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
            if (init.columnIndex("grass_cover") > -1)
            {
                int grass_cover_value = Int32.Parse(init.value(low_index, "grass_cover"));
                if (grass_cover_value < 0 || grass_cover_value > 100)
                {
                    throw new NotSupportedException(String.Format("The grass cover percentage (column 'grass_cover') for stand '{0}' is '{1}', which is invalid (expected: 0-100)", stand_id, grass_cover_value));
                }
                GlobalSettings.instance().model().grassCover().setInitialValues(lif_ptrs, grass_cover_value);
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
