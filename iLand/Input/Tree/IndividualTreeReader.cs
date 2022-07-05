using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.Input.Tree
{
    internal class IndividualTreeReader : TreeReader
    {
        // when reading .csv files, support conversion from Picus numeric species IDs to iLand USDA Plants codes
        // TODO: if needed, expand Picus species support or make translation specific to files with the Picus <trees> element
        private static readonly ReadOnlyCollection<string> iLandSpeciesIDs = new List<string>() { "piab", "piab", "fasy" }.AsReadOnly();
        private static readonly ReadOnlyCollection<int> PicusSpeciesIDs = new List<int>() { 0, 1, 17 }.AsReadOnly();

        public List<int> AgeInYears { get; private init; }
        public List<float> DbhInCM { get; private init; }
        public List<float> GisX { get; private init; }
        public List<float> GisY { get; private init; }
        public List<float> HeightInM { get; private init; }
        public List<string> SpeciesID { get; private init; }
        public List<int> StandID { get; private init; }
        public List<int> Tag { get; private init; }

        protected IndividualTreeReader(string treeFilePath)
            : base(treeFilePath)
        {
            AgeInYears = new();
            DbhInCM = new();
            GisX = new();
            GisY = new();
            HeightInM = new();
            SpeciesID = new();
            StandID = new();
            Tag = new();
        }

        public IndividualTreeReader(string treeFilePath, IndividualTreeHeader individualTreeHeader, CsvFile treeFile)
            : this(treeFilePath)
        {
            int treeCount = -1;
            treeFile.Parse((row) =>
            {
                ++treeCount;
                DbhInCM.Add(float.Parse(row[individualTreeHeader.Dbh]));
                GisX.Add(float.Parse(row[individualTreeHeader.X]));
                GisY.Add(float.Parse(row[individualTreeHeader.Y]));

                string speciesID = row[individualTreeHeader.Species];
                if (int.TryParse(speciesID, out int picusID))
                {
                    int speciesIndex = PicusSpeciesIDs.IndexOf(picusID);
                    if (speciesIndex == -1)
                    {
                        throw new NotSupportedException("Unknown Picus species id " + picusID + ".");
                    }
                    speciesID = iLandSpeciesIDs[speciesIndex];
                }
                SpeciesID.Add(speciesID);

                int tag = treeCount;
                if (individualTreeHeader.Tag >= 0)
                {
                    // override default of ID = count of trees currently on resource unit
                    // So long as all trees are specified from a tree list and AddTree() isn't called on the resource unit later then IDs will remain unique.
                    tag = int.Parse(row[individualTreeHeader.Tag]);
                }
                Tag.Add(tag);

                // convert from Picus-cm to m if necessary
                float height = individualTreeHeader.HeightConversionFactor * float.Parse(row[individualTreeHeader.Height]);
                HeightInM.Add(height);

                int age = 0;
                if (individualTreeHeader.Age >= 0)
                {
                    age = int.Parse(row[individualTreeHeader.Age]);
                }
                AgeInYears.Add(age);

                int standID = Constant.DefaultStandID;
                if (individualTreeHeader.StandID >= 0)
                {
                    standID = int.Parse(row[individualTreeHeader.StandID]);
                }
                StandID.Add(standID);
            });

            Debug.Assert(AgeInYears.Count == DbhInCM.Count &&
                         AgeInYears.Count == GisX.Count &&
                         AgeInYears.Count == GisY.Count &&
                         AgeInYears.Count == HeightInM.Count &&
                         AgeInYears.Count == SpeciesID.Count &&
                         AgeInYears.Count == StandID.Count &&
                         AgeInYears.Count == Tag.Count);
        }
    }
}
