﻿using iLand.Input;
using iLand.Input.ProjectFile;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace iLand.Simulation
{
    public class ScheduledEvents
    {
        private readonly Dictionary<int, List<(string Name, string Value)>> eventsByYear;

        public ScheduledEvents(Project projectFile, string eventFilePath)
        {
            this.eventsByYear = new();
            
            using CsvFile eventFile = new(projectFile.GetFilePath(ProjectDirectory.Home, eventFilePath));
            int yearIndex = eventFile.GetColumnIndex("year");
            if (yearIndex == -1)
            {
                throw new NotSupportedException(String.Format("TimeEvents: input file '{0}' has no 'year' column.", eventFilePath));
            }

            eventFile.Parse((string[] row) =>
            {
                int year = Int32.Parse(row[yearIndex], CultureInfo.InvariantCulture);
                if (this.eventsByYear.TryGetValue(year, out List<(string Name, string Value)>? eventsOfYear) == false)
                {
                    eventsOfYear = new();
                    this.eventsByYear.Add(year, eventsOfYear);
                }

                for (int column = 0; column < row.Length; column++)
                {
                    if (column != yearIndex)
                    {
                        (string Name, string Value) eventInYear = new(eventFile.Columns[column], row[column]);
                        eventsOfYear.Add(eventInYear);
                    }
                }
            });
        }

        public void RunYear(Model model)
        {
            int currentSimulationYear = model.SimulationState.CurrentYear;
            if (eventsByYear.TryGetValue(currentSimulationYear, out List<(string Name, string Value)>? eventsOfYear) == false)
            {
                return;
            }

            int valuesSet = 0;
            foreach ((string Name, string Value) eventInYear in eventsOfYear)
            {
                string key = eventInYear.Name; // key
                // special values: if (key=="xxx" ->
                if (String.Equals(key, "script", StringComparison.OrdinalIgnoreCase) || String.Equals(key, "javascript", StringComparison.OrdinalIgnoreCase))
                {
                    // execute as javascript expression within the management script context...
                    if (String.IsNullOrEmpty(eventInYear.Value.ToString()) == false)
                    {
                        throw new NotImplementedException();
                        // Debug.WriteLine("Executing JavaScript time event: " + eventInYear.Item2.ToString());
                    }
                }
                else
                {
                    throw new NotImplementedException();
                    //globalSettings.Settings.SetParameter(key, eventInYear.Item2.ToString());
                    // Debug.WriteLine("TimeEvents: set " + key + "to" + eventInYear.Item2.ToString());
                }
                ++valuesSet;
            }

            //if (valuesSet != 0)
            //{
            //    Debug.WriteLine("TimeEvents: year " + currentYear + ": " + valuesSet + " values set.");
            //}
        }

        // read value for key 'key' and year 'year' from the list of items.
        // return a empty object if for 'year' no value is set
        public string? GetEvent(int year, string eventKey)
        {
            if (eventsByYear.TryGetValue(year, out List<(string Name, string Value)>? eventsOfYear) == false)
            {
                return null;
            }

            foreach ((string Name, string Value) timeEvent in eventsOfYear)
            {
                if (timeEvent.Name == eventKey)
                {
                    return timeEvent.Value;
                }
            }
            return null;
        }
    }
}
