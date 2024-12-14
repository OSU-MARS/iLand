// C++/tool/{ expressionwrapper.h, expressionwrapper.cpp }
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.World
{
    internal class SaplingVariableAccessor : ExpressionVariableAccessor // C++: SaplingWrapper
    {
        private static readonly ReadOnlyCollection<string> VariableNames;

        private Sapling? sapling;
        private ResourceUnit? resourceUnit;

        static SaplingVariableAccessor()
        {
            SaplingVariableAccessor.VariableNames = new List<string>(ExpressionVariableAccessor.BaseVariableNames)
            {
               "species", "height", "age", // 0-2
               "nrep", "dbh", "foliagemass", // 3,4,5
               "x", "y", "patch" // 6,7,8
            }.AsReadOnly();
        }

        public SaplingVariableAccessor(SimulationState simulationState)
            : base(simulationState, null)
        {
            this.resourceUnit = null;
            this.sapling = null;
        }

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return SaplingVariableAccessor.VariableNames;
        }

        public override float GetValue(int variableIndex)
        {
            Debug.Assert((this.resourceUnit != null) && (this.sapling != null));

            switch (variableIndex - ExpressionVariableAccessor.BaseVariableNames.Count)
            {
                case 0: return this.sapling.SpeciesIndex; // Note: this is the numeric value that is also used for the constant species names in expressions!
                case 1: return this.sapling.HeightInM;
                case 2: return this.sapling.Age;
                case 3: return this.sapling.GetResourceUnitSpecies(this.resourceUnit).Species.SaplingGrowth.RepresentedStemNumberFromHeight(this.sapling.HeightInM);
                case 4: return 100.0F * this.sapling.HeightInM / this.sapling.GetResourceUnitSpecies(this.resourceUnit).Species.SaplingGrowth.HeightDiameterRatio;
                case 5:
                    TreeSpecies sp = this.sapling.GetResourceUnitSpecies(this.resourceUnit).Species;
                    float dbh = 100.0F * this.sapling.HeightInM / sp.SaplingGrowth.HeightDiameterRatio;
                    return sp.GetBiomassFoliage(dbh);
                case 6:
                    return this.resourceUnit.GetSaplingCellPosition(this.sapling).X;
                case 7:
                    return this.resourceUnit.GetSaplingCellPosition(this.sapling).Y;
                case 8:
                    //PointF p = this.mRU.GetSaplingCellPosition(this.mSapling);
                    //return ABE::Patches::getPatch(p);
                    throw new NotSupportedException("Patch access requires the agent based engine, which has not been ported.");
                default:
                    return base.GetValue(variableIndex);
            }
        }

        public void SetSapling(Sapling sapling, ResourceUnit ru) // C++: SaplingWrapper::setSaplingTree()
        {
            this.resourceUnit = ru;
            this.sapling = sapling;
        }
    }
}
