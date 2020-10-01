using System;
using System.Collections.Generic;

namespace iLand.Core
{
    internal class GrassCoverLayers : LayeredGrid<Int16>
    {
        private List<LayerElement> mNames;

        public void SetGrid(Grid<Int16> grid) { Grid = grid; }

        public override List<LayerElement> Names()
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
