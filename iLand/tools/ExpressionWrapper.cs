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
        protected static readonly List<string> baseVarList;

        static ExpressionWrapper()
        {
            baseVarList = new List<string>(1);
            baseVarList.Add("year");
        }

        public ExpressionWrapper()
        {
        }

        public abstract List<string> getVariablesList();

        // must be overloaded!
        public virtual double value(int variableIndex)
        {
            switch (variableIndex)
            {
                case 0: // year
                    return (double)GlobalSettings.instance().currentYear();
                default:
                    throw new NotSupportedException(string.Format("expression wrapper reached base with invalid index index {0}", variableIndex));
            }
        }

        public int variableIndex(string variableName)
        {
            return getVariablesList().IndexOf(variableName);
        }

        public double valueByName(string variableName)
        {
            int idx = variableIndex(variableName);
            return value(idx);
        }
    }
}
