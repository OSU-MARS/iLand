using Apache.Arrow;
using System.Linq;

namespace iLand.Input.Tree
{
    internal class IndividualTreeArrowBatch : ArrowBatch
    {
        public Int32Array? AgeInYears { get; private init; }
        public FloatArray DbhInCm { get; private init; }
        public FloatArray HeightInM { get; private init; }
        public StringArray Species { get; private init; }
        public Int32Array? StandID { get; private init; }
        public Int32Array? Tag { get; private init; }
        public FloatArray GisX { get; private init; }
        public FloatArray GisY { get; private init; }

        public IndividualTreeArrowBatch(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;

            this.AgeInYears = ArrowBatch.MaybeGetArray<Int32Array>("age", schema, fields);
            this.DbhInCm = ArrowBatch.GetArray<FloatArray>("dbh", schema, fields);
            this.HeightInM = ArrowBatch.GetArray<FloatArray>("height", schema, fields);
            this.Species = ArrowBatch.GetArray<StringArray>("species", schema, fields);
            this.StandID = ArrowBatch.MaybeGetArray<Int32Array>("standID", schema, fields);
            this.Tag = ArrowBatch.MaybeGetArray<Int32Array>("id", schema, fields);
            this.GisX = ArrowBatch.GetArray<FloatArray>("x", schema, fields);
            this.GisY = ArrowBatch.GetArray<FloatArray>("y", schema, fields);
        }
    }
}
