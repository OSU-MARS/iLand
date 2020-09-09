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
        private static readonly GlobalSettings gl = GlobalSettings.instance();

        private bool disposed;
        private OutputMode mMode;
        private bool mOpen;
        private bool mEnabled;
        private string mName; ///< name of the output
        private string mTableName; ///< name of the table/output file
        private string mDescription; ///< textual description of the content
        private List<OutputColumn> mColumns; ///< list of columns of output
        private List<object> mRow; ///< current row
        private SqliteCommand mInserter;
        private int mCount;
        private int mIndex;

        public bool isOpen() { return mOpen; } ///< returns true if output is open, i.e. has a open database connection
        public bool isEnabled() { return mEnabled; } ///< returns true if output is enabled, i.e. is "turned on"
        public void setEnabled(bool enabled) { mEnabled = enabled; if (enabled) open(); }
        public bool isRowEmpty() { return mIndex == 0; } ///< returns true if the buffer of the current row is empty

        public List<OutputColumn> getColumns() { return mColumns; }

        public string name() { return mName; } ///< descriptive name of the ouptut
        public string description() { return mDescription; } ///< description of output
        public string tableName() { return mTableName; } ///< internal output name (no spaces allowed)

        protected void setName(string name, string tableName) { mName = name; mTableName = tableName; }
        protected void setDescription(string description) { mDescription = description; }
        protected List<OutputColumn> columns() { return mColumns; }
        protected int currentYear() { return gl.currentYear(); }
        protected XmlHelper settings() { return gl.settings(); } ///< access XML settings (see class description)

        protected void add(double value1, double value2) { add(value1); add(value2); }
        protected void add(double value1, double value2, double value3) { add(value1, value2); add(value3); }
        protected void add(double value1, double value2, double value3, double value4) { add(value1, value2); add(value3, value4); }
        protected void add(double value1, double value2, double value3, double value4, double value5) { add(value1, value2); add(value3, value4, value5); }

        public virtual void exec()
        {
            Debug.WriteLine("exec() called! (should be overrided!)");
        }

        public void add(double value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(double)", "output index out of range!");
            mRow[mIndex++] = value;
        }

        public void add(string value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(string)", "output index out of range!");
            mRow[mIndex++] = value;
        }
        public void add(int value)
        {
            Debug.WriteLineIf(mIndex >= mCount || mIndex < 0, "add(int)", "output index out of range!");
            mRow[mIndex++] = value;
        }

        public void setup()
        {
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            if (disposing)
            {
                this.close();
            }
        }

        public Output()
        {
            mCount = 0;
            mMode = OutputMode.OutDatabase;
            mOpen = false;
            mEnabled = false;
            newRow();
        }


        /** create the database table and opens up the output.
          */
        private void openDatabase()
        {
            SqliteConnection db = GlobalSettings.instance().dbout();
            // create the "create table" statement
            StringBuilder sql = new StringBuilder("create table " + mTableName + "(");
            StringBuilder insert = new StringBuilder("insert into " + mTableName + " (");
            StringBuilder values = new StringBuilder();
            foreach (OutputColumn col in columns())
            {
                switch (col.mDatatype)
                {
                    case OutputDatatype.OutInteger: sql.Append(col.name() + " integer"); break;
                    case OutputDatatype.OutDouble: sql.Append(col.name() + " real"); break;
                    case OutputDatatype.OutString: sql.Append(col.name() + " text"); break;
                    default: throw new NotSupportedException();
                }
                insert.Append(col.name() + ",");
                values.Append(":" + col.name() + ",");
                sql.Append(",");
            }
            sql[sql.Length - 1] = ')'; // replace last "," with )
                                       //qDebug()<< sql;
            SqliteCommand drop = new SqliteCommand(String.Format("drop table if exists {0}", tableName()), db);
            drop.ExecuteNonQuery(); // drop table (if exists)
            SqliteCommand creator = new SqliteCommand(sql.ToString(), db);
            creator.ExecuteNonQuery(); // (re-)create table
                                       //creator.exec("delete from " + tableName()); // clear table??? necessary?

            insert[insert.Length - 1] = ')';
            values[values.Length - 1] = ')';
            insert.Append(" values (" + values);
            mInserter = new SqliteCommand(insert.ToString(), db);
            for (int i = 0; i < columns().Count; i++)
            {
                mInserter.Parameters.AddWithValue(columns()[i].name(), mRow[i]);
            }
            mOpen = true;
        }

        private void newRow()
        {
            mIndex = 0;
        }

        public void writeRow()
        {
            Debug.WriteLineIf(mIndex != mCount, "save()", "received invalid number of values!");
            if (!isOpen())
            {
                open();
            }

            switch (mMode)
            {
                case OutputMode.OutDatabase:
                    saveDatabase();
                    break;
                default:
                    throw new NotSupportedException("Invalid output mode");
            }
        }

        public void open()
        {
            if (isOpen())
            {
                return;
            }

            // setup columns
            mCount = columns().Count;
            mRow.Capacity = mCount;
            mOpen = true;
            newRow();
            // setup output
            switch (mMode)
            {
                case OutputMode.OutDatabase:
                    openDatabase();
                    break;
                default:
                    throw new NotSupportedException("Invalid output mode");
            }
        }

        public void close()
        {
            if (!isOpen())
            {
                return;
            }
            mOpen = false;
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

        private void saveDatabase()
        {
            for (int i = 0; i < mCount; i++)
            {
                mInserter.Parameters[i].Value = mRow[i];
            }
            mInserter.ExecuteNonQuery();
            newRow();
        }

        public string wikiFormat()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(name());
            result.AppendLine(String.Format("Table Name: {0}{2}{1}", name(), tableName(), description(), Environment.NewLine));
            // loop over columns...
            result.AppendLine("||__caption__|__datatype__|__description__"); // table begin
            foreach (OutputColumn col in mColumns)
            {
                result.AppendLine(String.Format("{0}|{1}|{2}", col.name(), col.datatype(), col.description()));
            }
            result.AppendLine("||");
            return result.ToString();
        }
    }
}
