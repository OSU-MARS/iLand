using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.abe
{
    internal class DynamicExpression
    {
        private enum FilterType { Invalid, Expression, Javascript }
        private FilterType filter_type;
        private Expression expr;
        private QJSValue func;

        public DynamicExpression()
        {
            filter_type = FilterType.Invalid;
            expr = null;
        }

        public bool IsValid() { return filter_type != FilterType.Invalid; }

        public void Setup(QJSValue js_value)
        {
            filter_type = FilterType.Invalid;
            expr = null;

            if (js_value.IsCallable())
            {
                func = js_value;
                filter_type = FilterType.Javascript;
                return;
            }
            if (js_value.IsString())
            {
                // we assume this is an expression

                string exprstr = js_value.ToString();
                // replace "." with "__" in variables (our Expression engine is
                // not able to cope with the "."-notation
                exprstr = exprstr.Replace("activity.", "activity__");
                exprstr = exprstr.Replace("stand.", "stand__");
                exprstr = exprstr.Replace("site.", "site__");
                exprstr = exprstr.Replace("unit.", "unit__");
                // add ....
                expr = new Expression(exprstr);
                filter_type = FilterType.Expression;
                return;
            }
        }

        public bool Evaluate(FMStand stand)
        {
            switch (filter_type)
            {
                case FilterType.Invalid:
                    return true; // message?
                case FilterType.Expression:
                    FOMEWrapper wrapper = new FOMEWrapper(stand);
                    double expressionResult = expr.Execute(null, wrapper); // using execute, we're in strict mode, i.e. wrong variables are reported.
                                                                        //result = expr.calculate(wrapper);
                    if (FMSTP.verbose())
                    {
                        Debug.WriteLine(stand.context() + " evaluate constraint (expr:" + expr.ExpressionString + ") for stand " + stand.id() + ": " + expressionResult);
                    }
                    return expressionResult > 0.0;
                case FilterType.Javascript:
                    // call javascript function
                    // provide the execution context
                    FomeScript.SetExecutionContext(stand);
                    QJSValue result = func.Call();
                    if (result.IsError())
                    {
                        throw new NotSupportedException(String.Format("Error in evaluating constraint (JS) for stand {0}: {1}", stand.id(), result.ToString()));
                    }
                    if (FMSTP.verbose())
                    {
                        Debug.WriteLine("evaluate constraint (JS) for stand " + stand.id() + " : " + result.ToString());
                    }
                    // convert boolean result to 1 - 0
                    if (result.IsBool())
                    {
                        return result.ToBool();
                    }
                    else
                    {
                        return result.ToNumber() != 0.0;
                    }
            }
            return true;
        }

        public string Dump()
        {
            return filter_type switch
            {
                FilterType.Invalid => "Invalid",
                FilterType.Expression => expr.ExpressionString,
                FilterType.Javascript => func.ToString(),
                _ => "invalid filter type!",
            };
        }
    }
}
