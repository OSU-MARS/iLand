using Apache.Arrow;
using System;
using System.Linq;

namespace iLand.Input.Tree
{
    internal class IndividualTreeArrowBatch : ArrowBatch
    {
        public UInt16Array? AgeInYears { get; private init; }
        public FloatArray DbhInCm { get; private init; }
        public FloatArray HeightInM { get; private init; }
        public UInt16Array? FiaCode { get; private init; }
        public UInt32Array? WorldFloraID { get; private init; }
        public UInt32Array? StandID { get; private init; }
        public UInt32Array? TreeID { get; private init; }
        public FloatArray GisX { get; private init; }
        public FloatArray GisY { get; private init; }

        public IndividualTreeArrowBatch(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;
            
            this.AgeInYears = ArrowBatch.MaybeGetArray<UInt16Array>("age", schema, fields);
            this.DbhInCm = ArrowBatch.GetArray<FloatArray>("dbh", schema, fields);
            this.FiaCode = ArrowBatch.MaybeGetArray<UInt16Array>("fiaCode", schema, fields);
            this.HeightInM = ArrowBatch.GetArray<FloatArray>("height", schema, fields);
            this.WorldFloraID = ArrowBatch.MaybeGetArray<UInt32Array>("wfoID", schema, fields);
            this.StandID = ArrowBatch.MaybeGetArray<UInt32Array>("standID", schema, fields);
            this.TreeID = ArrowBatch.MaybeGetArray<UInt32Array>("treeID", schema, fields);
            this.GisX = ArrowBatch.GetArray<FloatArray>("x", schema, fields);
            this.GisY = ArrowBatch.GetArray<FloatArray>("y", schema, fields);

            int wellKnownFields = 4 + (this.AgeInYears != null ? 1 : 0) + (this.FiaCode != null ? 1 : 0) + (this.WorldFloraID != null ? 1 : 0) + (this.StandID != null ? 1 : 0) + (this.TreeID != null ? 1 : 0);
            if (wellKnownFields != fields.Length)
            {
                throw new NotSupportedException("Individual tree record batch contains unexpected fields. Found " + wellKnownFields + " well known fields (standID, treeID, fiaCode, wfoID, dbh, height, x, y, age) in " + fields.Length + " total.");
            }

            // for now, allow both FIA code and World Flora Online identifiers to be specified and assume the caller indicates them consistently
            if ((this.FiaCode == null) && (this.WorldFloraID == null))
            {
                throw new NotSupportedException("Individual tree record batch does not indicate tree species. Either an fiaCode or wfoID field must be present.");
            }
        }

        public int GetBytesPerRecord()
        {
            int fourByteFieldCount = 4 + (this.StandID != null ? 1 : 0) + (this.TreeID != null ? 1 : 0) + (this.WorldFloraID != null ? 1 : 0); // DBH, height, x, and y plus optional fields
            int twoByteFieldCount = (this.AgeInYears != null ? 1 : 0) + (this.FiaCode != null ? 1 : 0);
            return 4 * fourByteFieldCount + 2 * twoByteFieldCount;
        }
    }
}
