using System.Collections.Generic;

namespace iLand.Input
{
    internal class WeatherReaderMonthly
    {
        public Dictionary<string, WeatherTimeSeriesMonthly> MonthlyWeatherByID { get; private init; }

        public WeatherReaderMonthly()
        {
            this.MonthlyWeatherByID = new();
        }
    }
}
