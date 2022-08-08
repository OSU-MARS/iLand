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
        private readonly int dbhClasses;
        private readonly int heightDiameterClasses;
        private readonly LightStamp?[,] lightStampsByDbhAndHeightDiameterRatio;
        private readonly List<LightStamp> lightStampsWithTreeSizes;

        public string Description { get; set; }

        public TreeSpeciesStamps(string stampFilePath)
        {
            // TODO: size array to actual number of DBH and height:diameter classes in the stamp file and remove the fill up code below
            // For now, size is hard coded to the maximum number of classes available for any species.
            this.dbhClasses = 62; // class count, see StampContainer.GetKey(): classes are smaller at smaller DBH
            this.heightDiameterClasses = 17; // class count: highest ratio: 185-195? 195-205?
            this.lightStampsByDbhAndHeightDiameterRatio = new LightStamp[this.dbhClasses, this.heightDiameterClasses];
            this.lightStampsWithTreeSizes = new();

            this.Description = String.Empty;

            using LightStampReaderArrow stampReader = new(stampFilePath);
            foreach (LightStamp stamp in stampReader.ReadLightStamps())
            {
                if (stamp.DbhInCm > 0.0F)
                {
                    (int diameterClass, int hdClass) = TreeSpeciesStamps.GetClasses(stamp.DbhInCm, stamp.HeightDiameterRatio); // decode dbh/hd-value
                    this.AddStamp(stamp, diameterClass, hdClass); // don't set crownradius
                }
                else
                {
                    // reader stamps are keyed only by crown radius, not DBH or height:diameter ratio
                    Debug.Assert((stamp.DbhInCm == 0.0F) && (stamp.HeightDiameterRatio == 0));

                    int crownRadiusClassIntegerPart = (int)stamp.CrownRadiusInM;
                    float crownRadiusResidual = (stamp.CrownRadiusInM % 1.0F) + 0.0001F;

                    int crownRadiusClassFractionalPart = (int)(10.0F * crownRadiusResidual); // 0 .. 9.99999999
                    if (crownRadiusClassFractionalPart >= this.heightDiameterClasses)
                    {
                        crownRadiusClassFractionalPart = this.heightDiameterClasses - 1;
                    }

                    this.AddStamp(stamp, crownRadiusClassIntegerPart, crownRadiusClassFractionalPart);
                }
            }

            // fill up lookup grid
            int maxStampSize = 0;
            for (int diameterClassIndex = 0; diameterClassIndex < this.dbhClasses; ++diameterClassIndex)
            {
                // find lowest value...
                int heightDiameterRatioClassIndex = 0;
                LightStamp? stamp = null;
                for (; heightDiameterRatioClassIndex < this.heightDiameterClasses; ++heightDiameterRatioClassIndex)
                {
                    stamp = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex];
                    if (stamp != null)
                    {
                        // fill up values left from this value
                        for (int hdClassIndex = 0; hdClassIndex < heightDiameterRatioClassIndex; hdClassIndex++)
                        {
                            this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, hdClassIndex] = stamp;
                        }
                        break;
                    }
                }
                // go to last filled cell...
                for (; heightDiameterRatioClassIndex < this.heightDiameterClasses; ++heightDiameterRatioClassIndex)
                {
                    if (this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex] == null)
                    {
                        break;
                    }
                    stamp = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex];
                }
                // fill up the rest...
                for (; heightDiameterRatioClassIndex < this.heightDiameterClasses; ++heightDiameterRatioClassIndex)
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
                    for (heightDiameterRatioClassIndex = 0; heightDiameterRatioClassIndex < this.heightDiameterClasses; heightDiameterRatioClassIndex++)
                    {
                        this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightDiameterRatioClassIndex] = this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex - 1, heightDiameterRatioClassIndex];
                    }
                }
            }

            if (this.lightStampsByDbhAndHeightDiameterRatio[0, 0] != null)
            {
                // first values are missing
                int b = 0;
                while (b < this.dbhClasses && this.lightStampsByDbhAndHeightDiameterRatio[b, 0] == null)
                {
                    b++;
                }
                for (int diameterClassIndex = 0; diameterClassIndex < b; ++diameterClassIndex)
                {
                    for (int heightClassIndex = 0; heightClassIndex < this.heightDiameterClasses; ++heightClassIndex)
                    {
                        this.lightStampsByDbhAndHeightDiameterRatio[diameterClassIndex, heightClassIndex] = this.lightStampsByDbhAndHeightDiameterRatio[b, heightClassIndex];
                    }
                }
            }
        }

        private void AddStamp(LightStamp stamp, int diameterClass, int heightDiameterClass)
        {
            if ((diameterClass < 0) || (diameterClass >= this.dbhClasses))
            {
                throw new ArgumentOutOfRangeException(nameof(diameterClass));
            }
            if ((heightDiameterClass < 0) || (heightDiameterClass >= this.heightDiameterClasses))
            {
                throw new ArgumentOutOfRangeException(nameof(heightDiameterClass));
            }

            this.lightStampsByDbhAndHeightDiameterRatio[diameterClass, heightDiameterClass] = stamp;
            this.lightStampsWithTreeSizes.Add(stamp);
        }

        public void AttachReaderStamps(TreeSpeciesStamps readerStamps)
        {
            foreach (LightStamp speciesStamp in this.lightStampsWithTreeSizes)
            {
                /** retrieve a read-out-stamp. Readers depend solely on a crown radius.
                    Internally, readers are stored in the same lookup-table, but using a encoding/decoding trick.*/
                // Readers: from 0..10m in 50 steps???
                float crownRadiusInMeters = speciesStamp.CrownRadiusInM;
                int heightDiameterClass = (int)(((crownRadiusInMeters % 1.0F) + 0.0001) * 10); // 0 .. 9.99999999
                if (heightDiameterClass >= this.heightDiameterClasses)
                {
                    heightDiameterClass = this.heightDiameterClasses - 1;
                }
                int crownRadiusClass = (int)crownRadiusInMeters;
                LightStamp? readerStamp = readerStamps.lightStampsByDbhAndHeightDiameterRatio[crownRadiusClass, heightDiameterClass];
                if (readerStamp == null)
                {
                    throw new InvalidOperationException("Reader stamp not found for crown radius class " + crownRadiusClass + " (crown radius " + crownRadiusInMeters + " m) and height class " + heightDiameterClass + " (height diameter ratio " + heightDiameterClass + ").");
                }

                speciesStamp.SetReaderStamp(readerStamp);
            }
        }

        public int Count() 
        { 
            return this.lightStampsWithTreeSizes.Count; 
        }

        /// getKey: decodes a floating point piar of dbh and hd-ratio to indices for the
        /// lookup table containing pointers to the actual stamps.
        private static (int diameterClass, int heightDiameterClass) GetClasses(float dbhInCm, float heightDiameterRatio)
        {
            int heightDiameterClass = (int)((heightDiameterRatio - Constant.LightStamp.HeightDiameterClassMinimum) / Constant.LightStamp.HeightDiameterClassSize);
            // dbh_class = int(dbh - cBHDclassLow) / cBHDclassWidth;
            // fixed scheme: smallest classification scheme for tree-diameters:
            // 1 cm width from 5 up to 9 cm,
            // 2 cm bins from 10 to 20 cm
            // 4 cm bins starting from 20 cm, max DBH at 70 classes = 255 cm
            int diameterClass;
            if (dbhInCm < 4.0F)
            {
                diameterClass = 0; // class 0
            }
            else if (dbhInCm < 10.0F)
            {
                diameterClass = (int)(dbhInCm - 4.0F); // classes from 0-5
            }
            else if (dbhInCm < 20.0F)
            {
                diameterClass = 6 + (int)((dbhInCm - 10.0F) / 2.0F); // 10-12 cm has index 6
            }
            else
            {
                diameterClass = 11 + (int)((dbhInCm - 20.0F) / 4.0F); // 20-24 cm has index 11
            }

            Debug.Assert((diameterClass >= 0) && (heightDiameterClass >= 0));
            return (diameterClass, heightDiameterClass);
        }

        public LightStamp GetStamp(float dbhInCm, float heightInM)
        {
            // retrieve closest matching stamp
            float heightDiameterRatio = 100.0F * heightInM / dbhInCm;
            (int diameterClass, int hdClass) = TreeSpeciesStamps.GetClasses(dbhInCm, heightDiameterRatio);

            int closestDiameterClass = diameterClass < this.dbhClasses ? diameterClass : this.dbhClasses - 1;
            int closestHDclass = hdClass < this.heightDiameterClasses ? hdClass : this.heightDiameterClasses - 1;

            LightStamp? stamp = this.lightStampsByDbhAndHeightDiameterRatio[closestDiameterClass, closestHDclass];
            if (stamp == null)
            {
                throw new ArgumentOutOfRangeException("Stamp for DBH " + dbhInCm + " and height:diameter ratio " + heightDiameterRatio + " (height " + heightInM + " m) not found.");
            }
            //Debug.Assert(((MathF.Abs(dbhInCm - stamp!.DbhInCm) < 2.0F) || (diameterClass >= this.dbhClasses)) && 
            //             ((MathF.Abs(heightDiameterRatio - stamp.HeightDiameterRatio) < 0.5F * Constant.LightStamp.HeightDiameterClassSize) || (hdClass >= this.heightDiameterClasses)));

            return stamp;
        }

        public void WriteToCsv(string filePath)
        {
            // for now, assume all write requests are for .csv
            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using StreamWriter writer = new(stream);

            writer.WriteLine("dbh,heightDiameterRatio,crownRadius,size,centerIndex,x,y,value");
            for (int stampIndex = 0; stampIndex < this.lightStampsWithTreeSizes.Count; ++stampIndex)
            {
                LightStamp stamp = this.lightStampsWithTreeSizes[stampIndex];
                string prefix = stamp.DbhInCm + "," + stamp.HeightDiameterRatio + "," + stamp.CrownRadiusInM;
                stamp.Write(prefix, writer);
            }
        }
    }
}
