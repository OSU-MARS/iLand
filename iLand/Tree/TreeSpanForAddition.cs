using iLand.Extensions;
using iLand.Input.Tree;
using System;
using System.Drawing;

namespace iLand.Tree
{
    public readonly ref struct TreeSpanForAddition
    {
        public Span<UInt16> AgeInYears { get; init; }
        public Span<float> DbhInCm { get; init; }
        public Span<float> HeightInM { get; init; }
        public Span<Point> LightCellIndexXY { get; init; }
        public Span<WorldFloraID> SpeciesID { get; init; }
        public Span<UInt32> StandID { get; init; }
        public Span<UInt32> TreeID { get; init; }

        public TreeSpanForAddition(IndividualTreeReader individualTreeReader, Point[] treePositions, int readerStartIndex, int treesToAdd)
        {
            this.AgeInYears = individualTreeReader.AgeInYears.Slice(readerStartIndex, treesToAdd);
            this.DbhInCm = individualTreeReader.DbhInCm.Slice(readerStartIndex, treesToAdd);
            this.HeightInM = individualTreeReader.HeightInM.Slice(readerStartIndex, treesToAdd);
            this.LightCellIndexXY = treePositions.Slice(0, treesToAdd);
            this.SpeciesID = individualTreeReader.SpeciesID.Slice(readerStartIndex, treesToAdd);
            this.StandID = individualTreeReader.StandID.Slice(readerStartIndex, treesToAdd);
            this.TreeID = individualTreeReader.TreeID.Slice(readerStartIndex, treesToAdd);
        }

        public int Length
        {
            get { return this.AgeInYears.Length; }
        }
    }
}
