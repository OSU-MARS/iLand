using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace iLand.Tools
{
    /** @class CSVFile
        Provides access to table data stored in text files (CSV style).
        Tables have headers in first line and can use "tab", ";", ",", and " " as delimeters.
        Table dimensions can be accessed with colCount() and rowCount(), cell values as object are retrieved
        by value(). full rows are retrieved using row().
        Files are loaded by loadFile() or by passing a filename to the constructor:
        @code
        CSVFile file(fileName);
        for (int row=0; row<file.rowCount(); row++)
         for (int col=0; col<file.colCount(); col++)
           value = file.value(row, col);
        @endcode
        Planned is also a "streaming" mode for large files (loadFile(), while(file.next()) file.value(x) ), but not finsihed yet.
        */
    internal class CsvFile
    {
        private readonly List<string> mRows;
        private char mSeparator;

        // properties
        public List<string> ColumnNames { get; private set; } // retrieve (a copy) of column headers

        // ctor, load @p fileName.
        public CsvFile(string fileName)
            : this()
        {
            this.LoadFile(fileName);
        }

        public CsvFile()
        {
            mRows = new List<string>();
            this.ColumnNames = new List<string>();
        }

        public int ColumnCount { get { return this.ColumnNames.Count; } }
        public int RowCount { get { return this.mRows.Count; } }

        // get caption of ith column.
        //public string GetColumnName(int col)
        //{
        //    return this.ColumnNames[col];
        //}

        // index of column or -1 if not available
        public int GetColumnIndex(string columnName)
        {
            return this.ColumnNames.IndexOf(columnName);
        }

        // value function with a column name
        public string GetValue(string columnName, int row)
        {
            return this.GetValue(this.GetColumnIndex(columnName), row);
        }

        private void Clear()
        {
            this.mRows.Clear();
            this.ColumnNames.Clear();
        }

        public bool LoadFile(string fileName)
        {
            using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            if (String.IsNullOrEmpty(content))
            {
                //Debug.WriteLine("loadFile: " + fileName + " does not exist or is empty.");
                return false;
            }
            return this.LoadFromString(content);
        }

        public bool LoadFromString(string content)
        {
            this.Clear();
            // split into rows: use either with windows or unix style delimiter
            string[] rows;
            ReadOnlySpan<char> contentSampleForNewlineDetection = content.AsSpan(0, Math.Min(content.Length, 1000));
            if (contentSampleForNewlineDetection.Contains("\r\n", StringComparison.Ordinal))
            {
                rows = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                rows = content.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            }

            if (rows.Length == 0)
            {
                return false;
            }

            // trimming of whitespaces is a problem
            // when having e.g. tabs as delimiters...
            //    if (!mFixedWidth) {
            //        for (int i=0;i<mRows.count();i++)
            //            mRows[i] = mRows[i].trimmed();
            //    }
            // drop comments (i.e. lines at the beginning that start with '#', also ignore '<' (are in tags of picus-ini-files)
            mRows.Capacity = rows.Length;
            foreach (string row in rows)
            {
                if (String.IsNullOrWhiteSpace(row) || row[0] == '#' || row[0] == '<')
                {
                    continue;
                }
                mRows.Add(row);
            }

            mSeparator = ';'; // default
            string first = mRows[0];

            // detect separator
            int tabIndex = first.IndexOf('\t');
            int semicolonIndex = first.IndexOf(';');
            int commaIndex = first.IndexOf(',');
            int spaceIndex = first.IndexOf(' ');
            if (tabIndex < 0 && semicolonIndex < 0 && commaIndex < 0 && spaceIndex < 0)
            {
                throw new NotSupportedException("Cannot recognize separator. first line: " + first);
            }

            mSeparator = ' ';
            if (tabIndex != -1)
            {
                mSeparator = '\t';
            }
            if (semicolonIndex != -1)
            {
                mSeparator = ';';
            }
            if (commaIndex != -1)
            {
                mSeparator = ',';
            }

            // captions
            this.ColumnNames.AddRange(first.Replace("\"", String.Empty).Split(mSeparator, StringSplitOptions.None)); // drop \ characters
            return true;
        }

        public List<string> GetRow(int rowIndex)
        {
            List<string> line = new List<string>(this.ColumnCount);
            line.AddRange(mRows[rowIndex].Split(mSeparator, StringSplitOptions.None));
            return line;
        }

        // TODO: remove and convert callers to GetRow()
        public string GetValue(int column, int row)
        {
            if (column < 0 || column >= this.ColumnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
            if (row < 0 || row >= this.RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            string[] line = mRows[row].Split(mSeparator);
            return line[column];
        }

        //public string Row(int row)
        //{
        //    return mRows[row];
        //}

        public List<string> GetColumnValues(int columnIndex)
        {
            List<string> result = new List<string>(this.RowCount);
            for (int row = 0; row < this.RowCount; ++row)
            {
                result.Add(this.GetValue(columnIndex, row));
            }
            return result;
        }

        //public void SetValue(int row, int col, object value)
        //{
        //    if (row < 0 || row >= this.RowCount || col < 0 || col > this.ColumnCount)
        //    {
        //        throw new ArgumentOutOfRangeException("Invalid index: row col:" + row + col + ". Size is " + this.RowCount + " rows, " + this.ColumnCount + " columns.");
        //    }
        //    string[] line = mRows[row].Split(mSeparator);
        //    if (col < line.Length)
        //    {
        //        // TODO: input ignored if past end of line
        //        line[col] = value.ToString();
        //    }
        //    mRows[row] = String.Join(mSeparator, line);
        //}

        /// save the contents of the CSVFile back to a file.
        /// this removes all comments and uses the system line-end
        //public void SaveFile(string fileName)
        //{
        //    using FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        //    using StreamWriter str = new StreamWriter(file);
        //    if (HasColumnNames)
        //    {
        //        str.WriteLine(String.Join(mSeparator, ColumnNames));
        //    }
        //    foreach (string s in mRows)
        //    {
        //        str.WriteLine(s);
        //    }
        //}
    }
}
