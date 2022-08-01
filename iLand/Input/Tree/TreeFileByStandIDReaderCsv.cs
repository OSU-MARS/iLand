using System;
using System.Collections.Generic;
using System.Globalization;

namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDReaderCsv : TreeReader
    {
        public List<(int StandID, string TreeFileName)> TreeFileNameByStandID { get; private init; }

        public TreeFileByStandIDReaderCsv(string treeFilePath, TreeFileByStandIDCsvHeader treeFileIndexHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            this.TreeFileNameByStandID = new();

            int lineNumber = 0;
            treeFile.Parse((row) =>
            {
                ++lineNumber;
                int standID = Int32.Parse(row[treeFileIndexHeader.StandID], CultureInfo.InvariantCulture);
                string treeFileName = row[treeFileIndexHeader.TreeFileName];
                if (String.IsNullOrWhiteSpace(treeFileName) == false)
                {
                    this.TreeFileNameByStandID.Add((standID, treeFileName));
                }
            });
        }
    }
}
