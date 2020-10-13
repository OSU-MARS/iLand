using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace iLand.Core
{
    /** Collection of stamps for one tree species.
        @ingroup core
        Per species several stamps are stored (different BHD, different HD relations). This class
        encapsulates storage and access to these stamps. The design goal is to deliver high
        access speeds for the "stamp()" method.
        Use getStamp(bhd, hd) or getStamp(bhd, height) to access. */
    public class SpeciesStamps
    {
        ///< grid holding precalculated distances to the stamp center
        // thread safe due to lock in FinalizeSetup()
        public static Grid<float> DistanceGrid { get; private set; }

        private struct StampItem
        {
            public float CrownRadius { get; set; }
            public float Dbh { get; set; }
            public float HDratio { get; set; }
            public Stamp Stamp { get; set; }
        }

        private string mFileName;
        private readonly Grid<Stamp> mStampsByClass;
        private readonly List<StampItem> mStamps;

        public string Description { get; set; }
        public bool UseLookup { get; set; } // use lookup table?

        static SpeciesStamps()
        {
            SpeciesStamps.DistanceGrid = new Grid<float>();
        }

        public SpeciesStamps()
        {
            this.mStampsByClass = new Grid<Stamp>();
            this.mStamps = new List<StampItem>();

            this.mStampsByClass.Setup(1.0F, // cellsize
                                      Constant.Stamp.DbhClassCount, // count x
                                      Constant.Stamp.HeightDiameterClassCount); // count y
            this.mStampsByClass.Initialize(null);
            //Debug.WriteLine("grid after init" << gridToString(m_lookup);
            this.UseLookup = true;
        }

        public int Count() { return mStamps.Count; }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private void GetClasses(float dbh, float hd_value, out int diameterClass, out int heightDiameterClass)
        {
            heightDiameterClass = (int)((hd_value - Constant.Stamp.HeightDiameterClassMinimum) / Constant.Stamp.HeightDiameterClassSize);
            // dbh_class = int(dbh - cBHDclassLow) / cBHDclassWidth;
            // fixed scheme: smallest classification scheme for tree-diameters:
            // 1cm width from 4 up to 9cm,
            // 2cm bins from 10 to 18cm
            // 4cm bins starting from 20cm, max DBH=255 (with 70 classes)
            if (dbh < 10.0F)
            {
                diameterClass = Math.Max(0, (int)(dbh - 4.0F)); // classes from 0..5
            }
            else if (dbh < 20.0F)
            {
                diameterClass = 6 + (int)((dbh - 10.0F) / 2.0F); // 10-12cm has index 6
            }
            else
            {
                diameterClass = 11 + (int)((dbh - 20.0F) / 4.0F); // 20-24cm has index 11
            }
        }

        /** fill up the nulls in the lookup map */
        private void FinalizeSetup()
        {
            if (this.UseLookup == false)
            {
                return;
            }

            int maxStampSize = 0;
            for (int diameterClass = 0; diameterClass < Constant.Stamp.DbhClassCount; diameterClass++)
            {
                // find lowest value...
                int hdIndex = 0;
                Stamp stamp = null;
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; hdIndex++)
                {
                    stamp = mStampsByClass[diameterClass, hdIndex];
                    if (stamp != null)
                    {
                        // fill up values left from this value
                        for (int hfill = 0; hfill < hdIndex; hfill++)
                        {
                            mStampsByClass[diameterClass, hfill] = stamp;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; hdIndex++)
                {
                    if (mStampsByClass[diameterClass, hdIndex] == null)
                    {
                        break;
                    }
                    stamp = mStampsByClass[diameterClass, hdIndex];
                }
                // fill up the rest...
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; hdIndex++)
                {
                    mStampsByClass[diameterClass, hdIndex] = stamp;
                }
                if (stamp != null)
                {
                    maxStampSize = Math.Max(maxStampSize, stamp.DataSize);
                }

                // if no stamps in this dbh-class, copy values (from last row)
                if (stamp == null && diameterClass > 0)
                {
                    for (hdIndex = 0; hdIndex < Constant.Stamp.HeightDiameterClassCount; hdIndex++)
                    {
                        mStampsByClass[diameterClass, hdIndex] = mStampsByClass[diameterClass - 1, hdIndex];
                    }
                }
            }

            if (mStampsByClass[0, 0] != null)
            {
                // first values are missing
                int b = 0;
                while (b < Constant.Stamp.DbhClassCount && mStampsByClass[b, 0] == null)
                {
                    b++;
                }
                for (int fill = 0; fill < b; fill++)
                {
                    for (int h = 0; h < Constant.Stamp.HeightDiameterClassCount; h++)
                    {
                        mStampsByClass[fill, h] = mStampsByClass[b, h];
                    }
                }
            }

            // distance grid
            if (SpeciesStamps.DistanceGrid.CellsX < maxStampSize)
            {
                lock (SpeciesStamps.DistanceGrid)
                {
                    if (SpeciesStamps.DistanceGrid.CellsX < maxStampSize)
                    {
                        float lightCellSize = Constant.LightSize;
                        SpeciesStamps.DistanceGrid.Setup(lightCellSize, maxStampSize, maxStampSize);
                        for (int index = 0; index < SpeciesStamps.DistanceGrid.Count; ++index)
                        {
                            Point cellPosition = SpeciesStamps.DistanceGrid.IndexOf(index);
                            SpeciesStamps.DistanceGrid[index] = lightCellSize * MathF.Sqrt(cellPosition.X * cellPosition.X + cellPosition.Y * cellPosition.Y);
                        }
                    }
                }
            }
        }

        private void AddStamp(Stamp stamp, int diameterClass, int hdClass, float crownRadiusInM, float dbh, float hdRatio)
        {
            if (this.UseLookup)
            {
                if (diameterClass < 0 || diameterClass >= Constant.Stamp.DbhClassCount || hdClass < 0 || hdClass >= Constant.Stamp.HeightDiameterClassCount)
                {
                    throw new NotSupportedException(String.Format("addStamp: Stamp out of range. dbh={0} hd={1}.", dbh, hdRatio));
                }
                mStampsByClass[diameterClass, hdClass] = stamp; // save address in look up table
            } // if (useLookup)

            stamp.SetCrownRadius(crownRadiusInM);
            StampItem si = new StampItem()
            {
                Dbh = dbh,
                HDratio = hdRatio,
                CrownRadius = crownRadiusInM,
                Stamp = stamp
            };
            mStamps.Add(si); // store entry in list of stamps
        }

        /** add a stamp to the internal storage.
            After loading the function finalizeSetup() must be called to ensure that gaps in the matrix get filled. */
        public void AddStamp(Stamp stamp, float dbh, float hdRatio, float crownRadius)
        {
            this.GetClasses(dbh, hdRatio, out int diameterClass, out int hdClass); // decode dbh/hd-value
            this.AddStamp(stamp, diameterClass, hdClass, crownRadius, dbh, hdRatio); // dont set crownradius
        }

        public void AddReaderStamp(Stamp stamp, float crownRadiusInMeters)
        {
            double rest = (crownRadiusInMeters % 1.0F) + 0.0001;
            int hdClass = (int)(rest * 10); // 0 .. 9.99999999
            if (hdClass >= Constant.Stamp.HeightDiameterClassCount)
            {
                hdClass = Constant.Stamp.HeightDiameterClassCount - 1;
            }
            int diameterClass = (int)crownRadiusInMeters;
            //Debug.WriteLine("Readerstamp r="<< crown_radius_m<<" index dbh hd:" << cls_dbh << cls_hd;
            stamp.SetCrownRadius(crownRadiusInMeters);

            // prepare special keys for reader stamps
            this.AddStamp(stamp, diameterClass, hdClass, crownRadiusInMeters, 0.0F, 0.0F); // set crownradius, but not dbh/hd
        }

        /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
            Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
        public Stamp GetReaderStamp(float crownRadiusInMeters)
        {
            // Readers: from 0..10m in 50 steps???
            int heightDiameterClass = (int)(((crownRadiusInMeters % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
            if (heightDiameterClass >= Constant.Stamp.HeightDiameterClassCount)
            {
                heightDiameterClass = Constant.Stamp.HeightDiameterClassCount - 1;
            }
            int diameterClass = (int)crownRadiusInMeters;
            Stamp stamp = mStampsByClass[diameterClass, heightDiameterClass];
            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException(nameof(crownRadiusInMeters));
            }
            return stamp;
        }

        /** fast access for an individual stamp using a lookup table.
            the dimensions of the lookup table are defined by class-constants.
            If stamp is not found there, the more complete list of stamps is searched. */
        public Stamp GetStamp(float dbhInCm, float heightInM)
        {
            float hdRatio = 100.0F * heightInM / dbhInCm;
            this.GetClasses(dbhInCm, hdRatio, out int diameterClass, out int hdClass);

            // retrieve stamp from lookup table when tree is within the lookup table's size range
            Stamp stamp = null;
            if ((diameterClass < Constant.Stamp.DbhClassCount) && (diameterClass >= 0) && 
                (hdClass < Constant.Stamp.HeightDiameterClassCount) && (hdClass >= 0))
            {
                stamp = mStampsByClass[diameterClass, hdClass];
            }
            // find a stamp of matching diameter if the HD-ratio is out of range
            else if ((diameterClass < Constant.Stamp.DbhClassCount) && (diameterClass >= 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + " (using smallest/largeset HD)");
                //}
                if (hdClass >= Constant.Stamp.HeightDiameterClassCount)
                {
                    stamp = mStampsByClass[diameterClass, Constant.Stamp.HeightDiameterClassCount - 1]; // tree is oversize
                }
                else
                {
                    stamp = mStampsByClass[diameterClass, 0]; // tree is underersize
                }
            }
            // find a stamp of matching height-diameter ratio if the DBH is out of range.
            else if (hdClass < Constant.Stamp.HeightDiameterClassCount && hdClass >= 0)
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH for stamp out of range dbh " + dbhInCm + "and h=" + heightInM + " -> using largest available DBH.");
                //}
                if (diameterClass >= Constant.Stamp.DbhClassCount)
                {
                    stamp = mStampsByClass[Constant.Stamp.DbhClassCount - 1, hdClass]; // tree is oversize
                }
                else
                {
                    stamp = mStampsByClass[0, hdClass]; // tree is undersize
                }
            }
            // both DBH and HD ratio are out of range
            else if ((diameterClass >= Constant.Stamp.DbhClassCount) && (hdClass < 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH/smallest HD.");
                //}
                stamp = mStampsByClass[Constant.Stamp.DbhClassCount - 1, 0];
            }
            // handle the case that DBH is too high and HD ratio is too high (not very likely)
            else if ((diameterClass >= Constant.Stamp.DbhClassCount) && (hdClass >= Constant.Stamp.HeightDiameterClassCount))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH.");
                //}
                stamp = mStampsByClass[Constant.Stamp.DbhClassCount - 1, Constant.Stamp.HeightDiameterClassCount - 1];
            }

            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException("Stamp for DBH " + dbhInCm + " and height " + heightInM + " not found.");
            }
            return stamp;
        }

        public void AttachReaderStamps(SpeciesStamps source)
        {
            int found = 0, total = 0;
            foreach (StampItem si in mStamps)
            {
                Stamp stamp = source.GetReaderStamp(si.CrownRadius);
                si.Stamp.SetReader(stamp);
                if (stamp != null)
                {
                    found++;
                }
                total++;
                //si.crown_radius
            }
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("attachReaderStamps: found " + found + " stamps of " + total);
            //}
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
            FileInfo stampFile = new FileInfo(fileName);
            if (!stampFile.Exists)
            {
                throw new FileNotFoundException(String.Format("The LIP stampfile {0} cannot be found!", fileName));
            }
            mFileName = fileName;

            using FileStream stampStream = stampFile.OpenRead();
            using StampReaderBigEndian stampReader = new StampReaderBigEndian(stampStream);
            // Debug.WriteLine("loading stamp file " + fileName);
            // LIP files are created with QDataStream, which iLand C++ uses with its big endian defaults
            UInt32 magic = stampReader.ReadUInt32();
            if (magic != 0xFEED0001)
            {
                throw new NotSupportedException("StampContainer: invalid file type!");
            }
            UInt16 version = stampReader.ReadUInt16();
            if (version != 100)
            {
                throw new NotSupportedException(String.Format("StampContainer: invalid file version: {0}", version));
            }

            int stampCount = stampReader.ReadInt32(); // read count of stamps
            if (stampCount <= 0)
            {
                throw new NotSupportedException("no stamps loaded!");
            }
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine(stampCount + " stamps to read");
            //}

            // TODO: does this interop or does Qt use different size and character encodings?
            this.Description = stampReader.ReadString(); // read textual description of stamp 
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("Stamp notes: " + Description);
            //}

            for (int stampIndex = 0; stampIndex < stampCount; stampIndex++)
            {
                int type = stampReader.ReadInt32(); // read type
                float dbh = stampReader.ReadSingle();
                float hdRatio = stampReader.ReadSingle();
                float crownRadius = stampReader.ReadSingle();
                Debug.Assert((dbh >= 0.0F) && (dbh < 200.0F)); // cm
                Debug.Assert((hdRatio >= 0.0F) && (hdRatio < 200.0F)); // ratio
                Debug.Assert((crownRadius >= 0.0F) && (crownRadius < 50.0F)); // m
                //Debug.WriteLine("stamp bhd hdvalue type readsum dominance type" + bhd + hdvalue + type + readsum + domvalue + type;

                Stamp stamp = new Stamp(type);
                stamp.Load(stampReader);

                if (dbh > 0.0F)
                {
                    AddStamp(stamp, dbh, hdRatio, crownRadius);
                }
                else
                {
                    AddReaderStamp(stamp, crownRadius);
                }
            }
            FinalizeSetup(); // fill up lookup grid
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
                output.Write(si.HDratio);
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
                res.AppendFormat("Stamp size: {0} offset: {1} dbh: {2} hd-ratio: {3}", Math.Sqrt((double)si.Stamp.Count()), si.Stamp.CenterCellPosition, si.Dbh, si.HDratio);
                // add data....
                maxidx = 2 * si.Stamp.CenterCellPosition + 1;
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
            for (int s = 0; s < mStampsByClass.Count; ++s)
            {
                if (mStampsByClass[s] != null)
                {
                    res.AppendFormat("P: x/y: {0}/{1}{2}", mStampsByClass.IndexOf(s).X, mStampsByClass.IndexOf(s).Y, System.Environment.NewLine);
                }
            }
            res.AppendLine(mStampsByClass.ToString());
            return res.ToString();
        }
    }
}
