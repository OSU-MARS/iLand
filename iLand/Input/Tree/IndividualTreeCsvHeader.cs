﻿namespace iLand.Input.Tree
{
    public class IndividualTreeCsvHeader
    {
        public int Age { get; private init; } // optional
        public int Dbh { get; private init; }
        public int Height { get; private init; }
        public int Species { get; private init; }
        public int StandID { get; private init; } // optional
        public int TreeID { get; private init; } // optional
        public int X { get; private init; }
        public int Y { get; private init; }

        public bool CanBeIndividualTreeFile { get; private init; }
        public float HeightConversionFactor { get; private init; }

        public IndividualTreeCsvHeader(CsvFile treeFile)
        {
            this.StandID = treeFile.GetColumnIndex("standID"); // optional
            this.TreeID = treeFile.GetColumnIndex("treeID"); // optional
            if (this.TreeID == -1)
            {
                this.TreeID = treeFile.GetColumnIndex("id"); // also optional but expected in Picus files
            }

            this.X = treeFile.GetColumnIndex("x");
            this.Y = treeFile.GetColumnIndex("y");
            this.Dbh = treeFile.GetColumnIndex("bhdfrom");
            if (this.Dbh < 0)
            {
                this.Dbh = treeFile.GetColumnIndex("dbh");
            }
            this.HeightConversionFactor = 0.01F; // tree heights are in cm; convert to m
            this.Height = treeFile.GetColumnIndex("treeheight");
            if (this.Height < 0)
            {
                this.Height = treeFile.GetColumnIndex("height");
                this.HeightConversionFactor = 1.0F; // tree heights are in meters
            }
            this.Species = treeFile.GetColumnIndex("species");
            this.Age = treeFile.GetColumnIndex("age"); // optional for individual trees, required in tree size distributions (TODO: why? this appears inconsistent)

            int columnCount = treeFile.Columns.Count;
            this.CanBeIndividualTreeFile = columnCount >= 6 && columnCount <= 9 &&
                this.Species >= 0 && this.Dbh >= 0 && this.Height >= 0 &&
                this.X >= 0 && this.Y >= 0;
        }
    }
}
