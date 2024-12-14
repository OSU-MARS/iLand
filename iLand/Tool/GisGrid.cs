// C++/tools/{ gisgrid.h, gisgrid.cpp }
using iLand.Extensions;
using iLand.World;
using OSGeo.GDAL;
using OSGeo.OSR;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace iLand.Tool
{
    /// <summary>
    /// A raster from a GIS data file.
    /// </summary>
    /// <remarks>
    /// Does not derive from <see cref="Grid{T}"/> because a GIS raster's origin most likely differs from the iLand project's origin and
    /// GDAL's convention of negative cell heights is opposite iLand's convention of positive cell heights.
    /// </remarks>
    internal class GisGrid<T> where T : struct, INumber<T>
    {
        protected T[] Data { get; private set; }
        protected bool NoDataIsNaN { get; private set; }

        public bool HasNoDataValue { get; protected set; }
        public T NoDataValue { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public GisGridTransform Transform { get; private init; }

        public GisGrid()
        {
            this.Data = [];
            this.HasNoDataValue = false;
            this.NoDataIsNaN = false;
            this.NoDataValue = default;
            this.SizeX = 0;
            this.SizeY = 0;
            this.Transform = new();
        }

        public void LoadFromFile(string rasterPath)
        {
            using Dataset rasterDataset = Gdal.Open(rasterPath, Access.GA_ReadOnly);
            if (rasterDataset.RasterCount != 1)
            {
                throw new NotSupportedException("Raster '" + rasterPath + "' has " + rasterDataset.RasterCount + " layers.");
            }
            SpatialReference crs = rasterDataset.GetSpatialRef();
            double linearUnits = crs.GetLinearUnits();
            if (linearUnits != 1.0)
            {
                throw new NotSupportedException("Raster '" + rasterPath + "''s coordinate system is " + crs.GetName() + ", which is not a projected coordinate system with metric units.");
            }

            this.Transform.Copy(rasterDataset);
            if (this.Transform.CellHeight != this.Transform.CellWidth)
            {
                throw new NotSupportedException("Raster '" + rasterPath + "' has cells which are not square. Its cell width is " + this.Transform.CellWidth + " and its cell height is " + this.Transform.CellHeight + ".");
            }

            this.SizeX = rasterDataset.RasterXSize;
            this.SizeY = rasterDataset.RasterYSize;
            this.Data = new T[this.SizeX * this.SizeY];

            Band gdalBand = rasterDataset.GetRasterBand(1);
            gdalBand.GetNoDataValue(out double gdalNoDataValue, out int hasNoDataValue);

            this.HasNoDataValue = hasNoDataValue != 0;
            if (this.HasNoDataValue)
            {
                this.NoDataValue = T.CreateChecked(gdalNoDataValue);
            }
            else
            {
                this.NoDataValue = default;
            }

            DataType bufferDataType = GisGrid<T>.GetGdalDataType();
            GCHandle dataPin = GCHandle.Alloc(this.Data, GCHandleType.Pinned);
            try
            {
                // read entire band from GDAL's cache at once
                CPLErr gdalErrorCode = gdalBand.ReadRaster(xOff: 0, yOff: 0, xSize: gdalBand.XSize, ySize: gdalBand.YSize, buffer: dataPin.AddrOfPinnedObject(), buf_xSize: gdalBand.XSize, buf_ySize: gdalBand.YSize, buf_type: bufferDataType, pixelSpace: 0, lineSpace: 0);
                GdalException.ThrowIfError(gdalErrorCode, nameof(gdalBand.ReadRaster));
            }
            finally
            {
                dataPin.Free();
            }
        }

        public T this[int xIndex, int yIndex]
        {
            get { return this.Data[this.ToCellIndex(xIndex, yIndex)]; }
            set { this.Data[this.ToCellIndex(xIndex, yIndex)] = value; }
        }

        public (double xCentroid, double yCentroid) GetCellCentroid(int indexX, int indexY)
        {
            double xCentroid = this.Transform.OriginX + this.Transform.CellWidth * (indexX + 0.5);
            double yCentroid = this.Transform.OriginY + this.Transform.CellHeight * (indexY + 0.5);
            return (xCentroid, yCentroid);
        }

        public string GetExtentString()
        {
            double yMin = this.Transform.OriginY;
            double yMax = this.Transform.OriginY;
            double signedHeight = this.SizeY * this.Transform.CellHeight; // positive if cell height > 0, otherwise negative
            if (this.Transform.CellHeight < 0.0)
            {
                yMin += signedHeight;
            }
            else
            {
                yMax += signedHeight;
            }
            return this.Transform.OriginX + ", " + (this.Transform.OriginX + this.SizeX * this.Transform.CellWidth) + ", " + yMin + ", " + yMax;
        }

        private static DataType GetGdalDataType()
        {
            return Type.GetTypeCode(typeof(T)) switch
            {
                TypeCode.Byte => DataType.GDT_Byte,
                TypeCode.Double => DataType.GDT_Float64,
                TypeCode.Int16 => DataType.GDT_Int16,
                TypeCode.Int32 => DataType.GDT_Int32,
                TypeCode.Int64 => DataType.GDT_Int64,
                TypeCode.SByte => DataType.GDT_Int8,
                TypeCode.Single => DataType.GDT_Float32,
                TypeCode.UInt16 => DataType.GDT_UInt16,
                TypeCode.UInt32 => DataType.GDT_UInt32,
                TypeCode.UInt64 => DataType.GDT_UInt64,
                // complex numbers (GDT_CInt16, 32, CFloat32, 64) and GDT_TypeCount not currently supported
                _ => throw new NotSupportedException("Unhandled data type " + Type.GetTypeCode(typeof(T)) + ".")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNoData(T value)
        {
            if (this.HasNoDataValue)
            {
                return this.NoDataIsNaN ? T.IsNaN(value) : this.NoDataValue == value; // have to test with IsNaN() since { float, double }.NaN == { float, double }.NaN = false
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int64 ToCellIndex(Int64 xIndex, Int64 yIndex)
        {
            Debug.Assert((0 <= xIndex) && (xIndex < this.SizeX) && (0 <= yIndex) && (yIndex < this.SizeY));
            return xIndex + yIndex * this.SizeX;
        }

        /// <summary>
        /// Convert a position to a cell index that might or might not be on the grid.
        /// </summary>
        /// <param name="x">x coordinate in grid's CRS.</param>
        /// <param name="y">y coordinate in grid's CRS.</param>
        /// <returns>An (x, y) index tuple whose values will lie on the grid if <paramref name="x"/> and <paramref name="y"/> lie within the grid.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int xIndex, int yIndex) ToGridIndices(double x, double y)
        {
            (double xIndexFractional, double yIndexFractional) = this.Transform.ToFractionalIndices(x, y);
            int xIndex = (int)xIndexFractional;
            int yIndex = (int)yIndexFractional;

            if (xIndexFractional < 0.0)
            {
                --xIndex; // integer truncation truncates towards zero
            }
            else if ((xIndex == this.SizeX) && (x == this.Transform.OriginX + this.Transform.CellWidth * this.SizeX))
            {
                // TODO: support rotated rasters
                xIndex -= 1; // if x lies exactly on grid edge, consider point part of the grid
            }

            if (yIndexFractional < 0.0)
            {
                --yIndex; // integer truncation truncates towards zero
            }
            else if (yIndex == this.SizeY)
            {
                // similarly, if y lies exactly on grid edge consider point part of the grid
                // TODO: support rotated rasters
                if (this.Transform.CellHeight < 0.0)
                {
                    if (y == this.Transform.OriginY + this.Transform.CellHeight * this.SizeX)
                    {
                        yIndex -= 1;
                    }
                }
                else
                {
                    if (y == this.Transform.OriginY)
                    {
                        yIndex -= 1;
                    }
                }
            }

            return (xIndex, yIndex);
        }

        // needs testing with nonzero row and column rotations
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int xIndex, int yIndex) ToInteriorGridIndices(double x, double y)
        {
            (double xIndexFractional, double yIndexFractional) = this.Transform.ToFractionalIndices(x, y);
            int xIndex = (int)xIndexFractional;
            int yIndex = (int)yIndexFractional;

            const double cellFractionTolerance = 0.000010; // 0.0000045 observed in practice
            if (xIndexFractional < 0.0)
            {
                if (xIndexFractional < -cellFractionTolerance)
                {
                    throw new NotSupportedException("Point at (x = " + x + ", y = " + y + " has an x value less than the grid's minimum x extent (" + this.GetExtentString() + " by a distance larger than can be attributed to numerical error.");
                }

                Debug.Assert(xIndex == 0); // cast to integer rounds toward zero
            }
            else if (xIndex >= this.SizeX)
            {
                if ((xIndex > this.SizeX) || (x > this.Transform.OriginX + this.Transform.CellWidth * (this.SizeX + cellFractionTolerance)))
                {
                    throw new NotSupportedException("Point at (x = " + x + ", y = " + y + " has an x value greater than the grid's maximum x extent (" + this.GetExtentString() + " by a distance larger than can be attributed to numerical error.");
                }

                xIndex -= 1; // if x lies exactly on or very close to grid edge, consider point part of the grid
            }
            // xIndex ∈ [ 0, this.XSize -1 ] falls through

            if (yIndexFractional < 0.0)
            {
                if (yIndexFractional < -cellFractionTolerance)
                {
                    throw new NotSupportedException("Point at (x = " + x + ", y = " + y + " lies outside the grid's y extent (" + this.GetExtentString() + " by a distance larger than can be attributed to numerical error.");
                }

                Debug.Assert(yIndex == 0); // cast to integer rounds toward zero
            }
            else if (yIndex >= this.SizeY)
            {
                // similarly, if y lies exactly on or very close grid edge consider point part of the grid
                if (this.Transform.CellHeight < 0.0)
                {
                    // y origin is grid's max y value
                    if ((yIndex > this.SizeY) || (y < this.Transform.OriginY + this.Transform.CellHeight * (this.SizeY + cellFractionTolerance)))
                    {
                        throw new NotSupportedException("Point at (x = " + x + ", y = " + y + " lies outside the grid's y extent (" + this.GetExtentString() + " by a distance larger than can be attributed to numerical error.");
                    }
                }
                else
                {
                    // y origin is grid's minimum y value
                    if ((yIndex > this.SizeY) || (y >= this.Transform.OriginY - cellFractionTolerance * this.Transform.CellHeight))
                    {
                        throw new NotSupportedException("Point at (x = " + x + ", y = " + y + " lies outside the grid's y extent (" + this.GetExtentString() + " by a distance larger than can be attributed to numerical error.");
                    }
                }

                yIndex -= 1;
            }
            // xIndex ∈ [ 0, this.XSize -1 ] falls through

            return (xIndex, yIndex);
        }
    }
}
