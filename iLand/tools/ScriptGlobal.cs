using iLand.core;
using iLand.output;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace iLand.tools
{
    /** @class ScriptGlobal
      @ingroup scripts
       This is a global interface providing useful functionality for javascripts.
      Within javascript-code an instance of this class can be accessed as "Globals" in the global scope
     (no instantiation necessary).*/

    /** \page globals Globals documentation
      Here are objects visible in the global space of javascript.
      \section sec An example section
      This page contains the subsections \ref subsection1 and \ref subsection2.
      For more info see page \ref page2.
      \subsection subsection1 The first subsection
      Text.
      \subsection subsection2 The second subsection
     - year integer. Current simulation year
     - currentDir current working directory. default value is the "script" directory defined in the project file.
      More text.
*/
    // Scripting Interface for MapGrid
    internal class ScriptGlobal
    {
        private readonly Model mModel;

        public string CurrentDir { get; private set; } ///< current execution directory (default is the Script execution directory)

        public bool qt5() ///< is this the qt5-model? (changes in script object creation)
        {
            return true;
        }

        public ScriptGlobal()
        {
            mModel = GlobalSettings.Instance.Model;
            if (mModel != null)
            {
                // BUGBUG: hard coded script path ignores <scripts> element of project file
                CurrentDir = GlobalSettings.Instance.Path(null, "script") + System.IO.Path.DirectorySeparatorChar;
            }
        }

        public object Setting(string key)
        {
            XmlHelper xml = GlobalSettings.Instance.Settings;
            if (!xml.HasNode(key))
            {
                Debug.WriteLine("scriptglobal: setting key " + key + "not valid.");
                return new object(); // undefined???
            }
            return xml.Value(key);
        }

        public void Set(string key, string value)
        {
            XmlHelper xml = GlobalSettings.Instance.Settings;
            if (!xml.HasNode(key))
            {
                Debug.WriteLine("scriptglobal: setting key " + key + "not valid.");
                return;
            }
            xml.SetNodeValue(key, value);
        }

        public void Print(string message)
        {
            Debug.WriteLine(message);
        }

        public void Alert(string message)
        {
            Helper.Message(message); // nothing happens when not in GUI mode
        }

        public void Include(string filename)
        {
            string path = GlobalSettings.Instance.Path(filename);
            if (!File.Exists(path))
            {
                throw new NotSupportedException(String.Format("include(): The javascript source file '{0}' could not be found.", path));
            }

            string includeFile = Helper.LoadTextFile(path);

            QJSValue ret = GlobalSettings.Instance.ScriptEngine.Evaluate(includeFile, path);
            if (ret.IsError())
            {
                string error_message = FormattedErrorMessage(ret, includeFile);
                Debug.WriteLine(error_message);
                throw new NotSupportedException("Error in javascript-include(): " + error_message);
            }
        }

        public string DefaultDirectory(string dir)
        {
            string result = GlobalSettings.Instance.Path(null, dir) + System.IO.Path.DirectorySeparatorChar;
            return result;
        }

        public string Path(string filename)
        {
            return GlobalSettings.Instance.Path(filename);
        }

        public int Year()
        {
            return GlobalSettings.Instance.CurrentYear;
        }

        public int ResourceUnitCount()
        {
            Debug.Assert(mModel != null);
            return mModel.ResourceUnits.Count;
        }

        public double WorldWidth()
        {
            return GlobalSettings.Instance.Model.PhysicalExtent.Width;
        }

        public double WorldHeight()
        {
            return GlobalSettings.Instance.Model.PhysicalExtent.Height;
        }

        // wrapped helper functions
        public string LoadTextFile(string fileName)
        {
            return Helper.LoadTextFile(GlobalSettings.Instance.Path(fileName));
        }

        public void SaveTextFile(string fileName, string content)
        {
            Helper.SaveToTextFile(fileName, content);
        }

        public bool FileExists(string fileName)
        {
            return File.Exists(fileName);
        }

        // unused in C++
        //public void systemCmd(string filePath, string arguments)
        //{
        //    Debug.WriteLine("running system command:" + filePath + " " + arguments);
        //    using Process process = new Process();
        //    process.StartInfo.FileName = filePath;
        //    process.StartInfo.Arguments = arguments;
        //    process.Start();
        //    process.WaitForExit(); // will wait forever until finished

        //    string res_stdout = process.StandardOutput.ReadToEnd();
        //    string res_stderr = process.StandardError.ReadToEnd(); // BUGBUG: potential deadlock
        //    Debug.WriteLine("result (stdout):" + res_stdout);
        //    Debug.WriteLine("result (stderr):" + res_stderr);
        //}

        /// add trees on given resource unit
        /// @param content init file in a string (containing headers)
        /// @return number of trees added
        public int AddSingleTrees(int resourceIndex, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            ResourceUnit ru = mModel.GetResourceUnit(resourceIndex);
            if (ru == null)
            {
                throw new NotSupportedException(String.Format("addSingleTrees: invalid resource unit (index: {0}", resourceIndex));
            }
            int cnt = loader.LoadSingleTreeList(content, ru, "called_from_script");
            Debug.WriteLine("script: addSingleTrees: " + cnt + " trees loaded.");
            return cnt;
        }

        public int AddTrees(int resourceIndex, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            ResourceUnit ru = mModel.GetResourceUnit(resourceIndex);
            if (ru == null)
            {
                throw new NotSupportedException(String.Format("addTrees: invalid resource unit (index: {0}", resourceIndex));
            }
            return loader.LoadDistributionList(content, ru, 0, "called_from_script");
        }

        public int AddTreesOnMap(int standID, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            return loader.LoadDistributionList(content, null, standID, "called_from_script");
        }

        public bool StartOutput(string table_name)
        {
            if (table_name == "debug_dynamic")
            {
                GlobalSettings.Instance.ModelController.DynamicOutputEnabled = true;
                Debug.WriteLine("started dynamic debug output");
                return true;
            }
            if (table_name.StartsWith("debug_"))
            {
                DebugOutputs dbg = GlobalSettings.Instance.DebugOutputID(table_name.Substring(6));
                if (dbg == 0)
                {
                    Debug.WriteLine("cannot start debug output" + table_name + "because this is not a valid name.");
                }
                GlobalSettings.Instance.SetDebugOutput(dbg, true);
                return true;
            }
            OutputManager om = GlobalSettings.Instance.OutputManager;
            if (om == null)
            {
                return false;
            }
            Output output = om.Find(table_name);
            if (output == null)
            {
                string err = String.Format("startOutput: Output '{0}' is not a valid output.", table_name);
                // TODO: ERROR function in script
                //        if (context())
                //           context().throwError(err);
                Trace.TraceWarning(err);
                return false;
            }
            output.IsEnabled = true;
            Debug.WriteLine("started output " + table_name);
            return true;
        }

        public bool StopOutput(string table_name)
        {
            if (table_name == "debug_dynamic")
            {
                GlobalSettings.Instance.ModelController.DynamicOutputEnabled = false;
                Debug.WriteLine("stopped dynamic debug output.");
                return true;
            }
            if (table_name.StartsWith("debug_"))
            {
                DebugOutputs dbg = GlobalSettings.Instance.DebugOutputID(table_name.Substring(6));
                if (dbg == 0)
                {
                    Debug.WriteLine("cannot stop debug output" + table_name + "because this is not a valid name.");
                }
                GlobalSettings.Instance.SetDebugOutput(dbg, false);
                return true;
            }
            OutputManager om = GlobalSettings.Instance.OutputManager;
            if (om == null)
            {
                return false;
            }
            Output output = om.Find(table_name);
            if (output == null)
            {
                string err = String.Format("stopOutput: Output '{0}' is not a valid output.", table_name);
                Trace.TraceWarning(err);
                // TODO: ERROR function in script
                //        if (context())
                //           context().throwError(err);
                return false;
            }
            output.IsEnabled = false;
            Debug.WriteLine("stopped output " + table_name);
            return true;
        }

        public bool Screenshot(string file_name)
        {
            if (GlobalSettings.Instance.ModelController != null)
            {
                GlobalSettings.Instance.ModelController.SaveScreenshot(file_name);
            }
            return true;
        }

        public void Repaint()
        {
            if (GlobalSettings.Instance.ModelController != null)
            {
                GlobalSettings.Instance.ModelController.Repaint();
            }
        }

        public void SetViewport(double x, double y, double scale_px_per_m)
        {
            if (GlobalSettings.Instance.ModelController != null)
            {
                GlobalSettings.Instance.ModelController.SetViewport(new PointF((float)x, (float)y), scale_px_per_m);
            }
        }

        // helper function...
        public string HeightGridHeight(HeightGridValue hgv)
        {
            return hgv.Height.ToString();
        }

        /// write grid to a file...
        public bool GridToFile(string grid_type, string file_name)
        {
            if (GlobalSettings.Instance.Model == null)
            {
                return false;
            }
            string result = null;
            if (grid_type == "height")
            {
                result = core.Grid.ToEsriRaster(GlobalSettings.Instance.Model.HeightGrid, this.HeightGridHeight);
            }
            if (grid_type == "lif")
            {
                result = core.Grid.ToEsriRaster(GlobalSettings.Instance.Model.LightGrid);
            }

            if (String.IsNullOrEmpty(result))
            {
                file_name = GlobalSettings.Instance.Path(file_name);
                Helper.SaveToTextFile(file_name, result);
                Debug.WriteLine("saved grid to " + file_name);
                return true;
            }
            Debug.WriteLine("could not save gridToFile because " + grid_type + " is not a valid grid.");
            return false;
        }

        public QJSValue Grid(string type)
        {
            int index = -1;
            if (type == "height") index = 0;
            if (type == "valid") index = 1;
            if (type == "count") index = 2;
            if (type == "forestoutside") index = 3;
            if (index < 0)
            {
                Debug.WriteLine("grid(): error: invalid grid specified:" + type + ". valid options: 'height', 'valid', 'count', 'forestoutside'.");
            }

            Grid<HeightGridValue> h = GlobalSettings.Instance.Model.HeightGrid;
            Grid<double> dgrid = new Grid<double>(h.CellSize, h.SizeX, h.SizeY);
            // fetch data from height grid
            for (int hgv = 0; hgv < h.Count; ++hgv)
            {
                switch (index)
                {
                    case 0: dgrid[hgv] = h[hgv].Height; break;
                    case 1: dgrid[hgv] = h[hgv].IsValid() ? 1.0 : 0.0; break;
                    case 2: dgrid[hgv] = h[hgv].Count(); break;
                    case 3: dgrid[hgv] = h[hgv].IsForestOutside() ? 1.0 : 0.0; break;
                }
            }

            QJSValue g = ScriptGrid.CreateGrid(dgrid, type);
            return g;
        }

        public QJSValue SpeciesShareGrid(string species)
        {
            Species s = GlobalSettings.Instance.Model.SpeciesSet().GetSpecies(species);
            if (s == null)
            {
                Debug.WriteLine("speciesShareGrid: invalid species" + species);
                return new QJSValue();
            }
            Grid<ResourceUnit> rug = GlobalSettings.Instance.Model.ResourceUnitGrid;
            Grid<double> grid = new Grid<double>(rug.CellSize, rug.SizeX, rug.SizeY);
            for (int ru = 0; ru < rug.Count; ++ru)
            {
                if (rug[ru] != null && rug[ru].ResourceUnitSpecies(s) != null)
                {
                    grid[ru] = rug[ru].ResourceUnitSpecies(s).Statistics.BasalArea;
                }
                else
                {
                    grid[ru] = 0.0;
                }
            }
            QJSValue g = ScriptGrid.CreateGrid(grid, species);
            return g;
        }

        public QJSValue ResourceUnitGrid(string expression)
        {
            Grid<ResourceUnit> rug = GlobalSettings.Instance.Model.ResourceUnitGrid;
            Grid<double> grid = new Grid<double>(rug.CellSize, rug.SizeX, rug.SizeY);
            RUWrapper ru_wrap = new RUWrapper();
            Expression ru_value = new Expression(expression, ru_wrap);

            for (int ru = 0; ru != rug.Count; ++ru)
            {
                if (rug[ru] != null)
                {
                    ru_wrap.ResourceUnit = rug[ru];
                    double value = ru_value.Execute();
                    grid[ru] = value;
                }
                else
                {
                    grid[ru] = 0.0;
                }
            }
            QJSValue g = ScriptGrid.CreateGrid(grid, "ru");
            return g;
        }

        public bool SeedMapToFile(string species, string file_name)
        {
            // does not fully work:
            // Problem: after a full year cycle the seed maps are already cleared and prepared for the next round
            // -. this is now more an "occurence" map
            if (GlobalSettings.Instance.Model == null)
            {
                return false;
            }

            // find species
            Species s = GlobalSettings.Instance.Model.SpeciesSet().GetSpecies(species);
            if (s == null)
            {
                Debug.WriteLine("invalid species " + species + ". No seed map saved.");
                return false;
            }

            s.SeedDispersal.DumpNextYearFileName = file_name;
            Debug.WriteLine("creating raster in the next year cycle for species " + s.ID);
            return true;

            //gridToImage( s.seedDispersal().seedMap(), true, 0., 1.).save(GlobalSettings.instance().path(file_name));
            //    string result = gridToESRIRaster(s.seedDispersal().seedMap());
            //    if (!result.isEmpty()) {
            //        file_name = GlobalSettings.instance().path(file_name);
            //        Helper.saveToTextFile(file_name, result);
            //        Debug.WriteLine("saved grid to " + file_name;
            //        return true;
            //    }
            //    Debug.WriteLine("failed creating seed map";
            //    return false;
        }

        public void Wait(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }

        public int AddSaplingsOnMap(MapGridWrapper map, int mapID, string species, int px_per_hectare, double height, int age)
        {
            string csv_file = String.Format("species;count;height;age{4}{0};{1};{2};{3}", species, px_per_hectare, height, age, System.Environment.NewLine);
            StandLoader loader = new StandLoader(mModel)
            {
                CurrentMap = map.Map
            };
            return loader.LoadSaplings(csv_file, mapID);
        }

        /// saves a snapshot of the current model state (trees, soil, etc.)
        /// to a dedicated SQLite database.
        public bool SaveModelSnapshot(string file_name)
        {
            Snapshot shot = new Snapshot();
            string output_db = GlobalSettings.Instance.Path(file_name);
            return shot.CreateSnapshot(output_db);
        }

        /// loads a snapshot of the current model state (trees, soil, etc.)
        /// from a dedicated SQLite database.
        public bool LoadModelSnapshot(string file_name)
        {
            Snapshot shot = new Snapshot();
            string input_db = GlobalSettings.Instance.Path(file_name);
            return shot.LoadSnapshot(input_db);
        }

        public bool SaveStandSnapshot(int stand_id, string file_name)
        {
            Snapshot shot = new Snapshot();
            MapGrid map_grid = GlobalSettings.Instance.Model.StandGrid;
            if (map_grid == null)
            {
                return false;
            }
            return shot.SaveStandSnapshot(stand_id, map_grid, GlobalSettings.Instance.Path(file_name));
        }

        public bool LoadStandSnapshot(int stand_id, string file_name)
        {
            Snapshot shot = new Snapshot();
            MapGrid map_grid = GlobalSettings.Instance.Model.StandGrid;
            if (map_grid == null)
            {
                return false;
            }
            return shot.LoadStandSnapshot(stand_id, map_grid, GlobalSettings.Instance.Path(file_name));
        }

        public void ReloadAbe()
        {
            Debug.WriteLine("attempting to reload ABE");
            GlobalSettings.Instance.Model.ReloadAbe();
        }

        public void SetUIshortcuts(QJSValue shortcuts)
        {
            if (!shortcuts.IsObject())
            {
                Debug.WriteLine("setUIShortcuts: expected a JS-object (name: javascript-call, value: description). Got: " + shortcuts.ToString());
            }
            GlobalSettings.Instance.ModelController.SetUIShortcuts(shortcuts);
        }

        public void TestTreeMortality(double thresh, int years, double p_death)
        {
#if ALT_TREE_MORTALITY
        Tree.mortalityParams(thresh, years, p_death);
#else
            Debug.WriteLine("test_tree_mortality() not enabled!!");
#endif
        }

        // unused in C++
        //private void throwError(string errormessage)
        //{
        //    GlobalSettings.instance().scriptEngine().evaluate(String.Format("throw '{0}'", errormessage));
        //    Trace.TraceWarning("Scripterror:" + errormessage);
        //    // TODO: check if this works....
        //    // BUGBUG: doesn't throw
        //}

        public static void LoadScript(string fileName)
        {
            QJSEngine engine = GlobalSettings.Instance.ScriptEngine;

            string program = Helper.LoadTextFile(fileName);
            if (String.IsNullOrEmpty(program))
            {
                Debug.WriteLine("loading of Javascript file " + fileName + " failed because file is either missing or empty.");
                return;
            }

            QJSValue result = engine.Evaluate(program);
            Debug.WriteLine("javascript file loaded " + fileName);
            if (result.IsError())
            {
                int lineno = result.Property("lineNumber").ToInt();
                List<string> code_lines = program.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendFormat("{0}: {1} {2}{3}", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : "", System.Environment.NewLine);
                }
                Debug.WriteLine("Javascript Error in file " + fileName + ": " + result.Property("lineNumber") + " : " + result.ToString() + " : " + System.Environment.NewLine + code_part);
            }
        }

        public static string ExecuteScript(string cmd)
        {
            using DebugTimer t = new DebugTimer("execute javascript");
            QJSEngine engine = GlobalSettings.Instance.ScriptEngine;
            QJSValue result = new QJSValue();
            if (engine != null)
            {
                result = engine.Evaluate(cmd);
            }
            if (result.IsError())
            {
                //int line = mEngine.uncaughtExceptionLineNumber();
                string msg = String.Format("Script Error occured: {0}\n", result.ToString());
                Debug.WriteLine(msg);
                //msg+=engine.uncaughtExceptionBacktrace().join("\n");
                return msg;
            }
            else
            {
                return String.Empty;
            }
        }

        public static string ExecuteJSFunction(string function)
        {
            using DebugTimer t = new DebugTimer("execute javascript");
            QJSEngine engine = GlobalSettings.Instance.ScriptEngine;
            if (engine == null)
            {
                return "No valid javascript engine!";
            }

            QJSValue result;
            if (engine.GlobalObject().Property(function).IsCallable())
            {
                result = engine.GlobalObject().Property(function).Call();
                if (result.IsError())
                {
                    string msg = "Script Error occured: " + result.ToString();
                    Debug.WriteLine(msg);
                    return msg;
                }
            }
            return String.Empty; // BUGBUG: should return result or be void?
        }

        public string FormattedErrorMessage(QJSValue error_value, string sourcecode)
        {
            if (error_value.IsError())
            {
                int lineno = error_value.Property("lineNumber").ToInt();
                string code = sourcecode;
                List<string> code_lines = code.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendFormat("{0}: {1} {2}{3}", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : "", System.Environment.NewLine);
                }
                string error_string = String.Format("Javascript Error in file '{0}:{1}':{2}{4}{3}",
                        error_value.Property("fileName"), error_value.Property("lineNumber"), error_value.ToString(), code_part, System.Environment.NewLine);
                return error_string;
            }
            return String.Empty;
        }

        public QJSValue ViewOptions()
        {
            QJSValue res = new QJSValue();
            return res;
        }

        // unused in C++
        //public void setViewOptions(QJSValue opts)
        //{
        //    // no non-GUI code
        //}

        public static void SetupGlobalScripting()
        {
            QJSEngine engine = GlobalSettings.Instance.ScriptEngine;
            //    QJSValue dbgprint = engine.newFunction(script_debug);
            //    QJSValue sinclude = engine.newFunction(script_include);
            //    QJSValue alert = engine.newFunction(script_alert);
            //    engine.globalObject().setProperty("print",dbgprint);
            //    engine.globalObject().setProperty("include",sinclude);
            //    engine.globalObject().setProperty("alert", alert);

            // check if update necessary
            if (engine.GlobalObject().Property("print").IsCallable())
            {
                return;
            }

            // wrapper functions for (former) stand-alone javascript functions
            // Qt5 - modification
            engine.Evaluate("function print(x) { Globals.print(x); } \n" +
                             "function include(x) { Globals.include(x); } \n" +
                             "function alert(x) { Globals.alert(x); } \n");
            // add a (fake) console.log / console.print
            engine.Evaluate("var console = { log: function(x) {Globals.print(x); }, " +
                             "                print: function(x) { for(var propertyName in x)  " +
                             "                                       console.log(propertyName + ': ' + x[propertyName]); " +
                             "                                   } " +
                             "              }");


            ScriptObjectFactory factory = new ScriptObjectFactory();
            QJSValue obj = GlobalSettings.Instance.ScriptEngine.NewQObject(factory);
            engine.GlobalObject().SetProperty("Factory", obj);

            // other object types
            ClimateConverter.AddToScriptEngine(engine);
            CsvFile.AddToScriptEngine(engine);
            MapGridWrapper.AddToScriptEngine(engine);
            SpatialAnalysis.AddToScriptEngine();
        }

        public int Milliseconds() // BUGBUG: naming
        {
            return (int)(DateTime.Now - DateTime.Today).TotalMilliseconds;
        }
    }
}
