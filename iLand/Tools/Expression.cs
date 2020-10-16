using iLand.Simulation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace iLand.Tools
{
    /** @class Expression
      An expression engine for mathematical expressions provided as strings.
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
    public class Expression
    {
        private static readonly ReadOnlyCollection<string> MathFunctions = new List<string>()
        {
            "sin", "cos", "tan", "exp", "ln", "sqrt", "min", "max", "if", "incsum", "polygon", "mod", "sigmoid", "rnd", "rndg", "in", "round"
        }.AsReadOnly();

        private static readonly int[] MaxArgCount = new int[] { 1, 1, 1, 1, 1, 1, -1, -1, 3, 1, -1, 2, 4, 2, 2, -1, 1 };

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

        private enum TokenType { Number, Operator, Variable, Function, Logical, Compare, Stop, Unknown, Delimiter }

        private struct Token
        {
            public TokenType Type { get; set; }
            public double Value { get; set; }
            public int Index { get; set; }

            public override string ToString()
            {
                return this.Type switch
                {
                    TokenType.Function => Expression.MathFunctions[this.Index],
                    TokenType.Number => this.Value.ToString(),
                    TokenType.Operator => new string((char)this.Index, 1),
                    TokenType.Stop => "<stop>",
                    TokenType.Unknown => "<unknown>",
                    // Compare, Delimiter, Logical, Variable
                    _ => this.Type.ToString().ToLowerInvariant() + "(" + this.Index + ")"
                };
            }
        }

        // inc-sum
        private double m_incSumVar;

        private bool m_parsed;
        private Token[] mTokens;
        private int m_execListSize; // size of buffer
        private int mExecuteIndex;
        private readonly double[] mVariableValues;
        private List<string> mExternalVariableNames;
        private double[] mExternalVariableValues;
        private TokenType mState;
        private TokenType mLastState;
        private int mParsePosition;
        private string m_token;
        private int mTokenCount;
        private readonly List<string> mVariableNames;

        // linearization
        private int mLinearizedDimensionCount;
        private readonly List<double> mLinearized;
        private double mLinearLow, mLinearHigh;
        private double mLinearStep;
        private double mLinearLowY, mLinearHighY;
        private double mLinearStepY;
        private int mLinearStepCountY;

        public bool CatchExceptions { get; set; }
        public string ExpressionString { get; set; }
        public ExpressionWrapper Wrapper { get; set; }

        public bool IsConstant { get; private set; } // returns true if current expression is a constant.
        public bool IsEmpty { get; private set; } // returns true if expression is empty
        /** strict property: if true, variables must be named before execution.
          When strict=true, all variables in the expression must be added by setVar or addVar.
          if false, variable values are assigned depending on occurence. strict is false by default for calls to "calculate()".
        */
        public bool IsStrict { get; set; }
        public string LastError { get; private set; }

        public Expression()
        {
            this.mTokens = null;
            this.mExternalVariableValues = null;
            this.mLinearized = new List<double>();
            this.mVariableNames = new List<string>();
            this.mVariableValues = new double[10];

            this.ExpressionString = null;
            this.Wrapper = null;
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

        //public double ExecuteLocked(Model model) // thread safe version
        //{
        //    lock (this.mTokens)
        //    {
        //        return Execute(model);
        //    }
        //}

        //public static void AddSpecies(string name, int index)
        //{
        //    Expression.SpeciesIndexByName.Add(name, index);
        //}

        private TokenType NextToken()
        {
            mTokenCount++;
            mLastState = mState;
            // nchsten m_token aus String lesen...
            // whitespaces eliminieren...
            while ((mParsePosition < ExpressionString.Length) && " \t\n\r".Contains(ExpressionString[mParsePosition]))
            {
                mParsePosition++;
            }

            if (mParsePosition >= ExpressionString.Length)
            {
                mState = TokenType.Stop;
                m_token = "";
                return TokenType.Stop; // Ende der Vorstellung
            }

            // whitespaces eliminieren...
            while (" \t\n\r".Contains(ExpressionString[mParsePosition]))
            {
                mParsePosition++;
            }
            if (ExpressionString[mParsePosition] == ',')
            {
                m_token = new string(ExpressionString[mParsePosition++], 1);
                mState = TokenType.Delimiter;
                return TokenType.Delimiter;
            }
            if ("+-*/(){}^".Contains(ExpressionString[mParsePosition]))
            {
                m_token = new string(ExpressionString[mParsePosition++], 1);
                mState = TokenType.Operator;
                return TokenType.Operator;
            }
            if ("=<>".Contains(ExpressionString[mParsePosition]))
            {
                m_token = new string(ExpressionString[mParsePosition++], 1);
                if (ExpressionString[mParsePosition] == '>' || ExpressionString[mParsePosition] == '=')
                {
                    m_token += ExpressionString[mParsePosition++];
                }
                mState = TokenType.Compare;
                return TokenType.Compare;
            }
            if (ExpressionString[mParsePosition] >= '0' && ExpressionString[mParsePosition] <= '9')
            {
                // Zahl
                int startPosition = mParsePosition;
                while ((mParsePosition < ExpressionString.Length) && "0123456789.".Contains(ExpressionString[mParsePosition]))
                {
                    mParsePosition++;  // nchstes Zeichen suchen...
                }
                m_token = ExpressionString[startPosition..mParsePosition];
                mState = TokenType.Number;
                return TokenType.Number;
            }

            if ((ExpressionString[mParsePosition] >= 'a' && ExpressionString[mParsePosition] <= 'z') || (ExpressionString[mParsePosition] >= 'A' && ExpressionString[mParsePosition] <= 'Z'))
            {
                // function ... find brace
                m_token = "";
                // TODO: simplify to Char.IsLetterOrDigit()
                while (mParsePosition < this.ExpressionString.Length && 
                       ((this.ExpressionString[mParsePosition] >= 'a' && this.ExpressionString[mParsePosition] <= 'z') || 
                        (this.ExpressionString[mParsePosition] >= 'A' && this.ExpressionString[mParsePosition] <= 'Z') || 
                        (this.ExpressionString[mParsePosition] >= '0' && this.ExpressionString[mParsePosition] <= '9') || 
                        this.ExpressionString[mParsePosition] == '_' || this.ExpressionString[mParsePosition] == '.') &&
                       this.ExpressionString[mParsePosition] != '(')
                {
                    m_token += ExpressionString[mParsePosition++];
                }
                // wenn am Ende Klammer, dann Funktion, sonst Variable.
                if (mParsePosition < this.ExpressionString.Length && 
                    (this.ExpressionString[mParsePosition] == '(' || this.ExpressionString[mParsePosition] == '{'))
                {
                    mParsePosition++; // skip brace
                    mState = TokenType.Function;
                    return TokenType.Function;
                }
                else
                {
                    if (m_token.ToLowerInvariant() == "and" || m_token.ToLowerInvariant() == "or")
                    {
                        mState = TokenType.Logical;
                        return TokenType.Logical;
                    }
                    else
                    {
                        mState = TokenType.Variable;
                        if (m_token == "true")
                        {
                            mState = TokenType.Number;
                            m_token = "1";
                            return TokenType.Number;
                        }
                        if (m_token == "false")
                        {
                            mState = TokenType.Number;
                            m_token = "0";
                            return TokenType.Number;
                        }
                        return TokenType.Variable;
                    }
                }
            }
            mState = TokenType.Unknown;
            return TokenType.Unknown; // in case no match was found
        }

        /** sets expression @p expr and checks the syntax (parse).
            Expressions are setup with strict = false, i.e. no fixed binding of variable names.
          */
        public void SetAndParse(string expr)
        {
            SetExpression(expr);
            this.IsStrict = false;
            Parse();
        }

        /// set the current expression.
        /// do some preprocessing (e.g. handle the different use of ",", ".", ";")
        public void SetExpression(string expression)
        {
            this.ExpressionString = expression == null ? String.Empty : String.Join(' ', expression.Trim().Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            mParsePosition = 0;  // set starting point...

            for (int i = 0; i < mVariableValues.Length; i++)
            {
                mVariableValues[i] = 0.0;
            }
            m_parsed = false;
            CatchExceptions = false;
            LastError = "";

            Wrapper = null;
            mExternalVariableValues = null;

            IsStrict = true; // default....
            // m_incSumEnabled = false;
            IsEmpty = String.IsNullOrWhiteSpace(expression);
            // Buffer:
            m_execListSize = 5; // inital value...
            if (mTokens == null)
            {
                mTokens = new Token[m_execListSize]; // init
            }

            mLinearizedDimensionCount = 0; // linearization is switched off
        }

        public override string ToString()
        {
            StringBuilder expression = new StringBuilder();
            foreach (Token token in this.mTokens)
            {
                expression.Append(token.Type switch
                {
                    TokenType.Compare => this.ExpressionString[token.Index],
                    TokenType.Delimiter => this.ExpressionString[token.Index],
                    TokenType.Function => Expression.MathFunctions[token.Index],
                    TokenType.Logical => this.ExpressionString[token.Index],
                    TokenType.Number => token.Value.ToString(),
                    TokenType.Operator => (char)token.Index,
                    TokenType.Stop => "<stop>",
                    TokenType.Unknown => "<unknown>",
                    TokenType.Variable => this.mVariableNames[token.Index].ToString(),
                    _ => throw new NotSupportedException("Unhandled token type " + token.Type + ".")
                } + " ");
                if (token.Type == TokenType.Stop)
                {
                    break;
                }
            }
            return expression.ToString();
        }

        public void Parse(ExpressionWrapper wrapper = null)
        {
            lock (this.mTokens)
            {
                if (m_parsed)
                {
                    return;
                }

                if (wrapper != null)
                {
                    Wrapper = wrapper;
                }
                this.mState = TokenType.Unknown;
                this.mLastState = TokenType.Unknown;
                this.IsConstant = true;
                this.mExecuteIndex = 0;
                this.mTokenCount = 0;
                NextToken();
                while (mState != TokenType.Stop)
                {
                    int preParseTokenCount = mTokenCount;
                    ParseLevelL0();  // start with logical level 0
                    if (preParseTokenCount == mTokenCount)
                    {
                        throw new NotSupportedException("parse(): Unbalanced Braces.");
                    }
                    if (mState == TokenType.Unknown)
                    {
                        throw new NotSupportedException("parse(): Syntax error, token: " + m_token);
                    }
                }
                this.IsEmpty = (mExecuteIndex == 0);
                this.mTokens[mExecuteIndex].Type = TokenType.Stop;
                this.mTokens[mExecuteIndex].Value = 0;
                this.mTokens[mExecuteIndex++].Index = 0;
                CheckBuffer(mExecuteIndex);
                this.m_parsed = true;
            }
        }

        private void ParseLevelL0()
        {
            // logical operations  (and, or, not)
            string op;
            ParseLevelL1();

            while (mState == TokenType.Logical)
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

                mTokens[mExecuteIndex].Type = TokenType.Logical;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = (int)logicaltok;
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevelL1()
        {
            // logische operationen (<,>,=,...)
            string op;
            ParseLevel0();
            //double temp=FResult;
            if (mState == TokenType.Compare)
            {
                op = m_token;
                NextToken();
                ParseLevel0();
                Operation logicaltok = 0;
                if (op == "<") 
                    logicaltok = Operation.LessThan;
                if (op == ">") 
                    logicaltok = Operation.GreaterThen;
                if (op == "<>") 
                    logicaltok = Operation.NotEqual;
                if (op == "<=") 
                    logicaltok = Operation.LessThanOrEqual;
                if (op == ">=") 
                    logicaltok = Operation.GreaterThanOrEqual;
                if (op == "=") 
                    logicaltok = Operation.Equal;

                mTokens[mExecuteIndex].Type = TokenType.Compare;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = (int)logicaltok;
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel0()
        {
            // plus und minus
            ParseLevel1();

            while (m_token == "+" || m_token == "-")
            {
                string plusOrMinus = m_token;
                NextToken();
                ParseLevel1();
                mTokens[mExecuteIndex].Type = TokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = plusOrMinus[0];
                CheckBuffer(mExecuteIndex);
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
                string multiplyOrDivide = m_token;
                NextToken();
                ParseLevel2();
                mTokens[mExecuteIndex].Type = TokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = multiplyOrDivide[0];
                CheckBuffer(mExecuteIndex);
            }
        }

        private void Atom()
        {
            if (mState == TokenType.Variable || mState == TokenType.Number)
            {
                if (mState == TokenType.Number)
                {
                    double result = double.Parse(m_token);
                    mTokens[mExecuteIndex].Type = TokenType.Number;
                    mTokens[mExecuteIndex].Value = result;
                    mTokens[mExecuteIndex++].Index = -1;
                    CheckBuffer(mExecuteIndex);
                }
                if (mState == TokenType.Variable)
                {
                    //if (SpeciesIndexByName.ContainsKey(m_token))
                    //{
                    //    // constant
                    //    double result = SpeciesIndexByName[m_token];
                    //    mTokens[mExecuteIndex].Type = TokenType.Number;
                    //    mTokens[mExecuteIndex].Value = result;
                    //    mTokens[mExecuteIndex++].Index = -1;
                    //    CheckBuffer(mExecuteIndex);
                    //}
                    //else
                    //{
                        // 'real' variable
                        if (!IsStrict) // in strict mode, the variable must be available by external bindings. in "lax" mode, the variable is added when encountered first.
                        {
                            AddVariable(m_token);
                        }
                        mTokens[mExecuteIndex].Type = TokenType.Variable;
                        mTokens[mExecuteIndex].Value = 0;
                        mTokens[mExecuteIndex++].Index = GetVariableIndex(m_token);
                        CheckBuffer(mExecuteIndex);
                        IsConstant = false;
                    //}
                }
                NextToken();
            }
            else if (mState == TokenType.Stop || mState == TokenType.Unknown)
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
                mTokens[mExecuteIndex].Type = TokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = '^';
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel3()
        {
            // unary operator (- bzw. +)
            string op;
            op = m_token;
            bool Unary = false;
            if (op == "-" && (mLastState == TokenType.Operator || mLastState == TokenType.Unknown || mLastState == TokenType.Compare || mLastState == TokenType.Logical || mLastState == TokenType.Function))
            {
                NextToken();
                Unary = true;
            }
            ParseLevel4();
            if (Unary && op == "-")
            {
                //FResult=-FResult;
                mTokens[mExecuteIndex].Type = TokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = '_';
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel4()
        {
            // Klammer und Funktionen
            string func;
            Atom();
            //double temp=FResult;
            if (m_token == "(" || mState == TokenType.Function)
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
                        if (mState == TokenType.Delimiter)
                        {
                            NextToken();
                        }
                    }
                    if (MaxArgCount[idx] > 0 && MaxArgCount[idx] != argcount)
                    {
                        throw new NotSupportedException(String.Format("Function {0} assumes {1} arguments!", func, MaxArgCount[idx]));
                    }
                    //throw std::logic_error("Funktion " + func + " erwartet " + std::string(MaxArgCount[idx]) + " Parameter!");
                    mTokens[mExecuteIndex].Type = TokenType.Function;
                    mTokens[mExecuteIndex].Value = argcount;
                    mTokens[mExecuteIndex++].Index = idx;
                    CheckBuffer(mExecuteIndex);
                }
                if (m_token != "}" && m_token != ")") // Fehler
                {
                    throw new NotSupportedException(String.Format("unbalanced number of parentheses in [{0}].", ExpressionString));
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
                mVariableValues[idx] = value;
            }
            else
            {
                throw new NotSupportedException("Invalid variable " + name);
            }
        }

        public double Calculate(Model model, double x = 0.0, double y = 0.0, bool forceExecution = false)
        {
            if (mLinearizedDimensionCount > 0 && !forceExecution)
            {
                if (mLinearizedDimensionCount == 1)
                {
                    return GetLinearizedValue(model, x);
                }
                return GetLinearizedValue(model, x, y); // matrix case
            }
            double[] var_space = new double[10];
            var_space[0] = x;
            var_space[1] = y;
            IsStrict = false;
            return this.Execute(model, var_space); // execute with local variables on stack
        }

        public double Calculate(ExpressionWrapper obj, Model model, double variable1 = 0.0, double variable2 = 0.0)
        {
            double[] variableStack = new double[10];
            variableStack[0] = variable1;
            variableStack[1] = variable2;
            IsStrict = false;
            return Execute(model, variableStack, obj); // execute with local variables on stack
        }

        private int GetFunctionIndex(string functionName)
        {
            int index = Expression.MathFunctions.IndexOf(functionName); // check full names
            if (index < 0)
            {
                throw new NotSupportedException("Function " + functionName + " not defined!");
            }
            return index;
        }

        public double Execute(Model model, double[] variableList = null, ExpressionWrapper obj = null)
        {
            if (!m_parsed)
            {
                this.Parse(obj);
                if (!m_parsed)
                {
                    throw new ApplicationException("Expression '" + this.ExpressionString + "' failed to parse.");
                }
            }
            if (this.IsEmpty)
            {
                // leere expr.
                //m_logicResult=false;
                return 0.0;
            }

            double[] varSpace = variableList ?? this.mVariableValues;
            List<double> stack = new List<double>(32);
            List<bool> logicStack = new List<bool>(32) { true }; // zumindest eins am anfang... (at least one thing at the beginning)
            int stackDepth = 0;  // p=head pointer
            int logicStackDepth = 1;
            int execIndex = 0;
            for (Token exec = this.mTokens[execIndex]; exec.Type != TokenType.Stop; exec = this.mTokens[++execIndex])
            {
                switch (exec.Type)
                {
                    case TokenType.Operator:
                        stackDepth--;
                        switch (exec.Index)
                        {
                            case '+': stack[stackDepth - 1] = stack[stackDepth - 1] + stack[stackDepth]; break;
                            case '-': stack[stackDepth - 1] = stack[stackDepth - 1] - stack[stackDepth]; break;
                            case '*': stack[stackDepth - 1] = stack[stackDepth - 1] * stack[stackDepth]; break;
                            case '/': stack[stackDepth - 1] = stack[stackDepth - 1] / stack[stackDepth]; break;
                            case '^': stack[stackDepth - 1] = Math.Pow(stack[stackDepth - 1], stack[stackDepth]); break;
                            case '_': stack[stackDepth] = -stack[stackDepth]; stackDepth++; break;  // unary operator -
                        }
                        break;
                    case TokenType.Variable:
                        double value;
                        if (exec.Index < 100)
                        {
                            value = varSpace[exec.Index];
                        }
                        else if (exec.Index < 1000)
                        {
                            value = GetModelVariable(exec.Index, model.GlobalSettings, obj);
                        }
                        else
                        {
                            value = GetExternVariable(exec.Index);
                        }
                        if (stack.Count <= stackDepth)
                        {
                            stack.Add(value);
                        }
                        else
                        {
                            stack[stackDepth] = value;
                        }
                        stackDepth++;
                        break;
                    case TokenType.Number:
                        if (stack.Count <= stackDepth)
                        {
                            stack.Add(exec.Value);
                        }
                        else
                        {
                            stack[stackDepth] = exec.Value;
                        }
                        stackDepth++;
                        break;
                    case TokenType.Function:
                        stackDepth--;
                        switch (exec.Index)
                        {
                            case 0: stack[stackDepth] = Math.Sin(stack[stackDepth]); break;
                            case 1: stack[stackDepth] = Math.Cos(stack[stackDepth]); break;
                            case 2: stack[stackDepth] = Math.Tan(stack[stackDepth]); break;
                            case 3: stack[stackDepth] = Math.Exp(stack[stackDepth]); break;
                            case 4: stack[stackDepth] = Math.Log(stack[stackDepth]); break;
                            case 5: stack[stackDepth] = Math.Sqrt(stack[stackDepth]); break;
                            // min, max, if:  variable zahl von argumenten
                            case 6: // min
                                for (int i = 0; i < exec.Value - 1; i++, stackDepth--)
                                {
                                    stack[stackDepth - 1] = (stack[stackDepth] < stack[stackDepth - 1]) ? stack[stackDepth] : stack[stackDepth - 1];
                                }
                                break;
                            case 7:  //max
                                for (int i = 0; i < exec.Value - 1; i++, stackDepth--)
                                {
                                    stack[stackDepth - 1] = (stack[stackDepth] > stack[stackDepth - 1]) ? stack[stackDepth] : stack[stackDepth - 1];
                                }
                                break;
                            case 8: // if
                                if (stack[stackDepth - 2] == 1) // true
                                {
                                    stack[stackDepth - 2] = stack[stackDepth - 1];
                                }
                                else
                                {
                                    stack[stackDepth - 2] = stack[stackDepth]; // false
                                }
                                stackDepth -= 2; // throw away both arguments
                                break;
                            case 9: // incrementelle summe
                                m_incSumVar += stack[stackDepth];
                                stack[stackDepth] = m_incSumVar;
                                break;
                            case 10: // Polygon-Funktion
                                stack[stackDepth - (int)(exec.Value - 1)] = UserDefinedPolygon(stack[stackDepth - (int)(exec.Value - 1)], stack, stackDepth, (int)exec.Value);
                                stackDepth -= (int)(exec.Value - 1);
                                break;
                            case 11: // Modulo-Division: erg=rest von arg1/arg2
                                stackDepth--; // p zeigt auf ergebnis...
                                stack[stackDepth] = stack[stackDepth] % stack[stackDepth + 1];
                                break;
                            case 12: // hilfsfunktion fr sigmoidie sachen.....
                                stack[stackDepth - 3] = UserDefinedSigmoid(stack[stackDepth - 3], stack[stackDepth - 2], stack[stackDepth - 1], stack[stackDepth]);
                                stackDepth -= 3; // drei argumente (4-1) wegwerfen...
                                break;
                            case 13:
                            case 14: // rnd(from, to) bzw. rndg(mean, stddev)
                                stackDepth--;
                                // index-13: 1 bei rnd, 0 bei rndg
                                stack[stackDepth] = UserDefinedRandom(model, exec.Index - 13, stack[stackDepth], stack[stackDepth + 1]);
                                break;
                            case 15: // in-list in() operator
                                stack[stackDepth - (int)(exec.Value - 1)] = UserDefinedFunctionInList(stack[stackDepth - (int)(exec.Value - 1)], stack, stackDepth, (int)exec.Value);
                                stackDepth -= (int)(exec.Value - 1);
                                break;
                            case 16: // round()
                                stack[stackDepth] = stack[stackDepth] < 0.0 ? Math.Ceiling(stack[stackDepth] - 0.5) : Math.Floor(stack[stackDepth] + 0.5);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        stackDepth++;
                        break;
                    case TokenType.Logical:
                        stackDepth--;
                        logicStackDepth--;
                        switch ((Operation)exec.Index)
                        {
                            case Operation.And:
                                logicStack[stackDepth - 1] = (logicStack[stackDepth - 1] && logicStack[stackDepth]);
                                break;
                            case Operation.Or:
                                logicStack[stackDepth - 1] = (logicStack[stackDepth - 1] || logicStack[stackDepth]);
                                break;
                        }
                        if (logicStack[stackDepth - 1])
                        {
                            stack[stackDepth - 1] = 1;
                        }
                        else
                        {
                            stack[stackDepth - 1] = 0;
                        }
                        break;
                    case TokenType.Compare:
                        {
                            stackDepth--;
                            bool LogicResult = false;
                            switch ((Operation)exec.Index)
                            {
                                case Operation.Equal: LogicResult = (stack[stackDepth - 1] == stack[stackDepth]); break;
                                case Operation.NotEqual: LogicResult = (stack[stackDepth - 1] != stack[stackDepth]); break;
                                case Operation.LessThan: LogicResult = (stack[stackDepth - 1] < stack[stackDepth]); break;
                                case Operation.GreaterThen: LogicResult = (stack[stackDepth - 1] > stack[stackDepth]); break;
                                case Operation.GreaterThanOrEqual: LogicResult = (stack[stackDepth - 1] >= stack[stackDepth]); break;
                                case Operation.LessThanOrEqual: LogicResult = (stack[stackDepth - 1] <= stack[stackDepth]); break;
                            }
                            if (LogicResult)
                            {
                                stack[stackDepth - 1] = 1.0;   // 1 means true...
                            }
                            else
                            {
                                stack[stackDepth - 1] = 0.0;
                            }

                            logicStack[stackDepth++] = LogicResult;
                            break;
                        }
                    case TokenType.Stop:
                    case TokenType.Unknown:
                    case TokenType.Delimiter:
                    default:
                        throw new NotSupportedException(String.Format("invalid token during execution: {0}", ExpressionString));
                } // switch()
            }

            // TODO: also check logic stack?
            if (stackDepth != 1)
            {
                throw new NotSupportedException(String.Format("execute: stack unbalanced: {0}", ExpressionString));
            }
            //m_logicResult=*(lp-1);
            return stack[stackDepth - 1];
        }

        public double AddVariable(string varName)
        {
            // add var
            int idx = mVariableNames.IndexOf(varName);
            if (idx == -1)
            {
                mVariableNames.Add(varName);
            }
            return mVariableValues[GetVariableIndex(varName)];
        }

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
            if ((mExternalVariableNames != null) && (mExternalVariableNames.Count > 0))
            {
                idx = mExternalVariableNames.IndexOf(variableName);
                if (idx > -1)
                {
                    return 1000 + idx;
                }
            }
            idx = mVariableNames.IndexOf(variableName);
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

        public double GetModelVariable(int varIdx, GlobalSettings globalSettings, ExpressionWrapper obj = null)
        {
            // der weg nach draussen....
            ExpressionWrapper model_object = obj ?? Wrapper;
            int idx = varIdx - 100; // intern als 100+x gespeichert...
            if (model_object != null)
            {
                return model_object.Value(idx, globalSettings);
            }
            // hier evtl. verschiedene objekte unterscheiden (Zahlenraum???)
            throw new NotSupportedException("getModelVar: invalid model variable!");
        }

        public void SetExternalVariableSpace(List<string> externalNames, double[] externalSpace)
        {
            // externe variablen (zB von Scripting-Engine) bekannt machen...
            mExternalVariableValues = externalSpace;
            mExternalVariableNames = externalNames;
        }

        public double GetExternVariable(int Index)
        {
            //if (Script)
            //   return Script->GetNumVar(Index-1000);
            //else   // berhaupt noch notwendig???
            return mExternalVariableValues[Index - 1000];
        }

        public void EnableIncrementalSum()
        {
            // Funktion "inkrementelle summe" einschalten.
            // dabei wird der zhler zurckgesetzt und ein flag gesetzt.
            // m_incSumEnabled = true;
            m_incSumVar = 0.0;
        }

        // "Userdefined Function" Polygon
        private double UserDefinedPolygon(double value, List<double> stack, int position, int ArgCount)
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

        private double UserDefinedFunctionInList(double value, List<double> stack, int position, int argCount)
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

        private void CheckBuffer(int index)
        {
            // um den Buffer fr Befehle kmmern.
            // wenn der Buffer zu klein wird, neuen Platz reservieren.
            if (index < m_execListSize)
            {
                return; // nix zu tun.
            }
            int NewSize = m_execListSize * 2; // immer verdoppeln: 5->10->20->40->80->160
                                              // (1) neuen Buffer anlegen....
            Token[] NewBuf = new Token[NewSize];
            // (2) bisherige Werte umkopieren....
            for (int i = 0; i < m_execListSize; i++)
            {
                NewBuf[i] = mTokens[i];
            }
            // (3) alten buffer lschen und pointer umsetzen...
            mTokens = NewBuf;
            m_execListSize = NewSize;
        }

        private double UserDefinedRandom(Model model, int type, double p1, double p2)
        {
            // random / gleichverteilt - normalverteilt
            if (type == 0)
            {
                return model.RandomGenerator.Random(p1, p2);
            }
            else    // gaussverteilt
            {
                return model.RandomGenerator.RandNorm(p1, p2);
            }
        }

        /** Linarize an expression, i.e. approximate the function by linear interpolation.
            This is an option for performance critical calculations that include time consuming mathematic functions (e.g. exp())
            low_value: linearization start at this value. values below produce an error
            high_value: upper limit
            steps: number of steps the function is split into
          */
        public void Linearize(Model model, double lowValue, double highValue, int steps = 1000)
        {
            if (model.Project.System.Settings.ExpressionLinearizationEnabled == false)
            {
                throw new NotSupportedException("Linearize() called when linearization is not enabled.");
            }

            mLinearized.Clear();
            mLinearLow = lowValue;
            mLinearHigh = highValue;
            mLinearStep = (highValue - lowValue) / (double)steps;
            // for the high value, add another step (i.e.: include maximum value) and add one step to allow linear interpolation
            for (int i = 0; i <= steps + 1; i++)
            {
                double x = mLinearLow + i * mLinearStep;
                double r = Calculate(model, x);
                mLinearized.Add(r);
            }
            mLinearizedDimensionCount = 1;
        }

        /// like 'linearize()' but for 2d-matrices
        public void Linearize(Model model, double lowX, double highX, double lowY, double highY, int stepsX = 50, int stepsY = 50)
        {
            if (model.Project.System.Settings.ExpressionLinearizationEnabled == false)
            {
                throw new NotSupportedException("Linearize() called when linearization is not enabled.");
            }
            mLinearized.Clear();
            mLinearLow = lowX;
            mLinearHigh = highX;
            mLinearLowY = lowY;
            mLinearHighY = highY;

            mLinearStep = (highX - lowX) / (double)stepsX;
            mLinearStepY = (highY - lowY) / (double)stepsY;
            for (int i = 0; i <= stepsX + 1; i++)
            {
                for (int j = 0; j <= stepsY + 1; j++)
                {
                    double x = mLinearLow + i * mLinearStep;
                    double y = mLinearLowY + j * mLinearStepY;
                    double r = Calculate(model, x, y);
                    mLinearized.Add(r);
                }
            }
            mLinearStepCountY = stepsY + 2;
            mLinearizedDimensionCount = 2;
        }

        /// calculate the linear approximation of the result value
        private double GetLinearizedValue(Model model, double x)
        {
            if (x < mLinearLow || x > mLinearHigh)
            {
                return Calculate(model, x, 0.0, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
        private double GetLinearizedValue(Model model, double x, double y)
        {
            if (x < mLinearLow || x > mLinearHigh || y < mLinearLowY || y > mLinearHighY)
            {
                return Calculate(model, x, y, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
