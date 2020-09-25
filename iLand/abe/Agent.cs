using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;

namespace iLand.abe
{
    internal class Agent
    {
        private static int mAgentsCreated = 0;

        // link to the base agent type
        private readonly AgentType mType;
        // the javascript object representing the agent:
        private readonly QJSValue mJSAgent;
        private readonly SchedulerOptions mSchedulerOptions; ///< agent specific scheduler options
        private readonly List<FMUnit> mUnits; ///< list of units managed by the agent

        // agent properties
        private string mName;

        public QJSValue jsAgent() { return mJSAgent; }
        public AgentType type() { return mType; }
        public string name() { return mName; }
        public SchedulerOptions schedulerOptions() { return mSchedulerOptions; }

        public Agent(AgentType type, QJSValue js)
        {
            this.mJSAgent = js;
            this.mName = String.Format("agent_{0}", mAgentsCreated);
            this.mSchedulerOptions = new SchedulerOptions();
            this.mType = type;
            this.mUnits = new List<FMUnit>();

            Agent.mAgentsCreated++; // BUGBUG: not thread safe?
        }

        /// add a unit to the list of managed units
        public void AddUnit(FMUnit unit) { mUnits.Add(unit); }

        public void SetName(string name)
        {
            mName = name;
            mJSAgent.SetProperty("name", name);
        }

        public double UseSustainableHarvest()
        {
            return schedulerOptions().useSustainableHarvest;
        }

        public void Setup()
        {
            QJSValue scheduler = jsAgent().Property("scheduler");
            mSchedulerOptions.Setup(scheduler);

            FMSTP stp = type().StpByName("default");
            if (stp != null)
            {
                throw new NotSupportedException("setup(): default-STP not defined");
            }
            QJSValue onSelect_handler = type().jsObject().Property("onSelect");

            MultiValueDictionary<FMUnit, FMStand> stand_map = ForestManagementEngine.instance().stands();
            foreach (FMUnit unit in mUnits)
            {
                unit.setU(stp.GetRotationLength(2)); // medium
                foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stand_map)
                {
                    if (it.Key != unit)
                    {
                        continue;
                    }

                    foreach (FMStand stand in it.Value)
                    {
                        // check if STP is already assigned. If not, do it now.
                        if (stand.stp() == null)
                        {
                            stand.Reload(); // fetch data from iLand ...
                            if (onSelect_handler.IsCallable())
                            {
                                FomeScript.SetExecutionContext(stand);
                                //QJSValue mix = onSelect_handler.call();
                                QJSValue mix = onSelect_handler.CallWithInstance(type().jsObject());
                                string mixture_type = mix.ToString();
                                if (type().StpByName(mixture_type) == null)
                                {
                                    throw new NotSupportedException(String.Format("setup(): the selected mixture type '{0}' for stand '{1}' is not valid for agent '{2}'.", mixture_type, stand.id(), mName));
                                }
                                stand.setSTP(type().StpByName(mixture_type));
                            }
                            else
                            {
                                // todo.... some automatic stp selection
                                stand.setSTP(stp);
                            }
                            stand.setU(unit.U());
                            stand.setThinningIntensity(unit.thinningIntensity());
                            stand.setTargetSpeciesIndex(unit.targetSpeciesIndex());
                            stand.Initialize(); // run initialization
                        }
                    }
                }
            }
        }
    }
}
