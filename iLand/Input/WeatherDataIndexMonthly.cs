using Apache.Arrow;

namespace iLand.Input
{
    internal class WeatherDataIndexMonthly
    {
        public int ID { get; private init; }
        public int Precipitation01 { get; private init; }
        public int Precipitation02 { get; private init; }
        public int Precipitation03 { get; private init; }
        public int Precipitation04 { get; private init; }
        public int Precipitation05 { get; private init; }
        public int Precipitation06 { get; private init; }
        public int Precipitation07 { get; private init; }
        public int Precipitation08 { get; private init; }
        public int Precipitation09 { get; private init; }
        public int Precipitation10 { get; private init; }
        public int Precipitation11 { get; private init; }
        public int Precipitation12 { get; private init; }
        public int Snow01 { get; private init; }
        public int Snow02 { get; private init; }
        public int Snow03 { get; private init; }
        public int Snow04 { get; private init; }
        public int Snow05 { get; private init; }
        public int Snow06 { get; private init; }
        public int Snow07 { get; private init; }
        public int Snow08 { get; private init; }
        public int Snow09 { get; private init; }
        public int Snow10 { get; private init; }
        public int Snow11 { get; private init; }
        public int Snow12 { get; private init; }
        public int SolarRadiation01 { get; private init; }
        public int SolarRadiation02 { get; private init; }
        public int SolarRadiation03 { get; private init; }
        public int SolarRadiation04 { get; private init; }
        public int SolarRadiation05 { get; private init; }
        public int SolarRadiation06 { get; private init; }
        public int SolarRadiation07 { get; private init; }
        public int SolarRadiation08 { get; private init; }
        public int SolarRadiation09 { get; private init; }
        public int SolarRadiation10 { get; private init; }
        public int SolarRadiation11 { get; private init; }
        public int SolarRadiation12 { get; private init; }
        public int TemperatureMax01 { get; private init; }
        public int TemperatureMax02 { get; private init; }
        public int TemperatureMax03 { get; private init; }
        public int TemperatureMax04 { get; private init; }
        public int TemperatureMax05 { get; private init; }
        public int TemperatureMax06 { get; private init; }
        public int TemperatureMax07 { get; private init; }
        public int TemperatureMax08 { get; private init; }
        public int TemperatureMax09 { get; private init; }
        public int TemperatureMax10 { get; private init; }
        public int TemperatureMax11 { get; private init; }
        public int TemperatureMax12 { get; private init; }
        public int TemperatureMean01 { get; private init; }
        public int TemperatureMean02 { get; private init; }
        public int TemperatureMean03 { get; private init; }
        public int TemperatureMean04 { get; private init; }
        public int TemperatureMean05 { get; private init; }
        public int TemperatureMean06 { get; private init; }
        public int TemperatureMean07 { get; private init; }
        public int TemperatureMean08 { get; private init; }
        public int TemperatureMean09 { get; private init; }
        public int TemperatureMean10 { get; private init; }
        public int TemperatureMean11 { get; private init; }
        public int TemperatureMean12 { get; private init; }
        public int TemperatureMin01 { get; private init; }
        public int TemperatureMin02 { get; private init; }
        public int TemperatureMin03 { get; private init; }
        public int TemperatureMin04 { get; private init; }
        public int TemperatureMin05 { get; private init; }
        public int TemperatureMin06 { get; private init; }
        public int TemperatureMin07 { get; private init; }
        public int TemperatureMin08 { get; private init; }
        public int TemperatureMin09 { get; private init; }
        public int TemperatureMin10 { get; private init; }
        public int TemperatureMin11 { get; private init; }
        public int TemperatureMin12 { get; private init; }
        public int Year { get; private init; }

