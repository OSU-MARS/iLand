using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iLand.tools
{
    /** @class Expression
      An expression engine for mathematical expressions provided as strings.
      @ingroup tools
      @ingroup script
      The main purpose is fast execution speed.
      notes regarding the syntax:
      +,-,*,/ as expected, additionally "^" for power.
      mod(x,y): modulo division, gets remainder of x/y
      functions:
        - sin cos tan
        - exp ln sqrt
        - round
        - min max: variable number of arguments, e.g: min(x,y,z)
        - if: if(condition, true, false): if condition=true, return true-case, else false-case. note: both (true, false) are evaluated anyway!
        - incsum: ?? incremental sum - currently not supported.
        - polygon: special function for polygons. polygon(value, x1,y1, x2,y2, x3,y3, ..., xn,yn): return is: y1 if value<x1, yn if value>xn, or the lineraly interpolated numeric y-value.
        - sigmoid: returns a sigmoid function. sigmoid(value, type, param1, param2). see udfSigmoid() for details.
        - rnd rndg: random functions; rnd(from, to): uniform random number, rndg(mean, stddev): gaussian randomnumber (mean and stddev in percent!)
        - in: returns true if the value is in the list of arguments in in(x, a1, a2, a3)
        The Expression class also supports some logical operations:
        (logical) True equals to "1", "False" to zero. The precedence rules for parentheses...
        - and
        - or
        - not
      @par Using Model Variables
      With the help of descendants of ExpressionWrapper values of model objects can be accessed. Example Usage:
      @code
      TreeWrapper wrapper;
      Expression basalArea("dbh*dbh*3.1415/4", &wrapper); // expression for basal area, add wrapper (see also setModelObject())
      AllTreeIterator at(GlobalSettings::instance()->model()); // iterator to iterate over all tree in the model
      double sum;
      while (Tree *tree = at.next()) {
          wrapper.setTree(tree); // set actual tree
          sum += basalArea.execute(); // execute calculation
      }
      @endcode

      Be careful with multithreading:
      Now the calculate(double v1, double v2) as well as the calculate(wrapper, v1,v2) are thread safe. execute() accesses the internal variable list and is therefore not thredsafe.
      A threadsafe version exists (executeLocked()). Special attention is needed when using setVar() or addVar().
    */
    internal class Expression
    {
        enum Operation
        {
            opEqual = 1,
            opGreaterThen = 2,
            opLowerThen = 3,
            opNotEqual = 4,
            opLowerOrEqual = 5,
            opGreaterOrEqual = 6,
            opAnd = 7,
            opOr = 8
        }

        private const string mathFuncList = " sin cos tan exp ln sqrt min max if incsum polygon mod sigmoid rnd rndg in round "; // a space at the end is important!
        private static readonly int[] MaxArgCount = new int[] { 1, 1, 1, 1, 1, 1, -1, -1, 3, 1, -1, 2, 4, 2, 2, -1, 1 };
        private static readonly string[] AggFuncList = new string[] { "sum", "avg", "max", "min", "stddev", "variance" };

        // space for constants
        private static Dictionary<string, double> mConstants;

        private enum ETokType { etNumber, etOperator, etVariable, etFunction, etLogical, etCompare, etStop, etUnknown, etDelimeter };
        private enum EValueClasses { evcBHD, evcHoehe, evcAlter };

        private struct ExtExecListItem
        {
            public ETokType Type;
            public double Value;
            public int Index;
        }

        private enum EDatatype { edtInfo, edtNumber, edtString, edtObject, edtVoid, edtObjVar, edtReference, edtObjectReference };
        private bool m_catchExceptions;
        private string m_errorMsg;

        // inc-sum
        private double m_incSumVar;
        private bool m_incSumEnabled;

        private bool m_parsed;
        private bool m_strict;
        private bool m_empty; // empty expression
        private bool m_constExpression;
        private string m_tokString;
        private string m_expression;
        private ExtExecListItem[] m_execList;
        private int m_execListSize; // size of buffer
        private int m_execIndex;
        private double[] m_varSpace;
        private List<string> m_varList;
        private List<string> m_externVarNames;
        private double[] m_externVarSpace;
        private ETokType m_state;
        private ETokType m_lastState;
        private int m_pos;
        private string m_expr;
        private string m_token;
        private string m_prepStr;
        private int m_tokCount;

        // link to external model variable
        private ExpressionWrapper mModelObject;

        private object m_execMutex;
        // linearization
        private int mLinearizeMode;
        private List<double> mLinearized;
        private double mLinearLow, mLinearHigh;
        private double mLinearStep;
        private double mLinearLowY, mLinearHighY;
        private double mLinearStepY;
        private int mLinearStepCountY;
        private static bool mLinearizationAllowed;

        // mutex used to serialize expression parsing.
        private object mutex;

        public void setModelObject(ExpressionWrapper wrapper) { mModelObject = wrapper; }
        public string expression() { return m_expression; }
        public static void setLinearizationEnabled(bool enable) { mLinearizationAllowed = enable; }

        public bool isConstExpression() { return m_constExpression; } ///< returns true if current expression is a constant.
        public bool isEmpty() { return m_empty; } ///< returns true if expression is empty
        public string lastError() { return m_errorMsg; }
        /** strict property: if true, variables must be named before execution.
          When strict=true, all variables in the expression must be added by setVar or addVar.
          if false, variable values are assigned depending on occurence. strict is false by default for calls to "calculate()".
        */
        public bool isStrict() { return m_strict; }
        public void setStrict(bool str) { m_strict = str; }
        public void setCatchExceptions(bool docatch = true) { m_catchExceptions = docatch; }

        public Expression()
        {
            m_expr = null;
            m_execList = default;
            m_execMutex = new object();
            m_externVarSpace = null;
            mLinearizationAllowed = false;
            mModelObject = null;
            mutex = new object();
            m_varSpace = new double[10];
        }

        public Expression(string expression)
            : this()
        {
            setExpression(expression);
        }

        public Expression(string expression, ExpressionWrapper wrapper)
            : this(expression)
        {
            mModelObject = wrapper;
        }

        public double executeLocked() ///< thread safe version
        {
            lock (m_execMutex)
            {
                return execute();
            }
        }

        public static void addConstant(string const_name, double const_value)
        {
            mConstants[const_name] = const_value;
        }

        private ETokType next_token()
        {
            m_tokCount++;
            m_lastState = m_state;
            // nchsten m_token aus String lesen...
            // whitespaces eliminieren...
            while (" \t\n\r".Contains(m_expr[m_pos]) && (m_pos < m_expr.Length))
            {
                m_pos++;
            }

            if (m_pos >= m_expr.Length)
            {
                m_state = ETokType.etStop;
                m_token = "";
                return ETokType.etStop; // Ende der Vorstellung
            }

            // whitespaces eliminieren...
            while (" \t\n\r".Contains(m_expr[m_pos]))
            {
                m_pos++;
            }
            if (m_expr[m_pos] == ',')
            {
                m_token = new string(m_expr[m_pos++], 1);
                m_state = ETokType.etDelimeter;
                return ETokType.etDelimeter;
            }
            if ("+-*/(){}^".Contains(m_expr[m_pos]))
            {
                m_token = new string(m_expr[m_pos++], 1);
                m_state = ETokType.etOperator;
                return ETokType.etOperator;
            }
            if ("=<>".Contains(m_expr[m_pos]))
            {
                m_token = new string(m_expr[m_pos++], 1);
                if (m_expr[m_pos] == '>' || m_expr[m_pos] == '=')
                {
                    m_token += m_expr[m_pos++];
                }
                m_state = ETokType.etCompare;
                return ETokType.etCompare;
            }
            if (m_expr[m_pos] >= '0' && m_expr[m_pos] <= '9')
            {
                // Zahl
                int startPosition = m_pos;
                while ("0123456789.".Contains(m_expr[m_pos]) && (m_pos < m_expr.Length))
                {
                    m_pos++;  // nchstes Zeichen suchen...
                }
                m_token = m_expr.Substring(startPosition, m_pos - startPosition + 1);
                m_state = ETokType.etNumber;
                return ETokType.etNumber;
            }

            if ((m_expr[m_pos] >= 'a' && m_expr[m_pos] <= 'z') || (m_expr[m_pos] >= 'A' && m_expr[m_pos] <= 'Z'))
            {
                // function ... find brace
                m_token = "";
                while (((m_expr[m_pos] >= 'a' && m_expr[m_pos] <= 'z') || (m_expr[m_pos] >= 'A' && m_expr[m_pos] <= 'Z') || (m_expr[m_pos] >= '0' && m_expr[m_pos] <= '9') || (m_expr[m_pos] == '_' || m_expr[m_pos] == '.')) &&
                         m_expr[m_pos] != '(' && m_pos != 0)
                {
                    m_token += m_expr[m_pos++];
                }
                // wenn am Ende Klammer, dann Funktion, sonst Variable.
                if (m_expr[m_pos] == '(' || m_expr[m_pos] == '{')
                {
                    m_pos++; // skip brace
                    m_state = ETokType.etFunction;
                    return ETokType.etFunction;
                }
                else
                {
                    if (m_token.ToLowerInvariant() == "and" || m_token.ToLowerInvariant() == "or")
                    {
                        m_state = ETokType.etLogical;
                        return ETokType.etLogical;
                    }
                    else
                    {
                        m_state = ETokType.etVariable;
                        if (m_token == "true")
                        {
                            m_state = ETokType.etNumber;
                            m_token = "1";
                            return ETokType.etNumber;
                        }
                        if (m_token == "false")
                        {
                            m_state = ETokType.etNumber;
                            m_token = "0";
                            return ETokType.etNumber;
                        }
                        return ETokType.etVariable;
                    }
                }
            }
            m_state = ETokType.etUnknown;
            return ETokType.etUnknown; // in case no match was found
        }

        /** sets expression @p expr and checks the syntax (parse).
            Expressions are setup with strict = false, i.e. no fixed binding of variable names.
          */
        public void setAndParse(string expr)
        {
            setExpression(expr);
            m_strict = false;
            parse();
        }

        /// set the current expression.
        /// do some preprocessing (e.g. handle the different use of ",", ".", ";")
        public void setExpression(string aExpression)
        {
            m_expression = String.Join(' ', aExpression.Trim().Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            m_expr = m_expression; // TODO: remove m_expr
            m_pos = 0;  // set starting point...

            for (int i = 0; i < m_varSpace.Length; i++)
            {
                m_varSpace[i] = 0.0;
            }
            m_parsed = false;
            m_catchExceptions = false;
            m_errorMsg = "";

            mModelObject = null;
            m_externVarSpace = null;

            m_strict = true; // default....
            m_incSumEnabled = false;
            m_empty = String.Equals(aExpression.Trim(), String.Empty, StringComparison.OrdinalIgnoreCase);
            // Buffer:
            m_execListSize = 5; // inital value...
            if (m_execList == null)
            {
                m_execList = new ExtExecListItem[m_execListSize]; // init
            }

            mLinearizeMode = 0; // linearization is switched off
        }

        public void parse(ExpressionWrapper wrapper = null)
        {
            lock (mutex)
            {
                if (m_parsed)
                {
                    return;
                }

                try
                {
                    if (wrapper != null)
                    {
                        mModelObject = wrapper;
                    }
                    m_tokString = "";
                    m_state = ETokType.etUnknown;
                    m_lastState = ETokType.etUnknown;
                    m_constExpression = true;
                    m_execIndex = 0;
                    m_tokCount = 0;
                    int aktTok;
                    next_token();
                    while (m_state != ETokType.etStop)
                    {
                        m_tokString += "\n" + m_token;
                        aktTok = m_tokCount;
                        parse_levelL0();  // start with logical level 0
                        if (aktTok == m_tokCount)
                        {
                            throw new NotSupportedException("parse(): Unbalanced Braces.");
                        }
                        if (m_state == ETokType.etUnknown)
                        {
                            m_tokString += "\n***Error***";
                            throw new NotSupportedException("parse(): Syntax error, token: " + m_token);
                        }
                    }
                    m_empty = (m_execIndex == 0);
                    m_execList[m_execIndex].Type = ETokType.etStop;
                    m_execList[m_execIndex].Value = 0;
                    m_execList[m_execIndex++].Index = 0;
                    checkBuffer(m_execIndex);
                    m_parsed = true;

                }
                catch (Exception e)
                {
                    m_errorMsg = String.Format("parse: error in '{0}'", m_expression);
                    if (m_catchExceptions)
                    {
                        Helper.msg(m_errorMsg, e);
                    }
                    else
                    {
                        throw new NotSupportedException(m_errorMsg, e);
                    }
                }
            }
        }

        private void parse_levelL0()
        {
            // logical operations  (and, or, not)
            string op;
            parse_levelL1();

            while (m_state == ETokType.etLogical)
            {
                op = m_token.ToLowerInvariant();
                next_token();
                parse_levelL1();
                Operation logicaltok = 0;
                if (op == "and")
                {
                    logicaltok = Operation.opAnd;
                }
                if (op == "or")
                {
                    logicaltok = Operation.opOr;
                }

                m_execList[m_execIndex].Type = ETokType.etLogical;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)logicaltok;
                checkBuffer(m_execIndex);
            }
        }

        private void parse_levelL1()
        {
            // logische operationen (<,>,=,...)
            string op;
            parse_level0();
            //double temp=FResult;
            if (m_state == ETokType.etCompare)
            {
                op = m_token;
                next_token();
                parse_level0();
                Operation logicaltok = 0;
                if (op == "<") logicaltok = Operation.opLowerThen;
                if (op == ">") logicaltok = Operation.opGreaterThen;
                if (op == "<>") logicaltok = Operation.opNotEqual;
                if (op == "<=") logicaltok = Operation.opLowerOrEqual;
                if (op == ">=") logicaltok = Operation.opGreaterOrEqual;
                if (op == "=") logicaltok = Operation.opEqual;

                m_execList[m_execIndex].Type = ETokType.etCompare;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)logicaltok;
                checkBuffer(m_execIndex);
            }
        }

        private void parse_level0()
        {
            // plus und minus
            parse_level1();

            while (m_token == "+" || m_token == "-")
            {
                next_token();
                parse_level1();
                m_execList[m_execIndex].Type = ETokType.etOperator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)m_token[0];///op.constData()[0];
                checkBuffer(m_execIndex);
            }
        }

        private void parse_level1()
        {
            // mal und division
            parse_level2();
            //double temp=FResult;
            // alt:        if (m_token=="*" || m_token=="/") {
            while (m_token == "*" || m_token == "/")
            {
                next_token();
                parse_level2();
                m_execList[m_execIndex].Type = ETokType.etOperator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)m_token[0];
                checkBuffer(m_execIndex);
            }
        }

        private void atom()
        {
            if (m_state == ETokType.etVariable || m_state == ETokType.etNumber)
            {
                if (m_state == ETokType.etNumber)
                {
                    double result = double.Parse(m_token);
                    m_execList[m_execIndex].Type = ETokType.etNumber;
                    m_execList[m_execIndex].Value = result;
                    m_execList[m_execIndex++].Index = -1;
                    checkBuffer(m_execIndex);
                }
                if (m_state == ETokType.etVariable)
                {
                    if (mConstants.ContainsKey(m_token))
                    {
                        // constant
                        double result = mConstants[m_token];
                        m_execList[m_execIndex].Type = ETokType.etNumber;
                        m_execList[m_execIndex].Value = result;
                        m_execList[m_execIndex++].Index = -1;
                        checkBuffer(m_execIndex);

                    }
                    else
                    {
                        // 'real' variable
                        if (!m_strict) // in strict mode, the variable must be available by external bindings. in "lax" mode, the variable is added when encountered first.
                        {
                            addVar(m_token);
                        }
                        m_execList[m_execIndex].Type = ETokType.etVariable;
                        m_execList[m_execIndex].Value = 0;
                        m_execList[m_execIndex++].Index = getVarIndex(m_token);
                        checkBuffer(m_execIndex);
                        m_constExpression = false;
                    }
                }
                next_token();
            }
            else if (m_state == ETokType.etStop || m_state == ETokType.etUnknown)
            {
                throw new NotSupportedException("Unexpected end of m_expression.");
            }
        }

        private void parse_level2()
        {
            // x^y
            parse_level3();
            //double temp=FResult;
            while (m_token == "^")
            {
                next_token();
                parse_level3();
                //FResult=pow(temp,FResult);
                m_execList[m_execIndex].Type = ETokType.etOperator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = '^';
                checkBuffer(m_execIndex);
            }
        }

        private void parse_level3()
        {
            // unary operator (- bzw. +)
            string op;
            op = m_token;
            bool Unary = false;
            if (op == "-" && (m_lastState == ETokType.etOperator || m_lastState == ETokType.etUnknown || m_lastState == ETokType.etCompare || m_lastState == ETokType.etLogical || m_lastState == ETokType.etFunction))
            {
                next_token();
                Unary = true;
            }
            parse_level4();
            if (Unary && op == "-")
            {
                //FResult=-FResult;
                m_execList[m_execIndex].Type = ETokType.etOperator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = '_';
                checkBuffer(m_execIndex);
            }
        }

        private void parse_level4()
        {
            // Klammer und Funktionen
            string func;
            atom();
            //double temp=FResult;
            if (m_token == "(" || m_state == ETokType.etFunction)
            {
                func = m_token;
                if (func == "(")   // klammerausdruck
                {
                    next_token();
                    parse_levelL0();
                }
                else        // funktion...
                {
                    int argcount = 0;
                    int idx = getFuncIndex(func);
                    next_token();
                    //m_token="{";
                    // bei funktionen mit mehreren Parametern
                    while (m_token != ")")
                    {
                        argcount++;
                        parse_levelL0();
                        if (m_state == ETokType.etDelimeter)
                        {
                            next_token();
                        }
                    }
                    if (MaxArgCount[idx] > 0 && MaxArgCount[idx] != argcount)
                    {
                        throw new NotSupportedException(String.Format("Function {0} assumes {1} arguments!", func, MaxArgCount[idx]));
                    }
                    //throw std::logic_error("Funktion " + func + " erwartet " + std::string(MaxArgCount[idx]) + " Parameter!");
                    m_execList[m_execIndex].Type = ETokType.etFunction;
                    m_execList[m_execIndex].Value = argcount;
                    m_execList[m_execIndex++].Index = idx;
                    checkBuffer(m_execIndex);
                }
                if (m_token != "}" && m_token != ")") // Fehler
                {
                    throw new NotSupportedException(String.Format("unbalanced number of parentheses in [%1].", m_expression));
                }
                next_token();
            }
        }

        public void setVar(string Var, double Value)
        {
            if (!m_parsed)
            {
                parse();
            }
            int idx = getVarIndex(Var);
            if (idx >= 0 && idx < 10)
            {
                m_varSpace[idx] = Value;
            }
            else
            {
                throw new NotSupportedException("Invalid variable " + Var);
            }
        }

        public double calculate(double Val1 = 0.0, double Val2 = 0.0, bool forceExecution = false)
        {
            if (mLinearizeMode > 0 && !forceExecution)
            {
                if (mLinearizeMode == 1)
                {
                    return linearizedValue(Val1);
                }
                return linearizedValue2d(Val1, Val2); // matrix case
            }
            double[] var_space = new double[10];
            var_space[0] = Val1;
            var_space[1] = Val2;
            m_strict = false;
            return execute(var_space); // execute with local variables on stack
        }

        public double calculate(ExpressionWrapper obj, double variable_value1 = 0.0, double variable_value2 = 0.0)
        {
            double[] var_space = new double[10];
            var_space[0] = variable_value1;
            var_space[1] = variable_value2;
            m_strict = false;
            return execute(var_space, obj); // execute with local variables on stack
        }

        private int getFuncIndex(string functionName)
        {
            int pos = mathFuncList.IndexOf(" " + functionName + " "); // check full names
            if (pos < 0)
            {
                throw new NotSupportedException("Function " + functionName + " not defined!");
            }
            int idx = 0;
            for (int i = 1; i <= pos; i++) // start at the first character (skip first space)
            {
                if (mathFuncList[i] == ' ')
                {
                    ++idx;
                }
            }
            return idx;
        }

        public double execute(double[] varlist = null, ExpressionWrapper obj = null)
        {
            if (!m_parsed)
            {
                this.parse(obj);
                if (!m_parsed)
                {
                    return 0.0;
                }
            }
            double[] varSpace = varlist != null ? varlist : m_varSpace;
            int execIndex = 0;
            ExtExecListItem exec = m_execList[execIndex];
            int i;
            double result = 0.0;
            double[] Stack = new double[200];
            bool[] LogicStack = new bool[200];
            int lp = 0;
            int p = 0;  // p=head pointer
            LogicStack[lp++] = true; // zumindest eins am anfang...
            if (isEmpty())
            {
                // leere expr.
                //m_logicResult=false;
                return 0.0;
            }
            while (exec.Type != ETokType.etStop)
            {
                switch (exec.Type)
                {
                    case ETokType.etOperator:
                        p--;
                        switch (exec.Index)
                        {
                            case '+': Stack[p - 1] = Stack[p - 1] + Stack[p]; break;
                            case '-': Stack[p - 1] = Stack[p - 1] - Stack[p]; break;
                            case '*': Stack[p - 1] = Stack[p - 1] * Stack[p]; break;
                            case '/': Stack[p - 1] = Stack[p - 1] / Stack[p]; break;
                            case '^': Stack[p - 1] = Math.Pow(Stack[p - 1], Stack[p]); break;
                            case '_': Stack[p] = -Stack[p]; p++; break;  // unary operator -
                        }
                        break;
                    case ETokType.etVariable:
                        if (exec.Index < 100)
                        {
                            Stack[p++] = varSpace[exec.Index];
                        }
                        else if (exec.Index < 1000)
                        {
                            Stack[p++] = getModelVar(exec.Index, obj);
                        }
                        else
                        {
                            Stack[p++] = getExternVar(exec.Index);
                        }
                        break;
                    case ETokType.etNumber:
                        Stack[p++] = exec.Value;
                        break;
                    case ETokType.etFunction:
                        p--;
                        switch (exec.Index)
                        {
                            case 0: Stack[p] = Math.Sin(Stack[p]); break;
                            case 1: Stack[p] = Math.Cos(Stack[p]); break;
                            case 2: Stack[p] = Math.Tan(Stack[p]); break;
                            case 3: Stack[p] = Math.Exp(Stack[p]); break;
                            case 4: Stack[p] = Math.Log(Stack[p]); break;
                            case 5: Stack[p] = Math.Sqrt(Stack[p]); break;
                            // min, max, if:  variable zahl von argumenten
                            case 6:      // min
                                for (i = 0; i < exec.Value - 1; i++, p--)
                                {
                                    Stack[p - 1] = (Stack[p] < Stack[p - 1]) ? Stack[p] : Stack[p - 1];
                                }
                                break;
                            case 7:  //max
                                for (i = 0; i < exec.Value - 1; i++, p--)
                                {
                                    Stack[p - 1] = (Stack[p] > Stack[p - 1]) ? Stack[p] : Stack[p - 1];
                                }
                                break;
                            case 8: // if
                                if (Stack[p - 2] == 1) // true
                                {
                                    Stack[p - 2] = Stack[p - 1];
                                }
                                else
                                {
                                    Stack[p - 2] = Stack[p]; // false
                                }
                                p -= 2; // throw away both arguments
                                break;
                            case 9: // incrementelle summe
                                m_incSumVar += Stack[p];
                                Stack[p] = m_incSumVar;
                                break;
                            case 10: // Polygon-Funktion
                                Stack[p - (int)(exec.Value - 1)] = udfPolygon(Stack[p - (int)(exec.Value - 1)], Stack, p, (int)exec.Value);
                                p -= (int)(exec.Value - 1);
                                break;
                            case 11: // Modulo-Division: erg=rest von arg1/arg2
                                p--; // p zeigt auf ergebnis...
                                Stack[p] = Stack[p] % Stack[p + 1];
                                break;
                            case 12: // hilfsfunktion fr sigmoidie sachen.....
                                Stack[p - 3] = udfSigmoid(Stack[p - 3], Stack[p - 2], Stack[p - 1], Stack[p]);
                                p -= 3; // drei argumente (4-1) wegwerfen...
                                break;
                            case 13:
                            case 14: // rnd(from, to) bzw. rndg(mean, stddev)
                                p--;
                                // index-13: 1 bei rnd, 0 bei rndg
                                Stack[p] = udfRandom(exec.Index - 13, Stack[p], Stack[p + 1]);
                                break;
                            case 15: // in-list in() operator
                                Stack[p - (int)(exec.Value - 1)] = udfInList(Stack[p - (int)(exec.Value - 1)], Stack, p, (int)exec.Value);
                                p -= (int)(exec.Value - 1);
                                break;
                            case 16: // round()
                                Stack[p] = Stack[p] < 0.0 ? Math.Ceiling(Stack[p] - 0.5) : Math.Floor(Stack[p] + 0.5); 
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        p++;
                        break;
                    case ETokType.etLogical:
                        p--;
                        lp--;
                        switch ((Operation)exec.Index)
                        {
                            case Operation.opAnd: 
                                LogicStack[p - 1] = (LogicStack[p - 1] && LogicStack[p]); 
                                break;
                            case Operation.opOr: 
                                LogicStack[p - 1] = (LogicStack[p - 1] || LogicStack[p]); 
                                break;
                        }
                        if (LogicStack[p - 1])
                        {
                            Stack[p - 1] = 1;
                        }
                        else
                        {
                            Stack[p - 1] = 0;
                        }
                        break;
                    case ETokType.etCompare:
                        {
                            p--;
                            bool LogicResult = false;
                            switch ((Operation)exec.Index)
                            {
                                case Operation.opEqual: LogicResult = (Stack[p - 1] == Stack[p]); break;
                                case Operation.opNotEqual: LogicResult = (Stack[p - 1] != Stack[p]); break;
                                case Operation.opLowerThen: LogicResult = (Stack[p - 1] < Stack[p]); break;
                                case Operation.opGreaterThen: LogicResult = (Stack[p - 1] > Stack[p]); break;
                                case Operation.opGreaterOrEqual: LogicResult = (Stack[p - 1] >= Stack[p]); break;
                                case Operation.opLowerOrEqual: LogicResult = (Stack[p - 1] <= Stack[p]); break;
                            }
                            if (LogicResult)
                            {
                                Stack[p - 1] = 1.0;   // 1 means true...
                            }
                            else
                            {
                                Stack[p - 1] = 0.0;
                            }

                            LogicStack[p++] = LogicResult;
                            break;
                        }
                    case ETokType.etStop:
                    case ETokType.etUnknown:
                    case ETokType.etDelimeter:
                    default:
                        throw new NotSupportedException(String.Format("invalid token during execution: {0}", m_expression));
                } // switch()

                exec = m_execList[execIndex++];
            }
            if (p != 1)
            {
                throw new NotSupportedException(String.Format("execute: stack unbalanced: {0}", m_expression));
            }
            result = Stack[0];
            //m_logicResult=*(lp-1);
            return result;
        }

        public double addVar(string VarName)
        {
            // add var
            int idx = m_varList.IndexOf(VarName);
            if (idx == -1)
            {
                m_varList.Add(VarName);
                idx = m_varList.Count - 1;
            }
            return m_varSpace[getVarIndex(VarName)];
        }

        public double getVarAdress(string VarName)
        {
            if (!m_parsed)
            {
                parse();
            }
            int idx = getVarIndex(VarName);
            if (idx >= 0 && idx < 10)
            {
                return m_varSpace[idx];
            }
            else
            {
                throw new NotSupportedException(String.Format("getVarAdress: Invalid variable <{0}>.", VarName));
            }
        }

        public int getVarIndex(string variableName)
        {
            int idx;
            if (mModelObject != null)
            {
                idx = mModelObject.variableIndex(variableName);
                if (idx > -1)
                {
                    return 100 + idx;
                }
            }

            /*if (Script)
                {
                   int dummy;
                   EDatatype aType;
                   idx=Script.GetName(VarName, aType, dummy);
                   if (idx>-1)
                      return 1000+idx;
                }*/

            // externe variablen
            if (m_externVarNames.Count > 0)
            {
                idx = m_externVarNames.IndexOf(variableName);
                if (idx > -1)
                {
                    return 1000 + idx;
                }
            }
            idx = m_varList.IndexOf(variableName);
            if (idx > -1)
            {
                return idx;
            }
            // if in strict mode, all variables must be already available at this stage.
            if (m_strict)
            {
                m_errorMsg = String.Format("Variable '{0}' in (strict) expression '{1}' not available!", variableName, m_expression);
                if (!m_catchExceptions)
                {
                    throw new NotSupportedException(m_errorMsg);
                }
            }
            return -1;
        }

        public double getModelVar(int varIdx, ExpressionWrapper obj = null)
        {
            // der weg nach draussen....
            ExpressionWrapper model_object = obj != null ? obj : mModelObject;
            int idx = varIdx - 100; // intern als 100+x gespeichert...
            if (model_object != null)
            {
                return model_object.value(idx);
            }
            // hier evtl. verschiedene objekte unterscheiden (Zahlenraum???)
            throw new NotSupportedException("getModelVar: invalid model variable!");
        }

        public void setExternalVarSpace(List<string> ExternSpaceNames, double[] ExternSpace)
        {
            // externe variablen (zB von Scripting-Engine) bekannt machen...
            m_externVarSpace = ExternSpace;
            m_externVarNames = ExternSpaceNames;
        }

        public double getExternVar(int Index)
        {
            //if (Script)
            //   return Script->GetNumVar(Index-1000);
            //else   // berhaupt noch notwendig???
            return m_externVarSpace[Index - 1000];
        }

        public void enableIncSum()
        {
            // Funktion "inkrementelle summe" einschalten.
            // dabei wird der zhler zurckgesetzt und ein flag gesetzt.
            m_incSumEnabled = true;
            m_incSumVar = 0.0;
        }

        // "Userdefined Function" Polygon
        private double udfPolygon(double Value, double[] Stack, int position, int ArgCount)
        {
            // Polygon-Funktion: auf dem Stack liegen (x/y) Paare, aus denen ein "Polygon"
            // aus Linien zusammengesetzt ist. return ist der y-Wert zu x (Value).
            // Achtung: *Stack zeigt auf das letzte Argument! (ist das letzte y).
            // Stack bereinigen tut der Aufrufer.
            if (ArgCount % 2 != 1)
            {
                throw new NotSupportedException("polygon: falsche zahl parameter. polygon(<val>; x0; y0; x1; y1; ....)");
            }
            int PointCnt = (ArgCount - 1) / 2;
            if (PointCnt < 2)
            {
                throw new NotSupportedException("polygon: falsche zahl parameter. polygon(<val>; x0; y0; x1; y1; ....)");
            }
            double x, y, xold, yold;
            y = Stack[position--];   // 1. Argument: ganz rechts.
            x = Stack[position--];
            if (Value > x)   // rechts drauen: annahme gerade.
                return y;
            for (int i = 0; i < PointCnt - 1; i++)
            {
                xold = x;
                yold = y;
                y = Stack[position--];   // x,y-Paar vom Stack....
                x = Stack[position--];
                if (Value > x)
                {
                    // es geht los: Gerade zwischen (x,y) und (xold,yold)
                    // es geht vielleicht eleganter, aber auf die schnelle:
                    return (yold - y) / (xold - x) * (Value - x) + y;
                }
            }
            // falls nichts gefunden: value < als linkester x-wert
            return y;
        }

        private double udfInList(double value, double[] stack, int position, int argCount)
        {
            for (int i = 0; i < argCount - 1; ++i)
            {
                if (value == stack[position--])
                {
                    return 1.0; // true
                }
            }
            return 0.0; // false
        }

        // userdefined func sigmoid....
        private double udfSigmoid(double Value, double sType, double p1, double p2)
        {
            // sType: typ der Funktion:
            // 0: logistische f
            // 1: Hill-funktion
            // 2: 1 - logistisch (geht von 1 bis 0)
            // 3: 1- hill
            double Result;

            double x = Math.Max(Math.Min(Value, 1.0), 0.0);  // limit auf [0..1]
            int typ = (int)sType;
            switch (typ)
            {
                case 0:
                case 2: // logistisch: f(x)=1 / (1 + p1 e^(-p2 * x))
                    Result = 1.0 / (1.0 + p1 * Math.Exp(-p2 * x));
                    break;
                case 1:
                case 3:     // Hill-Funktion: f(x)=(x^p1)/(p2^p1+x^p1)
                    Result = Math.Pow(x, p1) / (Math.Pow(p2, p1) + Math.Pow(x, p1));
                    break;
                default:
                    throw new NotSupportedException("sigmoid-funktion: ungltiger kurventyp. erlaubt: 0..3");
            }
            if (typ == 2 || typ == 3)
            {
                Result = 1.0 - Result;
            }

            return Result;
        }

        private void checkBuffer(int Index)
        {
            // um den Buffer fr Befehle kmmern.
            // wenn der Buffer zu klein wird, neuen Platz reservieren.
            if (Index < m_execListSize)
            {
                return; // nix zu tun.
            }
            int NewSize = m_execListSize * 2; // immer verdoppeln: 5->10->20->40->80->160
                                              // (1) neuen Buffer anlegen....
            ExtExecListItem[] NewBuf = new ExtExecListItem[NewSize];
            // (2) bisherige Werte umkopieren....
            for (int i = 0; i < m_execListSize; i++)
            {
                NewBuf[i] = m_execList[i];
            }
            // (3) alten buffer lschen und pointer umsetzen...
            m_execList = NewBuf;
            m_execListSize = NewSize;
        }

        private double udfRandom(int type, double p1, double p2)
        {
            // random / gleichverteilt - normalverteilt
            if (type == 0)
            {
                return RandomGenerator.nrandom(p1, p2);
            }
            else    // gaussverteilt
            {
                return RandomGenerator.randNorm(p1, p2);
            }
        }

        /** Linarize an expression, i.e. approximate the function by linear interpolation.
            This is an option for performance critical calculations that include time consuming mathematic functions (e.g. exp())
            low_value: linearization start at this value. values below produce an error
            high_value: upper limit
            steps: number of steps the function is split into
          */
        public void linearize(double low_value, double high_value, int steps = 1000)
        {
            if (!mLinearizationAllowed)
            {
                return;
            }

            mLinearized.Clear();
            mLinearLow = low_value;
            mLinearHigh = high_value;
            mLinearStep = (high_value - low_value) / (double)steps;
            // for the high value, add another step (i.e.: include maximum value) and add one step to allow linear interpolation
            for (int i = 0; i <= steps + 1; i++)
            {
                double x = mLinearLow + i * mLinearStep;
                double r = calculate(x);
                mLinearized.Add(r);
            }
            mLinearizeMode = 1;
        }

        /// like 'linearize()' but for 2d-matrices
        public void linearize2d(double low_x, double high_x, double low_y, double high_y, int stepsx = 50, int stepsy = 50)
        {
            if (!mLinearizationAllowed)
            {
                return;
            }
            mLinearized.Clear();
            mLinearLow = low_x;
            mLinearHigh = high_x;
            mLinearLowY = low_y;
            mLinearHighY = high_y;

            mLinearStep = (high_x - low_x) / (double)stepsx;
            mLinearStepY = (high_y - low_y) / (double)stepsy;
            for (int i = 0; i <= stepsx + 1; i++)
            {
                for (int j = 0; j <= stepsy + 1; j++)
                {
                    double x = mLinearLow + i * mLinearStep;
                    double y = mLinearLowY + j * mLinearStepY;
                    double r = calculate(x, y);
                    mLinearized.Add(r);
                }
            }
            mLinearStepCountY = stepsy + 2;
            mLinearizeMode = 2;
        }

        /// calculate the linear approximation of the result value
        private double linearizedValue(double x)
        {
            if (x < mLinearLow || x > mLinearHigh)
            {
                return calculate(x, 0.0, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
            }
            int lower = (int)((x - mLinearLow) / mLinearStep); // the lower point
            if (lower + 1 >= mLinearized.Count())
            {
                Debug.Assert(lower + 1 < mLinearized.Count());
            }
            List<double> data = mLinearized;
            // linear interpolation
            double result = data[lower] + (data[lower + 1] - data[lower]) / mLinearStep * (x - (mLinearLow + lower * mLinearStep));
            return result;
        }

        /// calculate the linear approximation of the result value
        private double linearizedValue2d(double x, double y)
        {
            if (x < mLinearLow || x > mLinearHigh || y < mLinearLowY || y > mLinearHighY)
            {
                return calculate(x, y, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
            }
            int lowerx = (int)((x - mLinearLow) / mLinearStep); // the lower point (x-axis)
            int lowery = (int)((y - mLinearLowY) / mLinearStepY); // the lower point (y-axis)
            int idx = mLinearStepCountY * lowerx + lowery;
            Debug.Assert(idx + mLinearStepCountY + 1 < mLinearized.Count());
            List<double> data = mLinearized;
            // linear interpolation
            // mean slope in x - direction
            double slope_x = ((data[idx + mLinearStepCountY] - data[idx]) / mLinearStepY + (data[idx + mLinearStepCountY + 1] - data[idx + 1]) / mLinearStepY) / 2.0;
            double slope_y = ((data[idx + 1] - data[idx]) / mLinearStep + (data[idx + mLinearStepCountY + 1] - data[idx + mLinearStepCountY]) / mLinearStep) / 2.0;
            double result = data[idx] + (x - (mLinearLow + lowerx * mLinearStep)) * slope_x + (y - (mLinearLowY + lowery * mLinearStepY)) * slope_y;
            return result;
        }
    }
}
