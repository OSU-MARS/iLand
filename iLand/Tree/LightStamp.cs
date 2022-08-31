using iLand.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace iLand.Tree
{
    /** Stamp is the basic class for the LIP field of a individual tree.
        In iLand jargon, a Stamp is a LIP (light influence pattern). These patterns are pre-calculated using the "LightRoom" (http://iland-model.org/Lightroom)
        and stand for a field of influence (w.r.t. light) of a individual tree of a given size and species.
        see http://iland-model.org/competition+for+light
    */
    public class LightStamp
    {
        // grid holding precalculated distances to the stamp center
        // private static Grid<float> DistanceFromCenterGrid { get; set; }

        // delta between edge of the stamp and the logical center point of the tree (same in x and y). E.g., a 5x5 stamp in an 8x8-grid has an offset from 2.
        public int CenterCellIndex { get; set; }
        public float CrownAreaInM2 { get; private set; }
        public float CrownRadiusInM { get; private set; }
        public float[] Data { get; private init; }
        // width of the stamp's data in light cells; e.g. 4 -> 4x4 stamp with 16 pixels.
        public int DataSize { get; private init; }
        public float DbhInCm { get; private set; }
        public int HeightDiameterRatio { get; private init; }
        // pointer to the appropriate reader stamp (if available)
        public LightStamp? ReaderStamp { get; private set; }

        //static LightStamp()
        //{
        //    LightStamp.DistanceFromCenterGrid = new();

        //    float lightCellSize = Constant.Grid.LightCellSizeInM;
        //    const int halfOfMaxStampSize = Constant.Grid.MaxLightStampSizeInLightCells / 2; // distance from center, so only need to cover the maximum radius possible in the largest stamp
        //    LightStamp.DistanceFromCenterGrid.Setup(halfOfMaxStampSize, halfOfMaxStampSize, lightCellSize);
        //    for (int indexX = 0; indexX < halfOfMaxStampSize; ++indexX)
        //    {
        //        for (int indexY = 0; indexY <= indexX; ++indexY)
        //        {
        //            // distance grid (matrix) is symmetric, so calculate once and assign twice
        //            float distanceInM = lightCellSize * MathF.Sqrt(indexX * indexX + indexY * indexY);
        //            LightStamp.DistanceFromCenterGrid[indexX, indexY] = distanceInM;
        //            LightStamp.DistanceFromCenterGrid[indexY, indexX] = distanceInM;
        //        }
        //    }
        //}

        public LightStamp(float dbhInCm, int heightDiameterRatio, float crownRadiusInM, int centerIndex, int dataSize)
        {
            if (Enum.IsDefined((LightStampSize)dataSize) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(dataSize));
            }
            if ((centerIndex < 0) || (centerIndex > dataSize / 2))
            {
                throw new ArgumentOutOfRangeException(nameof(centerIndex));
            }
            if ((crownRadiusInM <= 0.0F) || (crownRadiusInM > 0.5F * Constant.Grid.LightCellSizeInM * dataSize))
            {
                throw new ArgumentOutOfRangeException(nameof(crownRadiusInM));
            }
            if ((dbhInCm < 0.0F) || (dbhInCm > 500.0F)) // reader stamps have DBH set to zero
            {
                throw new ArgumentOutOfRangeException(nameof(dbhInCm));
            }
            if ((heightDiameterRatio < 0) || (heightDiameterRatio > 250)) // reader stamps have height:diameter ratio set to zero
            {
                throw new ArgumentOutOfRangeException(nameof(heightDiameterRatio));
            }

            this.CenterCellIndex = centerIndex;
            this.CrownAreaInM2 = MathF.PI * crownRadiusInM * crownRadiusInM;
            this.CrownRadiusInM = crownRadiusInM;
            this.Data = new float[dataSize * dataSize];
            this.DataSize = dataSize;
            this.DbhInCm = dbhInCm;
            this.HeightDiameterRatio = heightDiameterRatio;
            this.ReaderStamp = null;
        }

        public float GetDistanceToCenterInM(int indexX, int indexY)
        {
            // caculating distance each time profiles about 10% faster than using a lookup table
            int distanceX = indexX - this.CenterCellIndex;
            int distanceY = indexY - this.CenterCellIndex;
            return Constant.Grid.LightCellSizeInM * MathF.Sqrt(distanceX * distanceX + distanceY * distanceY);
            // return LightStamp.DistanceFromCenterGrid[Math.Abs(indexX - this.CenterCellIndex), Math.Abs(indexY - this.CenterCellIndex)];
        }

        public Vector128<float> GetDistanceToCenterInM(Vector128<int> indexX, Vector128<int> indexY)
        {
            // since distances are small integer truncation with multiply low isn't a concern
            Vector128<int> centerCellIndex = Avx2Extensions.BroadcastScalarToVector128(this.CenterCellIndex);
            Vector128<int> distanceX = Avx2.Subtract(indexX, centerCellIndex);
            Vector128<int> distanceY = Avx2.Subtract(indexY, centerCellIndex);
            Vector128<int> squaredDistance = Avx2.Add(Avx2.MultiplyLow(distanceX, distanceX), Avx2.MultiplyLow(distanceY, distanceY));
            return Avx.Multiply(Constant.Grid128F.LightCellSizeInM, Avx.Sqrt(Avx.ConvertToVector128Single(squaredDistance)));
        }

        public Vector256<float> GetDistanceToCenterInM(Vector256<int> indexX, Vector256<int> indexY)
        {
            // since distances are small integer truncation with multiply low isn't a concern
            Vector256<int> centerCellIndex = Avx2Extensions.BroadcastScalarToVector256(this.CenterCellIndex);
            Vector256<int> distanceX = Avx2.Subtract(indexX, centerCellIndex);
            Vector256<int> distanceY = Avx2.Subtract(indexY, centerCellIndex);
            Vector256<int> squaredDistance = Avx2.Add(Avx2.MultiplyLow(distanceX, distanceX), Avx2.MultiplyLow(distanceY, distanceY));
            return Avx.Multiply(Constant.Grid256F.LightCellSizeInM, Avx.Sqrt(Avx.ConvertToVector256Single(squaredDistance)));
        }

        public int GetSizeInLightCells() // width or height of the stamp in light cells
        {
            return Constant.Grid.LightCellSizeInM * this.CenterCellIndex + 1;
        }

        /// get index (e.g. for data()[index]) for indices x and y
        private int IndexXYToIndex(int indexX, int indexY) 
        { 
            Debug.Assert((indexY * this.DataSize + indexX) < this.Data.Length); 
            return indexY * this.DataSize + indexX; 
        }

        public void SetReaderStamp(LightStamp readerStamp)
        {
            float crownRadiusInM = readerStamp.CrownRadiusInM;
            this.CrownAreaInM2 = MathF.PI * crownRadiusInM * crownRadiusInM;
            this.CrownRadiusInM = crownRadiusInM;
            this.ReaderStamp = readerStamp;
        }

        public void Write(string classPrefix, StreamWriter writer)
        {
            string linePrefix = classPrefix + "," +
                                this.DataSize + "," +
                                this.CenterCellIndex;
            for (int indexX = 0; indexX < this.DataSize; ++indexX)
            {
                for (int indexY = 0; indexY < this.DataSize; ++indexY)
                {
                    int index = this.IndexXYToIndex(indexX, indexY);
                    writer.WriteLine(linePrefix + "," + indexX + "," + indexY + "," + this.Data[index].ToString());
                }
            }
        }
    }
}
