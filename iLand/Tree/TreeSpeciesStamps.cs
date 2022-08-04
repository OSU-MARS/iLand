using iLand.Input.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

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

        private readonly LightStamp?[,] lightStampsByDbhAndHeightDiameterRatio;
        private readonly List<LightStampWithTreeSize> lightStampsWithTreeSizes;

        public string Description { get; set; }

        static TreeSpeciesStamps()
        {
            TreeSpeciesStamps.DistanceGrid = new();
        }

        public TreeSpeciesStamps()
        {
            this.lightStampsByDbhAndHeightDiameterRatio = new LightStamp[Constant.LightStamp.DbhClasses, Constant.LightStamp.HeightDiameterClasses];
            this.lightStampsWithTreeSizes = new();

            this.Description = String.Empty;            
            // this.UseLookup = true;
            // Debug.WriteLine("grid after init" << gridToString(m_lookup);
        }

        private void AddStamp(LightStamp stamp, int diameterClass, int hdClass, float crownRadiusInM, float dbhInCm, float hdRatio)
        {
            if ((diameterClass < 0) || (diameterClass >= Constant.LightStamp.DbhClasses) || (hdClass < 0) || (hdClass >= Constant.LightStamp.HeightDiameterClasses))
            {
                throw new NotSupportedException(String.Format("addStamp: Stamp out of range. dbh={0} hd={1}.", dbhInCm, hdRatio));
            }
            this.lightStampsByDbhAndHeightDiameterRatio[diameterClass, hdClass] = stamp;

            stamp.SetCrownRadiusAndArea(crownRadiusInM);
            LightStampWithTreeSize stampWithTreeSize = new()
            {
                DbhInCm = dbhInCm,
                HeightDiameterRatio = hdRatio,
                CrownRadiusInM = crownRadiusInM,
                Stamp = stamp
            };
            this.lightStampsWithTreeSizes.Add(stampWithTreeSize);
        }

        public void AttachReaderStamps(TreeSpeciesStamps source)
        {
            int found = 0, total = 0;
            foreach (LightStampWithTreeSize stampItem in this.lightStampsWithTreeSizes)
            {
                LightStamp stamp = source.GetReaderStamp(stampItem.CrownRadiusInM);
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

        public int Count() 
        { 
            return this.lightStampsWithTreeSizes.Count; 
        }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private static (int diameterClass, int heightDiameterClass) GetClasses(float dbh, float heightDiameterRatio)
        {
            int heightDiameterClass = (int)((heightDiameterRatio - Constant.LightStamp.HeightDiameterClassMinimum) / Constant.LightStamp.HeightDiameterClassSize);
            // dbh_class = int(dbh - cBHDclassLow) / cBHDclassWidth;
            // fixed scheme: smallest classification scheme for tree-diameters:
            // 1cm width from 5 up to 9 cm,
            // 2cm bins from 10 to 20 cm
            // 4cm bins starting from 20 cm, max DBH at 70 classes = 255 cm
            int diameterClass;
            if (dbh < 4.0F)
            {
                diameterClass = 0; // class 0
            }
            else if (dbh < 10.0F)
            {
                diameterClass = (int)(dbh - 4.0F); // classes from 0-5
            }
            else if (dbh < 20.0F)
            {
                diameterClass = 6 + (int)((dbh - 10.0F) / 2.0F); // 10-12 cm has index 6
            }
            else
            {
                diameterClass = 11 + (int)((dbh - 20.0F) / 4.0F); // 20-24 cm has index 11
            }

            return (diameterClass, heightDiameterClass);
        }

        /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
            Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
        public LightStamp GetReaderStamp(float crownRadiusInMeters)
        {
            // Readers: from 0..10m in 50 steps???
            int heightDiameterClass = (int)(((crownRadiusInMeters % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
            if (heightDiameterClass >= Constant.LightStamp.HeightDiameterClasses)
            {
                heightDiameterClass = Constant.LightStamp.HeightDiameterClasses - 1;
            }
            int diameterClass = (int)crownRadiusInMeters;
            LightStamp? stamp = lightStampsByDbhAndHeightDiameterRatio[diameterClass, heightDiameterClass];
            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException(nameof(crownRadiusInMeters));
            }
            return stamp;
        }

        /** fast access for an individual stamp using a lookup table
            The dimensions of the lookup table are defined by class-constants.
            If stamp is not found there, the more complete list of stamps is searched. */
        public LightStamp GetStamp(float dbhInCm, float heightInM)
        {
            float heightDiameterRatio = 100.0F * heightInM / dbhInCm;
            (int diameterClass, int hdClass) = TreeSpeciesStamps.GetClasses(dbhInCm, heightDiameterRatio);

            // retrieve stamp from lookup table when tree is within the lookup table's size range
            LightStamp? stamp = null;
            if ((diameterClass < Constant.LightStamp.DbhClasses) && (diameterClass >= 0) && 
                (hdClass < Constant.LightStamp.HeightDiameterClasses) && (hdClass >= 0))
            {
                stamp = lightStampsByDbhAndHeightDiameterRatio[diameterClass, hdClass];
            }
            // find a stamp of matching diameter if the HD-ratio is out of range
            else if ((diameterClass < Constant.LightStamp.DbhClasses) && (diameterClass >= 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + " (using smallest/largeset HD)");
                //}
                if (hdClass >= Constant.LightStamp.HeightDiameterClasses)
                {
                    stamp = lightStampsByDbhAndHeightDiameterRatio[diameterClass, Constant.LightStamp.HeightDiameterClasses - 1]; // tree is oversize
                }
                else
                {
                    stamp = lightStampsByDbhAndHeightDiameterRatio[diameterClass, 0]; // tree is underersize
                }
            }
            // find a stamp of matching height-diameter ratio if the DBH is out of range.
            else if ((hdClass < Constant.LightStamp.HeightDiameterClasses) && (hdClass >= 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH for stamp out of range dbh " + dbhInCm + "and h=" + heightInM + " -> using largest available DBH.");
                //}
                if (diameterClass >= Constant.LightStamp.DbhClasses)
                {
                    stamp = lightStampsByDbhAndHeightDiameterRatio[Constant.LightStamp.DbhClasses - 1, hdClass]; // tree is oversize
                }
                else
                {
                    stamp = lightStampsByDbhAndHeightDiameterRatio[0, hdClass]; // tree is undersize
                }
            }
            // both DBH and HD ratio are out of range
            else if ((diameterClass >= Constant.LightStamp.DbhClasses) && (hdClass < 0))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH/smallest HD.");
                //}
                stamp = lightStampsByDbhAndHeightDiameterRatio[Constant.LightStamp.DbhClasses - 1, 0];
            }
            // handle the case that DBH is too high and HD ratio is too high (not very likely)
            else if ((diameterClass >= Constant.LightStamp.DbhClasses) && (hdClass >= Constant.LightStamp.HeightDiameterClasses))
            {
                //if (GlobalSettings.Instance.LogDebug())
                //{
                //    Debug.WriteLine("DBH AND HD for stamp out of range dbh " + dbhInCm + " and h=" + heightInM + "-> using largest available DBH.");
                //}
                stamp = lightStampsByDbhAndHeightDiameterRatio[Constant.LightStamp.DbhClasses - 1, Constant.LightStamp.HeightDiameterClasses - 1];
            }

            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException("Stamp for DBH " + dbhInCm + " and height " + heightInM + " not found.");
            }
            return stamp;
        }

        /// convenience function that loads stamps directly from a single file.
        public void Load(string stampFilePath)
        {
            using FileStream stampStream = new(stampFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using StampReaderBigEndian stampReader = new(stampStream);
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
                float dbhInCm = stampReader.ReadSingle();
                float heightDiameterRatio = stampReader.ReadSingle();
                float crownRadiusInM = stampReader.ReadSingle();
                Debug.Assert((dbhInCm >= 0.0F) && (dbhInCm < 225.0F), stampFilePath + ": DBH"); // cm, maximum set by Pacific Northwest Douglas-fir
                Debug.Assert((heightDiameterRatio >= 0.0F) && (heightDiameterRatio < 200.0F), stampFilePath + ": height-diameter ratio"); // ratio
                Debug.Assert((crownRadiusInM >= 0.0F) && (crownRadiusInM < 50.0F), stampFilePath + ": crown radius"); // m
                // Debug.WriteLine("stamp bhd hdvalue type readsum dominance type" + bhd + hdvalue + type + readsum + domvalue + type;

                LightStamp stamp = new(type);
                stamp.Load(stampReader);

                if (dbhInCm > 0.0F)
                {
                    (int diameterClass, int hdClass) = TreeSpeciesStamps.GetClasses(dbhInCm, heightDiameterRatio); // decode dbh/hd-value
                    this.AddStamp(stamp, diameterClass, hdClass, crownRadiusInM, dbhInCm, heightDiameterRatio); // don't set crownradius
                }
                else
                {
                    float crownRadiusResidual = (crownRadiusInM % 1.0F) + 0.0001F;
                    int hdClass = (int)(10.0F * crownRadiusResidual); // 0 .. 9.99999999
                    if (hdClass >= Constant.LightStamp.HeightDiameterClasses)
                    {
                        hdClass = Constant.LightStamp.HeightDiameterClasses - 1;
                    }
                    stamp.SetCrownRadiusAndArea(crownRadiusInM);

                    // reader stamps are keyed only by crownradius, not DBH or height:diameter ratio
                    int diameterClass = (int)crownRadiusInM;
                    this.AddStamp(stamp, diameterClass, hdClass, crownRadiusInM, 0.0F, 0.0F);
                }
            }

            // fill up lookup grid
            int maxStampSize = 0;
            for (int diameterClassIndex = 0; diameterClassIndex < Constant.LightStamp.DbhClasses; ++diameterClassIndex)
            {
                // find lowest value...
                int heightDiameterRatioClassIndex = 0;
                LightStamp? stamp = null;
                for (; heightDiameterRatioClassIndex < Constant.LightStamp.HeightDiameterClasses; ++heightDiameterRatioClassIndex)
                {
                    stamp = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex];
                    if (stamp != null)
                    {
                        // fill up values left from this value
                        for (int hfill = 0; hfill < heightDiameterRatioClassIndex; hfill++)
                        {
                            this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, hfill] = stamp;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; heightDiameterRatioClassIndex < Constant.LightStamp.HeightDiameterClasses; ++heightDiameterRatioClassIndex)
                {
                    if (this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex] == null)
                    {
                        break;
                    }
                    stamp = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex];
                }
                // fill up the rest...
                for (; heightDiameterRatioClassIndex < Constant.LightStamp.HeightDiameterClasses; ++heightDiameterRatioClassIndex)
                {
                    this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex] = stamp;
                }
                if (stamp != null)
                {
                    maxStampSize = Math.Max(maxStampSize, stamp.DataSize);
                }

                // if no stamps in this dbh-class, copy values (from last row)
                if (stamp == null && diameterClassIndex > 0)
                {
                    for (heightDiameterRatioClassIndex = 0; heightDiameterRatioClassIndex < Constant.LightStamp.HeightDiameterClasses; heightDiameterRatioClassIndex++)
                    {
                        this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex] = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex - 1, heightDiameterRatioClassIndex];
                    }
                }
            }

            if (this.lightStampsByDbhAndHeightDiameterRatio[0, 0] != null)
            {
                // first values are missing
                int b = 0;
                while (b < Constant.LightStamp.DbhClasses && this.lightStampsByDbhAndHeightDiameterRatio[b, 0] == null)
                {
                    b++;
                }
                for (int fill = 0; fill < b; fill++)
                {
                    for (int h = 0; h < Constant.LightStamp.HeightDiameterClasses; h++)
                    {
                        this.lightStampsByDbhAndHeightDiameterRatio[fill, h] = this.lightStampsByDbhAndHeightDiameterRatio[b, h];
                    }
                }
            }

            // distance grid
            if (TreeSpeciesStamps.DistanceGrid.SizeX < maxStampSize)
            {
                lock (TreeSpeciesStamps.DistanceGrid)
                {
                    if (TreeSpeciesStamps.DistanceGrid.SizeX < maxStampSize)
                    {
                        float lightCellSize = Constant.LightCellSizeInM;
                        TreeSpeciesStamps.DistanceGrid.Setup(maxStampSize, maxStampSize, lightCellSize);
                        for (int index = 0; index < TreeSpeciesStamps.DistanceGrid.CellCount; ++index)
                        {
                            Point cellPosition = TreeSpeciesStamps.DistanceGrid.GetCellXYIndex(index);
                            TreeSpeciesStamps.DistanceGrid[index] = lightCellSize * MathF.Sqrt(cellPosition.X * cellPosition.X + cellPosition.Y * cellPosition.Y);
                        }
                    }
                }
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
                output.Write(stamp.DbhInCm);
                output.Write(stamp.HeightDiameterRatio);
                output.Write(stamp.CrownRadiusInM);
                stamp.Stamp.Write(output);
            }
        }

        public void Write(StreamWriter writer)
        {
            writer.WriteLine("dbh,heightDiameterRatio,crownRadius,size,centerIndex,x,y,value");
            for (int stampIndex = 0; stampIndex < this.lightStampsWithTreeSizes.Count; ++stampIndex)
            {
                LightStampWithTreeSize stamp = this.lightStampsWithTreeSizes[stampIndex];
                string prefix = stamp.DbhInCm + "," + stamp.HeightDiameterRatio + "," + stamp.CrownRadiusInM;
                stamp.Stamp.Write(prefix, writer);
            }

            //stampString.AppendLine("Dump of lookup map" + System.Environment.NewLine + "=====================");
            //for (int stampIndex = 0; stampIndex < this.lightStampsByDbhAndHeightDiameterRatio.Length; ++stampIndex)
            //{
            //    if (this.lightStampsByDbhAndHeightDiameterRatio[stampIndex] != null)
            //    {
            //        stampString.AppendFormat("P: x/y: {0}/{1}{2}", lightStampsByDbhAndHeightDiameterRatio.GetCellXYIndex(stampIndex).X, lightStampsByDbhAndHeightDiameterRatio.GetCellXYIndex(stampIndex).Y, System.Environment.NewLine);
            //    }
            //}
            //stampString.AppendLine(this.lightStampsByDbhAndHeightDiameterRatio.ToString());
        }

        public void Write(string filePath)
        {
            // for now, assume all write requests are for .csv
            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using StreamWriter writer = new(stream);
            this.Write(writer);
        }

        private struct LightStampWithTreeSize
        {
            public float CrownRadiusInM { get; init; }
            public float DbhInCm { get; init; }
            public float HeightDiameterRatio { get; init; }
            public LightStamp Stamp { get; init; }
        }
    }
}
