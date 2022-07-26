namespace iLand.Input.Weather
{
    internal class CO2HeaderMonthlyCsv
    {
        public int CO2 { get; private init; }
        public int Month { get; private init; }
        public int Year { get; private init; }

        public CO2HeaderMonthlyCsv(CsvFile weatherFile)
        {
            this.CO2 = weatherFile.GetColumnIndex("co2");
            this.Month = weatherFile.GetColumnIndex("month");
            this.Year = weatherFile.GetColumnIndex("year");
        }
    }
}
