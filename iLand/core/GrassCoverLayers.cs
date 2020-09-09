using System;
using System.Collections.Generic;

namespace iLand.core
{
    internal class GrassCoverLayers : LayeredGrid<Int16>
    {
        private List<LayerElement> mNames;
        private GrassCover mGrassCover;

        public void setGrid(Grid<Int16> grid, GrassCover gc) { mGrid = grid; mGrassCover = gc; }

        // unused in C++
        //public override double value(Int16 data, int index)
        //{
        //    if (!mGrassCover.enabled())
        //    {
        //        return 0.0;
        //    }
        //    switch (index)
        //    {
        //        case 0: return mGrassCover.effect(data); //effect
        //        case 1: return mGrassCover.cover(data); // cover
        //        default: throw new NotSupportedException(String.Format("invalid variable index for a GrassCoverLayers: {0}", index));
        //    }
        //}

        public override List<LayerElement> names()
        {
            if (mNames == null)
            {
                mNames = new List<LayerElement>()
                {
                    new LayerElement("effect", "prohibiting effect on regeneration [0..1]", GridViewType.GridViewGreens),
                    new LayerElement("cover", "current grass cover on pixels [0..1 for continuous, or #(years+2) for pixel mode]", GridViewType.GridViewGreens)
                };
            }
            return mNames;
        }
    }
}
