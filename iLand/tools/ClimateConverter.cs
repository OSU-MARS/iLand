using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;

namespace iLand.tools
{
    /** @class ClimateConverter
        Converts text-file-based data into the iLand climate data format.
        For the iLand format see the wiki (ClimateFormat). For each column (i.e. year,month, day,
        temp, prec, rad, vpd), an expression providing access to the columns of the input file calculates
        the respective output value. Propertes tableName and fileName define the input file and the
        name of the output table (located in the "climate"-database of iLand) respectively.
        */
    /** @group script
        @class ClimateConverter
        This is the Scripting related documentation for the ClimateConverter tool.
        */
    internal class ClimateConverter
    {
        private double[] mVars;
        private string mFileName;
        private string mTableName;
        private string mDatabase;
        private bool mCaptions;

        private string mYear;
        private string mMonth;
        private string mDay;
        private string mTemp;
        private string mMinTemp;
        private string mPrec;
        private string mRad;
        private string mVpd;

        private Expression mExpYear;
        private Expression mExpMonth;
        private Expression mExpDay;
        private Expression mExpTemp;
        private Expression mExpMinTemp;
        private Expression mExpPrec;
        private Expression mExpRad;
        private Expression mExpVpd;

        // getters
        public string fileName() { return mFileName; }
        public string tableName() { return mTableName; }
        public string database() { return mDatabase; }
        public bool captions() { return mCaptions; }
        public string year() { return mYear; }
        public string month() { return mMonth; }
        public string day() { return mDay; }
        public string temp() { return mTemp; }
        public string minTemp() { return mMinTemp; }
        public string prec() { return mPrec; }
        public string rad() { return mRad; }
        public string vpd() { return mVpd; }

        // setters
        public void setFileName(string fileName) { mFileName = fileName; }
        public void setTableName(string tableName) { mTableName = tableName; }
        public void setDatabase(string db) { mDatabase = db; }
        public void setCaptions(bool on) { mCaptions = on; }
        public void setYear(string value) { mYear = value; }
        public void setMonth(string value) { mMonth = value; }
        public void setDay(string value) { mDay = value; }
        public void setTemp(string value) { mTemp = value; }
        public void setMinTemp(string value) { mMinTemp = value; }
        public void setPrec(string value) { mPrec = value; }
        public void setRad(string value) { mRad = value; }
        public void setVpd(string value) { mVpd = value; }

        public static void addToScriptEngine(QJSEngine engine)
        {
            // about this kind of scripting magic see: http://qt.nokia.com/developer/faqs/faq.2007-06-25.9557303148
            //QJSValue cc_class = engine.scriptValueFromQMetaObject<ClimateConverter>();
            // the script name for the object is "ClimateConverter".
            ClimateConverter cc = new ClimateConverter();
            QJSValue cc_class = engine.newQObject(cc);
            engine.globalObject().setProperty("ClimateConverter", cc_class);
        }

        public ClimateConverter(object parent = null)
        {
            mCaptions = true;
            mVars = new double[100];

            bindExpression(mExpYear, 0);
            bindExpression(mExpMonth, 1);
            bindExpression(mExpDay, 2);

            bindExpression(mExpTemp, 3);
            bindExpression(mExpMinTemp, 4);

            bindExpression(mExpPrec, 5);
            bindExpression(mExpRad, 6);
            bindExpression(mExpVpd, 7);
        }

        private void bindExpression(Expression expr, int index)
        {
            expr.setExpression(index.ToString(CultureInfo.InvariantCulture)); // "cX" is the default expression
            for (int i = 0; i < 10; i++)
            {
                mVars[index * 10 + i] = expr.addVar(i.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void run()
        {
            mExpYear.setExpression(mYear);
            mExpMonth.setExpression(mMonth);
            mExpDay.setExpression(mDay);

            mExpTemp.setExpression(mTemp);
            mExpMinTemp.setExpression(mMinTemp);
            mExpPrec.setExpression(mPrec);
            mExpRad.setExpression(mRad);
            mExpVpd.setExpression(mVpd);

            if (String.IsNullOrEmpty(mDatabase))
            {
                throw new NotSupportedException("ClimateConverter: database is empty!");
            }
            if (String.IsNullOrWhiteSpace(mTableName))
            {
                throw new NotSupportedException("run: invalid table name.");
            }
            if (String.IsNullOrEmpty(mFileName))
            {
                Debug.WriteLine("run: empty filename.");
                return;
            }

            // load file
            CSVFile file = new CSVFile();
            file.setHasCaptions(mCaptions);
            file.loadFile(mFileName);
            if (file.rowCount() == 0)
            {
                Debug.WriteLine("run: cannot load file: " + mFileName);
                return;
            }

            SqliteConnectionStringBuilder connectionString = new SqliteConnectionStringBuilder();
            connectionString.DataSource = mDatabase;
            using SqliteConnection db = new SqliteConnection(connectionString.ConnectionString);
            db.Open();
            using (SqliteTransaction transaction = db.BeginTransaction())
            {
                // prepare output database
                SqliteCommand dropIfExists = new SqliteCommand(String.Format("drop table if exists {0}", mTableName), db, transaction);
                dropIfExists.ExecuteNonQuery();
                SqliteCommand create = new SqliteCommand(String.Format("CREATE TABLE {0} ( year INTEGER, month INTEGER, day INTEGER, " +
                                                                        "temp REAL, min_temp REAL, prec REAL, rad REAL, vpd REAL)", mTableName),
                                                         db, transaction);
                create.ExecuteNonQuery();

                // prepare insert statement
                SqliteCommand insert = new SqliteCommand(String.Format("insert into {0} (year, month, day, temp, min_temp, prec, rad, vpd) values (?,?,?, ?,?,?,?,?)", mTableName),
                                                         db, transaction);
                // do this for each row
                for (int row = 0; row < file.rowCount(); row++)
                {
                    // fetch values from input file
                    for (int col = 0; col < file.colCount(); col++)
                    {
                        double value = Double.Parse(file.value(row, col));
                        // store value in each of the expression variables
                        for (int j = 0; j < 8; j++)
                        {
                            mVars[j * 10 + col] = value; // store in the locataion mVars[x] points to.
                        }
                    }

                    // calculate new values....
                    int year = (int)mExpYear.execute();
                    int month = (int)mExpMonth.execute();
                    int day = (int)mExpDay.execute();
                    double temp = mExpTemp.execute();
                    double min_temp = mExpMinTemp.execute();
                    double prec = mExpPrec.execute();
                    double rad = mExpRad.execute();
                    double vpd = mExpVpd.execute();

                    // bind values
                    insert.Parameters[0].Value = year;
                    insert.Parameters[1].Value = month;
                    insert.Parameters[2].Value = day;
                    insert.Parameters[3].Value = temp;
                    insert.Parameters[4].Value = min_temp;
                    insert.Parameters[5].Value = prec;
                    insert.Parameters[6].Value = rad;
                    insert.Parameters[7].Value = vpd;
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            Debug.WriteLine("run: processing complete. " + file.rowCount() + " rows inserted.");
        }
    }
}
