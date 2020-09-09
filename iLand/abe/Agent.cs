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
        private AgentType mType;
        // the javascript object representing the agent:
        private QJSValue mJSAgent;
        SchedulerOptions mSchedulerOptions; ///< agent specific scheduler options
        private List<FMUnit> mUnits; ///< list of units managed by the agent

        // agent properties
        private string mName;
        private double mKnowledge;
        private double mEconomy;
        private double mExperimentation;
        private double mAltruism;
        private double mRisk;

        public QJSValue jsAgent() { return mJSAgent; }
        public AgentType type() { return mType; }
        public string name() { return mName; }
        public SchedulerOptions schedulerOptions() { return mSchedulerOptions; }

        /// add a unit to the list of managed units
        public void addUnit(FMUnit unit) { mUnits.Add(unit); }

        public Agent(AgentType type, QJSValue js)
        {
            mType = type;
            mJSAgent = js;
            mAgentsCreated++;
            mName = String.Format("agent_{0}", mAgentsCreated);
        }

        public void setName(string name)
        {
            mName = name;
            mJSAgent.setProperty("name", name);
        }

        public double useSustainableHarvest()
        {
            return schedulerOptions().useSustainableHarvest;
        }

        public void setup()
        {
            QJSValue scheduler = jsAgent().property("scheduler");
            mSchedulerOptions.setup(scheduler);

            FMSTP stp = type().stpByName("default");
            if (stp != null)
            {
                throw new NotSupportedException("setup(): default-STP not defined");
            }
            QJSValue onSelect_handler = type().jsObject().property("onSelect");

            MultiValueDictionary<FMUnit, FMStand> stand_map = ForestManagementEngine.instance().stands();
            foreach (FMUnit unit in mUnits)
            {
                unit.setU(stp.rotationLengthOfType(2)); // medium
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
                            stand.reload(); // fetch data from iLand ...
                            if (onSelect_handler.isCallable())
                            {
                                FomeScript.setExecutionContext(stand);
                                //QJSValue mix = onSelect_handler.call();
                                QJSValue mix = onSelect_handler.callWithInstance(type().jsObject());
                                string mixture_type = mix.toString();
                                if (type().stpByName(mixture_type) == null)
                                {
                                    throw new NotSupportedException(String.Format("setup(): the selected mixture type '{0}' for stand '{1}' is not valid for agent '{2}'.", mixture_type, stand.id(), mName));
                                }
                                stand.setSTP(type().stpByName(mixture_type));
                            }
                            else
                            {
                                // todo.... some automatic stp selection
                                stand.setSTP(stp);
                            }
                            stand.setU(unit.U());
                            stand.setThinningIntensity(unit.thinningIntensity());
                            stand.setTargetSpeciesIndex(unit.targetSpeciesIndex());
                            stand.initialize(); // run initialization
                        }
                    }
                }
            }
        }
    }
}
