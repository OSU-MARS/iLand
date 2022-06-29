using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iLand.Input
{
    internal class TreeFileByStandID
    {
        public int StandID { get; private init; }
        public int TreeFileName { get; private init; }

        public bool CanBeIndexFile { get; private init; }

        public TreeFileByStandID(CsvFile treeFile)
        {
            this.StandID = treeFile.GetColumnIndex("standID");
            this.TreeFileName = treeFile.GetColumnIndex("treeFileName");

            this.CanBeIndexFile = (treeFile.Columns.Count == 2) && (this.StandID >= 0) && (this.TreeFileName >= 0);
        }
    }
}
