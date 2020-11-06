using iLand.Simulation;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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
        string filter = settings().value(".filter", String.Empty);
        if (filter != String.Empty)
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
        private string insertRowSqlText;

        public List<SqlColumn> Columns { get; protected set; }

        public string Name { get; set; } // descriptive name of the ouptut
        public string Description { get; protected set; } // description of output
        public string TableName { get; protected set; } // internal output name (no spaces allowed)

        public Output()
        {
            this.insertRowSqlText = null;

            this.Columns = new List<SqlColumn>();
        }

        public void LogYear(Model model, SqliteTransaction transaction)
        {
            SqliteCommand insertRow = new SqliteCommand(this.insertRowSqlText, transaction.Connection, transaction);
            for (int columnIndex = 0; columnIndex < this.Columns.Count; ++columnIndex)
            {
                insertRow.Parameters.Add("@" + this.Columns[columnIndex].Name, this.Columns[columnIndex].Datatype);
            }

            this.LogYear(model, insertRow);
        }

        protected abstract void LogYear(Model model, SqliteCommand insertRow);

        public void Open(SqliteTransaction transaction)
        {
            // ensure an empty table exists for this output to log to
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

            SqliteCommand dropTable = new SqliteCommand(String.Format("drop table if exists {0}", this.TableName), transaction.Connection, transaction);
            dropTable.ExecuteNonQuery(); // drop table (if exists)
            SqliteCommand createTable = new SqliteCommand(createTableCommand.ToString(), transaction.Connection, transaction);
            createTable.ExecuteNonQuery(); // (re-)create table

            this.insertRowSqlText = "insert into " + this.TableName + " (" + String.Join(", ", columnNames) + ") values (@" + String.Join(", @", columnNames) + ")";
        }

        public virtual void Setup(Model model)
        {
        }

        //public string WriteHeaderToWiki()
        //{
        //    StringBuilder result = new StringBuilder();
        //    result.AppendLine(Name);
        //    result.AppendLine(String.Format("Table Name: {0}{2}{1}", Name, TableName, Description, System.Environment.NewLine));
        //    // loop over columns...
        //    result.AppendLine("||__caption__|__datatype__|__description__"); // table begin
        //    foreach (SqlColumn col in Columns)
        //    {
        //        result.AppendLine(String.Format("{0}|{1}|{2}", col.Name, col.Datatype, col.Description));
        //    }
        //    result.AppendLine("||");
        //    return result.ToString();
        //}
    }
}
