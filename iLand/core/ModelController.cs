using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace iLand.core
{
    internal class ModelController
    {
        private static readonly List<string> AggList = new List<string>() { "mean" + "sum" + "min" + "max" + "p25" + "p50" + "p75" + "p5" + "p10" + "p90" + "p95" };

        private bool mPaused;
        private bool mRunning;
        private bool mFinished;
        private bool mCanceled;
        private bool mHasError;
        private string mInitFile;
        private readonly List<string> mDynFieldList;
        private readonly List<string> mDynData;

        public bool DynamicOutputEnabled { get; set; }
        public string LastError { get; private set; } ///< error message of the last received error
        public string LoadedJavascriptFile { get; set; }
        public Model Model { get; private set; }
        public int YearsToRun { get; private set; } ///< returns total number of years to simulate

        public ModelController()
        {
            mDynFieldList = new List<string>();
            mDynData = new List<string>();
            Model = null;
            mPaused = false;
            mRunning = false;
            mHasError = false;
            YearsToRun = 0;
            DynamicOutputEnabled = false;
        }

        public void ConnectSignals()
        {
            throw new NotImplementedException();
        }

        /// prepare a list of all (active) species
        public List<Species> AvailableSpecies()
        {
            List<Species> list = new List<Species>();
            if (Model != null)
            {
                SpeciesSet set = Model.SpeciesSet();
                if (set == null)
                {
                    throw new NotSupportedException("there are 0 or more than one species sets.");
                }
                foreach (Species s in set.ActiveSpecies)
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public bool CanCreate()
        {
            if (Model == null)
            {
                return false;
            }
            return true;
        }

        public bool CanDestroy()
        {
            return Model != null;
        }

        public bool CanRun()
        {
            if (Model != null && Model.IsSetup)
            {
                return true;
            }
            return false;
        }

        public bool IsRunning()
        {
            return mRunning;
        }

        public bool IsFinished()
        {
            if (Model != null)
            {
                return false;
            }
            return CanRun() && !IsRunning() && mFinished;
        }

        // unused in C++
        //private bool isPaused()
        //{
        //    return mPaused;
        //}

        public int CurrentYear()
        {
            return GlobalSettings.Instance.CurrentYear;
        }

        public void SetFileName(string initFileName)
        {
            mInitFile = initFileName;
            GlobalSettings.Instance.LoadProjectFile(mInitFile);
        }

        public void Create()
        {
            if (!CanCreate())
            {
                return;
            }

            Debug.WriteLine("**************************************************");
            Debug.WriteLine("project-file: " + mInitFile);
            Debug.WriteLine("started at: " + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss"));
            Debug.WriteLine("iLand " + this.GetType().Assembly.GetName().Version);
            Debug.WriteLine("**************************************************");

            mHasError = false;
            DebugTimer.ClearAllTimers();
            Model = new Model();
            Model.LoadProject();
            if (!Model.IsSetup)
            {
                mHasError = true;
                LastError = "An error occured during the loading of the project. Please check the logs.";
                return;
            }

            // reset clock...
            GlobalSettings.Instance.CurrentYear = 1;
            // initialization of trees, output on startup
            Model.BeforeRun();
            GlobalSettings.Instance.ExecuteJSFunction("onAfterCreate");

            Debug.WriteLine("Model created.");
        }

        public void Destroy()
        {
            if (CanDestroy())
            {
                GlobalSettings.Instance.ExecuteJSFunction("onBeforeDestroy");
                Model = null;
                GlobalSettings.Instance.CurrentYear = 0;
                Debug.WriteLine("ModelController: Model destroyed.");
            }
        }

        public void RunLoop()
        {
            DateTime sLastTime = DateTime.Now;
            //   QCoreApplication::processEvents();
            if (mPaused)
            {
                return;
            }
            bool doStop = false;
            mHasError = false;
            if (GlobalSettings.Instance.CurrentYear <= 1)
            {
                sLastTime = DateTime.Now; // reset clock at the beginning of the simulation
            }

            if (!mCanceled && GlobalSettings.Instance.CurrentYear < YearsToRun)
            {
                mHasError = RunYear(); // do the work!

                mRunning = true;
                if (!mHasError)
                {
                    int elapsed = (int)(DateTime.Now - sLastTime).TotalMilliseconds;
                    int time = 0;
                    if (CurrentYear() % 50 == 0 && elapsed > 10000)
                    {
                        time = 100; // a 100ms pause...
                    }
                    if (CurrentYear() % 100 == 0 && elapsed > 10000)
                    {
                        time = 500; // a 500ms pause...
                    }
                    if (time > 0)
                    {
                        Debug.WriteLine("--- little break ---- (after " + elapsed + "ms).");
                        //QTimer::singleShot(time,this, SLOT(runloop()));
                    }

                }
                else
                {
                    doStop = true; // an error occured
                    LastError = "An error occured while running the model. Please check the logs.";
                    mHasError = true;
                }

            }
            else
            {
                doStop = true; // all years simulated
            }

            if (doStop || mCanceled)
            {
                // finished
                InternalStop();
            }
        }

        public bool InternalRun()
        {
            // main loop
            while (mRunning && !mPaused && !mFinished)
            {
                RunLoop(); // start the running loop
            }
            return IsFinished();
        }

        public void InternalStop()
        {
            if (mRunning)
            {
                GlobalSettings.Instance.OutputManager.Save();
                DebugTimer.PrintAllTimers();
                SaveDebugOutputs();
                //if (GlobalSettings.instance().dbout().isOpen())
                //    GlobalSettings.instance().dbout().close();

                mFinished = true;
            }
            mRunning = false;
            mPaused = false; // in any case
        }

        public void Run(int years)
        {
            if (!CanRun())
            {
                return;
            }

            using DebugTimer many_runs = new DebugTimer(String.Format("Timer for {0} runs", years));
            mPaused = false;
            mFinished = false;
            mCanceled = false;
            YearsToRun = years;
            //GlobalSettings.instance().setCurrentYear(1); // reset clock

            DebugTimer.ClearAllTimers();
            mRunning = true;

            Debug.WriteLine("ModelControler: runloop started.");
            InternalRun();
        }

        public bool RunYear()
        {
            if (!CanRun())
            {
                return false;
            }
            using DebugTimer t = new DebugTimer("ModelController:runYear");
            Debug.WriteLine(DateTime.Now.ToString("hh:mm:ss:") + "ModelController: run year " + CurrentYear());

            if (GlobalSettings.Instance.Settings.GetBooleanParameter("debug_clear"))
            {
                GlobalSettings.Instance.ClearDebugLists();  // clear debug data
            }
            bool err = false;
            GlobalSettings.Instance.ExecuteJSFunction("onYearBegin");
            Model.RunYear();

            FetchDynamicOutput();

            return err;
        }

        // unused in C++
        //private bool pause()
        //{
        //    if (!isRunning())
        //    {
        //        return mPaused;
        //    }
        //    if (mPaused)
        //    {
        //        // currently in pause - mode . continue
        //        mPaused = false;

        //    }
        //    else
        //    {
        //        // currently running . set to pause mode
        //        GlobalSettings.instance().outputManager().save();
        //        mPaused = true;
        //    }
        //    return mPaused;
        //}

        public bool ContinueRun()
        {
            mRunning = true;
            return InternalRun();
        }

        public void Cancel()
        {
            mCanceled = true;
            InternalStop();
        }

        // this function is called when exceptions occur in multithreaded code.
        public void ThrowError(string msg)
        {
            Debug.WriteLine("ModelController: throwError reached:");
            Debug.WriteLine(msg);
            LastError = msg;
            mHasError = true;
            throw new NotSupportedException(msg); // raise error again
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

        public string DynamicOutput()
        {
            return String.Join(System.Environment.NewLine, mDynData);
        }

        public void FetchDynamicOutput()
        {
            if (!DynamicOutputEnabled || mDynFieldList.Count == 0)
            {
                return;
            }
            using DebugTimer t = new DebugTimer("dynamic output");
            List<string> var = new List<string>();
            string lastVar = "";
            List<double> data = new List<double>();
            AllTreeIterator at = new AllTreeIterator(Model);
            TreeWrapper tw = new TreeWrapper();
            int var_index;
            StatData stat = new StatData();
            double value;
            List<string> line = new List<string>();
            Expression custom_expr = new Expression();
            bool simple_expression;
            foreach (string field in mDynFieldList) 
            {
                if (field == "count" || field == "year")
                {
                    continue;
                }
                if (field.Length > 0 && field[0] == '[')
                {
                    simple_expression = false;
                }
                else
                {
                    var = Regex.Split(field, "\\W+").Where(str => String.IsNullOrEmpty(str) == false).ToList();
                    simple_expression = true;
                }
                if (var.Count != 2)
                {
                    throw new NotSupportedException("Invalid variable name for dynamic output: " + field);
                }
                if (var[0] != lastVar)
                {
                    // load new field
                    data.Clear();
                    at.Reset();
                    var_index = 0;
                    if (simple_expression)
                    {
                        var_index = tw.GetVariableIndex(var[0]);
                        if (var_index < 0)
                        {
                            throw new NotSupportedException("Invalid variable name for dynamic output: " + var[0]);
                        }
                    }
                    else
                    {
                        custom_expr.SetExpression(var[0]);
                        custom_expr.Wrapper = tw;
                    }
                    for (Tree tree = at.MoveNext(); tree != null; tree = at.MoveNext())
                    {
                        tw.Tree = tree;
                        if (simple_expression)
                        {
                            value = tw.Value(var_index);
                        }
                        else
                        {
                            value = custom_expr.Execute();
                        }
                        data.Add(value);
                    }
                    stat.SetData(data);
                }
                // fetch data
                int var_index_inner = AggList.IndexOf(var[1]);
                value = var_index_inner switch
                {
                    0 => stat.Mean,
                    1 => stat.Sum,
                    2 => stat.Min,
                    3 => stat.Max,
                    4 => stat.Percentile25(),
                    5 => stat.Median(),
                    6 => stat.Percentile75(),
                    7 => stat.Percentile(5),
                    8 => stat.Percentile(10),
                    9 => stat.Percentile(90),
                    10 => stat.Percentile(95),
                    _ => throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed: {1}",
                                                                        var[1], String.Join(' ', AggList), System.Environment.NewLine)),
                };
                line.Add(value.ToString());
            }
            line.Insert(0, data.Count.ToString());
            line.Insert(0, GlobalSettings.Instance.CurrentYear.ToString());
            mDynData.Add(String.Join(';', line));
        }

        public void SaveDebugOutputs()
        {
            // save to files if switch is true
            if (!GlobalSettings.Instance.Settings.ValueBool("system.settings.debugOutputAutoSave"))
            {
                return;
            }
            string p = GlobalSettings.Instance.Path("debug_", "temp");

            GlobalSettings.Instance.DebugDataTable(DebugOutputs.TreePartition, ";", p + "tree_partition.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.TreeGrowth, ";", p + "tree_growth.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.TreeNpp, ";", p + "tree_npp.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.StandGpp, ";", p + "stand_gpp.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.WaterCycle, ";", p + "water_cycle.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.DailyResponses, ";", p + "daily_responses.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.Establishment, ";", p + "establishment.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.SaplingGrowth, ";", p + "saplinggrowth.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.CarbonCycle, ";", p + "carboncycle.csv");
            GlobalSettings.Instance.DebugDataTable(DebugOutputs.Performance, ";", p + "performance.csv");
            Helper.SaveToTextFile(p + "dynamic.csv", DynamicOutput());
            Helper.SaveToTextFile(p + "version.txt", this.GetType().Assembly.FullName);

            Debug.WriteLine("saved debug outputs to " + p);
        }

        public void SaveScreenshot(string file_name)
        {
            throw new NotImplementedException();
        }

        public void PaintMap(MapGrid map, double min_value, double max_value)
        {
            throw new NotImplementedException();
        }

        public void AddGrid(Grid<float> grid, string name, GridViewType view_type, double min_value, double max_value)
        {
            throw new NotImplementedException();
        }

        public void AddLayers(LayeredGridBase layers, string name)
        {
            throw new NotImplementedException();
        }

        public void RemoveLayers(LayeredGridBase layers)
        {
            throw new NotImplementedException();
        }

        public void SetViewport(PointF center_point, double scale_px_per_m)
        {
            throw new NotImplementedException();
        }

        public void SetUIShortcuts(object shortcuts)
        {
            throw new NotImplementedException();
        }

        public void Repaint()
        {
            throw new NotImplementedException();
        }
    }
}
