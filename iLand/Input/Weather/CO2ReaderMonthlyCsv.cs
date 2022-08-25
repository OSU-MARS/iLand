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

                if (this.MonthlyCO2.Capacity - 12 < this.MonthlyCO2.Count)
                {
                    // default to expanding capacity by DefaultMonthlyAllocationIncrement
                    int estimatedNewCapacity = this.MonthlyCO2.Capacity + Constant.Data.DefaultMonthlyAllocationIncrement;
                    if (this.MonthlyCO2.Count >= 2 * Constant.Data.DefaultMonthlyAllocationIncrement)
                    {
                        // CO₂ .csv file contains a single ordered time series so, once enough of file has been read, attempt to
                        // estimate required capacity from the file's read position in order to limit reallocations of data arrays.
                        // Estimation accuracy can be poor due to read position quantization due to buffered reading and variation
                        // in file content.
                        double positionInFile = row.GetPositionInFile();
                        int estimatedCapacityFromFilePosition = (int)Math.Ceiling((double)this.MonthlyCO2.Capacity / positionInFile);
                        if (estimatedCapacityFromFilePosition > estimatedNewCapacity)
                        {
                            estimatedNewCapacity = estimatedCapacityFromFilePosition;
                        }
                    }
                    this.MonthlyCO2.Resize(estimatedNewCapacity);
                }

                byte month = byte.Parse(row[co2header.Month], NumberStyles.Integer);
                float co2concentration = Single.Parse(row[co2header.CO2], NumberStyles.Float);

                int monthIndex = this.MonthlyCO2.Count;
                this.MonthlyCO2.Year[monthIndex] = year;
                this.MonthlyCO2.Month[monthIndex] = month;
                this.MonthlyCO2.CO2ConcentrationInPpm[monthIndex] = co2concentration;

                this.MonthlyCO2.Count = monthIndex + 1;
            });

            this.MonthlyCO2.Validate(0, this.MonthlyCO2.Count);
        }
    }
}