        public WeatherDataIndexMonthly(CsvFile weatherFile)
        {
            this.ID = weatherFile.GetColumnIndex("ID2");
            this.Precipitation01 = weatherFile.GetColumnIndex("PPT01");
            this.Precipitation02 = weatherFile.GetColumnIndex("PPT02");
            this.Precipitation03 = weatherFile.GetColumnIndex("PPT03");
            this.Precipitation04 = weatherFile.GetColumnIndex("PPT04");
            this.Precipitation05 = weatherFile.GetColumnIndex("PPT05");
            this.Precipitation06 = weatherFile.GetColumnIndex("PPT06");
            this.Precipitation07 = weatherFile.GetColumnIndex("PPT07");
            this.Precipitation08 = weatherFile.GetColumnIndex("PPT08");
            this.Precipitation09 = weatherFile.GetColumnIndex("PPT09");
            this.Precipitation10 = weatherFile.GetColumnIndex("PPT10");
            this.Precipitation11 = weatherFile.GetColumnIndex("PPT11");
            this.Precipitation12 = weatherFile.GetColumnIndex("PPT12");
            this.Snow01 = weatherFile.GetColumnIndex("PAS01");
            this.Snow02 = weatherFile.GetColumnIndex("PAS02");
            this.Snow03 = weatherFile.GetColumnIndex("PAS03");
            this.Snow04 = weatherFile.GetColumnIndex("PAS04");
            this.Snow05 = weatherFile.GetColumnIndex("PAS05");
            this.Snow06 = weatherFile.GetColumnIndex("PAS06");
            this.Snow07 = weatherFile.GetColumnIndex("PAS07");
            this.Snow08 = weatherFile.GetColumnIndex("PAS08");
            this.Snow09 = weatherFile.GetColumnIndex("PAS09");
            this.Snow10 = weatherFile.GetColumnIndex("PAS10");
            this.Snow11 = weatherFile.GetColumnIndex("PAS11");
            this.Snow12 = weatherFile.GetColumnIndex("PAS12");
            this.SolarRadiation01 = weatherFile.GetColumnIndex("Rad01");
            this.SolarRadiation02 = weatherFile.GetColumnIndex("Rad02");
            this.SolarRadiation03 = weatherFile.GetColumnIndex("Rad03");
            this.SolarRadiation04 = weatherFile.GetColumnIndex("Rad04");
            this.SolarRadiation05 = weatherFile.GetColumnIndex("Rad05");
            this.SolarRadiation06 = weatherFile.GetColumnIndex("Rad06");
            this.SolarRadiation07 = weatherFile.GetColumnIndex("Rad07");
            this.SolarRadiation08 = weatherFile.GetColumnIndex("Rad08");
            this.SolarRadiation09 = weatherFile.GetColumnIndex("Rad09");
            this.SolarRadiation10 = weatherFile.GetColumnIndex("Rad10");
            this.SolarRadiation11 = weatherFile.GetColumnIndex("Rad11");
            this.SolarRadiation12 = weatherFile.GetColumnIndex("Rad12");
            this.TemperatureMax01 = weatherFile.GetColumnIndex("Tmax01");
            this.TemperatureMax02 = weatherFile.GetColumnIndex("Tmax02");
            this.TemperatureMax03 = weatherFile.GetColumnIndex("Tmax03");
            this.TemperatureMax04 = weatherFile.GetColumnIndex("Tmax04");
            this.TemperatureMax05 = weatherFile.GetColumnIndex("Tmax05");
            this.TemperatureMax06 = weatherFile.GetColumnIndex("Tmax06");
            this.TemperatureMax07 = weatherFile.GetColumnIndex("Tmax07");
            this.TemperatureMax08 = weatherFile.GetColumnIndex("Tmax08");
            this.TemperatureMax09 = weatherFile.GetColumnIndex("Tmax09");
            this.TemperatureMax10 = weatherFile.GetColumnIndex("Tmax10");
            this.TemperatureMax11 = weatherFile.GetColumnIndex("Tmax11");
            this.TemperatureMax12 = weatherFile.GetColumnIndex("Tmax12");
            this.TemperatureMean01 = weatherFile.GetColumnIndex("Tave01");
            this.TemperatureMean02 = weatherFile.GetColumnIndex("Tave02");
            this.TemperatureMean03 = weatherFile.GetColumnIndex("Tave03");
            this.TemperatureMean04 = weatherFile.GetColumnIndex("Tave04");
            this.TemperatureMean05 = weatherFile.GetColumnIndex("Tave05");
            this.TemperatureMean06 = weatherFile.GetColumnIndex("Tave06");
            this.TemperatureMean07 = weatherFile.GetColumnIndex("Tave07");
            this.TemperatureMean08 = weatherFile.GetColumnIndex("Tave08");
            this.TemperatureMean09 = weatherFile.GetColumnIndex("Tave09");
            this.TemperatureMean10 = weatherFile.GetColumnIndex("Tave10");
            this.TemperatureMean11 = weatherFile.GetColumnIndex("Tave11");
            this.TemperatureMean12 = weatherFile.GetColumnIndex("Tave12");
            this.TemperatureMin01 = weatherFile.GetColumnIndex("Tmin01");
            this.TemperatureMin02 = weatherFile.GetColumnIndex("Tmin02");
            this.TemperatureMin03 = weatherFile.GetColumnIndex("Tmin03");
            this.TemperatureMin04 = weatherFile.GetColumnIndex("Tmin04");
            this.TemperatureMin05 = weatherFile.GetColumnIndex("Tmin05");
            this.TemperatureMin06 = weatherFile.GetColumnIndex("Tmin06");
            this.TemperatureMin07 = weatherFile.GetColumnIndex("Tmin07");
            this.TemperatureMin08 = weatherFile.GetColumnIndex("Tmin08");
            this.TemperatureMin09 = weatherFile.GetColumnIndex("Tmin09");
            this.TemperatureMin10 = weatherFile.GetColumnIndex("Tmin10");
            this.TemperatureMin11 = weatherFile.GetColumnIndex("Tmin11");
            this.TemperatureMin12 = weatherFile.GetColumnIndex("Tmin12");
            this.Year = weatherFile.GetColumnIndex("Year");
        }

