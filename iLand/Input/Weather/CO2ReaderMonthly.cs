namespace iLand.Input.Weather
{
    public class CO2ReaderMonthly
    {
        public CO2TimeSeriesMonthly TimeSeries { get; private init; }   

        public CO2ReaderMonthly()
        {
            this.TimeSeries = new();
        }
    }
}
