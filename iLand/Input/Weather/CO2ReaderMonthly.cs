namespace iLand.Input.Weather
{
    public class CO2ReaderMonthly
    {
        public CO2TimeSeriesMonthly MonthlyCO2 { get; private init; }   

        public CO2ReaderMonthly()
        {
            this.MonthlyCO2 = new();
        }
    }
}
