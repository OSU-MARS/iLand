using System;
using System.Drawing;
using System.IO;

namespace iLand.Input
{
    public class EsriAsciiRasterReader
    {
        private readonly float[] data;
        private PointF lowerLeftCorner;
        private PointF upperRightCorner;

        //public int DataSize { get; private set; }   // number of data items (rows*cols)
        public int Rows { get; private set; } // number of rows
        public int Columns { get; private set; } // number of columns
        public float CellSize { get; private set; } // size of a cell in input layer's CRS
        //public float MinValue { get; private set; } // minimum data value
        //public float MaxValue { get; private set; } // maximum data value
        public float NoDataValue { get; private set; } // no data value of the grid

        public EsriAsciiRasterReader(string esriAsciiRasterPath)
        {
            // this.data initialized below
            this.lowerLeftCorner = new PointF(Constant.NoDataSingle, Constant.NoDataSingle);
            this.upperRightCorner = new PointF(Constant.NoDataSingle, Constant.NoDataSingle);

            this.CellSize = 1; // default value (for line mode)
            this.Columns = -1;
            //this.MinValue = Constant.NoDataSingle;
            //this.MaxValue = Constant.NoDataSingle;
            this.NoDataValue = Constant.NoDataSingle;
            this.Rows = -1;

            // read header
            using FileStream stream = new(esriAsciiRasterPath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using StreamReader reader = new(stream);

            int rowIndex = 0;
            string? line = reader.ReadLine();
            for (; line != null; line = reader.ReadLine())
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                line = string.Join(' ', line.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

                string key = line[..line.IndexOf(' ')].ToLowerInvariant();
                if (key.Length > 0 && (char.IsNumber(key[0]) || key[0] == '-'))
                {
                    break;
                }
                else
                {
                    string valueAsString = line[line.IndexOf(' ')..];
                    switch (key)
                    {
                        case "ncols":
                            Columns = Int32.Parse(valueAsString);
                            break;
                        case "nrows":
                            Rows = Int32.Parse(valueAsString);
                            break;
                        case "xllcorner":
                            this.lowerLeftCorner.X = Single.Parse(valueAsString);
                            break;
                        case "yllcorner":
                            this.lowerLeftCorner.Y = Single.Parse(valueAsString);
                            break;
                        case "cellsize":
                            this.CellSize = Single.Parse(valueAsString);
                            break;
                        case "nodata_value":
                            this.NoDataValue = Int32.Parse(valueAsString);
                            break;
                        default:
                            throw new NotSupportedException("Unknown header field '" + key + "'.");
                    }
                    ++rowIndex;
                }
            }

            // create data
            if ((this.Rows < 1) || (this.Columns < 1) || Single.IsNaN(this.lowerLeftCorner.X) || Single.IsNaN(this.lowerLeftCorner.Y))
            {
                throw new ArgumentException("Raster header is missing one or more of nrows, ncols, xllcorner, or yllcorner.", nameof(esriAsciiRasterPath));
            }
            this.data = new float[this.Rows * this.Columns];
            this.upperRightCorner.X = this.lowerLeftCorner.X + this.CellSize * this.Columns;
            this.upperRightCorner.Y = this.lowerLeftCorner.Y + this.CellSize * this.Rows;

            // loop thru datalines
            for (; line != null; line = reader.ReadLine())
            {
                string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int columnIndex = 0; columnIndex < values.Length; ++columnIndex)
                {
                    float value = Single.Parse(values[columnIndex]);
                    this.data[rowIndex * Columns + columnIndex] = value;

                    //if (value != this.NoDataValue)
                    //{
                    //    this.MinValue = MathF.Min(this.MinValue, value);
                    //    this.MaxValue = MathF.Max(this.MaxValue, value);
                    //}
                }
            }
        }

        public RectangleF GetBoundingBox()
        {
            return new RectangleF(this.lowerLeftCorner.X, this.lowerLeftCorner.Y, this.upperRightCorner.X - this.lowerLeftCorner.X, this.upperRightCorner.Y - this.lowerLeftCorner.Y);
        }

        public float GetValue(PointF modelCoordinate)
        {
            if ((modelCoordinate.X < this.lowerLeftCorner.X) || (modelCoordinate.X > this.upperRightCorner.X) ||
                (modelCoordinate.Y < this.lowerLeftCorner.Y) || (modelCoordinate.Y > this.upperRightCorner.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(modelCoordinate));
            }

            float offsetX = modelCoordinate.X - lowerLeftCorner.X;
            float offsetY = modelCoordinate.Y - lowerLeftCorner.Y;

            int indexX = (int)(offsetX / this.CellSize);
            int indexY = (int)(offsetY / this.CellSize);
            return this.data[indexY * this.Columns + indexX];
        }
    }
}
