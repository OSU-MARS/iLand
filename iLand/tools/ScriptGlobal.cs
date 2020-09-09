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
        private string mCurrentDir;
        private Model mModel;

        public string currentDir() { return mCurrentDir; } ///< current execution directory (default is the Script execution directory)
        public void setCurrentDir(string newDir) { mCurrentDir = newDir; } ///< set current working dir

        public bool qt5() ///< is this the qt5-model? (changes in script object creation)
        {
            return true;
        } 

        public ScriptGlobal(object parent = null)
            {
                mModel = GlobalSettings.instance().model();
                // current directory
                if (mModel != null)
                {
                    mCurrentDir = GlobalSettings.instance().path(null, "script") + Path.DirectorySeparatorChar;
                }
            }

        public object setting(string key)
        {
            XmlHelper xml = GlobalSettings.instance().settings();
            if (!xml.hasNode(key))
            {
                Debug.WriteLine("scriptglobal: setting key " + key + "not valid.");
                return new object(); // undefined???
            }
            return xml.value(key);
        }

        public void set(string key, string value)
        {
            XmlHelper xml = GlobalSettings.instance().settings();
            if (!xml.hasNode(key))
            {
                Debug.WriteLine("scriptglobal: setting key " + key + "not valid.");
                return;
            }
            xml.setNodeValue(key, value);
        }

        public void print(string message)
        {
            Debug.WriteLine(message);
        }

        public void alert(string message)
        {
            Helper.msg(message); // nothing happens when not in GUI mode
        }

        public void include(string filename)
        {
            string path = GlobalSettings.instance().path(filename);
            if (!File.Exists(path))
            {
                throw new NotSupportedException(String.Format("include(): The javascript source file '{0}' could not be found.", path));
            }

            string includeFile = Helper.loadTextFile(path);

            QJSValue ret = GlobalSettings.instance().scriptEngine().evaluate(includeFile, path);
            if (ret.isError())
            {
                string error_message = formattedErrorMessage(ret, includeFile);
                Debug.WriteLine(error_message);
                throw new NotSupportedException("Error in javascript-include(): " + error_message);
            }
        }

        public string defaultDirectory(string dir)
        {
            string result = GlobalSettings.instance().path(null, dir) + Path.DirectorySeparatorChar;
            return result;
        }

        public string path(string filename)
        {
            return GlobalSettings.instance().path(filename);
        }

        public int year()
        {
            return GlobalSettings.instance().currentYear();
        }

        public int resourceUnitCount()
        {
            Debug.Assert(mModel != null);
            return mModel.ruList().Count;
        }

        public double worldX()
        {
            return GlobalSettings.instance().model().extent().Width;
        }

        public double worldY()
        {
            return GlobalSettings.instance().model().extent().Height;
        }

        // wrapped helper functions
        public string loadTextFile(string fileName)
        {
            return Helper.loadTextFile(GlobalSettings.instance().path(fileName));
        }

        public void saveTextFile(string fileName, string content)
        {
            Helper.saveToTextFile(fileName, content);
        }

        public bool fileExists(string fileName)
        {
            return File.Exists(fileName);
        }

        public void systemCmd(string filePath, string arguments)
        {
            Debug.WriteLine("running system command:" + filePath + " " + arguments);
            using Process process = new Process();
            process.StartInfo.FileName = filePath;
            process.StartInfo.Arguments = arguments;
            process.Start();
            process.WaitForExit(); // will wait forever until finished

            string res_stdout = process.StandardOutput.ReadToEnd();
            string res_stderr = process.StandardError.ReadToEnd(); // BUGBUG: potential deadlock
            Debug.WriteLine("result (stdout):" + res_stdout);
            Debug.WriteLine("result (stderr):" + res_stderr);
        }

        /// add trees on given resource unit
        /// @param content init file in a string (containing headers)
        /// @return number of trees added
        public int addSingleTrees(int resourceIndex, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            ResourceUnit ru = mModel.ru(resourceIndex);
            if (ru == null)
            {
                throw new NotSupportedException(String.Format("addSingleTrees: invalid resource unit (index: {0}", resourceIndex));
            }
            int cnt = loader.loadSingleTreeList(content, ru, "called_from_script");
            Debug.WriteLine("script: addSingleTrees: " + cnt + " trees loaded.");
            return cnt;
        }

        public int addTrees(int resourceIndex, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            ResourceUnit ru = mModel.ru(resourceIndex);
            if (ru == null)
            {
                throw new NotSupportedException(String.Format("addTrees: invalid resource unit (index: {0}", resourceIndex));
            }
            return loader.loadDistributionList(content, ru, 0, "called_from_script");
        }

        public int addTreesOnMap(int standID, string content)
        {
            StandLoader loader = new StandLoader(mModel);
            return loader.loadDistributionList(content, null, standID, "called_from_script");
        }

        public bool startOutput(string table_name)
        {
            if (table_name == "debug_dynamic")
            {
                GlobalSettings.instance().controller().setDynamicOutputEnabled(true);
                Debug.WriteLine("started dynamic debug output");
                return true;
            }
            if (table_name.StartsWith("debug_"))
            {
                DebugOutputs dbg = GlobalSettings.instance().debugOutputId(table_name.Substring(6));
                if (dbg == 0)
                {
                    Debug.WriteLine("cannot start debug output" + table_name + "because this is not a valid name.");
                }
                GlobalSettings.instance().setDebugOutput(dbg, true);
                return true;
            }
            OutputManager om = GlobalSettings.instance().outputManager();
            if (om == null)
            {
                return false;
            }
            Output output = om.find(table_name);
            if (output == null)
            {
                string err = String.Format("startOutput: Output '{0}' is not a valid output.", table_name);
                // TODO: ERROR function in script
                //        if (context())
                //           context().throwError(err);
                Trace.TraceWarning(err);
                return false;
            }
            output.setEnabled(true);
            Debug.WriteLine("started output " + table_name);
            return true;
        }

        public bool stopOutput(string table_name)
        {
            if (table_name == "debug_dynamic")
            {
                GlobalSettings.instance().controller().setDynamicOutputEnabled(false);
                Debug.WriteLine("stopped dynamic debug output.");
                return true;
            }
            if (table_name.StartsWith("debug_"))
            {
                DebugOutputs dbg = GlobalSettings.instance().debugOutputId(table_name.Substring(6));
                if (dbg == 0)
                {
                    Debug.WriteLine("cannot stop debug output" + table_name + "because this is not a valid name.");
                }
                GlobalSettings.instance().setDebugOutput(dbg, false);
                return true;
            }
            OutputManager om = GlobalSettings.instance().outputManager();
            if (om == null)
            {
                return false;
            }
            Output output = om.find(table_name);
            if (output == null)
            {
                string err = String.Format("stopOutput: Output '{0}' is not a valid output.", table_name);
                Trace.TraceWarning(err);
                // TODO: ERROR function in script
                //        if (context())
                //           context().throwError(err);
                return false;
            }
            output.setEnabled(false);
            Debug.WriteLine("stopped output " + table_name);
            return true;
        }

        public bool screenshot(string file_name)
        {
            if (GlobalSettings.instance().controller() != null)
            {
                GlobalSettings.instance().controller().saveScreenshot(file_name);
            }
            return true;
        }

        public void repaint()
        {
            if (GlobalSettings.instance().controller() != null)
            {
                GlobalSettings.instance().controller().repaint();
            }
        }

        public void setViewport(double x, double y, double scale_px_per_m)
        {
            if (GlobalSettings.instance().controller() != null)
            {
                GlobalSettings.instance().controller().setViewport(new PointF((float)x, (float)y), scale_px_per_m);
            }
        }

        // helper function...
        public string heightGrid_height(HeightGridValue hgv)
        {
            return hgv.height.ToString();
        }

        /// write grid to a file...
        public bool gridToFile(string grid_type, string file_name)
        {
            if (GlobalSettings.instance().model() == null)
            {
                return false;
            }
            string result = null;
            if (grid_type == "height")
            {
                result = Grid.gridToESRIRaster(GlobalSettings.instance().model().heightGrid(), heightGrid_height);
            }
            if (grid_type == "lif")
            {
                result = Grid.gridToESRIRaster(GlobalSettings.instance().model().grid());
            }

            if (String.IsNullOrEmpty(result))
            {
                file_name = GlobalSettings.instance().path(file_name);
                Helper.saveToTextFile(file_name, result);
                Debug.WriteLine("saved grid to " + file_name);
                return true;
            }
            Debug.WriteLine("could not save gridToFile because " + grid_type + " is not a valid grid.");
            return false;
        }

        public QJSValue grid(string type)
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

            Grid<HeightGridValue> h = GlobalSettings.instance().model().heightGrid();
            Grid<double> dgrid = new Grid<double>(h.cellsize(), h.sizeX(), h.sizeY());
            // fetch data from height grid
            for (int hgv = 0; hgv < h.count(); ++hgv)
            {
                switch (index)
                {
                    case 0: dgrid[hgv] = h[hgv].height; break;
                    case 1: dgrid[hgv] = h[hgv].isValid() ? 1.0 : 0.0; break;
                    case 2: dgrid[hgv] = h[hgv].count(); break;
                    case 3: dgrid[hgv] = h[hgv].isForestOutside() ? 1.0 : 0.0; break;
                }
            }

            QJSValue g = ScriptGrid.createGrid(dgrid, type);
            return g;
        }

        public QJSValue speciesShareGrid(string species)
        {
            Species s = GlobalSettings.instance().model().speciesSet().species(species);
            if (s == null)
            {
                Debug.WriteLine("speciesShareGrid: invalid species" + species);
                return new QJSValue();
            }
            Grid<ResourceUnit> rug = GlobalSettings.instance().model().RUgrid();
            Grid<double> grid = new Grid<double>(rug.cellsize(), rug.sizeX(), rug.sizeY());
            for (int ru = 0; ru < rug.count(); ++ru)
            {
                if (rug[ru] != null && rug[ru].constResourceUnitSpecies(s) != null)
                {
                    grid[ru] = rug[ru].resourceUnitSpecies(s).statistics().basalArea();
                }
                else
                {
                    grid[ru] = 0.0;
                }
            }
            QJSValue g = ScriptGrid.createGrid(grid, species);
            return g;
        }

        public QJSValue resourceUnitGrid(string expression)
        {
            Grid<ResourceUnit> rug = GlobalSettings.instance().model().RUgrid();
            Grid<double> grid = new Grid<double>(rug.cellsize(), rug.sizeX(), rug.sizeY());
            RUWrapper ru_wrap = new RUWrapper();
            Expression ru_value = new Expression(expression, ru_wrap);

            for (int ru = 0; ru != rug.count(); ++ru)
            {
                if (rug[ru] != null)
                {
                    ru_wrap.setResourceUnit(rug[ru]);
                    double value = ru_value.execute();
                    grid[ru] = value;
                }
                else
                {
                    grid[ru] = 0.0;
                }
            }
            QJSValue g = ScriptGrid.createGrid(grid, "ru");
            return g;
        }

        public bool seedMapToFile(string species, string file_name)
        {
            // does not fully work:
            // Problem: after a full year cycle the seed maps are already cleared and prepared for the next round
            // -. this is now more an "occurence" map
            if (GlobalSettings.instance().model() == null)
            {
                return false;
            }

            // find species
            Species s = GlobalSettings.instance().model().speciesSet().species(species);
            if (s == null)
            {
                Debug.WriteLine("invalid species " + species + ". No seed map saved.");
                return false;
            }

            s.seedDispersal().dumpMapNextYear(file_name);
            Debug.WriteLine("creating raster in the next year cycle for species " + s.id());
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

        public void wait(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }

        public int addSaplingsOnMap(MapGridWrapper map, int mapID, string species, int px_per_hectare, double height, int age)
        {
            string csv_file = String.Format("species;count;height;age{4}{0};{1};{2};{3}", species, px_per_hectare, height, age, System.Environment.NewLine);
            StandLoader loader = new StandLoader(mModel);
            loader.setMap(map.map());
            return loader.loadSaplings(csv_file, mapID, "called from script");
        }

        /// saves a snapshot of the current model state (trees, soil, etc.)
        /// to a dedicated SQLite database.
        public bool saveModelSnapshot(string file_name)
        {
            Snapshot shot = new Snapshot();
            string output_db = GlobalSettings.instance().path(file_name);
            return shot.createSnapshot(output_db);
        }

        /// loads a snapshot of the current model state (trees, soil, etc.)
        /// from a dedicated SQLite database.
        public bool loadModelSnapshot(string file_name)
        {
            Snapshot shot = new Snapshot();
            string input_db = GlobalSettings.instance().path(file_name);
            return shot.loadSnapshot(input_db);
        }

        public bool saveStandSnapshot(int stand_id, string file_name)
        {
            Snapshot shot = new Snapshot();
            MapGrid map_grid = GlobalSettings.instance().model().standGrid();
            if (map_grid == null)
            {
                return false;
            }
            return shot.saveStandSnapshot(stand_id, map_grid, GlobalSettings.instance().path(file_name));
        }

        public bool loadStandSnapshot(int stand_id, string file_name)
        {
            Snapshot shot = new Snapshot();
            MapGrid map_grid = GlobalSettings.instance().model().standGrid();
            if (map_grid == null)
            {
                return false;
            }
            return shot.loadStandSnapshot(stand_id, map_grid, GlobalSettings.instance().path(file_name));
        }

        public void reloadABE()
        {
            Debug.WriteLine("attempting to reload ABE");
            GlobalSettings.instance().model().reloadABE();
        }

        public void setUIshortcuts(QJSValue shortcuts)
        {
            if (!shortcuts.isObject())
            {
                Debug.WriteLine("setUIShortcuts: expected a JS-object (name: javascript-call, value: description). Got: " + shortcuts.toString());
            }
            GlobalSettings.instance().controller().setUIShortcuts(shortcuts);
        }

        public void test_tree_mortality(double thresh, int years, double p_death)
        {
#if ALT_TREE_MORTALITY
        Tree.mortalityParams(thresh, years, p_death);
#else
            Debug.WriteLine("test_tree_mortality() not enabled!!");
#endif
        }

        private void throwError(string errormessage)
        {
            GlobalSettings.instance().scriptEngine().evaluate(String.Format("throw '{0}'", errormessage));
            Trace.TraceWarning("Scripterror:" + errormessage);
            // TODO: check if this works....
            // BUGBUG: doesn't throw
        }

        public static void loadScript(string fileName)
        {
            QJSEngine engine = GlobalSettings.instance().scriptEngine();

            string program = Helper.loadTextFile(fileName);
            if (String.IsNullOrEmpty(program))
            {
                Debug.WriteLine("loading of Javascript file " + fileName + " failed because file is either missing or empty.");
                return;
            }

            QJSValue result = engine.evaluate(program);
            Debug.WriteLine("javascript file loaded " + fileName);
            if (result.isError())
            {
                int lineno = result.property("lineNumber").toInt();
                List<string> code_lines = program.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendFormat("{0}: {1} {2}{3}", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : "", System.Environment.NewLine);
                }
                Debug.WriteLine("Javascript Error in file " + fileName + ": " + result.property("lineNumber") + " : " + result.toString() + " : " + System.Environment.NewLine + code_part);
            }
        }

        public static string executeScript(string cmd)
        {
            using DebugTimer t = new DebugTimer("execute javascript");
            QJSEngine engine = GlobalSettings.instance().scriptEngine();
            QJSValue result = new QJSValue();
            if (engine != null)
            {
                result = engine.evaluate(cmd);
            }
            if (result.isError())
            {
                //int line = mEngine.uncaughtExceptionLineNumber();
                string msg = String.Format("Script Error occured: {0}\n", result.toString());
                Debug.WriteLine(msg);
                //msg+=engine.uncaughtExceptionBacktrace().join("\n");
                return msg;
            }
            else
            {
                return String.Empty;
            }
        }

        public static string executeJSFunction(string function)
        {
            using DebugTimer t = new DebugTimer("execute javascript");
            QJSEngine engine = GlobalSettings.instance().scriptEngine();
            if (engine == null)
            {
                return "No valid javascript engine!";
            }

            QJSValue result;
            if (engine.globalObject().property(function).isCallable())
            {
                result = engine.globalObject().property(function).call();
                if (result.isError())
                {
                    string msg = "Script Error occured: " + result.toString();
                    Debug.WriteLine(msg);
                    return msg;
                }
            }
            return String.Empty; // BUGBUG: should return result or be void?
        }

        public string formattedErrorMessage(QJSValue error_value, string sourcecode)
        {
            if (error_value.isError())
            {
                int lineno = error_value.property("lineNumber").toInt();
                string code = sourcecode;
                List<string> code_lines = code.Replace("\r", "").Split('\n').ToList(); // remove CR, split by LF
                StringBuilder code_part = new StringBuilder();
                for (int i = Math.Max(0, lineno - 5); i < Math.Min(lineno + 5, code_lines.Count); ++i)
                {
                    code_part.AppendFormat("{0}: {1} {2}{3}", i, code_lines[i], i == lineno ? "  <---- [ERROR]" : "", System.Environment.NewLine);
                }
                string error_string = String.Format("Javascript Error in file '{0}:{1}':{2}{4}{3}",
                        error_value.property("fileName"), error_value.property("lineNumber"), error_value.toString(), code_part, System.Environment.NewLine);
                return error_string;
            }
            return String.Empty;
        }

        public QJSValue viewOptions()
        {
            QJSValue res = new QJSValue();
            return res;
        }

        public void setViewOptions(QJSValue opts)
        {
            // no non-GUI code
        }

        public static void setupGlobalScripting()
        {
            QJSEngine engine = GlobalSettings.instance().scriptEngine();
            //    QJSValue dbgprint = engine.newFunction(script_debug);
            //    QJSValue sinclude = engine.newFunction(script_include);
            //    QJSValue alert = engine.newFunction(script_alert);
            //    engine.globalObject().setProperty("print",dbgprint);
            //    engine.globalObject().setProperty("include",sinclude);
            //    engine.globalObject().setProperty("alert", alert);

            // check if update necessary
            if (engine.globalObject().property("print").isCallable())
            {
                return;
            }

            // wrapper functions for (former) stand-alone javascript functions
            // Qt5 - modification
            engine.evaluate("function print(x) { Globals.print(x); } \n" +
                             "function include(x) { Globals.include(x); } \n" +
                             "function alert(x) { Globals.alert(x); } \n");
            // add a (fake) console.log / console.print
            engine.evaluate("var console = { log: function(x) {Globals.print(x); }, " +
                             "                print: function(x) { for(var propertyName in x)  " +
                             "                                       console.log(propertyName + ': ' + x[propertyName]); " +
                             "                                   } " +
                             "              }");


            ScriptObjectFactory factory = new ScriptObjectFactory();
            QJSValue obj = GlobalSettings.instance().scriptEngine().newQObject(factory);
            engine.globalObject().setProperty("Factory", obj);

            // other object types
            ClimateConverter.addToScriptEngine(engine);
            CSVFile.addToScriptEngine(engine);
            MapGridWrapper.addToScriptEngine(engine);
            SpatialAnalysis.addToScriptEngine();
        }

        public int msec() // BUGBUG: naming
        {
            return (int)(DateTime.Now - DateTime.Today).TotalMilliseconds;
        }
    }
}
