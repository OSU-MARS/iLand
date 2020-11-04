﻿using iLand.Simulation;
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

        public Model Model { get; private set; }

        static ExpressionWrapper()
        {
            ExpressionWrapper.BaseVariableNames = new List<string>() { "year" }.AsReadOnly();
        }

        protected ExpressionWrapper(Model model)
        {
            this.Model = model;
        }

        public abstract ReadOnlyCollection<string> GetVariableNames();

        // must be overloaded!
        public virtual double GetValue(int variableIndex)
        {
            return variableIndex switch
            {
                // year
                0 => this.Model.CurrentYear,
                _ => throw new NotSupportedException(string.Format("expression wrapper reached base with invalid index index {0}", variableIndex)),
            };
        }

        public int GetVariableIndex(string variableName)
        {
            return this.GetVariableNames().IndexOf(variableName);
        }

        public double GetValueByName(string variableName)
        {
            int index = this.GetVariableIndex(variableName);
            return this.GetValue(index);
        }
    }
}
