using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    internal class UnitObj
    {
        private FMStand mStand;

        public void setStand(FMStand stand) { mStand = stand; }

        public UnitObj()
        {
        }

        public bool AgentUpdate(string what, string how, string when)
        {
            UpdateType type = abe.AgentUpdate.Label(what);
            if (type == UpdateType.UpdateInvalid)
            {
                Debug.WriteLine("unit.agentUpdate: invalid 'what': " + what);
            }

            AgentUpdate update = new AgentUpdate();
            update.setType(type);

            // how
            int idx = FomeScript.LevelIndex(how);
            if (idx > -1)
            {
                update.setValue(idx.ToString());
            }
            else
            {
                update.setValue(how);
            }

            // when
            if (Int32.TryParse(when, out int age))
            {
                update.setTimeAge(age);
            }
            else
            {
                update.setTimeActivity(when);
            }

            mStand.unit().agent().type().AddAgentUpdate(update, mStand.unit());
            Debug.WriteLine("Unit::agentUpdate: " + update.Dump());
            return true;
        }

        public string HarvestMode()
        {
            return mStand.unit().harvestMode();
        }

        public string SpeciesComposition()
        {
            int index = mStand.unit().targetSpeciesIndex();
            return mStand.unit().agent().type().SpeciesCompositionName(index);
        }

        public double U()
        {
            return mStand.U();
        }

        public string thinningIntensity()
        {
            int t = mStand.unit().thinningIntensity();
            return FomeScript.LevelLabel(t);
        }

        public double MAIChange()
        {
            // todo
            return mStand.unit().annualIncrement();
        }

        public double MAILevel()
        {
            return mStand.unit().averageMAI();
        }

        public double LandscapeMAI()
        {
            // hacky way of getting a MAI on landscape level
            double total_area = 0.0;
            double total_mai = 0.0;
            List<FMUnit> units = ForestManagementEngine.instance().units();
            for (int i = 0; i < units.Count; ++i)
            {
                total_area += units[i].area();
                total_mai += units[i].annualIncrement() * units[i].area();
            }
            if (total_area > 0.0)
            {
                return total_mai / total_area;
            }
            else
            {
                return 0.0;
            }
        }

        public double MortalityChange()
        {
            return 1; // todo
        }

        public double MortalityLevel()
        {
            return 1; // todo

        }

        public double RegenerationChange()
        {
            return 1; // todo

        }

        public double RegenerationLevel()
        {
            return 1; // todo

        }
    }
}
