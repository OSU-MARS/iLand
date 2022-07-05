using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDHeader
    {
        public int StandID { get; private init; }
        public int TreeFileName { get; private init; }

        public bool CanBeIndexFile { get; private init; }

        public TreeFileByStandIDHeader(CsvFile treeFile)
        {
            StandID = treeFile.GetColumnIndex("standID");
            TreeFileName = treeFile.GetColumnIndex("treeFileName");

            CanBeIndexFile = treeFile.Columns.Count == 2 && StandID >= 0 && TreeFileName >= 0;
        }
    }
}
