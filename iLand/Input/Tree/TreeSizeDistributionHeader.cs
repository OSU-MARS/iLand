using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iLand.Input.Tree
{
    internal class TreeSizeDistributionHeader
    {
        public int Count { get; private init; }
        public int Species { get; private init; }
        public int MinimumDbh { get; private init; }
        public int MaximumDbh { get; private init; }
        public int HeightDiameterRatio { get; private init; }
        public int Age { get; private init; }
        public int Density { get; private init; } // optional

        public bool CanBeSizeDistributionFile { get; private init; }

        public TreeSizeDistributionHeader(CsvFile treeFile)
        {
            Count = treeFile.GetColumnIndex("count");
            Species = treeFile.GetColumnIndex("species");
            MinimumDbh = treeFile.GetColumnIndex("dbhFrom");
            MaximumDbh = treeFile.GetColumnIndex("dbhTo");
            HeightDiameterRatio = treeFile.GetColumnIndex("hdRatio");
            Age = treeFile.GetColumnIndex("age");
            Density = treeFile.GetColumnIndex("density");

            int columnCount = treeFile.Columns.Count;
            CanBeSizeDistributionFile = columnCount >= 6 && columnCount <= 7 && Count >= 0 &&
                Species >= 0 && MinimumDbh >= 0 && MaximumDbh >= 0 && HeightDiameterRatio >= 0 && Age >= 0;
        }
    }
}
