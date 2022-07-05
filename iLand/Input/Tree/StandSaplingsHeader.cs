using System.IO;

namespace iLand.Input.Tree
{
    internal class StandSaplingsHeader
    {
        public int Age { get; private init; }
        public int Count { get; private init; }
        public int GrassCover { get; private init; }

        public int Height { get; private init; }
        public int HeightMin { get; private init; }
        public int HeightMax { get; private init; }

        public int MinLightIntensity { get; private init; }
        public int Species { get; private init; }
        public int StandID { get; private init; }
        public int AgeAt4m { get; private init; }

        public StandSaplingsHeader(CsvFile saplingFile)
        {
            Count = saplingFile.GetColumnIndex("count");
            Species = saplingFile.GetColumnIndex("species");
            if (Species < 0 || Count < 0)
            {
                throw new FileLoadException("Sapling files must have 'species' and 'count' columns.");
            }

            StandID = saplingFile.GetColumnIndex("stand_id");
            if (StandID < 0)
            {
                throw new FileLoadException("The sapling file contains no 'stand_id' column (required in 'standgrid' mode).");
            }

            Height = saplingFile.GetColumnIndex("height");
            HeightMin = saplingFile.GetColumnIndex("height_from");
            HeightMax = saplingFile.GetColumnIndex("height_to");
            if (Height < 0 && HeightMin < 0 ^ HeightMax < 0)
            {
                throw new FileLoadException("Height not correctly provided. Use either 'height' or both 'height_from' and 'height_to'.");
            }

            Age = saplingFile.GetColumnIndex("age");
            GrassCover = saplingFile.GetColumnIndex("grass_cover");
            MinLightIntensity = saplingFile.GetColumnIndex("min_lif");
            AgeAt4m = saplingFile.GetColumnIndex("age4m");
        }
    }
}
