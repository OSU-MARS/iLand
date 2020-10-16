using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace iLand.Trees
{
    /** Stamp is the basic class for the LIP field of a individual tree.
        In iLand jargon, a Stamp is a LIP (light influence pattern). These patterns are pre-calculated using the "LightRoom" (http://iland.boku.ac.at/Lightroom)
        and stand for a field of influence (w.r.t. light) of a individual tree of a given size and species.
        see http://iland.boku.ac.at/competition+for+light
    */
    public class Stamp
    {
        public int CenterCellPosition { get; set; } // delta between edge of the stamp and the logical center point (of the tree). e.g. a 5x5 stamp in an 8x8-grid has an offset from 2.
        public float CrownArea { get; private set; }
        public float CrownRadius { get; private set; }
        public float[] Data { get; private set; }
        public int DataSize { get; private set; } // internal size of the stamp; e.g. 4 -> 4x4 stamp with 16 pixels.
        public Stamp Reader { get; private set; } // pointer to the appropriate reader stamp (if available)

        public Stamp(int dataSize)
        {
            StampSize size = (StampSize)dataSize;
            switch (size)
            {
                case StampSize.Grid4x4:
                case StampSize.Grid8x8:
                case StampSize.Grid12x12:
                case StampSize.Grid16x16:
                case StampSize.Grid24x24:
                case StampSize.Grid32x32:
                case StampSize.Grid48x48:
                case StampSize.Grid64x64:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataSize));
            }

            this.Data = new float[dataSize * dataSize];
            this.DataSize = dataSize;
            this.CenterCellPosition = 0;
            this.Reader = null;
            this.CrownArea = 0.0F;
            this.CrownRadius = 0.0F;
        }

        /// get pointer to the element after the last element (iterator style)
        // public float end() { return m_data[m_size * m_size]; }
        /// get pointer to data item with indices x and y
        //public float data(int x, int y) { return m_data[IndexOf(x, y)]; }
        //public void setData(int x, int y, float value) { this[x, y] = value; }
        /// retrieve the value of the stamp at given indices x and y
        public float this[int x, int y]
        {
            get { return Data[IndexOf(x, y)]; }
            set { Data[IndexOf(x, y)] = value; }
        }

        public float this[int x, int y, int offset]
        {
            get { return this[x + offset, y + offset]; }
        }

        public int Count() { return DataSize * DataSize; } // count of pixels (rectangle)

        /// get index (e.g. for data()[index]) for indices x and y
        public int IndexOf(int x, int y) { Debug.Assert((y * DataSize + x) < Data.Length); return y * DataSize + x; }

        public void SetReader(Stamp reader)
        {
            Reader = reader;
            SetCrownRadius(reader.CrownRadius); /*calculates also the Area*/
        }

        public void SetCrownRadius(float r)
        {
            CrownRadius = r;
            CrownArea = r * r * MathF.PI;
        }

        public int Size() { return CenterCellPosition * 2 + 1; } // logical size of the stamp

        public float GetDistanceToCenter(int ix, int iy)
        {
            return SpeciesStamps.DistanceGrid[Math.Abs(ix - CenterCellPosition), Math.Abs(iy - CenterCellPosition)];
        }

        //float distanceToCenter(int ix, int iy)
        //{
        //    //
        //    return StampContainer::distanceGrid().constValueAtIndex(Math.Abs(ix-m_offset), Math.Abs(iy-m_offset));
        //}

        public string Dump()
        {
            StringBuilder result = new StringBuilder();
            int x, y;
            for (y = 0; y < DataSize; ++y)
            {
                string line = "";
                for (x = 0; x < DataSize; ++x)
                {
                    line += this[x, y].ToString() + " ";
                }
                result.AppendLine(line);
            }
            return result.ToString();
        }

        //public void LoadFromTextFile(string fileName)
        //{
        //    string txt = Helper.LoadTextFile(fileName);
        //    List<string> lines = txt.Split("\n").ToList();

        //    Setup(lines.Count);
        //    int l = 0;
        //    foreach (string line in lines)
        //    {
        //        List<string> cols = line.Split(";").ToList();
        //        if (cols.Count != lines.Count)
        //        {
        //            Debug.WriteLine("loadFromFile: invalid count of rows/cols.");
        //            return;
        //        }
        //        for (int i = 0; i < cols.Count; i++)
        //        {
        //            this[i, l] = Single.Parse(cols[i]);
        //        }
        //        l++;
        //    }
        //}

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

        public void Save(BinaryWriter output)
        {
            // see StampContainer doc for file stamp binary format
            output.Write(this.CenterCellPosition);
            for (int i = 0; i < this.Count(); i++)
            {
                output.Write(this.Data[i]);
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

        //    Stamp stamp = new Stamp((int)type);
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
