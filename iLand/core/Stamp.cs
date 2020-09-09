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
        private static Grid<float> mDistanceGrid = null;

        private float[] m_data;
        private float m_crownRadius;
        private float m_crownArea;
        private int m_size;
        private int m_offset;
        private Stamp m_reader; ///< pointer to the appropriate reader stamp (if available)

                                /// @enum StampType defines different grid sizes for stamps (4x4 floats, ... 48x48 floats).
                                /// the numeric value indicates also the size of the grid.
        public enum StampType { est4x4 = 4, est8x8 = 8, est12x12 = 12, est16x16 = 16, est24x24 = 24, est32x32 = 32, est48x48 = 48, est64x64 = 64 };
        public void setOffset(int offset) { m_offset = offset; }
        public static void setDistanceGrid(Grid<float> grid) { mDistanceGrid = grid; }
        public int offset() { return m_offset; } ///< delta between edge of the stamp and the logical center point (of the tree). e.g. a 5x5 stamp in an 8x8-grid has an offset from 2.
        public int count() { return m_size * m_size; } ///< count of pixels (rectangle)
        public int size() { return m_offset * 2 + 1; } ///< logical size of the stamp
        public int dataSize() { return m_size; } ///< internal size of the stamp; e.g. 4 -> 4x4 stamp with 16 pixels.
                                                 /// get a full access pointer to internal data
        public float[] data() { return m_data; }
        /// get pointer to the element after the last element (iterator style)
        public float end() { return m_data[m_size * m_size]; }
        /// get pointer to data item with indices x and y
        public float data(int x, int y) { return m_data[index(x, y)]; }
        public void setData(int x, int y, float value) { this[x, y] = value; }
        /// get index (e.g. for data()[index]) for indices x and y
        public int index(int x, int y) { Debug.Assert(y * m_size + x < m_size * m_size); return y * m_size + x; }
        /// retrieve the value of the stamp at given indices x and y
        public float this[int x, int y]
        {
            get { return data(x, y); }
            set { m_data[index(x, y)] = value; }
        }

        public float offsetValue(int x, int y, int offset) { return data(x + offset, y + offset); }
        public Stamp reader() { return m_reader; }

        public void setReader(Stamp reader)
        {
            m_reader = reader;
            setCrownRadius(reader.crownRadius()); /*calculates also the Area*/
        }

        // property crown radius
        public float crownRadius() { return m_crownRadius; }
        public float crownArea() { return m_crownArea; }
        public void setCrownRadius(float r)
        {
            m_crownRadius = r;
            m_crownArea = r * r * MathF.PI;
        }

        public float distanceToCenter(int ix, int iy)
        {
            return mDistanceGrid.constValueAtIndex(Math.Abs(ix - m_offset), Math.Abs(iy - m_offset));
        }

        public Stamp()
        {
            m_data = null;
        }

        public Stamp(int size)
        {
            m_data = null;
            setup(size);
        }

        private void setup(int size)
        {
            int c = size * size;
            m_size = size;
            m_offset = 0;
            m_reader = null;
            m_crownArea = 0.0F;
            m_crownRadius = 0.0F;
            m_data = new float[c];
            // set static variable values
            mDistanceGrid = StampContainer.distanceGrid();
        }

        //float distanceToCenter(int ix, int iy)
        //{
        //    //
        //    return StampContainer::distanceGrid().constValueAtIndex(Math.Abs(ix-m_offset), Math.Abs(iy-m_offset));
        //}

        public string dump()
        {
            StringBuilder result = new StringBuilder();
            int x, y;
            for (y = 0; y < m_size; ++y)
            {
                string line = "";
                for (x = 0; x < m_size; ++x)
                {
                    line += data(x, y).ToString() + " ";
                }
                result.AppendLine(line);
            }
            return result.ToString();
        }

        public void loadFromFile(string fileName)
        {
            string txt = Helper.loadTextFile(fileName);
            List<string> lines = txt.Split("\n").ToList();

            setup(lines.Count);
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
        public void load(BinaryReader input)
        {
            // see StampContainer doc for file stamp binary format
            m_offset = input.ReadInt32();
            // load data
            for (int i = 0; i < count(); i++)
            {
                m_data[i] = input.ReadSingle();
            }
        }

        public void save(BinaryWriter output)
        {
            // see StampContainer doc for file stamp binary format
            output.Write(m_offset);
            for (int i = 0; i < count(); i++)
            {
                output.Write(m_data[i]);
            }
        }

        public Stamp stampFromGrid(Grid<float> grid, int width)
        {
            StampType type = StampType.est4x4;
            int c = grid.sizeX(); // total size of input grid
            if (c % 2 == 0 || width % 2 == 0)
            {
                Debug.WriteLine("both grid and width should be uneven!!! returning null.");
                return null;
            }

            if (width <= 4) type = StampType.est4x4;
            else if (width <= 8) type = StampType.est8x8;
            else if (width <= 12) type = StampType.est12x12;
            else if (width <= 16) type = StampType.est16x16;
            else if (width <= 24) type = StampType.est24x24;
            else if (width <= 32) type = StampType.est32x32;
            else if (width <= 48) type = StampType.est48x48;
            else type = StampType.est64x64;

            Stamp stamp = new Stamp((int)type);
            int swidth = width;
            if (width > 63)
            {
                Debug.WriteLine("Warning: grid too big, truncated stamp to 63x63px!");
                swidth = 63;
            }
            stamp.setOffset(swidth / 2);
            int coff = c / 2 - swidth / 2; // e.g.: grid=25, width=7 -> coff = 12 - 3 = 9
            int x, y;
            for (x = 0; x < swidth; x++)
            {
                for (y = 0; y < swidth; y++)
                {
                    stamp.setData(x, y, grid[coff + x, coff + y]); // copy data (from a different rectangle)
                }
            }
            return stamp;
        }
    }
}
