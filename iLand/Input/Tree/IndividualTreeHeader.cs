namespace iLand.Input.Tree
{
    internal class IndividualTreeHeader
    {
        public int Age { get; private init; } // optional
        public int Dbh { get; private init; }
        public int Height { get; private init; }
        public int Species { get; private init; }
        public int StandID { get; private init; } // optional
        public int Tag { get; private init; } // optional
        public int X { get; private init; }
        public int Y { get; private init; }

        public bool CanBeIndividualTreeFile { get; private init; }
        public float HeightConversionFactor { get; private init; }

        public IndividualTreeHeader(CsvFile treeFile)
        {
            StandID = treeFile.GetColumnIndex("standID"); // optional
            Tag = treeFile.GetColumnIndex("id"); // optional
            X = treeFile.GetColumnIndex("x");
            Y = treeFile.GetColumnIndex("y");
            Dbh = treeFile.GetColumnIndex("bhdfrom");
            if (Dbh < 0)
            {
                Dbh = treeFile.GetColumnIndex("dbh");
            }
            HeightConversionFactor = 0.01F; // tree heights are in cm; convert to m
            Height = treeFile.GetColumnIndex("treeheight");
            if (Height < 0)
            {
                Height = treeFile.GetColumnIndex("height");
                HeightConversionFactor = 1.0F; // tree heights are in meters
            }
            Species = treeFile.GetColumnIndex("species");
            Age = treeFile.GetColumnIndex("age"); // optional for individual trees, required in tree size distributions (TODO: why? this appears inconsistent)

            int columnCount = treeFile.Columns.Count;
            CanBeIndividualTreeFile = columnCount >= 6 && columnCount <= 9 &&
                Species >= 0 && Dbh >= 0 && Height >= 0 &&
                X >= 0 && Y >= 0;
        }
    }
}
