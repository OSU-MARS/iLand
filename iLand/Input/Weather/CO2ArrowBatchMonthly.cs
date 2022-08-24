using Apache.Arrow;
using System.Linq;

namespace iLand.Input.Weather
{
    public class CO2ArrowBatchMonthly : ArrowBatch
    {
        public Int16Array Year { get; private init; }
        public UInt8Array Month { get; private init; }
        public FloatArray CO2 { get; private init; }

        public CO2ArrowBatchMonthly(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;

            this.Year = ArrowBatch.GetArray<Int16Array>("year", schema, fields);
            this.Month = ArrowBatch.GetArray<UInt8Array>("month", schema, fields);
            this.CO2 = ArrowBatch.GetArray<FloatArray>("co2", schema, fields);
        }
    }
}
