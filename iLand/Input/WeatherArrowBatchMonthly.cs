using Apache.Arrow;
using System.Linq;

namespace iLand.Input
{
    public class WeatherArrowBatchMonthly : ArrowBatch
    {
        public StringArray ID { get; private init; }
        public FloatArray Precipitation01 { get; private init; }
        public FloatArray Precipitation02 { get; private init; }
        public FloatArray Precipitation03 { get; private init; }
        public FloatArray Precipitation04 { get; private init; }
        public FloatArray Precipitation05 { get; private init; }
        public FloatArray Precipitation06 { get; private init; }
        public FloatArray Precipitation07 { get; private init; }
        public FloatArray Precipitation08 { get; private init; }
        public FloatArray Precipitation09 { get; private init; }
        public FloatArray Precipitation10 { get; private init; }
        public FloatArray Precipitation11 { get; private init; }
        public FloatArray Precipitation12 { get; private init; }
        public FloatArray RelativeHumidityMean01 { get; private init; }
        public FloatArray RelativeHumidityMean02 { get; private init; }
        public FloatArray RelativeHumidityMean03 { get; private init; }
        public FloatArray RelativeHumidityMean04 { get; private init; }
        public FloatArray RelativeHumidityMean05 { get; private init; }
        public FloatArray RelativeHumidityMean06 { get; private init; }
        public FloatArray RelativeHumidityMean07 { get; private init; }
        public FloatArray RelativeHumidityMean08 { get; private init; }
        public FloatArray RelativeHumidityMean09 { get; private init; }
        public FloatArray RelativeHumidityMean10 { get; private init; }
        public FloatArray RelativeHumidityMean11 { get; private init; }
        public FloatArray RelativeHumidityMean12 { get; private init; }
        public FloatArray Snow01 { get; private init; }
        public FloatArray Snow02 { get; private init; }
        public FloatArray Snow03 { get; private init; }
        public FloatArray Snow04 { get; private init; }
        public FloatArray Snow05 { get; private init; }
        public FloatArray Snow06 { get; private init; }
        public FloatArray Snow07 { get; private init; }
        public FloatArray Snow08 { get; private init; }
        public FloatArray Snow09 { get; private init; }
        public FloatArray Snow10 { get; private init; }
        public FloatArray Snow11 { get; private init; }
        public FloatArray Snow12 { get; private init; }
        public FloatArray SolarRadiation01 { get; private init; }
        public FloatArray SolarRadiation02 { get; private init; }
        public FloatArray SolarRadiation03 { get; private init; }
        public FloatArray SolarRadiation04 { get; private init; }
        public FloatArray SolarRadiation05 { get; private init; }
        public FloatArray SolarRadiation06 { get; private init; }
        public FloatArray SolarRadiation07 { get; private init; }
        public FloatArray SolarRadiation08 { get; private init; }
        public FloatArray SolarRadiation09 { get; private init; }
        public FloatArray SolarRadiation10 { get; private init; }
        public FloatArray SolarRadiation11 { get; private init; }
        public FloatArray SolarRadiation12 { get; private init; }
        public FloatArray TemperatureMax01 { get; private init; }
        public FloatArray TemperatureMax02 { get; private init; }
        public FloatArray TemperatureMax03 { get; private init; }
        public FloatArray TemperatureMax04 { get; private init; }
        public FloatArray TemperatureMax05 { get; private init; }
        public FloatArray TemperatureMax06 { get; private init; }
        public FloatArray TemperatureMax07 { get; private init; }
        public FloatArray TemperatureMax08 { get; private init; }
        public FloatArray TemperatureMax09 { get; private init; }
        public FloatArray TemperatureMax10 { get; private init; }
        public FloatArray TemperatureMax11 { get; private init; }
        public FloatArray TemperatureMax12 { get; private init; }
        public FloatArray TemperatureMean01 { get; private init; }
        public FloatArray TemperatureMean02 { get; private init; }
        public FloatArray TemperatureMean03 { get; private init; }
        public FloatArray TemperatureMean04 { get; private init; }
        public FloatArray TemperatureMean05 { get; private init; }
        public FloatArray TemperatureMean06 { get; private init; }
        public FloatArray TemperatureMean07 { get; private init; }
        public FloatArray TemperatureMean08 { get; private init; }
        public FloatArray TemperatureMean09 { get; private init; }
        public FloatArray TemperatureMean10 { get; private init; }
        public FloatArray TemperatureMean11 { get; private init; }
        public FloatArray TemperatureMean12 { get; private init; }
        public FloatArray TemperatureMin01 { get; private init; }
        public FloatArray TemperatureMin02 { get; private init; }
        public FloatArray TemperatureMin03 { get; private init; }
        public FloatArray TemperatureMin04 { get; private init; }
        public FloatArray TemperatureMin05 { get; private init; }
        public FloatArray TemperatureMin06 { get; private init; }
        public FloatArray TemperatureMin07 { get; private init; }
        public FloatArray TemperatureMin08 { get; private init; }
        public FloatArray TemperatureMin09 { get; private init; }
        public FloatArray TemperatureMin10 { get; private init; }
        public FloatArray TemperatureMin11 { get; private init; }
        public FloatArray TemperatureMin12 { get; private init; }
        public Int32Array Year { get; private init; }

