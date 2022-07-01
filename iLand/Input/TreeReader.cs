using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.Input
{
    internal class TreeReader
    {
        // provide a mapping between "Picus"-style and "iLand"-style species Ids
        // TODO: if needed, expand species support
        private static readonly ReadOnlyCollection<string> iLandSpeciesIDs = new List<string>() { "piab", "piab", "fasy" }.AsReadOnly();
        private static readonly ReadOnlyCollection<int> PicusSpeciesIDs = new List<int>() { 0, 1, 17 }.AsReadOnly();

        public List<int> IndividualAge { get; private init; }
        public List<float> IndividualDbhInCM { get; private init; }
        public List<float> IndividualHeightInM { get; private init; }
        public List<string> IndividualSpeciesID { get; private init; }
        public List<int> IndividualStandID { get; private init; }
        public List<int> IndividualTag { get; private init; }
        public List<float> IndividualGisX { get; private init; }
        public List<float> IndividualGisY { get; private init; }

        public string Path { get; private init; }

        public List<(int StandID, string TreeFileName)> TreeFileNameByStandID { get; private init; }
        public List<TreeSizeRange> TreeSizeDistribution { get; private init; }

        public TreeReader(string treeFilePath)
        {
            CsvFile treeFile = new(treeFilePath);
            IndividualTreeHeader individualTreeHeader = new(treeFile);
            TreeSizeDistributionHeader treeSizeHeader = new(treeFile);
            TreeFileByStandID treeFileIndexHeader = new(treeFile);

            int eligibleFileTypes = (individualTreeHeader.CanBeIndividualTreeFile ? 1 : 0) + (treeSizeHeader.CanBeSizeDistributionFile ? 1 : 0) + (treeFileIndexHeader.CanBeIndexFile ? 1 : 0);
            if (eligibleFileTypes != 1)
            {
                throw new NotSupportedException("Unable to autodetect format of tree file '" + treeFilePath + "'. Known formats are 1) individual trees (required columns are x, y, bhdfrom or dbh, species, and treeheight or height), 2) tree size distribution (columns are count, species, dbhFrom, dbhTo, hdRatio, and age), and 3) a list of other tree files by stand ID (columns are 'standID' and 'fileName').");
            }

            this.IndividualAge = new();
            this.IndividualDbhInCM = new();
            this.IndividualHeightInM = new();
            this.IndividualSpeciesID = new();
            this.IndividualStandID = new();
            this.IndividualTag = new();
            this.IndividualGisX = new();
            this.IndividualGisY = new();

            this.Path = treeFilePath;

            this.TreeFileNameByStandID = new();
            this.TreeSizeDistribution = new();

            if (individualTreeHeader.CanBeIndividualTreeFile)
            {
                int treeCount = -1;
                treeFile.Parse((string[] row) =>
                {
                    ++treeCount;
                    this.IndividualDbhInCM.Add(Single.Parse(row[individualTreeHeader.Dbh]));
                    this.IndividualGisX.Add(Single.Parse(row[individualTreeHeader.X]));
                    this.IndividualGisY.Add(Single.Parse(row[individualTreeHeader.Y]));

                    string speciesID = row[individualTreeHeader.Species];
                    if (Int32.TryParse(speciesID, out int picusID))
                    {
                        int speciesIndex = TreeReader.PicusSpeciesIDs.IndexOf(picusID);
                        if (speciesIndex == -1)
                        {
                            throw new NotSupportedException("Unknown Picus species id " + picusID + ".");
                        }
                        speciesID = TreeReader.iLandSpeciesIDs[speciesIndex];
                    }
                    this.IndividualSpeciesID.Add(speciesID);

                    int tag = treeCount;
                    if (individualTreeHeader.Tag >= 0)
                    {
                        // override default of ID = count of trees currently on resource unit
                        // So long as all trees are specified from a tree list and AddTree() isn't called on the resource unit later then IDs will remain unique.
                        tag = Int32.Parse(row[individualTreeHeader.Tag]);
                    }
                    this.IndividualTag.Add(tag);

                    // convert from Picus-cm to m if necessary
                    float height = individualTreeHeader.HeightConversionFactor * Single.Parse(row[individualTreeHeader.Height]);
                    this.IndividualHeightInM.Add(height);

                    int age = 0;
                    if (individualTreeHeader.Age >= 0)
                    {
                        age = Int32.Parse(row[individualTreeHeader.Age]);
                    }
                    this.IndividualAge.Add(age);

                    int standID = Constant.DefaultStandID;
                    if (individualTreeHeader.StandID >= 0)
                    {
                        standID = Int32.Parse(row[individualTreeHeader.StandID]);
                    }
                    this.IndividualStandID.Add(standID);
                });

                Debug.Assert((this.IndividualAge.Count == this.IndividualDbhInCM.Count) &&
                             (this.IndividualAge.Count == this.IndividualHeightInM.Count) &&
                             (this.IndividualAge.Count == this.IndividualSpeciesID.Count) &&
                             (this.IndividualAge.Count == this.IndividualStandID.Count) &&
                             (this.IndividualAge.Count == this.IndividualTag.Count) &&
                             (this.IndividualAge.Count == this.IndividualGisX.Count) &&
                             (this.IndividualAge.Count == this.IndividualGisY.Count));
            }
            else if (treeSizeHeader.CanBeSizeDistributionFile)
            {
                int lineNumber = 0;
                treeFile.Parse((string[] row) =>
                {
                    TreeSizeRange sizeRange = new(row[treeSizeHeader.Species])
                    {
                        Count = Single.Parse(row[treeSizeHeader.Count]),
                        DbhFrom = Single.Parse(row[treeSizeHeader.MinimumDbh]),
                        DbhTo = Single.Parse(row[treeSizeHeader.MaximumDbh]),
                        HeightDiameterRatio = Single.Parse(row[treeSizeHeader.HeightDiameterRatio])
                    };
                    ++lineNumber;

                    if ((sizeRange.HeightDiameterRatio == 0.0) || (sizeRange.DbhFrom / 100.0 * sizeRange.HeightDiameterRatio < Constant.Sapling.MaximumHeight))
                    {
                        throw new NotSupportedException("File '" + treeFilePath + "' tries to init trees below 4 m height at line " + lineNumber + ". Height-diameter ratio = " + sizeRange.HeightDiameterRatio + ", DBH = " + sizeRange.DbhFrom + ".");
                    }

                    // TODO: DbhFrom < DbhTo?
                    //throw new NotSupportedException(String.Format("load init file: file '{0}' tries to init trees below 4m height. hd={1}, dbh={2}.", fileName, item.hd, item.dbh_from) );
                    bool setAgeToZero = true;
                    if (treeSizeHeader.Age >= 0)
                    {
                        if (Int32.TryParse(row[treeSizeHeader.Age], out int age))
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
                        sizeRange.Density = Single.Parse(row[treeSizeHeader.Density]);
                    }
                    else
                    {
                        sizeRange.Density = 0.0F;
                    }
                    if (sizeRange.Density < -1)
                    {
                        throw new NotSupportedException("Invalid density " + sizeRange.Density + " in file '" + treeFilePath + "', line " + lineNumber + ". Allowed range is -1..1.");
                    }

                    if (String.IsNullOrEmpty(sizeRange.TreeSpecies))
                    {
                        throw new NotSupportedException("Missing species in file '" + treeFilePath + ", line " + lineNumber + ".");
                    }

                    this.TreeSizeDistribution.Add(sizeRange);
                });

            }
            else if (treeFileIndexHeader.CanBeIndexFile)
            {
                int lineNumber = 0;
                treeFile.Parse((string[] row) =>
                {
                    ++lineNumber;
                    int standID = Int32.Parse(row[treeFileIndexHeader.StandID]);
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
            else
            {
                throw new NotSupportedException("Unhandled format for tree file '" + treeFilePath + "'.");
            }
        }
    }
}
