using iLand.Extensions;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            string? mostRecentSpeciesName = null;
            WorldFloraID mostRecentSpeciesID = WorldFloraID.Unknown;
            treeFile.Parse((row) =>
            {
                if (this.Count >= this.Capacity)
                {
                    int estimatedNewCapacity = this.Capacity + Constant.Data.DefaultTreeAllocationIncrement;
                    if (this.Count >= 2 * Constant.Data.DefaultTreeAllocationIncrement)
                    {
                        double positionInFile = row.GetPositionInFile();
                        int estimatedCapacityFromFilePosition = (int)Math.Ceiling((double)this.Capacity / positionInFile);
                        if (estimatedCapacityFromFilePosition > estimatedNewCapacity)
                        {
                            estimatedNewCapacity = estimatedCapacityFromFilePosition;
                        }
                    }
                    this.Resize(estimatedNewCapacity);
                }

                int treeIndex = this.Count;
                this.DbhInCm[treeIndex] = Single.Parse(row[individualTreeHeader.Dbh], NumberStyles.Float);
                this.GisX[treeIndex] = Single.Parse(row[individualTreeHeader.X], NumberStyles.Float);
                this.GisY[treeIndex] = Single.Parse(row[individualTreeHeader.Y], NumberStyles.Float);

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
                if (MemoryExtensions.Equals(speciesID, mostRecentSpeciesName, StringComparison.OrdinalIgnoreCase) == false)
                {
                    mostRecentSpeciesName = speciesID.ToString();
                    mostRecentSpeciesID = WorldFloraIDExtensions.Parse(mostRecentSpeciesName);
                }
                this.SpeciesID[treeIndex] = mostRecentSpeciesID; // ID string reuse can be made more sophisticated if needed

                int treeID = treeIndex;
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
                        throw new NotSupportedException("Tree ID at line " + (this.Count + 1) + " is empty."); // +1 for header row
                    }
                    if ((treeIDAsString[0] == '"') && (treeIDAsString.Length > 1) && (treeIDAsString[^1] == '"'))
                    {
                        treeIDAsString = treeIDAsString[1..^2];
                    }
                    treeID = Int32.Parse(treeIDAsString, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                this.TreeID[treeIndex] = treeID;

                // convert from Picus-cm to m if necessary
                float height = individualTreeHeader.HeightConversionFactor * Single.Parse(row[individualTreeHeader.Height], NumberStyles.Float);
                this.HeightInM[treeIndex] = height;

                UInt16 age = 0;
                if (individualTreeHeader.Age >= 0)
                {
                    age = UInt16.Parse(row[individualTreeHeader.Age], NumberStyles.Integer);
                }
                this.AgeInYears[treeIndex] = age;

                int standID = Constant.DefaultStandID;
                if (individualTreeHeader.StandID >= 0)
                {
                    standID = Int32.Parse(row[individualTreeHeader.StandID], NumberStyles.Integer);
                }
                this.StandID[treeIndex] = standID;

                ++this.Count;
            });

            // no read time validation as it's done when trees are added to resource units
        }
    }
}
