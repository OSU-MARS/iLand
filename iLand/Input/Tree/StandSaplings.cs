using System;

namespace iLand.Input.Tree
{
    internal class StandSaplings
    {
        public int Age { get; private init; }
        public float AgeAt4m { get; private init; }
        public float Count { get; private init; }
        public int GrassCoverPercentage { get; private init; }
        public float Height { get; private init; }
        public float MaxHeight { get; private init; }
        public float MinHeight { get; private init; }
        public float MinLightIntensity { get; private init; }
        public int StandID { get; private init; }
        public string Species { get; private init; }

        public StandSaplings(StandSaplingsHeader saplingHeader, string[] row)
        {
            // required fields
            StandID = int.Parse(row[saplingHeader.StandID]); // no restrictions
            Species = row[saplingHeader.Species];
            if (string.IsNullOrWhiteSpace(Species))
            {
                throw new NotSupportedException("Sapling species (column 'species') for stand '" + StandID + "' is missing.");
            }

            // TODO: constants for defaults
            Age = 1;
            if (saplingHeader.Age >= 0)
            {
                Age = int.Parse(row[saplingHeader.Age]);
                if (Age <= 0)
                {
                    throw new NotSupportedException("Sapling age (column 'age') for stand '" + StandID + "' is zero or negative. The minimum age is one year.");
                }
            }

            AgeAt4m = saplingHeader.AgeAt4m >= 0 ? float.Parse(row[saplingHeader.AgeAt4m]) : 10.0F;
            Count = int.Parse(row[saplingHeader.Count]); // reqiured field
            GrassCoverPercentage = -1;
            if (saplingHeader.GrassCover >= 0)
            {
                GrassCoverPercentage = int.Parse(row[saplingHeader.GrassCover]);
                if (GrassCoverPercentage < 0 || GrassCoverPercentage > 100)
                {
                    throw new NotSupportedException("The grass cover percentage (column 'grass_cover') for stand '" + StandID + "' is '" + GrassCoverPercentage + "' is invalid.");
                }
            }

            if (saplingHeader.Height >= 0)
            {
                Height = float.Parse(row[saplingHeader.Height]);
                MaxHeight = Constant.NoDataSingle;
                MinHeight = Constant.NoDataSingle;
            }
            else
            {
                Height = Constant.NoDataSingle;
                MaxHeight = float.Parse(row[saplingHeader.HeightMax]);
                MinHeight = float.Parse(row[saplingHeader.HeightMin]);
            }

            MinLightIntensity = saplingHeader.MinLightIntensity >= 0 ? float.Parse(row[saplingHeader.MinLightIntensity]) : 1.0F;
        }
    }
}
