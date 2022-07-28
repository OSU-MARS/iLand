using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
        public int CenterCellPosition { get; set; }
        public float CrownArea { get; private set; }
        public float CrownRadius { get; private set; }
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

            this.CenterCellPosition = 0;
            this.CrownArea = 0.0F;
            this.CrownRadius = 0.0F;
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
            get { return this.Data[IndexOf(x, y)]; }
            set { this.Data[IndexOf(x, y)] = value; }
        }

        public float this[int x, int y, int offset]
        {
            get { return this[x + offset, y + offset]; }
        }

        public int Count() { return this.DataSize * this.DataSize; } // count of light cells in stamp
        public int Size() { return this.CenterCellPosition * 2 + 1; } // size of the stamp in light cells

        /// get index (e.g. for data()[index]) for indices x and y
        public int IndexOf(int x, int y) { Debug.Assert((y * DataSize + x) < Data.Length); return y * DataSize + x; }

        public void SetReader(LightStamp reader)
        {
            this.Reader = reader;
            this.SetCrownRadiusAndArea(reader.CrownRadius);
        }

        public void SetCrownRadiusAndArea(float radius)
        {
            this.CrownRadius = radius;
            this.CrownArea = MathF.PI * radius * radius;
        }

        public float GetDistanceToCenter(int indexX, int indexY)
        {
            return TreeSpeciesStamps.DistanceGrid[Math.Abs(indexX - this.CenterCellPosition), Math.Abs(indexY - this.CenterCellPosition)];
        }

        public string Dump()
        {
            StringBuilder result = new();
            for (int y = 0; y < this.DataSize; ++y)
            {
                string line = String.Empty;
                for (int x = 0; x < this.DataSize; ++x)
                {
                    line += this[x, y].ToString() + " ";
                }
                result.AppendLine(line);
            }
            return result.ToString();
        }

        // load from stream....
        public void Load(BinaryReader input)
        {
            // see StampContainer doc for file stamp binary format
            this.CenterCellPosition = input.ReadInt32();
            Debug.Assert((this.CenterCellPosition >= 0) && (this.CenterCellPosition <= this.DataSize / 2));
            // load data
            for (int index = 0; index < this.Count(); index++)
            {
                this.Data[index] = input.ReadSingle();
                Debug.Assert((this.Data[index] >= 0.0F) && (this.Data[index] <= 1.0F));
            }
        }

        public void Write(BinaryWriter output)
        {
            // see StampContainer doc for file stamp binary format
            output.Write(this.CenterCellPosition);
            for (int index = 0; index < this.Count(); index++)
            {
                output.Write(this.Data[index]);
            }
        }

        //public Stamp StampFromGrid(Grid<float> grid, int width)
        //{
        //    int c = grid.CellsX; // total size of input grid
        //    if (c % 2 == 0 || width % 2 == 0)
        //    {
        //        Debug.WriteLine("both grid and width should be uneven!!! returning null.");
        //        return null;
        //    }

        //    StampSize type;
        //    if (width <= 4) 
        //        type = StampSize.Grid4x4;
        //    else if (width <= 8) 
        //        type = StampSize.Grid8x8;
        //    else if (width <= 12) 
        //        type = StampSize.Grid12x12;
        //    else if (width <= 16) 
        //        type = StampSize.Grid16x16;
        //    else if (width <= 24)
        //        type = StampSize.Grid24x24;
        //    else if (width <= 32) 
        //        type = StampSize.Grid32x32;
        //    else if (width <= 48)
        //        type = StampSize.Grid48x48;
        //    else
        //        type = StampSize.Grid64x64;

        //    Stamp stamp = new((int)type);
        //    int swidth = width;
        //    if (width > 63)
        //    {
        //        Debug.WriteLine("Warning: grid too big, truncated stamp to 63x63px!");
        //        swidth = 63;
        //    }
        //    stamp.DistanceOffset = swidth / 2;
        //    int coff = c / 2 - swidth / 2; // e.g.: grid=25, width=7 -> coff = 12 - 3 = 9
        //    int x, y;
        //    for (x = 0; x < swidth; x++)
        //    {
        //        for (y = 0; y < swidth; y++)
        //        {
        //            stamp[x, y] = grid[coff + x, coff + y]; // copy data (from a different rectangle)
        //        }
        //    }
        //    return stamp;
        //}
    }
}
