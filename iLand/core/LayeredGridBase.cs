using System.Collections.Generic;
using System.Drawing;

namespace iLand.core
{
    internal abstract class LayeredGridBase
    {
        // access to properties
        public abstract int sizeX();
        public abstract int sizeY();
        public abstract RectangleF metricRect();
        public abstract RectangleF cellRect(Point p);

        public virtual bool onClick(PointF world_coord) 
        {
            return false; /*false: not handled*/ 
        }

        // available variables
        /// list of stored layers
        public abstract List<LayerElement> names();
        
        /// get layer index by name of the layer. returns -1 if layer is not available.
        public virtual int indexOf(string layer_name)
        {
            for (int i = 0; i < names().Count; ++i)
            {
                if (names()[i].name == layer_name)
                {
                    return i;
                }
            }
            return -1;
        }

        public virtual List<string> layerNames()
        {
            List<string> l = new List<string>();
            for (int i = 0; i < names().Count; ++i)
            {
                l.Add(names()[i].name);
            }
            return l;
        }

        // unused in C++
        // statistics
        /// retrieve min and max of variable 'index'
        //public abstract void range(ref double rMin, ref double rMax, int index);

        // unused in C++
        // data access functions
        //public abstract double value(float x, float y, int index);
        //public abstract double value(PointF world_coord, int index);
        //public abstract double value(int ix, int iy, int index);
        //public abstract double value(int grid_index, int index);

        // for classified values
        public virtual string labelvalue(int value, int index)
        {
            return "-";
        }
    }
}
