using iLand.core;
using iLand.output;
using Microsoft.Collections.Extensions;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

/** @class GlobalSettings
  This class contains various global structures/definitions. This class is a Singleton and accessed via the static instance() function.
  @par various (textual) meta data (SettingMetaData)

  @par global database connections
  There are two defined global database connections dbin() and dbout() with the names "in" and "out".
  They are setup with setupDatabaseConnection(). Currently, only SQLite DBs are supported.
  Use dbin() and dbout() to faciliate those database connections:
  @code
  ...
  QSqlQuery query(GlobalSettings::instance()->dbin());
  query.exec(...);
  ...
  @endcode

  @par Helpers with file Paths
  the actual project file is parsed for path defined in the <path> section.
  Use the path() function to expand a @p fileName to a iLand-Path. To check if a file exists, you could
  use fileExists().
  Available paths:
  - home: the project's home directory. All other directories can be defined relative to this dir.
  - lip: path for the storage of LIP (aka binary Stamp files) (default: home/lip)
  - database: base path for SQLite database files (default: home/database)
  - temp: path for storage of temporary files (default: home/temp)
  - log: storage for log-files (default: home/log)
  - exe: the path to the executable file.
  @code
  // home is "e:/iland/test", temp is "c:\temp" and log is omitted in project-file:
  QString p;
  p = Globals->path("somestuff.txt", "temp"); // > c:\temp\somestuff.txt
  p = Globals->path("e:\averyspecial\place.txt", "temp"); // -> e:\averyspecial\place.txt
                                                          //    (abs. path is not changed)
  p = Globals->path("log123.txt", "log"); // -> e:/iland/test/log/log123.txt (default for log)
  @endcode

  @par Fine-Grained debugging outputs
  The enumeration DebugOutputs defines a list of realms (uses binary notation: 1,2,4,8,...!).
  Use setDebugOutput() to enable/disable such an output. Use isDebugEnabled() to test inside the
  code if the generation of debug output for a specific type is enabled. Internally, this is a single
  bitwise operation which is very fast.
  Call debugLists() to retrieve a list of lists of data that fit specific criteria.
  @code
    // use something like that somewhere in a tree-growth-related routine:
    DBGMODE(
       if (GlobalSettings::instance()->isDebugEnabled(GlobalSettings::dTreeGrowth) {
            List<object> &out = GlobalSettings::instance()->debugList(mId, GlobalSettings::dTreeGrowth); // get a ref to the list
            out + hd_growth + factor_diameter + delta_d_estimate + d_increment;   // fill with data
       }
    ); // only in debugmode
  @endcode

*/
namespace iLand.tools
{
    internal class GlobalSettings : IDisposable
    {
        // storing the names of debug outputs
        //    enum DebugOutputs { dTreeNPP=1, dTreePartition=2, dTreeGrowth=4,
        // dStandNPP=8, dWaterCycle=16, dDailyResponses=32, dEstablishment=64, dCarbonCycle=128 }; ///< defines available debug output types.
        private static readonly ReadOnlyCollection<string> debug_output_names = new List<string>() { "treeNPP", "treePartition", "treeGrowth", "waterCycle", "dailyResponse", "establishment", "carbonCycle", "performance" }.AsReadOnly();
        private static GlobalSettings mInstance = null;

        private SqliteConnection databaseClimate;
        private SqliteConnection databaseIn;
        private SqliteConnection databaseOut;
        private bool isDisposed;
        private int _loglevel = 0;
        private Model mModel;
        private ModelController mModelController;
        private OutputManager mOutputManager;
        private QJSEngine mScriptEngine;
        private int mRunYear;
        private SystemStatistics mSystemStatistics;

        // special debug outputs
        private MultiValueDictionary<int, List<object>> mDebugLists;
        private uint mDebugOutputs; // "bitmap" of enabled debugoutputs.

        private Dictionary<string, SettingMetaData> mSettingMetaData; ///< storage container (QHash) for settings.
        private Dictionary<string, string> mFilePath; ///< storage for file paths

        private XmlHelper mXml;

        ///< xml-based hierarchical settings

