using iLand.Simulation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace iLand.Tool
{
    /** base class for objects whose numerical properties can be accessed within Expressions
      Derived from ExpressionWrapper are wrappers for e.g. Trees or ResourceUnits.
      They must provide a getVariablesList() and a value() function.
      */
    // base class for exposing C# elements to the built-in Expression engine. See TreeWrapper for an example.
    public abstract class ExpressionVariableAccessor
    {
        protected static readonly ReadOnlyCollection<string> BaseVariableNames;

        public RandomGenerator? RandomGenerator { get; private init; }
        public SimulationState? SimulationState { get; private init; }

        static ExpressionVariableAccessor()
        {
            ExpressionVariableAccessor.BaseVariableNames = new List<string>() { "year" }.AsReadOnly();
        }

        protected ExpressionVariableAccessor(SimulationState? simulationState, RandomGenerator? randomGenerator)
        {
            this.RandomGenerator = randomGenerator;
            this.SimulationState = simulationState;
        }

        public abstract ReadOnlyCollection<string> GetVariableNames();

        public virtual float GetValue(int variableIndex)
        {
            if (variableIndex == 0)
            {
                if (this.SimulationState == null)
                {
                    throw new NotSupportedException("Attempt to obtain current year from wrapper but Model was not specified.");
                }
                return this.SimulationState.CurrentCalendarYear;
            }

            throw new NotSupportedException("Unhandled variable index " + variableIndex + ".");
        }

        public int GetVariableIndex(string variableName)
        {
            return this.GetVariableNames().IndexOf(variableName);
        }

        public float GetValueByName(string variableName)
        {
            int index = this.GetVariableIndex(variableName);
            return this.GetValue(index);
        }
    }
}
