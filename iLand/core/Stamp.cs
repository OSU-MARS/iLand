using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace iLand.core
{
    /** Stamp is the basic class for the LIP field of a individual tree.
        @ingroup core
        In iLand jargon, a Stamp is a LIP (light influence pattern). These patterns are pre-calculated using the "LightRoom" (http://iland.boku.ac.at/Lightroom)
        and stand for a field of influence (w.r.t. light) of a individual tree of a given size and species.
        see http://iland.boku.ac.at/competition+for+light
    */
    internal class Stamp
    {
        public static Grid<float> DistanceGrid { get; set; } // BUGBUG

        public float CrownArea { get; private set; }
        public float CrownRadius { get; private set; }
        public float[] Data { get; private set; }
        public int DataSize { get; private set; } ///< internal size of the stamp; e.g. 4 -> 4x4 stamp with 16 pixels.
        public int DistanceOffset { get; set; } ///< delta between edge of the stamp and the logical center point (of the tree). e.g. a 5x5 stamp in an 8x8-grid has an offset from 2.
        public Stamp Reader { get; private set; } ///< pointer to the appropriate reader stamp (if available)

        public Stamp()
        {
            Data = null;
        }

        public Stamp(int size)
        {
            Data = null;
            Setup(size);
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

        public int Count() { return DataSize * DataSize; } ///< count of pixels (rectangle)

        /// get index (e.g. for data()[index]) for indices x and y
        public int IndexOf(int x, int y) { Debug.Assert(y * DataSize + x < DataSize * DataSize); return y * DataSize + x; }

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

        public int Size() { return DistanceOffset * 2 + 1; } ///< logical size of the stamp

        public float GetDistanceToCenter(int ix, int iy)
        {
            return DistanceGrid[Math.Abs(ix - DistanceOffset), Math.Abs(iy - DistanceOffset)];
        }

        private void Setup(int size)
        {
            int c = size * size;
            DataSize = size;
            DistanceOffset = 0;
            Reader = null;
            CrownArea = 0.0F;
            CrownRadius = 0.0F;
            Data = new float[c];
            // set static variable values
            DistanceGrid = StampContainer.DistanceGrid;
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

        public void LoadFromFile(string fileName)
        {
            string txt = Helper.LoadTextFile(fileName);
            List<string> lines = txt.Split("\n").ToList();

            Setup(lines.Count);
            int l = 0;
            foreach (string line in lines)
            {
                List<string> cols = line.Split(";").ToList();
                if (cols.Count != lines.Count)
                {
                    Debug.WriteLine("loadFromFile: invalid count of rows/cols.");
                    return;
                }
                for (int i = 0; i < cols.Count; i++)
                {
                    this[i, l] = Single.Parse(cols[i]);
                }
                l++;
            }
        }

        // load from stream....
        public void Load(BinaryReader input)
        {
            // see StampContainer doc for file stamp binary format
            DistanceOffset = input.ReadInt32();
            // load data
            for (int i = 0; i < Count(); i++)
            {
                Data[i] = input.ReadSingle();
            }
        }

        public void Save(BinaryWriter output)
        {
            // see StampContainer doc for file stamp binary format
            output.Write(DistanceOffset);
            for (int i = 0; i < Count(); i++)
            {
                output.Write(Data[i]);
            }
        }

        public Stamp StampFromGrid(Grid<float> grid, int width)
        {
            int c = grid.SizeX; // total size of input grid
            if (c % 2 == 0 || width % 2 == 0)
            {
                Debug.WriteLine("both grid and width should be uneven!!! returning null.");
                return null;
            }

            StampSize type;
            if (width <= 4) type = StampSize.Grid4x4;
            else if (width <= 8) type = StampSize.Grid8x8;
            else if (width <= 12) type = StampSize.Grid12x12;
            else if (width <= 16) type = StampSize.Grid16x16;
            else if (width <= 24) type = StampSize.Grid24x24;
            else if (width <= 32) type = StampSize.Grid32x32;
            else if (width <= 48) type = StampSize.Grid48x48;
            else type = StampSize.Grid64x64;

            Stamp stamp = new Stamp((int)type);
            int swidth = width;
            if (width > 63)
            {
                Debug.WriteLine("Warning: grid too big, truncated stamp to 63x63px!");
                swidth = 63;
            }
            stamp.DistanceOffset = swidth / 2;
            int coff = c / 2 - swidth / 2; // e.g.: grid=25, width=7 -> coff = 12 - 3 = 9
            int x, y;
            for (x = 0; x < swidth; x++)
            {
                for (y = 0; y < swidth; y++)
                {
                    stamp[x, y] = grid[coff + x, coff + y]; // copy data (from a different rectangle)
                }
            }
            return stamp;
        }
    }
}
