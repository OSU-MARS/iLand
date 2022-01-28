using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace iLand.World
{
    /** @class GisGrid
      GisGrid encapsulates a simple grid of values based on GIS data.
      GisGrid can load input files in ESRI text file format (loadFromFile()) and transforms
      coordinates to the current reference in iLand.
      */
    public class GisGrid
    {
        // global transformation record:
        private readonly CoordinateTransform transform;

        private PointF gisOrigin;
        private float[]? data;

        // access
        public int DataSize { get; private set; }   // number of data items (rows*cols)
        public int Rows { get; private set; } // number of rows
        public int Columns { get; private set; } // number of columns
        public float CellSize { get; private set; } // size of a cell (meters)
        public float MinValue { get; private set; } // minimum data value
        public float MaxValue { get; private set; } // maximum data value
        public int NoDataValue { get; private set; } // no data value of the grid

        public GisGrid()
        {
            this.data = null;
            this.transform = new CoordinateTransform();

            this.CellSize = 1; // default value (for line mode)
            this.Columns = 0;
            this.Rows = 0;
        }

        /// get grid value at local coordinates (X/Y); returs NODATAValue if out of range
        /// @p X and @p Y are local coordinates.
        public float GetValue(PointF position) { return this.GetValue(position.X, position.Y); }

        /// get value of grid at index positions
        public float GetValue(int indexX, int indexY)
        {
            if (indexX >= 0 && indexX < Columns && indexY >= 0 && indexY < Rows)
            {
                return this.data![indexY * Columns + indexX];
            }
            return -1.0F;  // out of scope
        }

        public float GetValue(float modelX, float modelY)
        {
            Vector3D modelCoordinate = new(modelX, modelY, 0.0F);
            Vector3D gisCoordinate = this.ModelToGis(modelCoordinate);

            gisCoordinate.X -= this.GisOrigin.X;
            gisCoordinate.Y -= this.GisOrigin.Y;

            // get value out of grid.
            // float rx = Origin.x + X * xAxis.x + Y * yAxis.x;
            // float ry = Origin.y + X * xAxis.y + Y * yAxis.y;
            if (gisCoordinate.X < 0.0 || gisCoordinate.Y < 0.0)
            {
                return -1.0F;
            }
            int indexX = (int)(gisCoordinate.X / this.CellSize);
            int indexY = (int)(gisCoordinate.Y / this.CellSize);
            if (indexX >= 0 && indexX < Columns && indexY >= 0 && indexY < Rows)
            {
                float value = this.data![indexY * this.Columns + indexX];
                if (value != this.NoDataValue)
                {
                    return value;
                }
            }
            return -10.0F; // the ultimate NODATA- or ErrorValue
        }

        // coordinates of the lower left corner of the grid
        public PointF GisOrigin { get { return this.gisOrigin; } }

        public PointF GisToModel(PointF gisCoordinate)
        {
            Vector3D modelCoordinate = this.GisToModel(new Vector3D(gisCoordinate.X, gisCoordinate.Y, 0.0F));
            return new PointF(modelCoordinate.X, modelCoordinate.Y);
        }

        private Vector3D GisToModel(Vector3D gisCoordinate)
        {
            float x = gisCoordinate.X - this.transform.OffsetX;
            float y = gisCoordinate.Y - this.transform.OffsetY;
            Vector3D modelCoordinate = new(x * this.transform.CosRotate - y * this.transform.SinRotate,
                                           x * this.transform.SinRotate + y * this.transform.CosRotate,
                                           gisCoordinate.Z - this.transform.OffsetZ);
            return modelCoordinate;
        }

        public PointF ModelToGis(PointF modelCoordinate)
        {
            Vector3D gisCoordinate = this.ModelToGis(new Vector3D(modelCoordinate.X, modelCoordinate.Y, 0.0F));
            return new PointF(gisCoordinate.X, gisCoordinate.Y);
        }

        private Vector3D ModelToGis(Vector3D modelCoordinate)
        {
            float x = modelCoordinate.X;
            float y = modelCoordinate.Y; // spiegeln
            Vector3D gisCoordinate = new(x * this.transform.CosRotateReverse - y * transform.SinRotateReverse + this.transform.OffsetX,
                                         x * this.transform.SinRotateReverse + y * transform.CosRotateReverse + this.transform.OffsetY,
                                         modelCoordinate.Z + transform.OffsetZ);
            return gisCoordinate;
        }

        public bool LoadFromFile(string? filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // loads from a ESRI-Grid [RasterToFile] File.
            string[] lines = File.ReadAllLines(filePath);

            this.MinValue = Single.MaxValue;
            this.MaxValue = Single.MinValue;

            // processing of header-data
            bool header = true;
            int row = 0;
            for (; row < lines.Length; ++row)
            {
                string line = String.Join(' ', lines[row].Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string key = line[..line.IndexOf(' ')].ToLowerInvariant();
                if (key.Length > 0 && (Char.IsNumber(key[0]) || key[0] == '-'))
                {
                    header = false;
                }
                else
                {
                    string valueAsString = line[line.IndexOf(' ')..];
                    if (key == "ncols")
                    {
                        this.Columns = Int32.Parse(valueAsString);
                    }
                    else if (key == "nrows")
                    {
                        this.Rows = Int32.Parse(valueAsString);
                    }
                    else if (key == "xllcorner")
                    {
                        this.gisOrigin.X = Single.Parse(valueAsString);
                    }
                    else if (key == "yllcorner")
                    {
                        this.gisOrigin.Y = Single.Parse(valueAsString);
                    }
                    else if (key == "cellsize")
                    {
                        this.CellSize = Single.Parse(valueAsString);
                    }
                    else if (key == "nodata_value")
                    {
                        this.NoDataValue = Int32.Parse(valueAsString);
                    }
                    else
                    {
                        throw new FileLoadException(String.Format("GISGrid: invalid key {0}.", key));
                    }
                    row++;
                }
            } while (header) ;

            // create data
            this.DataSize = this.Rows * this.Columns;
            this.data = new float[DataSize];

            // loop thru datalines
            for (;  row < lines.Length; ++row)
            {
                string[] values = lines[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int col = 0; col < Columns; col++)
                {
                    float value = Single.Parse(values[col]);
                    if (value != NoDataValue)
                    {
                        this.MinValue = MathF.Min(this.MinValue, value);
                        this.MaxValue = MathF.Max(this.MaxValue, value);
                    }
                    data[row * Columns + col] = value;
                }
            }
            return true;
        }

        // setup of global GIS transformation
        public void SetupTransformation(float offsetX, float offsetY, float offsetZ, float angleInDegrees)
        {
            this.transform.Setup(offsetX, offsetY, offsetZ, angleInDegrees);
        }

        //public List<float> GetDistinctValues()
        //{
        //    if (data == null)
        //    {
        //        return new List<float>();
        //    }
        //    Dictionary<float, float> temp_map = new Dictionary<float, float>();
        //    for (int index = 0; index < this.DataSize; ++index)
        //    {
        //        temp_map.Add(data[index], 1.0F);
        //    }
        //    temp_map.Remove(NoDataValue);
        //    return temp_map.Keys.ToList();
        //}

        /*
        public void GetDistinctValues(TStringList *ResultList, float x_m, float y_m)
        {
           // alle "distinct" values in einem rechteck (picus-koordinaten)
           // herauslesen. geht nur mit integers.
            float stepsize=CellSize/2; //  default stepsize, die haelfte der Cellsize, damit sollten alle pixel ueberstrichen werden.
            float x=0, y=0;
            int v;
            TList *List=new TList;
            while (x<=x_m) {
               y=0;
               while (y<=y_m) {
                  v=value(x,y);
                  if (List->IndexOf((void*)v)==-1)
                     List->Add((void*)v);
                  y+=stepsize;
               }
               x+=stepsize;
            }
            ResultList->Clear();
            for (int i=0;i<List->Count;i++)
               ResultList->Add(AnsiString((int)List->Items[i]));
            delete List;

        }*/

        /// get value of grid at index positions
        //public float GetValue(int index)
        //{
        //    if (index >= 0 && index < DataSize)
        //    {
        //        return this.data![index];
        //    }
        //    return -1.0F;  // out of scope
        //}

        //public RectangleF GetCellExtent(int indexX, int indexY)
        //{
        //    Vector3D gisCoordinate = new Vector3D(indexX * this.CellSize + this.Origin.X,
        //                                          indexY * this.CellSize + this.Origin.Y,
        //                                          0.0F);
        //    Vector3D model = this.GisToModel(gisCoordinate);
        //    RectangleF rect = new RectangleF(model.X, // left
        //                                     model.Y, // top
        //                                     this.CellSize, // width
        //                                     this.CellSize); // height
        //    return rect;
        //}

        private Vector3D GetCoordinate(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= this.DataSize)
            {
                throw new ArgumentOutOfRangeException(nameof(cellIndex), "Invalid cell index.");
            }
            int indexX = cellIndex % this.Columns;
            int indexY = cellIndex / this.Columns;
            return this.GetCoordinate(indexX, indexY);
        }

        public Vector3D GetCoordinate(int indexX, int indexY)
        {
            Vector3D gisCoordinate = new((indexX + 0.5F) * this.CellSize + this.GisOrigin.X,
                                         (indexY + 0.5F) * this.CellSize + this.GisOrigin.Y,
                                         0.0F);
            return this.GisToModel(gisCoordinate);
        }


        /*
        public void CountOccurence(int intID, int & Count, int & left, int & upper, int &right, int &lower, RectangleF *OuterBox)
        {
                // zaehlt, wie of intID im Grid vorkommt,
                // ausserdem das rectangle, in dem es vorkommt.
                // rectangle ist durch indices [z.b. 0..NRows-1] und nicht laengen definiert!
                int ix,iy;
                Count=0;
                left=100000;
                right=-1;
                upper=100000;
                lower=-1;
                QVector3D akoord;
                for (ix=0;ix<mNCols;ix++)
                   for (iy=0;iy<mNRows;iy++)
                       if (mData[iy*mNCols + ix]==intID) {
                            // gefunden!
                            // innerhalb der Box?
                            if (OuterBox) {
                               akoord = koord(iy*mNCols + ix);
                               if (akoord.x<OuterBox->x1 || akoord.x>OuterBox->x2 || akoord.y<OuterBox->y1 || akoord.y>OuterBox->y2)
                                   continue; // nicht zaehlen, falls punkt ausserhalb rect.
                            }
                            Count++;
                            left=ix<left?ix:left;
                            upper=iy<upper?iy:upper;
                            right=ix>right?ix:right;
                            lower=iy>lower?iy:lower;
                      }
                if (Count==0)
                   left=upper=right=lower=-1; // if not found.

        }
        */
        /*
        public QVector3D GetNthOccurence(int ID, int N, int left, int upper, int right, int lower)
        {
                // aus dem (index-)rectangle left/upper..right/lower
                // das N-te vorkommen von "ID" heraussuchen.
                // das ergebnis sind die koordinaten des mittelpunktes der grid-zelle.
                int ix,iy;
                int Counter=0;
                for (ix=left;ix<=right;ix++)
                   for (iy=upper;iy<=lower;iy++)
                       if (mData[iy*mNCols+ix]==ID) {
                           Counter++;
                           if (Counter==N) {  // N-tes vorkommen gefunden!!!
                               // Picus-Koordinaten zurueckgeben.
                               return koord(iy*mNCols + ix);
                           }
                       }
                // n-tes vorkommen nicht gefunden!!
                throw Exception("GISGrid:getNthOccurence. ID="+AnsiString(ID)+", N="+AnsiString(N)+" nicht gefunden!");
        }
        */
        /*
        public bool GetBoundingBox(int LookFor, RectangleF Result, float x_m, float y_m)
        {
             // alle "distinct" values in einem rechteck (picus-koordinaten)
             // herauslesen. geht nur mit integers.
              float stepsize=CellSize/2; //  default stepsize, die haelfte der Cellsize, damit sollten alle pixel ueberstrichen werden.
              float x=0, y=0;
              int v;
              Result.x1 = 1000000; Result.x2 = -10000000;
              Result.y1 = 1000000; Result.y2 = -10000000;
              bool Found = false;
              while (x<=x_m) {
                 y=0;
                 while (y<=y_m) {
                    v=value(x,y);
                    if (v==LookFor) {
                       Result.x1 = Min(Result.x1, x);
                       Result.x2 = Max(Result.x2, x);
                       Result.y1 = Min(Result.y1, y);
                       Result.y2 = Max(Result.y2, y);
                       Found = true;
                    }
                    y+=stepsize;
                 }
                 x+=stepsize;
              }
              return Found;
        }
        */

        //public void Clip(RectangleF clipExtent)
        //{
        //    // auf das angegebene Rechteck zuschneiden, alle
        //    // werte draussen auf -1 setzen.
        //    for (int indexX = 0; indexX < this.Columns; ++indexX)
        //    {
        //        for (int indexY = 0; indexY < this.Rows; ++indexY)
        //        {
        //            Vector3D akoord = this.GetCoordinate(indexY * Columns + indexX);
        //            if (clipExtent.Contains(akoord.X, akoord.Y) == false)
        //            {
        //                this.data![indexY * Columns + indexX] = -1.0F;
        //            }
        //        }
        //    }
        //}

        /*
        public void ExportToTable(AnsiString OutFileName)
        {
            TStringList *Result = new TStringList();
            AnsiString Line;
            int ix,iy;
            float Value;
            for (ix=0;ix<mNCols;ix++)
                for (iy=0;iy<mNRows;iy++) {
                   Value = mData[iy*mNCols + ix];
                   if (Value != mNODATAValue) {
                     Line.sprintf("%d;%d;%f", ix, iy, Value);
                     Result->Add(Line);
                   }
                }
            Result->SaveToFile(OutFileName);
            delete Result;
        }
        */
    }
}
