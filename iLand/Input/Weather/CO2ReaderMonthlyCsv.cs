using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public class CO2ReaderMonthlyCsv : CO2ReaderMonthly
    {
        public CO2ReaderMonthlyCsv(string co2filePath, int startYear)
        {
            CsvFile co2file = new(co2filePath);
            CO2HeaderMonthlyCsv co2header = new(co2file);

            co2file.Parse((row) =>
            {
                int year = Int32.Parse(row[co2header.Year], CultureInfo.InvariantCulture);
                if (year < startYear)
                {
                    return;
                }

                if (this.MonthlyCO2.Capacity - 12 < this.MonthlyCO2.Count)
                {
                    this.MonthlyCO2.Resize(this.MonthlyCO2.Capacity + Constant.Data.MonthlyWeatherAllocationIncrement);
                }

                int month = Int32.Parse(row[co2header.Month], CultureInfo.InvariantCulture);
                float co2concentration = Single.Parse(row[co2header.CO2], CultureInfo.InvariantCulture);

                int monthIndex = this.MonthlyCO2.Count;
                this.MonthlyCO2.Year[monthIndex] = year;
                this.MonthlyCO2.Month[monthIndex] = month;
                this.MonthlyCO2.CO2ConcentrationInPpm[monthIndex] = co2concentration;
                this.MonthlyCO2.Validate(monthIndex);

                this.MonthlyCO2.Count = monthIndex + 1;
            });
        }
    }
}
