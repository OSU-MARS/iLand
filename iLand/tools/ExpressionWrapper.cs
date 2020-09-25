using System;
using System.Collections.Generic;

namespace iLand.tools
{
    /** @class ExpressionWrapper
      @ingroup tools
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
    internal abstract class ExpressionWrapper
    {
        protected static readonly List<string> BaseVarList;

        static ExpressionWrapper()
        {
            ExpressionWrapper.BaseVarList = new List<string>() { "year" };
        }

        public abstract List<string> GetVariablesList();

        // must be overloaded!
        public virtual double Value(int variableIndex)
        {
            return variableIndex switch
            {
                // year
                0 => (double)GlobalSettings.Instance.CurrentYear,
                _ => throw new NotSupportedException(string.Format("expression wrapper reached base with invalid index index {0}", variableIndex)),
            };
        }

        public int GetVariableIndex(string variableName)
        {
            return GetVariablesList().IndexOf(variableName);
        }

        public double ValueByName(string variableName)
        {
            int idx = GetVariableIndex(variableName);
            return Value(idx);
        }
    }
}
