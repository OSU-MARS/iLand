using iLand.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace iLand.Tree
{
    public class TreeListMultispecies : TreeList
    {
        public Point[] LightCellIndexXY { get; private set; }
        public WorldFloraID[] SpeciesID { get; private set; }

        public TreeListMultispecies(int capacity)
            : base(capacity)
        {
            this.Allocate(capacity);
        }

        [MemberNotNull(nameof(TreeListMultispecies.LightCellIndexXY), nameof(TreeListMultispecies.SpeciesID))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.LightCellIndexXY = Array.Empty<Point>();
                this.SpeciesID = Array.Empty<WorldFloraID>();
            }
            else
            {
                this.LightCellIndexXY = new Point[capacity];
                this.SpeciesID = new WorldFloraID[capacity];
            }
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.SpeciesID = this.SpeciesID.Resize(newSize);
        }
    }
}
