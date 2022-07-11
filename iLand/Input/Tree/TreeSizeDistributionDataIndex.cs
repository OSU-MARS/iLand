using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iLand.Input.Tree
{
    internal class TreeSizeDistributionDataIndex
    {
        public int Count { get; private init; }
        public int Species { get; private init; }
        public int MinimumDbh { get; private init; }
        public int MaximumDbh { get; private init; }
        public int HeightDiameterRatio { get; private init; }
        public int Age { get; private init; }
        public int Density { get; private init; } // optional

        public bool CanBeSizeDistributionFile { get; private init; }

        public TreeSizeDistributionDataIndex(CsvFile treeFile)
        {
            this.Count = treeFile.GetColumnIndex("count");
            this.Species = treeFile.GetColumnIndex("species");
            this.MinimumDbh = treeFile.GetColumnIndex("dbhFrom");
            this.MaximumDbh = treeFile.GetColumnIndex("dbhTo");
            this.HeightDiameterRatio = treeFile.GetColumnIndex("hdRatio");
            this.Age = treeFile.GetColumnIndex("age");
            this.Density = treeFile.GetColumnIndex("density");

            int columnCount = treeFile.Columns.Count;
            this.CanBeSizeDistributionFile = columnCount >= 6 && columnCount <= 7 && this.Count >= 0 &&
                this.Species >= 0 && this.MinimumDbh >= 0 && this.MaximumDbh >= 0 && this.HeightDiameterRatio >= 0 && this.Age >= 0;
        }
    }
}
