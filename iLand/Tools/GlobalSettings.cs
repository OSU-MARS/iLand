using iLand.Input.ProjectFile;
using iLand.Output;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
namespace iLand.Tools
{
    public class GlobalSettings : IDisposable
    {
        private bool isDisposed;
        private readonly Dictionary<string, string> mFilePaths; // storage for file paths
        private int mLoglevel;

        public int CurrentYear { get; set; }
        public SqliteConnection DatabaseClimate { get; private set; }
        public SqliteConnection DatabaseInput { get; private set; }
        public SqliteConnection DatabaseOutput { get; private set; }

        public OutputManager OutputManager { get; private set; }
        //public SystemStatistics SystemStatistics { get; private set; }

        public GlobalSettings()
        {
            // lazy init
            // this.databaseClimate
            // this.databaseIn
            // this.databaseOut
            this.isDisposed = false;
            this.mFilePaths = new Dictionary<string, string>();
            this.mLoglevel = 0;

            this.OutputManager = new OutputManager();
            //this.SystemStatistics = new SystemStatistics();
        }

        public SqliteConnection DatabaseSnapshot() { throw new NotImplementedException(); }
        public SqliteConnection DatabaseSnapshotStand() { throw new NotImplementedException(); }

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
                    this.CloseDatabaseConnections();
                }

                isDisposed = true;
            }
        }

        public string GetPath(string fileName, string type = "home")
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                if (System.IO.Path.IsPathRooted(fileName))
                {
                    // canonicalize path
                    return System.IO.Path.GetFullPath(fileName);
                }
            }

            if (mFilePaths.TryGetValue(type, out string directoryPath) == false)
            {
                directoryPath = System.Environment.CurrentDirectory;
            }
            if (String.IsNullOrEmpty(fileName))
            {
                return directoryPath;
            }
            return System.IO.Path.Combine(directoryPath, fileName);
        }

        // Database connections
        public void CloseDatabaseConnections() // shutdown and clear connections
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
                Mode = fileMustExist ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            };
            SqliteConnection db = new SqliteConnection(connectionString.ConnectionString);
            // Debug.WriteLine("setup database connection " + dbname + " to " + databaseFilePath);
            db.Open();
            if (!fileMustExist)
            {
                // performance settings for output databases (http://www.sqlite.org/pragma.html)
                // Databases are typically expensive to create and maintain so SQLite defaults to conservative disk interactions. iLand
                // output data is cheap to generate and easy to recreate in the unlikely event something goes wrong flushing to disk, so
                // caution can be exchanged for speed. For example, journal_mode = memory, synchronous = off, and temp_store = memory 
                // make the model unit tests run 4-5x times faster than default settings.
                // pragma synchronous cannot be changed within a transaction
                using SqliteCommand synchronization = new SqliteCommand("pragma synchronous(off)", db);
                synchronization.ExecuteNonQuery();

                using SqliteTransaction transaction = db.BeginTransaction();
                // little to no difference between journal_mode = memory and journal_mode = off
                using SqliteCommand journalMode = new SqliteCommand("pragma journal_mode(memory)", db, transaction);
                journalMode.ExecuteNonQuery();
                using SqliteCommand tempStore = new SqliteCommand("pragma temp_store(memory)", db, transaction);
                tempStore.ExecuteNonQuery();
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
            return mLoglevel < 1;
        }

        // true, if only important aggreate info is logged
        public bool LogInfo()
        {
            return mLoglevel < 2;
        }

        // true if only severe warnings/errors are logged.
        public bool LogWarnings()
        {
            return mLoglevel < 3;
        }

        public void SetupDirectories(Paths paths, string projectFilePath)
        {
            mFilePaths.Clear();
            mFilePaths.Add("exe", this.GetType().Assembly.Location);
            string homePath = paths.Home;
            if (String.IsNullOrEmpty(homePath))
            {
                homePath = System.IO.Path.GetDirectoryName(projectFilePath);
                if (String.IsNullOrEmpty(homePath))
                {
                    throw new ArgumentOutOfRangeException(projectFilePath);
                }
            }

            mFilePaths.Add("home", homePath);
            // make other paths relative to "home" if given as relative paths
            mFilePaths.Add("lip", this.GetPath(paths.LightIntensityProfile, "home"));
            mFilePaths.Add("database", this.GetPath(paths.Database, "home"));
            mFilePaths.Add("temp", this.GetPath(paths.Temp, "home"));
            mFilePaths.Add("log", this.GetPath(paths.Log, "home"));
            mFilePaths.Add("script", this.GetPath(paths.Script, "home"));
            mFilePaths.Add("init", this.GetPath(paths.Init, "home"));
            mFilePaths.Add("output", this.GetPath(paths.Output, "home"));
        }

        public void SetLogLevel(int loglevel)
        {
            mLoglevel = loglevel;
            switch (loglevel)
            {
                case 0: Debug.WriteLine("Log level set to debug."); break;
                case 1: Debug.WriteLine("Log level set to info."); break;
                case 2: Debug.WriteLine("Log level set to warning."); break;
                case 3: Debug.WriteLine("Log level set to error/quiet."); break;
                default: throw new NotSupportedException("invalid log level " + loglevel);
            }
        }
    }
}
