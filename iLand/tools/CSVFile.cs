using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace iLand.tools
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
    internal class CSVFile
    {
        private bool mIsEmpty;
        private bool mHasCaptions;
        private bool mFixedWidth;
        private bool mFlat;
        private bool mStreamingMode;
        private List<string> mCaptions;
        private List<string> mRows;
        private string mSeparator;
        private int mRowCount;
        private int mColCount;

        // properties
        public bool streamingMode() { return mStreamingMode; } ///< return true, if in "streaming mode" (for large files)
        public bool hasCaptions() { return mHasCaptions; } ///< true, if first line contains headers
        public bool flat() { return mFlat; } ///< simple list, not multiple columns
        public int rowCount() { return mRowCount; } ///< number or rows (excl. captions), or -1.
        public int colCount() { return mColCount; } ///< number of columns, or -1
        public bool isEmpty() { return mIsEmpty; } /// returns true when no valid file has been loaded (returns false when a file with 0 rows is loaded)
        public List<string> captions() { return mCaptions; } ///< retrieve (a copy) of column headers
        public void setHasCaptions(bool hasCaps) { mHasCaptions = hasCaps; }
        public void setFixedWidth(bool hasFixedWidth) { mFixedWidth = hasFixedWidth; }
        public void setFlat(bool isflat) { mFlat = isflat; }
        ///< get caption of ith column.
        public string columnName(int col) 
        {
            if (col < mColCount)
            {
                return mCaptions[col];
            }
            return null; 
        }

        ///< index of column or -1 if not available
        public int columnIndex(string columnName) 
        { 
            return mCaptions.IndexOf(columnName); 
        } 

        // value function with a column name
        public string value(int row, string column_name) 
        { 
            return value(row, columnIndex(column_name)); 
        }

        public static void addToScriptEngine(QJSEngine engine)
        {
            // remove this code?
            // about this kind of scripting magic see: http://qt.nokia.com/developer/faqs/faq.2007-06-25.9557303148
            //QJSValue cc_class = engine.scriptValueFromQMetaObject<CSVFile>();

            // TODO: solution for creating objects (on the C++ side...)

            // the script name for the object is "CSVFile".
            //engine.globalObject().setProperty("CSVFile", cc_class);
        }

        ///< ctor, load @p fileName.
        public CSVFile(string fileName)
            : this()
        {
            mHasCaptions = true;
            loadFile(fileName);
        }

        public CSVFile(object parent = null)
        {
            mCaptions = new List<string>();
            mIsEmpty = true;
            mHasCaptions = true;
            mFlat = false;
            mFixedWidth = false;
            mRows = new List<string>();
            clear();
        }

        private void clear()
        {
            mColCount = mRowCount = -1;
            mCaptions.Clear();
            mRows.Clear();
            mIsEmpty = true;
        }

        public bool loadFromString(string content)
        {
            clear();
            // split into rows: use either with windows or unix style delimiter
            string[] rows;
            if (content.Substring(0, 1000).Contains("\r\n", StringComparison.Ordinal))
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

            mIsEmpty = false;
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
            if (!mFlat)
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
            if (mHasCaptions)
            {
                mCaptions.AddRange(first.Replace("\"", "").Split(mSeparator, StringSplitOptions.None)); // drop \ characters
            }
            else
            {
                // create pseudo captions
                int columns = first.Split(mSeparator, StringSplitOptions.None).Length;
                for (int i = 0; i < columns; i++)
                {
                    mCaptions.Add(i.ToString());
                }
            }

            mColCount = mCaptions.Count;
            mRowCount = mRows.Count;
            mStreamingMode = false;
            return true;
        }

        public bool loadFile(string fileName)
        {
            string content = Helper.loadTextFile(fileName);
            if (String.IsNullOrEmpty(content))
            {
                Debug.WriteLine("loadFile: " + fileName + " does not exist or is empty.");
                mIsEmpty = true;
                return false;
            }
            return loadFromString(content);
        }

        public List<object> values(int row)
        {
            List<object> line = new List<object>();
            line.AddRange(mRows[row].Split(mSeparator));
            return line;
        }

        public string value(int row, int col)
        {
            if (mStreamingMode)
            {
                return null;
            }

            if (row < 0 || row >= mRowCount || col < 0 || col >= mColCount)
            {
                Debug.WriteLine("value: invalid index: row col: " + row + col + ". Size is:" + mRowCount + mColCount);
                return null;
            }

            if (mFixedWidth)
            {
                // special case with space (1..n) as separator
                string s = mRows[row];
                char sep = mSeparator[0];
                if (col == mColCount - 1)
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
                            return s.Substring(lastsep, i - lastsep);
                        }
                        sepcount++;
                        lastsep = i + 1;
                    }
                }
                Debug.WriteLine("value: found no result: row " + row + " column " + col + ". Size is:" + mRowCount + mColCount);
                return null;
            }

            // one-character separators....
            if (mSeparator.Length == 1)
            {
                string s = mRows[row];
                char sep = mSeparator[0];
                string result = null;
                if (col == mColCount - 1)
                {
                    // last element:
                    if (s.Count(character => character == sep) == mColCount - 1)
                    {
                        result = s.Substring(s.LastIndexOf(sep) + 1);
                        if (result.StartsWith('\"') && result.EndsWith('\"'))
                        {
                            result = result.Substring(1, result.Length - 2);
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
                                result = s.Substring(lastsep, i - lastsep);
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

        public string row(int row)
        {
            if (mStreamingMode)
            {
                return null;
            }

            if (row < 0 || row >= mRowCount)
            {
                Debug.WriteLine("row: invalid index: row " + row + ". Size is:" + mRowCount);
                return null;
            }

            return mRows[row];
        }

        public bool openFile(string fileName)
        {
            // TODO: the function makes no sense, nonetheless.
            mStreamingMode = true;
            return false;
        }

        public List<string> column(int col)
        {
            List<string> result = new List<string>();
            for (int row = 0; row < rowCount(); row++)
            {
                result.Add(value(row, col));
            }
            return result;
        }

        public void setValue(int row, int col, object value)
        {
            if (row < 0 || row >= mRowCount || col < 0 || col > mColCount)
            {
                Debug.WriteLine("setValue: invalid index: row col:" + row + col + ". Size is: " + mRowCount + " rows, " + mColCount + " columns");
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
        public void saveFile(string fileName)
        {
            using FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using StreamWriter str = new StreamWriter(file);
            if (mHasCaptions)
            {
                str.WriteLine(String.Join(mSeparator, mCaptions));
            }
            foreach (string s in mRows)
            {
                str.WriteLine(s);
            }
        }
    }
}
