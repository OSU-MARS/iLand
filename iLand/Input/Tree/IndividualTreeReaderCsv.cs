using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;

namespace iLand.Input.Tree
{
    public class IndividualTreeReaderCsv : IndividualTreeReader
    {
        // when reading .csv files, support conversion from Picus numeric species IDs to iLand USDA Plants codes
        // if needed, 1) Picus species support can be expanded and 2) translation made specific to files with the Picus <trees> element
        private static readonly ReadOnlyCollection<string> iLandSpeciesIDs = new List<string>() { "piab", "piab", "fasy" }.AsReadOnly();
        private static readonly ReadOnlyCollection<int> PicusSpeciesIDs = new List<int>() { 0, 1, 17 }.AsReadOnly();

        public IndividualTreeReaderCsv(string treeFilePath, IndividualTreeCsvHeader individualTreeHeader, CsvFile treeFile)
            : base(treeFilePath)
        {
            int treeCount = -1;
            string? mostRecentSpeciesID = null;
            treeFile.Parse((row) =>
            {
                ++treeCount;
                this.DbhInCm.Add(Single.Parse(row[individualTreeHeader.Dbh], NumberStyles.Float));
                this.GisX.Add(Single.Parse(row[individualTreeHeader.X], NumberStyles.Float));
                this.GisY.Add(Single.Parse(row[individualTreeHeader.Y], NumberStyles.Float));

                ReadOnlySpan<char> speciesID = row[individualTreeHeader.Species];
                if (Int32.TryParse(speciesID, out int picusID))
                {
                    int speciesIndex = PicusSpeciesIDs.IndexOf(picusID);
                    if (speciesIndex == -1)
                    {
                        throw new NotSupportedException("Unknown Picus species id " + picusID + ".");
                    }
                    speciesID = iLandSpeciesIDs[speciesIndex];
                }
                if (MemoryExtensions.Equals(speciesID, mostRecentSpeciesID, StringComparison.OrdinalIgnoreCase) == false)
                {
                    mostRecentSpeciesID = speciesID.ToString();
                }
                this.SpeciesID.Add(mostRecentSpeciesID!); // ID string reuse can be made more sophisticated if needed

                int treeID = treeCount;
                if (individualTreeHeader.TreeID >= 0)
                {
                    // override default of ID = count of trees currently on resource unit
                    // So long as all trees are uniquely tagged in the input tree list and AddTree() isn't subsequently called on the resource
                    // unit later then IDs will remain unique.
                    // QgsVectorFileWriter (QGIS 3.22) unnecessarily places values of integer columns in quotation marks when writing .csv files,
                    // so check for and strip quotes.
                    ReadOnlySpan<char> treeIDAsString = row[individualTreeHeader.TreeID];
                    if (treeIDAsString.Length < 1)
                    {
                        throw new NotSupportedException("Tree ID at line " + (treeCount + 1) + " is empty."); // +1 for header row
                    }
                    if ((treeIDAsString[0] == '"') && (treeIDAsString.Length > 1) && (treeIDAsString[^1] == '"'))
                    {
                        treeIDAsString = treeIDAsString[1..^2];
                    }
                    treeID = Int32.Parse(treeIDAsString, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                this.TreeID.Add(treeID);

                // convert from Picus-cm to m if necessary
                float height = individualTreeHeader.HeightConversionFactor * Single.Parse(row[individualTreeHeader.Height], NumberStyles.Float);
                this.HeightInM.Add(height);

                UInt16 age = 0;
                if (individualTreeHeader.Age >= 0)
                {
                    age = UInt16.Parse(row[individualTreeHeader.Age], NumberStyles.Integer);
                }
                this.AgeInYears.Add(age);

                int standID = Constant.DefaultStandID;
                if (individualTreeHeader.StandID >= 0)
                {
                    standID = Int32.Parse(row[individualTreeHeader.StandID], NumberStyles.Integer);
                }
                this.StandID.Add(standID);
            });

            Debug.Assert(this.AgeInYears.Count == this.DbhInCm.Count &&
                         this.AgeInYears.Count == this.GisX.Count &&
                         this.AgeInYears.Count == this.GisY.Count &&
                         this.AgeInYears.Count == this.HeightInM.Count &&
                         this.AgeInYears.Count == this.SpeciesID.Count &&
                         this.AgeInYears.Count == this.StandID.Count &&
                         this.AgeInYears.Count == this.TreeID.Count);
        }
    }
}