        public WeatherArrowBatchMonthly(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;

            this.ID = ArrowBatch.GetArray<StringArray>("ID2", schema, fields);
            this.Precipitation01 = ArrowBatch.GetArray<FloatArray>("PPT01", schema, fields);
            this.Precipitation02 = ArrowBatch.GetArray<FloatArray>("PPT02", schema, fields);
            this.Precipitation03 = ArrowBatch.GetArray<FloatArray>("PPT03", schema, fields);
            this.Precipitation04 = ArrowBatch.GetArray<FloatArray>("PPT04", schema, fields);
            this.Precipitation05 = ArrowBatch.GetArray<FloatArray>("PPT05", schema, fields);
            this.Precipitation06 = ArrowBatch.GetArray<FloatArray>("PPT06", schema, fields);
            this.Precipitation07 = ArrowBatch.GetArray<FloatArray>("PPT07", schema, fields);
            this.Precipitation08 = ArrowBatch.GetArray<FloatArray>("PPT08", schema, fields);
            this.Precipitation09 = ArrowBatch.GetArray<FloatArray>("PPT09", schema, fields);
            this.Precipitation10 = ArrowBatch.GetArray<FloatArray>("PPT10", schema, fields);
            this.Precipitation11 = ArrowBatch.GetArray<FloatArray>("PPT11", schema, fields);
            this.Precipitation12 = ArrowBatch.GetArray<FloatArray>("PPT12", schema, fields);
            this.RelativeHumidityMean01 = ArrowBatch.GetArray<FloatArray>("RH01", schema, fields);
            this.RelativeHumidityMean02 = ArrowBatch.GetArray<FloatArray>("RH02", schema, fields);
            this.RelativeHumidityMean03 = ArrowBatch.GetArray<FloatArray>("RH03", schema, fields);
            this.RelativeHumidityMean04 = ArrowBatch.GetArray<FloatArray>("RH04", schema, fields);
            this.RelativeHumidityMean05 = ArrowBatch.GetArray<FloatArray>("RH05", schema, fields);
            this.RelativeHumidityMean06 = ArrowBatch.GetArray<FloatArray>("RH06", schema, fields);
            this.RelativeHumidityMean07 = ArrowBatch.GetArray<FloatArray>("RH07", schema, fields);
            this.RelativeHumidityMean08 = ArrowBatch.GetArray<FloatArray>("RH08", schema, fields);
            this.RelativeHumidityMean09 = ArrowBatch.GetArray<FloatArray>("RH09", schema, fields);
            this.RelativeHumidityMean10 = ArrowBatch.GetArray<FloatArray>("RH10", schema, fields);
            this.RelativeHumidityMean11 = ArrowBatch.GetArray<FloatArray>("RH11", schema, fields);
            this.RelativeHumidityMean12 = ArrowBatch.GetArray<FloatArray>("RH12", schema, fields);
            this.Snow01 = ArrowBatch.GetArray<FloatArray>("PAS01", schema, fields);
            this.Snow02 = ArrowBatch.GetArray<FloatArray>("PAS02", schema, fields);
            this.Snow03 = ArrowBatch.GetArray<FloatArray>("PAS03", schema, fields);
            this.Snow04 = ArrowBatch.GetArray<FloatArray>("PAS04", schema, fields);
            this.Snow05 = ArrowBatch.GetArray<FloatArray>("PAS05", schema, fields);
            this.Snow06 = ArrowBatch.GetArray<FloatArray>("PAS06", schema, fields);
            this.Snow07 = ArrowBatch.GetArray<FloatArray>("PAS07", schema, fields);
            this.Snow08 = ArrowBatch.GetArray<FloatArray>("PAS08", schema, fields);
            this.Snow09 = ArrowBatch.GetArray<FloatArray>("PAS09", schema, fields);
            this.Snow10 = ArrowBatch.GetArray<FloatArray>("PAS10", schema, fields);
            this.Snow11 = ArrowBatch.GetArray<FloatArray>("PAS11", schema, fields);
            this.Snow12 = ArrowBatch.GetArray<FloatArray>("PAS12", schema, fields);
            this.SolarRadiation01 = ArrowBatch.GetArray<FloatArray>("Rad01", schema, fields);
            this.SolarRadiation02 = ArrowBatch.GetArray<FloatArray>("Rad02", schema, fields);
            this.SolarRadiation03 = ArrowBatch.GetArray<FloatArray>("Rad03", schema, fields);
            this.SolarRadiation04 = ArrowBatch.GetArray<FloatArray>("Rad04", schema, fields);
            this.SolarRadiation05 = ArrowBatch.GetArray<FloatArray>("Rad05", schema, fields);
            this.SolarRadiation06 = ArrowBatch.GetArray<FloatArray>("Rad06", schema, fields);
            this.SolarRadiation07 = ArrowBatch.GetArray<FloatArray>("Rad07", schema, fields);
            this.SolarRadiation08 = ArrowBatch.GetArray<FloatArray>("Rad08", schema, fields);
            this.SolarRadiation09 = ArrowBatch.GetArray<FloatArray>("Rad09", schema, fields);
            this.SolarRadiation10 = ArrowBatch.GetArray<FloatArray>("Rad10", schema, fields);
            this.SolarRadiation11 = ArrowBatch.GetArray<FloatArray>("Rad11", schema, fields);
            this.SolarRadiation12 = ArrowBatch.GetArray<FloatArray>("Rad12", schema, fields);
            this.TemperatureMax01 = ArrowBatch.GetArray<FloatArray>("Tmax01", schema, fields);
            this.TemperatureMax02 = ArrowBatch.GetArray<FloatArray>("Tmax02", schema, fields);
            this.TemperatureMax03 = ArrowBatch.GetArray<FloatArray>("Tmax03", schema, fields);
            this.TemperatureMax04 = ArrowBatch.GetArray<FloatArray>("Tmax04", schema, fields);
            this.TemperatureMax05 = ArrowBatch.GetArray<FloatArray>("Tmax05", schema, fields);
            this.TemperatureMax06 = ArrowBatch.GetArray<FloatArray>("Tmax06", schema, fields);
            this.TemperatureMax07 = ArrowBatch.GetArray<FloatArray>("Tmax07", schema, fields);
            this.TemperatureMax08 = ArrowBatch.GetArray<FloatArray>("Tmax08", schema, fields);
            this.TemperatureMax09 = ArrowBatch.GetArray<FloatArray>("Tmax09", schema, fields);
            this.TemperatureMax10 = ArrowBatch.GetArray<FloatArray>("Tmax10", schema, fields);
            this.TemperatureMax11 = ArrowBatch.GetArray<FloatArray>("Tmax11", schema, fields);
            this.TemperatureMax12 = ArrowBatch.GetArray<FloatArray>("Tmax12", schema, fields);
            this.TemperatureMean01 = ArrowBatch.GetArray<FloatArray>("Tave01", schema, fields);
            this.TemperatureMean02 = ArrowBatch.GetArray<FloatArray>("Tave02", schema, fields);
            this.TemperatureMean03 = ArrowBatch.GetArray<FloatArray>("Tave03", schema, fields);
            this.TemperatureMean04 = ArrowBatch.GetArray<FloatArray>("Tave04", schema, fields);
            this.TemperatureMean05 = ArrowBatch.GetArray<FloatArray>("Tave05", schema, fields);
            this.TemperatureMean06 = ArrowBatch.GetArray<FloatArray>("Tave06", schema, fields);
            this.TemperatureMean07 = ArrowBatch.GetArray<FloatArray>("Tave07", schema, fields);
            this.TemperatureMean08 = ArrowBatch.GetArray<FloatArray>("Tave08", schema, fields);
            this.TemperatureMean09 = ArrowBatch.GetArray<FloatArray>("Tave09", schema, fields);
            this.TemperatureMean10 = ArrowBatch.GetArray<FloatArray>("Tave10", schema, fields);
            this.TemperatureMean11 = ArrowBatch.GetArray<FloatArray>("Tave11", schema, fields);
            this.TemperatureMean12 = ArrowBatch.GetArray<FloatArray>("Tave12", schema, fields);
            this.TemperatureMin01 = ArrowBatch.GetArray<FloatArray>("Tmin01", schema, fields);
            this.TemperatureMin02 = ArrowBatch.GetArray<FloatArray>("Tmin02", schema, fields);
            this.TemperatureMin03 = ArrowBatch.GetArray<FloatArray>("Tmin03", schema, fields);
            this.TemperatureMin04 = ArrowBatch.GetArray<FloatArray>("Tmin04", schema, fields);
            this.TemperatureMin05 = ArrowBatch.GetArray<FloatArray>("Tmin05", schema, fields);
            this.TemperatureMin06 = ArrowBatch.GetArray<FloatArray>("Tmin06", schema, fields);
            this.TemperatureMin07 = ArrowBatch.GetArray<FloatArray>("Tmin07", schema, fields);
            this.TemperatureMin08 = ArrowBatch.GetArray<FloatArray>("Tmin08", schema, fields);
            this.TemperatureMin09 = ArrowBatch.GetArray<FloatArray>("Tmin09", schema, fields);
            this.TemperatureMin10 = ArrowBatch.GetArray<FloatArray>("Tmin10", schema, fields);
            this.TemperatureMin11 = ArrowBatch.GetArray<FloatArray>("Tmin11", schema, fields);
            this.TemperatureMin12 = ArrowBatch.GetArray<FloatArray>("Tmin12", schema, fields);
            this.Year = ArrowBatch.GetArray<Int32Array>("Year", schema, fields);
        }
    }
}
