using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.abe
{
    internal class FOMEWrapper : ExpressionWrapper
    {
        private static readonly List<string> allVarList;
        private static readonly List<string> standVarList;
        private static readonly List<string> siteVarList;
        private static readonly int siteVarListOffset;

        private readonly FMStand mStand;

        static FOMEWrapper()
        {
            FOMEWrapper.allVarList = new List<string>();
            FOMEWrapper.standVarList = new List<string>() { "basalArea", "age", "absoluteAge", "nspecies", "volume", "dbh", "height",
                                                            "annualIncrement", "elapsed", "topHeight", "area", "year" };
            FOMEWrapper.siteVarList = new List<string>() { "annualIncrement", "harvestMode", "U" };
            FOMEWrapper.siteVarListOffset = standVarList.Count; // stand vars start here...
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

        public override List<string> GetVariablesList()
        {
            if (allVarList.Count == 0)
            {
                buildVarList();
            }
            return allVarList;
        }

        public override double Value(int variableIndex)
        {
            // dispatch
            if (variableIndex > siteVarListOffset)
            {
                return ValueSite(variableIndex - siteVarListOffset);
            }
            return ValueStand(variableIndex);
        }

        private double ValueStand(int variableIndex)
        {
            //"basalArea", "age" , "absoluteAge", "speciesCount", "volume", dbh, height
            return variableIndex switch
            {
                0 => mStand.basalArea(),// "basalArea"
                1 => mStand.age(),// mean age, "age"
                2 => mStand.AbsoluteAge(),// years since begin of rotation, "absoluteAge"
                3 => mStand.SpeciesCount(),// species richness, "nspecies"
                4 => mStand.volume(),// total standing volume, m3/ha, "volume"
                5 => mStand.dbh(),// mean dbh
                6 => mStand.height(),// "height" (m)
                7 => mStand.meanAnnualIncrementTotal(),// annual increment (since beginning of the rotation) m3/ha
                8 => ForestManagementEngine.instance().currentYear() - mStand.lastExecution(),// years since last execution of an activity for the stand (yrs)
                9 => mStand.topHeight(),// top height (m)
                10 => mStand.area(),// stand area (ha)
                11 => GlobalSettings.Instance.CurrentYear,// the current year
                _ => 0,
            };
        }

        private double ValueSite(int variableIndex)
        {
            return variableIndex switch
            {
                0 => mStand.unit().annualIncrement(),// annualIncrement
                2 => mStand.U(),// just testing
                _ => 0,
            };
        }
    }
}
