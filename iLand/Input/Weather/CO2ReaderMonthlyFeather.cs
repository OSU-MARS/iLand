using Apache.Arrow.Ipc;
using Apache.Arrow;
using System.IO;
using System;
using Apache.Arrow.Compression;

namespace iLand.Input.Weather
{
    public class CO2ReaderMonthlyFeather : CO2ReaderMonthly
    {
        public CO2ReaderMonthlyFeather(string co2filePath, Int16 startYear)
        {
            using FileStream co2stream = new(co2filePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader co2file = new(co2stream, new CompressionCodecFactory()); // ArrowFileReader.IsFileValid is false until a batch is read

            for (RecordBatch? batch = co2file.ReadNextRecordBatch(); batch != null; batch = co2file.ReadNextRecordBatch())
            {
                CO2ArrowBatchMonthly batchFields = new(batch);
                ReadOnlySpan<Int16> yearField = batchFields.Year.Values;

                int sourceIndex = 0;
                for ( ; sourceIndex < batch.Length; ++sourceIndex)
                {
                    Int16 year = yearField[sourceIndex];
                    if (year >= startYear)
                    {
                        break;
                    }
                }

                int monthsRemainingInBatch = batch.Length - sourceIndex;
                if (this.TimeSeries.Capacity <= this.TimeSeries.Count + monthsRemainingInBatch)
                {
                    // for now, assume CO₂ time series fits in a single record batch
                    // Monthly time series up to 5461 years fit in default R arrow::write_feather() batch length of 65536.
                    this.TimeSeries.Resize(this.TimeSeries.Count + monthsRemainingInBatch);
                }

                int monthsInBatch = batch.Length - sourceIndex;
                ReadOnlySpan<byte> monthField = batchFields.Month.Values;
                ReadOnlySpan<float> co2field = batchFields.CO2.Values;

                int destinationIndex = this.TimeSeries.Count;
                yearField[sourceIndex..].CopyTo(this.TimeSeries.Year.AsSpan()[destinationIndex..]);
                monthField[sourceIndex..].CopyTo(this.TimeSeries.Month.AsSpan()[destinationIndex..]);
                co2field[sourceIndex..].CopyTo(this.TimeSeries.CO2ConcentrationInPpm.AsSpan()[destinationIndex..]);

                this.TimeSeries.Count += monthsInBatch;
                this.TimeSeries.Validate(destinationIndex, monthsInBatch);
            }
        }
    }
}
