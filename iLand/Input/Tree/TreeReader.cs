using System;

namespace iLand.Input.Tree
{
    internal class TreeReader
    {
        public string Path { get; private init; }

        protected TreeReader(string treeFilePath)
        {
            Path = treeFilePath;
        }

        public static TreeReader Create(string treeFilePath)
        {
            CsvFile treeFile = new(treeFilePath);
            IndividualTreeDataIndex individualTreeHeader = new(treeFile);
            TreeSizeDistributionDataIndex treeSizeHeader = new(treeFile);
            TreeFileByStandIDDataIndex treeFileIndexHeader = new(treeFile);

            int eligibleFileTypes = (individualTreeHeader.CanBeIndividualTreeFile ? 1 : 0) + (treeSizeHeader.CanBeSizeDistributionFile ? 1 : 0) + (treeFileIndexHeader.CanBeIndexFile ? 1 : 0);
            if (eligibleFileTypes != 1)
            {
                throw new NotSupportedException("Unable to autodetect format of tree file '" + treeFilePath + "'. Known formats are 1) individual trees (required columns are x, y, bhdfrom or dbh, species, and treeheight or height), 2) tree size distribution (columns are count, species, dbhFrom, dbhTo, hdRatio, and age), and 3) a list of other tree files by stand ID (columns are 'standID' and 'fileName').");
            }

            if (individualTreeHeader.CanBeIndividualTreeFile)
            {
                return new IndividualTreeReader(treeFilePath, individualTreeHeader, treeFile);
            }
            else if (treeSizeHeader.CanBeSizeDistributionFile)
            {
                return new TreeSizeDistributionReader(treeFilePath, treeSizeHeader, treeFile);
            }
            else if (treeFileIndexHeader.CanBeIndexFile)
            {
                return new TreeFileByStandIDReader(treeFilePath, treeFileIndexHeader, treeFile);
            }
            else
            {
                throw new NotSupportedException("Unhandled format for tree file '" + treeFilePath + "'.");
            }
        }
    }
}
