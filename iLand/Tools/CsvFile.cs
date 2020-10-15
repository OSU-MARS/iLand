using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace iLand.Tools
{
    /** @class CSVFile
        @ingroup tools
        Provides access to table data stored in text files (CSV style).
        Tables have optionally headers in first line (hasCaptions()) and can use various
        delimiters ("tab",";",","," "). If separated by spaces, consecuteive spaces are merged.
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
        private string mSeparator;

        // properties
        public bool StreamingMode { get; private set; } ///< return true, if in "streaming mode" (for large files)
        public bool HasCaptions { get; set; } ///< true, if first line contains headers
        public bool Flat { get; set; } ///< simple list, not multiple columns
        public int RowCount { get; private set; } ///< number or rows (excl. captions), or -1.
        public int ColCount { get; private set; } ///< number of columns, or -1
        public bool IsEmpty { get; private set; } /// returns true when no valid file has been loaded (returns false when a file with 0 rows is loaded)
        public List<string> Captions { get; private set; } ///< retrieve (a copy) of column headers
        public bool FixedWidth { get; set; }

        ///< get caption of ith column.
        public string GetColumnName(int col) 
        {
            if (col < ColCount)
            {
                return Captions[col];
            }
            return null; 
        }

        ///< index of column or -1 if not available
        public int GetColumnIndex(string columnName) 
        { 
            return Captions.IndexOf(columnName); 
        } 

        // value function with a column name
        public string Value(int row, string column_name) 
        { 
            return Value(row, GetColumnIndex(column_name)); 
        }

        ///< ctor, load @p fileName.
        public CsvFile(string fileName)
            : this()
        {
            HasCaptions = true;
            LoadFile(fileName);
        }

        public CsvFile()
        {
            Captions = new List<string>();
            IsEmpty = true;
            HasCaptions = true;
            Flat = false;
            FixedWidth = false;
            mRows = new List<string>();
            Clear();
        }

        private void Clear()
        {
            ColCount = RowCount = -1;
            Captions.Clear();
            mRows.Clear();
            IsEmpty = true;
        }

        public bool LoadFromString(string content)
        {
            Clear();
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

            IsEmpty = false;
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

            mSeparator = ";"; // default
            string first = mRows[0];
            if (!Flat)
            {
                // detect separator
                int c_tab = first.IndexOf('\t');
                int c_semi = first.IndexOf(';');
                int c_comma = first.IndexOf(',');
                int c_space = first.IndexOf(' ');
                if (c_tab == -1 && c_semi == -1 && c_comma == -1 && c_space == -1)
                {
                    Debug.WriteLine("loadFile: cannot recognize separator. first line: " + first);
                    return false;
                }

                mSeparator = " ";
                if (c_tab != -1)
                {
                    mSeparator = "\t";
                }
                if (c_semi != -1)
                {
                    mSeparator = ";";
                }
                if (c_comma != -1)
                {
                    mSeparator = ",";
                }
            } // !mFlat

            // captions
            if (HasCaptions)
            {
                Captions.AddRange(first.Replace("\"", "").Split(mSeparator, StringSplitOptions.None)); // drop \ characters
            }
            else
            {
                // create pseudo captions
                int columns = first.Split(mSeparator, StringSplitOptions.None).Length;
                for (int i = 0; i < columns; i++)
                {
                    Captions.Add(i.ToString());
                }
            }

            ColCount = Captions.Count;
            RowCount = mRows.Count;
            StreamingMode = false;
            return true;
        }

        public bool LoadFile(string fileName)
        {
            string content = File.ReadAllText(fileName);
            if (String.IsNullOrEmpty(content))
            {
                Debug.WriteLine("loadFile: " + fileName + " does not exist or is empty.");
                IsEmpty = true;
                return false;
            }
            return LoadFromString(content);
        }

        public List<object> Values(int row)
        {
            List<object> line = new List<object>();
            line.AddRange(mRows[row].Split(mSeparator));
            return line;
        }

        public string Value(int row, int col)
        {
            if (StreamingMode)
            {
                return null;
            }

            if (row < 0 || row >= RowCount || col < 0 || col >= ColCount)
            {
                Debug.WriteLine("value: invalid index: row col: " + row + col + ". Size is:" + RowCount + ColCount);
                return null;
            }

            if (FixedWidth)
            {
                // special case with space (1..n) as separator
                string s = mRows[row];
                char sep = mSeparator[0];
                if (col == ColCount - 1)
                {
                    // last element:
                    return s.Substring(s.LastIndexOf(sep) + 1);
                }
                int sepcount = 0;
                int lastsep = 0;
                int i = 0;
                while (s[i] == sep && i < s.Length)
                {
                    i++; // skip initial spaces
                }
                for (; i < s.Length; i++)
                {
                    if (s[i] == sep)
                    {
                        // skip all spaces
                        while (s[i] == sep)
                        {
                            i++;
                        }
                        i--; // go back to last separator
                             // count the separators up to the wanted column
                        if (sepcount == col)
                        {
                            return s[lastsep..i];
                        }
                        sepcount++;
                        lastsep = i + 1;
                    }
                }
                Debug.WriteLine("value: found no result: row " + row + " column " + col + ". Size is:" + RowCount + ColCount);
                return null;
            }

            // one-character separators....
            if (mSeparator.Length == 1)
            {
                string s = mRows[row];
                char sep = mSeparator[0];
                string result = null;
                if (col == ColCount - 1)
                {
                    // last element:
                    if (s.Count(character => character == sep) == ColCount - 1)
                    {
                        result = s.Substring(s.LastIndexOf(sep) + 1);
                        if (result.StartsWith('\"') && result.EndsWith('\"'))
                        {
                            result = result[1..^1];
                        }
                    }
                    // if there are less than colcount-1 separators, then
                    // the last columns is empty
                    return result;
                }

                int sepcount = 0;
                int lastsep = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == sep)
                    {
                        // count the separators up to the wanted column
                        if (sepcount == col)
                        {
                            if (s[lastsep] == '\"' && s[i - 1] == '\"')
                            {
                                result = s.Substring(lastsep + 1, i - lastsep - 2); // ignore "
                            }
                            else
                            {
                                result = s[lastsep..i];
                            }
                            return result;
                        }
                        sepcount++;
                        lastsep = i + 1;
                    }
                }
                if (sepcount == col)
                {
                    result = s.Substring(s.LastIndexOf(sep) + 1);
                }
                //Debug.WriteLine("value: found no result:" + row + col + ". Size is:" + mRowCount + mColCount;
                return result;
            }

            // fallback, if separator is more than one character. This is very slow approach.... (old)
            string[] line = mRows[row].Split(mSeparator);
            if (col < line.Length)
            {
                return line[col];
            }
            return null;
        }

        public string Row(int row)
        {
            if (StreamingMode)
            {
                return null;
            }

            if (row < 0 || row >= RowCount)
            {
                Debug.WriteLine("row: invalid index: row " + row + ". Size is:" + RowCount);
                return null;
            }

            return mRows[row];
        }

        public List<string> Column(int col)
        {
            List<string> result = new List<string>();
            for (int row = 0; row < RowCount; row++)
            {
                result.Add(Value(row, col));
            }
            return result;
        }

        public void SetValue(int row, int col, object value)
        {
            if (row < 0 || row >= RowCount || col < 0 || col > ColCount)
            {
                Debug.WriteLine("setValue: invalid index: row col:" + row + col + ". Size is: " + RowCount + " rows, " + ColCount + " columns");
                return;
            }
            string[] line = mRows[row].Split(mSeparator);
            if (col < line.Length)
            {
                // BUGBUG: input ignored if past end of line
                line[col] = value.ToString();
            }
            mRows[row] = String.Join(mSeparator, line);
        }

        /// save the contents of the CSVFile back to a file.
        /// this removes all comments and uses the system line-end
        public void SaveFile(string fileName)
        {
            using FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using StreamWriter str = new StreamWriter(file);
            if (HasCaptions)
            {
                str.WriteLine(String.Join(mSeparator, Captions));
            }
            foreach (string s in mRows)
            {
                str.WriteLine(s);
            }
        }
    }
}
