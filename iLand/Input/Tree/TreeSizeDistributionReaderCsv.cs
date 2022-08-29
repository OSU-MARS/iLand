using iLand.Extensions;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace iLand.Input.Tree
{
    internal class TreeSizeDistributionReaderCsv : TreeReader
    {
        public List<TreeSizeRange> TreeSizeDistribution { get; private init; }

        public TreeSizeDistributionReaderCsv(string treeFilePath, TreeSizeDistributionCsvHeader treeSizeHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            this.TreeSizeDistribution = new();

            int lineNumber = 0;
            string? mostRecentSpeciesAbbreviation = null;
            WorldFloraID mostRecentSpeciesID = WorldFloraID.Unknown;
            treeFile.Parse((row) =>
            {
                ReadOnlySpan<char> species = row[treeSizeHeader.Species];
                if (MemoryExtensions.Equals(species, mostRecentSpeciesAbbreviation, StringComparison.OrdinalIgnoreCase) == false)
                {
                    mostRecentSpeciesAbbreviation = species.ToString();
                    mostRecentSpeciesID = WorldFloraIDExtensions.Parse(mostRecentSpeciesAbbreviation);
                }

                TreeSizeRange sizeRange = new(mostRecentSpeciesID)
                {
                    TreesPerResourceUnit = Int32.Parse(row[treeSizeHeader.Count], NumberStyles.Integer),
                    DbhFrom = Single.Parse(row[treeSizeHeader.MinimumDbh], NumberStyles.Float),
                    DbhTo = Single.Parse(row[treeSizeHeader.MaximumDbh], NumberStyles.Float),
                    HeightDiameterRatio = Single.Parse(row[treeSizeHeader.HeightDiameterRatio], NumberStyles.Float)
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
                    if (UInt16.TryParse(row[treeSizeHeader.Age], out UInt16 age))
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
                    sizeRange.Density = Single.Parse(row[treeSizeHeader.Density], NumberStyles.Float);
                }
                else
                {
                    sizeRange.Density = 0.0F;
                }
                if (sizeRange.Density < -1)
                {
                    throw new NotSupportedException("Invalid density " + sizeRange.Density + " in file '" + treeFilePath + "', line " + lineNumber + ". Allowed range is -1..1.");
                }

                this.TreeSizeDistribution.Add(sizeRange);
            });
        }
    }
}
