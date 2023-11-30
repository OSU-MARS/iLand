using iLand.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace iLand.Tree
{
    public class TreeListForAddition : TreeList
    {
        public Point[] LightCellIndexXY { get; private set; }
        public WorldFloraID[] SpeciesID { get; private set; }

        public TreeListForAddition(int capacity)
            : base(capacity)
        {
            this.Allocate(capacity);
        }

        [MemberNotNull(nameof(TreeListForAddition.LightCellIndexXY), nameof(TreeListForAddition.SpeciesID))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.LightCellIndexXY = [];
                this.SpeciesID = [];
            }
            else
            {
                this.LightCellIndexXY = new Point[capacity];
                this.SpeciesID = new WorldFloraID[capacity];
            }
        }

        public TreeSpanForAddition AsSpan()
        {
            return new TreeSpanForAddition()
            {
                AgeInYears = this.AgeInYears.Slice(0, this.Count),
                DbhInCm = this.DbhInCm.Slice(0, this.Count),
                HeightInM = this.HeightInM.Slice(0, this.Count),
                LightCellIndexXY = this.LightCellIndexXY.Slice(0, this.Count),
                SpeciesID = this.SpeciesID.Slice(0, this.Count),
                StandID = this.StandID.Slice(0, this.Count),
                TreeID = this.TreeID.Slice(0, this.Count)
            };
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.SpeciesID = this.SpeciesID.Resize(newSize);
        }
    }
}
