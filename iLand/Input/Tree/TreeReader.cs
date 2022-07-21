using System;
using System.IO;

namespace iLand.Input.Tree
{
    public class TreeReader
    {
        public string FilePath { get; private init; }

        protected TreeReader(string treeFilePath)
        {
            this.FilePath = treeFilePath;
        }

        public static TreeReader Create(string treeFilePath)
        {
            string treeFileExtension = Path.GetExtension(treeFilePath);
            switch (treeFileExtension)
            {
                case Constant.File.CsvExtension:
                case Constant.File.PicusExtension:
                    CsvFile treeFile = new(treeFilePath);
                    IndividualTreeCsvHeader individualTreeHeader = new(treeFile);
                    TreeSizeDistributionCsvHeader treeSizeHeader = new(treeFile);
                    TreeFileByStandIDCsvHeader treeFileIndexHeader = new(treeFile);

                    int eligibleFileTypes = (individualTreeHeader.CanBeIndividualTreeFile ? 1 : 0) + (treeSizeHeader.CanBeSizeDistributionFile ? 1 : 0) + (treeFileIndexHeader.CanBeIndexFile ? 1 : 0);
                    if (eligibleFileTypes != 1)
                    {
                        throw new NotSupportedException("Unable to autodetect format of tree file '" + treeFilePath + "'. Known formats are 1) individual trees (required columns are x, y, bhdfrom or dbh, species, and treeheight or height), 2) tree size distribution (columns are count, species, dbhFrom, dbhTo, hdRatio, and age), and 3) a list of other tree files by stand ID (columns are 'standID' and 'fileName').");
                    }

                    if (individualTreeHeader.CanBeIndividualTreeFile)
                    {
                        return new IndividualTreeReaderCsv(treeFilePath, individualTreeHeader, treeFile);
                    }
                    else if (treeSizeHeader.CanBeSizeDistributionFile)
                    {
                        return new TreeSizeDistributionReaderCsv(treeFilePath, treeSizeHeader, treeFile);
                    }
                    else if (treeFileIndexHeader.CanBeIndexFile)
                    {
                        return new TreeFileByStandIDReaderCsv(treeFilePath, treeFileIndexHeader, treeFile);
                    }
                    else
                    {
                        throw new NotSupportedException("Unhandled format for tree file '" + treeFilePath + "'.");
                    }
                case Constant.File.FeatherExtension:
                    // for now, assume .feather files are always individual tree files
                    return new IndividualTreeReaderFeather(treeFilePath);
                default:
                    throw new NotSupportedException("Unhandled extension '" + treeFileExtension + "' for tree file '" + treeFilePath + ".");
            }
        }
    }
}
