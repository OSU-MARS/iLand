using System.Collections.Generic;
using System.Drawing;

namespace iLand.World
{
    internal abstract class LayeredGridBase
    {
        // access to properties
        public abstract int SizeX();
        public abstract int SizeY();
        public abstract RectangleF PhysicalSize();
        public abstract RectangleF CellRect(Point p);

        public virtual bool OnClick(PointF world_coord) 
        {
            return false; /*false: not handled*/ 
        }

        // available variables
        /// list of stored layers
        public abstract List<LayerElement> GetLayers();

        public List<string> GetLayerNames()
        {
            List<LayerElement> layers = this.GetLayers();
            List<string> layerNames = new List<string>(layers.Count);
            for (int index = 0; index < GetLayers().Count; ++index)
            {
                layerNames.Add(layers[index].Name);
            }
            return layerNames;
        }

        /// get layer index by name of the layer. returns -1 if layer is not available.
        public int IndexOf(string layerName)
        {
            List<LayerElement> layers = this.GetLayers();
            for (int index = 0; index < layers.Count; ++index)
            {
                if (layers[index].Name == layerName)
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
