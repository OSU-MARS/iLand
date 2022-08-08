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

        public IEnumerable<LightStamp> ReadLightStamps()
        {
            LightStamp? currentStamp = null;
            int remainingDataValuesToReadInStamp = 0;
            for (RecordBatch? batch = arrowReader.ReadNextRecordBatch(); batch != null; batch = arrowReader.ReadNextRecordBatch())
            {
                LightStampArrowBatch fields = new(batch);
                for (int batchIndex = 0; batchIndex < batch.Length; /* batchIndex moved in if-else */)
                {
                    if (remainingDataValuesToReadInStamp > 0)
                    {
                        Debug.Assert(currentStamp != null);
                        Span<float> remainingDataDestination = currentStamp.Data.AsSpan(currentStamp.Data.Length - remainingDataValuesToReadInStamp);
                        fields.Value.Values.Slice(batchIndex, remainingDataValuesToReadInStamp).CopyTo(remainingDataDestination);

                        batchIndex += remainingDataValuesToReadInStamp;
                        remainingDataValuesToReadInStamp = 0;
                    }
                    else
                    {
                        int centerCellIndex = fields.CenterIndex.Values[batchIndex];
                        float crownRadiusInM = fields.CrownRadiusInM.Values[batchIndex];
                        int dataSize = fields.DataSize.Values[batchIndex];
                        float dbhInCm = fields.DbhInCm.Values[batchIndex];
                        int heightDiameterRatio = fields.HeightDiameterRatio.Values[batchIndex];

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

                        fields.Value.Values.Slice(batchIndex, dataValuesAvailableToRead).CopyTo(currentStamp.Data);

                        batchIndex += dataValuesAvailableToRead;
                    }
                    if (remainingDataValuesToReadInStamp == 0)
                    {
                        yield return currentStamp;
                    }
                }
            }
        }
    }
}
