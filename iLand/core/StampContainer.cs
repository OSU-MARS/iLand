using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace iLand.core
{
    /** Collection of Stamp for one tree species.
        @ingroup core
        Per species several stamps are stored (different BHD, different HD relations). This class
        encapsulates storage and access to these stamps. The design goal is to deliver high
        access speeds for the "stamp()" method.
        Use getStamp(bhd, hd) or getStamp(bhd, height) to access. */
    internal class StampContainer
    {
        // constants: comments may be wrong; conflicting information in C++
        private const int cBHDclassWidth = 4;
        private const int cBHDclassLow = 4; ///< bhd classes start with 4cm: class 0 = 4..8, class1 = 8..12
        private const int cBHDclassCount = 70; ///< class count, see getKey(): for lower dbhs classes are smaller
        private const int cHDclassWidth = 10;
        private const int cHDclassLow = 35; ///< hd classes offset is 35: class 0 = 35-45, class 1 = 45-55
        private const int cHDclassCount = 16; ///< class count. highest class:  185-195

        private static Grid<float> m_distance; ///< grid holding precalculated distances to the stamp center

        private struct StampItem
        {
            public Stamp stamp;
            public float dbh;
            public float hd;
            public float crown_radius;
        }

        private int m_maxBhd;
        private bool m_useLookup; // use lookup table?
        private List<StampItem> m_stamps;
        private Grid<Stamp> m_lookup;
        private string m_desc;
        private string m_fileName;

        public static Grid<float> distanceGrid() { return m_distance; }

        public void useLookup(bool use) { m_useLookup = use; }
        public int count() { return m_stamps.Count; }
        public string description() { return m_desc; }
        public void setDescription(string s) { m_desc = s; }

        public StampContainer()
        {
            m_stamps = new List<StampItem>();
            m_lookup.setup(1.0F, // cellsize
                           cBHDclassCount, // count x
                           cHDclassCount); // count y
            m_lookup.initialize(null);
            //Debug.WriteLine("grid after init" << gridToString(m_lookup);
            m_maxBhd = -1;
            m_useLookup = true;
        }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private void getKey(float dbh, float hd_value, out int dbh_class, out int hd_class)
        {
            hd_class = (int)((hd_value - cHDclassLow) / cHDclassWidth);
            // dbh_class = int(dbh - cBHDclassLow) / cBHDclassWidth;
            // fixed scheme: smallest classification scheme for tree-diameters:
            // 1cm width from 4 up to 9cm,
            // 2cm bins from 10 to 18cm
            // 4cm bins starting from 20cm, max DBH=255 (with 70 classes)
            if (dbh < 10.0F)
            {
                dbh_class = Math.Max(0, (int)(dbh - 4.0F)); // classes from 0..5
            }
            else if (dbh < 20.0F)
            {
                dbh_class = 6 + (int)((dbh - 10.0F) / 2.0F); // 10-12cm has index 6
            }
            else
            {
                dbh_class = 11 + (int)((dbh - 20.0F) / 4.0F); // 20-24cm has index 11
            }
        }

        /** fill up the nulls in the lookup map */
        private void finalizeSetup()
        {
            if (!m_useLookup)
            {
                return;
            }

            int max_size = 0;
            for (int b = 0; b < cBHDclassCount; b++)
            {
                // find lowest value...
                int h = 0;
                Stamp s = null;
                for (; h < cHDclassCount; h++)
                {
                    s = m_lookup.valueAtIndex(b, h);
                    if (s != null)
                    {
                        // fill up values left from this value
                        for (int hfill = 0; hfill < h; hfill++)
                        {
                            m_lookup[b, hfill] = s;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; h < cHDclassCount; h++)
                {
                    if (m_lookup.valueAtIndex(b, h) == null)
                    {
                        break;
                    }
                    s = m_lookup.valueAtIndex(b, h);
                }
                // fill up the rest...
                for (; h < cHDclassCount; h++)
                {
                    m_lookup[b, h] = s;
                }
                if (s != null)
                {
                    max_size = Math.Max(max_size, s.dataSize());
                }

                // if no stamps in this dbh-class, copy values (from last row)
                if (s == null && b > 0)
                {
                    for (h = 0; h < cHDclassCount; h++)
                    {
                        m_lookup[b, h] = m_lookup[b - 1, h];
                    }
                }
            }

            if (m_lookup.valueAtIndex(0, 0) != null)
            {
                // first values are missing
                int b = 0;
                while (b < cBHDclassCount && m_lookup.valueAtIndex(b, 0) == null)
                {
                    b++;
                }
                for (int fill = 0; fill < b; fill++)
                {
                    for (int h = 0; h < cHDclassCount; h++)
                    {
                        m_lookup[fill, h] = m_lookup[b, h];
                    }
                }
            }
            // distance grid
            if (m_distance.sizeX() < max_size)
            {
                setupDistanceGrid(max_size);
            }
        }

        private void setupDistanceGrid(int size)
        {
            float px_size = Constant.cPxSize;
            m_distance.setup(px_size, size, size);
            for (int p = 0; p < m_distance.count(); ++p)
            {
                Point idx = m_distance.indexOf(p);
                m_distance[p] = MathF.Sqrt(idx.X * idx.X + idx.Y * idx.Y) * px_size;
            }
        }

        private void addStamp(Stamp stamp, int cls_dbh, int cls_hd, float crown_radius_m, float dbh, float hd_value)
        {
            if (m_useLookup)
            {
                if (cls_dbh < 0 || cls_dbh >= cBHDclassCount || cls_hd < 0 || cls_hd >= cHDclassCount)
                {
                    throw new NotSupportedException(String.Format("addStamp: Stamp out of range. dbh={0} hd={1}.", dbh, hd_value));
                }
                m_lookup[cls_dbh, cls_hd] = stamp; // save address in look up table
            } // if (useLookup)

            stamp.setCrownRadius(crown_radius_m);
            StampItem si = new StampItem();
            si.dbh = dbh;
            si.hd = hd_value;
            si.crown_radius = crown_radius_m;
            si.stamp = stamp;
            m_stamps.Add(si); // store entry in list of stamps
        }

        /** add a stamp to the internal storage.
            After loading the function finalizeSetup() must be called to ensure that gaps in the matrix get filled. */
        public void addStamp(Stamp stamp, float dbh, float hd_value, float crown_radius)
        {
            getKey(dbh, hd_value, out int cls_dbh, out int cls_hd); // decode dbh/hd-value
            addStamp(stamp, cls_dbh, cls_hd, crown_radius, dbh, hd_value); // dont set crownradius
        }

        public void addReaderStamp(Stamp stamp, float crown_radius_m)
        {
            double rest = (crown_radius_m % 1.0F) + 0.0001;
            int cls_hd = (int)(rest * 10); // 0 .. 9.99999999
            if (cls_hd >= cHDclassCount)
            {
                cls_hd = cHDclassCount - 1;
            }
            int cls_dbh = (int)(crown_radius_m);
            //Debug.WriteLine("Readerstamp r="<< crown_radius_m<<" index dbh hd:" << cls_dbh << cls_hd;
            stamp.setCrownRadius(crown_radius_m);

            // prepare special keys for reader stamps
            addStamp(stamp, cls_dbh, cls_hd, crown_radius_m, 0.0F, 0.0F); // set crownradius, but not dbh/hd
        }

        /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
            Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
        public Stamp readerStamp(float crown_radius_m)
        {
            // Readers: from 0..10m in 50 steps???
            int cls_hd = (int)(((crown_radius_m % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
            if (cls_hd >= cHDclassCount)
            {
                cls_hd = cHDclassCount - 1;
            }
            int cls_bhd = (int)crown_radius_m;
            Stamp stamp = m_lookup[cls_bhd, cls_hd];
            if (stamp == null)
            {
                Debug.WriteLine("Stamp::readerStamp(): no stamp found for radius " + crown_radius_m);
            }
            return stamp;
        }

        /** fast access for an individual stamp using a lookup table.
            the dimensions of the lookup table are defined by class-constants.
            If stamp is not found there, the more complete list of stamps is searched. */
        public Stamp stamp(float bhd_cm, float height_m)
        {
            float hd_value = 100.0F * height_m / bhd_cm;
            getKey(bhd_cm, hd_value, out int cls_dbh, out int cls_hd);

            // check loopup table
            if (cls_dbh < cBHDclassCount && cls_dbh >= 0 && cls_hd < cHDclassCount && cls_hd >= 0)
            {
                Stamp stamp = m_lookup[cls_dbh, cls_hd];
                if (stamp != null)
                {
                    return stamp;
                }
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("stamp(): not in list: dbh height: " + bhd_cm + height_m + "in" + m_fileName);
                }
            }

            // extra work: search in list...
            // look for a stamp if the HD-ratio is out of range
            if (cls_dbh < cBHDclassCount && cls_dbh >= 0)
            {
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + " (using smallest/largeset HD)");
                }
                if (cls_hd >= cHDclassCount)
                {
                    return m_lookup[cls_dbh, cHDclassCount - 1]; // highest
                }
                return m_lookup[cls_dbh, 0]; // smallest
            }
            // look for a stamp if the DBH is out of range.
            if (cls_hd < cHDclassCount && cls_hd >= 0)
            {
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("DBH for stamp out of range dbh " + bhd_cm + "and h=" + height_m + " -> using largest available DBH.");
                }
                if (cls_dbh >= cBHDclassCount)
                {
                    return m_lookup[cBHDclassCount - 1, cls_hd]; // highest
                }
                return m_lookup[0, cls_hd]; // smallest

            }

            // handle the case DBH and HD are out of range
            if (cls_dbh >= cBHDclassCount && cls_hd < 0)
            {
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + "-> using largest available DBH/smallest HD.");
                }
                return m_lookup[cBHDclassCount - 1, 0];
            }

            // handle the case that DBH is too high and HD is too high (not very likely)
            if (cls_dbh >= cBHDclassCount && cls_hd >= cHDclassCount)
            {
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + "-> using largest available DBH.");
                }
                return m_lookup[cBHDclassCount - 1, cHDclassCount - 1];
            }

            throw new NotSupportedException(String.Format("No stamp defined for dbh " + bhd_cm + " and h=" + height_m));
        }

        public void attachReaderStamps(StampContainer source)
        {
            int found = 0, total = 0;
            foreach (StampItem si in m_stamps)
            {
                Stamp s = source.readerStamp(si.crown_radius);
                si.stamp.setReader(s);
                if (s != null)
                {
                    found++;
                }
                total++;
                //si.crown_radius
            }
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("attachReaderStamps: found " + found + " stamps of " + total);
            }
        }

        public void invert()
        {
            foreach (StampItem si in m_stamps)
            {
                Stamp s = si.stamp;
                float[] p = s.data();
                for (int index = 0; index < p.Length; ++index)
                {
                    p[index] = 1.0F - p[index];
                }
            }
        }

        /// convenience function that loads stamps directly from a single file.
        public void load(string fileName)
        {
            FileInfo readerfile = new FileInfo(fileName);
            if (!readerfile.Exists)
            {
                throw new FileNotFoundException(String.Format("The LIP stampfile {0} cannot be found!", fileName));
            }
            m_fileName = fileName;

            using FileStream stream = readerfile.OpenRead();
            using BinaryReader rin = new BinaryReader(stream);
            Debug.WriteLine("loading stamp file" + fileName);
            load(rin);
        }

        public void load(BinaryReader input)
        {
            UInt32 magic = input.ReadUInt32();
            if (magic != 0xFEED0001)
            {
                throw new NotSupportedException("StampContainer: invalid file type!");
            }
            UInt16 version = input.ReadUInt16();
            if (version != 100)
            {
                throw new NotSupportedException(String.Format("StampContainer: invalid file version: {0}", version));
            }

            int count = input.ReadInt32(); // read count of stamps
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine(count + " stamps to read");
            }

            // TODO: does this interop or does Qt use different size and character encodings?
            m_desc = input.ReadString(); // read textual description of stamp 
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("Stamp notes: " + m_desc);
            }

            for (int i = 0; i < count; i++)
            {
                int type = input.ReadInt32(); // read type
                float dbh = input.ReadSingle();
                float hdvalue = input.ReadSingle();
                float crownradius = input.ReadSingle();
                //Debug.WriteLine("stamp bhd hdvalue type readsum dominance type" + bhd + hdvalue + type + readsum + domvalue + type;

                Stamp stamp = new Stamp(type);
                stamp.load(input);

                if (dbh > 0.0F)
                {
                    addStamp(stamp, dbh, hdvalue, crownradius);
                }
                else
                {
                    addReaderStamp(stamp, crownradius);
                }
            }
            finalizeSetup(); // fill up lookup grid
            if (count == 0)
            {
                throw new NotSupportedException("no stamps loaded!");
            }
        }

        /** Saves all stamps of the container to a binary stream.
          Format: * count of stamps (int32)
                  * a string containing a description (free text) (string)
              for each stamp:
              - type (enum Stamp::StampType, 4, 8, 12, 16, ...)
              - bhd of the stamp (float)
              - hd-value of the tree (float)
              - crownradius of the stamp (float) in [m]
              - the sum of values in the center of the stamp (used for read out)
              - the dominance value of the stamp
              - individual data values (Stamp::save() / Stamp::load())
              -- offset (int) no. of pixels away from center
              -- list of data items (type*type items)
              see also stamp creation (FonStudio application, MainWindow.cpp).
        */
        public void save(BinaryWriter output)
        {
            output.Write((UInt32)0xFEED0001); // magic number
            output.Write((UInt16)100); // version

            output.Write(m_stamps.Count); // count of stamps...
            output.Write(m_desc); // text...
            foreach (StampItem si in m_stamps) 
            {
                int type = si.stamp.dataSize();
                output.Write(type);
                output.Write(si.dbh);
                output.Write(si.hd);
                output.Write(si.crown_radius);
                si.stamp.save(output);
            }
        }

        public string dump()
        {
            StringBuilder res = new StringBuilder(String.Format("****** Dump of StampContainer {0} **********", m_fileName));
            int maxidx;
            foreach (StampItem si in m_stamps)
            {
                res.AppendFormat("Stamp size: {0} offset: {1} dbh: {2} hd-ratio: {3}", Math.Sqrt((double)si.stamp.count()), si.stamp.offset(), si.dbh, si.hd);
                // add data....
                maxidx = 2 * si.stamp.offset() + 1;
                for (int y = 0; y < maxidx; ++y)
                {
                    for (int x = 0; x < maxidx; ++x)
                    {
                        res.Append(si.stamp.data(x, y).ToString() + " ");
                    }
                    res.Append(System.Environment.NewLine);
                }
                res.AppendLine("==============================================");
            }
            res.AppendLine("Dump of lookup map" + System.Environment.NewLine + "=====================");
            for (int s = 0; s < m_lookup.count(); ++s)
            {
                if (m_lookup[s] != null)
                {
                    res.AppendFormat("P: x/y: {0}/{1}{2}", m_lookup.indexOf(s).X, m_lookup.indexOf(s).Y, System.Environment.NewLine);
                }
            }
            res.AppendLine(System.Environment.NewLine + Grid.gridToString(m_lookup));
            return res.ToString();
        }
    }
}
