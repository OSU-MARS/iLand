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
        private static readonly List<string> aggList = new List<string>() { "mean" + "sum" + "min" + "max" + "p25" + "p50" + "p75" + "p5" + "p10" + "p90" + "p95" };

        private Model mModel;
        private bool mPaused;
        private bool mRunning;
        private bool mFinished;
        private bool mCanceled;
        private bool mHasError;
        private string mLastError;
        private int mYearsToRun;
        private string mInitFile;
        private bool mDynamicOutputEnabled;
        private List<string> mDynFieldList;
        private List<string> mDynData;
        private string mLastLoadedJSFile;

        public Model model() { return mModel; }
        public string lastError() { return mLastError; } ///< error message of the last received error
        public int totalYears() { return mYearsToRun; } ///< returns total number of years to simulate
        // dynamic outputs (variable fields)
        public void setDynamicOutputEnabled(bool enabled) { mDynamicOutputEnabled = enabled; }
        public void setLoadedJavascriptFile(string filename) { mLastLoadedJSFile = filename; }
        public string loadedJavascriptFile() { return mLastLoadedJSFile; }

        public ModelController()
        {
            mDynFieldList = new List<string>();
            mDynData = new List<string>();
            mModel = null;
            mPaused = false;
            mRunning = false;
            mHasError = false;
            mYearsToRun = 0;
            mDynamicOutputEnabled = false;
        }

        public void connectSignals()
        {
            throw new NotImplementedException();
        }

        /// prepare a list of all (active) species
        public List<Species> availableSpecies()
        {
            List<Species> list = new List<Species>();
            if (mModel != null)
            {
                SpeciesSet set = mModel.speciesSet();
                if (set == null)
                {
                    throw new NotSupportedException("there are 0 or more than one species sets.");
                }
                foreach (Species s in set.activeSpecies())
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public bool canCreate()
        {
            if (mModel == null)
            {
                return false;
            }
            return true;
        }

        public bool canDestroy()
        {
            return mModel != null;
        }

        public bool canRun()
        {
            if (mModel != null && mModel.isSetup())
            {
                return true;
            }
            return false;
        }

        public bool isRunning()
        {
            return mRunning;
        }

        public bool isFinished()
        {
            if (mModel != null)
            {
                return false;
            }
            return canRun() && !isRunning() && mFinished;
        }

        bool isPaused()
        {
            return mPaused;
        }

        public int currentYear()
        {
            return GlobalSettings.instance().currentYear();
        }

        public void setFileName(string initFileName)
        {
            mInitFile = initFileName;
            GlobalSettings.instance().loadProjectFile(mInitFile);
        }

        public void create()
        {
            if (!canCreate())
            {
                return;
            }

            Debug.WriteLine("**************************************************");
            Debug.WriteLine("project-file: " + mInitFile);
            Debug.WriteLine("started at: " + DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss"));
            Debug.WriteLine("iLand " + this.GetType().Assembly.GetName().Version);
            Debug.WriteLine("**************************************************");

            mHasError = false;
            DebugTimer.clearAllTimers();
            mModel = new Model();
            mModel.loadProject();
            if (!mModel.isSetup())
            {
                mHasError = true;
                mLastError = "An error occured during the loading of the project. Please check the logs.";
                return;
            }

            // reset clock...
            GlobalSettings.instance().setCurrentYear(1); // reset clock
                                                         // initialization of trees, output on startup
            mModel.beforeRun();
            GlobalSettings.instance().executeJSFunction("onAfterCreate");

            Debug.WriteLine("Model created.");
        }

        public void destroy()
        {
            if (canDestroy())
            {
                GlobalSettings.instance().executeJSFunction("onBeforeDestroy");
                mModel = null;
                GlobalSettings.instance().setCurrentYear(0);
                Debug.WriteLine("ModelController: Model destroyed.");
            }
        }

        public void runloop()
        {
            DateTime sLastTime = DateTime.Now;
            //   QCoreApplication::processEvents();
            if (mPaused)
            {
                return;
            }
            bool doStop = false;
            mHasError = false;
            if (GlobalSettings.instance().currentYear() <= 1)
            {
                sLastTime = DateTime.Now; // reset clock at the beginning of the simulation
            }

            if (!mCanceled && GlobalSettings.instance().currentYear() < mYearsToRun)
            {
                mHasError = runYear(); // do the work!

                mRunning = true;
                if (!mHasError)
                {
                    int elapsed = (int)(DateTime.Now - sLastTime).TotalMilliseconds;
                    int time = 0;
                    if (currentYear() % 50 == 0 && elapsed > 10000)
                    {
                        time = 100; // a 100ms pause...
                    }
                    if (currentYear() % 100 == 0 && elapsed > 10000)
                    {
                        time = 500; // a 500ms pause...
                    }
                    if (time > 0)
                    {
                        sLastTime = DateTime.Now; // reset clock
                        Debug.WriteLine("--- little break ---- (after " + elapsed + "ms).");
                        //QTimer::singleShot(time,this, SLOT(runloop()));
                    }

                }
                else
                {
                    doStop = true; // an error occured
                    mLastError = "An error occured while running the model. Please check the logs.";
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
                internalStop();
            }
        }

        public bool internalRun()
        {
            // main loop
            while (mRunning && !mPaused && !mFinished)
            {
                runloop(); // start the running loop
            }
            return isFinished();
        }

        public void internalStop()
        {
            if (mRunning)
            {
                GlobalSettings.instance().outputManager().save();
                DebugTimer.printAllTimers();
                saveDebugOutputs();
                //if (GlobalSettings.instance().dbout().isOpen())
                //    GlobalSettings.instance().dbout().close();

                mFinished = true;
            }
            mRunning = false;
            mPaused = false; // in any case
        }

        public void run(int years)
        {
            if (!canRun())
            {
                return;
            }

            DebugTimer many_runs = new DebugTimer(String.Format("Timer for {0} runs", years));
            mPaused = false;
            mFinished = false;
            mCanceled = false;
            mYearsToRun = years;
            //GlobalSettings.instance().setCurrentYear(1); // reset clock

            DebugTimer.clearAllTimers();
            mRunning = true;

            Debug.WriteLine("ModelControler: runloop started.");
            internalRun();
        }

        public bool runYear()
        {
            if (!canRun())
            {
                return false;
            }
            using DebugTimer t = new DebugTimer("ModelController:runYear");
            Debug.WriteLine(DateTime.Now.ToString("hh:mm:ss:") + "ModelController: run year " + currentYear());

            if (GlobalSettings.instance().settings().paramValueBool("debug_clear"))
            {
                GlobalSettings.instance().clearDebugLists();  // clear debug data
            }
            bool err = false;
            GlobalSettings.instance().executeJSFunction("onYearBegin");
            mModel.runYear();

            fetchDynamicOutput();

            return err;
        }

        bool pause()
        {
            if (!isRunning())
            {
                return mPaused;
            }
            if (mPaused)
            {
                // currently in pause - mode . continue
                mPaused = false;

            }
            else
            {
                // currently running . set to pause mode
                GlobalSettings.instance().outputManager().save();
                mPaused = true;
            }
            return mPaused;
        }

        public bool continueRun()
        {
            mRunning = true;
            return internalRun();
        }

        public void cancel()
        {
            mCanceled = true;
            internalStop();
        }


        // this function is called when exceptions occur in multithreaded code.
        public void throwError(string msg)
        {
            Debug.WriteLine("ModelController: throwError reached:");
            Debug.WriteLine(msg);
            mLastError = msg;
            mHasError = true;
            throw new NotSupportedException(msg); // raise error again

        }

        //////////////////////////////////////
        // dynamic output
        public void setupDynamicOutput(string fieldList)
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
            mDynamicOutputEnabled = true;
        }

        public string dynamicOutput()
        {
            return String.Join(System.Environment.NewLine, mDynData);
        }

        public void fetchDynamicOutput()
        {
            if (!mDynamicOutputEnabled || mDynFieldList.Count == 0)
            {
                return;
            }
            using DebugTimer t = new DebugTimer("dynamic output");
            List<string> var = new List<string>();
            string lastVar = "";
            List<double> data = new List<double>();
            AllTreeIterator at = new AllTreeIterator(mModel);
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
                    at.reset();
                    var_index = 0;
                    if (simple_expression)
                    {
                        var_index = tw.variableIndex(var[0]);
                        if (var_index < 0)
                        {
                            throw new NotSupportedException("Invalid variable name for dynamic output: " + var[0]);
                        }
                    }
                    else
                    {
                        custom_expr.setExpression(var[0]);
                        custom_expr.setModelObject(tw);
                    }
                    for (Tree tree = at.next(); tree != null; tree = at.next())
                    {
                        tw.setTree(tree);
                        if (simple_expression)
                        {
                            value = tw.value(var_index);
                        }
                        else
                        {
                            value = custom_expr.execute();
                        }
                        data.Add(value);
                    }
                    stat.setData(data);
                }
                // fetch data
                int var_index_inner = aggList.IndexOf(var[1]);
                switch (var_index_inner)
                {
                    case 0: value = stat.mean(); break;
                    case 1: value = stat.sum(); break;
                    case 2: value = stat.min(); break;
                    case 3: value = stat.max(); break;
                    case 4: value = stat.percentile25(); break;
                    case 5: value = stat.median(); break;
                    case 6: value = stat.percentile75(); break;
                    case 7: value = stat.percentile(5); break;
                    case 8: value = stat.percentile(10); break;
                    case 9: value = stat.percentile(90); break;
                    case 10: value = stat.percentile(95); break;
                    default:
                        throw new NotSupportedException(String.Format("Invalid aggregate expression for dynamic output: {0}{2}allowed: {1}",
                                                                       var[1], String.Join(' ', aggList), System.Environment.NewLine));
                }
                line.Add(value.ToString());
            }
            line.Insert(0, data.Count.ToString());
            line.Insert(0, GlobalSettings.instance().currentYear().ToString());
            mDynData.Add(String.Join(';', line));
        }

        public void saveDebugOutputs()
        {
            // save to files if switch is true
            if (!GlobalSettings.instance().settings().valueBool("system.settings.debugOutputAutoSave"))
            {
                return;
            }
            string p = GlobalSettings.instance().path("debug_", "temp");

            GlobalSettings.instance().debugDataTable(DebugOutputs.dTreePartition, ";", p + "tree_partition.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dTreeGrowth, ";", p + "tree_growth.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dTreeNPP, ";", p + "tree_npp.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dStandGPP, ";", p + "stand_gpp.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dWaterCycle, ";", p + "water_cycle.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dDailyResponses, ";", p + "daily_responses.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dEstablishment, ";", p + "establishment.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dSaplingGrowth, ";", p + "saplinggrowth.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dCarbonCycle, ";", p + "carboncycle.csv");
            GlobalSettings.instance().debugDataTable(DebugOutputs.dPerformance, ";", p + "performance.csv");
            Helper.saveToTextFile(p + "dynamic.csv", dynamicOutput());
            Helper.saveToTextFile(p + "version.txt", this.GetType().Assembly.FullName);

            Debug.WriteLine("saved debug outputs to " + p);
        }

        public void saveScreenshot(string file_name)
        {
            throw new NotImplementedException();
        }

        public void paintMap(MapGrid map, double min_value, double max_value)
        {
            throw new NotImplementedException();
        }

        public void addGrid(Grid<float> grid, string name, GridViewType view_type, double min_value, double max_value)
        {
            throw new NotImplementedException();
        }

        public void addLayers(LayeredGridBase layers, string name)
        {
            throw new NotImplementedException();
        }

        public void removeLayers(LayeredGridBase layers)
        {
            throw new NotImplementedException();
        }

        public void setViewport(PointF center_point, double scale_px_per_m)
        {
            throw new NotImplementedException();
        }

        public void setUIShortcuts(object shortcuts)
        {
            throw new NotImplementedException();
        }

        public void repaint()
        {
            throw new NotImplementedException();
        }
    }
}
