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
        private const int BHDclassCount = 70; ///< class count, see getKey(): for lower dbhs classes are smaller
        private const int HDclassWidth = 10;
        private const int HDclassLow = 35; ///< hd classes offset is 35: class 0 = 35-45, class 1 = 45-55
        private const int HDclassCount = 16; ///< class count. highest class:  185-195

        public static Grid<float> DistanceGrid { get; private set; } ///< grid holding precalculated distances to the stamp center

        private struct StampItem
        {
            public float CrownRadius { get; set; }
            public float Dbh { get; set; }
            public float HD { get; set; }
            public Stamp Stamp { get; set; }
        }

        private string mFileName;
        private readonly Grid<Stamp> mLookup;
        private readonly List<StampItem> mStamps;

        public string Description { get; set; }
        public bool UseLookup { get; set; } // use lookup table?

        static StampContainer()
        {
            StampContainer.DistanceGrid = new Grid<float>();
        }

        public StampContainer()
        {
            this.mLookup = new Grid<Stamp>();
            this.mStamps = new List<StampItem>();

            this.mLookup.Setup(1.0F, // cellsize
                                BHDclassCount, // count x
                                HDclassCount); // count y
            this.mLookup.Initialize(null);
            //Debug.WriteLine("grid after init" << gridToString(m_lookup);
            this.UseLookup = true;
        }

        public int Count() { return mStamps.Count; }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private void GetKey(float dbh, float hd_value, out int dbh_class, out int hd_class)
        {
            hd_class = (int)((hd_value - HDclassLow) / HDclassWidth);
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
        private void FinalizeSetup()
        {
            if (!UseLookup)
            {
                return;
            }

            int max_size = 0;
            for (int b = 0; b < BHDclassCount; b++)
            {
                // find lowest value...
                int h = 0;
                Stamp s = null;
                for (; h < HDclassCount; h++)
                {
                    s = mLookup[b, h];
                    if (s != null)
                    {
                        // fill up values left from this value
                        for (int hfill = 0; hfill < h; hfill++)
                        {
                            mLookup[b, hfill] = s;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; h < HDclassCount; h++)
                {
                    if (mLookup[b, h] == null)
                    {
                        break;
                    }
                    s = mLookup[b, h];
                }
                // fill up the rest...
                for (; h < HDclassCount; h++)
                {
                    mLookup[b, h] = s;
                }
                if (s != null)
                {
                    max_size = Math.Max(max_size, s.DataSize);
                }

                // if no stamps in this dbh-class, copy values (from last row)
                if (s == null && b > 0)
                {
                    for (h = 0; h < HDclassCount; h++)
                    {
                        mLookup[b, h] = mLookup[b - 1, h];
                    }
                }
            }

            if (mLookup[0, 0] != null)
            {
                // first values are missing
                int b = 0;
                while (b < BHDclassCount && mLookup[b, 0] == null)
                {
                    b++;
                }
                for (int fill = 0; fill < b; fill++)
                {
                    for (int h = 0; h < HDclassCount; h++)
                    {
                        mLookup[fill, h] = mLookup[b, h];
                    }
                }
            }
            // distance grid
            if (DistanceGrid.SizeX < max_size)
            {
                SetupDistanceGrid(max_size);
            }
        }

        private void SetupDistanceGrid(int size)
        {
            float px_size = Constant.LightSize;
            DistanceGrid.Setup(px_size, size, size);
            for (int p = 0; p < DistanceGrid.Count; ++p)
            {
                Point idx = DistanceGrid.IndexOf(p);
                DistanceGrid[p] = MathF.Sqrt(idx.X * idx.X + idx.Y * idx.Y) * px_size;
            }
        }

        private void AddStamp(Stamp stamp, int cls_dbh, int cls_hd, float crown_radius_m, float dbh, float hd_value)
        {
            if (UseLookup)
            {
                if (cls_dbh < 0 || cls_dbh >= BHDclassCount || cls_hd < 0 || cls_hd >= HDclassCount)
                {
                    throw new NotSupportedException(String.Format("addStamp: Stamp out of range. dbh={0} hd={1}.", dbh, hd_value));
                }
                mLookup[cls_dbh, cls_hd] = stamp; // save address in look up table
            } // if (useLookup)

            stamp.SetCrownRadius(crown_radius_m);
            StampItem si = new StampItem()
            {
                Dbh = dbh,
                HD = hd_value,
                CrownRadius = crown_radius_m,
                Stamp = stamp
            };
            mStamps.Add(si); // store entry in list of stamps
        }

        /** add a stamp to the internal storage.
            After loading the function finalizeSetup() must be called to ensure that gaps in the matrix get filled. */
        public void AddStamp(Stamp stamp, float dbh, float hd_value, float crown_radius)
        {
            GetKey(dbh, hd_value, out int cls_dbh, out int cls_hd); // decode dbh/hd-value
            AddStamp(stamp, cls_dbh, cls_hd, crown_radius, dbh, hd_value); // dont set crownradius
        }

        public void AddReaderStamp(Stamp stamp, float crown_radius_m)
        {
            double rest = (crown_radius_m % 1.0F) + 0.0001;
            int cls_hd = (int)(rest * 10); // 0 .. 9.99999999
            if (cls_hd >= HDclassCount)
            {
                cls_hd = HDclassCount - 1;
            }
            int cls_dbh = (int)(crown_radius_m);
            //Debug.WriteLine("Readerstamp r="<< crown_radius_m<<" index dbh hd:" << cls_dbh << cls_hd;
            stamp.SetCrownRadius(crown_radius_m);

            // prepare special keys for reader stamps
            AddStamp(stamp, cls_dbh, cls_hd, crown_radius_m, 0.0F, 0.0F); // set crownradius, but not dbh/hd
        }

        /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
            Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
        public Stamp ReaderStamp(float crown_radius_m)
        {
            // Readers: from 0..10m in 50 steps???
            int cls_hd = (int)(((crown_radius_m % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
            if (cls_hd >= HDclassCount)
            {
                cls_hd = HDclassCount - 1;
            }
            int cls_bhd = (int)crown_radius_m;
            Stamp stamp = mLookup[cls_bhd, cls_hd];
            if (stamp == null)
            {
                Debug.WriteLine("Stamp::readerStamp(): no stamp found for radius " + crown_radius_m);
            }
            return stamp;
        }

        /** fast access for an individual stamp using a lookup table.
            the dimensions of the lookup table are defined by class-constants.
            If stamp is not found there, the more complete list of stamps is searched. */
        public Stamp Stamp(float bhd_cm, float height_m)
        {
            float hd_value = 100.0F * height_m / bhd_cm;
            GetKey(bhd_cm, hd_value, out int cls_dbh, out int cls_hd);

            // check loopup table
            if (cls_dbh < BHDclassCount && cls_dbh >= 0 && cls_hd < HDclassCount && cls_hd >= 0)
            {
                Stamp stamp = mLookup[cls_dbh, cls_hd];
                if (stamp != null)
                {
                    return stamp;
                }
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("stamp(): not in list: dbh height: " + bhd_cm + height_m + "in" + mFileName);
                }
            }

            // extra work: search in list...
            // look for a stamp if the HD-ratio is out of range
            if (cls_dbh < BHDclassCount && cls_dbh >= 0)
            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + " (using smallest/largeset HD)");
                }
                if (cls_hd >= HDclassCount)
                {
                    return mLookup[cls_dbh, HDclassCount - 1]; // highest
                }
                return mLookup[cls_dbh, 0]; // smallest
            }
            // look for a stamp if the DBH is out of range.
            if (cls_hd < HDclassCount && cls_hd >= 0)
            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("DBH for stamp out of range dbh " + bhd_cm + "and h=" + height_m + " -> using largest available DBH.");
                }
                if (cls_dbh >= BHDclassCount)
                {
                    return mLookup[BHDclassCount - 1, cls_hd]; // highest
                }
                return mLookup[0, cls_hd]; // smallest

            }

            // handle the case DBH and HD are out of range
            if (cls_dbh >= BHDclassCount && cls_hd < 0)
            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + "-> using largest available DBH/smallest HD.");
                }
                return mLookup[BHDclassCount - 1, 0];
            }

            // handle the case that DBH is too high and HD is too high (not very likely)
            if (cls_dbh >= BHDclassCount && cls_hd >= HDclassCount)
            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + bhd_cm + " and h=" + height_m + "-> using largest available DBH.");
                }
                return mLookup[BHDclassCount - 1, HDclassCount - 1];
            }

            throw new NotSupportedException(String.Format("No stamp defined for dbh " + bhd_cm + " and h=" + height_m));
        }

        public void AttachReaderStamps(StampContainer source)
        {
            int found = 0, total = 0;
            foreach (StampItem si in mStamps)
            {
                Stamp s = source.ReaderStamp(si.CrownRadius);
                si.Stamp.SetReader(s);
                if (s != null)
                {
                    found++;
                }
                total++;
                //si.crown_radius
            }
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine("attachReaderStamps: found " + found + " stamps of " + total);
            }
        }

        public void Invert()
        {
            foreach (StampItem si in mStamps)
            {
                Stamp s = si.Stamp;
                float[] p = s.Data;
                for (int index = 0; index < p.Length; ++index)
                {
                    p[index] = 1.0F - p[index];
                }
            }
        }

        /// convenience function that loads stamps directly from a single file.
        public void Load(string fileName)
        {
            FileInfo readerfile = new FileInfo(fileName);
            if (!readerfile.Exists)
            {
                throw new FileNotFoundException(String.Format("The LIP stampfile {0} cannot be found!", fileName));
            }
            mFileName = fileName;

            using FileStream stream = readerfile.OpenRead();
            using BinaryReaderBigEndian rin = new BinaryReaderBigEndian(stream);
            Debug.WriteLine("loading stamp file" + fileName);
            Load(rin);
        }

        public void Load(BinaryReader input)
        {
            // LIP files are created with QDataStream, which iLand C++ uses with its big endian defaults
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
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine(count + " stamps to read");
            }

            // TODO: does this interop or does Qt use different size and character encodings?
            Description = input.ReadString(); // read textual description of stamp 
            if (GlobalSettings.Instance.LogInfo())
            {
                Debug.WriteLine("Stamp notes: " + Description);
            }

            for (int i = 0; i < count; i++)
            {
                int type = input.ReadInt32(); // read type
                float dbh = input.ReadSingle();
                float hdvalue = input.ReadSingle();
                float crownradius = input.ReadSingle();
                //Debug.WriteLine("stamp bhd hdvalue type readsum dominance type" + bhd + hdvalue + type + readsum + domvalue + type;

                Stamp stamp = new Stamp(type);
                stamp.Load(input);

                if (dbh > 0.0F)
                {
                    AddStamp(stamp, dbh, hdvalue, crownradius);
                }
                else
                {
                    AddReaderStamp(stamp, crownradius);
                }
            }
            FinalizeSetup(); // fill up lookup grid
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
        public void Save(BinaryWriter output)
        {
            output.Write((UInt32)0xFEED0001); // magic number
            output.Write((UInt16)100); // version

            output.Write(mStamps.Count); // count of stamps...
            output.Write(Description); // text...
            foreach (StampItem si in mStamps) 
            {
                int type = si.Stamp.DataSize;
                output.Write(type);
                output.Write(si.Dbh);
                output.Write(si.HD);
                output.Write(si.CrownRadius);
                si.Stamp.Save(output);
            }
        }

        public string Dump()
        {
            StringBuilder res = new StringBuilder(String.Format("****** Dump of StampContainer {0} **********", mFileName));
            int maxidx;
            foreach (StampItem si in mStamps)
            {
                res.AppendFormat("Stamp size: {0} offset: {1} dbh: {2} hd-ratio: {3}", Math.Sqrt((double)si.Stamp.Count()), si.Stamp.DistanceOffset, si.Dbh, si.HD);
                // add data....
                maxidx = 2 * si.Stamp.DistanceOffset + 1;
                for (int y = 0; y < maxidx; ++y)
                {
                    for (int x = 0; x < maxidx; ++x)
                    {
                        res.Append(si.Stamp[x, y].ToString() + " ");
                    }
                    res.Append(System.Environment.NewLine);
                }
                res.AppendLine("==============================================");
            }
            res.AppendLine("Dump of lookup map" + System.Environment.NewLine + "=====================");
            for (int s = 0; s < mLookup.Count; ++s)
            {
                if (mLookup[s] != null)
                {
                    res.AppendFormat("P: x/y: {0}/{1}{2}", mLookup.IndexOf(s).X, mLookup.IndexOf(s).Y, System.Environment.NewLine);
                }
            }
            res.AppendLine(System.Environment.NewLine + Grid.ToString(mLookup));
            return res.ToString();
        }
    }
}
