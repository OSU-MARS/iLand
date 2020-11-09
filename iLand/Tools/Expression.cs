﻿using System;
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
        public static readonly ReadOnlyCollection<string> MathFunctions = new List<string>()
        {
            // 0   1      2      3      4     5       6      7      8     9         10         11     12         13     14      15    16
            "sin", "cos", "tan", "exp", "ln", "sqrt", "min", "max", "if", "incsum", "polygon", "mod", "sigmoid", "rnd", "rndg", "in", "round"
        }.AsReadOnly();

        private static readonly int[] MaxArgCount = new int[] { 1, 1, 1, 1, 1, 1, -1, -1, 3, 1, -1, 2, 4, 2, 2, -1, 1 };

        // inc-sum
        private double mIncrementalSum;

        private bool isParsed;
        private ExpressionToken[] mTokens;
        private int mExecListSize; // size of buffer
        private int mExecuteIndex;
        private readonly double[] mVariableValues;
        //private readonly List<string> mExternalVariableNames;
        private double[] mExternalVariableValues;
        private ExpressionTokenType mState;
        private ExpressionTokenType mLastState;
        private int mParsePosition;
        private string mToken;
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
        public bool RequireExternalVariableBinding { get; set; }
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

        private ExpressionTokenType NextToken()
        {
            ++mTokenCount;
            mLastState = mState;
            // nchsten m_token aus String lesen...
            // whitespaces eliminieren...
            while ((mParsePosition < ExpressionString.Length) && " \t\n\r".Contains(ExpressionString[mParsePosition]))
            {
                mParsePosition++;
            }

            if (mParsePosition >= ExpressionString.Length)
            {
                this.mState = ExpressionTokenType.Stop;
                this.mToken = String.Empty;
                return ExpressionTokenType.Stop; // Ende der Vorstellung
            }

            // whitespaces eliminieren...
            while (" \t\n\r".Contains(ExpressionString[mParsePosition]))
            {
                mParsePosition++;
            }
            if (ExpressionString[mParsePosition] == ',')
            {
                mToken = new string(ExpressionString[mParsePosition++], 1);
                mState = ExpressionTokenType.Delimiter;
                return ExpressionTokenType.Delimiter;
            }
            if ("+-*/(){}^".Contains(ExpressionString[mParsePosition]))
            {
                mToken = new string(ExpressionString[mParsePosition++], 1);
                mState = ExpressionTokenType.Operator;
                return ExpressionTokenType.Operator;
            }
            if ("=<>".Contains(ExpressionString[mParsePosition]))
            {
                mToken = new string(ExpressionString[mParsePosition++], 1);
                if (ExpressionString[mParsePosition] == '>' || ExpressionString[mParsePosition] == '=')
                {
                    mToken += ExpressionString[mParsePosition++];
                }
                mState = ExpressionTokenType.Compare;
                return ExpressionTokenType.Compare;
            }
            if (ExpressionString[mParsePosition] >= '0' && ExpressionString[mParsePosition] <= '9')
            {
                // Zahl
                int startPosition = mParsePosition;
                while ((mParsePosition < ExpressionString.Length) && "0123456789.".Contains(ExpressionString[mParsePosition]))
                {
                    mParsePosition++;  // nchstes Zeichen suchen...
                }
                mToken = ExpressionString[startPosition..mParsePosition];
                mState = ExpressionTokenType.Number;
                return ExpressionTokenType.Number;
            }

            if ((ExpressionString[mParsePosition] >= 'a' && ExpressionString[mParsePosition] <= 'z') || (ExpressionString[mParsePosition] >= 'A' && ExpressionString[mParsePosition] <= 'Z'))
            {
                // function ... find brace
                this.mToken = String.Empty;
                // TODO: simplify to Char.IsLetterOrDigit()
                while (mParsePosition < this.ExpressionString.Length && 
                       ((this.ExpressionString[mParsePosition] >= 'a' && this.ExpressionString[mParsePosition] <= 'z') || 
                        (this.ExpressionString[mParsePosition] >= 'A' && this.ExpressionString[mParsePosition] <= 'Z') || 
                        (this.ExpressionString[mParsePosition] >= '0' && this.ExpressionString[mParsePosition] <= '9') || 
                        this.ExpressionString[mParsePosition] == '_' || this.ExpressionString[mParsePosition] == '.') &&
                       this.ExpressionString[mParsePosition] != '(')
                {
                    mToken += ExpressionString[mParsePosition++];
                }
                // wenn am Ende Klammer, dann Funktion, sonst Variable.
                if (mParsePosition < this.ExpressionString.Length && 
                    (this.ExpressionString[mParsePosition] == '(' || this.ExpressionString[mParsePosition] == '{'))
                {
                    mParsePosition++; // skip brace
                    mState = ExpressionTokenType.Function;
                    return ExpressionTokenType.Function;
                }
                else
                {
                    if (mToken.ToLowerInvariant() == "and" || mToken.ToLowerInvariant() == "or")
                    {
                        mState = ExpressionTokenType.Logical;
                        return ExpressionTokenType.Logical;
                    }
                    else
                    {
                        mState = ExpressionTokenType.Variable;
                        if (mToken == "true")
                        {
                            mState = ExpressionTokenType.Number;
                            mToken = "1";
                            return ExpressionTokenType.Number;
                        }
                        if (mToken == "false")
                        {
                            mState = ExpressionTokenType.Number;
                            mToken = "0";
                            return ExpressionTokenType.Number;
                        }
                        return ExpressionTokenType.Variable;
                    }
                }
            }
            mState = ExpressionTokenType.Unknown;
            return ExpressionTokenType.Unknown; // in case no match was found
        }

        /** sets expression @p expr and checks the syntax (parse).
            Expressions are setup with strict = false, i.e. no fixed binding of variable names.
          */
        public void SetAndParse(string expression)
        {
            this.SetExpression(expression);
            this.RequireExternalVariableBinding = false;
            this.Parse();
        }

        /// set the current expression.
        /// do some preprocessing (e.g. handle the different use of ",", ".", ";")
        public void SetExpression(string expression)
        {
            this.ExpressionString = expression == null ? String.Empty : String.Join(' ', expression.Trim().Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            mParsePosition = 0;  // set starting point...

            for (int index = 0; index < mVariableValues.Length; index++)
            {
                mVariableValues[index] = 0.0;
            }
            isParsed = false;
            CatchExceptions = false;
            LastError = String.Empty;

            Wrapper = null;
            mExternalVariableValues = null;

            RequireExternalVariableBinding = true; // default....
            // m_incSumEnabled = false;
            IsEmpty = String.IsNullOrWhiteSpace(expression);
            // Buffer:
            mExecListSize = 5; // inital value...
            if (mTokens == null)
            {
                mTokens = new ExpressionToken[mExecListSize]; // init
            }

            mLinearizedDimensionCount = 0; // linearization is switched off
        }

        public override string ToString()
        {
            StringBuilder expression = new StringBuilder();
            foreach (ExpressionToken token in this.mTokens)
            {
                expression.Append(token.Type switch
                {
                    ExpressionTokenType.Compare => this.ExpressionString[token.Index],
                    ExpressionTokenType.Delimiter => this.ExpressionString[token.Index],
                    ExpressionTokenType.Function => Expression.MathFunctions[token.Index],
                    ExpressionTokenType.Logical => this.ExpressionString[token.Index],
                    ExpressionTokenType.Number => token.Value.ToString(),
                    ExpressionTokenType.Operator => (char)token.Index,
                    ExpressionTokenType.Stop => "<stop>",
                    ExpressionTokenType.Unknown => "<unknown>",
                    ExpressionTokenType.Variable => this.mVariableNames[token.Index].ToString(),
                    _ => throw new NotSupportedException("Unhandled token type " + token.Type + ".")
                } + " ");
                if (token.Type == ExpressionTokenType.Stop)
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
                if (isParsed)
                {
                    return;
                }

                if (wrapper != null)
                {
                    Wrapper = wrapper;
                }
                this.mState = ExpressionTokenType.Unknown;
                this.mLastState = ExpressionTokenType.Unknown;
                this.IsConstant = true;
                this.mExecuteIndex = 0;
                this.mTokenCount = 0;
                NextToken();
                while (mState != ExpressionTokenType.Stop)
                {
                    int preParseTokenCount = mTokenCount;
                    ParseLevelL0();  // start with logical level 0
                    if (preParseTokenCount == mTokenCount)
                    {
                        throw new NotSupportedException("parse(): Unbalanced Braces.");
                    }
                    if (mState == ExpressionTokenType.Unknown)
                    {
                        throw new NotSupportedException("parse(): Syntax error, token: " + mToken);
                    }
                }
                this.IsEmpty = (mExecuteIndex == 0);
                this.mTokens[mExecuteIndex].Type = ExpressionTokenType.Stop;
                this.mTokens[mExecuteIndex].Value = 0;
                this.mTokens[mExecuteIndex++].Index = 0;
                CheckBuffer(mExecuteIndex);
                this.isParsed = true;
            }
        }

        private void ParseLevelL0()
        {
            // logical operations  (and, or, not)
            string op;
            ParseLevelL1();

            while (mState == ExpressionTokenType.Logical)
            {
                op = mToken.ToLowerInvariant();
                NextToken();
                ParseLevelL1();
                ExpressionOperation logicaltok = 0;
                if (op == "and")
                {
                    logicaltok = ExpressionOperation.And;
                }
                if (op == "or")
                {
                    logicaltok = ExpressionOperation.Or;
                }

                mTokens[mExecuteIndex].Type = ExpressionTokenType.Logical;
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
            if (mState == ExpressionTokenType.Compare)
            {
                op = mToken;
                NextToken();
                ParseLevel0();
                ExpressionOperation logicaltok = 0;
                if (op == "<") 
                    logicaltok = ExpressionOperation.LessThan;
                if (op == ">") 
                    logicaltok = ExpressionOperation.GreaterThen;
                if (op == "<>") 
                    logicaltok = ExpressionOperation.NotEqual;
                if (op == "<=") 
                    logicaltok = ExpressionOperation.LessThanOrEqual;
                if (op == ">=") 
                    logicaltok = ExpressionOperation.GreaterThanOrEqual;
                if (op == "=") 
                    logicaltok = ExpressionOperation.Equal;

                mTokens[mExecuteIndex].Type = ExpressionTokenType.Compare;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = (int)logicaltok;
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel0()
        {
            // plus und minus
            ParseLevel1();

            while (mToken == "+" || mToken == "-")
            {
                string plusOrMinus = mToken;
                NextToken();
                ParseLevel1();
                mTokens[mExecuteIndex].Type = ExpressionTokenType.Operator;
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
            while (mToken == "*" || mToken == "/")
            {
                string multiplyOrDivide = mToken;
                NextToken();
                ParseLevel2();
                mTokens[mExecuteIndex].Type = ExpressionTokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = multiplyOrDivide[0];
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseAtom()
        {
            if (mState == ExpressionTokenType.Variable || mState == ExpressionTokenType.Number)
            {
                if (mState == ExpressionTokenType.Number)
                {
                    double result = double.Parse(mToken);
                    mTokens[mExecuteIndex].Type = ExpressionTokenType.Number;
                    mTokens[mExecuteIndex].Value = result;
                    mTokens[mExecuteIndex++].Index = -1;
                    CheckBuffer(mExecuteIndex);
                }
                if (mState == ExpressionTokenType.Variable)
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
                        if (!RequireExternalVariableBinding) // in strict mode, the variable must be available by external bindings. in "lax" mode, the variable is added when encountered first.
                        {
                            AddVariable(mToken);
                        }
                        mTokens[mExecuteIndex].Type = ExpressionTokenType.Variable;
                        mTokens[mExecuteIndex].Value = 0;
                        mTokens[mExecuteIndex++].Index = GetVariableIndex(mToken);
                        CheckBuffer(mExecuteIndex);
                        IsConstant = false;
                    //}
                }
                NextToken();
            }
            else if (mState == ExpressionTokenType.Stop || mState == ExpressionTokenType.Unknown)
            {
                throw new NotSupportedException("Unexpected end of m_expression.");
            }
        }

        private void ParseLevel2()
        {
            // x^y
            ParseLevel3();
            //double temp=FResult;
            while (mToken == "^")
            {
                NextToken();
                ParseLevel3();
                //FResult=pow(temp,FResult);
                mTokens[mExecuteIndex].Type = ExpressionTokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = '^';
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel3()
        {
            // unary operator (- bzw. +)
            string op;
            op = mToken;
            bool Unary = false;
            if (op == "-" && (mLastState == ExpressionTokenType.Operator || mLastState == ExpressionTokenType.Unknown || mLastState == ExpressionTokenType.Compare || mLastState == ExpressionTokenType.Logical || mLastState == ExpressionTokenType.Function))
            {
                NextToken();
                Unary = true;
            }
            ParseLevel4();
            if (Unary && op == "-")
            {
                //FResult=-FResult;
                mTokens[mExecuteIndex].Type = ExpressionTokenType.Operator;
                mTokens[mExecuteIndex].Value = 0;
                mTokens[mExecuteIndex++].Index = '_';
                CheckBuffer(mExecuteIndex);
            }
        }

        private void ParseLevel4()
        {
            // Klammer und Funktionen
            string functionName;
            ParseAtom();
            //double temp=FResult;
            if (mToken == "(" || mState == ExpressionTokenType.Function)
            {
                functionName = mToken;
                if (functionName == "(")   // klammerausdruck
                {
                    NextToken();
                    ParseLevelL0();
                }
                else        // funktion...
                {
                    int argcount = 0;
                    int idx = Expression.MathFunctions.IndexOf(functionName); // check full names
                    if (idx < 0)
                    {
                        throw new NotSupportedException("Function " + functionName + " not defined!");
                    }

                    NextToken();
                    //m_token="{";
                    // bei funktionen mit mehreren Parametern
                    while (mToken != ")")
                    {
                        argcount++;
                        ParseLevelL0();
                        if (mState == ExpressionTokenType.Delimiter)
                        {
                            NextToken();
                        }
                    }
                    if (MaxArgCount[idx] > 0 && MaxArgCount[idx] != argcount)
                    {
                        throw new NotSupportedException(String.Format("Function {0} assumes {1} arguments!", functionName, MaxArgCount[idx]));
                    }
                    //throw std::logic_error("Funktion " + func + " erwartet " + std::string(MaxArgCount[idx]) + " Parameter!");
                    mTokens[mExecuteIndex].Type = ExpressionTokenType.Function;
                    mTokens[mExecuteIndex].Value = argcount;
                    mTokens[mExecuteIndex++].Index = idx;
                    CheckBuffer(mExecuteIndex);
                }
                if (mToken != "}" && mToken != ")") // Fehler
                {
                    throw new NotSupportedException(String.Format("unbalanced number of parentheses in [{0}].", ExpressionString));
                }
                NextToken();
            }
        }

        public void SetVariable(string name, double value)
        {
            if (!isParsed)
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

        public double Evaluate(double variable1 = 0.0, double variable2 = 0.0, bool forceExecution = false)
        {
            if ((mLinearizedDimensionCount > 0) && (forceExecution == false))
            {
                if (mLinearizedDimensionCount == 1)
                {
                    return this.GetLinearizedValue(variable1);
                }
                return this.GetLinearizedValue(variable1, variable2); // matrix case
            }
            double[] variableList = new double[10];
            variableList[0] = variable1;
            variableList[1] = variable2;
            this.RequireExternalVariableBinding = false;
            return this.Execute(variableList); // execute with local variables on stack
        }

        public double Evaluate(ExpressionWrapper wrapper, double variable1 = 0.0, double variable2 = 0.0)
        {
            double[] variableList = new double[10];
            variableList[0] = variable1;
            variableList[1] = variable2;
            this.RequireExternalVariableBinding = false;
            return this.Execute(variableList, wrapper); // execute with local variables on stack
        }

        public double Execute(double[] variableList = null, ExpressionWrapper wrapper = null)
        {
            if (!isParsed)
            {
                this.Parse(wrapper);
                if (!isParsed)
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
            for (ExpressionToken exec = this.mTokens[execIndex]; exec.Type != ExpressionTokenType.Stop; exec = this.mTokens[++execIndex])
            {
                switch (exec.Type)
                {
                    case ExpressionTokenType.Operator:
                        --stackDepth;
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
                    case ExpressionTokenType.Variable:
                        double value;
                        if (exec.Index < 100)
                        {
                            value = varSpace[exec.Index];
                        }
                        else if (exec.Index < 1000)
                        {
                            value = this.GetModelVariable(exec.Index, wrapper);
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
                        ++stackDepth;
                        break;
                    case ExpressionTokenType.Number:
                        if (stack.Count <= stackDepth)
                        {
                            stack.Add(exec.Value);
                        }
                        else
                        {
                            stack[stackDepth] = exec.Value;
                        }
                        ++stackDepth;
                        break;
                    case ExpressionTokenType.Function:
                        --stackDepth;
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
                                mIncrementalSum += stack[stackDepth];
                                stack[stackDepth] = mIncrementalSum;
                                break;
                            case 10: // Polygon-Funktion
                                stack[stackDepth - (int)(exec.Value - 1)] = ExecuteUserDefinedPolygon(stack[stackDepth - (int)(exec.Value - 1)], stack, stackDepth, (int)exec.Value);
                                stackDepth -= (int)(exec.Value - 1);
                                break;
                            case 11: // Modulo-Division: erg=rest von arg1/arg2
                                stackDepth--; // p zeigt auf ergebnis...
                                stack[stackDepth] = stack[stackDepth] % stack[stackDepth + 1];
                                break;
                            case 12: // hilfsfunktion fr sigmoidie sachen.....
                                stack[stackDepth - 3] = ExecuteUserDefinedSigmoid(stack[stackDepth - 3], (int)stack[stackDepth - 2], stack[stackDepth - 1], stack[stackDepth]);
                                stackDepth -= 3; // drei argumente (4-1) wegwerfen...
                                break;
                            case 13:
                            case 14: // rnd(from, to) bzw. rndg(mean, stddev)
                                stackDepth--;
                                // index-13: 1 bei rnd, 0 bei rndg
                                stack[stackDepth] = this.ExecuteUserDefinedRandom(exec.Index - 13, stack[stackDepth], stack[stackDepth + 1]);
                                break;
                            case 15: // in-list in() operator
                                stack[stackDepth - (int)(exec.Value - 1)] = ExecuteUserDefinedFunctionInList(stack[stackDepth - (int)(exec.Value - 1)], stack, stackDepth, (int)exec.Value);
                                stackDepth -= (int)(exec.Value - 1);
                                break;
                            case 16: // round()
                                stack[stackDepth] = stack[stackDepth] < 0.0 ? Math.Ceiling(stack[stackDepth] - 0.5) : Math.Floor(stack[stackDepth] + 0.5);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        ++stackDepth;
                        break;
                    case ExpressionTokenType.Logical:
                        --stackDepth;
                        --logicStackDepth;
                        switch ((ExpressionOperation)exec.Index)
                        {
                            case ExpressionOperation.And:
                                logicStack[stackDepth - 1] = (logicStack[stackDepth - 1] && logicStack[stackDepth]);
                                break;
                            case ExpressionOperation.Or:
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
                    case ExpressionTokenType.Compare:
                        {
                            stackDepth--;
                            bool logicResult = false;
                            switch ((ExpressionOperation)exec.Index)
                            {
                                case ExpressionOperation.Equal: 
                                    logicResult = stack[stackDepth - 1] == stack[stackDepth]; 
                                    break;
                                case ExpressionOperation.NotEqual: 
                                    logicResult = stack[stackDepth - 1] != stack[stackDepth]; 
                                    break;
                                case ExpressionOperation.LessThan: 
                                    logicResult = stack[stackDepth - 1] < stack[stackDepth]; 
                                    break;
                                case ExpressionOperation.GreaterThen: 
                                    logicResult = stack[stackDepth - 1] > stack[stackDepth]; 
                                    break;
                                case ExpressionOperation.GreaterThanOrEqual: 
                                    logicResult = stack[stackDepth - 1] >= stack[stackDepth]; 
                                    break;
                                case ExpressionOperation.LessThanOrEqual: 
                                    logicResult = stack[stackDepth - 1] <= stack[stackDepth]; 
                                    break;
                            }
                            if (logicResult)
                            {
                                stack[stackDepth - 1] = 1.0;   // 1 means true...
                            }
                            else
                            {
                                stack[stackDepth - 1] = 0.0;
                            }

                            if (logicStack.Count <= stackDepth)
                            {
                                logicStack.Add(logicResult);
                            }
                            else
                            {
                                logicStack[stackDepth] = logicResult;
                            }
                            break;
                        }
                    case ExpressionTokenType.Stop:
                    case ExpressionTokenType.Unknown:
                    case ExpressionTokenType.Delimiter:
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

            // external variablen
            //if ((mExternalVariableNames != null) && (mExternalVariableNames.Count > 0))
            //{
            //    idx = mExternalVariableNames.IndexOf(variableName);
            //    if (idx > -1)
            //    {
            //        return 1000 + idx;
            //    }
            //}
            idx = mVariableNames.IndexOf(variableName);
            if (idx > -1)
            {
                return idx;
            }
            // if in strict mode, all variables must be already available at this stage.
            if (RequireExternalVariableBinding)
            {
                LastError = String.Format("Variable '{0}' in (strict) expression '{1}' not available!", variableName, ExpressionString);
                if (!CatchExceptions)
                {
                    throw new NotSupportedException(LastError);
                }
            }
            return -1;
        }

        private double GetModelVariable(int valueIndex, ExpressionWrapper wrapper = null)
        {
            // der weg nach draussen....
            ExpressionWrapper modelObject = wrapper ?? this.Wrapper;
            int index = valueIndex - 100; // intern als 100+x gespeichert...
            if (modelObject != null)
            {
                return modelObject.GetValue(index);
            }
            // hier evtl. verschiedene objekte unterscheiden (Zahlenraum???)
            throw new ArgumentOutOfRangeException(nameof(valueIndex), "Model variable not found.");
        }

        //public void SetExternalVariableSpace(List<string> externalNames, double[] externalSpace)
        //{
        //    // externe variablen (zB von Scripting-Engine) bekannt machen...
        //    mExternalVariableValues = externalSpace;
        //    mExternalVariableNames = externalNames;
        //}

        public double GetExternVariable(int index)
        {
            //if (Script)
            //   return Script->GetNumVar(Index-1000);
            //else   // berhaupt noch notwendig???
            return mExternalVariableValues[index - 1000];
        }

        public void EnableIncrementalSum()
        {
            // Funktion "inkrementelle summe" einschalten.
            // dabei wird der zhler zurckgesetzt und ein flag gesetzt.
            // m_incSumEnabled = true;
            mIncrementalSum = 0.0;
        }

        // "Userdefined Function" Polygon
        private double ExecuteUserDefinedPolygon(double value, List<double> stack, int position, int argumentCount)
        {
            // Polygon-Funktion: auf dem Stack liegen (x/y) Paare, aus denen ein "Polygon"
            // aus Linien zusammengesetzt ist. return ist der y-Wert zu x (Value).
            // Achtung: *Stack zeigt auf das letzte Argument! (ist das letzte y).
            // Stack bereinigen tut der Aufrufer.
            if (argumentCount % 2 != 1)
            {
                throw new NotSupportedException("polygon: falsche zahl parameter. polygon(<val>; x0; y0; x1; y1; ....)");
            }
            int pointCount = (argumentCount - 1) / 2;
            if (pointCount < 2)
            {
                throw new NotSupportedException("polygon: falsche zahl parameter. polygon(<val>; x0; y0; x1; y1; ....)");
            }
            double x, y, xold, yold;
            y = stack[position--];   // 1. Argument: ganz rechts.
            x = stack[position--];
            if (value > x)   // rechts drauen: annahme gerade.
            {
                return y;
            }

            for (int i = 0; i < pointCount - 1; i++)
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

        private double ExecuteUserDefinedFunctionInList(double value, List<double> stack, int position, int argumentCount)
        {
            for (int index = 0; index < argumentCount - 1; ++index)
            {
                if (value == stack[position--])
                {
                    return 1.0; // true
                }
            }
            return 0.0; // false
        }

        // userdefined func sigmoid....
        private double ExecuteUserDefinedSigmoid(double value, int sigmoidType, double p1, double p2)
        {
            // sType: typ der Funktion:
            // 0: logistische f
            // 1: Hill-funktion
            // 2: 1 - logistisch (geht von 1 bis 0)
            // 3: 1- hill
            double x = Math.Max(Math.Min(value, 1.0), 0.0);  // limit auf [0..1]
            double result = sigmoidType switch
            {
                0 or 2 => 1.0 / (1.0 + p1 * Math.Exp(-p2 * x)),
                1 or 3 => Math.Pow(x, p1) / (Math.Pow(p2, p1) + Math.Pow(x, p1)),
                _ => throw new NotSupportedException("sigmoid-funktion: ungltiger kurventyp. erlaubt: 0..3")
            };
            if (sigmoidType == 2 || sigmoidType == 3)
            {
                result = 1.0 - result;
            }

            return result;
        }

        private void CheckBuffer(int index)
        {
            // um den Buffer fr Befehle kmmern.
            // wenn der Buffer zu klein wird, neuen Platz reservieren.
            if (index < mExecListSize)
            {
                return; // nix zu tun.
            }
            int NewSize = mExecListSize * 2; // immer verdoppeln: 5->10->20->40->80->160
                                              // (1) neuen Buffer anlegen....
            ExpressionToken[] NewBuf = new ExpressionToken[NewSize];
            // (2) bisherige Werte umkopieren....
            for (int i = 0; i < mExecListSize; i++)
            {
                NewBuf[i] = mTokens[i];
            }
            // (3) alten buffer lschen und pointer umsetzen...
            mTokens = NewBuf;
            mExecListSize = NewSize;
        }

        private double ExecuteUserDefinedRandom(int type, double p1, double p2)
        {
            if ((this.Wrapper == null) || (this.Wrapper.Model == null))
            {
                throw new NotSupportedException("Unable to access random number generator. Ensure that a wrapper is specified with a non-null model.");
            }

            // random / gleichverteilt - normalverteilt
            if (type == 0)
            {
                return this.Wrapper.Model.RandomGenerator.GetRandomDouble(p1, p2);
            }
            else    // gaussverteilt
            {
                return this.Wrapper.Model.RandomGenerator.GetRandomNormal(p1, p2);
            }
        }

        /** Linarize an expression, i.e. approximate the function by linear interpolation.
            This is an option for performance critical calculations that include time consuming mathematic functions (e.g. exp())
            low_value: linearization start at this value. values below produce an error
            high_value: upper limit
            steps: number of steps the function is split into
          */
        public void Linearize(double lowValue, double highValue, int steps = 1000)
        {
            mLinearized.Clear();
            mLinearLow = lowValue;
            mLinearHigh = highValue;
            mLinearStep = (highValue - lowValue) / (double)steps;
            // for the high value, add another step (i.e.: include maximum value) and add one step to allow linear interpolation
            for (int i = 0; i <= steps + 1; i++)
            {
                double x = mLinearLow + i * mLinearStep;
                double r = this.Evaluate(x);
                mLinearized.Add(r);
            }
            mLinearizedDimensionCount = 1;
        }

        /// like 'linearize()' but for 2d-matrices
        public void Linearize(double lowX, double highX, double lowY, double highY, int stepsX = 50, int stepsY = 50)
        {
            mLinearized.Clear();
            mLinearLow = lowX;
            mLinearHigh = highX;
            mLinearLowY = lowY;
            mLinearHighY = highY;

            mLinearStep = (highX - lowX) / (double)stepsX;
            mLinearStepY = (highY - lowY) / (double)stepsY;
            for (int indexX = 0; indexX <= stepsX + 1; indexX++)
            {
                for (int indexY = 0; indexY <= stepsY + 1; indexY++)
                {
                    double x = mLinearLow + indexX * mLinearStep;
                    double y = mLinearLowY + indexY * mLinearStepY;
                    double r = this.Evaluate(x, y);
                    mLinearized.Add(r);
                }
            }
            mLinearStepCountY = stepsY + 2;
            mLinearizedDimensionCount = 2;
        }

        /// calculate the linear approximation of the result value
        private double GetLinearizedValue(double x)
        {
            if (x < mLinearLow || x > mLinearHigh)
            {
                return this.Evaluate(x, 0.0, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
                return this.Evaluate(x, y, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
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