        private GlobalSettings()
        {
            mDebugOutputs = 0;
            mModel = null;
            mModelController = null;
            mSystemStatistics = new SystemStatistics();
            // create output manager
            mOutputManager = new OutputManager();
            mScriptEngine = null;
        }

        // database access functions
        public SqliteConnection dbclimate() { return this.databaseClimate; }
        public SqliteConnection dbin() { return this.databaseIn; }
        public SqliteConnection dbout() { return this.databaseOut; }
        public SqliteConnection dbsnapshot() { throw new NotImplementedException(); }
        public SqliteConnection dbsnapshotstand() { throw new NotImplementedException(); }

        // singleton-access
        public static GlobalSettings instance() { if (mInstance != null) return mInstance; mInstance = new GlobalSettings(); return mInstance; }

        // Access
        // model and clock
        public ModelController controller() { return mModelController; }
        public void setModelController(ModelController mc) { mModelController = mc; }

        public int currentDebugOutput() { return (int)mDebugOutputs; }
        public int currentYear() { return mRunYear; }
        public void setCurrentYear(int year) { mRunYear = year; }

        public Model model() { return mModel; }
        public void setModel(Model model) { mModel = model; }

        public bool isDebugEnabled(DebugOutputs dbg) { return (dbg != 0) & (mDebugOutputs != 0); } ///< returns true, if a specific debug outut type is enabled.

        // output manager
        public OutputManager outputManager() { return mOutputManager; }

        public QJSEngine scriptEngine() { return mScriptEngine; }
        public List<string> settingNames() { return mSettingMetaData.Keys.ToList(); } ///< retrieve list of all names of settings.
        // xml project file
        public XmlHelper settings() { return mXml; }
        // system statistics
        public SystemStatistics systemStatistics() { return mSystemStatistics; }

        private string childText(XmlNode elem, string name, string defaultTest = "")
        {
            foreach (XmlNode node in elem.SelectNodes(name))
            {
                return node.InnerText;
            }
            return defaultTest;
        }

        public void clearDebugLists() ///< clear all debug data
        {
            mDebugLists.Clear();
        }

        private void dbg_helper(string where, string what, string file, int line)
        {
            Debug.WriteLine("Warning in " + where + ":" + what + ". (file: " + file + "line:" + line);
        }
        private void dbg_helper_ext(string where, string what, string file, int line, string s)
        {
            Debug.WriteLine("Warning in " + where + ":" + what + ". (file: " + file + "line:" + line + "more:" + s);
        }

        public List<string> debugDataTable(DebugOutputs type, string separator, string fileName = null) ///< output for all available items (trees, ...) in table form
        {
            GlobalSettings g = GlobalSettings.instance();
            List<List<object>> ddl = g.debugLists(-1, type); // get all debug data

            List<string> result = new List<string>();
            if (ddl.Count < 1)
            {
                return result;
            }

            FileStream out_file = null;
            TextWriter ts = null;
            if (!String.IsNullOrWhiteSpace(fileName))
            {
                out_file = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                ts = new StreamWriter(out_file);
                ts.WriteLine(String.Join(separator, g.debugListCaptions(type)));
            }

            try
            {
                for (int i = ddl.Count - 1; i >= 0; --i)
                {
                    StringBuilder line = new StringBuilder();
                    int c = 0;
                    foreach (string value in ddl[i])
                    {
                        if (c++ != 0)
                        {
                            line.Append(separator);
                        }
                        line.Append(value.ToString());
                    }
                    if (ts != null)
                    {
                        ts.WriteLine(line);
                    }
                    else
                    {
                        result.Add(line.ToString());
                    }
                }
            }
            finally
            {
                if (ts != null)
                {
                    ts.Dispose();
                }
            }

            if (result.Count > 0)
            {
                // TODO: hoist this
                result.Insert(0, String.Join(separator, g.debugListCaptions(type)));
            }
            return result;
        }

        public DebugOutputs debugOutputId(string debug_name) ///< returns the DebugOutputs bit or 0 if not found
        {
            int index = debug_output_names.IndexOf(debug_name);
            if (index == -1) return default;
            return (DebugOutputs)(2 << index); // 1,2,4,8, ...
        }

