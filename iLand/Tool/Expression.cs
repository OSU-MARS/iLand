using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace iLand.Tool
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
      float sum;
      while (Tree *tree = at.next()) {
          wrapper.setTree(tree); // set actual tree
          sum += basalArea.execute(); // execute calculation
      }
      @endcode

      Be careful with multithreading:
      Now the calculate(float v1, float v2) as well as the calculate(wrapper, v1,v2) are thread safe. execute() accesses the internal variable list and is therefore not thredsafe.
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
        private float incrementalSum;

        private bool isParsed;
        private ExpressionToken[] tokens;
        private int execListSize; // size of buffer
        private int executeIndex;
        private readonly float[] variableValues;
        //private readonly List<string> mExternalVariableNames;
        //private float[]? mExternalVariableValues;
        private ExpressionTokenType state;
        private ExpressionTokenType lastState;
        private int parsePosition;
        private string token;
        private int tokenCount;
        private readonly List<string> variableNames;

        // linearization
        private int linearizedDimensionCount;
        private readonly List<float> linearized;
        private float linearLow, linearHigh;
        private float linearStep;
        private float linearLowY, linearHighY;
        private float linearStepY;
        private int linearStepCountY;

        public string? ExpressionString { get; set; }
        public ExpressionVariableAccessor? Wrapper { get; set; }

        public bool IsConstant { get; private set; } // returns true if current expression is a constant.
        public bool IsEmpty { get; private set; } // returns true if expression is empty
        /** strict property: if true, variables must be named before execution.
          When strict=true, all variables in the expression must be added by setVar or addVar.
          if false, variable values are assigned depending on occurence. strict is false by default for calls to "calculate()".
        */
        public bool RequireExternalVariableBinding { get; set; }

        public Expression()
        {
            this.execListSize = 5; // initial size
            //this.mExternalVariableValues = null;
            this.linearized = new();
            this.token = String.Empty;
            this.tokens = new ExpressionToken[this.execListSize];
            this.variableNames = new();
            this.variableValues = new float[10];

            this.ExpressionString = null;
            this.IsEmpty = true;
            this.Wrapper = null;
        }

        public Expression(string expression)
            : this()
        {
            this.SetExpression(expression);
        }

        public Expression(string expression, ExpressionVariableAccessor wrapper)
            : this(expression)
        {
            this.Wrapper = wrapper;
        }

        //public float ExecuteLocked(Model model) // thread safe version
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
            ++this.tokenCount;
            this.lastState = this.state;
            // nachsten m_token aus String lesen...
            // whitespaces eliminieren...
            // this.ExpressionString is guaranteed non-null in Parse()
            while ((this.parsePosition < this.ExpressionString!.Length) && " \t\n\r".Contains(this.ExpressionString[this.parsePosition]))
            {
                ++this.parsePosition;
            }

            if (this.parsePosition >= this.ExpressionString.Length)
            {
                this.state = ExpressionTokenType.Stop;
                this.token = String.Empty;
                return ExpressionTokenType.Stop; // Ende der Vorstellung
            }

            // whitespaces eliminieren...
            while (" \t\n\r".Contains(this.ExpressionString[this.parsePosition]))
            {
                ++this.parsePosition;
            }
            if (this.ExpressionString[this.parsePosition] == ',')
            {
                this.token = new string(this.ExpressionString[parsePosition++], 1);
                this.state = ExpressionTokenType.Delimiter;
                return ExpressionTokenType.Delimiter;
            }
            if ("+-*/(){}^".Contains(this.ExpressionString[parsePosition]))
            {
                this.token = new string(this.ExpressionString[parsePosition++], 1);
                this.state = ExpressionTokenType.Operator;
                return ExpressionTokenType.Operator;
            }
            if ("=<>".Contains(this.ExpressionString[this.parsePosition]))
            {
                this.token = new string(this.ExpressionString[this.parsePosition++], 1);
                if (this.ExpressionString[this.parsePosition] == '>' || this.ExpressionString[this.parsePosition] == '=')
                {
                    this.token += this.ExpressionString[this.parsePosition++];
                }
                this.state = ExpressionTokenType.Compare;
                return ExpressionTokenType.Compare;
            }
            if (this.ExpressionString[this.parsePosition] >= '0' && this.ExpressionString[this.parsePosition] <= '9')
            {
                // Zahl
                int startPosition = this.parsePosition;
                while ((this.parsePosition < this.ExpressionString.Length) && "0123456789.".Contains(this.ExpressionString[this.parsePosition]))
                {
                    this.parsePosition++;  // nchstes Zeichen suchen...
                }
                this.token = this.ExpressionString[startPosition..parsePosition];
                this.state = ExpressionTokenType.Number;
                return ExpressionTokenType.Number;
            }

            if ((this.ExpressionString[parsePosition] >= 'a' && this.ExpressionString[parsePosition] <= 'z') || 
                (this.ExpressionString[parsePosition] >= 'A' && this.ExpressionString[parsePosition] <= 'Z'))
            {
                // function ... find brace
                this.token = String.Empty;
                // TODO: simplify to Char.IsLetterOrDigit()
                while (this.parsePosition < this.ExpressionString.Length && 
                       ((this.ExpressionString[this.parsePosition] >= 'a' && this.ExpressionString[this.parsePosition] <= 'z') || 
                        (this.ExpressionString[this.parsePosition] >= 'A' && this.ExpressionString[this.parsePosition] <= 'Z') || 
                        (this.ExpressionString[this.parsePosition] >= '0' && this.ExpressionString[this.parsePosition] <= '9') || 
                        this.ExpressionString[this.parsePosition] == '_' || this.ExpressionString[this.parsePosition] == '.') &&
                       this.ExpressionString[this.parsePosition] != '(')
                {
                    this.token += this.ExpressionString[this.parsePosition++];
                }
                // wenn am Ende Klammer, dann Funktion, sonst Variable.
                if (this.parsePosition < this.ExpressionString.Length && 
                    (this.ExpressionString[this.parsePosition] == '(' || this.ExpressionString[this.parsePosition] == '{'))
                {
                    this.parsePosition++; // skip brace
                    this.state = ExpressionTokenType.Function;
                    return ExpressionTokenType.Function;
                }
                else
                {
                    if (this.token.ToLowerInvariant() == "and" || this.token.ToLowerInvariant() == "or")
                    {
                        this.state = ExpressionTokenType.Logical;
                        return ExpressionTokenType.Logical;
                    }
                    else
                    {
                        this.state = ExpressionTokenType.Variable;
                        if (this.token == "true")
                        {
                            this.state = ExpressionTokenType.Number;
                            this.token = "1";
                            return ExpressionTokenType.Number;
                        }
                        if (this.token == "false")
                        {
                            this.state = ExpressionTokenType.Number;
                            this.token = "0";
                            return ExpressionTokenType.Number;
                        }
                        return ExpressionTokenType.Variable;
                    }
                }
            }
            this.state = ExpressionTokenType.Unknown;
            return ExpressionTokenType.Unknown; // in case no match was found
        }

        /** sets expression @p expr and checks the syntax (parse).
            Expressions are setup with strict = false, i.e. no fixed binding of variable names.
          */
        public void SetAndParse(string? expression)
        {
            this.SetExpression(expression);
            this.RequireExternalVariableBinding = false;
            this.Parse();
        }

        /// set the current expression.
        /// do some preprocessing (e.g. handle the different use of ",", ".", ";")
        public void SetExpression(string? expressionString)
        {
            this.ExpressionString = expressionString == null ? String.Empty : String.Join(' ', expressionString.Trim().Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            this.parsePosition = 0;  // set starting point...

            for (int index = 0; index < this.variableValues.Length; ++index)
            {
                this.variableValues[index] = 0.0F;
            }
            this.isParsed = false;
            this.Wrapper = null;

            this.RequireExternalVariableBinding = true; // default....
            // m_incSumEnabled = false;
            this.IsEmpty = String.IsNullOrWhiteSpace(expressionString);

            this.linearizedDimensionCount = 0; // linearization is switched off
        }

        public override string ToString()
        {
            if (this.ExpressionString == null)
            {
                return "<null>";
            }

            StringBuilder expression = new();
            foreach (ExpressionToken token in this.tokens)
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
                    ExpressionTokenType.Variable => this.variableNames[token.Index].ToString(),
                    _ => throw new NotSupportedException("Unhandled token type " + token.Type + ".")
                } + " ");
                if (token.Type == ExpressionTokenType.Stop)
                {
                    break;
                }
            }
            return expression.ToString();
        }

        private void Parse(ExpressionVariableAccessor? wrapper = null)
        {
            if (this.isParsed)
            {
                return;
            }

            if (this.ExpressionString == null)
            {
                throw new NotSupportedException("Expression string is null.");
            }
            if (wrapper != null)
            {
                this.Wrapper = wrapper;
            }
            this.state = ExpressionTokenType.Unknown;
            this.lastState = ExpressionTokenType.Unknown;
            this.IsConstant = true;
            this.executeIndex = 0;
            this.tokenCount = 0;
            this.NextToken();
            while (this.state != ExpressionTokenType.Stop)
            {
                int preParseTokenCount = tokenCount;
                this.ParseLevelL0();  // start with logical level 0
                if (preParseTokenCount == tokenCount)
                {
                    throw new NotSupportedException("parse(): Unbalanced Braces.");
                }
                if (this.state == ExpressionTokenType.Unknown)
                {
                    throw new NotSupportedException("parse(): Syntax error, token: " + token);
                }
            }
            this.IsEmpty = this.executeIndex == 0;
            this.tokens[this.executeIndex].Type = ExpressionTokenType.Stop;
            this.tokens[this.executeIndex].Value = 0;
            this.tokens[this.executeIndex++].Index = 0;
            this.CheckBuffer(this.executeIndex);
            this.isParsed = true;
        }

        private void ParseLevelL0()
        {
            // logical operations (and, or, not)
            this.ParseLevelL1();

            while (this.state == ExpressionTokenType.Logical)
            {
                string op = this.token.ToLowerInvariant();
                this.NextToken();
                this.ParseLevelL1();
                ExpressionOperation logicalOperator = op switch
                {
                    "and" => ExpressionOperation.And,
                    "or" => ExpressionOperation.Or,
                    _ => throw new NotSupportedException("Unhandled logical operator '" + op + "'.")
                };

                tokens[executeIndex].Type = ExpressionTokenType.Logical;
                tokens[executeIndex].Value = 0;
                tokens[executeIndex++].Index = (int)logicalOperator;
                CheckBuffer(executeIndex);
            }
        }

        private void ParseLevelL1()
        {
            // logische operationen (<,>,=,...)
            this.ParseLevel0();
            //float temp=FResult;
            if (this.state == ExpressionTokenType.Compare)
            {
                string op = this.token;
                this.NextToken();
                this.ParseLevel0();
                ExpressionOperation logicalOperator = op switch
                {
                    "<" => ExpressionOperation.LessThan,
                    ">" => ExpressionOperation.GreaterThen,
                    "<>" => ExpressionOperation.NotEqual,
                    "<=" => ExpressionOperation.LessThanOrEqual,
                    ">=" => ExpressionOperation.GreaterThanOrEqual,
                    "=" => ExpressionOperation.Equal,
                    _ => throw new NotSupportedException("Unhandled logical operator " + op + ".")
                };

                this.tokens[executeIndex].Type = ExpressionTokenType.Compare;
                this.tokens[executeIndex].Value = 0;
                this.tokens[executeIndex++].Index = (int)logicalOperator;
                this.CheckBuffer(executeIndex);
            }
        }

        private void ParseLevel0()
        {
            // plus und minus
            this.ParseLevel1();

            while (this.token == "+" || this.token == "-")
            {
                string plusOrMinus = token;
                this.NextToken();
                this.ParseLevel1();
                this.tokens[executeIndex].Type = ExpressionTokenType.Operator;
                this.tokens[executeIndex].Value = 0;
                this.tokens[executeIndex++].Index = plusOrMinus[0];
                this.CheckBuffer(executeIndex);
            }
        }

        private void ParseLevel1()
        {
            // mal und division
            ParseLevel2();
            //float temp=FResult;
            // alt:        if (m_token=="*" || m_token=="/") {
            while (token == "*" || token == "/")
            {
                string multiplyOrDivide = token;
                this.NextToken();
                ParseLevel2();
                tokens[executeIndex].Type = ExpressionTokenType.Operator;
                tokens[executeIndex].Value = 0;
                tokens[executeIndex++].Index = multiplyOrDivide[0];
                CheckBuffer(executeIndex);
            }
        }

        private void ParseAtom()
        {
            if (state == ExpressionTokenType.Variable || state == ExpressionTokenType.Number)
            {
                if (state == ExpressionTokenType.Number)
                {
                    float result = Single.Parse(token);
                    tokens[executeIndex].Type = ExpressionTokenType.Number;
                    tokens[executeIndex].Value = result;
                    tokens[executeIndex++].Index = -1;
                    CheckBuffer(executeIndex);
                }
                if (state == ExpressionTokenType.Variable)
                {
                    //if (SpeciesIndexByName.ContainsKey(m_token))
                    //{
                    //    // constant
                    //    float result = SpeciesIndexByName[m_token];
                    //    mTokens[mExecuteIndex].Type = TokenType.Number;
                    //    mTokens[mExecuteIndex].Value = result;
                    //    mTokens[mExecuteIndex++].Index = -1;
                    //    CheckBuffer(mExecuteIndex);
                    //}
                    //else
                    //{
                        // 'real' variable
                        if (this.RequireExternalVariableBinding == false) // in strict mode, the variable must be available by external bindings. in "lax" mode, the variable is added when encountered first.
                        {
                            this.AddVariable(token);
                        }
                        this.tokens[executeIndex].Type = ExpressionTokenType.Variable;
                        this.tokens[executeIndex].Value = 0;
                        this.tokens[executeIndex++].Index = this.GetVariableIndex(this.token);
                        this.CheckBuffer(this.executeIndex);
                        this.IsConstant = false;
                    //}
                }
                this.NextToken();
            }
            else if (state == ExpressionTokenType.Stop || state == ExpressionTokenType.Unknown)
            {
                throw new NotSupportedException("Unexpected end of m_expression.");
            }
        }

        private void ParseLevel2()
        {
            // x^y
            this.ParseLevel3();
            //float temp=FResult;
            while (this.token == "^")
            {
                this.NextToken();
                this.ParseLevel3();
                //FResult=pow(temp,FResult);
                this.tokens[this.executeIndex].Type = ExpressionTokenType.Operator;
                this.tokens[this.executeIndex].Value = 0;
                this.tokens[this.executeIndex++].Index = '^';
                this.CheckBuffer(this.executeIndex);
            }
        }

        private void ParseLevel3()
        {
            // unary operator (- bzw. +)
            string? op = this.token;
            bool isUnaryOperator = false;
            if (op == "-" && (lastState == ExpressionTokenType.Operator || lastState == ExpressionTokenType.Unknown || lastState == ExpressionTokenType.Compare || lastState == ExpressionTokenType.Logical || lastState == ExpressionTokenType.Function))
            {
                this.NextToken();
                isUnaryOperator = true;
            }
            this.ParseLevel4();
            if (isUnaryOperator && op == "-")
            {
                //FResult=-FResult;
                this.tokens[executeIndex].Type = ExpressionTokenType.Operator;
                this.tokens[executeIndex].Value = 0;
                this.tokens[executeIndex++].Index = '_';
                this.CheckBuffer(executeIndex);
            }
        }

        private void ParseLevel4()
        {
            // Klammer und Funktionen
            this.ParseAtom();
            //float temp=FResult;
            if (this.token == "(" || this.state == ExpressionTokenType.Function)
            {
                string functionName = this.token;
                if (functionName == "(")   // klammerausdruck
                {
                    this.NextToken();
                    this.ParseLevelL0();
                }
                else        // funktion...
                {
                    int argumentCount = 0;
                    int functionIndex = Expression.MathFunctions.IndexOf(functionName); // check full names
                    if (functionIndex < 0)
                    {
                        throw new NotSupportedException("Function " + functionName + " not defined!");
                    }

                    this.NextToken();
                    //m_token="{";
                    // bei funktionen mit mehreren Parametern
                    while (token != ")")
                    {
                        ++argumentCount;
                        this.ParseLevelL0();
                        if (state == ExpressionTokenType.Delimiter)
                        {
                            this.NextToken();
                        }
                    }
                    if (MaxArgCount[functionIndex] > 0 && MaxArgCount[functionIndex] != argumentCount)
                    {
                        throw new NotSupportedException(String.Format("Function {0} assumes {1} arguments!", functionName, MaxArgCount[functionIndex]));
                    }
                    //throw std::logic_error("Funktion " + func + " erwartet " + std::string(MaxArgCount[idx]) + " Parameter!");
                    tokens[executeIndex].Type = ExpressionTokenType.Function;
                    tokens[executeIndex].Value = argumentCount;
                    tokens[executeIndex++].Index = functionIndex;
                    this.CheckBuffer(executeIndex);
                }
                if (token != "}" && token != ")") // Fehler
                {
                    throw new NotSupportedException(String.Format("unbalanced number of parentheses in [{0}].", ExpressionString));
                }
                this.NextToken();
            }
        }

        public void SetVariable(string name, float value)
        {
            if (!isParsed)
            {
                this.Parse();
            }
            int variableIndex = this.GetVariableIndex(name);
            if (variableIndex >= 0 && variableIndex < 10)
            {
                this.variableValues[variableIndex] = value;
            }
            else
            {
                throw new NotSupportedException("Invalid variable " + name);
            }
        }

        public float Evaluate(float variable1 = 0.0F, float variable2 = 0.0F, bool forceExecution = false)
        {
            if ((linearizedDimensionCount > 0) && (forceExecution == false))
            {
                if (linearizedDimensionCount == 1)
                {
                    return this.GetLinearizedValue(variable1);
                }
                return this.GetLinearizedValue(variable1, variable2); // matrix case
            }
            float[] variableList = new float[10];
            variableList[0] = variable1;
            variableList[1] = variable2;
            this.RequireExternalVariableBinding = false;
            return this.Execute(variableList); // execute with local variables on stack
        }

        public float Evaluate(ExpressionVariableAccessor wrapper, float variable1 = 0.0F, float variable2 = 0.0F)
        {
            float[] variableList = new float[10];
            variableList[0] = variable1;
            variableList[1] = variable2;
            this.RequireExternalVariableBinding = false;
            return this.Execute(variableList, wrapper); // execute with local variables on stack
        }

        public float Execute(float[]? variableList = null, ExpressionVariableAccessor? wrapper = null)
        {
            if (this.isParsed == false)
            {
                this.Parse(wrapper);
                if (this.isParsed == false)
                {
                    throw new ApplicationException("Expression '" + this.ExpressionString + "' failed to parse.");
                }
            }
            if (this.IsEmpty)
            {
                // leere expr.
                //m_logicResult=false;
                return 0.0F;
            }

            float[] varSpace = variableList ?? this.variableValues;
            List<float> stack = new(32);
            List<bool> logicStack = new(32) { true }; // zumindest eins am anfang... (at least one thing at the beginning)
            int stackDepth = 0;  // p=head pointer
            int logicStackDepth = 1;
            int execIndex = 0;
            for (ExpressionToken exec = this.tokens[execIndex]; exec.Type != ExpressionTokenType.Stop; exec = this.tokens[++execIndex])
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
                            case '^': stack[stackDepth - 1] = MathF.Pow(stack[stackDepth - 1], stack[stackDepth]); break;
                            case '_': stack[stackDepth] = -stack[stackDepth]; stackDepth++; break;  // unary operator -
                        }
                        break;
                    case ExpressionTokenType.Variable:
                        float value;
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
                            throw new NotSupportedException("External variable not accessible.");
                            //value = this.GetExternVariable(exec.Index);
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
                            case 0: stack[stackDepth] = MathF.Sin(stack[stackDepth]); break;
                            case 1: stack[stackDepth] = MathF.Cos(stack[stackDepth]); break;
                            case 2: stack[stackDepth] = MathF.Tan(stack[stackDepth]); break;
                            case 3: stack[stackDepth] = MathF.Exp(stack[stackDepth]); break;
                            case 4: stack[stackDepth] = MathF.Log(stack[stackDepth]); break;
                            case 5: stack[stackDepth] = MathF.Sqrt(stack[stackDepth]); break;
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
                                incrementalSum += stack[stackDepth];
                                stack[stackDepth] = incrementalSum;
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
                                stack[stackDepth - 3] = Expression.ExecuteUserDefinedSigmoid(stack[stackDepth - 3], (int)stack[stackDepth - 2], stack[stackDepth - 1], stack[stackDepth]);
                                stackDepth -= 3; // drei argumente (4-1) wegwerfen...
                                break;
                            case 13:
                            case 14: // rnd(from, to) bzw. rndg(mean, stddev)
                                stackDepth--;
                                // index-13: zero -> uniformly distributed random value, nonzero -> normally distributed
                                stack[stackDepth] = this.ExecuteUserDefinedRandom(stack[stackDepth], stack[stackDepth + 1], isNormallyDistributed: (exec.Index - 13) != 0);
                                break;
                            case 15: // in-list in() operator
                                stack[stackDepth - (int)(exec.Value - 1)] = Expression.ExecuteUserDefinedFunctionInList(stack[stackDepth - (int)(exec.Value - 1)], stack, stackDepth, (int)exec.Value);
                                stackDepth -= (int)(exec.Value - 1);
                                break;
                            case 16: // round()
                                stack[stackDepth] = stack[stackDepth] < 0.0F ? MathF.Ceiling(stack[stackDepth] - 0.5F) : MathF.Floor(stack[stackDepth] + 0.5F);
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
                                stack[stackDepth - 1] = 1.0F;   // 1 means true...
                            }
                            else
                            {
                                stack[stackDepth - 1] = 0.0F;
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

        public float AddVariable(string varName)
        {
            // add var
            int idx = this.variableNames.IndexOf(varName);
            if (idx == -1)
            {
                this.variableNames.Add(varName);
            }
            return this.variableValues[this.GetVariableIndex(varName)];
        }

        private int GetVariableIndex(string variableName)
        {
            int index;
            if (this.Wrapper != null)
            {
                index = this.Wrapper.GetVariableIndex(variableName);
                if (index > -1)
                {
                    return 100 + index;
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
            index = variableNames.IndexOf(variableName);
            if (index > -1)
            {
                return index;
            }
            // if in strict mode, all variables must be already available at this stage.
            if (this.RequireExternalVariableBinding)
            {
                throw new NotSupportedException(String.Format("Variable '{0}' in (strict) expression '{1}' not available!", variableName, this.ExpressionString));
            }
            throw new ArgumentOutOfRangeException(nameof(variableName), "Variable '" + variableName + "' not found in expression.");
        }

        private float GetModelVariable(int valueIndex, ExpressionVariableAccessor? wrapper = null)
        {
            // der weg nach draussen....
            ExpressionVariableAccessor? modelWrapper = wrapper ?? this.Wrapper;
            int index = valueIndex - 100; // intern als 100+x gespeichert...
            if (modelWrapper != null)
            {
                return modelWrapper.GetValue(index);
            }
            // hier evtl. verschiedene objekte unterscheiden (Zahlenraum???)
            throw new ArgumentOutOfRangeException(nameof(valueIndex), "Model variable not found.");
        }

        //public void SetExternalVariableSpace(List<string> externalNames, float[] externalSpace)
        //{
        //    // externe variablen (zB von Scripting-Engine) bekannt machen...
        //    mExternalVariableValues = externalSpace;
        //    mExternalVariableNames = externalNames;
        //}

        //public float GetExternVariable(int index)
        //{
        //    //if (Script)
        //    //   return Script->GetNumVar(Index-1000);
        //    //else   // berhaupt noch notwendig???
        //    return mExternalVariableValues[index - 1000];
        //}

        public void EnableIncrementalSum()
        {
            // Funktion "inkrementelle summe" einschalten.
            // dabei wird der zhler zurckgesetzt und ein flag gesetzt.
            // m_incSumEnabled = true;
            incrementalSum = 0.0F;
        }

        // "Userdefined Function" Polygon
        private static float ExecuteUserDefinedPolygon(float value, List<float> stack, int position, int argumentCount)
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
            float x, y, xold, yold;
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

        private static float ExecuteUserDefinedFunctionInList(float value, List<float> stack, int position, int argumentCount)
        {
            for (int index = 0; index < argumentCount - 1; ++index)
            {
                if (value == stack[position--])
                {
                    return 1.0F; // true
                }
            }
            return 0.0F; // false
        }

        // userdefined func sigmoid....
        private static float ExecuteUserDefinedSigmoid(float value, int sigmoidType, float p1, float p2)
        {
            // sType: typ der Funktion:
            // 0: logistische f
            // 1: Hill-funktion
            // 2: 1 - logistisch (geht von 1 bis 0)
            // 3: 1- hill
            float x = MathF.Max(MathF.Min(value, 1.0F), 0.0F);  // limit auf [0..1]
            float result = sigmoidType switch
            {
                0 or 2 => 1.0F / (1.0F + p1 * MathF.Exp(-p2 * x)),
                1 or 3 => MathF.Pow(x, p1) / (MathF.Pow(p2, p1) + MathF.Pow(x, p1)),
                _ => throw new NotSupportedException("sigmoid-funktion: ungltiger kurventyp. erlaubt: 0..3")
            };
            if (sigmoidType == 2 || sigmoidType == 3)
            {
                result = 1.0F - result;
            }

            return result;
        }

        private void CheckBuffer(int index)
        {
            // um den Buffer fr Befehle kmmern.
            // wenn der Buffer zu klein wird, neuen Platz reservieren.
            if (index < execListSize)
            {
                return; // nix zu tun.
            }
            int newSize = 2 * execListSize; // immer verdoppeln: 5->10->20->40->80->160
                                            // (1) neuen Buffer anlegen....
            ExpressionToken[] newBuffer = new ExpressionToken[newSize];
            // (2) bisherige Werte umkopieren....
            for (int copyIndex = 0; copyIndex < execListSize; copyIndex++)
            {
                newBuffer[copyIndex] = tokens[copyIndex];
            }
            // (3) alten buffer lschen und pointer umsetzen...
            tokens = newBuffer;
            execListSize = newSize;
        }

        private float ExecuteUserDefinedRandom(float fromInclusive, float toInclusive, bool isNormallyDistributed)
        {
            if ((this.Wrapper == null) || (this.Wrapper.RandomGenerator == null))
            {
                throw new NotSupportedException("Unable to access random number generator. Ensure that a wrapper is specified with a non-null model.");
            }

            if (isNormallyDistributed)
            {
                return this.Wrapper.RandomGenerator.GetRandomNormal(fromInclusive, toInclusive);
            }
            else
            {
                return this.Wrapper.RandomGenerator.GetRandomFloat(fromInclusive, toInclusive); // uniform distribution
            }
        }

        /** Linarize an expression, i.e. approximate the function by linear interpolation.
            This is an option for performance critical calculations that include time consuming mathematic functions (e.g. exp())
            low_value: linearization start at this value. values below produce an error
            high_value: upper limit
            steps: number of steps the function is split into
          */
        public void Linearize(float lowValue, float highValue, int steps = 1000)
        {
            linearized.Clear();
            linearLow = lowValue;
            linearHigh = highValue;
            linearStep = (highValue - lowValue) / (float)steps;
            // for the high value, add another step (i.e.: include maximum value) and add one step to allow linear interpolation
            for (int index = 0; index <= steps + 1; ++index)
            {
                float x = linearLow + index * linearStep;
                float r = this.Evaluate(x);
                linearized.Add(r);
            }
            linearizedDimensionCount = 1;
        }

        /// like 'linearize()' but for 2d-matrices
        public void Linearize(float lowX, float highX, float lowY, float highY, int stepsX = 50, int stepsY = 50)
        {
            linearized.Clear();
            linearLow = lowX;
            linearHigh = highX;
            linearLowY = lowY;
            linearHighY = highY;

            linearStep = (highX - lowX) / (float)stepsX;
            linearStepY = (highY - lowY) / (float)stepsY;
            for (int indexX = 0; indexX <= stepsX + 1; indexX++)
            {
                for (int indexY = 0; indexY <= stepsY + 1; indexY++)
                {
                    float x = linearLow + indexX * linearStep;
                    float y = linearLowY + indexY * linearStepY;
                    float r = this.Evaluate(x, y);
                    linearized.Add(r);
                }
            }
            linearStepCountY = stepsY + 2;
            linearizedDimensionCount = 2;
        }

        /// calculate the linear approximation of the result value
        private float GetLinearizedValue(float x)
        {
            if ((x < linearLow) || (x > linearHigh))
            {
                return this.Evaluate(x, 0.0F, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
            }
            int lower = (int)((x - linearLow) / linearStep); // the lower point
            if (lower + 1 >= linearized.Count)
            {
                Debug.Assert(lower + 1 < linearized.Count);
            }
            List<float> data = linearized;
            // linear interpolation
            float result = data[lower] + (data[lower + 1] - data[lower]) / linearStep * (x - (linearLow + lower * linearStep));
            return result;
        }

        /// calculate the linear approximation of the result value
        private float GetLinearizedValue(float x, float y)
        {
            if (x < linearLow || x > linearHigh || y < linearLowY || y > linearHighY)
            {
                return this.Evaluate(x, y, true); // standard calculation without linear optimization- but force calculation to avoid infinite loop
            }
            int lowerx = (int)((x - linearLow) / linearStep); // the lower point (x-axis)
            int lowery = (int)((y - linearLowY) / linearStepY); // the lower point (y-axis)
            int idx = linearStepCountY * lowerx + lowery;
            Debug.Assert(idx + linearStepCountY + 1 < linearized.Count);
            List<float> data = linearized;
            // linear interpolation
            // mean slope in x - direction
            float slope_x = ((data[idx + linearStepCountY] - data[idx]) / linearStepY + (data[idx + linearStepCountY + 1] - data[idx + 1]) / linearStepY) / 2.0F;
            float slope_y = ((data[idx + 1] - data[idx]) / linearStep + (data[idx + linearStepCountY + 1] - data[idx + linearStepCountY]) / linearStep) / 2.0F;
            float result = data[idx] + (x - (linearLow + lowerx * linearStep)) * slope_x + (y - (linearLowY + lowery * linearStepY)) * slope_y;
            return result;
        }
    }
}
