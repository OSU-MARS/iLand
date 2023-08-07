using Apache.Arrow;
using System.Linq;

namespace iLand.Input.Tree
{
    internal class LightStampArrowBatch : ArrowBatch
    {
        public UInt8Array CenterIndex { get; private init; }
        public FloatArray CrownRadiusInM { get; private init; }
        public FloatArray DbhInCm { get; private init; }
        public UInt8Array HeightDiameterRatio { get; private init; }
        public UInt8Array DataSize { get; private init; }
        public FloatArray Value { get; private init; }
        public UInt8Array X { get; private init; }
        public UInt8Array Y { get; private init; }

        public LightStampArrowBatch(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;

            this.CenterIndex = ArrowBatch.GetArray<UInt8Array>("centerIndex", schema, fields);
            this.CrownRadiusInM = ArrowBatch.GetArray<FloatArray>("crownRadius", schema, fields);
            this.DbhInCm = ArrowBatch.GetArray<FloatArray>("dbh", schema, fields);
            this.HeightDiameterRatio = ArrowBatch.GetArray<UInt8Array>("heightDiameterRatio", schema, fields);
            this.DataSize = ArrowBatch.GetArray<UInt8Array>("size", schema, fields);
            this.X = ArrowBatch.GetArray<UInt8Array>("x", schema, fields);
            this.Y = ArrowBatch.GetArray<UInt8Array>("y", schema, fields);
            this.Value = ArrowBatch.GetArray<FloatArray>("value", schema, fields);
        }
    }
}