        public WeatherDataIndexMonthly(RecordBatch apacheBatch)
        {
            Schema schema = apacheBatch.Schema;

            this.ID = schema.GetFieldIndex("ID2");
            this.Precipitation01 = schema.GetFieldIndex("PPT01");
            this.Precipitation02 = schema.GetFieldIndex("PPT02");
            this.Precipitation03 = schema.GetFieldIndex("PPT03");
            this.Precipitation04 = schema.GetFieldIndex("PPT04");
            this.Precipitation05 = schema.GetFieldIndex("PPT05");
            this.Precipitation06 = schema.GetFieldIndex("PPT06");
            this.Precipitation07 = schema.GetFieldIndex("PPT07");
            this.Precipitation08 = schema.GetFieldIndex("PPT08");
            this.Precipitation09 = schema.GetFieldIndex("PPT09");
            this.Precipitation10 = schema.GetFieldIndex("PPT10");
            this.Precipitation11 = schema.GetFieldIndex("PPT11");
            this.Precipitation12 = schema.GetFieldIndex("PPT12");
            this.Snow01 = schema.GetFieldIndex("PAS01");
            this.Snow02 = schema.GetFieldIndex("PAS02");
            this.Snow03 = schema.GetFieldIndex("PAS03");
            this.Snow04 = schema.GetFieldIndex("PAS04");
            this.Snow05 = schema.GetFieldIndex("PAS05");
            this.Snow06 = schema.GetFieldIndex("PAS06");
            this.Snow07 = schema.GetFieldIndex("PAS07");
            this.Snow08 = schema.GetFieldIndex("PAS08");
            this.Snow09 = schema.GetFieldIndex("PAS09");
            this.Snow10 = schema.GetFieldIndex("PAS10");
            this.Snow11 = schema.GetFieldIndex("PAS11");
            this.Snow12 = schema.GetFieldIndex("PAS12");
            this.SolarRadiation01 = schema.GetFieldIndex("Rad01");
            this.SolarRadiation02 = schema.GetFieldIndex("Rad02");
            this.SolarRadiation03 = schema.GetFieldIndex("Rad03");
            this.SolarRadiation04 = schema.GetFieldIndex("Rad04");
            this.SolarRadiation05 = schema.GetFieldIndex("Rad05");
            this.SolarRadiation06 = schema.GetFieldIndex("Rad06");
            this.SolarRadiation07 = schema.GetFieldIndex("Rad07");
            this.SolarRadiation08 = schema.GetFieldIndex("Rad08");
            this.SolarRadiation09 = schema.GetFieldIndex("Rad09");
            this.SolarRadiation10 = schema.GetFieldIndex("Rad10");
            this.SolarRadiation11 = schema.GetFieldIndex("Rad11");
            this.SolarRadiation12 = schema.GetFieldIndex("Rad12");
            this.TemperatureMax01 = schema.GetFieldIndex("Tmax01");
            this.TemperatureMax02 = schema.GetFieldIndex("Tmax02");
            this.TemperatureMax03 = schema.GetFieldIndex("Tmax03");
            this.TemperatureMax04 = schema.GetFieldIndex("Tmax04");
            this.TemperatureMax05 = schema.GetFieldIndex("Tmax05");
            this.TemperatureMax06 = schema.GetFieldIndex("Tmax06");
            this.TemperatureMax07 = schema.GetFieldIndex("Tmax07");
            this.TemperatureMax08 = schema.GetFieldIndex("Tmax08");
            this.TemperatureMax09 = schema.GetFieldIndex("Tmax09");
            this.TemperatureMax10 = schema.GetFieldIndex("Tmax10");
            this.TemperatureMax11 = schema.GetFieldIndex("Tmax11");
            this.TemperatureMax12 = schema.GetFieldIndex("Tmax12");
            this.TemperatureMean01 = schema.GetFieldIndex("Tave01");
            this.TemperatureMean02 = schema.GetFieldIndex("Tave02");
            this.TemperatureMean03 = schema.GetFieldIndex("Tave03");
            this.TemperatureMean04 = schema.GetFieldIndex("Tave04");
            this.TemperatureMean05 = schema.GetFieldIndex("Tave05");
            this.TemperatureMean06 = schema.GetFieldIndex("Tave06");
            this.TemperatureMean07 = schema.GetFieldIndex("Tave07");
            this.TemperatureMean08 = schema.GetFieldIndex("Tave08");
            this.TemperatureMean09 = schema.GetFieldIndex("Tave09");
            this.TemperatureMean10 = schema.GetFieldIndex("Tave10");
            this.TemperatureMean11 = schema.GetFieldIndex("Tave11");
            this.TemperatureMean12 = schema.GetFieldIndex("Tave12");
            this.TemperatureMin01 = schema.GetFieldIndex("Tmin01");
            this.TemperatureMin02 = schema.GetFieldIndex("Tmin02");
            this.TemperatureMin03 = schema.GetFieldIndex("Tmin03");
            this.TemperatureMin04 = schema.GetFieldIndex("Tmin04");
            this.TemperatureMin05 = schema.GetFieldIndex("Tmin05");
            this.TemperatureMin06 = schema.GetFieldIndex("Tmin06");
            this.TemperatureMin07 = schema.GetFieldIndex("Tmin07");
            this.TemperatureMin08 = schema.GetFieldIndex("Tmin08");
            this.TemperatureMin09 = schema.GetFieldIndex("Tmin09");
            this.TemperatureMin10 = schema.GetFieldIndex("Tmin10");
            this.TemperatureMin11 = schema.GetFieldIndex("Tmin11");
            this.TemperatureMin12 = schema.GetFieldIndex("Tmin12");
            this.Year = schema.GetFieldIndex("Year");
        }
    }
}
