using System;
using System.Collections.Generic;
using System.Globalization;

namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDReaderCsv : TreeReader
    {
        public List<(UInt32 StandID, string TreeFileName)> TreeFileNameByStandID { get; private init; }

        public TreeFileByStandIDReaderCsv(string treeFilePath, TreeFileByStandIDCsvHeader treeFileIndexHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            this.TreeFileNameByStandID = [];

            int lineNumber = 0;
            treeFile.Parse((row) =>
            {
                ++lineNumber;
                UInt32 standID = UInt32.Parse(row[treeFileIndexHeader.StandID], NumberStyles.Integer);
                ReadOnlySpan<char> treeFileName = row[treeFileIndexHeader.TreeFileName];
                if (MemoryExtensions.IsWhiteSpace(treeFileName) == false)
                {
                    this.TreeFileNameByStandID.Add((standID, treeFileName.ToString()));
                }
            });
        }
    }
}
