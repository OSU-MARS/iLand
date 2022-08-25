using Apache.Arrow;
using Apache.Arrow.Ipc;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace iLand.Input.Tree
{
    internal class LightStampReaderArrow : IDisposable
    {
        private readonly ArrowFileReader arrowReader;
        private bool isDisposed;

        public LightStampReaderArrow(string stampFilePath)
        {
            FileStream stream = new(stampFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            this.arrowReader = new(stream);
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.arrowReader.Dispose();
                }

                this.isDisposed = true;
            }
        }

        public IList<LightStamp> ReadLightStamps()
        {
            // can't hoist span conversions as ReadOnlySpan<T> and Memory<T>.Span cannot be used with yield return
            // Implementation options are therefore
            //   1) use yield return anyway and amoritize span conversions by stamp
            //   2) accumulate stamps and return some form of IList<T>
            // The second approach is used here. As of August 2022, tree species have from 89 to 255 stamps (bepe to psme, repectively;
            // stamps.R), hence the default list capacity of 256 stamps.
            List<LightStamp> stamps = new(256);

            LightStamp? currentStamp = null;
            int remainingDataValuesToReadInStamp = 0;
            for (RecordBatch? batch = arrowReader.ReadNextRecordBatch(); batch != null; batch = arrowReader.ReadNextRecordBatch())
            {
                LightStampArrowBatch fields = new(batch);
                ReadOnlySpan<byte> centerIndexField = fields.CenterIndex.Values;
                ReadOnlySpan<float> crownRadiusField = fields.CrownRadiusInM.Values;
                ReadOnlySpan<byte> dataSizeField = fields.DataSize.Values;
                ReadOnlySpan<float> dbhField = fields.DbhInCm.Values;
                ReadOnlySpan<byte> heightDiameterRatioField = fields.HeightDiameterRatio.Values;
                ReadOnlySpan<float> stampField = fields.Value.Values;

                for (int batchIndex = 0; batchIndex < batch.Length; /* batchIndex moved in if-else */)
                {
                    if (remainingDataValuesToReadInStamp > 0)
                    {
                        Debug.Assert(currentStamp != null);
                        Span<float> remainingDataDestination = currentStamp.Data.AsSpan(currentStamp.Data.Length - remainingDataValuesToReadInStamp);
                        stampField.Slice(batchIndex, remainingDataValuesToReadInStamp).CopyTo(remainingDataDestination);

                        batchIndex += remainingDataValuesToReadInStamp;
                        remainingDataValuesToReadInStamp = 0;
                    }
                    else
                    {
                        byte centerCellIndex = centerIndexField[batchIndex];
                        float crownRadiusInM = crownRadiusField[batchIndex];
                        byte dataSize = dataSizeField[batchIndex];
                        float dbhInCm = dbhField[batchIndex];
                        byte heightDiameterRatio = heightDiameterRatioField[batchIndex];

                        // sanity check boundary between stamps is correctly located
                        // For now, data size, DBH, height:diameter, center cell, and crown radius are not checked for consistency
                        // row by row.
                        Debug.Assert((currentStamp == null) || (currentStamp.DataSize != dataSize) || (currentStamp.CenterCellIndex != centerCellIndex) ||
                                     (currentStamp.CrownRadiusInM != crownRadiusInM) || (currentStamp.DbhInCm != dbhInCm) || 
                                     (currentStamp.HeightDiameterRatio != heightDiameterRatio));

                        currentStamp = new(dbhInCm, heightDiameterRatio, crownRadiusInM, centerCellIndex, dataSize);

                        int dataValuesAvailableToRead = currentStamp.Data.Length;
                        int stampEndIndex = batchIndex + dataValuesAvailableToRead;
                        remainingDataValuesToReadInStamp = 0;
                        if (stampEndIndex > batch.Length)
                        {
                            dataValuesAvailableToRead = batch.Length - batchIndex;
                            remainingDataValuesToReadInStamp = currentStamp.Data.Length - dataValuesAvailableToRead;
                        }

                        stampField.Slice(batchIndex, dataValuesAvailableToRead).CopyTo(currentStamp.Data);

                        batchIndex += dataValuesAvailableToRead;
                    }
                    if (remainingDataValuesToReadInStamp == 0)
                    {
                        stamps.Add(currentStamp);
                    }
                }
            }

            return stamps;
        }
    }
}
