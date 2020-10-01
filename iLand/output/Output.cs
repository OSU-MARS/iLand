using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.Output
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
    public abstract class Output
    {
        private string mInsertRowSql;
        private readonly List<object> mRow; ///< current row

        public bool IsOpen { get; private set; } ///< returns true if output is open, i.e. has a open database connection
        public bool IsEnabled { get; set; } ///< returns true if output is enabled, i.e. is "turned on"
        public bool IsRowEmpty() { return this.mRow.Count == 0; } ///< returns true if the buffer of the current row is empty

        public List<SqlColumn> Columns { get; protected set; }

        public string Name { get; set; } ///< descriptive name of the ouptut
        public string Description { get; protected set; } ///< description of output
        public string TableName { get; protected set; } ///< internal output name (no spaces allowed)

        //protected void Name(string name, string tableName) { Name = name; TableName = tableName; }
        protected int CurrentYear() { return GlobalSettings.Instance.CurrentYear; }
        protected XmlHelper Settings() { return GlobalSettings.Instance.Settings; } ///< access XML settings (see class description)

        protected void Add(double value1, double value2) { Add(value1); Add(value2); }
        protected void Add(double value1, double value2, double value3) { Add(value1, value2); Add(value3); }
        protected void Add(double value1, double value2, double value3, double value4) { Add(value1, value2); Add(value3, value4); }
        protected void Add(double value1, double value2, double value3, double value4, double value5) { Add(value1, value2); Add(value3, value4, value5); }

        public Output()
        {
            this.mInsertRowSql = null;
            this.mRow = new List<object>();

            this.Columns = new List<SqlColumn>();
            this.IsEnabled = false;
            this.IsOpen = false;

            this.NewRow();
        }

        public void Add(object value)
        {
            Debug.Assert(this.mRow.Count < this.Columns.Count);
            this.mRow.Add(value);
        }

        public void LogYear()
        {
            using SqliteTransaction insertTransaction = GlobalSettings.Instance.DatabaseOutput.BeginTransaction();
            using SqliteCommand insertRow = new SqliteCommand(this.mInsertRowSql, GlobalSettings.Instance.DatabaseOutput, insertTransaction);
            for (int columnIndex = 0; columnIndex < this.Columns.Count; columnIndex++)
            {
                insertRow.Parameters.Add("@" + this.Columns[columnIndex].Name, this.Columns[columnIndex].Datatype);
            }

            this.LogYear(insertRow);

            insertTransaction.Commit();
        }

        protected abstract void LogYear(SqliteCommand insertRow);

        private void NewRow()
        {
            this.mRow.Clear();
        }

        public void Open()
        {
            if (this.IsOpen)
            {
                return;
            }

            this.EnsureEmptySqlTable();
            this.mRow.Capacity = this.Columns.Count;
            this.NewRow();
        }

        /** create the database table and opens up the output.
          */
        private void EnsureEmptySqlTable()
        {
            SqliteConnection db = GlobalSettings.Instance.DatabaseOutput;
            // create the "create table" statement
            StringBuilder createTableCommand = new StringBuilder("create table " + this.TableName + "(");
            List<string> columnNames = new List<string>(this.Columns.Count);
            foreach (SqlColumn column in this.Columns)
            {
                switch (column.Datatype)
                {
                    case SqliteType.Integer: 
                        createTableCommand.Append(column.Name + " integer,"); 
                        break;
                    case SqliteType.Real: 
                        createTableCommand.Append(column.Name + " real,");
                        break;
                    case SqliteType.Text: 
                        createTableCommand.Append(column.Name + " text,"); 
                        break;
                    default: 
                        throw new NotSupportedException(); // blob
                }
                columnNames.Add(column.Name);
            }

            createTableCommand[^1] = ')'; // replace last "," with )

            SqliteCommand dropTable = new SqliteCommand(String.Format("drop table if exists {0}", this.TableName), db);
            dropTable.ExecuteNonQuery(); // drop table (if exists)
            SqliteCommand createTable = new SqliteCommand(createTableCommand.ToString(), db);
            createTable.ExecuteNonQuery(); // (re-)create table

            this.mInsertRowSql = "insert into " + this.TableName + " (" + String.Join(", ", columnNames) + ") values (@" + String.Join(", @", columnNames) + ")";

            this.IsOpen = true;
        }

        public virtual void Setup()
        {
        }

        public string WriteHeaderToWiki()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(Name);
            result.AppendLine(String.Format("Table Name: {0}{2}{1}", Name, TableName, Description, Environment.NewLine));
            // loop over columns...
            result.AppendLine("||__caption__|__datatype__|__description__"); // table begin
            foreach (SqlColumn col in Columns)
            {
                result.AppendLine(String.Format("{0}|{1}|{2}", col.Name, col.Datatype, col.Description));
            }
            result.AppendLine("||");
            return result.ToString();
        }

        protected void WriteRow(SqliteCommand insertRow)
        {
            for (int columnIndex = 0; columnIndex < this.Columns.Count; columnIndex++)
            {
                insertRow.Parameters[columnIndex].Value = this.mRow[columnIndex];
            }
            insertRow.ExecuteNonQuery();
            this.NewRow();
        }
    }
}
