using iLand.Input;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace iLand.Tree
{
    /** Collection of stamps for one tree species.
        Per species several stamps are stored (different BHD, different HD relations). This class
        encapsulates storage and access to these stamps. The design goal is to deliver high
        access speeds for the "stamp()" method.
        Use getStamp(bhd, hd) or getStamp(bhd, height) to access. */
    public class TreeSpeciesStamps
    {
        // grid holding precalculated distances to the stamp center
        // thread safe due to lock in FinalizeSetup()
        // TODO: not safe across multiple light cell sizes
        public static Grid<float> DistanceGrid { get; private set; }

        private struct LightStampWithTreeSize
        {
            public float CrownRadius { get; set; }
            public float Dbh { get; set; }
            public float HDratio { get; set; }
            public LightStamp Stamp { get; set; }
        }

        private string? mFileName;
        private readonly Grid<LightStamp?> lightStampsByDbhAndHDRatio;
        private readonly List<LightStampWithTreeSize> lightStampsWithTreeSizes;

        public string Description { get; set; }
        //public bool UseLookup { get; set; } // use lookup table?

        static TreeSpeciesStamps()
        {
            TreeSpeciesStamps.DistanceGrid = new Grid<float>();
        }

        public TreeSpeciesStamps()
        {
            this.lightStampsByDbhAndHDRatio = new Grid<LightStamp?>();
            this.lightStampsWithTreeSizes = new List<LightStampWithTreeSize>();

            this.lightStampsByDbhAndHDRatio.Setup(Constant.Stamp.DbhClassCount, // cellsize
                                                  Constant.Stamp.HeightDiameterClassCount, // count x
                                                  1.0F); // count y
            this.lightStampsByDbhAndHDRatio.Fill(null);

            this.Description = String.Empty;            
            //this.UseLookup = true;
            //Debug.WriteLine("grid after init" << gridToString(m_lookup);
        }

        public int Count() { return lightStampsWithTreeSizes.Count; }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private static void GetClasses(float dbh, float hd_value, out int diameterClass, out int heightDiameterClass)
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
            //if (this.UseLookup == false)
            //{
            //    return;
            //}

            int maxStampSize = 0;
            for (int diameterClass = 0; diameterClass < Constant.Stamp.DbhClassCount; ++diameterClass)
            {
                // find lowest value...
                int hdIndex = 0;
                LightStamp? stamp = null;
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; ++hdIndex)
                {
                    stamp = lightStampsByDbhAndHDRatio[diameterClass, hdIndex];
                    if (stamp != null)
                    {
                        // fill up values left from this value
                        for (int hfill = 0; hfill < hdIndex; hfill++)
                        {
                            lightStampsByDbhAndHDRatio[diameterClass, hfill] = stamp;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; ++hdIndex)
                {
                    if (lightStampsByDbhAndHDRatio[diameterClass, hdIndex] == null)
                    {
                        break;
                    }
                    stamp = lightStampsByDbhAndHDRatio[diameterClass, hdIndex];
                }
                // fill up the rest...
                for (; hdIndex < Constant.Stamp.HeightDiameterClassCount; ++hdIndex)
                {
                    lightStampsByDbhAndHDRatio[diameterClass, hdIndex] = stamp;
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
                        lightStampsByDbhAndHDRatio[diameterClass, hdIndex] = lightStampsByDbhAndHDRatio[diameterClass - 1, hdIndex];
                    }
                }
            }

            if (lightStampsByDbhAndHDRatio[0, 0] != null)
            {
                // first values are missing
                int b = 0;
                while (b < Constant.Stamp.DbhClassCount && lightStampsByDbhAndHDRatio[b, 0] == null)
                {
                    b++;
                }
                for (int fill = 0; fill < b; fill++)
                {
                    for (int h = 0; h < Constant.Stamp.HeightDiameterClassCount; h++)
                    {
                        lightStampsByDbhAndHDRatio[fill, h] = lightStampsByDbhAndHDRatio[b, h];
                    }
                }
            }

            // distance grid
            if (TreeSpeciesStamps.DistanceGrid.CellsX < maxStampSize)
            {
                lock (TreeSpeciesStamps.DistanceGrid)
                {
                    if (TreeSpeciesStamps.DistanceGrid.CellsX < maxStampSize)
                    {
                        float lightCellSize = Constant.LightSize;
                        TreeSpeciesStamps.DistanceGrid.Setup(maxStampSize, maxStampSize, lightCellSize);
                        for (int index = 0; index < TreeSpeciesStamps.DistanceGrid.Count; ++index)
                        {
                            Point cellPosition = TreeSpeciesStamps.DistanceGrid.GetCellPosition(index);
                            TreeSpeciesStamps.DistanceGrid[index] = lightCellSize * MathF.Sqrt(cellPosition.X * cellPosition.X + cellPosition.Y * cellPosition.Y);
                        }
                    }
                }
            }
        }

        private void AddStamp(LightStamp stamp, int diameterClass, int hdClass, float crownRadiusInM, float dbh, float hdRatio)
        {
            //if (this.UseLookup)
            //{
                if (diameterClass < 0 || diameterClass >= Constant.Stamp.DbhClassCount || hdClass < 0 || hdClass >= Constant.Stamp.HeightDiameterClassCount)
                {
                    throw new NotSupportedException(String.Format("addStamp: Stamp out of range. dbh={0} hd={1}.", dbh, hdRatio));
                }
                lightStampsByDbhAndHDRatio[diameterClass, hdClass] = stamp; // save address in look up table
            //} // if (useLookup)

            stamp.SetCrownRadiusAndArea(crownRadiusInM);
            LightStampWithTreeSize si = new LightStampWithTreeSize()
            {
                Dbh = dbh,
                HDratio = hdRatio,
                CrownRadius = crownRadiusInM,
                Stamp = stamp
            };
            lightStampsWithTreeSizes.Add(si); // store entry in list of stamps
        }

        /** add a stamp to the internal storage.
            After loading the function finalizeSetup() must be called to ensure that gaps in the matrix get filled. */
        public void AddStamp(LightStamp stamp, float dbh, float hdRatio, float crownRadius)
        {
            TreeSpeciesStamps.GetClasses(dbh, hdRatio, out int diameterClass, out int hdClass); // decode dbh/hd-value
            this.AddStamp(stamp, diameterClass, hdClass, crownRadius, dbh, hdRatio); // dont set crownradius
        }

        public void AddReaderStamp(LightStamp stamp, float crownRadiusInMeters)
        {
            float rest = (crownRadiusInMeters % 1.0F) + 0.0001F;
            int hdClass = (int)(10.0F * rest); // 0 .. 9.99999999
            if (hdClass >= Constant.Stamp.HeightDiameterClassCount)
            {
                hdClass = Constant.Stamp.HeightDiameterClassCount - 1;
            }
            int diameterClass = (int)crownRadiusInMeters;
            //Debug.WriteLine("Readerstamp r="<< crown_radius_m<<" index dbh hd:" << cls_dbh << cls_hd;
            stamp.SetCrownRadiusAndArea(crownRadiusInMeters);

            // prepare special keys for reader stamps
            this.AddStamp(stamp, diameterClass, hdClass, crownRadiusInMeters, 0.0F, 0.0F); // set crownradius, but not dbh/hd
        }

        /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
            Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
        public LightStamp GetReaderStamp(float crownRadiusInMeters)
        {
            // Readers: from 0..10m in 50 steps???
            int heightDiameterClass = (int)(((crownRadiusInMeters % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
            if (heightDiameterClass >= Constant.Stamp.HeightDiameterClassCount)
            {
                heightDiameterClass = Constant.Stamp.HeightDiameterClassCount - 1;
            }
            int diameterClass = (int)crownRadiusInMeters;
            LightStamp? stamp = lightStampsByDbhAndHDRatio[diameterClass, heightDiameterClass];
            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException(nameof(crownRadiusInMeters));
            }
            return stamp;
        }

        /** fast access for an individual stamp using a lookup table.
            the dimensions of the lookup table are defined by class-constants.
            If stamp is not found there, the more complete list of stamps is searched. */
        public LightStamp GetStamp(float dbhInCm, float heightInM)
        {
            float hdRatio = 100.0F * heightInM / dbhInCm;
            TreeSpeciesStamps.GetClasses(dbhInCm, hdRatio, out int diameterClass, out int hdClass);

            // retrieve stamp from lookup table when tree is within the lookup table's size range
            LightStamp? stamp = null;
            if ((diameterClass < Constant.Stamp.DbhClassCount) && (diameterClass >= 0) && 
                (hdClass < Constant.Stamp.HeightDiameterClassCount) && (hdClass >= 0))
            {
                stamp = lightStampsByDbhAndHDRatio[diameterClass, hdClass];
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
                    stamp = lightStampsByDbhAndHDRatio[diameterClass, Constant.Stamp.HeightDiameterClassCount - 1]; // tree is oversize
                }
                else
                {
                    stamp = lightStampsByDbhAndHDRatio[diameterClass, 0]; // tree is underersize
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
                    stamp = lightStampsByDbhAndHDRatio[Constant.Stamp.DbhClassCount - 1, hdClass]; // tree is oversize
                }
                else
                {
                    stamp = lightStampsByDbhAndHDRatio[0, hdClass]; // tree is undersize
                }
            }
            // both DBH and HD ratio are out of range
            else if ((diameterClass >= Constant.Stamp.DbhClassCount) && (hdClass < 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH/smallest HD.");
                //}
                stamp = lightStampsByDbhAndHDRatio[Constant.Stamp.DbhClassCount - 1, 0];
            }
            // handle the case that DBH is too high and HD ratio is too high (not very likely)
            else if ((diameterClass >= Constant.Stamp.DbhClassCount) && (hdClass >= Constant.Stamp.HeightDiameterClassCount))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH.");
                //}
                stamp = lightStampsByDbhAndHDRatio[Constant.Stamp.DbhClassCount - 1, Constant.Stamp.HeightDiameterClassCount - 1];
            }

            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException("Stamp for DBH " + dbhInCm + " and height " + heightInM + " not found.");
            }
            return stamp;
        }

        public void AttachReaderStamps(TreeSpeciesStamps source)
        {
            int found = 0, total = 0;
            foreach (LightStampWithTreeSize stampItem in this.lightStampsWithTreeSizes)
            {
                LightStamp stamp = source.GetReaderStamp(stampItem.CrownRadius);
                stampItem.Stamp.SetReader(stamp);
                if (stamp != null)
                {
                    ++found;
                }
                ++total;
                //si.crown_radius
            }
            //if (GlobalSettings.Instance.LogInfo())
            //{
            //    Debug.WriteLine("attachReaderStamps: found " + found + " stamps of " + total);
            //}
        }

        public void Invert()
        {
            foreach (LightStampWithTreeSize si in lightStampsWithTreeSizes)
            {
                LightStamp s = si.Stamp;
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

                LightStamp stamp = new LightStamp(type);
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
        public void Write(BinaryWriter output)
        {
            output.Write((UInt32)0xFEED0001); // magic number
            output.Write((UInt16)100); // version

            output.Write(lightStampsWithTreeSizes.Count); // count of stamps...
            output.Write(this.Description); // text...
            foreach (LightStampWithTreeSize stamp in this.lightStampsWithTreeSizes) 
            {
                int type = stamp.Stamp.DataSize;
                output.Write(type);
                output.Write(stamp.Dbh);
                output.Write(stamp.HDratio);
                output.Write(stamp.CrownRadius);
                stamp.Stamp.Write(output);
            }
        }

        public string Dump()
        {
            StringBuilder stampString = new StringBuilder(String.Format("****** Dump of StampContainer {0} **********", this.mFileName));
            foreach (LightStampWithTreeSize stamp in this.lightStampsWithTreeSizes)
            {
                stampString.AppendFormat("Stamp size: {0} offset: {1} dbh: {2} hd-ratio: {3}", Math.Sqrt((double)stamp.Stamp.Count()), stamp.Stamp.CenterCellPosition, stamp.Dbh, stamp.HDratio);
                // add data....
                int maxIndex = 2 * stamp.Stamp.CenterCellPosition + 1;
                for (int y = 0; y < maxIndex; ++y)
                {
                    for (int x = 0; x < maxIndex; ++x)
                    {
                        stampString.Append(stamp.Stamp[x, y].ToString() + " ");
                    }
                    stampString.Append(System.Environment.NewLine);
                }
                stampString.AppendLine("==============================================");
            }
            stampString.AppendLine("Dump of lookup map" + System.Environment.NewLine + "=====================");
            for (int s = 0; s < lightStampsByDbhAndHDRatio.Count; ++s)
            {
                if (lightStampsByDbhAndHDRatio[s] != null)
                {
                    stampString.AppendFormat("P: x/y: {0}/{1}{2}", lightStampsByDbhAndHDRatio.GetCellPosition(s).X, lightStampsByDbhAndHDRatio.GetCellPosition(s).Y, System.Environment.NewLine);
                }
            }
            stampString.AppendLine(lightStampsByDbhAndHDRatio.ToString());
            return stampString.ToString();
        }
    }
}
