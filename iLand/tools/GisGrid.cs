using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace iLand.tools
{
    /** @class GisGrid
      @ingroup tools
      GisGrid encapsulates a simple grid of values based on GIS data.
      GisGrid can load input files in ESRI text file format (loadFromFile()) and transforms
      coordinates to the current reference in iLand.

      */
    internal class GisGrid
    {
        // global transformation record:
        private static readonly SCoordTrans GISCoordTrans;

        private PointF origin;
        private double[] mData;

        // access
        public int DataSize { get; private set; }   ///< number of data items (rows*cols)
        public int Rows { get; private set; } ///< number of rows
        public int Cols { get; private set; } ///< number of columns
        public double CellSize { get; private set; } ///< size of a cell (meters)
        public double MinValue { get; private set; } ///< minimum data value
        public double MaxValue { get; private set; } ///< maximum data value
        public int NoDataValue { get; private set; } ///< no data value of the grid

        /// get grid value at local coordinates (X/Y); returs NODATAValue if out of range
        /// @p X and @p Y are local coordinates.
        public double GetValue(PointF p) { return GetValue(p.X, p.Y); }
        ///< coordinates of the lower left corner of the grid
        public PointF Origin 
        {
            get { return origin; }
        }

        // setup of global GIS transformation
        // not a good place to put that code here.... please relocate!
        public static void SetupGISTransformation(double offsetx, double offsety, double offsetz, double angle_degree)
        {
            GISCoordTrans.SetupTransformation(offsetx, offsety, offsetz, angle_degree);
        }

        public static void WorldToModel(Vector3D From, Vector3D To)
        {
            double x = From.X - GISCoordTrans.OffsetX;
            double y = From.Y - GISCoordTrans.OffsetY;
            To.X = x * GISCoordTrans.CosRotate - y * GISCoordTrans.SinRotate;
            To.Y = x * GISCoordTrans.SinRotate + y * GISCoordTrans.CosRotate;
            To.Z = From.Z - GISCoordTrans.OffsetZ;
            //To.setY(-To.y()); // spiegeln
        }

        public static void ModelToWorld(Vector3D From, Vector3D To)
        {
            double x = From.X;
            double y = From.Y; // spiegeln
            To.X = x * GISCoordTrans.CosRotateReverse - y * GISCoordTrans.SinRotateReverse + GISCoordTrans.OffsetX;
            To.Y = x * GISCoordTrans.SinRotateReverse + y * GISCoordTrans.CosRotateReverse + GISCoordTrans.OffsetY;
            To.Z = From.Z + GISCoordTrans.OffsetZ;
        }

        static GisGrid()
        {
            GisGrid.GISCoordTrans = new SCoordTrans();
        }

        public GisGrid()
        {
            mData = null;
            Rows = 0;
            Cols = 0;
            CellSize = 1; // default value (for line mode)
        }

        public bool LoadFromFile(string fileName)
        {
            MinValue = 1000000000;
            MaxValue = -1000000000;

            // loads from a ESRI-Grid [RasterToFile] File.
            string[] lines = File.ReadAllLines(fileName);

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

                string key = line.Substring(0, line.IndexOf(' ')).ToLowerInvariant();
                if (key.Length > 0 && (Char.IsNumber(key[0]) || key[0] == '-'))
                {
                    header = false;
                }
                else
                {
                    double value = Double.Parse(line.Substring(line.IndexOf(' ')));
                    if (key == "ncols")
                    {
                        Cols = (int)value;
                    }
                    else if (key == "nrows")
                    {
                        Rows = (int)value;
                    }
                    else if (key == "xllcorner")
                    {
                        this.origin.X = (float)value;
                    }
                    else if (key == "yllcorner")
                    {
                        this.origin.Y = (float)value;
                    }
                    else if (key == "cellsize")
                    {
                        CellSize = value;
                    }
                    else if (key == "nodata_value")
                    {
                        NoDataValue = (int)value;
                    }
                    else
                    {
                        throw new FileLoadException(String.Format("GISGrid: invalid key {0}.", key));
                    }
                    row++;
                }
            } while (header) ;

            // create data
            DataSize = Rows * Cols;
            mData = new double[DataSize];

            // loop thru datalines
            for (;  row < lines.Length; ++row)
            {
                string[] values = lines[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int col = 0; col < Cols; col++)
                {
                    double value = Double.Parse(values[col]);
                    if (value != NoDataValue)
                    {
                        MinValue = Math.Min(MinValue, value);
                        MaxValue = Math.Max(MaxValue, value);
                    }
                    mData[row * Cols + col] = value;
                }
            }
            return true;
        }

        public List<double> DistinctValues()
        {
            if (mData == null)
            {
                return new List<double>();
            }
            Dictionary<double, double> temp_map = new Dictionary<double, double>();
            for (int i = 0; i < DataSize; i++)
            {
                temp_map.Add(mData[i], 1.0);
            }
            temp_map.Remove(NoDataValue);
            return temp_map.Keys.ToList();
        }

        public static PointF ModelToWorld(PointF model_coordinates)
        {
            Vector3D to = new Vector3D();
            ModelToWorld(new Vector3D(model_coordinates.X, model_coordinates.Y, 0.0), to);
            return new PointF((float)to.X, (float)to.Y);
        }

        public static PointF WorldToModel(PointF world_coordinates)
        {
            Vector3D to = new Vector3D();
            WorldToModel(new Vector3D(world_coordinates.X, world_coordinates.Y, 0.0), to);
            return new PointF((float)to.X, (float)to.Y);
        }

        /*
        public void GetDistinctValues(TStringList *ResultList, double x_m, double y_m)
        {
           // alle "distinct" values in einem rechteck (picus-koordinaten)
           // herauslesen. geht nur mit integers.
            double stepsize=CellSize/2; //  default stepsize, die haelfte der Cellsize, damit sollten alle pixel ueberstrichen werden.
            double x=0, y=0;
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
        public double GetValue(int indexx, int indexy)
        {
            if (indexx >= 0 && indexx < Cols && indexy >= 0 && indexy < Rows)
            {
                return mData[indexy * Cols + indexx];
            }
            return -1.0;  // out of scope
        }

        /// get value of grid at index positions
        public double GetValue(int Index)
        {
            if (Index >= 0 && Index < DataSize)
            {
                return mData[Index];
            }
            return -1.0;  // out of scope
        }

        public double GetValue(double X, double Y)
        {
            Vector3D model = new Vector3D(X, Y, 0.0);
            Vector3D world = new Vector3D();
            ModelToWorld(model, world);

            world.X -= Origin.X;
            world.Y -= Origin.Y;

            // get value out of grid.
            // double rx = Origin.x + X * xAxis.x + Y * yAxis.x;
            // double ry = Origin.y + X * xAxis.y + Y * yAxis.y;
            if (world.X < 0.0 || world.Y < 0.0)
            {
                return -1.0;
            }
            int ix = (int)(world.X / CellSize);
            int iy = (int)(world.Y / CellSize);
            if (ix >= 0 && ix < Cols && iy >= 0 && iy < Rows)
            {
                double value = mData[iy * Cols + ix];
                if (value != NoDataValue)
                {
                    return value;
                }
            }
            return -10.0; // the ultimate NODATA- or ErrorValue
        }

        public Vector3D GetCoordinate(int indexx, int indexy)
        {
            Vector3D world = new Vector3D((indexx + 0.5) * CellSize + Origin.X,
                                          (indexy + 0.5) * CellSize + Origin.Y,
                                          0.0);
            Vector3D model = new Vector3D();
            WorldToModel(world, model);
            return model;
        }

        public RectangleF GetCellExtent(int indexX, int indexY)
        {
            Vector3D world = new Vector3D(indexX * CellSize + Origin.X,
                                        indexY * CellSize + Origin.Y,
                                        0.0);
            Vector3D model = new Vector3D();
            WorldToModel(world, model);
            RectangleF rect = new RectangleF((float)model.X, // left
                        (float)model.Y, // top
                        (float)CellSize, // width
                        (float)CellSize); // height
            return rect;
        }

        public Vector3D GetCoordinate(int Index)
        {
            if (Index < 0 || Index >= DataSize)
            {
                throw new ArgumentOutOfRangeException(nameof(Index), "gisgrid:coord: invalid index.");
            }
            int ix = Index % Cols;
            int iy = Index / Cols;
            return GetCoordinate(ix, iy);
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
        public bool GetBoundingBox(int LookFor, RectangleF Result, double x_m, double y_m)
        {
             // alle "distinct" values in einem rechteck (picus-koordinaten)
             // herauslesen. geht nur mit integers.
              double stepsize=CellSize/2; //  default stepsize, die haelfte der Cellsize, damit sollten alle pixel ueberstrichen werden.
              double x=0, y=0;
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

        public void Clip(RectangleF box)
        {
            // auf das angegebene Rechteck zuschneiden, alle
            // werte draussen auf -1 setzen.
            for (int ix = 0; ix < Cols; ix++)
            {
                for (int iy = 0; iy < Rows; iy++)
                {
                    Vector3D akoord = GetCoordinate(iy * Cols + ix);
                    if (!box.Contains((float)akoord.X, (float)akoord.Y))
                    {
                        mData[iy * Cols + ix] = -1.0;
                    }
                }
            }
        }

        /*
        public void ExportToTable(AnsiString OutFileName)
        {
            TStringList *Result = new TStringList();
            AnsiString Line;
            int ix,iy;
            double Value;
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
