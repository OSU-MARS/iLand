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
            if (singleton_fome_engine == null)
            {
                singleton_fome_engine = new ForestManagementEngine();
            }
            return singleton_fome_engine;
        }

        private int mCurrentYear; ///< current year of the simulation (=year of the model)

        private readonly List<FMSTP> mSTP;

        // scripting bridge (accessing model properties from javascript)
        private FomeScript mScriptBridge;

        // forest management units
        private readonly List<FMUnit> mUnits; ///< container for forest management units
        // mapping of stands to units
        private readonly MultiValueDictionary<FMUnit, FMStand> mUnitStandMap;
        private readonly List<FMStand> mStands;
        private readonly Dictionary<int, FMStand> mStandHash;

        // agents
        private readonly List<AgentType> mAgentTypes; ///< collection of agent types
        private readonly List<Agent> mAgents; ///< collection of all agents (individuals)

        // grids, visuals, etc.
        private readonly Grid<FMStand> mFMStandGrid;
        private readonly ABELayers mStandLayers;

        private bool mCancel;
        private bool mStandLayoutChanged;
        private string mLastErrorMessage;

        private bool isCancel() { return mCancel; }
        public int currentYear() { return mCurrentYear; }
        /// access to the "global" Javascript engine
        public FomeScript scriptBridge() { return mScriptBridge; }

        public MultiValueDictionary<FMUnit, FMStand> stands() { return mUnitStandMap; }
        public List<FMUnit> units() { return mUnits; }

        public ForestManagementEngine()
        {
            this.mAgents = new List<Agent>();
            this.mAgentTypes = new List<AgentType>();
            this.mCancel = false;
            this.mFMStandGrid = new Grid<FMStand>();
            this.mScriptBridge = null;
            this.mStandHash = new Dictionary<int, FMStand>();
            this.mStandLayers = new ABELayers();
            this.mStands = new List<FMStand>();
            this.mSTP = new List<FMSTP>();
            this.mUnits = new List<FMUnit>();
            this.mUnitStandMap = new MultiValueDictionary<FMUnit, FMStand>();

            singleton_fome_engine = this; // BUGBUG: singleton pattern violation in C++
            SetupOutputs(); // add ABE output definitions
        }

        public void AddAgent(Agent agent) { mAgents.Add(agent); }
        /// add an agent type (called from JS)
        public void AddAgentType(AgentType at) { mAgentTypes.Add(at); }
        /// add a stand treatment programme to the list of programs.
        public void AddStandTreatmentProgram(FMSTP stp) { mSTP.Add(stp); }

        public static MapGrid StandGrid()
        {
            return GlobalSettings.Instance.Model.StandGrid;
        }

        private void SetupScripting()
        {
            // setup the ABE system
            XmlHelper xml = GlobalSettings.Instance.Settings;

            ScriptGlobal.SetupGlobalScripting(); // general iLand scripting helper functions and such

            // the link between the scripting and the C++ side of ABE
            mScriptBridge = new FomeScript();
            mScriptBridge.SetupScriptEnvironment();

            string file_name = GlobalSettings.Instance.Path(xml.Value("model.management.abe.file"));
            string code = Helper.LoadTextFile(file_name);
            Debug.WriteLine("Loading script file " + file_name);
            QJSValue result = GlobalSettings.Instance.ScriptEngine.Evaluate(code, file_name);
            if (result.IsError())
            {
                int lineno = result.Property("lineNumber").ToInt();
                List<string> code_lines = code.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendLine(String.Format("{0}: {1} {2}\n", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : ""));
                }
                Debug.WriteLine("Javascript Error in file" + result.Property("fileName").ToString() + ":" + result.Property("lineNumber").ToInt() + ":" + result.ToString() + ":\n" + code_part);
            }
        }

        private void PrepareRun()
        {
            mStandLayoutChanged = false; // can be changed by salvage operations / stand polygon changes
        }

        private void FinalizeRun()
        {
            // empty the harvest counter; it will be filled again
            // during the (next) year.

            foreach (FMStand stand in mStands)
            {
                stand.ResetHarvestCounter();
            }

            foreach (FMUnit unit in mUnits)
            {
                unit.ResetHarvestCounter();
            }

            //
            if (mStandLayoutChanged)
            {
                using DebugTimer timer = new DebugTimer("ABE:stand_layout_update");
                // renew the internal stand grid
                Grid<int> mapGrid = StandGrid().Grid;
                for (int p = 0; p < mapGrid.Count; ++p)
                {
                    mFMStandGrid[p] = mapGrid[p] < 0 ? null : mStandHash[p];
                }
                // renew neigborhood information in the stand grid
                StandGrid().UpdateNeighborList();
                // renew the spatial indices
                StandGrid().CreateIndex();
                mStandLayoutChanged = false;

                // now check the stands
                foreach (FMStand it in mStands)
                {
                    // renew area
                    it.CheckArea();
                    // initial activity (if missing)
                    if (it.CurrentActivity() != null)
                    {
                        it.Initialize();
                    }
                }
            }
        }

        private void SetupOutputs()
        {
            if (GlobalSettings.Instance.OutputManager.Find("abeUnit") != null)
            {
                return; // already set up
            }
            GlobalSettings.Instance.OutputManager.AddOutput(new UnitOut());
            GlobalSettings.Instance.OutputManager.AddOutput(new ABEStandOut());
            GlobalSettings.Instance.OutputManager.AddOutput(new ABEStandDetailsOut());
            GlobalSettings.Instance.OutputManager.AddOutput(new ABEStandRemovalOut());
        }

        private void RunJavascript()
        {
            QJSValue handler = ScriptEngine().GlobalObject().Property("run");
            if (handler.IsCallable())
            {
                FomeScript.SetExecutionContext(null, false);
                QJSValue result = handler.Call(new List<QJSValue>() { new QJSValue(mCurrentYear) });
                if (FMSTP.verbose())
                {
                    Debug.WriteLine("executing 'run' function for year " + mCurrentYear + ", result: " + result.ToString());
                }
            }

            handler = ScriptEngine().GlobalObject().Property("runStand");
            if (handler.IsCallable())
            {
                Debug.WriteLine("running the 'runStand' javascript function for " + mStands.Count + " stands.");
                foreach (FMStand stand in mStands)
                {
                    FomeScript.SetExecutionContext(stand, true);
                    handler.Call(new List<QJSValue>() { new QJSValue(mCurrentYear) });
                }
            }
        }

        public AgentType GetAgentType(string name)
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

        public Agent GetAgent(string name)
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
        private void ExecuteUnit(FMUnit unit)
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
                    stand.stp().ExecuteRepeatingActivities(stand);
                    if (stand.Execute())
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
            unit.scheduler().Run();

            // collect the harvests
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stand_map)
            {
                if (it.Key != unit)
                {
                    break;
                }

                foreach (FMStand stand in it.Value)
                {
                    unit.AddRealizedHarvest(stand.TotalHarvest());
                }
            }
        }

        public void UpdateUnitPlan(FMUnit unit)
        {
            if (instance().isCancel())
            {
                return;
            }
            if (instance().currentYear() % 10 == 0)
            {
                Debug.WriteLine("*** execute decadal plan update ***");
                unit.ManagementPlanUpdate();
                unit.RunAgent();
            }

            // first update happens *after* a full year of running ABE.
            if (instance().currentYear() > 1)
            {
                unit.UpdatePlanOfCurrentYear();
            }
        }

        public void Setup()
        {
            using DebugTimer time_setup = new DebugTimer("ABE:setupScripting");
            Clear();

            // (1) setup the scripting environment and load all the javascript code
            SetupScripting();
            if (isCancel())
            {
                throw new NotSupportedException(String.Format("ABE-Error (setup): {0}", mLastErrorMessage));
            }

            if (GlobalSettings.Instance.Model == null)
            {
                throw new NotSupportedException("No model created.... invalid operation.");
            }

            // (2) spatial data (stands, units, ...)
            MapGrid stand_grid = GlobalSettings.Instance.Model.StandGrid;

            if (stand_grid == null || stand_grid.IsValid() == false)
            {
                throw new NotSupportedException("The ABE management model requires a valid stand grid.");
            }
            XmlHelper xml = GlobalSettings.Instance.Settings;

            string data_file_name = GlobalSettings.Instance.Path(xml.Value("model.management.abe.agentDataFile"));
            Debug.WriteLine("loading ABE agentDataFile " + data_file_name + "...");
            CsvFile data_file = new CsvFile(data_file_name);
            if (data_file.IsEmpty)
            {
                throw new NotSupportedException(String.Format("Stand-Initialization: the standDataFile file {0} is empty or missing!", data_file_name));
            }
            int ikey = data_file.GetColumnIndex("id");
            int iunit = data_file.GetColumnIndex("unit");
            int iagent = data_file.GetColumnIndex("agent");
            int iagent_type = data_file.GetColumnIndex("agentType");
            int istp = data_file.GetColumnIndex("stp");
            // unit properties
            int ispeciescomp = data_file.GetColumnIndex("speciesComposition");
            int ithinning = data_file.GetColumnIndex("thinningIntensity");
            int irotation = data_file.GetColumnIndex("U");
            int iMAI = data_file.GetColumnIndex("MAI");
            int iharvest_mode = data_file.GetColumnIndex("harvestMode");

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
            for (int i = 0; i < data_file.RowCount; ++i)
            {
                int stand_id = Int32.Parse(data_file.Value(i, ikey));
                if (!stand_grid.IsValid(stand_id))
                {
                    continue; // skip stands that are not in the map (e.g. when a smaller extent is simulated)
                }
                if (FMSTP.verbose())
                {
                    Debug.WriteLine("setting up stand " + stand_id);
                }

                // check agents
                string agent_code = iagent > -1 ? (string)data_file.Value(i, iagent) : null;
                string agent_type_code = iagent_type > -1 ? (string)data_file.Value(i, iagent_type) : null;
                string unit_id = (string)data_file.Value(i, iunit);

                Agent ag = null;
                AgentType at = null;
                if (String.IsNullOrEmpty(agent_code) && String.IsNullOrEmpty(agent_type_code))
                {
                    throw new NotSupportedException(String.Format("setup ABE agentDataFile row '{0}': no code for columns 'agent' and 'agentType' available.", i));
                }

                if (String.IsNullOrEmpty(agent_code) == false)
                {
                    // search for a specific agent
                    ag = GetAgent(agent_code);
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
                    at = GetAgentType(agent_type_code);
                    if (at != null)
                    {
                        throw new NotSupportedException(String.Format("Agent type '{0}' is not set up (row '{1}')! Use the 'addAgentType()' JS function to add agent-type definitions.", agent_type_code, i));
                    }
                    if (!unit_codes.Contains(unit_id))
                    {
                        // we create an agent for the unit only once (per unit)
                        ag = at.CreateAgent();
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
                        unit.setHarvestMode(data_file.Value(i, iharvest_mode));
                    }
                    if (ithinning > -1)
                    {
                        unit.setThinningIntensity(Int32.Parse(data_file.Value(i, ithinning)));
                    }
                    if (irotation > -1)
                    {
                        unit.setU(Double.Parse(data_file.Value(i, irotation)));
                    }
                    if (iMAI > -1)
                    {
                        unit.setAverageMAI(Double.Parse(data_file.Value(i, iMAI)));
                    }
                    if (ispeciescomp > -1)
                    {
                        int index;
                        index = at.SpeciesCompositionIndex((string)data_file.Value(i, ispeciescomp));
                        if (index == -1)
                        {
                            throw new NotSupportedException(String.Format("The species composition '{0}' for unit '{1}' is not a valid composition type (agent type: '{2}').", data_file.Value(i, ispeciescomp), unit.id(), at.name()));
                        }
                        unit.setTargetSpeciesCompositionIndex(index);
                    }
                    mUnits.Add(unit);
                    unit_codes.Add(unit_id);
                    ag.AddUnit(unit); // add the unit to the list of managed units of the agent
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
                    string stp = data_file.Value(i, istp);
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

            mFMStandGrid.Setup(StandGrid().Grid.PhysicalSize, StandGrid().Grid.CellSize);
            mFMStandGrid.Initialize(null);
            for (int p = 0; p < StandGrid().Grid.Count; ++p)
            {
                mFMStandGrid[p] = StandGrid().Grid[p] < 0 ? null : mStandHash[StandGrid().Grid[p]];
            }

            mStandLayers.setGrid(mFMStandGrid);
            mStandLayers.ClearClasses();
            mStandLayers.RegisterLayers();

            // now initialize STPs (if they are defined in the init file)
            foreach (KeyValuePair<FMStand, string> it in initial_stps)
            {
                FMStand s = it.Key;
                FMSTP stp = s.unit().agent().type().StpByName(it.Value);
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

        public void Initialize()
        {
            using DebugTimer time_setup = new DebugTimer("ABE:setup");

            foreach (FMStand stand in mStands)
            {
                if (stand.stp() != null)
                {
                    stand.setU(stand.unit().U());
                    stand.setThinningIntensity(stand.unit().thinningIntensity());
                    stand.setTargetSpeciesIndex(stand.unit().targetSpeciesIndex());

                    stand.Initialize();
                    if (isCancel())
                    {
                        throw new NotSupportedException(String.Format("ABE-Error: init of stand {1}: {0}", mLastErrorMessage, stand.id()));
                    }
                }
            }

            // now initialize the agents....
            foreach (Agent ag in mAgents)
            {
                ag.Setup();
                if (isCancel())
                {
                    throw new NotSupportedException(String.Format("ABE-Error: setup of agent '{1}': {0}", mLastErrorMessage, ag.name()));
                }
            }

            // run the initial planning unit setup
            GlobalSettings.Instance.Model.ThreadRunner.Run(UpdateUnitPlan, mUnits);
            Debug.WriteLine("ABE setup complete. " + mUnitStandMap.Count + " stands on " + mUnits.Count + " units, managed by " + mAgents.Count + " agents.");
        }

        public void Clear()
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

        public void AbortExecution(string message)
        {
            mLastErrorMessage = message;
            mCancel = true;
        }

        public void RunOnInit(bool before_init)
        {
            string handler = before_init ? "onInit" : "onAfterInit";
            if (GlobalSettings.Instance.ScriptEngine.GlobalObject().HasProperty(handler))
            {
                QJSValue result = GlobalSettings.Instance.ScriptEngine.Evaluate(String.Format("{0}()", handler));
                if (result.IsError())
                {
                    Debug.WriteLine("Javascript Error in global " + handler + "-Handler: " + result.ToString());
                }
            }
        }

        /// this is the main function of the forest management engine.
        /// the function is called every year.
        public void Run(int debug_year = -1)
        {
            if (debug_year > -1)
            {
                mCurrentYear++;
            }
            else
            {
                mCurrentYear = GlobalSettings.Instance.CurrentYear;
            }
            // now re-evaluate stands
            if (FMSTP.verbose())
            {
                Debug.WriteLine("ForestManagementEngine: run year " + mCurrentYear);
            }

            PrepareRun();

            // execute an event handler before invoking the ABE core
            RunJavascript();
            {
                // launch the planning unit level update (annual and thorough analysis every ten years)
                using DebugTimer plu = new DebugTimer("ABE:planUpdate");
                GlobalSettings.Instance.Model.ThreadRunner.Run(UpdateUnitPlan, mUnits, true);
            }

            GlobalSettings.Instance.Model.ThreadRunner.Run(ExecuteUnit, mUnits, true); // force single thread operation for now
            if (isCancel())
            {
                throw new NotSupportedException(String.Format("ABE-Error: {0}", mLastErrorMessage));
            }

            // create outputs
            {
                using DebugTimer plu = new DebugTimer("ABE:outputs");
                GlobalSettings.Instance.OutputManager.Execute("abeUnit");
                GlobalSettings.Instance.OutputManager.Execute("abeStand");
                GlobalSettings.Instance.OutputManager.Execute("abeStandDetail");
                GlobalSettings.Instance.OutputManager.Execute("abeStandRemoval");
            }

            FinalizeRun();
        }

        public static QJSEngine ScriptEngine()
        {
            // use global engine from iLand
            return GlobalSettings.Instance.ScriptEngine;
        }

        public FMSTP Stp(string stp_name)
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

        public FMStand Stand(int stand_id)
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

        public List<string> StandIDs()
        {
            List<string> standids = new List<string>();
            foreach (FMStand s in mStands)
            {
                standids.Add(s.id().ToString());
            }
            return standids;
        }

        public void NotifyTreeRemoval(Tree tree, int reason)
        {
            // we use an 'int' instead of Tree:TreeRemovalType because it does not work
            // with forward declaration (and I dont want to include the tree.h header in this class header).
            FMStand stand = mFMStandGrid[tree.GetCellCenterPoint()];
            if (stand != null)
            {
                stand.NotifyTreeRemoval(tree, reason);
            }
            else
            {
                Debug.WriteLine("notifyTreeRemoval(): tree not on stand at (metric coords): " + tree.GetCellCenterPoint() + " ID:" + tree.ID);
            }
        }

        public bool NotifyBarkBeetleAttack(ResourceUnit ru, double generations, int n_infested_px)
        {
            // find out which stands are within the resource unit
            GridRunner<FMStand> gr = new GridRunner<FMStand>(mFMStandGrid, ru.BoundingBox);
            Dictionary<FMStand, bool> processed_items = new Dictionary<FMStand, bool>();
            bool forest_changed = false;
            for (gr.MoveNext(); gr.IsValid(); gr.MoveNext())
            {
                FMStand s = gr.Current;
                if (!processed_items.ContainsKey(s))
                {
                    processed_items.Add(s, true);
                    forest_changed |= s.NotifyBarkBeetleAttack(generations, n_infested_px);
                }
            }
            return forest_changed;
        }

        public FMStand SplitExistingStand(FMStand stand)
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
