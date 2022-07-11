using System.IO;

namespace iLand.Input.Tree
{
    internal class StandSaplingsDataIndex
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

        public StandSaplingsDataIndex(CsvFile saplingFile)
        {
            this.Count = saplingFile.GetColumnIndex("count");
            this.Species = saplingFile.GetColumnIndex("species");
            if (this.Species < 0 || Count < 0)
            {
                throw new FileLoadException("Sapling files must have 'species' and 'count' columns.");
            }

            this.StandID = saplingFile.GetColumnIndex("stand_id");
            if (this.StandID < 0)
            {
                throw new FileLoadException("The sapling file contains no 'stand_id' column (required in 'standgrid' mode).");
            }

            this.Height = saplingFile.GetColumnIndex("height");
            this.HeightMin = saplingFile.GetColumnIndex("height_from");
            this.HeightMax = saplingFile.GetColumnIndex("height_to");
            if (this.Height < 0 && this.HeightMin < 0 ^ this.HeightMax < 0)
            {
                throw new FileLoadException("Height not correctly provided. Use either 'height' or both 'height_from' and 'height_to'.");
            }

            this.Age = saplingFile.GetColumnIndex("age");
            this.GrassCover = saplingFile.GetColumnIndex("grass_cover");
            this.MinLightIntensity = saplingFile.GetColumnIndex("min_lif");
            this.AgeAt4m = saplingFile.GetColumnIndex("age4m");
        }
    }
}
