using iLand.Tool;
using System;
using System.Globalization;

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

        public StandSaplings(StandSaplingsCsvHeader saplingHeader, SplitString row)
        {
            // required fields
            this.StandID = Int32.Parse(row[saplingHeader.StandID], NumberStyles.Integer); // no restrictions
            this.Species = row[saplingHeader.Species].ToString();
            if (String.IsNullOrWhiteSpace(Species))
            {
                throw new NotSupportedException("Sapling species (column 'species') for stand '" + StandID + "' is missing.");
            }

            // TODO: constants for default values?
            this.Age = 1;
            if (saplingHeader.Age >= 0)
            {
                this.Age = Int32.Parse(row[saplingHeader.Age], NumberStyles.Integer);
                if (this.Age <= 0)
                {
                    throw new NotSupportedException("Sapling age (column 'age') for stand '" + StandID + "' is zero or negative. The minimum age is one year.");
                }
            }

            this.AgeAt4m = saplingHeader.AgeAt4m >= 0 ? Single.Parse(row[saplingHeader.AgeAt4m], NumberStyles.Float) : 10.0F;
            this.Count = Int32.Parse(row[saplingHeader.Count], NumberStyles.Integer); // reqiured field
            this.GrassCoverPercentage = -1;
            if (saplingHeader.GrassCover >= 0)
            {
                this.GrassCoverPercentage = Int32.Parse(row[saplingHeader.GrassCover], NumberStyles.Integer);
                if ((this.GrassCoverPercentage < 0) || (this.GrassCoverPercentage > 100))
                {
                    throw new NotSupportedException("The grass cover percentage (column 'grass_cover') for stand '" + this.StandID + "' is '" + this.GrassCoverPercentage + "' is invalid.");
                }
            }

            if (saplingHeader.Height >= 0)
            {
                this.Height = Single.Parse(row[saplingHeader.Height], NumberStyles.Float);
                this.MaxHeight = Constant.NoDataFloat;
                this.MinHeight = Constant.NoDataFloat;
            }
            else
            {
                this.Height = Constant.NoDataFloat;
                this.MaxHeight = Single.Parse(row[saplingHeader.HeightMax], NumberStyles.Float);
                this.MinHeight = Single.Parse(row[saplingHeader.HeightMin], NumberStyles.Float);
            }

            this.MinLightIntensity = saplingHeader.MinLightIntensity >= 0 ? Single.Parse(row[saplingHeader.MinLightIntensity], NumberStyles.Float) : 1.0F;
        }
    }
}
