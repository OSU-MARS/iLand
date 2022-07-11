using System;
using System.Collections.Generic;

namespace iLand.Input.Tree
{
    internal class TreeSizeDistributionReader : TreeReader
    {
        public List<TreeSizeRange> TreeSizeDistribution { get; private init; }

        public TreeSizeDistributionReader(string treeFilePath, TreeSizeDistributionDataIndex treeSizeHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            this.TreeSizeDistribution = new();

            int lineNumber = 0;
            treeFile.Parse((row) =>
            {
                TreeSizeRange sizeRange = new(row[treeSizeHeader.Species])
                {
                    Count = float.Parse(row[treeSizeHeader.Count]),
                    DbhFrom = float.Parse(row[treeSizeHeader.MinimumDbh]),
                    DbhTo = float.Parse(row[treeSizeHeader.MaximumDbh]),
                    HeightDiameterRatio = float.Parse(row[treeSizeHeader.HeightDiameterRatio])
                };
                ++lineNumber;

                if (sizeRange.HeightDiameterRatio == 0.0 || sizeRange.DbhFrom / 100.0 * sizeRange.HeightDiameterRatio < Constant.Sapling.MaximumHeight)
                {
                    throw new NotSupportedException("File '" + treeFilePath + "' tries to init trees below 4 m height at line " + lineNumber + ". Height-diameter ratio = " + sizeRange.HeightDiameterRatio + ", DBH = " + sizeRange.DbhFrom + ".");
                }

                // TODO: DbhFrom < DbhTo?
                //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                bool setAgeToZero = true;
                if (treeSizeHeader.Age >= 0)
                {
                    if (int.TryParse(row[treeSizeHeader.Age], out int age))
                    {
                        setAgeToZero = false;
                        sizeRange.Age = age;
                    }
                }
                if (setAgeToZero)
                {
                    sizeRange.Age = 0;
                }

                if (treeSizeHeader.Density >= 0)
                {
                    sizeRange.Density = float.Parse(row[treeSizeHeader.Density]);
                }
                else
                {
                    sizeRange.Density = 0.0F;
                }
                if (sizeRange.Density < -1)
                {
                    throw new NotSupportedException("Invalid density " + sizeRange.Density + " in file '" + treeFilePath + "', line " + lineNumber + ". Allowed range is -1..1.");
                }

                if (string.IsNullOrEmpty(sizeRange.TreeSpecies))
                {
                    throw new NotSupportedException("Missing species in file '" + treeFilePath + ", line " + lineNumber + ".");
                }

                this.TreeSizeDistribution.Add(sizeRange);
            });
        }
    }
}
