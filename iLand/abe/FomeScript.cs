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
        private static readonly string mInvalidContext = "S---";
        private FMStand mStand;
        private StandObj mStandObj;
        private UnitObj mUnitObj;
        private SimulationObj mSimulationObj;
        private ActivityObj mActivityObj;
        private FMTreeList mTrees;
        private SchedulerObj mSchedulerObj;
        private STPObj mSTPObj;
        // unused in C++
        // private string mLastErrorMessage;

        /// returns a string for debug/trace messages
        public string context() { return mStand != null ? mStand.context() : mInvalidContext; }

        public StandObj standObj() { return mStandObj; }
        public UnitObj siteObj() { return mUnitObj; }
        public FMTreeList treesObj() { return mTrees; }
        public ActivityObj activityObj() { return mActivityObj; }

        public FomeScript()
        {
            mStandObj = null;
            mUnitObj = null;
            mSimulationObj = null;
            mActivityObj = null;
            mSchedulerObj = null;
            mTrees = null;
            mStand = null;
        }

        public static FomeScript bridge()
        {
            // get the right bridge object (for the right thread??)
            return ForestManagementEngine.instance().scriptBridge();
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

        public void SetupScriptEnvironment()
        {
            // create javascript objects in the script engine
            // these objects can be accessed from Javascript code representing forest management activities
            // or agents.

            // stand variables
            mStandObj = new StandObj();
            QJSValue stand_value = ForestManagementEngine.ScriptEngine().NewQObject(mStandObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("stand", stand_value);

            // site variables
            mUnitObj = new UnitObj();
            QJSValue site_value = ForestManagementEngine.ScriptEngine().NewQObject(mUnitObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("unit", site_value);

            // general simulation variables (mainly scenariolevel)
            mSimulationObj = new SimulationObj();
            QJSValue simulation_value = ForestManagementEngine.ScriptEngine().NewQObject(mSimulationObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("simulation", simulation_value);

            //access to the current activity
            mActivityObj = new ActivityObj();
            QJSValue activity_value = ForestManagementEngine.ScriptEngine().NewQObject(mActivityObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("activity", activity_value);

            // general simulation variables (mainly scenariolevel)
            mTrees = new FMTreeList();
            QJSValue treelist_value = ForestManagementEngine.ScriptEngine().NewQObject(mTrees);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("trees", treelist_value);

            // options of the STP
            mSTPObj = new STPObj();
            QJSValue stp_value = ForestManagementEngine.ScriptEngine().NewQObject(mSTPObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("stp", stp_value);

            // scheduler options
            mSchedulerObj = new SchedulerObj();
            QJSValue scheduler_value = ForestManagementEngine.ScriptEngine().NewQObject(mSchedulerObj);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("scheduler", scheduler_value);

            // the script object itself
            QJSValue script_value = ForestManagementEngine.ScriptEngine().NewQObject(this);
            ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("fmengine", script_value);

        }

        public static void SetExecutionContext(FMStand stand, bool add_agent = false)
        {
            FomeScript br = bridge();
            br.mStand = stand;
            br.mStandObj.setStand(stand);
            br.mTrees.SetStand(stand);
            br.mUnitObj.setStand(stand);
            br.mActivityObj.SetStand(stand);
            br.mSchedulerObj.setStand(stand);
            br.mSTPObj.SetStp(stand);
            if (stand != null && stand.TracingEnabled())
            {
                Debug.WriteLine(br.context() + " Prepared execution context (thread " + Thread.CurrentThread.ManagedThreadId + ").");
            }
            if (add_agent)
            {
                Agent ag = stand.unit().agent();
                ForestManagementEngine.ScriptEngine().GlobalObject().SetProperty("agent", ag.jsAgent());
            }
        }

        public static void SetActivity(Activity act)
        {
            FomeScript br = bridge();
            SetExecutionContext(null);
            br.mActivityObj.SetActivity(act);
        }

        public static string JStoString(QJSValue value)
        {
            if (value.IsArray() || value.IsObject())
            {
                QJSValue fun = ForestManagementEngine.ScriptEngine().Evaluate("(function(a) { return JSON.stringify(a); })");
                QJSValue result = fun.Call(new List<QJSValue>() { value });
                return result.ToString();
            }
            else
            {
                return value.ToString();
            }
        }
        public void SetStandId(int new_stand_id)
        {
            FMStand stand = ForestManagementEngine.instance().Stand(new_stand_id);
            if (stand == null)
            {
                Debug.WriteLine(bridge().context() + " invalid stand id " + new_stand_id);
                return;
            }

            SetExecutionContext(stand);
        }

        public void Log(QJSValue value)
        {
            string msg = JStoString(value);
            Debug.WriteLine(bridge().context() + msg);
        }

        public void Abort(QJSValue message)
        {
            Log(message);
            ForestManagementEngine.instance().AbortExecution(String.Format("{0}: {1}", context(), message.ToString()));
        }

        public bool AddManagement(QJSValue program, string name)
        {
            FMSTP stp = new FMSTP();
            stp.Setup(program, name);
            ForestManagementEngine.instance().AddStandTreatmentProgram(stp);
            return true;
        }

        public bool UpdateManagement(QJSValue program, string name)
        {
            FMSTP stp = ForestManagementEngine.instance().Stp(name);
            if (stp == null)
            {
                Trace.TraceWarning("updateManagement: STP " + name + " not found. No program updated.");
                return false;
            }
            stp.Setup(program, name);
            return true;
        }

        public bool AddManagementToAgentType(string name, string agentname)
        {
            FMSTP stp = ForestManagementEngine.instance().Stp(name);
            if (stp == null)
            {
                Trace.TraceWarning("addManagementToAgentType: STP " + name + " not found!");
                return false;
            }
            AgentType at = ForestManagementEngine.instance().GetAgentType(agentname);
            if (at == null)
            {
                Trace.TraceWarning("addManagementToAgentType: agenttype " + agentname + " not found!");
                return false;
            }
            at.AddStp(name);
            return true;
        }

        public bool AddAgentType(QJSValue program, string name)
        {
            AgentType at = new AgentType();
            at.SetupStp(program, name);
            ForestManagementEngine.instance().AddAgentType(at);
            return true;
        }

        public QJSValue AddAgent(string agent_type, string agent_name)
        {
            // find the agent type
            AgentType at = ForestManagementEngine.instance().GetAgentType(agent_type);
            if (at != null)
            {
                Abort(new QJSValue(String.Format("fmengine.addAgent: invalid 'agent_type': '{0}'", agent_type)));
                return null;
            }
            Agent ag = at.CreateAgent(agent_name);
            return ag.jsAgent();
        }

        /// force execution of an activity (outside of the usual execution context, e.g. for debugging)
        public bool RunActivity(int stand_id, string activity)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().Stand(stand_id);
            if (stand == null)
            {
                return false;
            }
            if (stand.stp() == null)
            {
                return false;
            }
            Activity act = stand.stp().GetActivity(activity);
            if (act == null)
            {
                return false;
            }

            // run the activity....
            Debug.WriteLine("running activity " + activity + " for stand " + stand_id);
            return act.Execute(stand);
        }

        public bool RunActivityEvaluate(int stand_id, string activity)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().Stand(stand_id);
            if (stand == null)
            {
                return false;
            }
            if (stand.stp() == null)
            {
                return false;
            }
            Activity act = stand.stp().GetActivity(activity);
            if (act == null)
            {
                return false;
            }

            // run the activity....
            Debug.WriteLine("running evaluate of activity " + activity + " for stand " + stand_id);
            return act.Evaluate(stand);
        }

        public bool RunAgent(int stand_id, string function)
        {
            // find stand
            FMStand stand = ForestManagementEngine.instance().Stand(stand_id);
            if (stand == null)
            {
                return false;
            }

            SetExecutionContext(stand, true); // true: add also agent as 'agent'

            QJSValue val;
            QJSValue agent_type = stand.unit().agent().type().jsObject();
            if (agent_type.Property(function).IsCallable())
            {
                val = agent_type.Property(function).CallWithInstance(agent_type);
                Debug.WriteLine("running agent-function " + function + " for stand " + stand_id + ": " + val.ToString());
            }
            else
            {
                Debug.WriteLine("function " + function + " is not a valid function of agent-type " + stand.unit().agent().type().name());
            }

            return true;
        }

        public bool IsValidStand(int stand_id)
        {
            FMStand stand = ForestManagementEngine.instance().Stand(stand_id);
            if (stand != null)
            {
                return true;
            }

            return false;
        }

        public List<string> StandIDs()
        {
            // BUGBUG: why wrap this?
            return ForestManagementEngine.instance().StandIDs();
        }

        public QJSValue Activity(string stp_name, string activity_name)
        {

            FMSTP stp = ForestManagementEngine.instance().Stp(stp_name);
            if (stp == null)
            {
                Debug.WriteLine("fmengine.activty: invalid stp " + stp_name);
                return null;
            }

            Activity act = stp.GetActivity(activity_name);
            if (act == null)
            {
                Debug.WriteLine("fmengine.activty: activity " + activity_name + " not found in stp: " + stp_name);
                return null;
            }

            int idx = stp.GetIndexOf(act);
            ActivityObj ao = new ActivityObj(null, act, idx);
            QJSValue value = ForestManagementEngine.ScriptEngine().NewQObject(ao);
            return value;
        }

        public void RunPlanting(int stand_id, QJSValue planting_item)
        {
            FMStand stand = ForestManagementEngine.instance().Stand(stand_id);
            if (stand == null)
            {
                Trace.TraceWarning("runPlanting: stand not found " + stand_id);
                return;
            }

            ActPlanting.RunSinglePlantingItem(stand, planting_item);
        }

        public static int LevelIndex(string level_label)
        {
            if (level_label == "low") return 1;
            if (level_label == "medium") return 2;
            if (level_label == "high") return 3;
            return -1;
        }

        public static string LevelLabel(int level_index)
        {
            return level_index switch
            {
                1 => "low",
                2 => "medium",
                3 => "high",
                _ => "invalid",
            };
        }
    }
}
