using iLand.core;
using System;
using System.Collections.Generic;
using System.IO;

namespace iLand.tools
{
    internal class ScriptGrid
    {
        private static int mCreated = 0;

        public Grid<double> Grid { get; set; }
        public string VariableName { get; private set; }

        public ScriptGrid()
        {
            Grid = null;
            VariableName = "x"; // default name
            mCreated++;
        }

        public ScriptGrid(Grid<double> grid)
            : this()
        {
            Grid = grid;
        }

        public int CellSize() { return Grid != null ? (int)Grid.CellSize : -1; } // BUGBUG: why truncate float cellsize to integer?
        public int Count() { return Grid != null ? Grid.Count : -1; }
        public int Height() { return Grid != null ? Grid.SizeY : -1; }
        public bool IsValid() { return Grid != null && !Grid.IsEmpty(); }
        public int Width() { return Grid != null ? Grid.SizeX : -1; }

        /// access values of the grid
        public double Value(int x, int y)
        {
            return (IsValid() && Grid.Contains(x, y)) ? Grid[x, y] : -1.0;
        }

        /// write values to the grid
        public void SetValue(int x, int y, double value)
        {
            if (IsValid() && Grid.Contains(x, y))
            {
                Grid[x, y] = value;
            }
        }

        // create a ScriptGrid-Wrapper around "grid". Note: destructing the 'grid' is done via the JS-garbage-collector.
        public static QJSValue CreateGrid(Grid<double> grid, string name)
        {
            ScriptGrid g = new ScriptGrid(grid);
            if (String.IsNullOrEmpty(name))
            {
                g.VariableName = name;
            }
            QJSValue jsgrid = GlobalSettings.Instance.ScriptEngine.NewQObject(g);
            return jsgrid;
        }

        public QJSValue Copy()
        {
            if (Grid == null)
            {
                return new QJSValue();
            }

            ScriptGrid newgrid = new ScriptGrid()
            {
                Grid = new Grid<double>(Grid)
            };

            QJSValue jsgrid = GlobalSettings.Instance.ScriptEngine.NewQObject(newgrid);
            return jsgrid;
        }

        public void Clear()
        {
            if (Grid != null && !Grid.IsEmpty())
            {
                Grid.ClearDefault();
            }
        }

        // unused in C++
        //public void paint(double min_val, double max_val)
        //{
        //    //if (GlobalSettings.instance().controller())
        //    //    GlobalSettings.instance().controller().addGrid(mGrid, mVariableName, GridViewRainbow, min_val, max_val);
        //}

        public string Info()
        {
            if (Grid == null || Grid.IsEmpty())
            {
                return "not valid / empty.";
            }
            return String.Format("grid-dimensions: {0}/{1} (cellsize: {4}, N cells: {2}), grid-name='{3}'", Grid.SizeX, Grid.SizeY, Grid.Count, VariableName, Grid.CellSize);
        }

        public void Save(string fileName)
        {
            if (Grid != null || Grid.IsEmpty())
            {
                return;
            }
            fileName = GlobalSettings.Instance.Path(fileName);
            string result = core.Grid.ToEsriRaster(Grid);
            Helper.SaveToTextFile(fileName, result);
            Console.WriteLine("saved grid " + this.VariableName + " to " + fileName);
        }

        public bool Load(string fileName)
        {
            fileName = GlobalSettings.Instance.Path(fileName);
            // load the grid from file
            MapGrid mg = new MapGrid(fileName, false);
            if (!mg.IsValid())
            {
                Console.WriteLine("load(): load not successful of file: " + fileName);
                return false;
            }
            Grid = mg.Grid.ToDouble(); // create a copy of the mapgrid-grid
            VariableName = Path.GetFileNameWithoutExtension(new FileInfo(fileName).Name);
            return !Grid.IsEmpty();
        }

        public void Apply(string expression)
        {
            if (Grid == null || Grid.IsEmpty())
            {
                return;
            }

            Expression expr = new Expression();
            expr.AddVariable(VariableName);
            expr.SetExpression(expression);
            expr.Parse();

            // now apply function on grid
            for (int p = 0; p != Grid.Count; ++p)
            {
                expr.SetVariable(VariableName, Grid[p]);
                Grid[p] = expr.Execute();
            }
        }

        public void Combine(string expression, QJSValue grid_object)
        {
            if (!grid_object.IsObject())
            {
                Console.WriteLine("ERROR: combine(): no valid grids object" + grid_object.ToString());
                return;
            }

            List<Grid<double>> grids = new List<Grid<double>>();
            List<string> names = new List<string>();
            QJSValueIterator it = new QJSValueIterator(grid_object);
            while (it.HasNext())
            {
                it.Next();
                names.Add(it.Name());
                ScriptGrid o = (ScriptGrid)it.Value().ToVariant();
                if (o != null)
                {
                    grids.Add(o.Grid);
                    if (grids[^1].IsEmpty() || grids[^1].CellSize != Grid.CellSize || grids[^1].Size() != Grid.Size())
                    {
                        Console.WriteLine("ERROR: combine(): the grid " + it.Name() + "is empty or has different dimensions:" + o.Info());
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: combine(): no valid grid object with name:" + it.Name());
                    return;
                }
            }
            // now add names
            Expression expr = new Expression();
            List<double> vars = new List<double>();
            for (int i = 0; i < names.Count; ++i)
            {
                vars.Add(expr.AddVariable(names[i]));
            }
            expr.SetExpression(expression);
            expr.Parse();

            // now apply function on grid
            for (int i = 0; i < Grid.Count; ++i)
            {
                // set variable values in the expression object
                for (int v = 0; v < names.Count; ++v)
                {
                    vars[v] = grids[v][i];
                }
                double result = expr.Execute();
                Grid[i] = result; // write back value
            }
        }

        public double Sum(string expression)
        {
            if (Grid == null || Grid.IsEmpty())
            {
                return -1.0;
            }

            Expression expr = new Expression();
            expr.AddVariable(VariableName);
            expr.SetExpression(expression);
            expr.Parse();

            // now apply function on grid
            double sum = 0.0;
            for (int p = 0; p != Grid.Count; ++p)
            {
                expr.SetVariable(VariableName, Grid[p]);
                sum += expr.Execute();
            }
            return sum;
        }
    }
}
