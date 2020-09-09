using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    internal class Constraints
    {
        private List<DynamicExpression> mConstraints;

        public Constraints()
        {
        }

        public void setup(QJSValue js_value)
        {
            mConstraints.Clear();
            if ((js_value.isArray() || js_value.isObject()) && !js_value.isCallable())
            {
                QJSValueIterator it = new QJSValueIterator(js_value);
                while (it.hasNext())
                {
                    it.next();
                    if (it.name() == "length")
                    {
                        continue;
                    }
                    DynamicExpression expression = new DynamicExpression();
                    expression.setup(it.value());
                    mConstraints.Add(expression);
                }
            }
            else
            {
                DynamicExpression expression = new DynamicExpression();
                expression.setup(js_value);
                mConstraints.Add(expression);
            }
        }

        public double evaluate(FMStand stand)
        {
            if (mConstraints.Count == 0)
            {
                return 1.0; // no constraints to evaluate
            }
            
            double p_min = 1;
            for (int i = 0; i < mConstraints.Count; ++i)
            {
                double p = mConstraints[i].evaluate(stand) ? 1.0 : 0.0;
                if (p == 0.0)
                {
                    if (stand.trace())
                    {
                        Debug.WriteLine(stand.context() + " constraint " + mConstraints[i].dump() + " did not pass.");
                    }
                    return 0.0; // one constraint failed
                }
                else
                {
                    // save the lowest value...
                    p_min = Math.Min(p, p_min);
                }
            }
            return p_min; // all constraints passed, return the lowest returned value...
        }

        public List<string> dump()
        {
            List<string> info = new List<string>();
            for (int i = 0; i < mConstraints.Count; ++i)
            {
                info.Add(String.Format("constraint: {0}", mConstraints[i].dump()));
            }
            return info;
        }
    }
}
