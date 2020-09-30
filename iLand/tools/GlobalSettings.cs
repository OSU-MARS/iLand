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
        private static readonly ReadOnlyCollection<string> DebugOutputNames = new List<string>() { "treeNPP", "treePartition", "treeGrowth", "waterCycle", "dailyResponse", "establishment", "carbonCycle", "performance" }.AsReadOnly();
        public static GlobalSettings Instance { get; private set; }

        private bool isDisposed;
        private int _loglevel;
        // special debug outputs
        private readonly MultiValueDictionary<int, List<object>> mDebugLists;
        private readonly Dictionary<string, string> mFilePath; ///< storage for file paths
        private readonly Dictionary<string, SettingMetaData> mSettingMetaData; ///< storage container (QHash) for settings.

        public int CurrentYear { get; set; }
        public SqliteConnection DatabaseClimate { get; private set; }
        public SqliteConnection DatabaseInput { get; private set; }
        public SqliteConnection DatabaseOutput { get; private set; }
        public int DebugOutputs { get; set; }
        public Model Model { get; set; }
        public ModelController ModelController { get; set; }

        public OutputManager OutputManager { get; private set; }
        // xml project file
        public XmlHelper Settings { get; private set; }
        public SystemStatistics SystemStatistics { get; private set; }

        static GlobalSettings()
        {
            GlobalSettings.Instance = new GlobalSettings();
        }

        private GlobalSettings()
        {
            // lazy init
            // this.databaseClimate
            // this.databaseIn
            // this.databaseOut
            this._loglevel = 0;
            this.isDisposed = false;
            this.mDebugLists = new MultiValueDictionary<int, List<object>>();
            this.DebugOutputs = 0;
            this.mFilePath = new Dictionary<string, string>();
            this.Model = null;
            this.ModelController = null;
            this.OutputManager = new OutputManager();
            // initialized externall
            // this.mRunYear
            this.mSettingMetaData = new Dictionary<string, SettingMetaData>();
            this.SystemStatistics = new SystemStatistics();
            this.Settings = new XmlHelper();
        }

        public SqliteConnection DatabaseSnapshot() { throw new NotImplementedException(); }
        public SqliteConnection DatabaseSnapshotstand() { throw new NotImplementedException(); }

        public bool IsDebugEnabled(DebugOutputs dbg) 
        { 
            return ((int)dbg & DebugOutputs) != 0;
        } ///< returns true, if a specific debug outut type is enabled.

        public List<string> SettingNames() { return mSettingMetaData.Keys.ToList(); } ///< retrieve list of all names of settings.

        private string ChildText(XmlNode elem, string name, string defaultTest = "")
        {
            foreach (XmlNode node in elem.SelectNodes(name))
            {
                return node.InnerText;
            }
            return defaultTest;
        }

        public void ClearDebugLists() ///< clear all debug data
        {
            mDebugLists.Clear();
        }

        public List<string> DebugDataTable(DebugOutputs type, string separator, string fileName = null) ///< output for all available items (trees, ...) in table form
        {
            GlobalSettings g = GlobalSettings.Instance;
            List<List<object>> ddl = g.DebugLists(-1, type); // get all debug data

            List<string> result = new List<string>();
            if (ddl.Count < 1)
            {
                return result;
            }

            TextWriter ts = null;
            if (!String.IsNullOrWhiteSpace(fileName))
            {
                FileStream out_file = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                ts = new StreamWriter(out_file);
                ts.WriteLine(String.Join(separator, g.DebugListCaptions(type)));
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
                result.Insert(0, String.Join(separator, g.DebugListCaptions(type)));
            }
            return result;
        }

        public DebugOutputs DebugOutputID(string debug_name) ///< returns the DebugOutputs bit or 0 if not found
        {
            int index = DebugOutputNames.IndexOf(debug_name);
            if (index == -1)
            {
                return default;
            }
            return (DebugOutputs)(2 << index); // 1,2,4,8, ...
        }

        public List<object> DebugList(int ID, DebugOutputs dbg) ///< returns a ref to a list ready to be filled with debug output of a type/id combination.
        {
            // serialize creation of debug outputs
            lock (mDebugLists)
            {
                List<object> dbglist = new List<object>() { ID, dbg, CurrentYear };
                int id = ID;
                // use negative values for debug-outputs on RU - level
                // Note: at some point we will also have to handle RUS-level...
                if (dbg == tools.DebugOutputs.Establishment || dbg == tools.DebugOutputs.CarbonCycle || dbg == tools.DebugOutputs.SaplingGrowth)
                {
                    id = -id;
                }
                mDebugLists.Add(id, dbglist);
                return dbglist;
            }
        }

        private int DebugListSorter(List<object> i, List<object> j)
        {
            // TODO: implement less fragile compare
            return Comparer<int>.Default.Compare((int)i[0], (int)j[0]);
        }

        public List<string> DebugListCaptions(DebugOutputs dbg) ///< returns stringlist of captions for a specific output type
        {
            List<string> treeCaps = new List<string>() { "Id", "Species", "Dbh", "Height", "x", "y", "ru_index", "LRI", "mWoody", "mRoot", "mFoliage", "LA" };
            if (dbg == 0)
            {
                return treeCaps;
            }

            // TODO: what if multiple flags are set?
            switch (dbg)
            {
                case tools.DebugOutputs.TreeNpp:
                    List<string> treeNpp = new List<string>() { "id", "type", "year" };
                    treeNpp.AddRange(treeCaps);
                    treeNpp.AddRange(new string[] { "LRI_modRU", "lightResponse", "effective_area", "raw_gpp", "gpp", "npp", "aging" });
                    return treeNpp;
                case tools.DebugOutputs.TreeGrowth:
                    List<string> treeGrowth = new List<string>() { "id", "type", "year" };
                    treeGrowth.AddRange(treeCaps);
                    treeGrowth.AddRange(new string[] { "netNPPStem", "massStemOld", "hd_growth", "factor_diameter", "delta_d_estimate", "d_increment" });
                    return treeGrowth;
                case tools.DebugOutputs.TreePartition:
                    List<string> treePartition = new List<string>() { "id", "type", "year" };
                    treePartition.AddRange(treeCaps);
                    treePartition.AddRange(new string[] { "npp_kg", "apct_foliage", "apct_wood", "apct_root", "delta_foliage", "delta_woody", "delta_root", "mNPPReserve", "netStemInc", "stress_index" });
                    return treePartition;
                case tools.DebugOutputs.StandGpp:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid", "lai_factor", "gpp_kg_m2", "gpp_kg", "avg_aging", "f_env_yr" };
                case tools.DebugOutputs.WaterCycle:
                    return new List<string>() { "id", "type", "year", "date", "ruindex", "rid", "temp", "vpd", "prec", "rad", "combined_response"
                                        , "after_intercept", "after_snow", "et_canopy", "evapo_intercepted"
                                        , "content", "psi_kpa", "excess_mm", "snow_height" };
                case tools.DebugOutputs.DailyResponses:
                    return new List<string>() { "id", "type", "year", "species", "date", "RU_index", "rid"
                                        , "waterResponse", "tempResponse", "VpdResponse", "Radiation of day", "util.Radiation" };
                case tools.DebugOutputs.Establishment:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid"
                                        , "avgProbDensity", "TACAminTemp", "TACAchill", "TACAfrostFree", "TACAgdd", "TACAFrostAfterBud", "waterLimitation", "TACAAbioticEnv"
                                        , "fEnvYr", "N_Established" };
                case tools.DebugOutputs.SaplingGrowth:
                    return new List<string>() { "id", "type", "year", "species", "RU_index", "rid"
                                        , "Living_cohorts", "averageHeight", "averageAge", "avgDeltaHPot", "avgDeltaHRealized"
                                        , "Added", "Died", "Recruited", "refRatio" };
                case tools.DebugOutputs.CarbonCycle:
                    return new List<string>() { "id", "type", "year", "RU_index", "rid"
                                        , "SnagState_c", "TotalC_in", "TotalC_toAtm", "SWDtoDWD_c", "SWDtoDWD_n", "toLabile_c", "toLabile_n", "toRefr_c", "toRefr_n"
                                        , "swd1_c", "swd1_n", "swd1_count", "swd1_tsd", "toSwd1_c", "toSwd1_n", "dbh1", "height1", "volume1"  // pool of small dbhs
                                        , "swd2_c", "swd2_n", "swd2_count", "swd2_tsd", "toSwd2_c", "toSwd2_n", "dbh2", "height2", "volume2"   // standing woody debris medium dbhs
                                        , "swd3_c", "swd3_n", "swd3_count", "swd3_tsd", "toSwd3_c", "toSwd3_n", "dbh3", "height3", "volume3"   // large trees
                                        , "otherWood1_c", "otherWood1_n", "otherWood2_c", "otherWood2_n", "otherWood3_c", "otherWood3_n", "otherWood4_c", "otherWood4_n", "otherWood5_c", "otherWood5_n"
                                        , "iLabC", "iLabN", "iKyl", "iRefC", "iRefN", "iKyr", "re", "kyl", "kyr", "ylC", "ylN", "yrC", "yrN", "somC", "somN"
                                        , "NAvailable", "NAVLab", "NAVRef", "NAVSom" };
                case tools.DebugOutputs.Performance:
                    return new List<string>() { "id", "type", "year", "treeCount", "saplingCount", "newSaplings", "management"
                                        , "applyPattern", "readPattern", "treeGrowth", "seedDistribution", "establishment", "saplingGrowth", "carbonCycle"
                                        , "writeOutput", "totalYear" };
                default:
                    return new List<string>() { "invalid debug output!" };
            }
        }

        public List<List<object>> DebugLists(int ID, DebugOutputs dbg) ///< return a list of debug outputs
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
            result_list.Sort(DebugListSorter);
            return result_list;
        }

        public string DebugOutputName(DebugOutputs d) ///< returns the name attached to 'd' or an empty string if not found
        {
            // this is a little hacky...(and never really tried!)
            for (int index = 0; index < DebugOutputNames.Count; ++index)
            {
                if (((int)d & (2 << index)) != 0)
                    return DebugOutputNames[index];
            }
            throw new NotSupportedException();
        }

        public List<Tuple<string, object>> DebugValues(int ID) ///< all debug values for object with given ID
        {
            List<Tuple<string, object>> result = new List<Tuple<string, object>>();
            foreach (List<object> list in mDebugLists[ID])
            {
                if (list.Count > 2) // TODO: should this be zero?
                { // contains data
                    List<string> cap = DebugListCaptions((DebugOutputs)list[1]);
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
                    ClearDatabaseConnections();
                    //if (mScriptEngine)
                    //    delete mScriptEngine;
                }

                isDisposed = true;
            }
        }

        // path and directory
        public bool FileExists(string fileName, string type = "home")
        {
            // TODO: review use since semantics here seem confused
            string name = System.IO.Path.Combine(fileName, type);

            if (!Directory.Exists(name)) // TODO: also try File.Exists()
            {
                Debug.WriteLine("Path " + fileName + " (expanded to:) " + name + " does not exist!");
                return false;
            }
            return true;
        }

        public string Path(string fileName, string type = "home")
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                if (System.IO.Path.IsPathRooted(fileName))
                {
                    // canonicalize path
                    return System.IO.Path.GetFullPath(fileName);
                }
            }

            if (mFilePath.TryGetValue(type, out string directoryPath) == false)
            {
                directoryPath = System.Environment.CurrentDirectory;
            }
            if (String.IsNullOrEmpty(fileName))
            {
                return directoryPath;
            }
            return System.IO.Path.Combine(directoryPath, fileName);
        }

        // xml project settings
        public void LoadProjectFile(string fileName)
        {
            Debug.WriteLine("Loading Project file " + fileName);
            if (File.Exists(fileName) == false)
            {
                throw new ArgumentException(String.Format("The project file {0} does not exist!", fileName), nameof(fileName));
            }
            Settings.LoadFromFile(fileName);
            SetupDirectories(Settings.Node("system.path"), new FileInfo(fileName).FullName);
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
        public void LoadSettingsMetaDataFromXml(XmlElement topNode)
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

                SettingMetaData md = new SettingMetaData(tools.SettingMetaData.TypeFromName(elt.Attributes["type"].Value), // type
                              settingName, // name
                              ChildText(elt, "description"), // description
                              ChildText(elt, "url"), // url
                              ChildText(elt, "default"));
                mSettingMetaData[settingName] = md;

                Debug.WriteLine(md.Dump());
                //mSettingMetaData[settingName].dump();
            }
            Debug.WriteLine("setup settingmetadata complete." + mSettingMetaData.Count + "items loaded.");
        }

        // Database connections
        public void ClearDatabaseConnections() ///< shutdown and clear connections
        {
            if (this.DatabaseClimate != null)
            {
                this.DatabaseClimate.Dispose();
                this.DatabaseClimate = null;
            }
            if (this.DatabaseInput != null)
            {
                this.DatabaseInput.Dispose();
                this.DatabaseInput = null;
            }
            if (this.DatabaseOutput != null)
            {
                this.DatabaseOutput.Dispose();
                this.DatabaseOutput = null;
            }
        }

        public bool SetupDatabaseConnection(string dbname, string databaseFilePath, bool fileMustExist)
        {
            if ((String.Equals(dbname, "in", StringComparison.Ordinal) == false) &&
                (String.Equals(dbname, "out", StringComparison.Ordinal) == false) &&
                (String.Equals(dbname, "climate", StringComparison.Ordinal) == false))
            {
                throw new ArgumentOutOfRangeException(nameof(dbname));
            }
            if (fileMustExist)
            {
                if (File.Exists(databaseFilePath) == false)
                {
                    throw new ArgumentException("Database file '" + databaseFilePath + "'does not exist!", nameof(databaseFilePath));
                }
            }

            // TODO: check if database is already open
            //QSqlDatabase::database(dbname).close(); // close database
            SqliteConnectionStringBuilder connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFilePath,
                Mode = fileMustExist ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,
            };
            SqliteConnection db = new SqliteConnection(connectionString.ConnectionString);
            Trace.WriteLine("setup database connection " + dbname + " to " + databaseFilePath);
            db.Open();
            if (!fileMustExist)
            {
                // for output databases:
                // some special commands (pragmas: see also: http://www.sqlite.org/pragma.html)
                using SqliteTransaction transaction = db.BeginTransaction();
                SqliteCommand tempStore = new SqliteCommand("pragma temp_store(2)", db, transaction); // temp storage in memory
                // for now, use default Sqlite synchronization
                // db.exec("pragma synchronous(1)"); // medium synchronization between memory and disk (faster than "full", more than "none")
                transaction.Commit();
            }

            switch (dbname)
            {
                case "climate":
                    this.DatabaseClimate = db;
                    break;
                case "in":
                    this.DatabaseInput = db;
                    break;
                case "out":
                    this.DatabaseOutput = db;
                    break;
                default:
                    throw new NotSupportedException();
            }
            return true;
        }

        // true, if detailed debug information is logged
        public bool LogDebug()
        {
            return _loglevel < 1;
        }

        // true, if only important aggreate info is logged
        public bool LogInfo()
        {
            return _loglevel < 2;
        }

        // true if only severe warnings/errors are logged.
        public bool LogWarnings()
        {
            return _loglevel < 3;
        }

        // path
        public void PrintDirectories()
        {
            Debug.WriteLine("current File Paths:");
            foreach (KeyValuePair<string, string> filePath in mFilePath)
            {
                Debug.WriteLine(filePath.Key + ": " + filePath.Value);
            }
        }

        public void SetupDirectories(XmlNode pathNode, string projectFilePath)
        {
            mFilePath.Clear();
            mFilePath.Add("exe", this.GetType().Assembly.Location);
            XmlHelper xml = new XmlHelper(pathNode);
            string homePath = xml.Value("home", System.IO.Path.GetDirectoryName(projectFilePath));
            mFilePath.Add("home", homePath);
            // make other paths relative to "home" if given as relative paths
            // BUGBUG: doesn't detect missing entries in project file
            mFilePath.Add("lip", Path(xml.Value("lip", "lip"), "home"));
            mFilePath.Add("database", Path(xml.Value("database", "database"), "home"));
            mFilePath.Add("temp", Path(xml.Value("temp", ""), "home"));
            mFilePath.Add("log", Path(xml.Value("log", ""), "home"));
            mFilePath.Add("script", Path(xml.Value("script", ""), "home"));
            mFilePath.Add("init", Path(xml.Value("init", ""), "home"));
            mFilePath.Add("output", Path(xml.Value("output", "output"), "home"));
        }

        public void SetDebugOutput(DebugOutputs dbg, bool enable = true) ///< enable/disable a specific output type.
        {
            if (enable)
            {
                DebugOutputs |= (int)dbg;
            }
            else
            {
                DebugOutputs &= (int)((uint)dbg ^ 0xffffffff);
            }
        }

        public void SetLogLevel(int loglevel)
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
        public SettingMetaData SettingMetaData(string name) // unused??
        {
            if (mSettingMetaData.ContainsKey(name))
            {
                return mSettingMetaData[name];
            }
            return null;
        }

        /// retrieve the default value of the setting @p name.
        public object SettingDefaultValue(string name) // unused?
        {
            SettingMetaData smd = SettingMetaData(name);
            if (smd != null)
            {
                return smd.DefaultValue;
            }
            return null;
        }
    }
}
