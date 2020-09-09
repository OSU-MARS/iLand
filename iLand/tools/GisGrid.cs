using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

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
        private static SCoordTrans GISCoordTrans;

        private int mDataSize;  ///< number of data items (rows*cols)
        private double mCellSize;   // size of cells [m]
        private double max_value;
        private double min_value;
        private PointF mOrigin; // lowerleftcorner
        private PointF xAxis;  // transformed axis (moved, rotated)
        private PointF yAxis;
        private int mNRows;
        private int mNCols;     // count of rows and cols
        private double[] mData;
        private int mNODATAValue;

        // access
        public int dataSize() { return mDataSize; }   ///< number of data items (rows*cols)
        public int rows() { return mNRows; } ///< number of rows
        public int cols() { return mNCols; } ///< number of columns
        public PointF origin() { return mOrigin; } ///< coordinates of the lower left corner of the grid
        public double cellSize() { return mCellSize; } ///< size of a cell (meters)
        public double minValue() { return min_value; } ///< minimum data value
        public double maxValue() { return max_value; } ///< maximum data value
        public int noDataValue() { return mNODATAValue; } ///< no data value of the grid
                                                          /// get grid value at local coordinates (X/Y); returs NODATAValue if out of range
                                                          /// @p X and @p Y are local coordinates.
        public double value(PointF p) { return value(p.X, p.Y); }

        // setup of global GIS transformation
        // not a good place to put that code here.... please relocate!
        public static void setupGISTransformation(double offsetx, double offsety, double offsetz, double angle_degree)
        {
            GISCoordTrans.setupTransformation(offsetx, offsety, offsetz, angle_degree);
        }

        public static void worldToModel(Vector3D From, Vector3D To)
        {
            double x = From.x() - GISCoordTrans.offsetX;
            double y = From.y() - GISCoordTrans.offsetY;
            To.setZ(From.z() - GISCoordTrans.offsetZ);
            To.setX(x * GISCoordTrans.cosRotate - y * GISCoordTrans.sinRotate);
            To.setY(x * GISCoordTrans.sinRotate + y * GISCoordTrans.cosRotate);
            //To.setY(-To.y()); // spiegeln
        }

        public static void modelToWorld(Vector3D From, Vector3D To)
        {
            double x = From.x();
            double y = From.y(); // spiegeln
            To.setX(x * GISCoordTrans.cosRotateReverse - y * GISCoordTrans.sinRotateReverse + GISCoordTrans.offsetX);
            To.setY(x * GISCoordTrans.sinRotateReverse + y * GISCoordTrans.cosRotateReverse + GISCoordTrans.offsetY);
            To.setZ(From.z() + GISCoordTrans.offsetZ);
        }

        public GisGrid()
        {
            mData = null;
            mNRows = 0;
            mNCols = 0;
            mCellSize = 1; // default value (for line mode)
        }

        public bool loadFromFile(string fileName)
        {
            min_value = 1000000000;
            max_value = -1000000000;

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
                        mNCols = (int)value;
                    }
                    else if (key == "nrows")
                    {
                        mNRows = (int)value;
                    }
                    else if (key == "xllcorner")
                    {
                        mOrigin.X = (float)value;
                    }
                    else if (key == "yllcorner")
                    {
                        mOrigin.Y = (float)value;
                    }
                    else if (key == "cellsize")
                    {
                        mCellSize = value;
                    }
                    else if (key == "nodata_value")
                    {
                        mNODATAValue = (int)value;
                    }
                    else
                    {
                        throw new FileLoadException(String.Format("GISGrid: invalid key {0}.", key));
                    }
                    row++;
                }
            } while (header) ;

            // create data
            mDataSize = mNRows * mNCols;
            mData = new double[mDataSize];

            // loop thru datalines
            for (;  row < lines.Length; ++row)
            {
                string[] values = lines[row].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int col = 0; col < mNCols; col++)
                {
                    double value = Double.Parse(values[col]);
                    if (value != mNODATAValue)
                    {
                        min_value = Math.Min(min_value, value);
                        max_value = Math.Max(max_value, value);
                    }
                    mData[row * mNCols + col] = value;
                }
            }
            return true;
        }

        public List<double> distinctValues()
        {
            if (mData == null)
            {
                return new List<double>();
            }
            Dictionary<double, double> temp_map = new Dictionary<double, double>();
            for (int i = 0; i < mDataSize; i++)
            {
                temp_map.Add(mData[i], 1.0);
            }
            temp_map.Remove(mNODATAValue);
            return temp_map.Keys.ToList();
        }

        public static PointF modelToWorld(PointF model_coordinates)
        {
            Vector3D to = new Vector3D();
            modelToWorld(new Vector3D(model_coordinates.X, model_coordinates.Y, 0.0), to);
            return new PointF((float)to.x(), (float)to.y());
        }

        public static PointF worldToModel(PointF world_coordinates)
        {
            Vector3D to = new Vector3D();
            worldToModel(new Vector3D(world_coordinates.X, world_coordinates.Y, 0.0), to);
            return new PointF((float)to.x(), (float)to.y());
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
        public double value(int indexx, int indexy)
        {
            if (indexx >= 0 && indexx < mNCols && indexy >= 0 && indexy < mNRows)
            {
                return mData[indexy * mNCols + indexx];
            }
            return -1.0;  // out of scope
        }

        /// get value of grid at index positions
        public double value(int Index)
        {
            if (Index >= 0 && Index < mDataSize)
            {
                return mData[Index];
            }
            return -1.0;  // out of scope
        }

        public double value(double X, double Y)
        {
            Vector3D model = new Vector3D();
            model.setX(X);
            model.setY(Y);
            model.setZ(0.0);
            Vector3D world = new Vector3D();
            modelToWorld(model, world);

            world.setX(world.x() - mOrigin.X);
            world.setY(world.y() - mOrigin.Y);

            // get value out of grid.
            // double rx = Origin.x + X * xAxis.x + Y * yAxis.x;
            // double ry = Origin.y + X * xAxis.y + Y * yAxis.y;
            if (world.x() < 0.0 || world.y() < 0.0)
            {
                return -1.0;
            }
            int ix = (int)(world.x() / mCellSize);
            int iy = (int)(world.y() / mCellSize);
            if (ix >= 0 && ix < mNCols && iy >= 0 && iy < mNRows)
            {
                double value = mData[iy * mNCols + ix];
                if (value != mNODATAValue)
                {
                    return value;
                }
            }
            return -10.0; // the ultimate NODATA- or ErrorValue
        }

        public Vector3D coord(int indexx, int indexy)
        {
            Vector3D world = new Vector3D((indexx + 0.5) * mCellSize + mOrigin.X,
                                          (indexy + 0.5) * mCellSize + mOrigin.Y,
                                          0.0);
            Vector3D model = new Vector3D();
            worldToModel(world, model);
            return model;
        }

        public RectangleF rectangle(int indexx, int indexy)
        {
            Vector3D world = new Vector3D(indexx * mCellSize + mOrigin.X,
                                        indexy * mCellSize + mOrigin.Y,
                                        0.0);
            Vector3D model = new Vector3D();
            worldToModel(world, model);
            RectangleF rect = new RectangleF((float)model.x(), // left
                        (float)model.y(), // top
                        (float)mCellSize, // width
                        (float)mCellSize); // height
            return rect;
        }

        public Vector3D coord(int Index)
        {
            if (Index < 0 || Index >= mDataSize)
            {
                throw new ArgumentOutOfRangeException(nameof(Index), "gisgrid:coord: invalid index.");
            }
            int ix = Index % mNCols;
            int iy = Index / mNCols;
            return coord(ix, iy);
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

        public void clip(RectangleF box)
        {
            // auf das angegebene Rechteck zuschneiden, alle
            // werte draussen auf -1 setzen.
            for (int ix = 0; ix < mNCols; ix++)
            {
                for (int iy = 0; iy < mNRows; iy++)
                {
                    Vector3D akoord = coord(iy * mNCols + ix);
                    if (!box.Contains((float)akoord.x(), (float)akoord.y()))
                    {
                        mData[iy * mNCols + ix] = -1.0;
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
