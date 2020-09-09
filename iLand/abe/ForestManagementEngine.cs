using iLand.abe.output;
using iLand.core;
using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace iLand.abe
{
    /** @defgroup abe iLand agent based forest management engine (ABE)
        ABE is the Agent Based management Engine that allows the simulation of both forest management activties (e.g., harvesting of trees)
        and forest managers (e.g., deciding when and where to execute an activity).
        The ABE framework relies heavily on a blend of C++ (for low-level management activties) and Javascript (for higher level definition of
        management programs).

        The smallest spatial entity is a forest stand (FMStand), which may be grouped into forest management unit (FMUnit). Forest managers (Agent) can select
        stand treatment programs (FMSTP) for a unit. The management activities derive from a basic activity (Activity); specialized code exists
        for various activities such as planting or thinning. A scheduler (Scheduler) keeps track of where and when to execute activities following
        guidelines given by the management agent (Agent). Agents represent individual foresters that may be grouped into AgentTypes (e.g., farmers).
        */
    /** @class ForestManagementEngine
     * @ingroup abe
     */
    internal class ForestManagementEngine
    {
        private static ForestManagementEngine singleton_fome_engine = null;
        private static int mMaxStandId = -1;

        public static ForestManagementEngine instance()
        {
            if (singleton_fome_engine != null)
            {
                return singleton_fome_engine;
            }
            singleton_fome_engine = new ForestManagementEngine();
            return singleton_fome_engine;
        }

        private int mCurrentYear; ///< current year of the simulation (=year of the model)

        private List<FMSTP> mSTP;

        // scripting bridge (accessing model properties from javascript)
        private FomeScript mScriptBridge;

        // forest management units
        private List<FMUnit> mUnits; ///< container for forest management units
        // mapping of stands to units
        private MultiValueDictionary<FMUnit, FMStand> mUnitStandMap;
        private List<FMStand> mStands;
        private Dictionary<int, FMStand> mStandHash;

        // agents
        private List<AgentType> mAgentTypes; ///< collection of agent types
        private List<Agent> mAgents; ///< collection of all agents (individuals)

        // grids, visuals, etc.
        private Grid<FMStand> mFMStandGrid;
        private ABELayers mStandLayers;

        private bool mCancel;
        private bool mStandLayoutChanged;
        private string mLastErrorMessage;

        private bool isCancel() { return mCancel; }
        public int currentYear() { return mCurrentYear; }
        /// access to the "global" Javascript engine
        public FomeScript scriptBridge() { return mScriptBridge; }

        /// add a stand treatment programme to the list of programs.
        public void addSTP(FMSTP stp) { mSTP.Add(stp); }
        /// add an agent type (called from JS)
        public void addAgentType(AgentType at) { mAgentTypes.Add(at); }
        public void addAgent(Agent agent) { mAgents.Add(agent); }
        public MultiValueDictionary<FMUnit, FMStand> stands() { return mUnitStandMap; }
        public List<FMUnit> units() { return mUnits; }

        public ForestManagementEngine()
        {
            mScriptBridge = null;
            singleton_fome_engine = this;
            mCancel = false;
            setupOutputs(); // add ABE output definitions
        }

        public static MapGrid standGrid()
        {
            return GlobalSettings.instance().model().standGrid();
        }

        private void setupScripting()
        {
            // setup the ABE system
            XmlHelper xml = GlobalSettings.instance().settings();

            ScriptGlobal.setupGlobalScripting(); // general iLand scripting helper functions and such

            // the link between the scripting and the C++ side of ABE
            mScriptBridge = new FomeScript();
            mScriptBridge.setupScriptEnvironment();

            string file_name = GlobalSettings.instance().path(xml.value("model.management.abe.file"));
            string code = Helper.loadTextFile(file_name);
            Debug.WriteLine("Loading script file " + file_name);
            QJSValue result = GlobalSettings.instance().scriptEngine().evaluate(code, file_name);
            if (result.isError())
            {
                int lineno = result.property("lineNumber").toInt();
                List<string> code_lines = code.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendLine(String.Format("{0}: {1} {2}\n", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : ""));
                }
                Debug.WriteLine("Javascript Error in file" + result.property("fileName").toString() + ":" + result.property("lineNumber").toInt() + ":" + result.toString() + ":\n" + code_part);
            }
        }

        private void prepareRun()
        {
            mStandLayoutChanged = false; // can be changed by salvage operations / stand polygon changes
        }

        private void finalizeRun()
        {
            // empty the harvest counter; it will be filled again
            // during the (next) year.

            foreach (FMStand stand in mStands)
            {
                stand.resetHarvestCounter();
            }

            foreach (FMUnit unit in mUnits)
            {
                unit.resetHarvestCounter();
            }

            //
            if (mStandLayoutChanged)
            {
                using DebugTimer timer = new DebugTimer("ABE:stand_layout_update");
                // renew the internal stand grid
                Grid<int> mapGrid = standGrid().grid();
                for (int p = 0; p < mapGrid.count(); ++p)
                {
                    mFMStandGrid[p] = mapGrid[p] < 0 ? null : mStandHash[p];
                }
                // renew neigborhood information in the stand grid
                standGrid().updateNeighborList();
                // renew the spatial indices
                standGrid().createIndex();
                mStandLayoutChanged = false;

                // now check the stands
                foreach (FMStand it in mStands)
                {
                    // renew area
                    it.checkArea();
                    // initial activity (if missing)
                    if (it.currentActivity() != null)
                    {
                        it.initialize();
                    }
                }
            }
        }

        private void setupOutputs()
        {
            if (GlobalSettings.instance().outputManager().find("abeUnit") != null)
            {
                return; // already set up
            }
            GlobalSettings.instance().outputManager().addOutput(new UnitOut());
            GlobalSettings.instance().outputManager().addOutput(new ABEStandOut());
            GlobalSettings.instance().outputManager().addOutput(new ABEStandDetailsOut());
            GlobalSettings.instance().outputManager().addOutput(new ABEStandRemovalOut());
        }

        private void runJavascript()
        {
            QJSValue handler = scriptEngine().globalObject().property("run");
            if (handler.isCallable())
            {
                FomeScript.setExecutionContext(null, false);
                QJSValue result = handler.call(new List<QJSValue>() { new QJSValue(mCurrentYear) });
                if (FMSTP.verbose())
                {
                    Debug.WriteLine("executing 'run' function for year " + mCurrentYear + ", result: " + result.toString());
                }
            }

            handler = scriptEngine().globalObject().property("runStand");
            if (handler.isCallable())
            {
                Debug.WriteLine("running the 'runStand' javascript function for " + mStands.Count + " stands.");
                foreach (FMStand stand in mStands)
                {
                    FomeScript.setExecutionContext(stand, true);
                    handler.call(new List<QJSValue>() { new QJSValue(mCurrentYear) });
                }
            }
        }

        public AgentType agentType(string name)
        {
            for (int i = 0; i < mAgentTypes.Count; ++i)
            {
                if (mAgentTypes[i].name() == name)
                {
                    return mAgentTypes[i];
                }
            }
            return null;
        }

        public Agent agent(string name)
        {
            for (int i = 0; i < mAgents.Count; ++i)
            {
                if (mAgents[i].name() == name)
                {
                    return mAgents[i];
                }
            }
            return null;
        }

        // multithreaded execution routines
        private void nc_execute_unit(FMUnit unit)
        {
            if (instance().isCancel())
            {
                return;
            }

            //Debug.WriteLine("called for unit" + unit;
            MultiValueDictionary<FMUnit, FMStand> stand_map = instance().stands();
            int executed = 0;
            int total = 0;
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stand_map)
            {
                if (it.Key != unit)
                {
                    continue;
                }

                foreach (FMStand stand in it.Value)
                {
                    stand.stp().executeRepeatingActivities(stand);
                    if (stand.execute())
                    {
                        ++executed;
                    }
                    //MapGrid::freeLocksForStand( it.value().id() );
                    if (instance().isCancel())
                    {
                        break;
                    }
                    ++total;
                }
            }
            if (instance().isCancel())
            {
                return;
            }

            if (FMSTP.verbose())
            {
                Debug.WriteLine("execute unit '" + unit.id() + "', ran " + executed + " of " + total);
            }

            // now run the scheduler
            unit.scheduler().run();

            // collect the harvests
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stand_map)
            {
                if (it.Key != unit)
                {
                    break;
                }

                foreach (FMStand stand in it.Value)
                {
                    unit.addRealizedHarvest(stand.totalHarvest());
                }
            }
        }

        public void nc_plan_update_unit(FMUnit unit)
        {
            if (instance().isCancel())
            {
                return;
            }
            if (instance().currentYear() % 10 == 0)
            {
                Debug.WriteLine("*** execute decadal plan update ***");
                unit.managementPlanUpdate();
                unit.runAgent();
            }

            // first update happens *after* a full year of running ABE.
            if (instance().currentYear() > 1)
            {
                unit.updatePlanOfCurrentYear();
            }
        }

        public void setup()
        {
            using DebugTimer time_setup = new DebugTimer("ABE:setupScripting");
            clear();

            // (1) setup the scripting environment and load all the javascript code
            setupScripting();
            if (isCancel())
            {
                throw new NotSupportedException(String.Format("ABE-Error (setup): {0}", mLastErrorMessage));
            }

            if (GlobalSettings.instance().model() == null)
            {
                throw new NotSupportedException("No model created.... invalid operation.");
            }

            // (2) spatial data (stands, units, ...)
            MapGrid stand_grid = GlobalSettings.instance().model().standGrid();

            if (stand_grid == null || stand_grid.isValid() == false)
            {
                throw new NotSupportedException("The ABE management model requires a valid stand grid.");
            }
            XmlHelper xml = GlobalSettings.instance().settings();

            string data_file_name = GlobalSettings.instance().path(xml.value("model.management.abe.agentDataFile"));
            Debug.WriteLine("loading ABE agentDataFile " + data_file_name + "...");
            CSVFile data_file = new CSVFile(data_file_name);
            if (data_file.isEmpty())
            {
                throw new NotSupportedException(String.Format("Stand-Initialization: the standDataFile file {0} is empty or missing!", data_file_name));
            }
            int ikey = data_file.columnIndex("id");
            int iunit = data_file.columnIndex("unit");
            int iagent = data_file.columnIndex("agent");
            int iagent_type = data_file.columnIndex("agentType");
            int istp = data_file.columnIndex("stp");
            // unit properties
            int ispeciescomp = data_file.columnIndex("speciesComposition");
            int ithinning = data_file.columnIndex("thinningIntensity");
            int irotation = data_file.columnIndex("U");
            int iMAI = data_file.columnIndex("MAI");
            int iharvest_mode = data_file.columnIndex("harvestMode");

            if (ikey < 0 || iunit < 0)
            {
                throw new NotSupportedException("setup ABE agentDataFile: one (or two) of the required columns 'id' or 'unit' not available.");
            }
            if (iagent < 0 && iagent_type < 0)
            {
                throw new NotSupportedException("setup ABE agentDataFile: the columns 'agent' or 'agentType' are not available. You have to include at least one of the columns.");
            }

            List<string> unit_codes = new List<string>();
            Dictionary<FMStand, string> initial_stps = new Dictionary<FMStand, string>();
            for (int i = 0; i < data_file.rowCount(); ++i)
            {
                int stand_id = Int32.Parse(data_file.value(i, ikey));
                if (!stand_grid.isValid(stand_id))
                {
                    continue; // skip stands that are not in the map (e.g. when a smaller extent is simulated)
                }
                if (FMSTP.verbose())
                {
                    Debug.WriteLine("setting up stand " + stand_id);
                }

                // check agents
                string agent_code = iagent > -1 ? (string)data_file.value(i, iagent) : null;
                string agent_type_code = iagent_type > -1 ? (string)data_file.value(i, iagent_type) : null;
                string unit_id = (string)data_file.value(i, iunit);

                Agent ag = null;
                AgentType at = null;
                if (String.IsNullOrEmpty(agent_code) && String.IsNullOrEmpty(agent_type_code))
                {
                    throw new NotSupportedException(String.Format("setup ABE agentDataFile row '{0}': no code for columns 'agent' and 'agentType' available.", i));
                }

                if (String.IsNullOrEmpty(agent_code) == false)
                {
                    // search for a specific agent
                    ag = agent(agent_code);
                    if (ag != null)
                    {
                        throw new NotSupportedException(String.Format("Agent '{0}' is not set up (row '{1}')! Use the 'newAgent()' JS function of agent-types to add agent definitions.", agent_code, i));
                    }
                    at = ag.type();
                }
                else
                {
                    // look up the agent type and create the agent on the fly
                    // create the agent / agent type
                    at = agentType(agent_type_code);
                    if (at != null)
                    {
                        throw new NotSupportedException(String.Format("Agent type '{0}' is not set up (row '{1}')! Use the 'addAgentType()' JS function to add agent-type definitions.", agent_type_code, i));
                    }
                    if (!unit_codes.Contains(unit_id))
                    {
                        // we create an agent for the unit only once (per unit)
                        ag = at.createAgent();
                    }
                }

                // check units
                FMUnit unit = null;
                if (!unit_codes.Contains(unit_id))
                {
                    // create the unit
                    unit = new FMUnit(ag);
                    unit.setId(unit_id);
                    if (iharvest_mode > -1)
                    {
                        unit.setHarvestMode(data_file.value(i, iharvest_mode));
                    }
                    if (ithinning > -1)
                    {
                        unit.setThinningIntensity(Int32.Parse(data_file.value(i, ithinning)));
                    }
                    if (irotation > -1)
                    {
                        unit.setU(Double.Parse(data_file.value(i, irotation)));
                    }
                    if (iMAI > -1)
                    {
                        unit.setAverageMAI(Double.Parse(data_file.value(i, iMAI)));
                    }
                    if (ispeciescomp > -1)
                    {
                        int index;
                        index = at.speciesCompositionIndex((string)data_file.value(i, ispeciescomp));
                        if (index == -1)
                        {
                            throw new NotSupportedException(String.Format("The species composition '{0}' for unit '{1}' is not a valid composition type (agent type: '{2}').", data_file.value(i, ispeciescomp), unit.id(), at.name()));
                        }
                        unit.setTargetSpeciesCompositionIndex(index);
                    }
                    mUnits.Add(unit);
                    unit_codes.Add(unit_id);
                    ag.addUnit(unit); // add the unit to the list of managed units of the agent
                }
                else
                {
                    // get unit by id ... in this case we have the same order of appending values
                    unit = mUnits[unit_codes.IndexOf(unit_id)];
                }

                // create stand
                FMStand stand = new FMStand(unit, stand_id);
                if (istp > -1)
                {
                    string stp = data_file.value(i, istp);
                    initial_stps[stand] = stp;
                }
                mMaxStandId = Math.Max(mMaxStandId, stand_id);

                mUnitStandMap.Add(unit, stand);
                mStands.Add(stand);
            }

            // count the number of stands within each unit
            foreach (FMUnit unit in mUnits)
            {
                unit.setNumberOfStands(mUnitStandMap[unit].Count);
            }

            // set up the stand grid (visualizations)...
            // set up a hash for helping to establish stand-id <. fmstand-link
            mStandHash.Clear();
            for (int i = 0; i < mStands.Count; ++i)
            {
                mStandHash[mStands[i].id()] = mStands[i];
            }

            mFMStandGrid.setup(standGrid().grid().metricRect(), standGrid().grid().cellsize());
            mFMStandGrid.initialize(null);
            for (int p = 0; p < standGrid().grid().count(); ++p)
            {
                mFMStandGrid[p] = standGrid().grid()[p] < 0 ? null : mStandHash[standGrid().grid()[p]];
            }

            mStandLayers.setGrid(mFMStandGrid);
            mStandLayers.clearClasses();
            mStandLayers.registerLayers();

            // now initialize STPs (if they are defined in the init file)
            foreach (KeyValuePair<FMStand, string> it in initial_stps)
            {
                FMStand s = it.Key;
                FMSTP stp = s.unit().agent().type().stpByName(it.Value);
                if (stp != null)
                {
                    s.setSTP(stp);
                }
                else
                {
                    Debug.WriteLine("Warning during reading of CSV setup file: the STP '" + it.Value + "' is not valid for AgentType: " + s.unit().agent().type().name());
                }
            }
            Debug.WriteLine("ABE setup completed.");
        }

        public void initialize()
        {
            using DebugTimer time_setup = new DebugTimer("ABE:setup");

            foreach (FMStand stand in mStands)
            {
                if (stand.stp() != null)
                {
                    stand.setU(stand.unit().U());
                    stand.setThinningIntensity(stand.unit().thinningIntensity());
                    stand.setTargetSpeciesIndex(stand.unit().targetSpeciesIndex());

                    stand.initialize();
                    if (isCancel())
                    {
                        throw new NotSupportedException(String.Format("ABE-Error: init of stand {1}: {0}", mLastErrorMessage, stand.id()));
                    }
                }
            }

            // now initialize the agents....
            foreach (Agent ag in mAgents)
            {
                ag.setup();
                if (isCancel())
                {
                    throw new NotSupportedException(String.Format("ABE-Error: setup of agent '{1}': {0}", mLastErrorMessage, ag.name()));
                }
            }

            // run the initial planning unit setup
            GlobalSettings.instance().model().threadExec().run(nc_plan_update_unit, mUnits);
            Debug.WriteLine("ABE setup complete. " + mUnitStandMap.Count + " stands on " + mUnits.Count + " units, managed by " + mAgents.Count + " agents.");
        }

        public void clear()
        {
            mStands.Clear();
            mUnits.Clear();
            mUnitStandMap.Clear();

            mAgents.Clear();
            mAgentTypes.Clear();
            mSTP.Clear();
            mCurrentYear = 0;
            mCancel = false;
            mLastErrorMessage = null;
        }

        public void abortExecution(string message)
        {
            mLastErrorMessage = message;
            mCancel = true;
        }

        public void runOnInit(bool before_init)
        {
            string handler = before_init ? "onInit" : "onAfterInit";
            if (GlobalSettings.instance().scriptEngine().globalObject().hasProperty(handler))
            {
                QJSValue result = GlobalSettings.instance().scriptEngine().evaluate(String.Format("{0}()", handler));
                if (result.isError())
                {
                    Debug.WriteLine("Javascript Error in global " + handler + "-Handler: " + result.toString());
                }
            }
        }

        /// this is the main function of the forest management engine.
        /// the function is called every year.
        public void run(int debug_year = -1)
        {
            if (debug_year > -1)
            {
                mCurrentYear++;
            }
            else
            {
                mCurrentYear = GlobalSettings.instance().currentYear();
            }
            // now re-evaluate stands
            if (FMSTP.verbose())
            {
                Debug.WriteLine("ForestManagementEngine: run year " + mCurrentYear);
            }

            prepareRun();

            // execute an event handler before invoking the ABE core
            runJavascript();
            {
                // launch the planning unit level update (annual and thorough analysis every ten years)
                using DebugTimer plu = new DebugTimer("ABE:planUpdate");
                GlobalSettings.instance().model().threadExec().run(nc_plan_update_unit, mUnits, true);
            }

            GlobalSettings.instance().model().threadExec().run(nc_execute_unit, mUnits, true); // force single thread operation for now
            if (isCancel())
            {
                throw new NotSupportedException(String.Format("ABE-Error: {0}", mLastErrorMessage));
            }

            // create outputs
            {
                using DebugTimer plu = new DebugTimer("ABE:outputs");
                GlobalSettings.instance().outputManager().execute("abeUnit");
                GlobalSettings.instance().outputManager().execute("abeStand");
                GlobalSettings.instance().outputManager().execute("abeStandDetail");
                GlobalSettings.instance().outputManager().execute("abeStandRemoval");
            }

            finalizeRun();
        }

        public void test()
        {
            // test code
            //Activity::setVerbose(true);
            // setup the activities and the javascript environment...
            GlobalSettings.instance().resetScriptEngine(); // clear the script
            ScriptGlobal.setupGlobalScripting(); // general iLand scripting helper functions and such
            mScriptBridge = new FomeScript();
            mScriptBridge.setupScriptEnvironment();

            string file_name = "E:/Daten/iLand/modeling/abm/knowledge_base/test/test_stp.js";
            string code = Helper.loadTextFile(file_name);
            QJSValue result = GlobalSettings.instance().scriptEngine().evaluate(code, file_name);
            if (result.isError())
            {
                int lineno = result.property("lineNumber").toInt();
                List<string> code_lines = code.Replace("\r", String.Empty).Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendLine(String.Format("{0}: {1} {2}\n", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : ""));
                }
                Debug.WriteLine("Javascript Error in file" + result.property("fileName") + ":" + result.property("lineNumber") + ":" + result + ":" + System.Environment.NewLine + code_part);
            }

            //    try {
            //        Debug.WriteLine("*** test 1 ***";
            //        FMSTP stp;
            //        stp.setVerbose(true);
            //        stp.setup(GlobalSettings.instance().scriptEngine().globalObject().property("stp"), "stp");
            //        stp.dumpInfo();

            //    } catch (IException &e) {
            //        Debug.WriteLine("An error occured:" + e.message();
            //    }
            //    try {
            //        Debug.WriteLine("*** test 2 ***";
            //        FMSTP stp2;
            //        stp2.setVerbose(true);
            //        stp2.setup(GlobalSettings.instance().scriptEngine().globalObject().property("degenerated"), "degenerated");
            //        stp2.dumpInfo();
            //    } catch (IException &e) {
            //        Debug.WriteLine("An error occured:" + e.message();
            //    }

            // dump all objects:
            foreach (FMSTP stp in mSTP)
            {
                stp.dumpInfo();
            }
            setup();
            Debug.WriteLine("finished");

        }

        public List<string> evaluateClick(PointF coord, string grid_name)
        {
            // Q_UNUSED(grid_name); // for the moment
            // find the stand at coord.
            FMStand stand = mFMStandGrid.constValueAt(coord);
            if (stand != null)
            {
                return stand.info();
            }
            return null;
        }

        public static QJSEngine scriptEngine()
        {
            // use global engine from iLand
            return GlobalSettings.instance().scriptEngine();
        }

        public FMSTP stp(string stp_name)
        {
            foreach (FMSTP it in mSTP)
            {
                if (it.name() == stp_name)
                {
                    return it;
                }
            }
            return null;
        }

        public FMStand stand(int stand_id)
        {
            if (mStandHash.ContainsKey(stand_id))
            {
                return mStandHash[stand_id];
            }
            // exhaustive search... should not happen
            Debug.WriteLine("stand() fallback to exhaustive search.");
            foreach (FMStand it in mStands)
            {
                if (it.id() == stand_id)
                {
                    return it;
                }
            }
            return null;
        }

        public List<string> standIds()
        {
            List<string> standids = new List<string>();
            foreach (FMStand s in mStands)
            {
                standids.Add(s.id().ToString());
            }
            return standids;
        }

        public void notifyTreeRemoval(Tree tree, int reason)
        {
            // we use an 'int' instead of Tree:TreeRemovalType because it does not work
            // with forward declaration (and I dont want to include the tree.h header in this class header).
            FMStand stand = mFMStandGrid[tree.position()];
            if (stand != null)
            {
                stand.notifyTreeRemoval(tree, reason);
            }
            else
            {
                Debug.WriteLine("notifyTreeRemoval(): tree not on stand at (metric coords): " + tree.position() + " ID:" + tree.id());
            }
        }

        public bool notifyBarkbeetleAttack(ResourceUnit ru, double generations, int n_infested_px)
        {
            // find out which stands are within the resource unit
            GridRunner<FMStand> gr = new GridRunner<FMStand>(mFMStandGrid, ru.boundingBox());
            Dictionary<FMStand, bool> processed_items = new Dictionary<FMStand, bool>();
            bool forest_changed = false;
            for (gr.next(); gr.isValid(); gr.next())
            {
                FMStand s = gr.current();
                if (!processed_items.ContainsKey(s))
                {
                    processed_items.Add(s, true);
                    forest_changed |= s.notifyBarkBeetleAttack(generations, n_infested_px);
                }
            }
            return forest_changed;
        }

        public FMStand splitExistingStand(FMStand stand)
        {
            int new_stand_id = Interlocked.Increment(ref mMaxStandId);

            FMUnit unit = stand.unit();
            FMStand new_stand = new FMStand(unit, new_stand_id);

            mUnitStandMap.Add(unit, new_stand);
            mStands.Add(new_stand);
            mStandHash[new_stand_id] = new_stand;

            unit.setNumberOfStands(mUnitStandMap[unit].Count);

            mStandLayoutChanged = true;
            return new_stand;
        }
    }
}
