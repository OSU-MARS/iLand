﻿using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace iLand.Input
{
    /// <summary>
    /// Resource unit climates and soil properties plus a few other settings.
    /// </summary>
    /// <remarks>
    /// Data is read from various sources and presented to the core model with a standardized interface.
    //  See http://iland-model.org/simulation+extent.
    /// </remarks>
    public class EnvironmentReader
    {
        private readonly Dictionary<string, int> environmentIndexByCoordinateOrID;
        private readonly List<Environment> environments;
        private bool isGridMode;

        public Dictionary<string, World.Climate> ClimatesByID { get; private init; }
        public World.Climate? CurrentClimate { get; private set; }
        public Environment? CurrentEnvironment { get; private set; }
        public int CurrentResourceUnitID { get; private set; } // current grid id (in grid mode the id of the stand grid, in matrix mode simply the an autogenerated index)
        public TreeSpeciesSet? CurrentSpeciesSet { get; private set; } // get species set on current pos

        // soil parameters not currently supported in environment file
        public float AnnualNitrogenDeposition { get; private set; }
        public float SoilQb { get; private set; }
        public bool UseDynamicAvailableNitrogen { get; private set; } // if true, iLand utilizes the soil-model N for species responses (and the dynamically calculated N available?)

        public GisGrid GisGrid { get; private init; }
        public Dictionary<string, TreeSpeciesSet> SpeciesSetsByTableName { get; private init; } // created species sets

        public EnvironmentReader()
        {
            this.environmentIndexByCoordinateOrID = new Dictionary<string, int>();
            this.environments = new();
            this.isGridMode = false;

            this.ClimatesByID = new Dictionary<string, World.Climate>();
            this.CurrentClimate = null;
            this.CurrentEnvironment = null;
            this.CurrentResourceUnitID = 0;
            this.CurrentSpeciesSet = null;
            this.GisGrid = new GisGrid();
            this.SpeciesSetsByTableName = new Dictionary<string, TreeSpeciesSet>();
        }

        public void LoadFromProjectAndEnvironmentFile(Project projectFile)
        {
            string environmentFilePath = projectFile.GetFilePath(ProjectDirectory.Home, projectFile.World.EnvironmentFile); // TODO: stop requiring gis\ prefix in project file
            using CsvFile resourceUnitEnvironmentFile = new(environmentFilePath);
            
            EnvironmentHeader environmentHeader = new(resourceUnitEnvironmentFile);
            if ((this.isGridMode) && (environmentHeader.ResourceUnitID < 0))
            {
                throw new NotSupportedException("Environment file has no 'id' column.");
            }
            else if((environmentHeader.X < 0) || (environmentHeader.Y < 0))
            {
                throw new NotSupportedException("Environment file must have 'x' and 'y' columns.");
            }

            Environment defaultEnvironment = new(projectFile.World);
            if (String.IsNullOrEmpty(defaultEnvironment.ClimateID) && (environmentHeader.ClimateID < 0))
            {
                throw new NotSupportedException("Environment file must have a '" + Constant.Setting.Climate.Name + "' column if '" + Constant.Setting.Climate.Name + "' is not specified in the project file.");
            }
            if (String.IsNullOrEmpty(defaultEnvironment.SpeciesTableName) && (environmentHeader.SpeciesTableName < 0))
            {
                throw new NotSupportedException("Environment file must have a '" + Constant.Setting.SpeciesTable + "' column if '" + Constant.Setting.SpeciesTable + "' is not specified in the project file.");
            }

            this.ClimatesByID.Clear();
            this.environmentIndexByCoordinateOrID.Clear();
            this.environments.Clear();
            this.CurrentResourceUnitID = 0;
            this.SpeciesSetsByTableName.Clear();

            HashSet<string> uniqueSpeciesTableNames = new();
            resourceUnitEnvironmentFile.Parse((string[] row) =>
            {
                Environment resourceUnitEnvironment = new(environmentHeader, row, defaultEnvironment);

                string keyOrID;
                if (this.isGridMode)
                {
                    keyOrID = row[environmentHeader.ResourceUnitID];
                }
                else
                {
                    keyOrID = row[environmentHeader.X] + "_" + row[environmentHeader.Y];
                }
                environmentIndexByCoordinateOrID[keyOrID] = this.environments.Count;
                this.environments.Add(resourceUnitEnvironment);

                if (this.ClimatesByID.TryGetValue(resourceUnitEnvironment.ClimateID, out World.Climate? climate) == false)
                {
                    // create only those climate sets that are really used in the current landscape
                    climate = new World.Climate(projectFile, resourceUnitEnvironment.ClimateID);
                    this.ClimatesByID.Add(resourceUnitEnvironment.ClimateID, climate);
                }

                if (uniqueSpeciesTableNames.Contains(resourceUnitEnvironment.SpeciesTableName) == false)
                {
                    uniqueSpeciesTableNames.Add(resourceUnitEnvironment.SpeciesTableName);
                }
            });

            if (this.environments.Count < 1)
            {
                throw new NotSupportedException("Resource unit environment file '" + environmentFilePath + "' is empty or has only headers.");
            }
            if (uniqueSpeciesTableNames.Count < 1)
            {
                throw new NotSupportedException("No species table was found in resource unit environment file '" + environmentFilePath + "' or in the project file.");
            }

            // create species sets
            foreach (string tableName in uniqueSpeciesTableNames)
            {
                TreeSpeciesSet speciesSet = new(tableName);
                speciesSet.Setup(projectFile);
                this.SpeciesSetsByTableName.Add(tableName, speciesSet);
            }

            // climates
            if (resourceUnitEnvironmentFile.GetColumnIndex(Constant.Setting.Climate.Name) == -1)
            {
                // no named climates defined: create a single default climate
                string defaultClimateName = "default";
                World.Climate defaultClimate = new(projectFile, defaultClimateName);
                this.ClimatesByID.Add(defaultClimateName, defaultClimate);
                this.CurrentClimate = defaultClimate;
            }
            // otherwise, instantiate named climates as needed
        }

        public bool SetGridMode(string gridFileName)
        {
            this.GisGrid.LoadFromFile(gridFileName);
            this.isGridMode = true;
            return true;
        }

        /** sets the "pointer" to a "position" (metric coordinates).
            All specified values are set (also the climate/species-set pointers).
            */
        public void SetPosition(PointF ruGridCellPosition)
        {
            string key;
            if (this.isGridMode)
            {
                // grid mode
                int ruID = (int)this.GisGrid.GetValue(ruGridCellPosition);
                this.CurrentResourceUnitID = ruID;
                key = ruID.ToString();
                if (ruID == -1)
                {
                    return; // no data for the resource unit
                }
                if (environmentIndexByCoordinateOrID.ContainsKey(key) == false)
                {
                    throw new ArgumentOutOfRangeException(nameof(ruGridCellPosition), String.Format("Resource unit {0} (position ({1}, {2}) m) not found in environment file.", ruID, ruGridCellPosition.X, ruGridCellPosition.Y));
                }
            }
            else
            {
                // access data in the matrix by resource unit indices
                int indexX = (int)(ruGridCellPosition.X / Constant.RUSize);
                int indexY = (int)(ruGridCellPosition.Y / Constant.RUSize);
                ++this.CurrentResourceUnitID; // to have Ids for each resource unit

                key = String.Format("{0}_{1}", indexX, indexY);
                if (environmentIndexByCoordinateOrID.ContainsKey(key) == false)
                {
                    throw new FileLoadException(String.Format("Resource unit not found at coordinates {0}, {1} in environment file (physical position {2}, {3} m).", indexX, indexY, ruGridCellPosition.X, ruGridCellPosition.Y));
                }
            }

            int environmentIndex = environmentIndexByCoordinateOrID[key];
            this.CurrentEnvironment = this.environments[environmentIndex];

            this.CurrentClimate = this.ClimatesByID[this.CurrentEnvironment.ClimateID];
            this.CurrentResourceUnitID = this.CurrentEnvironment.ResourceUnitID;
            this.CurrentSpeciesSet = this.SpeciesSetsByTableName[this.CurrentEnvironment.SpeciesTableName];
        }
    }
}
