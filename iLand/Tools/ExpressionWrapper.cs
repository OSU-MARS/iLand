using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace iLand.Tools
{
    /** @class ExpressionWrapper
      The base class for objects that can be used within Expressions.
      Derived from ExpressionWrapper are wrappers for e.g. Trees or ResourceUnits.
      They must provide a getVariablesList() and a value() function.
      Note: the must also provide "virtual double value(string variableName) { return value(variableName); }"
          because it seems to be not possible in C++ to use functions from derived and base class simultaneously that only differ in the
          argument signature.
      @sa Expression

      */
    /** ExpressionWrapper is the base class for exposing C++ elements
     *  to the built-in Expression engine. See TreeWrapper for an example.
     */
    public abstract class ExpressionWrapper
    {
        protected static readonly ReadOnlyCollection<string> BaseVariableNames;

        static ExpressionWrapper()
        {
            ExpressionWrapper.BaseVariableNames = new List<string>() { "year" }.AsReadOnly();
        }

        public abstract ReadOnlyCollection<string> GetVariablesList();

        // must be overloaded!
        public virtual double Value(int variableIndex, GlobalSettings globalSettings)
        {
            return variableIndex switch
            {
                // year
                0 => (double)globalSettings.CurrentYear,
                _ => throw new NotSupportedException(string.Format("expression wrapper reached base with invalid index index {0}", variableIndex)),
            };
        }

        public int GetVariableIndex(string variableName)
        {
            return GetVariablesList().IndexOf(variableName);
        }

        public double ValueByName(string variableName, GlobalSettings globalSettings)
        {
            int idx = GetVariableIndex(variableName);
            return Value(idx, globalSettings);
        }
    }
}
