using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public class CO2ReaderMonthlyCsv : CO2ReaderMonthly
    {
        public CO2ReaderMonthlyCsv(string co2filePath, Int16 startYear)
        {
            CsvFile co2file = new(co2filePath);
            CO2HeaderMonthlyCsv co2header = new(co2file);

            co2file.Parse((row) =>
            {
                Int16 year = Int16.Parse(row[co2header.Year], NumberStyles.Integer);
                if (year < startYear)
                {
                    return;
                }

                if (this.TimeSeries.Capacity - 12 < this.TimeSeries.Count)
                {
                    // default to expanding capacity by DefaultMonthlyAllocationIncrement
                    int estimatedNewCapacity = this.TimeSeries.Capacity + Constant.Data.DefaultMonthlyAllocationIncrement;
                    if (this.TimeSeries.Count >= 2 * Constant.Data.DefaultMonthlyAllocationIncrement)
                    {
                        // CO₂ .csv file contains a single ordered time series so, once enough of file has been read, attempt to
                        // estimate required capacity from the file's read position in order to limit reallocations of data arrays.
                        // Estimation accuracy can be poor due to read position quantization due to buffered reading and variation
                        // in file content.
                        double positionInFile = row.GetPositionInFile();
                        int estimatedCapacityFromFilePosition = (int)Math.Ceiling((double)this.TimeSeries.Capacity / positionInFile);
                        if (estimatedCapacityFromFilePosition > estimatedNewCapacity)
                        {
                            estimatedNewCapacity = estimatedCapacityFromFilePosition;
                        }
                    }
                    this.TimeSeries.Resize(estimatedNewCapacity);
                }

                byte month = byte.Parse(row[co2header.Month], NumberStyles.Integer);
                float co2concentration = Single.Parse(row[co2header.CO2], NumberStyles.Float);

                int monthIndex = this.TimeSeries.Count;
                this.TimeSeries.Year[monthIndex] = year;
                this.TimeSeries.Month[monthIndex] = month;
                this.TimeSeries.CO2ConcentrationInPpm[monthIndex] = co2concentration;

                this.TimeSeries.Count = monthIndex + 1;
            });

            this.TimeSeries.Validate(0, this.TimeSeries.Count);
        }
    }
}
