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
        private const string MathFuncList = " sin cos tan exp ln sqrt min max if incsum polygon mod sigmoid rnd rndg in round "; // a space at the end is important!

        private static readonly Dictionary<string, double> Constants;
        private static readonly int[] MaxArgCount = new int[] { 1, 1, 1, 1, 1, 1, -1, -1, 3, 1, -1, 2, 4, 2, 2, -1, 1 };

        public static bool LinearizationEnabled { get; set; }

        private enum Operation
        {
            Equal = 1,
            GreaterThen = 2,
            LessThan = 3,
            NotEqual = 4,
            LessThanOrEqual = 5,
            GreaterThanOrEqual = 6,
            And = 7,
            Or = 8
        }

        private enum Datatype { Info, Number, String, Object, Void, ObjVar, Reference, ObjectReference };
        private enum TokenType { Number, Operator, Variable, Function, Logical, Compare, Stop, Unknown, Delimeter };

        private struct ExtExecListItem
        {
            public TokenType Type;
            public double Value;
            public int Index;
        }

        // inc-sum
        private double m_incSumVar;

        private bool m_parsed;
        private ExtExecListItem[] m_execList;
        private int m_execListSize; // size of buffer
        private int m_execIndex;
        private readonly double[] m_varSpace;
        private List<string> m_externVarNames;
        private double[] m_externVarSpace;
        private TokenType m_state;
        private TokenType m_lastState;
        private int m_pos;
        private string m_expr;
        private string m_token;
        private int m_tokCount;
        private readonly List<string> m_varList;

        // unused in C++
        //private string m_prepStr;
        //private bool m_incSumEnabled;

        private readonly object m_execMutex;
        // linearization
        private int mLinearizeMode;
        private readonly List<double> mLinearized;
        private double mLinearLow, mLinearHigh;
        private double mLinearStep;
        private double mLinearLowY, mLinearHighY;
        private double mLinearStepY;
        private int mLinearStepCountY;

        // mutex used to serialize expression parsing.
        private readonly object mutex;

        public bool CatchExceptions { get; set; }
        public string ExpressionString { get; set; }
        public ExpressionWrapper Wrapper { get; set; }

        public bool IsConstant { get; private set; } ///< returns true if current expression is a constant.
        public bool IsEmpty { get; private set; } ///< returns true if expression is empty
        /** strict property: if true, variables must be named before execution.
          When strict=true, all variables in the expression must be added by setVar or addVar.
          if false, variable values are assigned depending on occurence. strict is false by default for calls to "calculate()".
        */
        public bool IsStrict { get; set; }
        public string LastError { get; private set; }

        static Expression()
        {
            Expression.Constants = new Dictionary<string, double>();
            Expression.LinearizationEnabled = false;
        }

        public Expression()
        {
            this.m_execList = default;
            this.m_execMutex = new object();
            this.m_expr = null;
            this.m_externVarSpace = null;
            this.mLinearized = new List<double>();
            this.m_varList = new List<string>();
            this.m_varSpace = new double[10];
            this.Wrapper = null;
            this.mutex = new object();
        }

        public Expression(string expression)
            : this()
        {
            SetExpression(expression);
        }

        public Expression(string expression, ExpressionWrapper wrapper)
            : this(expression)
        {
            Wrapper = wrapper;
        }

        public double ExecuteLocked() ///< thread safe version
        {
            lock (m_execMutex)
            {
                return Execute();
            }
        }

        public static void AddConstant(string const_name, double const_value)
        {
            Constants[const_name] = const_value;
        }

        private TokenType NextToken()
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
                m_state = TokenType.Stop;
                m_token = "";
                return TokenType.Stop; // Ende der Vorstellung
            }

            // whitespaces eliminieren...
            while (" \t\n\r".Contains(m_expr[m_pos]))
            {
                m_pos++;
            }
            if (m_expr[m_pos] == ',')
            {
                m_token = new string(m_expr[m_pos++], 1);
                m_state = TokenType.Delimeter;
                return TokenType.Delimeter;
            }
            if ("+-*/(){}^".Contains(m_expr[m_pos]))
            {
                m_token = new string(m_expr[m_pos++], 1);
                m_state = TokenType.Operator;
                return TokenType.Operator;
            }
            if ("=<>".Contains(m_expr[m_pos]))
            {
                m_token = new string(m_expr[m_pos++], 1);
                if (m_expr[m_pos] == '>' || m_expr[m_pos] == '=')
                {
                    m_token += m_expr[m_pos++];
                }
                m_state = TokenType.Compare;
                return TokenType.Compare;
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
                m_state = TokenType.Number;
                return TokenType.Number;
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
                    m_state = TokenType.Function;
                    return TokenType.Function;
                }
                else
                {
                    if (m_token.ToLowerInvariant() == "and" || m_token.ToLowerInvariant() == "or")
                    {
                        m_state = TokenType.Logical;
                        return TokenType.Logical;
                    }
                    else
                    {
                        m_state = TokenType.Variable;
                        if (m_token == "true")
                        {
                            m_state = TokenType.Number;
                            m_token = "1";
                            return TokenType.Number;
                        }
                        if (m_token == "false")
                        {
                            m_state = TokenType.Number;
                            m_token = "0";
                            return TokenType.Number;
                        }
                        return TokenType.Variable;
                    }
                }
            }
            m_state = TokenType.Unknown;
            return TokenType.Unknown; // in case no match was found
        }

        /** sets expression @p expr and checks the syntax (parse).
            Expressions are setup with strict = false, i.e. no fixed binding of variable names.
          */
        public void SetAndParse(string expr)
        {
            SetExpression(expr);
            IsStrict = false;
            Parse();
        }

        /// set the current expression.
        /// do some preprocessing (e.g. handle the different use of ",", ".", ";")
        public void SetExpression(string aExpression)
        {
            ExpressionString = String.Join(' ', aExpression.Trim().Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            m_expr = ExpressionString; // TODO: remove m_expr
            m_pos = 0;  // set starting point...

            for (int i = 0; i < m_varSpace.Length; i++)
            {
                m_varSpace[i] = 0.0;
            }
            m_parsed = false;
            CatchExceptions = false;
            LastError = "";

            Wrapper = null;
            m_externVarSpace = null;

            IsStrict = true; // default....
            // m_incSumEnabled = false;
            IsEmpty = String.Equals(aExpression.Trim(), String.Empty, StringComparison.OrdinalIgnoreCase);
            // Buffer:
            m_execListSize = 5; // inital value...
            if (m_execList == null)
            {
                m_execList = new ExtExecListItem[m_execListSize]; // init
            }

            mLinearizeMode = 0; // linearization is switched off
        }

        public void Parse(ExpressionWrapper wrapper = null)
        {
            lock (mutex)
            {
                if (m_parsed)
                {
                    return;
                }

                if (wrapper != null)
                {
                    Wrapper = wrapper;
                }
                m_state = TokenType.Unknown;
                m_lastState = TokenType.Unknown;
                IsConstant = true;
                m_execIndex = 0;
                m_tokCount = 0;
                int aktTok;
                NextToken();
                while (m_state != TokenType.Stop)
                {
                    aktTok = m_tokCount;
                    ParseLevelL0();  // start with logical level 0
                    if (aktTok == m_tokCount)
                    {
                        throw new NotSupportedException("parse(): Unbalanced Braces.");
                    }
                    if (m_state == TokenType.Unknown)
                    {
                        throw new NotSupportedException("parse(): Syntax error, token: " + m_token);
                    }
                }
                IsEmpty = (m_execIndex == 0);
                m_execList[m_execIndex].Type = TokenType.Stop;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = 0;
                CheckBuffer(m_execIndex);
                m_parsed = true;
            }
        }

        private void ParseLevelL0()
        {
            // logical operations  (and, or, not)
            string op;
            ParseLevelL1();

            while (m_state == TokenType.Logical)
            {
                op = m_token.ToLowerInvariant();
                NextToken();
                ParseLevelL1();
                Operation logicaltok = 0;
                if (op == "and")
                {
                    logicaltok = Operation.And;
                }
                if (op == "or")
                {
                    logicaltok = Operation.Or;
                }

                m_execList[m_execIndex].Type = TokenType.Logical;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)logicaltok;
                CheckBuffer(m_execIndex);
            }
        }

        private void ParseLevelL1()
        {
            // logische operationen (<,>,=,...)
            string op;
            ParseLevel0();
            //double temp=FResult;
            if (m_state == TokenType.Compare)
            {
                op = m_token;
                NextToken();
                ParseLevel0();
                Operation logicaltok = 0;
                if (op == "<") logicaltok = Operation.LessThan;
                if (op == ">") logicaltok = Operation.GreaterThen;
                if (op == "<>") logicaltok = Operation.NotEqual;
                if (op == "<=") logicaltok = Operation.LessThanOrEqual;
                if (op == ">=") logicaltok = Operation.GreaterThanOrEqual;
                if (op == "=") logicaltok = Operation.Equal;

                m_execList[m_execIndex].Type = TokenType.Compare;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)logicaltok;
                CheckBuffer(m_execIndex);
            }
        }

        private void ParseLevel0()
        {
            // plus und minus
            ParseLevel1();

            while (m_token == "+" || m_token == "-")
            {
                NextToken();
                ParseLevel1();
                m_execList[m_execIndex].Type = TokenType.Operator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)m_token[0];///op.constData()[0];
                CheckBuffer(m_execIndex);
            }
        }

        private void ParseLevel1()
        {
            // mal und division
            ParseLevel2();
            //double temp=FResult;
            // alt:        if (m_token=="*" || m_token=="/") {
            while (m_token == "*" || m_token == "/")
            {
                NextToken();
                ParseLevel2();
                m_execList[m_execIndex].Type = TokenType.Operator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = (int)m_token[0];
                CheckBuffer(m_execIndex);
            }
        }

        private void Atom()
        {
            if (m_state == TokenType.Variable || m_state == TokenType.Number)
            {
                if (m_state == TokenType.Number)
                {
                    double result = double.Parse(m_token);
                    m_execList[m_execIndex].Type = TokenType.Number;
                    m_execList[m_execIndex].Value = result;
                    m_execList[m_execIndex++].Index = -1;
                    CheckBuffer(m_execIndex);
                }
                if (m_state == TokenType.Variable)
                {
                    if (Constants.ContainsKey(m_token))
                    {
                        // constant
                        double result = Constants[m_token];
                        m_execList[m_execIndex].Type = TokenType.Number;
                        m_execList[m_execIndex].Value = result;
                        m_execList[m_execIndex++].Index = -1;
                        CheckBuffer(m_execIndex);

                    }
                    else
                    {
                        // 'real' variable
                        if (!IsStrict) // in strict mode, the variable must be available by external bindings. in "lax" mode, the variable is added when encountered first.
                        {
                            AddVariable(m_token);
                        }
                        m_execList[m_execIndex].Type = TokenType.Variable;
                        m_execList[m_execIndex].Value = 0;
                        m_execList[m_execIndex++].Index = GetVariableIndex(m_token);
                        CheckBuffer(m_execIndex);
                        IsConstant = false;
                    }
                }
                NextToken();
            }
            else if (m_state == TokenType.Stop || m_state == TokenType.Unknown)
            {
                throw new NotSupportedException("Unexpected end of m_expression.");
            }
        }

        private void ParseLevel2()
        {
            // x^y
            ParseLevel3();
            //double temp=FResult;
            while (m_token == "^")
            {
                NextToken();
                ParseLevel3();
                //FResult=pow(temp,FResult);
                m_execList[m_execIndex].Type = TokenType.Operator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = '^';
                CheckBuffer(m_execIndex);
            }
        }

        private void ParseLevel3()
        {
            // unary operator (- bzw. +)
            string op;
            op = m_token;
            bool Unary = false;
            if (op == "-" && (m_lastState == TokenType.Operator || m_lastState == TokenType.Unknown || m_lastState == TokenType.Compare || m_lastState == TokenType.Logical || m_lastState == TokenType.Function))
            {
                NextToken();
                Unary = true;
            }
            ParseLevel4();
            if (Unary && op == "-")
            {
                //FResult=-FResult;
                m_execList[m_execIndex].Type = TokenType.Operator;
                m_execList[m_execIndex].Value = 0;
                m_execList[m_execIndex++].Index = '_';
                CheckBuffer(m_execIndex);
            }
        }

        private void ParseLevel4()
        {
            // Klammer und Funktionen
            string func;
            Atom();
            //double temp=FResult;
            if (m_token == "(" || m_state == TokenType.Function)
            {
                func = m_token;
                if (func == "(")   // klammerausdruck
                {
                    NextToken();
                    ParseLevelL0();
                }
                else        // funktion...
                {
                    int argcount = 0;
                    int idx = GetFunctionIndex(func);
                    NextToken();
                    //m_token="{";
                    // bei funktionen mit mehreren Parametern
                    while (m_token != ")")
                    {
                        argcount++;
                        ParseLevelL0();
                        if (m_state == TokenType.Delimeter)
                        {
                            NextToken();
                        }
                    }
                    if (MaxArgCount[idx] > 0 && MaxArgCount[idx] != argcount)
                    {
                        throw new NotSupportedException(String.Format("Function {0} assumes {1} arguments!", func, MaxArgCount[idx]));
                    }
                    //throw std::logic_error("Funktion " + func + " erwartet " + std::string(MaxArgCount[idx]) + " Parameter!");
                    m_execList[m_execIndex].Type = TokenType.Function;
                    m_execList[m_execIndex].Value = argcount;
                    m_execList[m_execIndex++].Index = idx;
                    CheckBuffer(m_execIndex);
                }
                if (m_token != "}" && m_token != ")") // Fehler
                {
                    throw new NotSupportedException(String.Format("unbalanced number of parentheses in [%1].", ExpressionString));
                }
                NextToken();
            }
        }

        public void SetVariable(string name, double value)
        {
            if (!m_parsed)
            {
                Parse();
            }
            int idx = GetVariableIndex(name);
            if (idx >= 0 && idx < 10)
            {
                m_varSpace[idx] = value;
            }
            else
            {
                throw new NotSupportedException("Invalid variable " + name);
            }
        }

        public double Calculate(double Val1 = 0.0, double Val2 = 0.0, bool forceExecution = false)
        {
            if (mLinearizeMode > 0 && !forceExecution)
            {
                if (mLinearizeMode == 1)
                {
                    return GetLinearizedValue(Val1);
                }
                return GetLinearizedValue(Val1, Val2); // matrix case
            }
            double[] var_space = new double[10];
            var_space[0] = Val1;
            var_space[1] = Val2;
            IsStrict = false;
            return Execute(var_space); // execute with local variables on stack
        }

        public double Calculate(ExpressionWrapper obj, double variable_value1 = 0.0, double variable_value2 = 0.0)
        {
            double[] var_space = new double[10];
            var_space[0] = variable_value1;
            var_space[1] = variable_value2;
            IsStrict = false;
            return Execute(var_space, obj); // execute with local variables on stack
        }

        private int GetFunctionIndex(string functionName)
        {
            int pos = MathFuncList.IndexOf(" " + functionName + " "); // check full names
            if (pos < 0)
            {
                throw new NotSupportedException("Function " + functionName + " not defined!");
            }
            int idx = 0;
            for (int i = 1; i <= pos; i++) // start at the first character (skip first space)
            {
                if (MathFuncList[i] == ' ')
                {
                    ++idx;
                }
            }
            return idx;
        }

        public double Execute(double[] varlist = null, ExpressionWrapper obj = null)
        {
            if (!m_parsed)
            {
                this.Parse(obj);
                if (!m_parsed)
                {
                    return 0.0;
                }
            }
            double[] varSpace = varlist ?? m_varSpace;
            int execIndex = 0;
            ExtExecListItem exec = m_execList[execIndex];
            int i;
            double[] Stack = new double[200];
            bool[] LogicStack = new bool[200];
            int lp = 0;
            int p = 0;  // p=head pointer
            LogicStack[lp++] = true; // zumindest eins am anfang...
            if (IsEmpty)
            {
                // leere expr.
                //m_logicResult=false;
                return 0.0;
            }
            while (exec.Type != TokenType.Stop)
            {
                switch (exec.Type)
                {
                    case TokenType.Operator:
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
                    case TokenType.Variable:
                        if (exec.Index < 100)
                        {
                            Stack[p++] = varSpace[exec.Index];
                        }
                        else if (exec.Index < 1000)
                        {
                            Stack[p++] = GetModelVariable(exec.Index, obj);
                        }
                        else
                        {
                            Stack[p++] = GetExternVariable(exec.Index);
                        }
                        break;
                    case TokenType.Number:
                        Stack[p++] = exec.Value;
                        break;
                    case TokenType.Function:
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
                                Stack[p - (int)(exec.Value - 1)] = UserDefinedPolygon(Stack[p - (int)(exec.Value - 1)], Stack, p, (int)exec.Value);
                                p -= (int)(exec.Value - 1);
                                break;
                            case 11: // Modulo-Division: erg=rest von arg1/arg2
                                p--; // p zeigt auf ergebnis...
                                Stack[p] = Stack[p] % Stack[p + 1];
                                break;
                            case 12: // hilfsfunktion fr sigmoidie sachen.....
                                Stack[p - 3] = UserDefinedSigmoid(Stack[p - 3], Stack[p - 2], Stack[p - 1], Stack[p]);
                                p -= 3; // drei argumente (4-1) wegwerfen...
                                break;
                            case 13:
                            case 14: // rnd(from, to) bzw. rndg(mean, stddev)
                                p--;
                                // index-13: 1 bei rnd, 0 bei rndg
                                Stack[p] = UserDefinedRandom(exec.Index - 13, Stack[p], Stack[p + 1]);
                                break;
                            case 15: // in-list in() operator
                                Stack[p - (int)(exec.Value - 1)] = UserDefinedFunctionInList(Stack[p - (int)(exec.Value - 1)], Stack, p, (int)exec.Value);
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
                    case TokenType.Logical:
                        p--;
                        lp--;
                        switch ((Operation)exec.Index)
                        {
                            case Operation.And:
                                LogicStack[p - 1] = (LogicStack[p - 1] && LogicStack[p]);
                                break;
                            case Operation.Or:
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
                    case TokenType.Compare:
                        {
                            p--;
                            bool LogicResult = false;
                            switch ((Operation)exec.Index)
                            {
                                case Operation.Equal: LogicResult = (Stack[p - 1] == Stack[p]); break;
                                case Operation.NotEqual: LogicResult = (Stack[p - 1] != Stack[p]); break;
                                case Operation.LessThan: LogicResult = (Stack[p - 1] < Stack[p]); break;
                                case Operation.GreaterThen: LogicResult = (Stack[p - 1] > Stack[p]); break;
                                case Operation.GreaterThanOrEqual: LogicResult = (Stack[p - 1] >= Stack[p]); break;
                                case Operation.LessThanOrEqual: LogicResult = (Stack[p - 1] <= Stack[p]); break;
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
                    case TokenType.Stop:
                    case TokenType.Unknown:
                    case TokenType.Delimeter:
                    default:
                        throw new NotSupportedException(String.Format("invalid token during execution: {0}", ExpressionString));
                } // switch()

                exec = m_execList[execIndex++];
            }
            if (p != 1)
            {
                throw new NotSupportedException(String.Format("execute: stack unbalanced: {0}", ExpressionString));
            }
            //m_logicResult=*(lp-1);
            return Stack[0];
        }

        public double AddVariable(string varName)
        {
            // add var
            int idx = m_varList.IndexOf(varName);
            if (idx == -1)
            {
                m_varList.Add(varName);
            }
            return m_varSpace[GetVariableIndex(varName)];
        }

        // unused in C++
        //public double getVarAdress(string varName)
        //{
        //    if (!m_parsed)
        //    {
        //        parse();
        //    }
        //    int idx = getVarIndex(varName);
        //    if (idx >= 0 && idx < 10)
        //    {
        //        return m_varSpace[idx];
        //    }
        //    else
        //    {
        //        throw new NotSupportedException(String.Format("getVarAdress: Invalid variable <{0}>.", varName));
        //    }
        //}

        public int GetVariableIndex(string variableName)
        {
            int idx;
            if (Wrapper != null)
            {
                idx = Wrapper.GetVariableIndex(variableName);
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
            if (IsStrict)
            {
                LastError = String.Format("Variable '{0}' in (strict) expression '{1}' not available!", variableName, ExpressionString);
                if (!CatchExceptions)
                {
                    throw new NotSupportedException(LastError);
                }
            }
            return -1;
        }

        public double GetModelVariable(int varIdx, ExpressionWrapper obj = null)
        {
            // der weg nach draussen....
            ExpressionWrapper model_object = obj ?? Wrapper;
            int idx = varIdx - 100; // intern als 100+x gespeichert...
            if (model_object != null)
            {
                return model_object.Value(idx);
            }
            // hier evtl. verschiedene objekte unterscheiden (Zahlenraum???)
            throw new NotSupportedException("getModelVar: invalid model variable!");
        }

        public void SetExternalVariableSpace(List<string> externalNames, double[] externalSpace)
        {
            // externe variablen (zB von Scripting-Engine) bekannt machen...
            m_externVarSpace = externalSpace;
            m_externVarNames = externalNames;
        }

        public double GetExternVariable(int Index)
        {
            //if (Script)
            //   return Script->GetNumVar(Index-1000);
            //else   // berhaupt noch notwendig???
            return m_externVarSpace[Index - 1000];
        }

        public void EnableIncrementalSum()
        {
            // Funktion "inkrementelle summe" einschalten.
            // dabei wird der zhler zurckgesetzt und ein flag gesetzt.
            // m_incSumEnabled = true;
            m_incSumVar = 0.0;
        }

        // "Userdefined Function" Polygon
        private double UserDefinedPolygon(double value, double[] stack, int position, int ArgCount)
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
            y = stack[position--];   // 1. Argument: ganz rechts.
            x = stack[position--];
            if (value > x)   // rechts drauen: annahme gerade.
                return y;
            for (int i = 0; i < PointCnt - 1; i++)
            {
                xold = x;
                yold = y;
                y = stack[position--];   // x,y-Paar vom Stack....
                x = stack[position--];
                if (value > x)
                {
                    // es geht los: Gerade zwischen (x,y) und (xold,yold)
                    // es geht vielleicht eleganter, aber auf die schnelle:
                    return (yold - y) / (xold - x) * (value - x) + y;
                }
            }
            // falls nichts gefunden: value < als linkester x-wert
            return y;
        }

        private double UserDefinedFunctionInList(double value, double[] stack, int position, int argCount)
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
        private double UserDefinedSigmoid(double Value, double sType, double p1, double p2)
        {
            // sType: typ der Funktion:
            // 0: logistische f
            // 1: Hill-funktion
            // 2: 1 - logistisch (geht von 1 bis 0)
            // 3: 1- hill
            double result;

            double x = Math.Max(Math.Min(Value, 1.0), 0.0);  // limit auf [0..1]
            int typ = (int)sType;
            switch (typ)
            {
                case 0:
                case 2: // logistisch: f(x)=1 / (1 + p1 e^(-p2 * x))
                    result = 1.0 / (1.0 + p1 * Math.Exp(-p2 * x));
                    break;
                case 1:
                case 3:     // Hill-Funktion: f(x)=(x^p1)/(p2^p1+x^p1)
                    result = Math.Pow(x, p1) / (Math.Pow(p2, p1) + Math.Pow(x, p1));
                    break;
                default:
                    throw new NotSupportedException("sigmoid-funktion: ungltiger kurventyp. erlaubt: 0..3");
            }
            if (typ == 2 || typ == 3)
            {
                result = 1.0 - result;
            }

            return result;
        }

        private void CheckBuffer(int Index)
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

        private double UserDefinedRandom(int type, double p1, double p2)
        {
            // random / gleichverteilt - normalverteilt
            if (type == 0)
            {
                return RandomGenerator.Random(p1, p2);
            }
            else    // gaussverteilt
            {
                return RandomGenerator.RandNorm(p1, p2);
            }
        }

        /** Linarize an expression, i.e. approximate the function by linear interpolation.
            This is an option for performance critical calculations that include time consuming mathematic functions (e.g. exp())
            low_value: linearization start at this value. values below produce an error
            high_value: upper limit
            steps: number of steps the function is split into
          */
        public void Linearize(double low_value, double high_value, int steps = 1000)
        {
            if (!LinearizationEnabled)
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
                double r = Calculate(x);
                mLinearized.Add(r);
            }
            mLinearizeMode = 1;
        }

        /// like 'linearize()' but for 2d-matrices
        public void Linearize(double low_x, double high_x, double low_y, double high_y, int stepsx = 50, int stepsy = 50)
        {
            if (!LinearizationEnabled)
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
                    double r = Calculate(x, y);
                    mLinearized.Add(r);
                }
            }
            mLinearStepCountY = stepsy + 2;
            mLinearizeMode = 2;
        }

        /// calculate the linear approximation of the result value
        private double GetLinearizedValue(double x)
        {
            if (x < mLinearLow || x > mLinearHigh)
            {
                return Calculate(x, 0.0, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
        private double GetLinearizedValue(double x, double y)
        {
            if (x < mLinearLow || x > mLinearHigh || y < mLinearLowY || y > mLinearHighY)
            {
                return Calculate(x, y, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
