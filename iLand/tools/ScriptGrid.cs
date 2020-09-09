using iLand.core;
using System;
using System.Collections.Generic;
using System.IO;

namespace iLand.tools
{
    internal class ScriptGrid
    {
        private Grid<double> mGrid;
        private string mVariableName;
        private static int mDeleted = 0;
        private static int mCreated = 0;

        public ScriptGrid(object parent = null)
        {
            mGrid = null;
            mVariableName = "x"; // default name
            mCreated++;
        }

        public ScriptGrid(Grid<double> grid)
            : this()
        {
            mGrid = grid;
        }

        public string name() { return mVariableName; }
        public Grid<double> grid() { return mGrid; }
        void setGrid(Grid<double> grid) { mGrid = grid; }

        public int width() { return mGrid != null ? mGrid.sizeX() : -1; }
        public int height() { return mGrid != null ? mGrid.sizeY() : -1; }
        public int count() { return mGrid != null ? mGrid.count() : -1; }
        public int cellsize() { return mGrid != null ? (int)mGrid.cellsize() : -1; } // BUGBUG: why truncate float cellsize to integer?
        public bool isValid() { return mGrid != null ? !mGrid.isEmpty() : false; }

        public void setName(string arg) { mVariableName = arg; }

        /// access values of the grid
        public double value(int x, int y)
        {
            return (isValid() && mGrid.isIndexValid(x, y)) ? mGrid.valueAtIndex(x, y) : -1.0;
        }

        /// write values to the grid
        public void setValue(int x, int y, double value)
        {
            if (isValid() && mGrid.isIndexValid(x, y))
            {
                mGrid[x, y] = value;
            }
        }

        // create a ScriptGrid-Wrapper around "grid". Note: destructing the 'grid' is done via the JS-garbage-collector.
        public static QJSValue createGrid(Grid<double> grid, string name)
        {
            ScriptGrid g = new ScriptGrid(grid);
            if (String.IsNullOrEmpty(name))
            {
                g.setName(name);
            }
            QJSValue jsgrid = GlobalSettings.instance().scriptEngine().newQObject(g);
            return jsgrid;
        }

        public QJSValue copy()
        {
            if (mGrid == null)
            {
                return new QJSValue();
            }

            ScriptGrid newgrid = new ScriptGrid();
            // copy the data
            Grid<double> copy_grid = new Grid<double>(mGrid);
            newgrid.setGrid(copy_grid);

            QJSValue jsgrid = GlobalSettings.instance().scriptEngine().newQObject(newgrid);
            return jsgrid;
        }

        public void clear()
        {
            if (mGrid != null && !mGrid.isEmpty())
            {
                mGrid.wipe();
            }
        }

        public void paint(double min_val, double max_val)
        {
            // BUGBUG: no op in C++
            //if (GlobalSettings.instance().controller())
            //    GlobalSettings.instance().controller().addGrid(mGrid, mVariableName, GridViewRainbow, min_val, max_val);
        }

        public string info()
        {
            if (mGrid == null || mGrid.isEmpty())
            {
                return "not valid / empty.";
            }
            return String.Format("grid-dimensions: {0}/{1} (cellsize: {4}, N cells: {2}), grid-name='{3}'", mGrid.sizeX(), mGrid.sizeY(), mGrid.count(), mVariableName, mGrid.cellsize());
        }

        public void save(string fileName)
        {
            if (mGrid != null || mGrid.isEmpty())
            {
                return;
            }
            fileName = GlobalSettings.instance().path(fileName);
            string result = Grid.gridToESRIRaster(mGrid);
            Helper.saveToTextFile(fileName, result);
            Console.WriteLine("saved grid " + name() + " to " + fileName);
        }

        public bool load(string fileName)
        {
            fileName = GlobalSettings.instance().path(fileName);
            // load the grid from file
            MapGrid mg = new MapGrid(fileName, false);
            if (!mg.isValid())
            {
                Console.WriteLine("load(): load not successful of file: " + fileName);
                return false;
            }
            mGrid = mg.grid().toDouble(); // create a copy of the mapgrid-grid
            mVariableName = Path.GetFileNameWithoutExtension(new FileInfo(fileName).Name);
            return !mGrid.isEmpty();

        }

        public void apply(string expression)
        {
            if (mGrid == null || mGrid.isEmpty())
            {
                return;
            }

            Expression expr = new Expression();
            double varptr = expr.addVar(mVariableName);
            expr.setExpression(expression);
            expr.parse();

            // now apply function on grid
            for (int p = 0; p != mGrid.count(); ++p)
            {
                expr.setVar(mVariableName, mGrid[p]);
                mGrid[p] = expr.execute();
            }
        }

        public void combine(string expression, QJSValue grid_object)
        {
            if (!grid_object.isObject())
            {
                Console.WriteLine("ERROR: combine(): no valid grids object" + grid_object.ToString());
                return;
            }

            List<Grid<double>> grids = new List<Grid<double>>();
            List<string> names = new List<string>();
            QJSValueIterator it = new QJSValueIterator(grid_object);
            while (it.hasNext())
            {
                it.next();
                names.Add(it.name());
                ScriptGrid o = (ScriptGrid)it.value().toVariant();
                if (o != null)
                {
                    grids.Add(o.grid());
                    if (grids[^1].isEmpty() || grids[^1].cellsize() != mGrid.cellsize() || grids[^1].rectangle() != mGrid.rectangle())
                    {
                        Console.WriteLine("ERROR: combine(): the grid " + it.name() + "is empty or has different dimensions:" + o.info());
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: combine(): no valid grid object with name:" + it.name());
                    return;
                }
            }
            // now add names
            Expression expr = new Expression();
            List<double> vars = new List<double>();
            for (int i = 0; i < names.Count; ++i)
            {
                vars.Add(expr.addVar(names[i]));
            }
            expr.setExpression(expression);
            expr.parse();

            // now apply function on grid
            for (int i = 0; i < mGrid.count(); ++i)
            {
                // set variable values in the expression object
                for (int v = 0; v < names.Count; ++v)
                {
                    vars[v] = grids[v].valueAtIndex(i);
                }
                double result = expr.execute();
                mGrid[i] = result; // write back value
            }
        }

        public double sum(string expression)
        {
            if (mGrid == null || mGrid.isEmpty())
            {
                return -1.0;
            }

            Expression expr = new Expression();
            expr.addVar(mVariableName);
            expr.setExpression(expression);
            expr.parse();

            // now apply function on grid
            double sum = 0.0;
            for (int p = 0; p != mGrid.count(); ++p)
            {
                expr.setVar(mVariableName, mGrid[p]);
                sum += expr.execute();
            }
            return sum;
        }
    }
}
