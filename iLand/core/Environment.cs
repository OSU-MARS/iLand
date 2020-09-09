﻿using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace iLand.core
{
    /** Represents the input of various variables with regard to climate, soil properties and more.
        @ingroup tools
        Data is read from various sources and presented to the core model with a standardized interface.
        see http://iland.boku.ac.at/simulation+extent
        */
    internal class Environment
    {
        // ******** specific keys *******
        private const string speciesKey = "model.species.source";
        private const string climateKey = "model.climate.tableName";

        private bool mGridMode;
        private int mCurrentID; ///< in grid mode, current id is the (grid) ID of the resource unit
        private Climate mCurrentClimate; ///< climate at current location
        private SpeciesSet mCurrentSpeciesSet; ///< species set at current location
        private List<Climate> mClimate; ///< created climates.
        private List<SpeciesSet> mSpeciesSets; ///< created species sets
        private List<string> mKeys;
        private Dictionary<string, int> mRowCoordinates;
        private Dictionary<string, object> mCreatedObjects;
        private CSVFile mInfile;
        private GisGrid mGrid;

        public bool isSetup() { return mInfile != null; }
        public void setDefaultValues(Climate climate, SpeciesSet speciesSet) { mCurrentClimate = climate; mCurrentSpeciesSet = speciesSet; }
        public List<Climate> climateList() { return mClimate; } ///< created climates.
        public List<SpeciesSet> speciesSetList() { return mSpeciesSets; } ///< created species sets
        public Climate climate() { return mCurrentClimate; } ///< get climate at current pos
        public SpeciesSet speciesSet() { return mCurrentSpeciesSet; } ///< get species set on current pos
        public int currentID() { return mCurrentID; } ///< current grid id (in grid mode the id of the stand grid, in matrix mode simply the an autogenerated index)

        public Environment()
        {
            mInfile = null;
            mGrid = null;
            mGridMode = false;
            mCurrentSpeciesSet = null;
            mCurrentClimate = null;
            mCurrentID = 0;
        }

        public bool loadFromFile(string fileName)
        {
            string source = Helper.loadTextFile(GlobalSettings.instance().path(fileName));
            if (String.IsNullOrEmpty(source))
            {
                throw new NotSupportedException(String.Format("Environment: input file does not exist or is empty ({0})", fileName));
            }
            return loadFromString(source);
        }

        public bool loadFromString(string source)
        {
            mInfile = new CSVFile();

            mInfile.loadFromString(source);
            mKeys = mInfile.captions();

            XmlHelper xml = GlobalSettings.instance().settings();
            mSpeciesSets.Clear(); // note: the objects are not destroyed - potential memory leak.
            mClimate.Clear();
            mRowCoordinates.Clear();
            mCreatedObjects.Clear();
            mCurrentID = 0;

            int index;
            if (mGridMode)
            {
                int id = mInfile.columnIndex("id");
                if (id < 0)
                {
                    throw new NotSupportedException(" (grid mode) input file has no 'id' column!");
                }
                for (int row = 0; row < mInfile.rowCount(); row++)
                {
                    mRowCoordinates[mInfile.value(row, id).ToString()] = row;
                }
            }
            else
            {
                // ***  Matrix mode ******
                // each row must contain 'x' and 'y' coordinates
                // setup coordinates (x,y)
                int ix, iy;
                ix = mInfile.columnIndex("x");
                iy = mInfile.columnIndex("y");
                if (ix < 0 || iy < 0)
                    throw new NotSupportedException(" (matrix mode) input file has no x/y coordinates!");
                for (int row = 0; row < mInfile.rowCount(); row++)
                {
                    string key = String.Format("{0}_{1}", mInfile.value(row, ix).ToString(), mInfile.value(row, iy).ToString());
                    mRowCoordinates[key] = row;
                }
            }

            // ******** setup of Species Sets *******
            if ((index = mKeys.IndexOf(speciesKey)) > -1)
            {
                using DebugTimer t = new DebugTimer("environment:load species");
                List<string> speciesNames = mInfile.column(index).Distinct().ToList();
                Debug.WriteLine("creating species sets: " + speciesNames);
                foreach (string name in speciesNames)
                {
                    xml.setNodeValue(speciesKey, name); // set xml value
                    // create species sets
                    SpeciesSet set = new SpeciesSet();
                    mSpeciesSets.Add(set);
                    mCreatedObjects[name] = (object)set;
                    set.setup();
                }
                Debug.WriteLine(mSpeciesSets.Count + " species sets created.");
            }
            else
            {
                // no species sets specified
                SpeciesSet speciesSet = new SpeciesSet();
                mSpeciesSets.Add(speciesSet);
                speciesSet.setup();
                mCurrentSpeciesSet = speciesSet;
            }

            // ******** setup of Climate *******
            if ((index = mKeys.IndexOf(climateKey)) > -1)
            {
                using DebugTimer t = new DebugTimer("environment:load climate");
                List<string> climateNames = mInfile.column(index).Distinct().ToList();
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("creating climate: " + climateNames);
                }
                Debug.WriteLine("Environment: climate: # of climates in environment file:" + climateNames.Count);
                foreach (string name in climateNames)
                {
                    // create an entry in the list of created objects, but
                    // really create the climate only if required (see setPosition() )
                    mCreatedObjects[name] = null;
                    xml.setNodeValue(climateKey, name); // set xml value
                }
            }
            else
            {
                // no climate defined - setup default climate
                Climate c = new Climate();
                mClimate.Add(c);
                c.setup();
                mCurrentClimate = c;
            }
            if (mCurrentClimate == null && mClimate.Count > 0)
            {
                mCurrentClimate = mClimate[0];
            }
            if (mCurrentSpeciesSet == null && mSpeciesSets.Count > 0)
            {
                mCurrentSpeciesSet = mSpeciesSets[0];
            }
            return true;
        }

        /** sets the "pointer" to a "position" (metric coordinates).
            All specified values are set (also the climate/species-set pointers).
            */
        public void setPosition(PointF position)
        {
            // no changes occur, when the "environment" is not loaded
            if (!isSetup())
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
                id = (int)mGrid.value(position);
                mCurrentID = id;
                key = id.ToString();
                if (id == -1)
                {
                    return; // no data for the resource unit
                }
            }
            else
            {
                // access data in the matrix by resource unit indices
                ix = (int)(position.X / 100.0); // suppose size of 1 ha for each coordinate
                iy = (int)(position.Y / 100.0);
                mCurrentID++; // to have Ids for each resource unit

                key = String.Format("{0}_{1}", ix, iy);
            }

            if (mRowCoordinates.ContainsKey(key))
            {
                XmlHelper xml = GlobalSettings.instance().settings();
                int row = mRowCoordinates[key];
                string value;
                if (GlobalSettings.instance().logLevelInfo())
                {
                    Debug.WriteLine("settting up point " + position + " with row " + row);
                }
                for (int col = 0; col < mInfile.colCount(); col++)
                {
                    if (mKeys[col] == "id")
                    {
                        mCurrentID = Int32.Parse(mInfile.value(row, col));
                        continue;
                    }
                    if (mKeys[col] == "x" || mKeys[col] == "y") // ignore "x" and "y" keys
                    {
                        continue;
                    }
                    value = mInfile.value(row, col).ToString();
                    if (GlobalSettings.instance().logLevelInfo())
                    {
                        Debug.WriteLine("set " + mKeys[col] + " to " + value);
                    }
                    xml.setNodeValue(mKeys[col], value);
                    // special handling for constructed objects:
                    if (mKeys[col] == speciesKey)
                        mCurrentSpeciesSet = (SpeciesSet)mCreatedObjects[value];
                    if (mKeys[col] == climateKey)
                    {
                        mCurrentClimate = (Climate)mCreatedObjects[value];
                        if (mCurrentClimate == null)
                        {
                            // create only those climate sets that are really used in the current landscape
                            Climate climate = new Climate();
                            mClimate.Add(climate);
                            mCreatedObjects[value] = (object)climate;
                            climate.setup();
                            mCurrentClimate = climate;

                        }
                    }
                }
            }
            else
            {
                if (mGridMode)
                {
                    throw new NotSupportedException(String.Format("Environment:setposition: invalid grid id (or not present in input file): {0}m/{1}m (mapped to id {2}).",
                                 position.X, position.Y, id));
                }
                else
                {
                    throw new NotSupportedException(String.Format("Environment:setposition: invalid coordinates (or not present in input file): {0}m/{1}m (mapped to indices {2}/{3}).",
                                     position.X, position.Y, ix, iy));
                }
            }
        }

        public bool setGridMode(string grid_file_name)
        {
            mGrid = new GisGrid();
            mGrid.loadFromFile(grid_file_name);
            mGridMode = true;
            return true;
        }
    }
}
