using System;
using System.Collections.Generic;
using System.IO;

namespace iLand.Input
{
    /// <summary>
    /// Provides access to table data stored in delimitedtext files (CSV style). First line is a header and commas, tabs, spaces, and semicolons 
    /// can be used as delimiters.
    /// </summary>
    public class CsvFile : IDisposable
    {
        private bool isDisposed;
        private readonly char separator;
        private readonly StreamReader reader;

        public List<string> Columns { get; private init; }

        public CsvFile(string filePath)
        {
            FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            reader = new(stream);

            // get header line
            // Skip <trees> at beginning of Picus tree files.
            string? header = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(header))
            {
                throw new FileLoadException("File '" + filePath + "' is empty.");
            }
            if (String.Equals(header, "<trees>", StringComparison.Ordinal))
            {
                header = reader.ReadLine();
                if (header == null)
                {
                    throw new NotSupportedException("Picus tree file '" + filePath + "' is empty.");
                }
            }

            // detect separator
            int tabIndex = header.IndexOf('\t');
            int semicolonIndex = header.IndexOf(';');
            int commaIndex = header.IndexOf(',');
            int spaceIndex = header.IndexOf(' ');
            if (tabIndex != -1)
            {
                separator = '\t';
            }
            else if (semicolonIndex != -1)
            {
                separator = ';';
            }
            else if (commaIndex != -1)
            {
                separator = ',';
            }
            else if (spaceIndex != -1)
            {
                separator = ' ';
            }
            else
            {
                throw new NotSupportedException("Field separator for file '" + filePath + "' is not a comma, tab, semicolon, or space. Header line: " + header);
            }

            // parse header
            Columns = new();
            Columns.AddRange(header.Split(separator, StringSplitOptions.None)); // C++ iLand removes \ characters here for an undocumented reason
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    reader.Dispose(); // disposes underlying stream
                }

                isDisposed = true;
            }
        }

        // index of column or -1 if not available
        public int GetColumnIndex(string columnName)
        {
            return this.Columns.IndexOf(columnName);
        }

        public void Parse(Action<string[]> parseRow)
        {
            for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                // skip
                //   empty lines
                //   comments (lines beginning with '#')
                //   Picus closing </trees>
                if (String.IsNullOrWhiteSpace(line) || line[0] == '#' || String.Equals(line, "</trees>", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // should whitespace be trimmed? repeated spaces treated as a single delimiter?
                string[] row = line.Split(separator, StringSplitOptions.None);
                parseRow.Invoke(row);
            }
        }

        /// save the contents of the CSVFile back to a file.
        /// this removes all comments and uses the system line-end
        //public void SaveFile(string fileName)
        //{
        //    using FileStream file = new(fileName, FileMode.Create, FileAccess.Write);
        //    using StreamWriter str = new(file);
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
