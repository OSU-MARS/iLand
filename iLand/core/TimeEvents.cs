using iLand.Tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace iLand.Core
{
    public class TimeEvents
    {
        private readonly MultiValueDictionary<int, MutableTuple<string, object>> mData;

        public TimeEvents()
        {
            this.mData = new MultiValueDictionary<int, MutableTuple<string, object>>();
        }

        public void Clear() 
        { 
            mData.Clear(); 
        }

        public bool LoadFromFile(string fileName)
        {
            string source = Helper.LoadTextFile(GlobalSettings.Instance.Path(fileName));
            if (String.IsNullOrEmpty(source))
            {
                throw new FileNotFoundException(String.Format("TimeEvents: input file does not exist or is empty ({0})", fileName));
            }

            CsvFile infile = new CsvFile();
            infile.LoadFromString(source);
            List<string> captions = infile.Captions;
            int yearcol = infile.GetColumnIndex("year");
            if (yearcol == -1)
            {
                throw new NotSupportedException(String.Format("TimeEvents: input file '{0}' has no 'year' column.", fileName));
            }
            // BUGBUG: no checking of header line
            for (int row = 1; row < infile.RowCount; row++)
            {
                int year = Int32.Parse(infile.Value(row, yearcol));
                List<object> line = infile.Values(row);
                for (int col = 0; col < line.Count; col++)
                {
                    if (col != yearcol)
                    {
                        MutableTuple<string, object> entry = new MutableTuple<string, object>(captions[col], line[col]);
                        mData.Add(year, entry);
                    }
                }
            } // for each row
            Debug.WriteLine(String.Format("loaded TimeEvents (file: {0}). {1} items stored.", fileName, mData.Count));
            return true;
        }

        public void Run()
        {
            int currentYear = GlobalSettings.Instance.CurrentYear;
            if (mData.TryGetValue(currentYear, out IReadOnlyCollection<MutableTuple<string, object>> currentEvents) == false || currentEvents.Count == 0)
            {
                return;
            }

            int valuesSet = 0;
            foreach (MutableTuple<string, object> eventInYear in currentEvents)
            {
                string key = eventInYear.Item1; // key
                // special values: if (key=="xxx" ->
                if (key == "script" || key == "javascript")
                {
                    // execute as javascript expression within the management script context...
                    if (String.IsNullOrEmpty(eventInYear.Item2.ToString()) == false)
                    {
                        Debug.WriteLine("executing Javascript time event: " + eventInYear.Item2.ToString());
                    }
                }
                else
                {
                    // no special value: a xml node...
                    if (GlobalSettings.Instance.Settings.SetNodeValue(key, eventInYear.Item2.ToString()))
                    {
                        Debug.WriteLine("TimeEvents: Error: Key " + key + "not found! (tried to set to " + eventInYear.Item2.ToString() + ")");
                    }
                    else
                    {
                        Debug.WriteLine("TimeEvents: set " + key + "to" + eventInYear.Item2.ToString());
                    }
                }
                valuesSet++;
            }

            if (valuesSet != 0)
            {
                Debug.WriteLine("TimeEvents: year " + currentYear + ": " + valuesSet + " values set.");
            }
        }

        // read value for key 'key' and year 'year' from the list of items.
        // return a empty object if for 'year' no value is set
        public object Value(int year, string key)
        {
            if (mData.TryGetValue(year, out IReadOnlyCollection<MutableTuple<string, object>> it) == false)
            {
                return null;
            }

            foreach (MutableTuple<string, object> timeEvent in it)
            {
                if (timeEvent.Item1 == key)
                {
                    return timeEvent.Item2;
                }
            }
            return null;
        }
    }
}
