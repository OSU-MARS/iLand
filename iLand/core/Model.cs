using iLand.abe;
using iLand.output;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace iLand.core
{
    internal class Model : IDisposable
    {
        private static ModelSettings mSettings;

        private ThreadRunner threadRunner;
        private bool mSetup;
        /// container holding all ressource units
        private List<ResourceUnit> mRU;
        /// grid specifying a map of ResourceUnits
        private Grid<ResourceUnit> mRUmap;
        /// container holding all species sets
        private List<SpeciesSet> mSpeciesSets;
        /// container holding all the climate objects
        private List<Climate> mClimates;
        //
        private Modules mModules; ///< the list of modules/plugins
        //
        private RectangleF mModelRect; ///< extent of the model (without buffer)
        private double mTotalStockableArea; ///< total stockable area (ha)
        // global grids...
        private Grid<float> mGrid; ///< the main LIF grid of the model (2x2m resolution)
        private Grid<HeightGridValue> mHeightGrid; ///< grid with 10m resolution that stores maximum-heights, tree counts and some flags
        private Saplings mSaplings;
        private Management mManagement; ///< management sub-module (simple mode)
        private ForestManagementEngine mABEManagement; ///< management sub-module (agent based management engine)
        private Environment mEnvironment; ///< definition of paramter values on resource unit level (modify the settings tree)
        private TimeEvents mTimeEvents; ///< sub module to handle predefined events in time (modifies the settings tree in time)
        private MapGrid mStandGrid; ///< map of the stand map (10m resolution)
        // Digital elevation model
        private DEM mDEM; ///< digital elevation model
        private GrassCover mGrassCover; ///< cover of the ground with grass / herbs
        private bool isDisposed;

        public Model()
        {
            initialize();
            GlobalSettings.instance().setModel(this); // BUGBUG: many to one set
            GlobalSettings.instance().resetScriptEngine(); // clear the script
            Debug.WriteLine("extended debug checks disabled.");
        }

        public bool multithreading() { return threadRunner.multithreading(); }

        // access to elements
        public ThreadRunner threadExec() {return threadRunner; }
        public RectangleF extent() { return mModelRect; } ///< extent of the model (without buffer)
        public double totalStockableArea() { return mTotalStockableArea; } ///< total stockable area of the landscape (ha)

        public List<ResourceUnit> ruList() {return mRU; }
        public Management management() { return mManagement; }
        public ForestManagementEngine ABEngine() { return mABEManagement; }
        public Environment environment() {return mEnvironment; }
        public Saplings saplings() {return mSaplings; }
        public TimeEvents timeEvents() { return mTimeEvents; }
        public Modules modules() { return mModules; }
        public DEM dem() { return mDEM; }
        public GrassCover grassCover() { return mGrassCover; }
        public List<Climate> climates() { return mClimates; }

        public bool isSetup() { return mSetup; } ///< return true if the model world is correctly setup.
        public ModelSettings settings() { return mSettings; } ///< access to global model settings.
        public ModelSettings changeSettings() { return mSettings; } ///< write access to global model settings.

        // global grids
        public Grid<float> grid() { return mGrid; } ///< this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public Grid<HeightGridValue> heightGrid() { return mHeightGrid; } ///< stores maximum heights of trees and some flags (currently 10x10m)
        public MapGrid standGrid() { return mStandGrid; } ///< retrieve the spatial grid that defines the stands (10m resolution)
        public Grid<ResourceUnit> RUgrid() { return mRUmap; }

        // start/stop/run
        public void afterStop() ///< finish and cleanup
        {
            // do some cleanup
            // no op in C++ iLand sources
        }

        /// beforeRun performs several steps before the models starts running.
        /// inter alia: * setup of the stands
        ///             * setup of the climates
        public void beforeRun() ///< initializations
        {
            // setup outputs
            // setup output database
            if (GlobalSettings.instance().dbout().State != ConnectionState.Closed)
            {
                GlobalSettings.instance().dbout().Close();
            }
            initOutputDatabase();
            GlobalSettings.instance().outputManager().setup();
            GlobalSettings.instance().clearDebugLists();

            // initialize stands
            StandLoader loader = new StandLoader(this);
            {
                using DebugTimer loadtrees = new DebugTimer("load trees");
                loader.processInit();
            }
            // initalization of ABE
            if (mABEManagement != null)
            {
                mABEManagement.setup();
                mABEManagement.runOnInit(true);
            }

            // load climate
            {
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("attempting to load climate...");
                }
                using DebugTimer loadclim = new DebugTimer("load climate");
                foreach (Climate c in mClimates)
                {
                    if (!c.isSetup())
                    {
                        c.setup();
                    }
                }
                // load the first year of the climate database
                foreach (Climate c in mClimates)
                {
                    c.nextYear();
                }
            }

            {
                using DebugTimer loadinit = new DebugTimer("load standstatistics");
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("attempting to calculate initial stand statistics (incl. apply and read pattern)...");
                }
                Tree.setGrid(mGrid, mHeightGrid);
                // debugCheckAllTrees(); // introduced for debugging session (2012-04-06)
                applyPattern();
                readPattern();
                loader.processAfterInit(); // e.g. initialization of saplings

                // force the compilation of initial stand statistics
                createStandStatistics();
            }

            // initalization of ABE (now all stands are properly set up)
            if (mABEManagement != null)
            {
                mABEManagement.initialize();
                mABEManagement.runOnInit(false);
            }

            // outputs to create with inital state (without any growth) are called here:
            GlobalSettings.instance().setCurrentYear(0); // set clock to "0" (for outputs with initial state)

            GlobalSettings.instance().outputManager().execute("stand"); // year=0
            GlobalSettings.instance().outputManager().execute("landscape"); // year=0
            GlobalSettings.instance().outputManager().execute("sapling"); // year=0
            GlobalSettings.instance().outputManager().execute("saplingdetail"); // year=0
            GlobalSettings.instance().outputManager().execute("tree"); // year=0
            GlobalSettings.instance().outputManager().execute("dynamicstand"); // year=0

            GlobalSettings.instance().setCurrentYear(1); // set to first year
        }

        /** Main model runner.
          The sequence of actions is as follows:
          (1) Load the climate of the new year
          (2) Reset statistics for resource unit as well as for dead/managed trees
          (3) Invoke Management.
          (4) *after* that, calculate Light patterns
          (5) 3PG on stand level, tree growth. Clear stand-statistcs before they are filled by single-tree-growth. calculate water cycle (with LAIs before management)
          (6) execute Regeneration
          (7) invoke disturbance modules
          (8) calculate carbon cycle
          (9) calculate statistics for the year
          (10) write database outputs
          */
        public void runYear() ///< run a single year
        {
            using DebugTimer t = new DebugTimer("Model.runYear()");
            GlobalSettings.instance().systemStatistics().reset();
            RandomGenerator.checkGenerator(); // see if we need to generate new numbers...
                                              // initalization at start of year for external modules
            mModules.yearBegin();

            // execute scheduled events for the current year
            if (mTimeEvents != null)
            {
                mTimeEvents.run();
            }
            // load the next year of the climate database (except for the first year - the first climate year is loaded immediately
            if (GlobalSettings.instance().currentYear() > 1)
            {
                foreach (Climate c in mClimates)
                {
                    c.nextYear();
                }
            }

            // reset statistics
            foreach (ResourceUnit ru in mRU)
            {
                ru.newYear();
            }

            foreach (SpeciesSet set in mSpeciesSets)
            {
                set.newYear();
            }
            // management classic
            if (mManagement != null)
            {
                using DebugTimer t2 = new DebugTimer("management");
                mManagement.run();
                GlobalSettings.instance().systemStatistics().tManagement += t.elapsed();
            }
            // ... or ABE (the agent based variant)
            if (mABEManagement != null)
            {
                using DebugTimer t3 = new DebugTimer("ABE:run");
                mABEManagement.run();
                GlobalSettings.instance().systemStatistics().tManagement += t.elapsed();
            }

            // if trees are dead/removed because of management, the tree lists
            // need to be cleaned (and the statistics need to be recreated)
            cleanTreeLists(true); // recalculate statistics (LAIs per species needed later in production)

            // process a cycle of individual growth
            applyPattern(); // create Light Influence Patterns
            readPattern(); // readout light state of individual trees
            grow(); // let the trees grow (growth on stand-level, tree-level, mortality)
            mGrassCover.execute(); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (settings().regenerationEnabled)
            {
                // seed dispersal
                using DebugTimer tseed = new DebugTimer("Seed dispersal, establishment, sapling growth");
                foreach (SpeciesSet set in mSpeciesSets)
                {
                    set.regeneration(); // parallel execution for each species set
                }
                GlobalSettings.instance().systemStatistics().tSeedDistribution += tseed.elapsed();
                // establishment
                Saplings.updateBrowsingPressure();

                {
                    using DebugTimer t2 = new DebugTimer("establishment");
                    executePerResourceUnit(nc_establishment, false /* true: force single threaded operation */);
                    GlobalSettings.instance().systemStatistics().tEstablishment += t.elapsed();
                }
                {
                    using DebugTimer t3 = new DebugTimer("sapling growth");
                    executePerResourceUnit(nc_sapling_growth, false /* true: force single threaded operation */);
                    GlobalSettings.instance().systemStatistics().tSapling += t.elapsed();
                }

                // Establishment.debugInfo(); // debug test

            }

            // external modules/disturbances
            mModules.run();
            // cleanup of tree lists if external modules removed trees.
            cleanTreeLists(false); // do not recalculate statistics - this is done in ru.yearEnd()


            // calculate soil / snag dynamics
            if (settings().carbonCycleEnabled)
            {
                using DebugTimer ccycle = new DebugTimer("carbon cylce");
                executePerResourceUnit(nc_carbonCycle, false /* true: force single threaded operation */);
                GlobalSettings.instance().systemStatistics().tCarbonCycle += ccycle.elapsed();
            }

            using DebugTimer toutput = new DebugTimer("outputs");
            // calculate statistics
            foreach (ResourceUnit ru in mRU)
            {
                ru.yearEnd();
            }
            // create outputs
            OutputManager om = GlobalSettings.instance().outputManager();
            om.execute("tree"); // single tree output
            om.execute("treeremoval"); // single removed tree output
            om.execute("stand"); //resource unit level x species
            om.execute("landscape"); //landscape x species
            om.execute("landscape_removed"); //removed trees on landscape x species
            om.execute("sapling"); // sapling layer per RU x species
            om.execute("saplingdetail"); // individual sapling cohorts (per RU)
            om.execute("production_month"); // 3pg responses growth per species x RU x month
            om.execute("dynamicstand"); // output with user-defined columns (based on species x RU)
            om.execute("standdead"); // resource unit level x species
            om.execute("management"); // resource unit level x species
            om.execute("carbon"); // resource unit level, carbon pools above and belowground
            om.execute("carbonflow"); // resource unit level, GPP, NPP and total carbon flows (atmosphere, harvest, ...)
            om.execute("water"); // resource unit/landscape level water output (ET, rad, snow cover, ...)

            GlobalSettings.instance().systemStatistics().tWriteOutput += toutput.elapsed();
            GlobalSettings.instance().systemStatistics().tTotalYear += t.elapsed();
            GlobalSettings.instance().systemStatistics().writeOutput();

            // global javascript event
            GlobalSettings.instance().executeJSFunction("onYearEnd");

            GlobalSettings.instance().setCurrentYear(GlobalSettings.instance().currentYear() + 1);

            // try to clean up a bit of memory (useful if many large JS objects (e.g., grids) are used)
            GlobalSettings.instance().scriptEngine().collectGarbage();
        }

        // setup/maintenance
        /** clear() frees all ressources allocated with the run of a simulation.
          */
        public void clear() ///< free resources
        {
            mSetup = false;
            Debug.WriteLine("Model clear: attempting to clear " + mRU.Count + "RU, " + mSpeciesSets.Count + " SpeciesSets.");
            
            mRU.Clear();
            mSpeciesSets.Clear();
            mClimates.Clear();

            mGrid = null;
            mHeightGrid = null;
            mManagement = null;
            mEnvironment = null;
            mTimeEvents = null;
            mStandGrid = null;
            mModules = null;
            mDEM = null;
            mGrassCover = null;
            mABEManagement = null;

            GlobalSettings.instance().outputManager().close();

            Debug.WriteLine("Model resources freed.");
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    clear();
                    GlobalSettings.instance().setModel(null); // BUGBUG
                }
                this.isDisposed = true;
            }
        }

        public void onlyApplyLightPattern() 
        { 
            applyPattern(); 
            readPattern();
        }

        /** Setup of the Simulation.
          This really creates the simulation environment and does the setup of various aspects.
          */
        public void loadProject() ///< setup and load a project
        {
            DebugTimer dt = new DebugTimer("load project");
            GlobalSettings g = GlobalSettings.instance();
            g.printDirectories();
            XmlHelper xml = g.settings();

            g.clearDatabaseConnections();
            // database connections: reset
            GlobalSettings.instance().clearDatabaseConnections();
            // input and climate connection
            // see initOutputDatabase() for output database
            string dbPath = g.path(xml.value("system.database.in"), "database");
            GlobalSettings.instance().setupDatabaseConnection("in", dbPath, true);
            dbPath = g.path(xml.value("system.database.climate"), "database");
            GlobalSettings.instance().setupDatabaseConnection("climate", dbPath, true);

            mSettings.loadModelSettings();
            mSettings.print();
            // random seed: if stored value is <> 0, use this as the random seed (and produce hence always an equal sequence of random numbers)
            int seed = Int32.Parse(xml.value("system.settings.randomSeed", "0"));
            RandomGenerator.setup(RandomGenerator.ERandomGenerators.ergMersenneTwister, seed); // use the MersenneTwister as default
                                                                                               // linearization of expressions: if true *and* linearize() is explicitely called, then
                                                                                               // function results will be cached over a defined range of values.
            bool do_linearization = xml.valueBool("system.settings.expressionLinearizationEnabled", false);
            Expression.setLinearizationEnabled(do_linearization);
            if (do_linearization)
            {
                Debug.WriteLine("The linearization of expressions is enabled (performance optimization).");
            }

            // log level
            string log_level = xml.value("system.settings.logLevel", "debug").ToLowerInvariant();
            if (log_level == "debug") GlobalSettings.instance().setLogLevel(0);
            if (log_level == "info") GlobalSettings.instance().setLogLevel(1);
            if (log_level == "warning") GlobalSettings.instance().setLogLevel(2);
            if (log_level == "error") GlobalSettings.instance().setLogLevel(3);

            // snag dynamics / soil model enabled? (info used during setup of world)
            changeSettings().carbonCycleEnabled = xml.valueBool("model.settings.carbonCycleEnabled", false);
            // class size of snag classes
            Snag.setupThresholds(xml.valueDouble("model.settings.soil.swdDBHClass12"),
                                 xml.valueDouble("model.settings.soil.swdDBHClass23"));

            // setup of modules
            mModules = new Modules();

            changeSettings().regenerationEnabled = xml.valueBool("model.settings.regenerationEnabled", false);


            setupSpace();
            if (mRU.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            // (3) additional issues
            // (3.1) load javascript code into the engine
            string script_file = xml.value("system.javascript.fileName");
            if (String.IsNullOrEmpty(script_file) == false)
            {
                script_file = g.path(script_file, "script");
                ScriptGlobal.loadScript(script_file);
                g.controller().setLoadedJavascriptFile(script_file);
            }

            // (3.2) setup of regeneration
            if (settings().regenerationEnabled)
            {
                foreach (SpeciesSet ss in mSpeciesSets)
                {
                    ss.setupRegeneration();
                }
            }
            Saplings.setRecruitmentVariation(xml.valueDouble("model.settings.seedDispersal.recruitmentDimensionVariation", 0.1));

            // (3.3) management
            bool use_abe = xml.valueBool("model.management.abeEnabled");
            if (use_abe)
            {
                // use the agent based forest management engine
                mABEManagement = new ForestManagementEngine();
                // setup of ABE after loading of trees.

            }
            // use the standard management
            string mgmtFile = xml.value("model.management.file");
            if (xml.valueBool("model.management.enabled"))
            {
                mManagement = new Management();
                string path = GlobalSettings.instance().path(mgmtFile, "script");
                mManagement.loadScript(path);
                Debug.WriteLine("setup management using script" + path);
            }
        }

        public void reloadABE() ///< force a recreate of the agent based forest management engine
        {
            // delete firest
            mABEManagement = new ForestManagementEngine();
            // and setup
            mABEManagement.setup();
            mABEManagement.runOnInit(true);

            mABEManagement.initialize();
            mABEManagement.runOnInit(false);
        }

        /// get the value of the (10m) Height grid at the position index ix and iy (of the LIF grid)
        public HeightGridValue heightGridValue(int ix, int iy)
        {
            return mHeightGrid.constValueAtIndex(ix / Constant.cPxPerHeight, iy / Constant.cPxPerHeight);
        }

        // unused in C++
        //public HeightGridValue heightGridValue(float lif_ptr)
        //{
        //    Point p = mGrid.indexOf(lif_ptr);
        //    return mHeightGrid.constValueAtIndex(p.X / Constant.cPxPerHeight, p.Y / Constant.cPxPerHeight);
        //}

        public SpeciesSet speciesSet()
        {
            if (mSpeciesSets.Count == 1)
            {
                return mSpeciesSets[0];
            }
            return null;
        }

        // actions
        /// build stand statistics (i.e. stats based on resource units)
        public void createStandStatistics()
        {
            calculateStockedArea();
            foreach (ResourceUnit ru in mRU)
            {
                ru.addTreeAgingForAllTrees();
                ru.createStandStatistics();
            }
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        public void cleanTreeLists(bool recalculate_stats)
        {
            foreach (ResourceUnit ru in GlobalSettings.instance().model().ruList())
            {
                if (ru.hasDiedTrees())
                {
                    ru.cleanTreeList();
                    ru.recreateStandStatistics(recalculate_stats);
                }
            }
        }

        /// execute a function for each resource unit using multiple threads. "funcptr" is a ptr to a simple function
        public void executePerResourceUnit(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false) 
        { 
            threadRunner.run(funcptr, forceSingleThreaded); 
        }

        private void initialize() ///< basic startup without creating a simulation
        {
            mSetup = false;
            GlobalSettings.instance().setCurrentYear(0); // BUGBUG
            mGrid = null;
            mHeightGrid = null;
            mManagement = null;
            mABEManagement = null;
            mEnvironment = null;
            mTimeEvents = null;
            mStandGrid = null;
            mModules = null;
            mDEM = null;
            mGrassCover = null;
            mSaplings = null;
        }

        private void setupSpace() ///< setup the "world"(spatial grids, ...), create ressource units
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.world"));
            float cellSize = (float)xml.valueDouble("cellSize", 2.0);
            float width = (float)xml.valueDouble("width", 100.0);
            float height = (float)xml.valueDouble("height", 100.0);
            float buffer = (float)xml.valueDouble("buffer", 60.0);
            mModelRect = new RectangleF(0.0F, 0.0F, width, height);

            Debug.WriteLine(String.Format("setup of the world: {0}x{1}m with cell-size={2}m and {3]m buffer", width, height, cellSize, buffer));

            RectangleF total_grid = new RectangleF(new PointF(-buffer, -buffer), new SizeF(width + buffer, height + buffer));
            Debug.WriteLine("setup grid rectangle: " + total_grid);

            mGrid = new Grid<float>(total_grid, cellSize);
            mGrid.initialize(1.0F);
            mHeightGrid = new Grid<HeightGridValue>(total_grid, cellSize * Constant.cPxPerHeight);
            mHeightGrid.wipe(); // set all to zero
            Tree.setGrid(mGrid, mHeightGrid);

            // setup the spatial location of the project area
            if (xml.hasNode("location"))
            {
                // setup of spatial location
                double loc_x = xml.valueDouble("location.x");
                double loc_y = xml.valueDouble("location.y");
                double loc_z = xml.valueDouble("location.z");
                double loc_rot = xml.valueDouble("location.rotation");
                GisGrid.setupGISTransformation(loc_x, loc_y, loc_z, loc_rot);
                Debug.WriteLine("setup of spatial location: x/y/z" + loc_x + loc_y + loc_z + "rotation:" + loc_rot);
            }
            else
            {
                GisGrid.setupGISTransformation(0.0, 0.0, 0.0, 0.0);
            }

            // load environment (multiple climates, speciesSets, ...
            mEnvironment = new Environment();

            if (xml.valueBool("environmentEnabled", false))
            {
                string env_file = GlobalSettings.instance().path(xml.value("environmentFile"));
                bool grid_mode = (xml.value("environmentMode") == "grid");
                string grid_file = GlobalSettings.instance().path(xml.value("environmentGrid"));
                if (grid_mode)
                {
                    if (File.Exists(grid_file) && String.IsNullOrEmpty(xml.value("environmentGrid")) == false)
                    {
                        mEnvironment.setGridMode(grid_file);
                    }
                    else
                    {
                        throw new NotSupportedException(String.Format("File '{0}' specified in key 'environmentGrid' does not exist ('environmentMode' is 'grid').", grid_file));
                    }
                }

                if (!mEnvironment.loadFromFile(env_file))
                {
                    return;
                }
            }
            else
            {
                // load and prepare default values
                // (2) SpeciesSets: currently only one a global species set.
                SpeciesSet speciesSet = new SpeciesSet();
                mSpeciesSets.Add(speciesSet);
                speciesSet.setup();
                // Climate...
                Climate c = new Climate();
                mClimates.Add(c);
                mEnvironment.setDefaultValues(c, speciesSet);
            } // environment?

            // time series data
            if (xml.valueBool(".timeEventsEnabled", false))
            {
                mTimeEvents = new TimeEvents();
                mTimeEvents.loadFromFile(GlobalSettings.instance().path(xml.value("timeEventsFile"), "script"));
            }

            // simple case: create resource units in a regular grid.
            if (xml.valueBool("resourceUnitsAsGrid"))
            {
                mRUmap.setup(new RectangleF(0.0F, 0.0F, width, height), 100.0); // Grid, that holds positions of resource units
                mRUmap.wipe();

                bool mask_is_setup = false;
                if (xml.valueBool("standGrid.enabled"))
                {
                    string fileName = GlobalSettings.instance().path(xml.value("standGrid.fileName"));
                    mStandGrid = new MapGrid(fileName, false); // create stand grid index later

                    if (mStandGrid.isValid())
                    {
                        for (int i = 0; i < mStandGrid.grid().count(); i++)
                        {
                            int grid_value = mStandGrid.grid().constValueAtIndex(i);
                            mHeightGrid.valueAtIndex(i).setValid(grid_value > -1);
                            // BUGBUG: unclear why this is present in C++, appears removable
                            //if (grid_value > -1)
                            //{
                            //    mRUmap[mStandGrid.grid().cellCenterPoint(i)] = (ResourceUnit)1;
                            //}
                            if (grid_value < -1)
                            {
                                mHeightGrid.valueAtIndex(i).setForestOutside(true);
                            }
                        }
                    }
                    mask_is_setup = true;
                }
                else
                {
                    if (!settings().torusMode)
                    {
                        // in the case we have no stand grid but only a large rectangle (without the torus option)
                        // we assume a forest outside
                        for (int i = 0; i < mHeightGrid.count(); ++i)
                        {
                            PointF p = mHeightGrid.cellCenterPoint(mHeightGrid.indexOf(i));
                            if (p.X < 0.0F || p.X > width || p.Y < 0.0F || p.Y > height)
                            {
                                mHeightGrid.valueAtIndex(i).setForestOutside(true);
                                mHeightGrid.valueAtIndex(i).setValid(false);
                            }
                        }
                    }
                }

                int ru_index = 0;
                for (int p = 0; p < mRUmap.count(); ++p)
                {
                    RectangleF r = mRUmap.cellRect(mRUmap.indexOf(p));
                    if (mStandGrid == null || !mStandGrid.isValid())
                    {
                        mEnvironment.setPosition(r.Center()); // if environment is 'disabled' default values from the project file are used.
                                                               // create resource units for valid positions only
                        ResourceUnit new_ru = new ResourceUnit(ru_index++); // create resource unit
                        new_ru.setClimate(mEnvironment.climate());
                        new_ru.setSpeciesSet(mEnvironment.speciesSet());
                        new_ru.setup();
                        new_ru.setID(mEnvironment.currentID()); // set id of resource unit in grid mode
                        new_ru.setBoundingBox(r);
                        mRU.Add(new_ru);
                        mRUmap[p] = new_ru; // save in the RUmap grid
                    }
                }
                if (mEnvironment != null)
                {
                    // retrieve species sets and climates (that were really used)
                    mSpeciesSets.AddRange(mEnvironment.speciesSetList());
                    mClimates.AddRange(mEnvironment.climateList());
                    StringBuilder climate_file_list = new StringBuilder();
                    for (int i = 0, c = 0; i < mClimates.Count; ++i)
                    {
                        climate_file_list.Append(mClimates[i].name() + ", ");
                        if (++c > 5)
                        {
                            climate_file_list.Append("...");
                            break;
                        }

                    }
                    Debug.WriteLine("Setup of climates: #loaded: " + mClimates.Count + " tables: " + climate_file_list);
                }

                Debug.WriteLine("setup of " + mEnvironment.climateList().Count + " climates performed.");

                if (mStandGrid != null && mStandGrid.isValid())
                {
                    mStandGrid.createIndex();
                }
                // now store the pointers in the grid.
                // Important: This has to be done after the mRU-QList is complete - otherwise pointers would
                // point to invalid memory when QList's memory is reorganized (expanding)
                //        ru_index = 0;
                //        for (p=mRUmap.begin();p!=mRUmap.end(); ++p) {
                //            *p = mRU.value(ru_index++);
                //        }
                Debug.WriteLine("created a grid of ResourceUnits: count=" + mRU.Count + "number of RU-map-cells:" + mRUmap.count());


                calculateStockableArea();

                // setup of the project area mask
                if (!mask_is_setup && xml.valueBool("areaMask.enabled", false) && xml.hasNode("areaMask.imageFile"))
                {
                    // to be extended!!! e.g. to load ESRI-style text files....
                    // setup a grid with the same size as the height grid...
                    Grid<float> tempgrid = new Grid<float>((int) mHeightGrid.cellsize(), mHeightGrid.sizeX(), mHeightGrid.sizeY());
                    string fileName = GlobalSettings.instance().path(xml.value("areaMask.imageFile"));
                    Grid.loadGridFromImage(fileName, tempgrid); // fetch from image
                    for (int i = 0; i < tempgrid.count(); i++)
                    {
                        mHeightGrid.valueAtIndex(i).setValid(tempgrid.valueAtIndex(i) > 0.99);
                    }
                    Debug.WriteLine("loaded project area mask from" + fileName);
                }

                // list of "valid" resource units
                List<ResourceUnit> valid_rus = new List<ResourceUnit>();
                foreach (ResourceUnit ru in mRU)
                {
                    if (ru.id() != -1)
                    {
                        valid_rus.Add(ru);
                    }
                }

                // setup of the digital elevation map (if present)
                string dem_file = xml.value("DEM");
                if (String.IsNullOrEmpty(dem_file) == false)
                {
                    mDEM = new DEM(GlobalSettings.instance().path(dem_file));
                    // add them to the visuals...
                    GlobalSettings.instance().controller().addGrid(mDEM, "DEM height", GridViewType.GridViewRainbow, 0, 1000);
                    GlobalSettings.instance().controller().addGrid(mDEM.slopeGrid(), "DEM slope", GridViewType.GridViewRainbow, 0, 3);
                    GlobalSettings.instance().controller().addGrid(mDEM.aspectGrid(), "DEM aspect", GridViewType.GridViewRainbow, 0, 360);
                    GlobalSettings.instance().controller().addGrid(mDEM.viewGrid(), "DEM view", GridViewType.GridViewGray, 0, 1);
                }

                // setup of saplings
                if (mSaplings != null)
                {
                    mSaplings = null;
                }
                if (settings().regenerationEnabled)
                {
                    mSaplings = new Saplings();
                    mSaplings.setup();
                }

                // setup of the grass cover
                if (mGrassCover == null)
                {
                    mGrassCover = new GrassCover();
                }
                mGrassCover.setup();

                // setup of external modules
                mModules.setup();
                if (mModules.hasSetupResourceUnits())
                {
                    for (int index = 0; index < mRUmap.count(); ++index)
                    {
                        ResourceUnit p = mRUmap[index];
                        if (p != null)
                        {
                            RectangleF r = mRUmap.cellRect(mRUmap.indexOf(p));
                            mEnvironment.setPosition(r.Center()); // if environment is 'disabled' default values from the project file are used.
                            mModules.setupResourceUnit(p);
                        }
                    }
                }

                // setup of scripting environment
                ScriptGlobal.setupGlobalScripting();

                // setup the helper that does the multithreading
                threadRunner.setup(valid_rus);
                threadRunner.setMultithreading(GlobalSettings.instance().settings().valueBool("system.settings.multithreading"));
                threadRunner.print();


            }
            else
            {
                throw new NotSupportedException("resourceUnitsAsGrid MUST be set to true - at least currently :)");
            }
            mSetup = true;
        }

        private void initOutputDatabase() ///< setup output database (run metadata, ...)
        {
            GlobalSettings g = GlobalSettings.instance();
            string dbPath = g.path(g.settings().value("system.database.out"), "output");
            // create run-metadata
            int maxid = (int)SqlHelper.queryValue("select max(id) from runs", g.dbin());

            maxid++;
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_hhmmss");
            SqlHelper.executeSql(String.Format("insert into runs (id, timestamp) values ({0}, '{1}')", maxid, timestamp), g.dbin());
            // replace path information
            dbPath.Replace("$id$", maxid.ToString(), StringComparison.Ordinal);
            dbPath.Replace("$date$", timestamp, StringComparison.Ordinal);
            // setup final path
            g.setupDatabaseConnection("out", dbPath, false);
        }

        private void applyPattern() ///< apply LIP-patterns of all trees
        {
            using DebugTimer t = new DebugTimer("applyPattern()");
            // intialize grids...
            initializeGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int h = 0; h < mHeightGrid.count(); ++h)
            {
                mHeightGrid[h].resetCount(); // set count = 0, but do not touch the flags
                mHeightGrid[h].height = 4.0F;
            }

            threadRunner.run(nc_applyPattern);
            GlobalSettings.instance().systemStatistics().tApplyPattern += t.elapsed();
        }

        private void readPattern() ///< retrieve LRI for trees
        {
            using DebugTimer t = new DebugTimer("readPattern()");
            threadRunner.run(nc_readPattern);
            GlobalSettings.instance().systemStatistics().tReadPattern += t.elapsed();
        }

        /** Main function for the growth of stands and trees.
           This includes several steps.
           (1) calculate the stocked area (i.e. count pixels in height grid)
           (2) 3PG production (including response calculation, water cycle)
           (3) single tree growth (including mortality)
           (4) cleanup of tree lists (remove dead trees)
          */
        private void grow() ///< grow - both on RU-level and tree-level
        {
            {
                using DebugTimer t = new DebugTimer("growRU()");
                calculateStockedArea();

                // Production of biomass (stand level, 3PG)
                threadRunner.run(nc_production);
            }

            using DebugTimer t2 = new DebugTimer("growTrees()");
            threadRunner.run(nc_grow); // actual growth of individual trees

            foreach (ResourceUnit ru in mRU)
            {
                ru.cleanTreeList();
                ru.afterGrow();
                //Debug.WriteLine((b-n) + "trees died (of" + b + ").");
            }
            GlobalSettings.instance().systemStatistics().tTreeGrowth += t2.elapsed();
        }

        /** calculate for each resource unit the fraction of area which is stocked.
          This is done by checking the pixels of the global height grid.
          */
        private void calculateStockedArea() ///< calculate area stocked with trees for each RU
        {
            // iterate over the whole heightgrid and count pixels for each ressource unit
            for (int i = 0; i < mHeightGrid.count(); ++i)
            {
                PointF cp = mHeightGrid.cellCenterPoint(i);
                if (mRUmap.coordValid(cp))
                {
                    ResourceUnit ru = mRUmap.valueAt(cp);
                    if (ru != null)
                    {
                        ru.countStockedPixel(mHeightGrid[i].count() > 0);
                    }
                }
            }
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void calculateStockableArea() ///< calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
        {
            mTotalStockableArea = 0.0;
            foreach (ResourceUnit ru in mRU) 
            {
                // //
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridRunner<HeightGridValue> heightRunner = new GridRunner<HeightGridValue>(mHeightGrid, ru.boundingBox());
                int valid = 0;
                int total = 0;
                for (heightRunner.next(); heightRunner.isValid(); heightRunner.next())
                {
                    HeightGridValue current = heightRunner.current();
                    if (current != null && current.isValid())
                    {
                        valid++;
                    }
                    total++;
                }
                if (total != 0)
                {
                    ru.setStockableArea(Constant.cHeightPixelArea * valid); // in m2
                    if (ru.snag() != null)
                    {
                        ru.snag().scaleInitialState();
                    }
                    mTotalStockableArea += Constant.cHeightPixelArea * valid / Constant.cRUArea; // in ha
                    if (valid == 0 && ru.id() > -1)
                    {
                        // invalidate this resource unit
                        ru.setID(-1);
                    }
                    if (valid > 0 && ru.id() == -1)
                    {
                        Debug.WriteLine("Warning: a resource unit has id=-1 but stockable area (id was set to 0)!!! ru: " + ru.boundingBox() + "with index" + ru.index());
                        ru.setID(0);
                        // test-code
                        //GridRunner<HeightGridValue> runner(*mHeightGrid, ru.boundingBox());
                        //while (runner.next()) {
                        //    Debug.WriteLine(mHeightGrid.cellCenterPoint(mHeightGrid.indexOf( runner.current() )) + ": " + runner.current().isValid());
                        //}
                    }
                }
                else
                {
                    throw new NotSupportedException("calculateStockableArea: resource unit without pixels!");
                }
            }

            // mark those pixels that are at the edge of a "forest-out-of-area"
            GridRunner<HeightGridValue> runner = new GridRunner<HeightGridValue>(mHeightGrid, mHeightGrid.metricRect());
            HeightGridValue[] neighbors = new HeightGridValue[8];
            for (runner.next(); runner.isValid(); runner.next())
            {
                if (runner.current().isForestOutside())
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    runner.neighbors8(neighbors);
                    for (int i = 0; i < 8; ++i)
                    {
                        if (neighbors[i] != null && neighbors[i].isValid())
                        {
                            runner.current().setIsRadiating();
                        }
                    }
                }
            }

            Debug.WriteLine("Total stockable area of the landscape is" + mTotalStockableArea + "ha.");
        }

        private void initializeGrid() ///< initialize the LIF grid
        {
            // fill the whole grid with a value of "1."
            mGrid.initialize(1.0F);

            // apply special values for grid cells border regions where out-of-area cells
            // radiate into the main LIF grid.
            Point p;
            int ix_min, ix_max, iy_min, iy_max, ix_center, iy_center;
            int px_offset = Constant.cPxPerHeight / 2; // for 5 px per height grid cell, the offset is 2
            int max_radiate_distance = 7;
            float step_width = 1.0f / (float)max_radiate_distance;
            int c_rad = 0;
            for (int index = 0; index < mHeightGrid.count(); ++index)
            {
                HeightGridValue hgv = mHeightGrid[index];
                if (hgv.isRadiating())
                {
                    p = mHeightGrid.indexOf(hgv);
                    ix_min = p.X * Constant.cPxPerHeight - max_radiate_distance + px_offset;
                    ix_max = ix_min + 2 * max_radiate_distance + 1;
                    ix_center = ix_min + max_radiate_distance;
                    iy_min = p.Y * Constant.cPxPerHeight - max_radiate_distance + px_offset;
                    iy_max = iy_min + 2 * max_radiate_distance + 1;
                    iy_center = iy_min + max_radiate_distance;
                    for (int y = iy_min; y <= iy_max; ++y)
                    {
                        for (int x = ix_min; x <= ix_max; ++x)
                        {
                            if (!mGrid.isIndexValid(x, y) || !mHeightGrid[x / Constant.cPxPerHeight, y / Constant.cPxPerHeight].isValid())
                            {
                                continue;
                            }
                            float value = MathF.Max(MathF.Abs(x - ix_center), MathF.Abs(y - iy_center)) * step_width;
                            float v = mGrid.valueAtIndex(x, y);
                            if (value >= 0.0F && v > value)
                            {
                                v = value;
                            }
                        }
                    }
                    c_rad++;
                }
            }
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("initialize grid:" + c_rad + "radiating pixels...");
            }
        }

        /// multithreaded running function for the resource unit level establishment
        private void nc_establishment(ResourceUnit unit)
        {
            Saplings s = GlobalSettings.instance().model().saplings();
            s.establishment(unit);
        }

        /// multithreaded running function for the resource unit level establishment
        private void nc_sapling_growth(ResourceUnit unit)
        {
            Saplings s = GlobalSettings.instance().model().saplings();
            s.saplingGrowth(unit);
        }

        /// multithreaded running function for LIP printing
        private void nc_applyPattern(ResourceUnit unit)
        {
            List<Tree> trees = unit.trees();
            // light concurrence influence
            if (!GlobalSettings.instance().model().settings().torusMode)
            {
                // height dominance grid
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].heightGrid();
                }
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].applyLIP();
                }
            }
            else
            {
                // height dominance grid
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].heightGrid_torus();
                }
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].applyLIP_torus();
                }
            }
        }

        /// multithreaded running function for LIP value extraction
        private void nc_readPattern(ResourceUnit unit)
        {
            List<Tree> trees = unit.trees();
            if (!GlobalSettings.instance().model().settings().torusMode)
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].readLIF(); // multiplicative approach
                }
            }
            else
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].readLIF_torus();
                }
            }
        }

        /// multithreaded running function for the growth of individual trees
        private void nc_grow(ResourceUnit unit)
        {
            unit.beforeGrow(); // reset statistics
                               // calculate light responses
                               // responses are based on *modified* values for LightResourceIndex
            List<Tree> trees = unit.trees();
            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                trees[treeIndex].calcLightResponse();
            }
            unit.calculateInterceptedArea();

            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                trees[treeIndex].grow(); // actual growth of individual trees
            }

            GlobalSettings.instance().systemStatistics().treeCount += unit.trees().Count;
        }

        /// multithreaded running function for the resource level production
        private void nc_production(ResourceUnit unit)
        {
            unit.production();
        }

        /// multithreaded execution of the carbon cycle routine
        private void nc_carbonCycle(ResourceUnit unit)
        {
            // (1) do calculations on snag dynamics for the resource unit
            // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
            unit.calculateCarbonCycle();
        }

        private void debugCheckAllTrees()
        {
            AllTreeIterator at = new AllTreeIterator(this);
            bool has_errors = false; 
            double dummy = 0.0;
            for (Tree t = at.next(); t != null; t = at.next())
            {
                // plausibility
                if (t.dbh() < 0 || t.dbh() > 10000.0 || t.biomassFoliage() < 0.0 || t.height() > 1000.0 || t.height() < 0.0 || t.biomassFoliage() < 0.0)
                {
                    has_errors = true;
                }
                // check for objects....
                dummy = t.stamp().offset() + t.ru().ruSpecies()[1].statistics().count();
            }
            if (has_errors)
            {
                Debug.WriteLine("model: debugCheckAllTrees found problems " + dummy);
            }
        }

        public ResourceUnit ru()
        {
            return mRU[0];
        }

        public ResourceUnit ru(PointF coord) ///< ressource unit at given coordinates
        {
            if (!mRUmap.isEmpty() && mRUmap.coordValid(coord))
            {
                return mRUmap.valueAt(coord);
            }
            if (mRUmap.isEmpty())
            {
                return ru(); // default RU if there is only one
            }
            else
            {
                return null; // in this case, no valid coords were provided
            }
        }

        public ResourceUnit ru(int index)  ///< get resource unit by index
        {
            return (index >= 0 && index < mRU.Count) ? mRU[index] : null;
        }
    }
}
