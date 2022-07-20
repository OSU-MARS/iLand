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

        public StandSaplings(StandSaplingsDataIndex saplingHeader, string[] row)
        {
            // required fields
            this.StandID = Int32.Parse(row[saplingHeader.StandID], CultureInfo.InvariantCulture); // no restrictions
            this.Species = row[saplingHeader.Species];
            if (string.IsNullOrWhiteSpace(Species))
            {
                throw new NotSupportedException("Sapling species (column 'species') for stand '" + StandID + "' is missing.");
            }

            // TODO: constants for default values?
            this.Age = 1;
            if (saplingHeader.Age >= 0)
            {
                this.Age = Int32.Parse(row[saplingHeader.Age], CultureInfo.InvariantCulture);
                if (this.Age <= 0)
                {
                    throw new NotSupportedException("Sapling age (column 'age') for stand '" + StandID + "' is zero or negative. The minimum age is one year.");
                }
            }

            this.AgeAt4m = saplingHeader.AgeAt4m >= 0 ? Single.Parse(row[saplingHeader.AgeAt4m], CultureInfo.InvariantCulture) : 10.0F;
            this.Count = Int32.Parse(row[saplingHeader.Count], CultureInfo.InvariantCulture); // reqiured field
            this.GrassCoverPercentage = -1;
            if (saplingHeader.GrassCover >= 0)
            {
                this.GrassCoverPercentage = Int32.Parse(row[saplingHeader.GrassCover], CultureInfo.InvariantCulture);
                if ((this.GrassCoverPercentage < 0) || (this.GrassCoverPercentage > 100))
                {
                    throw new NotSupportedException("The grass cover percentage (column 'grass_cover') for stand '" + this.StandID + "' is '" + this.GrassCoverPercentage + "' is invalid.");
                }
            }

            if (saplingHeader.Height >= 0)
            {
                this.Height = Single.Parse(row[saplingHeader.Height], CultureInfo.InvariantCulture);
                this.MaxHeight = Constant.NoDataSingle;
                this.MinHeight = Constant.NoDataSingle;
            }
            else
            {
                this.Height = Constant.NoDataSingle;
                this.MaxHeight = Single.Parse(row[saplingHeader.HeightMax], CultureInfo.InvariantCulture);
                this.MinHeight = Single.Parse(row[saplingHeader.HeightMin], CultureInfo.InvariantCulture);
            }

            this.MinLightIntensity = saplingHeader.MinLightIntensity >= 0 ? Single.Parse(row[saplingHeader.MinLightIntensity], CultureInfo.InvariantCulture) : 1.0F;
        }
    }
}
