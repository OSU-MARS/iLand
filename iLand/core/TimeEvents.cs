using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace iLand.core
{
    internal class TimeEvents
    {
        private static string lastLoadedFile;

        private MultiValueDictionary<int, MutableTuple<string, object>> mData;

        public void clear() { mData.Clear(); }

        public bool loadFromFile(string fileName)
        {
            string source = Helper.loadTextFile(GlobalSettings.instance().path(fileName));
            if (String.IsNullOrEmpty(source))
            {
                throw new FileNotFoundException(String.Format("TimeEvents: input file does not exist or is empty (%1)", fileName));
            }
            lastLoadedFile = fileName;
            return loadFromString(source);
        }

        public bool loadFromString(string source)
        {
            CSVFile infile = new CSVFile();
            infile.loadFromString(source);
            List<string> captions = infile.captions();
            int yearcol = infile.columnIndex("year");
            if (yearcol == -1)
            {
                throw new NotSupportedException(String.Format("TimeEvents: input file '{0}' has no 'year' column.", lastLoadedFile));
            }
            for (int row = 0; row < infile.rowCount(); row++)
            {
                int year = Int32.Parse(infile.value(row, yearcol));
                List<object> line = infile.values(row);
                for (int col = 0; col < line.Count; col++)
                {
                    if (col != yearcol)
                    {
                        MutableTuple<string, object> entry = new MutableTuple<string, object>(captions[col], line[col]);
                        mData.Add(year, entry);
                    }
                }
            } // for each row
            Debug.WriteLine(String.Format("loaded TimeEvents (file: {0}). {1} items stored.", lastLoadedFile, mData.Count));
            return true;
        }

        public void run()
        {
            int current_year = GlobalSettings.instance().currentYear();
            List<MutableTuple<string, object>> entries = mData[current_year].ToList();
            if (entries.Count == 0)
            {
                return;
            }

            string key;
            int values_set = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                key = entries[i].Item1; // key
                                        // special values: if (key=="xxx" ->
                if (key == "script" || key == "javascript")
                {
                    // execute as javascript expression within the management script context...
                    if (String.IsNullOrEmpty(entries[i].Item2.ToString()) == false)
                    {
                        Debug.WriteLine("executing Javascript time event: " + entries[i].Item2.ToString());
                        GlobalSettings.instance().executeJavascript(entries[i].Item2.ToString());
                    }
                }
                else
                {
                    // no special value: a xml node...
                    if (GlobalSettings.instance().settings().setNodeValue(key, entries[i].Item2.ToString()))
                        Debug.WriteLine("TimeEvents: Error: Key " + key + "not found! (tried to set to " + entries[i].Item2.ToString() + ")");
                    else
                        Debug.WriteLine("TimeEvents: set " + key + "to" + entries[i].Item2.ToString());
                }
                values_set++;
            }
            if (values_set != 0)
            {
                Debug.WriteLine("TimeEvents: year " + current_year + ": " + values_set + " values set.");
            }
        }

        // read value for key 'key' and year 'year' from the list of items.
        // return a empty object if for 'year' no value is set
        public object value(int year, string key)
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
