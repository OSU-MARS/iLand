using System;
using System.Diagnostics;

namespace iLand.tools
{
    internal class ScriptObjectFactory
    {
        private int mObjCreated;

        public ScriptObjectFactory(object parent = null)
        {
            mObjCreated = 0;
        }

        public QJSValue newCSVFile(string filename)
        {
            CSVFile csv_file = new CSVFile();
            if (String.IsNullOrEmpty(filename) == false)
            {
                Debug.WriteLine("CSVFile: loading file " + filename);
                csv_file.loadFile(filename);
            }

            QJSValue obj = GlobalSettings.instance().scriptEngine().newQObject(csv_file);
            mObjCreated++;
            return obj;
        }

        public QJSValue newClimateConverter()
        {
            ClimateConverter cc = new ClimateConverter(0);
            QJSValue obj = GlobalSettings.instance().scriptEngine().newQObject(cc);
            mObjCreated++;
            return obj;
        }

        public QJSValue newMap()
        {
            MapGridWrapper map = new MapGridWrapper(0);
            QJSValue obj = GlobalSettings.instance().scriptEngine().newQObject(map);
            mObjCreated++;
            return obj;
        }
    }
}
