﻿using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit climates and soil properties plus a few other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See http://iland.boku.ac.at/simulation+extent.
    /// </remarks>
    public class EnvironmentReader
    {
        private bool isGridMode;
        private List<string> columnNames;
        private CsvFile mInfile;
        private readonly Dictionary<string, int> rowIndexByCoordinateOrID;

        public Dictionary<string, World.Climate> ClimatesByName { get; private set; }
        public World.Climate CurrentClimate { get; private set; }
        public int CurrentResourceUnitID { get; private set; } // current grid id (in grid mode the id of the stand grid, in matrix mode simply the an autogenerated index)

        public float AnnualNitrogenDeposition { get; private set; }

        public Nullable<float> CurrentSnagOtherC { get; private set; }
        public Nullable<float> CurrentSnagOtherCN { get; private set; }
        public Nullable<float> CurrentSnagSwdC { get; private set; }
        public Nullable<float> CurrentSnagSwdCN { get; private set; }
        public Nullable<float> CurrentSnagSwdCount { get; private set; }
        public Nullable<float> CurrentSnagSwdDecompositionRate { get; private set; }
        public Nullable<float> CurrentSnagSwdHalfLife { get; private set; }
        public Nullable<float> CurrentSnagSwdN { get; private set; }

        public Nullable<float> CurrentSoilAvailableNitrogen { get; private set; }
        public Nullable<float> CurrentSoilDepth { get; private set; }
        public Nullable<float> CurrentSoilEr { get; private set; }
        public Nullable<float> CurrentSoilEl { get; private set; }
        public Nullable<float> CurrentSoilLeaching { get; private set; }
        public Nullable<float> CurrentSoilHumificationRate { get; private set; }
        public Nullable<float> CurrentSoilOrganicC { get; private set; }
        public Nullable<float> CurrentSoilOrganicDecompositionRate { get; private set; }
        public Nullable<float> CurrentSoilOrganicN { get; private set; }
        public Nullable<float> CurrentSoilQh { get; private set; }
        public Nullable<float> CurrentSoilSand { get; private set; }
        public Nullable<float> CurrentSoilSilt { get; private set; }
        public Nullable<float> CurrentSoilClay { get; private set; }
        public Nullable<float> CurrentSoilYoungLabileC { get; private set; }
        public Nullable<float> CurrentSoilYoungLabileDecompositionRate { get; private set; }
        public Nullable<float> CurrentSoilYoungLabileN { get; private set; }
        public Nullable<float> CurrentSoilYoungRefractoryC { get; private set; }
        public Nullable<float> CurrentSoilYoungRefractoryDecompositionRate { get; private set; }
        public Nullable<float> CurrentSoilYoungRefractoryN { get; private set; }

        public TreeSpeciesSet CurrentSpeciesSet { get; private set; } // get species set on current pos
        public GisGrid GisGrid { get; private set; }

        public float SoilLeaching { get; private set; }
        public float SoilQb { get; private set; }

        public Dictionary<string, TreeSpeciesSet> SpeciesSetsByTableName { get; private set; } // created species sets
        public bool UseDynamicAvailableNitrogen { get; private set; } // if true, iLand utilizes the soil-model N for species responses (and the dynamically calculated N available?)

        public EnvironmentReader()
        {
            this.isGridMode = false;
            this.mInfile = null;
            this.rowIndexByCoordinateOrID = new Dictionary<string, int>();

            this.ClimatesByName = new Dictionary<string, World.Climate>();
            this.CurrentResourceUnitID = 0;
            this.CurrentSpeciesSet = null;
            this.GisGrid = new GisGrid();
            this.SpeciesSetsByTableName = new Dictionary<string, TreeSpeciesSet>();
        }

        public bool IsSetup() { return mInfile != null; }

        public bool LoadFromProjectAndEnvironmentFile(Project projectFile, Landscape landscape)
        {
            string environmentFilePath = projectFile.GetFilePath(ProjectDirectory.Home, projectFile.Model.World.EnvironmentFile); // TODO: stop requiring gis\ prefix in project file

            mInfile = new CsvFile();
            mInfile.LoadFile(environmentFilePath);
            if (mInfile.RowCount < 2)
            {
                throw new NotSupportedException("Input file '" + environmentFilePath + "' is empty.");
            }
            columnNames = mInfile.ColumnNames;

            rowIndexByCoordinateOrID.Clear();
            ClimatesByName.Clear();
            CurrentResourceUnitID = 0;
            SpeciesSetsByTableName.Clear();

            if (isGridMode)
            {
                int idIndex = mInfile.GetColumnIndex("id");
                if (idIndex < 0)
                {
                    throw new NotSupportedException("Input file has no 'id' column!");
                }
                for (int rowIndex = 0; rowIndex < mInfile.RowCount; ++rowIndex)
                {
                    rowIndexByCoordinateOrID[mInfile.GetValue(idIndex, rowIndex).ToString()] = rowIndex;
                }
            }
            else
            {
                // ***  Matrix mode ******
                // each row must contain 'x' and 'y' coordinates
                // setup coordinates (x,y)
                int xIndex = mInfile.GetColumnIndex("x");
                int yIndex = mInfile.GetColumnIndex("y");
                if (xIndex < 0 || yIndex < 0)
                {
                    throw new NotSupportedException("Input file must have x and y columns.");
                }
                for (int rowIndex = 0; rowIndex < mInfile.RowCount; ++rowIndex)
                {
                    string key = String.Format("{0}_{1}", mInfile.GetValue(xIndex, rowIndex).ToString(), mInfile.GetValue(yIndex, rowIndex).ToString());
                    rowIndexByCoordinateOrID[key] = rowIndex;
                }
            }

            // soil parameters
            this.AnnualNitrogenDeposition = projectFile.Model.Settings.Soil.NitrogenDeposition;
            this.SoilLeaching = projectFile.Model.Settings.Soil.Leaching;
            this.SoilQb = projectFile.Model.Settings.Soil.Qb;
            this.UseDynamicAvailableNitrogen = projectFile.Model.Settings.Soil.UseDynamicAvailableNitrogen;

            // species sets
            int speciesTableNameIndex;
            if ((speciesTableNameIndex = columnNames.IndexOf(Constant.Setting.SpeciesTable)) > -1)
            {
                //using DebugTimer t = model.DebugTimers.Create("Environment.LoadFromString(species)");
                List<string> uniqueSpeciesSetNames = mInfile.GetColumnValues(speciesTableNameIndex).Distinct().ToList();
                //Debug.WriteLine("Environment: Creating " + uniqueSpeciesSetNames + " species sets.");
                foreach (string name in uniqueSpeciesSetNames)
                {
                    //model.GlobalSettings.Settings.SetParameter(Constant.Setting.SpeciesTable, name); // set xml value
                    // create species sets
                    TreeSpeciesSet speciesSet = new TreeSpeciesSet(name);
                    speciesSet.Setup(projectFile, landscape);

                    if (this.CurrentSpeciesSet == null)
                    {
                        this.CurrentSpeciesSet = speciesSet;
                    }
                    this.SpeciesSetsByTableName.Add(name, speciesSet);
                }
            }
            else
            {
                // no species sets specified
                TreeSpeciesSet defaultSpeciesSet = new TreeSpeciesSet("species");
                defaultSpeciesSet.Setup(projectFile, landscape);
                CurrentSpeciesSet = defaultSpeciesSet;
                SpeciesSetsByTableName.Add(defaultSpeciesSet.SqlTableName, defaultSpeciesSet);
            }

            // climates
            if (columnNames.IndexOf(Constant.Setting.Climate.Name) == -1)
            {
                // no named climates defined: create a single default climate
                World.Climate defaultClimate = new World.Climate("default");
                defaultClimate.Setup(projectFile, landscape);
                ClimatesByName.Add(defaultClimate.Name, defaultClimate);
                CurrentClimate = defaultClimate;
            }
            // otherwise, instantiate named climates as needed

            return true;
        }

        /** sets the "pointer" to a "position" (metric coordinates).
            All specified values are set (also the climate/species-set pointers).
            */
        public void SetPosition(Project projectFile, Landscape landscape, PointF position)
        {
            // no changes occur, when the "environment" is not loaded
            if (this.IsSetup() == false)
            {
                return;
            }

            string key;
            if (this.isGridMode)
            {
                // grid mode
                int id = (int)this.GisGrid.GetValue(position);
                this.CurrentResourceUnitID = id;
                key = id.ToString();
                if (id == -1)
                {
                    return; // no data for the resource unit
                }
                if (rowIndexByCoordinateOrID.ContainsKey(key) == false)
                {
                    throw new FileLoadException(String.Format("Resource unit {0} (position ({1}, {2}) m) not found in environment file.", id, position.X, position.Y));
                }
            }
            else
            {
                // access data in the matrix by resource unit indices
                int ix = (int)(position.X / Constant.RUSize);
                int iy = (int)(position.Y / Constant.RUSize);
                ++this.CurrentResourceUnitID; // to have Ids for each resource unit

                key = String.Format("{0}_{1}", ix, iy);
                if (rowIndexByCoordinateOrID.ContainsKey(key) == false)
                {
                    throw new FileLoadException(String.Format("Resource unit not found at coordinates {0}, {1} in environment file (physical position {2}, {3} m).", ix, iy, position.X, position.Y));
                }
            }

            int row = rowIndexByCoordinateOrID[key];
            for (int columnIndex = 0; columnIndex < mInfile.ColumnCount; ++columnIndex)
            {
                if (columnNames[columnIndex] == "id")
                {
                    this.CurrentResourceUnitID = Int32.Parse(mInfile.GetValue(columnIndex, row));
                    continue;
                }
                if (columnNames[columnIndex] == "x" || columnNames[columnIndex] == "y") // ignore "x" and "y" keys
                {
                    continue;
                }

                string value = mInfile.GetValue(columnIndex, row);
                //if (model.GlobalSettings.LogDebug())
                //{
                //    Debug.WriteLine("Environment: set global parameter " + columnNames[columnIndex] + " to " + value);
                //}
                //model.GlobalSettings.Settings.SetXmlNodeValue(columnNames[columnIndex], value);
                // special handling for constructed objects:
                switch (columnNames[columnIndex])
                {
                    case Constant.Setting.SpeciesTable:
                        this.CurrentSpeciesSet = this.SpeciesSetsByTableName[value];
                        break;
                    case Constant.Setting.Climate.Name:
                        if (this.ClimatesByName.TryGetValue(value, out World.Climate climate) == false)
                        {
                            // create only those climate sets that are really used in the current landscape
                            climate = new World.Climate(value);
                            climate.Setup(projectFile, landscape);
                            this.ClimatesByName.Add(climate.Name, climate);
                        }
                        this.CurrentClimate = climate;
                        break;
                    case Constant.Setting.Snag.OtherC:
                        this.CurrentSnagOtherC = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.OtherCN:
                        this.CurrentSnagOtherCN = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.SwdC:
                        this.CurrentSnagSwdC = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.SwdCN:
                        this.CurrentSnagSwdCN = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.SwdDecompositionRate:
                        this.CurrentSnagSwdDecompositionRate = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.SwdHalfLife:
                        this.CurrentSnagSwdHalfLife = Single.Parse(value);
                        break;
                    case Constant.Setting.Snag.SwdN:
                        this.CurrentSnagSwdN = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.AvailableNitrogen:
                        this.CurrentSoilAvailableNitrogen = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.Depth:
                        this.CurrentSoilDepth = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.El:
                        this.CurrentSoilEl = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.Er:
                        this.CurrentSoilEr = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.Leaching:
                        this.CurrentSoilLeaching = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.HumificationRate:
                        this.CurrentSoilHumificationRate = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.OrganicMatterC:
                        this.CurrentSoilOrganicC = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.OrganicMatterDecompositionRate:
                        this.CurrentSoilOrganicDecompositionRate = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.OrganincMatterN:
                        this.CurrentSoilOrganicN = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.PercentClay:
                        this.CurrentSoilClay = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.PercentSand:
                        this.CurrentSoilSand = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.PercentSilt:
                        this.CurrentSoilSilt = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.Qh:
                        this.CurrentSoilQh = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungLabileC:
                        this.CurrentSoilYoungLabileC = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungLabileDecompositionRate:
                        this.CurrentSoilYoungLabileDecompositionRate = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungLabileN:
                        this.CurrentSoilYoungLabileN = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungRefractoryC:
                        this.CurrentSoilYoungRefractoryC = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungRefractoryDecompositionRate:
                        this.CurrentSoilYoungRefractoryDecompositionRate = Single.Parse(value);
                        break;
                    case Constant.Setting.Soil.YoungRefractoryN:
                        this.CurrentSoilYoungRefractoryN = Single.Parse(value);
                        break;
                    default:
                        throw new NotSupportedException("Unhandled environment table column '" + columnNames[columnIndex] + "'.");
                }
            }
        }

        public bool SetGridMode(string gridFileName)
        {
            this.GisGrid.LoadFromFile(gridFileName);
            this.isGridMode = true;
            return true;
        }
    }
}
