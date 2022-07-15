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

        public StandSaplings(StandSaplingsDataIndex saplingHeader, string[] row)
        {
            // required fields
            this.StandID = int.Parse(row[saplingHeader.StandID]); // no restrictions
            this.Species = row[saplingHeader.Species];
            if (string.IsNullOrWhiteSpace(Species))
            {
                throw new NotSupportedException("Sapling species (column 'species') for stand '" + StandID + "' is missing.");
            }

            // TODO: constants for default values?
            this.Age = 1;
            if (saplingHeader.Age >= 0)
            {
                this.Age = int.Parse(row[saplingHeader.Age]);
                if (this.Age <= 0)
                {
                    throw new NotSupportedException("Sapling age (column 'age') for stand '" + StandID + "' is zero or negative. The minimum age is one year.");
                }
            }

            this.AgeAt4m = saplingHeader.AgeAt4m >= 0 ? float.Parse(row[saplingHeader.AgeAt4m]) : 10.0F;
            this.Count = int.Parse(row[saplingHeader.Count]); // reqiured field
            this.GrassCoverPercentage = -1;
            if (saplingHeader.GrassCover >= 0)
            {
                this.GrassCoverPercentage = int.Parse(row[saplingHeader.GrassCover]);
                if ((this.GrassCoverPercentage < 0) || (this.GrassCoverPercentage > 100))
                {
                    throw new NotSupportedException("The grass cover percentage (column 'grass_cover') for stand '" + this.StandID + "' is '" + this.GrassCoverPercentage + "' is invalid.");
                }
            }

            if (saplingHeader.Height >= 0)
            {
                this.Height = float.Parse(row[saplingHeader.Height]);
                this.MaxHeight = Constant.NoDataSingle;
                this.MinHeight = Constant.NoDataSingle;
            }
            else
            {
                this.Height = Constant.NoDataSingle;
                this.MaxHeight = float.Parse(row[saplingHeader.HeightMax]);
                this.MinHeight = float.Parse(row[saplingHeader.HeightMin]);
            }

            this.MinLightIntensity = saplingHeader.MinLightIntensity >= 0 ? float.Parse(row[saplingHeader.MinLightIntensity]) : 1.0F;
        }
    }
}
