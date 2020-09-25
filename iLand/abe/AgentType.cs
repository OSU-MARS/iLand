using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class AgentType
        @ingroup abe
        AgentType implements an abstract agent type (e.g., farmer or forest company). The class defines basic behavior of
        agents.
      */
    internal class AgentType
    {
        private readonly MultiValueDictionary<FMUnit, AgentUpdate> mAgentChanges;
        private QJSValue mJSObj; ///< javascript object
        private string mName; // agent name
        private readonly List<string> mSpeciesCompositions; ///< list of available target species composition (objects)
        private readonly Dictionary<string, FMSTP> mSTP; ///< list of all STP linked to this agent type

        public string name() { return mName; }
        /// access to the javascript object
        public QJSValue jsObject() { return mJSObj; }

        public AgentType()
        {
            this.mAgentChanges = new MultiValueDictionary<FMUnit, AgentUpdate>();
            this.mJSObj = null;
            this.mName = null;
            this.mSpeciesCompositions = new List<string>();
            this.mSTP = new Dictionary<string, FMSTP>();
        }

        public void SetupStp(QJSValue agent_code, string agent_name)
        {
            mName = agent_name;
            mSTP.Clear();
            mJSObj = agent_code;
            if (!agent_code.IsObject())
            {
                throw new NotSupportedException(String.Format("ABE:AgentType:setup: the javascript object for agent '{0}' could not be found.", agent_name));
            }
            QJSValue stps = agent_code.Property("stp");
            if (!stps.IsObject())
            {
                throw new NotSupportedException(String.Format("ABE:AgentType:setup: the javascript definition of agent '{0}' does not have a section for 'stp'.", agent_name));
            }
            QJSValueIterator it = new QJSValueIterator(stps);
            while (it.HasNext())
            {
                it.Next();
                FMSTP stp = ForestManagementEngine.instance().Stp(it.Value().ToString());
                mSTP[it.Name()] = stp ?? throw new NotSupportedException(String.Format("ABE:AgentType:setup: definition of agent '{0}': the STP for mixture type '{1}': '{2}' is not available.", agent_name, it.Name(), it.Value().ToString()));
            }

            if (FMSTP.verbose())
            {
                Debug.WriteLine("setup of agent " + agent_name + mSTP.Count + " links to STPs established.");
            }
        }

        public void AddStp(string stp_name)
        {
            FMSTP stp = ForestManagementEngine.instance().Stp(stp_name);
            mSTP[stp_name] = stp ?? throw new NotSupportedException(String.Format("addSTP: definition of agent '{0}': the STP  '{1}' is not available.", mName, stp_name));
        }

        public Agent CreateAgent(string agent_name = null)
        {
            // call the newAgent function in the javascript object assigned to this agent type
            QJSValue func = (QJSValue)mJSObj.Property("newAgent");
            if (!func.IsCallable())
            {
                throw new NotSupportedException(String.Format("The agent type '{0}' does not have a valid 'newAgent' function.", name()));
            }
            QJSValue result = func.CallWithInstance(mJSObj);
            if (result.IsError())
            {
                throw new NotSupportedException(String.Format("calling the 'newAgent' function of agent type '{0}' returned with the following error: {1}", name(), result.ToString()));
            }
            Agent agent = new Agent(this, result);
            if (String.IsNullOrEmpty(agent_name))
            {
                agent.SetName(agent_name);
            }
            else
            {
                if (result.Property("name").IsUndefined())
                {
                    result.SetProperty("name", agent.name()); //  set the auto-generated name also for the JS world
                }
                else
                {
                    agent.SetName(result.Property("name").ToString()); // set the JS-name also internally
                }
            }
            ForestManagementEngine.instance().AddAgent(agent);

            return agent;
        }

        public void AddAgentUpdate(AgentUpdate update, FMUnit unit)
        {
            // clear agent updates...
            List<KeyValuePair<FMUnit, AgentUpdate>> updatesToRemove = new List<KeyValuePair<FMUnit, AgentUpdate>>();
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<AgentUpdate>> hi in mAgentChanges)
            {
                foreach (AgentUpdate existingUpdate in hi.Value)
                {
                    if (existingUpdate.isValid() == false)
                    {
                        updatesToRemove.Add(new KeyValuePair<FMUnit, AgentUpdate>(hi.Key, existingUpdate));
                    }
                }
            }
            foreach (KeyValuePair<FMUnit, AgentUpdate> updateToRemove in updatesToRemove)
            {
                mAgentChanges.Remove(updateToRemove.Key, updateToRemove.Value);
            }

            mAgentChanges.Add(unit, update);
            update.setCounter(unit.numberOfStands());

            // set default unit value
            switch (update.type())
            {
                case UpdateType.UpdateU: 
                    unit.setU(Double.Parse(update.value())); 
                    break;
                case UpdateType.UpdateThinning: 
                    unit.setThinningIntensity(Int32.Parse(update.value())); 
                    break;
                case UpdateType.UpdateSpecies: 
                    break;
                default: 
                    break;
            }

            if (update.age() == -1)
            {
                return;
            }

            // check stands that should be updated immediateley
            MultiValueDictionary<FMUnit, FMStand> stands = ForestManagementEngine.instance().stands();
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stands)
            {
                if (it.Key != unit)
                {
                    continue;
                }

                foreach (FMStand stand in it.Value)
                {
                    if (stand.TracingEnabled())
                    {
                        Debug.WriteLine(stand.context() + " Agent-update: update if stand-age: " + stand.age() + " < update-age: " + update.age());
                    }
                    if (stand.age() <= update.age())
                    {
                        AgentUpdateForStand(stand, null, (int)stand.age());
                    }
                }
            }
        }

        public bool AgentUpdateForStand(FMStand stand, string after_activity, int age)
        {
            IReadOnlyCollection<AgentUpdate> uit = mAgentChanges[stand.unit()];
            bool action = false;
            foreach (AgentUpdate update in uit)
            {
                if (update.isValid() == false)
                {
                    continue;
                }

                // timing of update
                if (String.IsNullOrEmpty(after_activity) == false && update.afterActivity() == after_activity)
                {
                    // do something
                    action = true;
                }
                if (update.age() > -1 && age < update.age())
                {
                    // do something
                    action = true;
                }

                // update the stand
                if (action)
                {
                    update.decrease();
                    switch (update.type())
                    {
                        case UpdateType.UpdateU:
                            {
                                int current_u = (int)stand.U(); // stand.stp().rotationLengthType(stand.U());
                                int new_u = Int32.Parse(update.value());
                                if (current_u == new_u)
                                {
                                    if (stand.TracingEnabled())
                                    {
                                        Debug.WriteLine(stand.context() + " AgentUpdate: update of U to " + new_u + " not done (value already set).");
                                    }
                                    break;
                                }
                                stand.setU(new_u);
                                // stand.setU( stand.stp().rotationLengthOfType(new_u) );
                                Debug.WriteLine(stand.context() + " AgentUpdate: changed to U " + stand.U());
                                // QML like dynamic expressions
                                stand.stp().EvaluateDynamicExpressions(stand);
                                break;
                            }
                        case UpdateType.UpdateThinning:
                            {
                                int current_th = stand.thinningIntensity();
                                int new_th = Int32.Parse(update.value());
                                if (current_th == new_th)
                                {
                                    if (stand.TracingEnabled())
                                    {
                                        Debug.WriteLine(stand.context() + "AgentUpdate: update of thinningIntensity class to " + new_th + " not done (value already set).");
                                    }
                                    break;
                                }
                                stand.setThinningIntensity(new_th);
                                Debug.WriteLine(stand.context() + " AgentUpdate: changed to thinningIntensity class: " + stand.thinningIntensity());
                                stand.stp().EvaluateDynamicExpressions(stand);
                                break;
                            }
                        default: 
                            break; // TODO: UpdateSpecies???
                    }
                }
            }
            return action;
        }

        public FMSTP StpByName(string name)
        {
            if (mSTP.ContainsKey(name))
            {
                return mSTP[name];
            }
                return null;
        }

        public int SpeciesCompositionIndex(string key)
        {
            for (int i = 0; i < mSpeciesCompositions.Count; ++i)
            {
                if (mSpeciesCompositions[i] == key)
                {
                    return i;
                }
            }
            return -1;
        }

        public string SpeciesCompositionName(int index)
        {
            if (index >= 0 && index < mSpeciesCompositions.Count)
            {
                return mSpeciesCompositions[index];
            }
            return null;
        }
    }
}
