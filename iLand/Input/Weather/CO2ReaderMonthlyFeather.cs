using Apache.Arrow.Ipc;
using Apache.Arrow;
using System.IO;
using System;
using System.Diagnostics;

namespace iLand.Input.Weather
{
    public class CO2ReaderMonthlyFeather : CO2ReaderMonthly
    {
        public CO2ReaderMonthlyFeather(string co2filePath, Int16 startYear)
        {
            using FileStream co2stream = new(co2filePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader co2file = new(co2stream); // ArrowFileReader.IsFileValid is false until a batch is read

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
                if (this.MonthlyCO2.Capacity <= this.MonthlyCO2.Count + monthsRemainingInBatch)
                {
                    this.MonthlyCO2.Resize(this.MonthlyCO2.Count + monthsRemainingInBatch);
                }

                int monthsInBatch = batch.Length - sourceIndex;
                ReadOnlySpan<byte> monthField = batchFields.Month.Values;
                ReadOnlySpan<float> co2field = batchFields.CO2.Values;

                int destinationIndex = this.MonthlyCO2.Count;
                yearField[sourceIndex..].CopyTo(this.MonthlyCO2.Year.AsSpan()[destinationIndex..]);
                monthField[sourceIndex..].CopyTo(this.MonthlyCO2.Month.AsSpan()[destinationIndex..]);
                co2field[sourceIndex..].CopyTo(this.MonthlyCO2.CO2ConcentrationInPpm.AsSpan()[destinationIndex..]);

                this.MonthlyCO2.Count += monthsInBatch;
                this.MonthlyCO2.Validate(destinationIndex, monthsInBatch);
            }
        }
    }
}
