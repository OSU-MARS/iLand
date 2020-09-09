using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace iLand.abe
{
    /** @class FomeScript
        @ingroup abe
        The FomeScript class is visible to Javascript via the 'fmengine' object. The main functions of ABE are available through this class.
        */
    internal class FomeScript
    {
        private static string mInvalidContext = "S---";
        private FMStand mStand;
        private StandObj mStandObj;
        private UnitObj mUnitObj;
        private SimulationObj mSimulationObj;
        private ActivityObj mActivityObj;
        private FMTreeList mTrees;
        private SchedulerObj mSchedulerObj;
        private STPObj mSTPObj;
        private string mLastErrorMessage;

        /// returns a string for debug/trace messages
        public string context() { return mStand != null ? mStand.context() : mInvalidContext; }

        public StandObj standObj() { return mStandObj; }
        public UnitObj siteObj() { return mUnitObj; }
        public FMTreeList treesObj() { return mTrees; }
        public ActivityObj activityObj() { return mActivityObj; }

        public FomeScript(object parent = null)
        {
            mStandObj = null;
            mUnitObj = null;
            mSimulationObj = null;
            mActivityObj = null;
            mSchedulerObj = null;
            mTrees = null;
            mStand = null;
        }

        public void setupScriptEnvironment()
        {
            // create javascript objects in the script engine
            // these objects can be accessed from Javascript code representing forest management activities
            // or agents.

            // stand variables
            mStandObj = new StandObj();
            QJSValue stand_value = ForestManagementEngine.scriptEngine().newQObject(mStandObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("stand", stand_value);

            // site variables
            mUnitObj = new UnitObj();
            QJSValue site_value = ForestManagementEngine.scriptEngine().newQObject(mUnitObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("unit", site_value);

            // general simulation variables (mainly scenariolevel)
            mSimulationObj = new SimulationObj();
            QJSValue simulation_value = ForestManagementEngine.scriptEngine().newQObject(mSimulationObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("simulation", simulation_value);

            //access to the current activity
            mActivityObj = new ActivityObj();
            QJSValue activity_value = ForestManagementEngine.scriptEngine().newQObject(mActivityObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("activity", activity_value);

            // general simulation variables (mainly scenariolevel)
            mTrees = new FMTreeList();
            QJSValue treelist_value = ForestManagementEngine.scriptEngine().newQObject(mTrees);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("trees", treelist_value);

            // options of the STP
            mSTPObj = new STPObj();
            QJSValue stp_value = ForestManagementEngine.scriptEngine().newQObject(mSTPObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("stp", stp_value);

            // scheduler options
            mSchedulerObj = new SchedulerObj();
            QJSValue scheduler_value = ForestManagementEngine.scriptEngine().newQObject(mSchedulerObj);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("scheduler", scheduler_value);

            // the script object itself
            QJSValue script_value = ForestManagementEngine.scriptEngine().newQObject(this);
            ForestManagementEngine.scriptEngine().globalObject().setProperty("fmengine", script_value);

        }

        public static void setExecutionContext(FMStand stand, bool add_agent = false)
        {
            FomeScript br = bridge();
            br.mStand = stand;
            br.mStandObj.setStand(stand);
            br.mTrees.setStand(stand);
            br.mUnitObj.setStand(stand);
            br.mActivityObj.setStand(stand);
            br.mSchedulerObj.setStand(stand);
            br.mSTPObj.setSTP(stand);
            if (stand != null && stand.trace())
            {
                Debug.WriteLine(br.context() + " Prepared execution context (thread " + Thread.CurrentThread.ManagedThreadId + ").");
            }
            if (add_agent)
            {
                Agent ag = stand.unit().agent();
                ForestManagementEngine.scriptEngine().globalObject().setProperty("agent", ag.jsAgent());
            }
        }

        public static void setActivity(Activity act)
        {
            FomeScript br = bridge();
            setExecutionContext(null);
            br.mActivityObj.setActivity(act);
        }

        public static FomeScript bridge()
        {
            // get the right bridge object (for the right thread??)
            return ForestManagementEngine.instance().scriptBridge();
        }

        public static string JStoString(QJSValue value)
        {
            if (value.isArray() || value.isObject())
            {
                QJSValue fun = ForestManagementEngine.scriptEngine().evaluate("(function(a) { return JSON.stringify(a); })");
                QJSValue result = fun.call(new List<QJSValue>() { value });
                return result.toString();
            }
            else
            {
                return value.toString();
            }
        }

        public bool verbose()
        {
            return FMSTP.verbose();
        }

        public void setVerbose(bool arg)
        {
            FMSTP.setVerbose(arg);
            Debug.WriteLine("setting verbose property of ABE to " + arg);
        }

        public int standId()
        {
            if (mStand != null)
            {
                return mStand.id();
            }
            return -1;
        }

        public void setStandId(int new_stand_id)
        {
            FMStand stand = ForestManagementEngine.instance().stand(new_stand_id);
            if (stand == null)
            {
                Debug.WriteLine(bridge().context() + " invalid stand id " + new_stand_id);
                return;
            }

            setExecutionContext(stand);
        }

        public void log(QJSValue value)
        {
            string msg = JStoString(value);
            Debug.WriteLine(bridge().context() + msg);
        }

        public void abort(QJSValue message)
        {
            log(message);
            ForestManagementEngine.instance().abortExecution(String.Format("{0}: {1}", context(), message.toString()));
        }

        public bool addManagement(QJSValue program, string name)
        {
            FMSTP stp = new FMSTP();
            stp.setup(program, name);
            ForestManagementEngine.instance().addSTP(stp);
            return true;
        }

        public bool updateManagement(QJSValue program, string name)
        {
            FMSTP stp = ForestManagementEngine.instance().stp(name);
            if (stp == null)
            {
                Trace.TraceWarning("updateManagement: STP " + name + " not found. No program updated.");
                return false;
            }
            stp.setup(program, name);
            return true;
        }

        public bool addManagementToAgentType(string name, string agentname)
        {
            FMSTP stp = ForestManagementEngine.instance().stp(name);
            if (stp == null)
            {
                Trace.TraceWarning("addManagementToAgentType: STP " + name + " not found!");
                return false;
            }
            AgentType at = ForestManagementEngine.instance().agentType(agentname);
            if (at == null)
            {
                Trace.TraceWarning("addManagementToAgentType: agenttype " + agentname + " not found!");
                return false;
            }
            at.addSTP(name);
            return true;
        }

        public bool addAgentType(QJSValue program, string name)
        {
            AgentType at = new AgentType();
            at.setupSTP(program, name);
            ForestManagementEngine.instance().addAgentType(at);
            return true;
        }

        public QJSValue addAgent(string agent_type, string agent_name)
        {
            // find the agent type
            AgentType at = ForestManagementEngine.instance().agentType(agent_type);
            if (at != null)
            {
                abort(new QJSValue(String.Format("fmengine.addAgent: invalid 'agent_type': '{0}'", agent_type)));
                return null;
            }
            Agent ag = at.createAgent(agent_name);
            return ag.jsAgent();
        }

        /// force execution of an activity (outside of the usual execution context, e.g. for debugging)
        public bool runActivity(int stand_id, string activity)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().stand(stand_id);
            if (stand == null)
            {
                return false;
            }
            if (stand.stp() == null)
            {
                return false;
            }
            Activity act = stand.stp().activity(activity);
            if (act == null)
            {
                return false;
            }

            // run the activity....
            Debug.WriteLine("running activity " + activity + " for stand " + stand_id);
            return act.execute(stand);
        }

        public bool runActivityEvaluate(int stand_id, string activity)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().stand(stand_id);
            if (stand == null)
            {
                return false;
            }
            if (stand.stp() == null)
            {
                return false;
            }
            Activity act = stand.stp().activity(activity);
            if (act == null)
            {
                return false;
            }

            // run the activity....
            Debug.WriteLine("running evaluate of activity " + activity + " for stand " + stand_id);
            return act.evaluate(stand);
        }

        public bool runAgent(int stand_id, string function)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().stand(stand_id);
            if (stand == null)
            {
                return false;
            }

            setExecutionContext(stand, true); // true: add also agent as 'agent'

            QJSValue val;
            QJSValue agent_type = stand.unit().agent().type().jsObject();
            if (agent_type.property(function).isCallable())
            {
                val = agent_type.property(function).callWithInstance(agent_type);
                Debug.WriteLine("running agent-function " + function + " for stand " + stand_id + ": " + val.toString());
            }
            else
            {
                Debug.WriteLine("function " + function + " is not a valid function of agent-type " + stand.unit().agent().type().name());
            }

            return true;
        }

        public bool isValidStand(int stand_id)
        {
            FMStand stand = ForestManagementEngine.instance().stand(stand_id);
            if (stand != null)
            {
                return true;
            }

            return false;
        }

        public List<string> standIds()
        {
            return ForestManagementEngine.instance().standIds();
        }

        public QJSValue activity(string stp_name, string activity_name)
        {

            FMSTP stp = ForestManagementEngine.instance().stp(stp_name);
            if (stp == null)
            {
                Debug.WriteLine("fmengine.activty: invalid stp " + stp_name);
                return null;
            }

            Activity act = stp.activity(activity_name);
            if (act == null)
            {
                Debug.WriteLine("fmengine.activty: activity " + activity_name + " not found in stp: " + stp_name);
                return null;
            }

            int idx = stp.activityIndex(act);
            ActivityObj ao = new ActivityObj(null, act, idx);
            QJSValue value = ForestManagementEngine.scriptEngine().newQObject(ao);
            return value;
        }

        public void runPlanting(int stand_id, QJSValue planting_item)
        {
            FMStand stand = ForestManagementEngine.instance().stand(stand_id);
            if (stand == null)
            {
                Trace.TraceWarning("runPlanting: stand not found " + stand_id);
                return;
            }

            ActPlanting.runSinglePlantingItem(stand, planting_item);
        }

        public static int levelIndex(string level_label)
        {
            if (level_label == "low") return 1;
            if (level_label == "medium") return 2;
            if (level_label == "high") return 3;
            return -1;
        }

        public static string levelLabel(int level_index)
        {
            switch (level_index)
            {
                case 1: return "low";
                case 2: return "medium";
                case 3: return "high";
            }
            return "invalid";
        }
    }
}
