using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.abe
{
    internal class DynamicExpression
    {
        private enum FilterType { ftInvalid, ftExpression, ftJavascript }
        private FilterType filter_type;
        private Expression expr;
        private QJSValue func;

        public bool isValid() { return filter_type != FilterType.ftInvalid; }

        public DynamicExpression()
        {
            filter_type = FilterType.ftInvalid;
            expr = null;
        }

        public void setup(QJSValue js_value)
        {
            filter_type = FilterType.ftInvalid;
            expr = null;

            if (js_value.isCallable())
            {
                func = js_value;
                filter_type = FilterType.ftJavascript;
                return;
            }
            if (js_value.isString())
            {
                // we assume this is an expression

                string exprstr = js_value.toString();
                // replace "." with "__" in variables (our Expression engine is
                // not able to cope with the "."-notation
                exprstr = exprstr.Replace("activity.", "activity__");
                exprstr = exprstr.Replace("stand.", "stand__");
                exprstr = exprstr.Replace("site.", "site__");
                exprstr = exprstr.Replace("unit.", "unit__");
                // add ....
                expr = new Expression(exprstr);
                filter_type = FilterType.ftExpression;
                return;
            }
        }

        public bool evaluate(FMStand stand)
        {
            switch (filter_type)
            {
                case FilterType.ftInvalid:
                    return true; // message?
                case FilterType.ftExpression:
                    FOMEWrapper wrapper = new FOMEWrapper(stand);
                    double expressionResult = expr.execute(null, wrapper); // using execute, we're in strict mode, i.e. wrong variables are reported.
                                                                        //result = expr.calculate(wrapper);
                    if (FMSTP.verbose())
                    {
                        Debug.WriteLine(stand.context() + " evaluate constraint (expr:" + expr.expression() + ") for stand " + stand.id() + ": " + expressionResult);
                    }
                    return expressionResult > 0.0;
                case FilterType.ftJavascript:
                    // call javascript function
                    // provide the execution context
                    FomeScript.setExecutionContext(stand);
                    QJSValue result = func.call();
                    if (result.isError())
                    {
                        throw new NotSupportedException(String.Format("Error in evaluating constraint (JS) for stand {0}: {1}", stand.id(), result.toString()));
                    }
                    if (FMSTP.verbose())
                    {
                        Debug.WriteLine("evaluate constraint (JS) for stand " + stand.id() + " : " + result.toString());
                    }
                    // convert boolean result to 1 - 0
                    if (result.isBool())
                    {
                        return result.toBool();
                    }
                    else
                    {
                        return result.toNumber() != 0.0;
                    }
            }
            return true;
        }

        public string dump()
        {
            switch (filter_type)
            {
                case FilterType.ftInvalid: return "Invalid";
                case FilterType.ftExpression: return expr.expression();
                case FilterType.ftJavascript: return func.toString();
                default: return "invalid filter type!";
            }
        }
    }
}
