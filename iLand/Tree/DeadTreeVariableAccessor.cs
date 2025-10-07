// C++/tools/{ expressionwrapper.h, expressionwrapper.cpp }
using iLand.Simulation;
using iLand.Tool;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.Tree
{
    public class DeadTreeVariableAccessor : ExpressionVariableAccessor // C++ DeadTreeWrapper
    {
        private static readonly ReadOnlyCollection<string> VariableNames;

        public int DeadTreeIndex { get; set; }
        public DeadTreeListSpatial? DeadTrees { get; set; }

        static DeadTreeVariableAccessor()
        {
            DeadTreeVariableAccessor.VariableNames = new List<string>(ExpressionVariableAccessor.BaseVariableNames)
            {
                "x",  "y", // 0,1
                "snag", // 2
                "species", "volume", // 3,4
                "decayClass", "biomass", "fractionOfInitialBiomassRemaining", // 5,6,7
                "yearsStanding", "yearsDown", "reason" // 8,9, 10
            }.AsReadOnly();
        }

        public DeadTreeVariableAccessor(SimulationState? simulationState)
            : base(simulationState)
        {
            this.DeadTreeIndex = 0;
            this.DeadTrees = null;
        }
        
        public DeadTreeVariableAccessor(DeadTreeListSpatial deadTrees, SimulationState? simulationState)
            : base(simulationState)
        { 
            this.DeadTrees = deadTrees;
        }

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return DeadTreeVariableAccessor.VariableNames;
        }

        public override float GetValue(int variableIndex)
        {
            Debug.Assert((this.DeadTrees != null) && (this.DeadTrees.Species != null));

            return (variableIndex - ExpressionVariableAccessor.BaseVariableNames.Count) switch
            {
                0 => this.DeadTrees.LightCellIndexXY[this.DeadTreeIndex].X, // x
                1 => this.DeadTrees.LightCellIndexXY[this.DeadTreeIndex].Y, // y
                2 => this.DeadTrees.IsStanding[this.DeadTreeIndex] ? 1.0F : 0.0F, // snag
                3 => this.DeadTrees.Species.Index, // species
                4 => this.DeadTrees.Volume[this.DeadTreeIndex], // volume
                5 => this.DeadTrees.DecayClass[this.DeadTreeIndex], // decayClass
                6 => this.DeadTrees.Biomass[this.DeadTreeIndex], // biomass
                7 => this.DeadTrees.Biomass[this.DeadTreeIndex] / this.DeadTrees.InitialBiomass[this.DeadTreeIndex], // fraction of initial biomass remaining
                8 => this.DeadTrees.YearsStanding[this.DeadTreeIndex], // yearsStanding
                9 => this.DeadTrees.YearsDown[this.DeadTreeIndex], // yearsDowned
                10 => (float)this.DeadTrees.Reason[this.DeadTreeIndex], // reason of death
                _ => base.GetValue(variableIndex),
            };
        }
    }
}
