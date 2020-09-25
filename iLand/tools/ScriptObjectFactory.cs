using System;
using System.Diagnostics;

namespace iLand.tools
{
    internal class ScriptObjectFactory
    {
        public ScriptObjectFactory()
        {
        }

        public QJSValue NewCsvFile(string filename)
        {
            CsvFile csv_file = new CsvFile();
            if (String.IsNullOrEmpty(filename) == false)
            {
                Debug.WriteLine("CSVFile: loading file " + filename);
                csv_file.LoadFile(filename);
            }

            QJSValue obj = GlobalSettings.Instance.ScriptEngine.NewQObject(csv_file);
            return obj;
        }

        public QJSValue NewClimateConverter()
        {
            ClimateConverter cc = new ClimateConverter();
            QJSValue obj = GlobalSettings.Instance.ScriptEngine.NewQObject(cc);
            return obj;
        }

        public QJSValue NewMap()
        {
            MapGridWrapper map = new MapGridWrapper();
            QJSValue obj = GlobalSettings.Instance.ScriptEngine.NewQObject(map);
            return obj;
        }
    }
}
