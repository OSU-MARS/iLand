using System;
using System.Collections.Generic;

namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDReader : TreeReader
    {
        public List<(int StandID, string TreeFileName)> TreeFileNameByStandID { get; private init; }

        public TreeFileByStandIDReader(string treeFilePath, TreeFileByStandIDDataIndex treeFileIndexHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            this.TreeFileNameByStandID = new();

            int lineNumber = 0;
            treeFile.Parse((row) =>
            {
                ++lineNumber;
                int standID = int.Parse(row[treeFileIndexHeader.StandID]);
                if (standID < Constant.DefaultStandID)
                {
                    throw new NotSupportedException("Stand IDs must be zero or greater (ID " + standID + " in line " + lineNumber + " of '" + treeFilePath + "').");
                }

                string treeFileName = row[treeFileIndexHeader.TreeFileName];
                if (String.IsNullOrWhiteSpace(treeFileName) == false)
                {
                    this.TreeFileNameByStandID.Add((standID, treeFileName));
                }
            });
        }
    }
}