        public List<object> debugList(int ID, DebugOutputs dbg) ///< returns a ref to a list ready to be filled with debug output of a type/id combination.
        {
            // serialize creation of debug outputs
            lock (mDebugLists)
            {
                List<object> dbglist = new List<object>() { ID , dbg, currentYear() };
                int id = ID;
                // use negative values for debug-outputs on RU - level
                // Note: at some point we will also have to handle RUS-level...
                if (dbg == DebugOutputs.dEstablishment || dbg == DebugOutputs.dCarbonCycle || dbg == DebugOutputs.dSaplingGrowth)
                {
                    id = -id;
                }
                mDebugLists.Add(id, dbglist);
                return dbglist;
            }
        }

        private int debuglist_sorter(List<object> i, List<object> j)
        {
            // TODO: implement less fragile compare
            return Comparer<int>.Default.Compare((int)i[0], (int)j[0]);
        }

        public List<string> debugListCaptions(DebugOutputs dbg) ///< returns stringlist of captions for a specific output type
        {
            List<string> treeCaps = new List<string>() { "Id", "Species", "Dbh", "Height", "x", "y", "ru_index", "LRI", "mWoody", "mRoot", "mFoliage", "LA" };
            if (dbg == 0)
            {
                return treeCaps;
            }

            // TODO: what if multiple flags are set?
            switch (dbg)
            {
                case DebugOutputs.dTreeNPP:
                    List<string> treeNpp = new List<string>() { "id", "type", "year" };
                    treeNpp.AddRange(treeCaps);
                    treeNpp.AddRange(new string[] { "LRI_modRU", "lightResponse", "effective_area", "raw_gpp", "gpp", "npp", "aging" });
                    return treeNpp;
                case DebugOutputs.dTreeGrowth:
                    List<string> treeGrowth = new List<string>() { "id", "type", "year" };
                    treeGrowth.AddRange(treeCaps);
                    treeGrowth.AddRange(new string[] { "netNPPStem", "massStemOld", "hd_growth", "factor_diameter", "delta_d_estimate", "d_increment" });
                    return treeGrowth;
                case DebugOutputs.dTreePartition:
                    List<string> treePartition = new List<string>() { "id", "type", "year" };
                    treePartition.AddRange(treeCaps);
                    treePartition.AddRange(new string[] { "npp_kg", "apct_foliage", "apct_wood", "apct_root", "delta_foliage", "delta_woody", "delta_root", "mNPPReserve", "netStemInc", "stress_index" });
                    return treePartition;
                case DebugOutputs.dStandGPP:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid", "lai_factor", "gpp_kg_m2", "gpp_kg", "avg_aging", "f_env_yr" };
                case DebugOutputs.dWaterCycle:
                    return new List<string>() { "id", "type", "year", "date", "ruindex", "rid", "temp", "vpd", "prec", "rad", "combined_response"
                                        , "after_intercept", "after_snow", "et_canopy", "evapo_intercepted"
                                        , "content", "psi_kpa", "excess_mm", "snow_height" };
                case DebugOutputs.dDailyResponses:
                    return new List<string>() { "id", "type", "year", "species", "date", "RU_index", "rid"
                                        , "waterResponse", "tempResponse", "VpdResponse", "Radiation of day", "util.Radiation" };
                case DebugOutputs.dEstablishment:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid"
                                        , "avgProbDensity", "TACAminTemp", "TACAchill", "TACAfrostFree", "TACAgdd", "TACAFrostAfterBud", "waterLimitation", "TACAAbioticEnv"
                                        , "fEnvYr", "N_Established" };
                case DebugOutputs.dSaplingGrowth:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid"
                                        , "Living_cohorts", "averageHeight", "averageAge", "avgDeltaHPot", "avgDeltaHRealized"
                                        , "Added", "Died", "Recruited", "refRatio" };
                case DebugOutputs.dCarbonCycle:
                    return new List<string>() { "id", "type", "year", "RU_index", "rid"
                                        , "SnagState_c", "TotalC_in", "TotalC_toAtm", "SWDtoDWD_c", "SWDtoDWD_n", "toLabile_c", "toLabile_n", "toRefr_c", "toRefr_n"
                                        , "swd1_c", "swd1_n", "swd1_count", "swd1_tsd", "toSwd1_c", "toSwd1_n", "dbh1", "height1", "volume1"  // pool of small dbhs
                                        , "swd2_c", "swd2_n", "swd2_count", "swd2_tsd", "toSwd2_c", "toSwd2_n", "dbh2", "height2", "volume2"   // standing woody debris medium dbhs
                                        , "swd3_c", "swd3_n", "swd3_count", "swd3_tsd", "toSwd3_c", "toSwd3_n", "dbh3", "height3", "volume3"   // large trees
                                        , "otherWood1_c", "otherWood1_n", "otherWood2_c", "otherWood2_n", "otherWood3_c", "otherWood3_n", "otherWood4_c", "otherWood4_n", "otherWood5_c", "otherWood5_n"
                                        , "iLabC", "iLabN", "iKyl", "iRefC", "iRefN", "iKyr", "re", "kyl", "kyr", "ylC", "ylN", "yrC", "yrN", "somC", "somN"
                                        , "NAvailable", "NAVLab", "NAVRef", "NAVSom" };
                case DebugOutputs.dPerformance:
                    return new List<string>() { "id", "type", "year", "treeCount", "saplingCount", "newSaplings", "management"
                                        , "applyPattern", "readPattern", "treeGrowth", "seedDistribution", "establishment", "saplingGrowth", "carbonCycle"
                                        , "writeOutput", "totalYear" };
                default:
                    return new List<string>() { "invalid debug output!" };
            }
        }

        public List<List<object>> debugLists(int ID, DebugOutputs dbg) ///< return a list of debug outputs
        {
            List<List<object>> result_list = new List<List<object>>();
            if (ID == -1)
            {
                foreach (List<object> list in mDebugLists.Values)
                {
                    if (list.Count > 2)  // contains data TODO: should this be zero?
                    {
                        if ((int)dbg == -1 || ((uint)list[1] & (uint)dbg) != 0) // type fits or is -1 for all
                        {
                            result_list.Add(list);
                        }
                    }
                }
            }
            else
            {
                // search a specific id
                foreach (List<object> list in mDebugLists[ID])
                {
                    if (list.Count > 2)  // contains data TODO: should this be zero
                        if ((int)dbg == -1 || ((uint)list[1] & (uint)dbg) != 0) // type fits or is -1 for all
                            result_list.Add(list);
                }
            }
            // sort result list
            //std::sort(result_list.begin(), result_list.end(), debuglist_sorter); // changed because of compiler warnings
            result_list.Sort(debuglist_sorter);
            return result_list;
        }

        public string debugOutputName(DebugOutputs d) ///< returns the name attached to 'd' or an empty string if not found
        {
            // this is a little hacky...(and never really tried!)
            for (int index = 0; index < debug_output_names.Count; ++index)
            {
                if (((int)d & (2 << index)) != 0)
                    return debug_output_names[index];
            }
            throw new NotSupportedException();
        }

        public List<Tuple<string, object>> debugValues(int ID) ///< all debug values for object with given ID
        {
            List<Tuple<string, object>> result = new List<Tuple<string, object>>();
            foreach (List<object> list in mDebugLists[ID])
            {
                if (list.Count > 2) // TODO: should this be zero?
                { // contains data
                    List<string> cap = debugListCaptions((DebugOutputs)list[1]);
                    result.Add(new Tuple<string, object>("Debug data", "Debug data"));
                    int first_index = 3;
                    if (String.Equals((string)list[3], "Id", StringComparison.Ordinal))  // skip default data fields (not needed for drill down)
                    {
                        first_index = 14;
                    }
                    for (int i = first_index; i < list.Count; ++i)
                    {
                        result.Add(new Tuple<string, object>(cap[i], list[i]));
                    }
                }
            }
            return result;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    // meta data... really clear ressources...
                    //qDeleteAll(mSettingMetaData.values());
                    //delete mSystemStatistics;
                    //mInstance = NULL;
                    //delete mOutputManager;
                    // clear all databases
                    clearDatabaseConnections();
                    //if (mScriptEngine)
                    //    delete mScriptEngine;
                }

                isDisposed = true;
            }
        }

        /// access the global QScriptEngine used throughout the model
        /// for all Javascript related functionality.
        public string executeJavascript(string command)
        {
            return ScriptGlobal.executeScript(command);
        }

        /// execute a javasript function in the global context
        public string executeJSFunction(string function_name)
        {
            return ScriptGlobal.executeJSFunction(function_name);
        }

        // path and directory
        public bool fileExists(string fileName, string type = "home")
        {
            // TODO: review use since semantics here seem confused
            string name = Path.Combine(fileName, type);

            if (!Directory.Exists(name)) // TODO: also try File.Exists()
            {
                Debug.WriteLine("Path " + fileName + " (expanded to:) " + name + " does not exist!");
                return false;
            }
            return true;
        }

        public string path(string fileName, string type = "home")
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                if (Path.IsPathRooted(fileName))
                {
                    // canonicalize path
                    return Path.GetFullPath(fileName);
                }
            }

            DirectoryInfo d;
            if (mFilePath.TryGetValue(type, out string directoryPath))
            {
                d = new DirectoryInfo(directoryPath);
            }
            else
            {
                Debug.WriteLine("GlobalSettings::path() called with unknown type " + type);
                d = new DirectoryInfo(System.Environment.CurrentDirectory);
            }

            return d.FullName;
        }

        // xml project settings
        public void loadProjectFile(string fileName)
        {
            Debug.WriteLine("Loading Project file " + fileName);
            if (File.Exists(fileName) == false)
            {
                throw new ArgumentException(String.Format("The project file {0} does not exist!", fileName), nameof(fileName));
            }
            mXml.loadFromFile(fileName);
            setupDirectories(mXml.node("system.path"), new FileInfo(fileName).FullName);
        }

        // meta data of settings
        public void loadSettingsMetaDataFromFile(string fileName)
        {
            // TODO; apparently a no op?
            string metadata = Helper.loadTextFile(fileName);
        }

        /** Load setting meta data from a piece of XML.
            @p topNode is a XML node, that contains the "setting" nodes as childs:
            @code
            <topnode>
            <setting>...</setting>
            <setting>...</setting>
            ...
            </topnode>
            @endcode
          */
        public void loadSettingsMetaDataFromXml(XmlElement topNode)
        {
            mSettingMetaData.Clear();
            if (topNode == null)
            {
                Debug.WriteLine("GlobalSettings::loadSettingsMetaDataFromXml():: no globalsettings section!");
                return;
            }

            for (XmlNode elt = topNode.SelectSingleNode("setting"); elt != null; elt = elt.NextSibling)
            {
                string settingName = elt.Attributes["name"].Value;
                if (mSettingMetaData.ContainsKey(settingName))
                {
                    throw new NotSupportedException();
                }

                SettingMetaData md = new SettingMetaData(SettingMetaData.typeFromName(elt.Attributes["type"].Value), // type
                              settingName, // name
                              childText(elt, "description"), // description
                              childText(elt, "url"), // url
                              childText(elt, "default"));
                mSettingMetaData[settingName] = md;

                Debug.WriteLine(md.dump());
                //mSettingMetaData[settingName].dump();
            }
            Debug.WriteLine("setup settingmetadata complete." + mSettingMetaData.Count + "items loaded.");
        }

        // Database connections
        public void clearDatabaseConnections() ///< shutdown and clear connections
        {
            this.dbin().Dispose();
            this.dbout().Dispose();
            this.dbclimate().Dispose();
        }

        public bool setupDatabaseConnection(string dbname, string fileName, bool fileMustExist)
        {
            if ((String.Equals(dbname, "in", StringComparison.Ordinal) == false) &&
                (String.Equals(dbname, "out", StringComparison.Ordinal) == false) &&
                (String.Equals(dbname, "climate", StringComparison.Ordinal) == false))
            {
                throw new ArgumentOutOfRangeException(nameof(dbname));
            }

            // TODO: check if database is already open
            //QSqlDatabase::database(dbname).close(); // close database
            SqliteConnection db = new SqliteConnection(dbname);
            Trace.WriteLine("setup database connection " + dbname + " to " + fileName);
            if (fileMustExist)
            {
                if (File.Exists(fileName) == false)
                {
                    throw new ArgumentException("Error setting up database connection: file " + fileName + " does not exist!", nameof(fileName));
                }
            }
            db.Open();
            if (!fileMustExist)
            {
                // for output databases:
                // some special commands (pragmas: see also: http://www.sqlite.org/pragma.html)
                using (SqliteTransaction transaction = db.BeginTransaction())
                {
                    SqliteCommand tempStore = new SqliteCommand("pragma temp_store(2)", db, transaction); // temp storage in memory
                    // for now, use default Sqlite synchronization
                    // db.exec("pragma synchronous(1)"); // medium synchronization between memory and disk (faster than "full", more than "none")
                    transaction.Commit();
                }
            }

            switch (dbname)
            {
                case "climate":
                    this.databaseClimate = db;
                    break;
                case "in":
                    this.databaseIn = db;
                    break;
                case "out":
                    this.databaseOut = db;
                    break;
                default:
                    throw new NotSupportedException();
            }
            return true;
        }

        // true, if detailed debug information is logged
        public bool logLevelDebug()
        {
            return _loglevel < 1;
        }

        // true, if only important aggreate info is logged
        public bool logLevelInfo()
        {
            return _loglevel < 2;
        }

        // true if only severe warnings/errors are logged.
        public bool logLevelWarning()
        {
            return _loglevel < 3;
        }

        // path
        public void printDirectories()
        {
            Debug.WriteLine("current File Paths:");
            foreach (KeyValuePair<string, string> filePath in mFilePath)
            {
                Debug.WriteLine(filePath.Key + ": " + filePath.Value);
            }
        }

        public void setupDirectories(XmlNode pathNode, string projectFilePath)
        {
            mFilePath.Clear();
            mFilePath.Add("exe", this.GetType().Assembly.Location);
            XmlHelper xml = new XmlHelper(pathNode);
            string homePath = xml.value("home", projectFilePath);
            mFilePath.Add("home", homePath);
            // make other paths relativ to "home" if given as relative paths
            mFilePath.Add("lip", path(xml.value("lip", "lip"), "home"));
            mFilePath.Add("database", path(xml.value("database", "database"), "home"));
            mFilePath.Add("temp", path(xml.value("temp", ""), "home"));
            mFilePath.Add("log", path(xml.value("log", ""), "home"));
            mFilePath.Add("script", path(xml.value("script", ""), "home"));
            mFilePath.Add("init", path(xml.value("init", ""), "home"));
            mFilePath.Add("output", path(xml.value("output", "output"), "home"));
        }

        public void resetScriptEngine() ///< re-creates the script engine (when the Model is re-created)
        {
            mScriptEngine = new QJSEngine();
            // globals object: instatiate here, but ownership goes to script engine
            ScriptGlobal global = new ScriptGlobal();
            object glb = mScriptEngine.newQObject(global);
            mScriptEngine.globalObject().setProperty("Globals", glb);
        }

        public void setDebugOutput(int debug) 
        { 
            mDebugOutputs = (uint)debug;
        }

        public void setDebugOutput(DebugOutputs dbg, bool enable = true) ///< enable/disable a specific output type.
        {
            if (enable)
            {
                mDebugOutputs |= (uint)dbg;
            }
            else
            {
                mDebugOutputs &= (uint)dbg ^ 0xffffffff;
            }
        }

        public void setLogLevel(int loglevel)
        {
            _loglevel = loglevel;
            switch (loglevel)
            {
                case 0: Debug.WriteLine("Loglevel set to Debug."); break;
                case 1: Debug.WriteLine("Loglevel set to Info."); break;
                case 2: Debug.WriteLine("Loglevel set to Warning."); break;
                case 3: Debug.WriteLine("Loglevel set to Error/Quiet."); break;
                default: throw new NotSupportedException("invalid log level " + loglevel);
            }
        }

        // setting-meta-data
        /// access an individual SettingMetaData named @p name.
        public SettingMetaData settingMetaData(string name) // unused??
        {
            if (mSettingMetaData.ContainsKey(name))
            {
                return mSettingMetaData[name];
            }
            return null;
        }

        /// retrieve the default value of the setting @p name.
        public object settingDefaultValue(string name) // unused?
        {
            SettingMetaData smd = settingMetaData(name);
            if (smd != null)
            {
                return smd.defaultValue();
            }
            return null;
        }
    }
}
