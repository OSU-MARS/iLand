﻿using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace iLand.Core
{
    /** Represents the input of various variables with regard to climate, soil properties and more.
        @ingroup tools
        Data is read from various sources and presented to the core model with a standardized interface.
        see http://iland.boku.ac.at/simulation+extent
        */
    public class Environment
    {
        // ******** specific keys *******
        private const string SpeciesKey = "model.species.source";
        private const string ClimateKey = "model.climate.tableName";

        private bool mGridMode;
        private List<string> mKeys;
        private readonly Dictionary<string, int> mRowCoordinates;
        private readonly Dictionary<string, object> mCreatedObjects;
        private CsvFile mInfile;

        public List<Climate> Climates { get; private set; } ///< created climates.
        public List<SpeciesSet> SpeciesSets { get; private set; } ///< created species sets
        public Climate CurrentClimate { get; private set; } ///< get climate at current pos
        public SpeciesSet CurrentSpeciesSet { get; private set; } ///< get species set on current pos
        public int CurrentID { get; private set; } ///< current grid id (in grid mode the id of the stand grid, in matrix mode simply the an autogenerated index)
        public GisGrid GisGrid { get; private set; }

        public bool IsSetup() { return mInfile != null; }
        public void SetDefaultValues(Climate climate, SpeciesSet speciesSet) { CurrentClimate = climate; CurrentSpeciesSet = speciesSet; }

        public Environment()
        {
            this.mCreatedObjects = new Dictionary<string, object>();
            this.mGridMode = false;
            this.mInfile = null;
            this.mRowCoordinates = new Dictionary<string, int>();

            this.Climates = new List<Climate>();
            this.CurrentClimate = null;
            this.CurrentID = 0;
            this.CurrentSpeciesSet = null;
            this.GisGrid = new GisGrid();
            this.SpeciesSets = new List<SpeciesSet>();
        }

        public bool LoadFromFile(string filePath, Model model)
        {
            string source = Helper.LoadTextFile(filePath);
            if (String.IsNullOrEmpty(source))
            {
                throw new NotSupportedException(String.Format("Environment: input file does not exist or is empty ({0})", filePath));
            }

            mInfile = new CsvFile();
            mInfile.LoadFromString(source);
            mKeys = mInfile.Captions;

            SpeciesSets.Clear(); // note: the objects are not destroyed - potential memory leak.
            Climates.Clear();
            mRowCoordinates.Clear();
            mCreatedObjects.Clear();
            CurrentID = 0;

            int index;
            if (mGridMode)
            {
                int id = mInfile.GetColumnIndex("id");
                if (id < 0)
                {
                    throw new NotSupportedException(" (grid mode) input file has no 'id' column!");
                }
                for (int row = 0; row < mInfile.RowCount; row++)
                {
                    mRowCoordinates[mInfile.Value(row, id).ToString()] = row;
                }
            }
            else
            {
                // ***  Matrix mode ******
                // each row must contain 'x' and 'y' coordinates
                // setup coordinates (x,y)
                int ix, iy;
                ix = mInfile.GetColumnIndex("x");
                iy = mInfile.GetColumnIndex("y");
                if (ix < 0 || iy < 0)
                {
                    throw new NotSupportedException(" (matrix mode) input file has no x/y coordinates!");
                }
                for (int row = 0; row < mInfile.RowCount; row++)
                {
                    string key = String.Format("{0}_{1}", mInfile.Value(row, ix).ToString(), mInfile.Value(row, iy).ToString());
                    mRowCoordinates[key] = row;
                }
            }

            // ******** setup of Species Sets *******
            if ((index = mKeys.IndexOf(SpeciesKey)) > -1)
            {
                using DebugTimer t = model.DebugTimers.Create("Environment.LoadFromString(species)");
                List<string> speciesNames = mInfile.Column(index).Distinct().ToList();
                Debug.WriteLine("creating species sets: " + speciesNames);
                foreach (string name in speciesNames)
                {
                    model.GlobalSettings.Settings.SetNodeValue(SpeciesKey, name); // set xml value
                    // create species sets
                    SpeciesSet set = new SpeciesSet();
                    SpeciesSets.Add(set);
                    mCreatedObjects[name] = (object)set;
                    set.Setup(model);
                }
                Debug.WriteLine(SpeciesSets.Count + " species sets created.");
            }
            else
            {
                // no species sets specified
                SpeciesSet speciesSet = new SpeciesSet();
                SpeciesSets.Add(speciesSet);
                speciesSet.Setup(model);
                CurrentSpeciesSet = speciesSet;
            }

            // ******** setup of Climate *******
            if ((index = mKeys.IndexOf(ClimateKey)) > -1)
            {
                using DebugTimer t = model.DebugTimers.Create("Environment.LoadFromString(climate)");
                List<string> climateNames = mInfile.Column(index).Distinct().ToList();
                if (model.GlobalSettings.LogDebug())
                {
                    Debug.WriteLine("creating climate: " + climateNames);
                    Debug.WriteLine("Environment: climate: # of climates in environment file:" + climateNames.Count);
                }
                foreach (string name in climateNames)
                {
                    // create an entry in the list of created objects, but
                    // really create the climate only if required (see setPosition() )
                    mCreatedObjects[name] = null;
                    model.GlobalSettings.Settings.SetNodeValue(ClimateKey, name); // set xml value
                }
            }
            else
            {
                // no climate defined - setup default climate
                Climate c = new Climate();
                Climates.Add(c);
                c.Setup(model);
                CurrentClimate = c;
            }
            if (CurrentClimate == null && Climates.Count > 0)
            {
                CurrentClimate = Climates[0];
            }
            if (CurrentSpeciesSet == null && SpeciesSets.Count > 0)
            {
                CurrentSpeciesSet = SpeciesSets[0];
            }
            return true;
        }

        /** sets the "pointer" to a "position" (metric coordinates).
            All specified values are set (also the climate/species-set pointers).
            */
        public void SetPosition(PointF position, Model model)
        {
            // no changes occur, when the "environment" is not loaded
            if (!IsSetup())
            {
                return;
            }

            int id = -1;
            int ix = -1;
            int iy = -1;
            string key;
            if (mGridMode)
            {
                // grid mode
                id = (int)GisGrid.GetValue(position);
                CurrentID = id;
                key = id.ToString();
                if (id == -1)
                {
                    return; // no data for the resource unit
                }
            }
            else
            {
                // access data in the matrix by resource unit indices
                ix = (int)(position.X / Constant.RUSize);
                iy = (int)(position.Y / Constant.RUSize);
                CurrentID++; // to have Ids for each resource unit

                key = String.Format("{0}_{1}", ix, iy);
            }

            if (mRowCoordinates.ContainsKey(key) == false)
            {
                if (mGridMode)
                {
                    throw new FileLoadException(String.Format("Resource unit {0} (position ({1}, {2}) m) not found in environment file.", id, position.X, position.Y));
                }
                else
                {
                    throw new FileLoadException(String.Format("Resource unit not found at coordinates {0}, {1} in environment file (physical position {2}, {3} m).", ix, iy, position.X, position.Y));
                }
            }

            int row = mRowCoordinates[key];
            if (model.GlobalSettings.LogInfo())
            {
                Debug.WriteLine("settting up point " + position + " with row " + row);
            }
            for (int col = 0; col < mInfile.ColCount; col++)
            {
                if (mKeys[col] == "id")
                {
                    CurrentID = Int32.Parse(mInfile.Value(row, col));
                    continue;
                }
                if (mKeys[col] == "x" || mKeys[col] == "y") // ignore "x" and "y" keys
                {
                    continue;
                }
                string value = mInfile.Value(row, col).ToString();
                if (model.GlobalSettings.LogInfo())
                {
                    Debug.WriteLine("set " + mKeys[col] + " to " + value);
                }
                model.GlobalSettings.Settings.SetNodeValue(mKeys[col], value);
                // special handling for constructed objects:
                if (mKeys[col] == SpeciesKey)
                {
                    CurrentSpeciesSet = (SpeciesSet)mCreatedObjects[value];
                }
                if (mKeys[col] == ClimateKey)
                {
                    CurrentClimate = (Climate)mCreatedObjects[value];
                    if (CurrentClimate == null)
                    {
                        // create only those climate sets that are really used in the current landscape
                        Climate climate = new Climate();
                        Climates.Add(climate);
                        mCreatedObjects[value] = (object)climate;
                        climate.Setup(model);
                        CurrentClimate = climate;
                    }
                }
            }
        }

        public bool SetGridMode(string gridFileName)
        {
            GisGrid.LoadFromFile(gridFileName);
            mGridMode = true;
            return true;
        }
    }
}
