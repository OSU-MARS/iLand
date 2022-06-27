using iLand.Input;
using iLand.Input.ProjectFile;
using System;
using System.Collections.Generic;

namespace iLand.Simulation
{
    public class ScheduledEvents
    {
        private readonly Dictionary<int, List<MutableTuple<string, string>>> eventsByYear;

        public ScheduledEvents(Project projectFile, string eventFilePath)
        {
            this.eventsByYear = new Dictionary<int, List<MutableTuple<string, string>>>();
            
            using CsvFile eventFile = new(projectFile.GetFilePath(ProjectDirectory.Home, eventFilePath));
            int yearIndex = eventFile.GetColumnIndex("year");
            if (yearIndex == -1)
            {
                throw new NotSupportedException(String.Format("TimeEvents: input file '{0}' has no 'year' column.", eventFilePath));
            }

            eventFile.Parse((string[] row) =>
            {
                int year = Int32.Parse(row[yearIndex]);
                if (this.eventsByYear.TryGetValue(year, out List<MutableTuple<string, string>>? eventsOfYear) == false)
                {
                    eventsOfYear = new List<MutableTuple<string, string>>();
                    this.eventsByYear.Add(year, eventsOfYear);
                }

                for (int column = 0; column < row.Length; column++)
                {
                    if (column != yearIndex)
                    {
                        MutableTuple<string, string> eventInYear = new(eventFile.Columns[column], row[column]);
                        eventsOfYear.Add(eventInYear);
                    }
                }
            });
        }

        public void RunYear(Model model)
        {
            int currentYear = model.CurrentYear;
            if (eventsByYear.TryGetValue(currentYear, out List<MutableTuple<string, string>>? eventsOfYear) == false)
            {
                return;
            }

            int valuesSet = 0;
            foreach (MutableTuple<string, string> eventInYear in eventsOfYear)
            {
                string key = eventInYear.Item1; // key
                // special values: if (key=="xxx" ->
                if (String.Equals(key, "script", StringComparison.OrdinalIgnoreCase) || String.Equals(key, "javascript", StringComparison.OrdinalIgnoreCase))
                {
                    // execute as javascript expression within the management script context...
                    if (String.IsNullOrEmpty(eventInYear.Item2.ToString()) == false)
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
            if (eventsByYear.TryGetValue(year, out List<MutableTuple<string, string>>? eventsOfYear) == false)
            {
                return null;
            }

            foreach (MutableTuple<string, string> timeEvent in eventsOfYear)
            {
                if (timeEvent.Item1 == eventKey)
                {
                    return timeEvent.Item2;
                }
            }
            return null;
        }
    }
}
