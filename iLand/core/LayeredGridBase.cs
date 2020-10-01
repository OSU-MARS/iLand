using System.Collections.Generic;
using System.Drawing;

namespace iLand.Core
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
        public abstract List<LayerElement> Names();
        
        /// get layer index by name of the layer. returns -1 if layer is not available.
        public virtual int IndexOf(string layer_name)
        {
            for (int i = 0; i < Names().Count; ++i)
            {
                if (Names()[i].Name == layer_name)
                {
                    return i;
                }
            }
            return -1;
        }

        public virtual List<string> LayerNames()
        {
            List<string> l = new List<string>();
            for (int i = 0; i < Names().Count; ++i)
            {
                l.Add(Names()[i].Name);
            }
            return l;
        }
    }
}
