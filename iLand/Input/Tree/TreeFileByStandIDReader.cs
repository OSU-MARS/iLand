using System;
using System.Collections.Generic;

namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDReader : TreeReader
    {
        public List<(int StandID, string TreeFileName)> TreeFileNameByStandID { get; private init; }

        protected TreeFileByStandIDReader(string treeFilePath)
            : base(treeFilePath)
        {
            TreeFileNameByStandID = new();
        }

        public TreeFileByStandIDReader(string treeFilePath, TreeFileByStandIDHeader treeFileIndexHeader, CsvFile treeFile)
            : this(treeFilePath)
        {
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
                if (string.IsNullOrWhiteSpace(treeFileName) == false)
                {
                    TreeFileNameByStandID.Add((standID, treeFileName));
                }
            });
        }
    }
}
