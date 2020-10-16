using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace iLand.Simulation
{
    internal class ModelController
    {
        //private static readonly ReadOnlyCollection<string> Aggregations = new List<string>() { "mean", "sum", "min", "max", "p25", "p50", "p75", "p5", "p10", "p90", "p95" }.AsReadOnly();

        private readonly List<string> mDynFieldList;
        private readonly List<string> mDynData;

        public bool DynamicOutputEnabled { get; set; }
        public string LastError { get; private set; } // error message of the last received error
        public string LoadedJavascriptFile { get; set; }
        public Model Model { get; private set; }
        public int YearsToRun { get; private set; } // returns total number of years to simulate

        public ModelController()
        {
            this.mDynFieldList = new List<string>();
            this.mDynData = new List<string>();

            this.Model = null;
            this.YearsToRun = 0;
            this.DynamicOutputEnabled = false;
        }

        public void ConnectSignals()
        {
            throw new NotImplementedException();
        }

        /// prepare a list of all (active) species
        //public List<Species> AvailableSpecies()
        //{
        //    List<Species> list = new List<Species>();
        //    if (Model != null)
        //    {
        //        SpeciesSet set = Model.SpeciesSet();
        //        if (set == null)
        //        {
        //            throw new NotSupportedException("there are 0 or more than one species sets.");
        //        }
        //        foreach (Species s in set.ActiveSpecies)
        //        {
        //            list.Add(s);
        //        }
        //    }
        //    return list;
        //}

        //public bool CanCreate()
        //{
        //    if (Model == null)
        //    {
        //        return false;
        //    }
        //    return true;
        //}

        //public bool CanDestroy()
        //{
        //    return Model != null;
        //}

        //public bool CanRun()
        //{
        //    if (Model != null && Model.IsSetup)
        //    {
        //        return true;
        //    }
        //    return false;
        //}

        //public void LoadFile(string initFileName)
        //{
        //    mInitFile = initFileName;
        //    Model.LoadProject(initFileName);
        //}

        public void Create()
        {
            //if (!CanCreate())
            //{
            //    return;
            //}

            //Debug.WriteLine("**************************************************");
            //Debug.WriteLine("project-file: " + mInitFile);
            //Debug.WriteLine("started at: " + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss"));
            //Debug.WriteLine("iLand " + this.GetType().Assembly.GetName().Version);
            //Debug.WriteLine("**************************************************");

            Model = new Model();
            if (!Model.IsSetup)
            {
                LastError = "An error occured during the loading of the project. Please check the logs.";
                return;
            }

            // reset clock...
            this.Model.GlobalSettings.CurrentYear = 1;
            // initialization of trees, output on startup
            this.Model.BeforeRun();

            Debug.WriteLine("Model created.");
        }

        //public void Destroy()
        //{
        //    if (CanDestroy())
        //    {
        //        Model = null;
        //        Debug.WriteLine("ModelController: Model destroyed.");
        //    }
        //}

        public void RunYear()
        {
            //if (!CanRun())
            //{
            //    return false;
            //}
            //using DebugTimer t = this.Model.DebugTimers.Create("ModelController.RunYear()");

            //if (this.Model.GlobalSettings.Settings.GetBooleanParameter("debug_clear"))
            //{
            //    this.Model.GlobalSettings.ClearDebugLists();  // clear debug data
            //}
            Model.RunYear();

            //FetchDynamicOutput(this.Model);
            //Debug.WriteLine(t.Elapsed + " ModelController: run year " + this.Model.GlobalSettings.CurrentYear);
        }

        //////////////////////////////////////
        // dynamic output
        public void SetupDynamicOutput(string fieldList)
        {
            mDynFieldList.Clear();
            if (String.IsNullOrEmpty(fieldList) == false)
            {
                Regex rx = new Regex("((?:\\[.+\\]|\\w+)\\.\\w+)");
                MatchCollection matches = rx.Matches(fieldList);
                for (int pos = 0; pos < matches.Count; ++pos)
                {
                    mDynFieldList.Add(matches[pos].Value);
                }

                //mDynFieldList = fieldList.split(QRegExp("(?:\\[.+\\]|\\w+)\\.\\w+"), string::SkipEmptyParts);
                mDynFieldList.Insert(0, "count");
                mDynFieldList.Insert(0, "year"); // fixed fields.
            }
            mDynData.Clear();
            mDynData.Add(String.Join(';', mDynFieldList));
            DynamicOutputEnabled = true;
        }

        //public string DynamicOutput()
        //{
        //    return String.Join(System.Environment.NewLine, mDynData);
        //}

        //public void FetchDynamicOutput(Model model)
        //{
        //    if (!DynamicOutputEnabled || mDynFieldList.Count == 0)
        //    {
        //        return;
        //    }
        //    using DebugTimer t = model.DebugTimers.Create("ModelController.FetchDynamicOutput()");
        //    List<string> var = new List<string>();
        //    string lastVar = "";
        //    List<double> data = new List<double>();
        //    AllTreeIterator at = new AllTreeIterator(Model);
        //    TreeWrapper tw = new TreeWrapper();
        //    int var_index;
        //    StatData stat = new StatData();
        //    double value;
        //    List<string> line = new List<string>();
        //    Expression custom_expr = new Expression();
        //    bool simple_expression;
        //    foreach (string field in mDynFieldList) 
        //    {
        //        if (field == "count" || field == "year")
        //        {
        //            continue;
        //        }
        //        if (field.Length > 0 && field[0] == '[')
        //        {
        //            simple_expression = false;
        //        }
        //        else
        //        {
        //            var = Regex.Split(field, "\\W+").Where(str => String.IsNullOrEmpty(str) == false).ToList();
        //            simple_expression = true;
        //        }
        //        if (var.Count != 2)
        //        {
        //            throw new NotSupportedException("Invalid variable name for dynamic output: " + field);
        //        }
        //        if (var[0] != lastVar)
        //        {
        //            // load new field
        //            data.Clear();
        //            at.Reset();
        //            var_index = 0;
        //            if (simple_expression)
        //            {
        //                var_index = tw.GetVariableIndex(var[0]);
        //                if (var_index < 0)
        //                {
        //                    throw new NotSupportedException("Invalid variable name for dynamic output: " + var[0]);
        //                }
        //            }
        //            else
        //            {
        //                custom_expr.SetExpression(var[0]);
        //                custom_expr.Wrapper = tw;
        //            }
        //            for (Tree tree = at.MoveNext(); tree != null; tree = at.MoveNext())
        //            {
        //                tw.Tree = tree;
        //                if (simple_expression)
        //                {
        //                    value = tw.Value(var_index, this.Model.GlobalSettings);
        //                }
        //                else
        //                {
        //                    value = custom_expr.Execute(this.Model);
        //                }
        //                data.Add(value);
        //            }
        //            stat.SetData(data);
        //        }
        //        // fetch data
        //        int var_index_inner = Aggregations.IndexOf(var[1]);
        //        value = var_index_inner switch
        //        {
        //            0 => stat.Mean,
        //            1 => stat.Sum,
        //            2 => stat.Min,
        //            3 => stat.Max,
        //            4 => stat.Percentile25(),
        //            5 => stat.Median(),
        //            6 => stat.Percentile75(),
        //            7 => stat.Percentile(5),
        //            8 => stat.Percentile(10),
        //            9 => stat.Percentile(90),
        //            10 => stat.Percentile(95),
        //            _ => throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed: {1}",
        //                                                                var[1], String.Join(' ', Aggregations), System.Environment.NewLine)),
        //        };
        //        line.Add(value.ToString());
        //    }
        //    line.Insert(0, data.Count.ToString());
        //    line.Insert(0, this.Model.GlobalSettings.CurrentYear.ToString());
        //    mDynData.Add(String.Join(';', line));
        //}

        //public void SaveDebugOutputs()
        //{
        //    // save to files if switch is true
        //    if (!this.Model.GlobalSettings.Settings.GetBool("system.settings.debugOutputAutoSave"))
        //    {
        //        return;
        //    }
        //    string p = this.Model.GlobalSettings.Path("debug_", "temp");

        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.TreePartition, ";", p + "tree_partition.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.TreeGrowth, ";", p + "tree_growth.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.TreeNpp, ";", p + "tree_npp.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.StandGpp, ";", p + "stand_gpp.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.WaterCycle, ";", p + "water_cycle.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.DailyResponses, ";", p + "daily_responses.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.Establishment, ";", p + "establishment.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.SaplingGrowth, ";", p + "saplinggrowth.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.CarbonCycle, ";", p + "carboncycle.csv");
        //    this.Model.GlobalSettings.DebugDataTable(DebugOutputs.Performance, ";", p + "performance.csv");
        //    Helper.SaveToTextFile(p + "dynamic.csv", DynamicOutput());
        //    Helper.SaveToTextFile(p + "version.txt", this.GetType().Assembly.FullName);

        //    Debug.WriteLine("saved debug outputs to " + p);
        //}
    }
}
