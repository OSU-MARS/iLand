// C++/tools/gisgrid.h
using OSGeo.GDAL;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace iLand.Tool
{
    internal class GisGridTransform
    {
        // https://gdal.org/tutorials/geotransforms_tut.html
        public double CellHeight { get; private set; } // north-south resolution, negative if north up
        public double CellWidth { get; private set; } // east-west resolution
        public double ColumnRotation { get; private set; } // zero if north up 
        public double OriginX { get; private set; }
        public double OriginY { get; private set; }
        public double RowRotation { get; private set; } // zero if north up

        public GisGridTransform()
        {
            this.CellHeight = Double.NaN;
            this.CellWidth = Double.NaN;
            this.ColumnRotation = Double.NaN;
            this.OriginX = Double.NaN;
            this.OriginY = Double.NaN;
            this.RowRotation = Double.NaN;
        }

        public void Copy(Dataset rasterDataset)
        {
            double[] padfTransform = new double[6]; // can't stackalloc as GetGeoTransform() doesn't support ReadOnlySpan<double>
            rasterDataset.GetGeoTransform(padfTransform);

            this.OriginX = padfTransform[0];
            this.CellWidth = padfTransform[1];
            this.RowRotation = padfTransform[2];
            this.OriginY = padfTransform[3];
            this.ColumnRotation = padfTransform[4];
            this.CellHeight = padfTransform[5];

            if ((Double.IsFinite(this.OriginX) == false) ||
                (Double.IsFinite(this.OriginY) == false) ||
                (this.CellWidth <= 0.0) || (Double.IsFinite(this.CellWidth) == false) ||
                (this.CellHeight == 0.0) || (Double.IsFinite(this.CellHeight) == false) ||
                (Double.IsFinite(this.ColumnRotation) == false) ||
                (Double.IsFinite(this.RowRotation) == false))
            {
                throw new ArgumentOutOfRangeException(nameof(rasterDataset), "Raster transform contains non-finite numbers, a negative cell width, or a zero cell height.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double xIndexFractional, double yIndexFractional) ToFractionalIndices(double x, double y)
        {
            if (this.ColumnRotation != 0.0)
            {
                double yIndexFractional = (y - this.OriginY - this.ColumnRotation / this.CellWidth * (x - this.OriginX)) / (this.CellHeight - this.ColumnRotation * this.RowRotation / this.CellWidth);
                double xIndexFractional = (x - this.OriginX - yIndexFractional * this.RowRotation) / this.CellWidth;
                return (xIndexFractional, yIndexFractional);
            }

            Debug.Assert(this.RowRotation == 0.0);
            return ((x - this.OriginX) / this.CellWidth, (y - this.OriginY) / this.CellHeight);
        }
    }
}
