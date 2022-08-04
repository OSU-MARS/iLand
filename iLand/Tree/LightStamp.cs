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
        // delta between edge of the stamp and the logical center point of the tree (same in x and y). E.g., a 5x5 stamp in an 8x8-grid has an offset from 2.
        public int CenterCellIndex { get; set; }
        public float CrownAreaInM2 { get; private set; }
        public float CrownRadiusInM { get; private set; }
        public float[] Data { get; private init; }
        // internal size of the stamp; e.g. 4 -> 4x4 stamp with 16 pixels.
        public int DataSize { get; private init; }
        // pointer to the appropriate reader stamp (if available)
        public LightStamp? Reader { get; private set; }

        public LightStamp(int dataSize)
        {
            LightStampSize size = (LightStampSize)dataSize;
            switch (size)
            {
                case LightStampSize.Grid4x4:
                case LightStampSize.Grid8x8:
                case LightStampSize.Grid12x12:
                case LightStampSize.Grid16x16:
                case LightStampSize.Grid24x24:
                case LightStampSize.Grid32x32:
                case LightStampSize.Grid48x48:
                case LightStampSize.Grid64x64:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataSize));
            }

            this.CenterCellIndex = 0;
            this.CrownAreaInM2 = 0.0F;
            this.CrownRadiusInM = 0.0F;
            this.Data = new float[dataSize * dataSize];
            this.DataSize = dataSize;
            this.Reader = null;
        }

        /// get pointer to the element after the last element (iterator style)
        // public float end() { return m_data[m_size * m_size]; }
        /// get pointer to data item with indices x and y
        //public float data(int x, int y) { return m_data[IndexOf(x, y)]; }
        //public void setData(int x, int y, float value) { this[x, y] = value; }
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

        private int Count() // count of light cells in stamp
        { 
            return this.DataSize * this.DataSize; 
        }

        public int Size() // width or height of the stamp in light cells
        { 
            return 2 * this.CenterCellIndex + 1; 
        }

        /// get index (e.g. for data()[index]) for indices x and y
        private int IndexOf(int x, int y) 
        { 
            Debug.Assert((y * this.DataSize + x) < this.Data.Length); 
            return y * this.DataSize + x; 
        }

        public void SetReader(LightStamp reader)
        {
            this.Reader = reader;
            this.SetCrownRadiusAndArea(reader.CrownRadiusInM);
        }

        public void SetCrownRadiusAndArea(float radiusInM)
        {
            this.CrownRadiusInM = radiusInM;
            this.CrownAreaInM2 = MathF.PI * radiusInM * radiusInM;
        }

        public float GetDistanceToCenter(int indexX, int indexY)
        {
            return TreeSpeciesStamps.DistanceGrid[Math.Abs(indexX - this.CenterCellIndex), Math.Abs(indexY - this.CenterCellIndex)];
        }

        // load from stream....
        public void Load(BinaryReader input)
        {
            // see StampContainer doc for file stamp binary format
            this.CenterCellIndex = input.ReadInt32();
            Debug.Assert((this.CenterCellIndex >= 0) && (this.CenterCellIndex <= this.DataSize / 2));
            // load data
            for (int index = 0; index < this.Count(); ++index)
            {
                this.Data[index] = input.ReadSingle();
                Debug.Assert((this.Data[index] >= 0.0F) && (this.Data[index] <= 1.0F));
            }
        }

        public void Write(BinaryWriter output)
        {
            // see StampContainer doc for file stamp binary format
            output.Write(this.CenterCellIndex);
            for (int index = 0; index < this.Count(); index++)
            {
                output.Write(this.Data[index]);
            }
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
