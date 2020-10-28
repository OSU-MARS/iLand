using iLand.World;
using System;
using System.Collections.Generic;

namespace iLand.Simulation
{
    internal class GrassCoverLayers : LayeredGrid<Int16>
    {
        private List<LayerElement> layers;

        public void SetGrid(Grid<Int16> grid) { Grid = grid; }

        public override List<LayerElement> GetLayers()
        {
            if (layers == null)
            {
                layers = new List<LayerElement>()
                {
                    new LayerElement("effect", "prohibiting effect on regeneration [0..1]", GridViewType.Greens),
                    new LayerElement("cover", "current grass cover on pixels [0..1 for continuous, or #(years+2) for pixel mode]", GridViewType.Greens)
                };
            }
            return layers;
        }
    }
}
