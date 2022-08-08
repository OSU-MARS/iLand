using iLand.World;
using System;
using System.Diagnostics;
using System.IO;

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
        private static Grid<float> DistanceFromCenterGrid { get; set; }

        // delta between edge of the stamp and the logical center point of the tree (same in x and y). E.g., a 5x5 stamp in an 8x8-grid has an offset from 2.
        public int CenterCellIndex { get; set; }
        public float CrownAreaInM2 { get; private set; }
        public float CrownRadiusInM { get; private set; }
        public float[] Data { get; private init; }
        // width of the stamp's data in light cells; e.g. 4 -> 4x4 stamp with 16 pixels.
        public int DataSize { get; private init; }
        public int HeightDiameterRatio { get; private init; }
        public float DbhInCm { get; private set; }
        // pointer to the appropriate reader stamp (if available)
        public LightStamp? ReaderStamp { get; private set; }

        static LightStamp()
        {
            LightStamp.DistanceFromCenterGrid = new();

            float lightCellSize = Constant.LightCellSizeInM;
            int halfOfMaxStampSize = ((int)LightStampSize.Grid64x64) / 2; // distance from center, so only need to cover the maximum radius possible in the largest stamp
            LightStamp.DistanceFromCenterGrid.Setup(halfOfMaxStampSize, halfOfMaxStampSize, lightCellSize);
            for (int indexX = 0; indexX < halfOfMaxStampSize; ++indexX)
            {
                for (int indexY = 0; indexY < halfOfMaxStampSize; ++indexY)
                {
                    LightStamp.DistanceFromCenterGrid[indexX, indexY] = lightCellSize * MathF.Sqrt(indexX * indexX + indexY * indexY);
                }
            }
        }

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
            if ((crownRadiusInM <= 0.0F) || (crownRadiusInM > 0.5F * Constant.LightCellSizeInM * dataSize))
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

        /// retrieve the value of the stamp at given indices x and y
        public float this[int x, int y]
        {
            get { return this.Data[this.IndexOf(x, y)]; }
            set { this.Data[this.IndexOf(x, y)] = value; }
        }

        public float this[int x, int y, int offset]
        {
            get { return this[x + offset, y + offset]; }
        }

        public float GetDistanceToCenter(int indexX, int indexY)
        {
            return LightStamp.DistanceFromCenterGrid[Math.Abs(indexX - this.CenterCellIndex), Math.Abs(indexY - this.CenterCellIndex)];
        }

        public int GetSizeInLightCells() // width or height of the stamp in light cells
        {
            return Constant.LightCellSizeInM * this.CenterCellIndex + 1;
        }

        /// get index (e.g. for data()[index]) for indices x and y
        private int IndexOf(int x, int y) 
        { 
            Debug.Assert((y * this.DataSize + x) < this.Data.Length); 
            return y * this.DataSize + x; 
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
            for (int stampX = 0; stampX < this.DataSize; ++stampX)
            {
                for (int stampY = 0; stampY < this.DataSize; ++stampY)
                {
                    writer.WriteLine(linePrefix + "," + stampX + "," + stampY + "," + this[stampX, stampY].ToString());
                }
            }
        }
    }
}
