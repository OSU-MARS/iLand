using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    internal class Constraints
    {
        private readonly List<DynamicExpression> mConstraints;

        public Constraints()
        {
            this.mConstraints = new List<DynamicExpression>();
        }

        public void Setup(QJSValue js_value)
        {
            mConstraints.Clear();
            if ((js_value.IsArray() || js_value.IsObject()) && !js_value.IsCallable())
            {
                QJSValueIterator it = new QJSValueIterator(js_value);
                while (it.HasNext())
                {
                    it.Next();
                    if (it.Name() == "length")
                    {
                        continue;
                    }
                    DynamicExpression expression = new DynamicExpression();
                    expression.Setup(it.Value());
                    mConstraints.Add(expression);
                }
            }
            else
            {
                DynamicExpression expression = new DynamicExpression();
                expression.Setup(js_value);
                mConstraints.Add(expression);
            }
        }

        public double Evaluate(FMStand stand)
        {
            if (mConstraints.Count == 0)
            {
                return 1.0; // no constraints to evaluate
            }
            
            double p_min = 1;
            for (int i = 0; i < mConstraints.Count; ++i)
            {
                double p = mConstraints[i].Evaluate(stand) ? 1.0 : 0.0;
                if (p == 0.0)
                {
                    if (stand.TracingEnabled())
                    {
                        Debug.WriteLine(stand.context() + " constraint " + mConstraints[i].Dump() + " did not pass.");
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

        public List<string> Dump()
        {
            List<string> info = new List<string>();
            for (int i = 0; i < mConstraints.Count; ++i)
            {
                info.Add(String.Format("constraint: {0}", mConstraints[i].Dump()));
            }
            return info;
        }
    }
}
