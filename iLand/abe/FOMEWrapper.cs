using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.abe
{
    internal class FOMEWrapper : ExpressionWrapper
    {
        private static List<string> allVarList;
        private readonly static List<string> standVarList;
        private readonly static List<string> siteVarList;
        private readonly static int siteVarListOffset;

        private FMStand mStand;

        static FOMEWrapper()
        {
            standVarList = new List<string>() { "basalArea", "age", "absoluteAge", "nspecies", "volume", "dbh", "height",
                                                "annualIncrement", "elapsed", "topHeight", "area", "year" };
            siteVarList = new List<string>() { "annualIncrement", "harvestMode", "U" };
            siteVarListOffset = standVarList.Count; // stand vars start here...
        }

        public FOMEWrapper()
        {
            mStand = null;
        }

        public FOMEWrapper(FMStand stand)
        {
            mStand = stand;
        }

        // setup the variable names
        // we use "__" internally (instead of .)
        private static void buildVarList()
        {
            allVarList.Clear();
            foreach (string var in standVarList)
            {
                allVarList.Add(String.Format("stand__{0}", var));
            }
            foreach (string var in siteVarList)
            {
                allVarList.Add(String.Format("site__{0}", var));
            }
        }

        public override List<string> getVariablesList()
        {
            if (allVarList.Count == 0)
            {
                buildVarList();
            }
            return allVarList;
        }

        public double value(int variableIndex)
        {
            // dispatch
            if (variableIndex > siteVarListOffset)
            {
                return valueSite(variableIndex - siteVarListOffset);
            }
            return valueStand(variableIndex);
        }

        private double valueStand(int variableIndex)
        {
            //"basalArea", "age" , "absoluteAge", "speciesCount", "volume", dbh, height
            switch (variableIndex)
            {
                case 0: return mStand.basalArea(); // "basalArea"
                case 1: return mStand.age(); // mean age, "age"
                case 2: return mStand.absoluteAge(); // years since begin of rotation, "absoluteAge"
                case 3: return mStand.nspecies(); // species richness, "nspecies"
                case 4: return mStand.volume(); // total standing volume, m3/ha, "volume"
                case 5: return mStand.dbh(); // mean dbh
                case 6: return mStand.height(); // "height" (m)
                case 7: return mStand.meanAnnualIncrementTotal(); // annual increment (since beginning of the rotation) m3/ha
                case 8: return ForestManagementEngine.instance().currentYear() - mStand.lastExecution(); // years since last execution of an activity for the stand (yrs)
                case 9: return mStand.topHeight(); // top height (m)
                case 10: return mStand.area(); // stand area (ha)
                case 11: return GlobalSettings.instance().currentYear(); // the current year
                default: return 0;
            }
        }

        private double valueSite(int variableIndex)
        {
            switch (variableIndex)
            {
                case 0: return mStand.unit().annualIncrement(); // annualIncrement
                case 2: return mStand.U(); // just testing
                default: return 0;
            }
        }
    }
}
