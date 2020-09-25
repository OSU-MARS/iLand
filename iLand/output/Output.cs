using iLand.tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.output
{
    /** @class Output
        The Output class abstracts output data (database, textbased, ...).
        To create a new output, create a class derived from Output and perform the following steps:
        - Overwriteructor:
        Create columns and set fixed properties (e.g. table name)
        - overwrite setup()
        this function is called after the project file is read. You can access a XmlHelper calling settings()
        which is set to the top-node of the output (defined by tableName() which is set in theructor). Access settings
        using relative xml-pathes (see example).
        - overwrite exec()
        add data using the stream operators or add() function of Output. Call writeRow() after each row. Each invokation
        of exec() is a database transaction.
        - Add the output to theructor of @c OutputManager

        @par Example
        @code
        // (1) Overwriteructor and set name, description and columns
        TreeOut::TreeOut()
        {
        setName("Tree Output", "tree");
        setDescription("Output of indivdual trees.");
        columns() << OutputColumn("id", "id of the tree", OutInteger)
                 << OutputColumn("name", "tree species name", OutString)
                 << OutputColumn("v1", "a double value", OutDouble);
        }
        // (2) optionally: some special settings (here: filter)
        void TreeOut::setup()
        {
        string filter = settings().value(".filter","");
        if (filter!="")
            mFilter = QSharedPointer<Expression>(new Expression(filter));
        }

        // (3) the execution
        void TreeOut::exec()
        {
        AllTreeIterator at(GlobalSettings.instance().model());
        while (Tree *t=at.next()) {
            if (mFilter && !mFilter.execute()) // skip if filter present
                continue;
            this << t.id() << t.species().id() << t.dbh(); // stream operators
            writeRow(); // executes DB insert
        }
        }
        // in outputmanager.cpp:
        OutputManager::OutputManager()
        {
        ...
        mOutputs.Add(new TreeOut); // add the output
        ...
        }
        @endcode
        */
    internal class Output
    {
        private static readonly GlobalSettings gl = GlobalSettings.Instance;

        private bool isDisposed;
        private readonly OutputMode mMode;
        private readonly List<object> mRow; ///< current row
        private SqliteCommand mInserter;
        private int mCount;
        private int mIndex;

        public bool IsOpen { get; private set; } ///< returns true if output is open, i.e. has a open database connection
        public bool IsEnabled { get; set; } ///< returns true if output is enabled, i.e. is "turned on"
        public bool IsRowEmpty() { return mIndex == 0; } ///< returns true if the buffer of the current row is empty

        public List<OutputColumn> Columns { get; protected set; }

        public string Name { get; set; } ///< descriptive name of the ouptut
        public string Description { get; protected set; } ///< description of output
        public string TableName { get; protected set; } ///< internal output name (no spaces allowed)

        //protected void Name(string name, string tableName) { Name = name; TableName = tableName; }
        protected int CurrentYear() { return gl.CurrentYear; }
        protected XmlHelper Settings() { return gl.Settings; } ///< access XML settings (see class description)

        protected void Add(double value1, double value2) { Add(value1); Add(value2); }
        protected void Add(double value1, double value2, double value3) { Add(value1, value2); Add(value3); }
        protected void Add(double value1, double value2, double value3, double value4) { Add(value1, value2); Add(value3, value4); }
        protected void Add(double value1, double value2, double value3, double value4, double value5) { Add(value1, value2); Add(value3, value4, value5); }

        public Output()
        {
            this.Columns = new List<OutputColumn>();
            this.mCount = 0;
            this.IsEnabled = false;
            this.mMode = OutputMode.OutDatabase;
            this.IsOpen = false;
            this.mRow = new List<object>();

            NewRow();
        }

        public virtual void Exec()
        {
            Debug.WriteLine("exec() called! (should be overrided!)");
        }

        public void Add(double value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(double)", "output index out of range!");
            mRow[mIndex++] = value;
        }

        public void Add(string value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(string)", "output index out of range!");
            mRow[mIndex++] = value;
        }

        public void Add(int value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(int)", "output index out of range!");
            mRow[mIndex++] = value;
        }

        public virtual void Setup()
        {
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }
            if (disposing)
            {
                this.Close();
            }

            this.isDisposed = true;
        }

        /** create the database table and opens up the output.
          */
        private void OpenDatabase()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseOutput;
            // create the "create table" statement
            StringBuilder sql = new StringBuilder("create table " + TableName + "(");
            StringBuilder insert = new StringBuilder("insert into " + TableName + " (");
            StringBuilder values = new StringBuilder();
            foreach (OutputColumn col in Columns)
            {
                switch (col.mDatatype)
                {
                    case OutputDatatype.OutInteger: sql.Append(col.Name + " integer"); break;
                    case OutputDatatype.OutDouble: sql.Append(col.Name + " real"); break;
                    case OutputDatatype.OutString: sql.Append(col.Name + " text"); break;
                    default: throw new NotSupportedException();
                }
                insert.Append(col.Name + ",");
                values.Append(":" + col.Name + ",");
                sql.Append(",");
            }
            sql[^1] = ')'; // replace last "," with )
                                       //qDebug()<< sql;
            SqliteCommand drop = new SqliteCommand(String.Format("drop table if exists {0}", TableName), db);
            drop.ExecuteNonQuery(); // drop table (if exists)
            SqliteCommand creator = new SqliteCommand(sql.ToString(), db);
            creator.ExecuteNonQuery(); // (re-)create table
                                       //creator.exec("delete from " + tableName()); // clear table??? necessary?

            insert[^1] = ')';
            values[^1] = ')';
            insert.Append(" values (" + values);
            mInserter = new SqliteCommand(insert.ToString(), db);
            for (int i = 0; i < Columns.Count; i++)
            {
                mInserter.Parameters.AddWithValue(Columns[i].Name, mRow[i]);
            }
            IsOpen = true;
        }

        private void NewRow()
        {
            mIndex = 0;
        }

        public void WriteRow()
        {
            Debug.WriteLineIf(mIndex != mCount, "save()", "received invalid number of values!");
            if (!IsOpen)
            {
                Open();
            }

            switch (mMode)
            {
                case OutputMode.OutDatabase:
                    SaveDatabase();
                    break;
                default:
                    throw new NotSupportedException("Invalid output mode");
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            // setup columns
            mCount = Columns.Count;
            mRow.Capacity = mCount;
            IsOpen = true;
            NewRow();
            // setup output
            switch (mMode)
            {
                case OutputMode.OutDatabase:
                    OpenDatabase();
                    break;
                default:
                    throw new NotSupportedException("Invalid output mode");
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            switch (mMode)
            {
                case OutputMode.OutDatabase:
                    if (mInserter != null)
                    {
                        mInserter.Dispose();
                    }
                    mInserter = null;
                    break;
                default:
                    Trace.TraceWarning("close with invalid mode");
                    break;
            }
        }

        private void SaveDatabase()
        {
            for (int i = 0; i < mCount; i++)
            {
                mInserter.Parameters[i].Value = mRow[i];
            }
            mInserter.ExecuteNonQuery();
            NewRow();
        }

        public string WikiFormat()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(Name);
            result.AppendLine(String.Format("Table Name: {0}{2}{1}", Name, TableName, Description, Environment.NewLine));
            // loop over columns...
            result.AppendLine("||__caption__|__datatype__|__description__"); // table begin
            foreach (OutputColumn col in Columns)
            {
                result.AppendLine(String.Format("{0}|{1}|{2}", col.Name, col.Datatype(), col.Description));
            }
            result.AppendLine("||");
            return result.ToString();
        }
    }
}
